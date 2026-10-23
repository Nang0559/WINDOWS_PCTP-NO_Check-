[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = ".\artifacts\pctp-ui-smoke",

    [Parameter(Mandatory = $false)]
    [string]$ExpectedWindowTitleRegex = ".*",

    [Parameter(Mandatory = $false)]
    [int]$StartupTimeoutSeconds = 45,

    [Parameter(Mandatory = $false)]
    [switch]$CloseOnSuccess
)

$ErrorActionPreference = "Stop"
if (-not $IsWindows -and $env:OS -ne "Windows_NT") {
    throw "PCTP WinForms UI smoke test must run on Windows."
}
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw "WinForms executable not found: $ExePath"
}

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$out = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $out | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$process = Start-Process -FilePath $resolvedExe -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
$window = $null

while ([DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 400
    $process.Refresh()
    if ($process.HasExited) {
        throw "PCTP exited during startup (exit code $($process.ExitCode))."
    }
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
        try {
            $candidate = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            if ($candidate -and -not [string]::IsNullOrWhiteSpace($candidate.Current.Name)) {
                $window = $candidate
                break
            }
        } catch {
            # Main window can be recreated while the WinForms shell starts.
        }
    }
}

if (-not $window) {
    throw "No accessible main window appeared within $StartupTimeoutSeconds seconds."
}

$title = $window.Current.Name
if ($title -notmatch $ExpectedWindowTitleRegex) {
    throw "Unexpected main window title '$title'; expected regex '$ExpectedWindowTitleRegex'."
}

$tree = New-Object System.Collections.Generic.List[object]
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$queue = New-Object System.Collections.Generic.Queue[object]
$queue.Enqueue([pscustomobject]@{ Element = $window; Depth = 0 })

while ($queue.Count -gt 0) {
    $entry = $queue.Dequeue()
    $element = $entry.Element
    try {
        $rect = $element.Current.BoundingRectangle
        $tree.Add([pscustomobject]@{
            Depth = $entry.Depth
            Name = $element.Current.Name
            AutomationId = $element.Current.AutomationId
            ControlType = $element.Current.ControlType.ProgrammaticName
            ClassName = $element.Current.ClassName
            FrameworkId = $element.Current.FrameworkId
            ProcessId = $element.Current.ProcessId
            Enabled = $element.Current.IsEnabled
            Offscreen = $element.Current.IsOffscreen
            IsKeyboardFocusable = $element.Current.IsKeyboardFocusable
            HasKeyboardFocus = $element.Current.HasKeyboardFocus
            X = [int]$rect.X
            Y = [int]$rect.Y
            Width = [int]$rect.Width
            Height = [int]$rect.Height
        })
        if ($entry.Depth -lt 12) {
            $child = $walker.GetFirstChild($element)
            while ($child) {
                $queue.Enqueue([pscustomobject]@{ Element = $child; Depth = $entry.Depth + 1 })
                $child = $walker.GetNextSibling($child)
            }
        }
    } catch {
        # Some WinForms custom controls do not expose every UIA property.
    }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$treePath = Join-Path $out "pctp-ui-tree-$stamp.json"
$reportPath = Join-Path $out "pctp-ui-smoke-$stamp.json"
$screenshotPath = Join-Path $out "pctp-ui-main-window-$stamp.png"

$tree | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $treePath -Encoding UTF8
$bounds = $window.Current.BoundingRectangle
if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
    throw "Main window has an invalid bounding rectangle; screenshot was not created."
}
$bitmap = New-Object System.Drawing.Bitmap ([int]$bounds.Width), ([int]$bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
    $bitmap.Save($screenshotPath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

$report = [pscustomobject]@{
    Status = "PASS"
    Test = "WinForms startup/accessibility smoke"
    Executable = $resolvedExe
    ProcessId = $process.Id
    WindowTitle = $title
    WindowHandle = $process.MainWindowHandle.ToInt64()
    AccessibleElementCount = $tree.Count
    Screenshot = $screenshotPath
    AutomationTree = $treePath
    CompletedAt = (Get-Date).ToString("o")
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | Format-List | Out-String | Write-Host

if ($CloseOnSuccess -and -not $process.HasExited) {
    $process.CloseMainWindow() | Out-Null
}

Write-Host "PASS: PCTP main window detected and screenshot/UI Automation tree saved."
Write-Host "Report: $reportPath"
