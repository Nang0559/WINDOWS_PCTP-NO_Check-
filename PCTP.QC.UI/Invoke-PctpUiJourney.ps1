[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $false)][string]$OutputDirectory = ".\artifacts\pctp-ui-journey",
    [Parameter(Mandatory = $false)][string]$ExpectedWindowTitleRegex = ".*",
    [Parameter(Mandatory = $false)][int]$StartupTimeoutSeconds = 45,
    [Parameter(Mandatory = $false)][int]$StepTimeoutSeconds = 20,
    [Parameter(Mandatory = $false)][switch]$CloseOnFinish
)
$ErrorActionPreference = "Stop"
if ($env:OS -ne "Windows_NT") { throw "PCTP UI journey automation must run on Windows." }
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "Executable not found: $ExePath" }
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "Manifest not found: $ManifestPath" }

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if (-not $manifest.steps -or $manifest.steps.Count -eq 0) { throw "Manifest must define at least one step." }
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$out = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $out | Out-Null
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

function Get-ProcessWindows {
    $all = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    $found = @()
    foreach ($item in $all) {
        try {
            if ($item.Current.ProcessId -eq $process.Id -and $item.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) {
                $found += $item
            }
        } catch { }
    }
    return $found
}
$process = Start-Process -FilePath $resolvedExe -PassThru
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
$mainWindow = $null
while ([DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 350
    $process.Refresh()
    if ($process.HasExited) { throw "Application exited during startup with code $($process.ExitCode)." }
    $mainWindow = Get-ProcessWindows | Select-Object -First 1
    if ($mainWindow) { break }
}
if (-not $mainWindow) { throw "No accessible PCTP window within $StartupTimeoutSeconds seconds." }
if ($mainWindow.Current.Name -notmatch $ExpectedWindowTitleRegex) {
    throw "Unexpected startup window title '$($mainWindow.Current.Name)' (expected '$ExpectedWindowTitleRegex')."
}


function Get-CurrentWindow {
    $windows = @(Get-ProcessWindows)
    if ($windows.Count -eq 0) { return $null }
    # Prefer the newest visible owned window, otherwise retain the main window.
    $visible = @($windows | Where-Object { -not $_.Current.IsOffscreen })
    if ($visible.Count -gt 0) { return $visible[$visible.Count - 1] }
    return $windows[0]
}
function Find-Target([object]$step) {
    $property = if ($step.automationId) {
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    } elseif ($step.name) {
        [System.Windows.Automation.AutomationElement]::NameProperty
    } else { throw "Step '$($step.id)' must specify automationId or name." }
    $value = if ($step.automationId) { [string]$step.automationId } else { [string]$step.name }
    $condition = New-Object System.Windows.Automation.PropertyCondition($property, $value)
    foreach ($root in @(Get-ProcessWindows)) {
        $target = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($target) { return $target }
    }
    return $null
}
function Save-WindowScreenshot([string]$label) {
    $window = Get-CurrentWindow
    if (-not $window) { return $null }
    $safe = ($label -replace '[^A-Za-z0-9_-]', '_')
    $bounds = $window.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) { return $null }
    $path = Join-Path $out ("{0:D2}-{1}.png" -f $script:stepIndex, $safe)
    $bitmap = New-Object System.Drawing.Bitmap ([int]$bounds.Width), ([int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    return $path
}
function Wait-ForWindow([string]$regex, [int]$timeout) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do {
        foreach ($candidate in @(Get-ProcessWindows)) {
            try {
                if (-not $candidate.Current.IsOffscreen -and $candidate.Current.Name -match $regex) { return $candidate }
            } catch { }
        }
        Start-Sleep -Milliseconds 300
    } while ([DateTime]::UtcNow -lt $until)
    throw "No visible PCTP window matched title regex '$regex' within $timeout seconds."
}

$results = New-Object System.Collections.Generic.List[object]
$script:stepIndex = 0
$failure = $null
try {
    foreach ($step in $manifest.steps) {
        $script:stepIndex++
        $label = if ($step.id) { [string]$step.id } else { "step-$script:stepIndex" }
        $action = ([string]$step.action).ToLowerInvariant()
        $target = $null
        switch ($action) {
            "assert-window-title" {
                $window = Get-CurrentWindow
                if (-not $window -or $window.Current.Name -notmatch [string]$step.value) {
                    throw "Step '$label': active window title did not match '$($step.value)'."
                }
            }
            "wait-for-window" {
                $null = Wait-ForWindow ([string]$step.value) $(if ($step.timeoutSeconds) { [int]$step.timeoutSeconds } else { $StepTimeoutSeconds })
            }
            "wait" {
                $milliseconds = [int]$step.milliseconds
                if ($milliseconds -lt 0 -or $milliseconds -gt 30000) { throw "Step '$label': wait must be between 0 and 30000 ms." }
                Start-Sleep -Milliseconds $milliseconds
            }
            default {
                $target = Find-Target $step
                if (-not $target) { throw "Step '$label': UI element not found (AutomationId='$($step.automationId)', Name='$($step.name)')." }
                switch ($action) {
                    "assert-exists" { }
                    "click" {
                        if (-not $target.Current.IsEnabled) { throw "Step '$label': target is disabled." }
                        $pattern = $null
                        if (-not $target.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
                            if (-not $target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
                                throw "Step '$label': target has no Invoke/SelectionItem pattern."
                            }
                        }
                        if ($pattern -is [System.Windows.Automation.InvokePattern]) { $pattern.Invoke() } else { $pattern.Select() }
                    }
                    "set-value" {
                        $pattern = $null
                        if (-not $target.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
                            throw "Step '$label': target does not expose ValuePattern."
                        }
                        if ($pattern.Current.IsReadOnly) { throw "Step '$label': target value is read-only." }
                        $pattern.SetValue([string]$step.value)
                    }
                    "assert-name-contains" {
                        if ($target.Current.Name -notlike ("*" + [string]$step.value + "*")) {
                            throw "Step '$label': name '$($target.Current.Name)' does not contain '$($step.value)'."
                        }
                    }
                    default { throw "Step '$label': unsupported action '$action'." }
                }
            }
        }
        if ($step.waitAfterMs) {
            $delay = [int]$step.waitAfterMs
            if ($delay -lt 0 -or $delay -gt 30000) { throw "Step '$label': waitAfterMs must be between 0 and 30000." }
            Start-Sleep -Milliseconds $delay
        }
        $screenshot = Save-WindowScreenshot $label
        $window = Get-CurrentWindow
        $results.Add([pscustomobject]@{
            Step = $script:stepIndex; Id = $label; Action = $action; Status = "PASS"
            AutomationId = $step.automationId; Name = $step.name
            ActiveWindowTitle = if ($window) { $window.Current.Name } else { $null }
            Screenshot = $screenshot
        })
    }
} catch {
    $failure = $_.Exception.Message
    $results.Add([pscustomobject]@{ Step = $script:stepIndex; Status = "FAIL"; Error = $failure; Screenshot = (Save-WindowScreenshot "failure") })
} finally {
    $report = [pscustomobject]@{
        Status = if ($failure) { "FAIL" } else { "PASS" }
        Test = "WinForms UI journey"
        Executable = $resolvedExe
        ProcessId = $process.Id
        StartupWindowTitle = $mainWindow.Current.Name
        Manifest = (Resolve-Path -LiteralPath $ManifestPath).Path
        StartedAt = (Get-Date).ToString("o")
        CompletedAt = (Get-Date).ToString("o")
        StepsPassed = @($results | Where-Object Status -eq "PASS").Count
        StepsFailed = @($results | Where-Object Status -eq "FAIL").Count
        Results = @($results)
    }
    $reportPath = Join-Path $out "pctp-ui-journey-report.json"
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    $report | Format-List Status, Test, StartupWindowTitle, StepsPassed, StepsFailed, Manifest | Out-String | Write-Host
    Write-Host "Report: $reportPath"
    if ($CloseOnFinish -and -not $process.HasExited) { $process.CloseMainWindow() | Out-Null }
}
if ($failure) { throw "UI journey failed: $failure" }
Write-Host "PASS: all manifest UI steps completed."
