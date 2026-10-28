[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $false)][string]$OutputDirectory = ".\artifacts\pctp-ui-journey",
    [Parameter(Mandatory = $false)][string]$ExpectedWindowTitleRegex = ".*",
    [Parameter(Mandatory = $false)][int]$StartupTimeoutSeconds = 45,
    [Parameter(Mandatory = $false)][int]$StepTimeoutSeconds = 20,
    [Parameter(Mandatory = $false)][switch]$AllowMutations,
    [Parameter(Mandatory = $false)][switch]$CloseOnFinish,
    # Keep running after a failed step so one run shows every UI defect, not just the first.
    [Parameter(Mandatory = $false)][switch]$ContinueOnFailure,
    # Leave PCTP open when the journey failed so the screen can be inspected by hand.
    [Parameter(Mandatory = $false)][switch]$KeepOpenOnFailure,
    # G6 (sql-seed / sql-assert / sql-cleanup). Production is always rejected by PctpSql.Common.ps1.
    [Parameter(Mandatory = $false)][string]$SqlServer = "192.168.200.57",
    [Parameter(Mandatory = $false)][string]$SqlDatabase = "B7R2_FCC",
    # The runner cannot see which DB PCTP.exe really uses; the operator confirms it points at the isolated DB.
    [Parameter(Mandatory = $false)][switch]$ConfirmAppUsesIsolatedDb
)
$ErrorActionPreference = "Stop"
# Diagnostics: a bare "Argument types do not match" gives no location. Print type, line and stack to the log
# (captured by Invoke-PctpQcGates) before rethrowing, so a failed run is diagnosable from the evidence alone.
trap {
    Write-Host ("JOURNEY-FATAL: {0}: {1}" -f $_.Exception.GetType().FullName, $_.Exception.Message)
    if ($_.InvocationInfo) { Write-Host $_.InvocationInfo.PositionMessage }
    if ($_.ScriptStackTrace) { Write-Host $_.ScriptStackTrace }
    if ($_.Exception.InnerException) { Write-Host ("INNER: {0}" -f $_.Exception.InnerException.Message) }
    break
}
if ($env:OS -ne "Windows_NT") { throw "PCTP UI journey automation must run on Windows." }
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "Executable not found: $ExePath" }
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "Manifest not found: $ManifestPath" }

. (Join-Path $PSScriptRoot "PctpUi.Common.ps1")
# Must run before any UIA bounds / screen capture so coordinates are physical pixels on scaled displays.
[void][PctpUi.Native]::SetProcessDPIAware()

$manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $manifest.steps -or $manifest.steps.Count -eq 0) { throw "Manifest must define at least one step." }
if ($manifest.PSObject.Properties["configured"] -and $manifest.configured -eq $false) { throw "Manifest is a template and is not configured yet: $($manifest.configurationNeeded)" }
$declaredMutations = [bool]$manifest.dataMutation -or @($manifest.steps | Where-Object { $_.mutatesData -eq $true }).Count -gt 0
if ($declaredMutations -and -not $AllowMutations) { throw "This manifest declares data mutations. Re-run only against disposable QC data with -AllowMutations after verifying its reset contract." }

. (Join-Path $PSScriptRoot "PctpSql.Common.ps1")
$sqlActions = @("sql-seed", "sql-assert", "sql-cleanup")
$sqlSteps = @($manifest.steps | Where-Object { $sqlActions -contains ([string]$_.action).ToLowerInvariant() })
$hasSql = $sqlSteps.Count -gt 0
if ($hasSql) {
    foreach ($ss in $sqlSteps) {
        $a = ([string]$ss.action).ToLowerInvariant()
        if (($a -eq "sql-seed" -or $a -eq "sql-cleanup") -and $ss.mutatesData -ne $true) { throw "Step '$($ss.id)': $a must declare mutatesData=true." }
    }
    if (@($sqlSteps | Where-Object { ([string]$_.action).ToLowerInvariant() -eq "sql-seed" }).Count -gt 0 -and
        @($sqlSteps | Where-Object { ([string]$_.action).ToLowerInvariant() -eq "sql-cleanup" }).Count -eq 0) {
        throw "Manifest seeds data but defines no sql-cleanup step. Every seed needs a cleanup."
    }
    if (-not $AllowMutations -and @($sqlSteps | Where-Object { $_.mutatesData -eq $true }).Count -gt 0) { throw "Manifest seeds/cleans SQL data; -AllowMutations is required (isolated DB only)." }
    Test-PctpSqlTarget $SqlServer $SqlDatabase
}
$manifestDir = Split-Path -Parent ((Resolve-Path -LiteralPath $ManifestPath).Path)
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$out = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $out | Out-Null
$testData = if ($manifest.testData) { $manifest.testData } else { New-Object psobject }
$testDataText = Format-PctpTestData $testData
$scenario = $manifest.scenario
$scenarioId = if ($scenario -and $scenario.id) { [string]$scenario.id } else { [string]$manifest.name }
$scenarioTitle = if ($scenario -and $scenario.title) { [string]$scenario.title } else { [string]$manifest.purpose }
$scenarioDescription = if ($scenario -and $scenario.description) { [string]$scenario.description } else { "" }
$stepTotal = @($manifest.steps).Count

$script:ProcName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedExe)
foreach ($p in @(Get-Process -Name $script:ProcName -ErrorAction SilentlyContinue)) { [void]$script:PreExisting.Add($p.Id) }
if ($script:PreExisting.Count -gt 0) {
    Write-Warning "Another $($script:ProcName) instance is already running (PID $(($script:PreExisting | Sort-Object) -join ',')). It is ignored; close it if startup fails."
}

function New-Banner([string]$status, [string]$stepLabel, [string]$action, [string]$windowTitle, [object]$step, [object[]]$checks, [string]$errorText) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("[$status] $($manifest.name) | $stepLabel | action=$action")
    if ($scenarioId -or $scenarioTitle) { $lines.Add("Scenario: $scenarioId - $scenarioTitle") }
    if ($scenarioDescription) { $lines.Add($scenarioDescription) }
    if ($testDataText) { $lines.Add("Test data: $testDataText") }
    if ($step -and $step.description) { $lines.Add("Step: $(Expand-PctpTokens ([string]$step.description) $testData)") }
    if ($step -and $step.expected) { $lines.Add("Expected: $(Expand-PctpTokens ([string]$step.expected) $testData)") }
    foreach ($c in @($checks)) { if ($c) { $lines.Add("[$($c.Status)] $($c.Target): $($c.Expected) | actual '$($c.Actual)'") } }
    if ($errorText) { $lines.Add("[FAIL] $errorText") }
    if ($windowTitle) { $lines.Add("Window: $windowTitle") }
    return $lines.ToArray()
}

$journeyStartedAt = (Get-Date).ToString("o")
$runId = if ($env:PCTP_QC_RUN_ID) { $env:PCTP_QC_RUN_ID } else { [guid]::NewGuid().ToString("N") }
$sqlPrefix = "QCUI_" + $runId.Substring(0, [Math]::Min(8, $runId.Length))
$cleanupDone = New-Object 'System.Collections.Generic.HashSet[string]'
if ($hasSql) {
    # Tokens usable in manifests: {{prefix}} {{runId}}
    $testData | Add-Member -NotePropertyName prefix -NotePropertyValue $sqlPrefix -Force
    $testData | Add-Member -NotePropertyName runId -NotePropertyValue $runId -Force
    $testDataText = Format-PctpTestData $testData
}
$results = New-Object System.Collections.Generic.List[object]
$failure = $null
$startup = $null
$mainWindow = $null
$script:stepIndex = 0
try {
    # ---------------- startup (always produces a screenshot + diagnostics, even when it fails) -------------
    $workDir = Split-Path -Parent $resolvedExe
    # DevExpress AccordionControl items are not exposed through UI Automation. The app-side bridge
    # (Main_APP.QcUiBridge.cs) publishes one real button per menu element, but only when this variable is set.
    # Process-scoped: restored in finally so a normal PCTP launch from this shell is never affected.
    if ($hasSql) {
        $appDbCheck = Test-PctpAppTargetsIsolatedDb $resolvedExe $SqlServer -confirmed:$ConfirmAppUsesIsolatedDb
        Write-Host "G6 guard: app DB target check = $appDbCheck; SQL target = $SqlServer/$SqlDatabase; prefix = $sqlPrefix"
        Open-PctpSql $SqlServer $SqlDatabase
    }
    $previousBridge = $env:PCTP_QC_UI_BRIDGE
    $env:PCTP_QC_UI_BRIDGE = "1"
    try { $script:Process = Start-Process -FilePath $resolvedExe -WorkingDirectory $workDir -PassThru }
    finally { if ($null -eq $previousBridge) { Remove-Item Env:PCTP_QC_UI_BRIDGE -ErrorAction SilentlyContinue } else { $env:PCTP_QC_UI_BRIDGE = $previousBridge } }
    $startedAt = Get-Date
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $startupError = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 400
        $script:Process.Refresh()
        if ($script:Process.HasExited) {
            $startupError = "Application exited during startup with code $($script:Process.ExitCode) after $([int]((Get-Date) - $startedAt).TotalSeconds)s. Run PCTP.exe by hand from '$workDir' and check config/SQL/DevExpress licence."
            break
        }
        foreach ($w in @(Get-PctpWindows)) {
            try {
                if (-not $w.Current.IsOffscreen -and $w.Current.Name -match $ExpectedWindowTitleRegex) { $mainWindow = $w; break }
            } catch { }
        }
        if ($mainWindow) { break }
    }
    $inventory = @(Get-WindowInventory)
    if (-not $mainWindow -and -not $startupError) {
        $seen = if ($inventory.Count -gt 0) { (@($inventory | ForEach-Object { "'$($_.Name)' [$($_.ClassName)] texts: $((@($_.Texts)) -join ' / ')" }) -join ' ; ') } else { "no top-level window owned by the process (MainWindowHandle=$($script:Process.MainWindowHandle))" }
        $startupError = "No visible window matching '$ExpectedWindowTitleRegex' within ${StartupTimeoutSeconds}s. Seen: $seen"
    }
    $startup = [pscustomobject]@{ Ok = (-not $startupError); Error = $startupError; WorkingDirectory = $workDir; Windows = $inventory }
    ConvertTo-Json -InputObject $startup -Depth 6 | Set-Content -LiteralPath (Join-Path $out "startup-windows.json") -Encoding UTF8

    $startShot = Save-PctpScreenshot (Join-Path $out "00-startup.png") (New-Banner $(if ($startupError) { "FAIL" } else { "PASS" }) "startup" "launch" $(if ($mainWindow) { $mainWindow.Current.Name } else { "" }) $null @() $startupError) $(if ($mainWindow) { $mainWindow } else { Get-CurrentPctpWindow })
    $results.Add([pscustomobject]@{
        Step = 0; Id = "startup"; Action = "launch"; Status = $(if ($startupError) { "FAIL" } else { "PASS" })
        Error = $startupError; ActiveWindowTitle = $(if ($mainWindow) { $mainWindow.Current.Name } else { $null })
        Screenshot = $startShot.Path; ScreenshotError = $startShot.Error
    })
    if ($startupError) { $failure = $startupError }

    # ---------------- steps ----------------
    if (-not $startupError) {
        foreach ($step in $manifest.steps) {
            $script:stepIndex++
            $label = if ($step.id) { [string]$step.id } else { "step-$script:stepIndex" }
            $action = ([string]$step.action).ToLowerInvariant()
            $errors = New-Object System.Collections.Generic.List[string]
            $checks = @()
            $foundBy = $null; $method = $null
            try {
                if ($step.mutatesData -eq $true -and -not $AllowMutations) { throw "Step '$label' is marked mutatesData; -AllowMutations is required." }
                switch ($action) {
                    "assert-window-title" {
                        $re = Expand-PctpTokens ([string]$step.value) $testData
                        $match = $null
                        foreach ($candidate in @(Get-PctpWindows)) {
                            try { if (-not $candidate.Current.IsOffscreen -and $candidate.Current.Name -match $re) { $match = $candidate; break } } catch { }
                        }
                        if (-not $match) {
                            $titles = (@(Get-PctpWindows | ForEach-Object { try { "'" + $_.Current.Name + "'" } catch { } }) -join ', ')
                            throw "Step '$label': no visible PCTP window title matched '$re'. Visible windows: $titles"
                        }
                    }
                    "wait-for-window" {
                        $re = Expand-PctpTokens ([string]$step.value) $testData
                        $timeout = if ($step.timeoutSeconds) { [int]$step.timeoutSeconds } else { $StepTimeoutSeconds }
                        $until = [DateTime]::UtcNow.AddSeconds($timeout); $hit = $null
                        do {
                            foreach ($candidate in @(Get-PctpWindows)) {
                                try { if (-not $candidate.Current.IsOffscreen -and $candidate.Current.Name -match $re) { $hit = $candidate; break } } catch { }
                            }
                            if (-not $hit) { Start-Sleep -Milliseconds 300 }
                        } while (-not $hit -and [DateTime]::UtcNow -lt $until)
                        if (-not $hit) {
                            $titles = (@(Get-PctpWindows | ForEach-Object { try { "'" + $_.Current.Name + "'" } catch { } }) -join ', ')
                            throw "Step '$label': no visible PCTP window matched '$re' within $timeout s. Visible windows: $titles"
                        }
                    }
                    "wait" {
                        $ms = [int]$step.milliseconds
                        if ($ms -lt 0 -or $ms -gt 30000) { throw "Step '$label': wait must be between 0 and 30000 ms." }
                        Start-Sleep -Milliseconds $ms
                    }
                    "dump-tree" { }
                    "sql-seed" { $checks = @(Invoke-PctpSqlStep $step $manifestDir $sqlPrefix $runId "seed"); $method = "sql-seed" }
                    "sql-assert" { $checks = @(Invoke-PctpSqlStep $step $manifestDir $sqlPrefix $runId "assert"); $method = "sql-assert" }
                    "sql-cleanup" { [void]$cleanupDone.Add([string]$step.id); $checks = @(Invoke-PctpSqlStep $step $manifestDir $sqlPrefix $runId "cleanup"); $method = "sql-cleanup" }
                    default {
                        $found = Find-PctpTarget $step
                        if (-not $found) { throw "Step '$label': UI element not found (AutomationId='$($step.automationId)', Name='$($step.name)', FallbackName='$($step.fallbackName)'). See the control-tree dump for the real AutomationIds/Names." }
                        $target = $found.Element; $foundBy = $found.FoundBy
                        switch ($action) {
                            "assert-exists" { $method = "exists" }
                            "click" {
                                if (-not $target.Current.IsEnabled) { throw "Step '$label': target is disabled." }
                                $pattern = $null
                                if ($target.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); $method = "invoke" }
                                elseif ($target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select(); $method = "select" }
                                else { Invoke-PctpMouseClick $target; $method = "mouse-fallback" }
                            }
                            "expand" {
                                $pattern = $null
                                if ($target.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
                                    if ($pattern.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Expanded) { $pattern.Expand() }
                                    $method = "expand-pattern"
                                } else { Invoke-PctpMouseClick $target; $method = "mouse-fallback" }
                            }
                            "collapse" {
                                $pattern = $null
                                if ($target.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
                                    if ($pattern.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Collapsed) { $pattern.Collapse() }
                                    $method = "collapse-pattern"
                                } else { Invoke-PctpMouseClick $target; $method = "mouse-fallback" }
                            }
                            "set-value" {
                                $pattern = $null
                                if (-not $target.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { throw "Step '$label': target does not expose ValuePattern." }
                                if ($pattern.Current.IsReadOnly) { throw "Step '$label': target value is read-only." }
                                $pattern.SetValue((Expand-PctpTokens ([string]$step.value) $testData)); $method = "value-pattern"
                            }
                            "assert-name-contains" {
                                $want = Expand-PctpTokens ([string]$step.value) $testData
                                if ($target.Current.Name -notlike ("*" + $want + "*")) { throw "Step '$label': name '$($target.Current.Name)' does not contain '$want'." }
                                $method = "name"
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
            } catch { $errors.Add($_.Exception.Message) }

            # Scenario expectations are evaluated even when the action failed, so the screenshot shows what the screen really holds.
            if ($step.expect) {
                $evaluated = New-Object System.Collections.Generic.List[object]
                foreach ($exp in @($step.expect)) {
                    try { $evaluated.Add((Test-PctpExpect $exp $testData)) }
                    catch { $evaluated.Add([pscustomobject]@{ Target = [string]$exp.automationId; Property = [string]$exp.property; Expected = "invalid expect"; Actual = $_.Exception.Message; Status = "FAIL" }) }
                }
                $checks = $evaluated.ToArray()
                foreach ($c in $checks) { if ($c.Status -eq "FAIL") { $errors.Add("Expect failed: $($c.Target) $($c.Expected) but actual '$($c.Actual)'") } }
            }

            # If PCTP died (crash / self-close) every later step fails with "element not found"; say so explicitly.
            $appExited = $false
            if ($script:Process) {
                try {
                    $script:Process.Refresh()
                    if ($script:Process.HasExited) {
                        $appExited = $true
                        $errors.Add("PCTP process exited (exit code $($script:Process.ExitCode)) during or right after this step. Check Windows Event Viewer > Windows Logs > Application ('Application Error', '.NET Runtime') at this time for the crash cause.")
                    }
                } catch { }
            }
            $status = if ($errors.Count -gt 0) { "FAIL" } else { "PASS" }
            $errorText = ($errors.ToArray() -join " ; ")
            $window = Get-CurrentPctpWindow
            $windowTitle = if ($window) { try { $window.Current.Name } catch { $null } } else { $null }
            $shotName = "{0:D2}-{1}.png" -f $script:stepIndex, ($label -replace '[^A-Za-z0-9_-]', '_')
            $banner = New-Banner $status "step $script:stepIndex/$stepTotal $label" $action $windowTitle $step $checks $errorText
            $shot = Save-PctpScreenshot (Join-Path $out $shotName) $banner $window
            $texts = if ($window) { @(Get-ElementTexts $window 12) } else { @() }
            $treePath = $null
            if ($status -eq "FAIL" -or $action -eq "dump-tree" -or $step.dump -eq $true) {
                $safeLabel = ($label -replace '[^A-Za-z0-9_-]', '_')
                # Always preserve the selected window tree, then dump every visible PCTP top-level
                # window. DevExpress workflows often open a separate form; a missing control in
                # the dashboard tree alone is not enough to diagnose the failure.
                if ($window) {
                    $treePath = Join-Path $out ("{0:D2}-{1}-tree.json" -f $script:stepIndex, $safeLabel)
                    try { [void](Export-PctpTree $window $treePath) } catch { $treePath = $null }
                }
                if ($status -eq "FAIL") {
                    $windowIndex = 0
                    foreach ($diagnosticWindow in @(Get-PctpWindows)) {
                        $windowIndex++
                        try {
                            $wc = $diagnosticWindow.Current
                            if ($wc.IsOffscreen) { continue }
                            $diagnosticPath = Join-Path $out ("{0:D2}-{1}-window-{2}-pid-{3}-tree.json" -f $script:stepIndex, $safeLabel, $windowIndex, $wc.ProcessId)
                            [void](Export-PctpTree $diagnosticWindow $diagnosticPath)
                        } catch { }
                    }
                }
            }
            $results.Add([pscustomobject]@{
                Step = $script:stepIndex; Id = $label; Action = $action; Status = $status
                Description = $(if ($step.description) { Expand-PctpTokens ([string]$step.description) $testData } else { $null }); Expected = $(if ($step.expected) { Expand-PctpTokens ([string]$step.expected) $testData } else { $null })
                AutomationId = $step.automationId; Name = $step.name; FoundBy = $foundBy; Method = $method
                Checks = @($checks); ActiveWindowTitle = $windowTitle; WindowTexts = @($texts)
                Error = $(if ($errorText) { $errorText } else { $null })
                Screenshot = $shot.Path; ScreenshotError = $shot.Error; TreeDump = $treePath
            })
            if ($status -eq "FAIL") {
                if (-not $failure) { $failure = $errorText }
                if (-not $ContinueOnFailure -or $appExited) { break }
            }
        }
    }
} catch {
    # Last-resort guard: anything unexpected still yields a report and a screenshot.
    $failure = $_.Exception.Message
    $shot = Save-PctpScreenshot (Join-Path $out "99-runner-error.png") @("[FAIL] runner error: $failure") (Get-CurrentPctpWindow)
    $results.Add([pscustomobject]@{ Step = $script:stepIndex; Id = "runner-error"; Action = "runner"; Status = "FAIL"; Error = $failure; Screenshot = $shot.Path; ScreenshotError = $shot.Error })
} finally {
    # G6: cleanup ALWAYS runs (also after a failed UI step) and must prove no seeded row remains.
    if ($hasSql -and $script:PctpSqlConnection) {
        foreach ($cs in @($sqlSteps | Where-Object { ([string]$_.action).ToLowerInvariant() -eq "sql-cleanup" })) {
            if ($cleanupDone.Contains([string]$cs.id)) { continue }
            $script:stepIndex++
            $cErr = $null; $cChecks = @()
            try { $cChecks = @(Invoke-PctpSqlStep $cs $manifestDir $sqlPrefix $runId "cleanup") } catch { $cErr = $_.Exception.Message }
            $results.Add([pscustomobject]@{
                Step = $script:stepIndex; Id = [string]$cs.id + "(finally)"; Action = "sql-cleanup"; Status = $(if ($cErr) { "FAIL" } else { "PASS" })
                Description = "Cleanup in finally (UI steps may have aborted before reaching it)"; Checks = @($cChecks); Error = $cErr
            })
            if ($cErr -and -not $failure) { $failure = $cErr }
        }
    }
    Close-PctpSql
    $failedCount = @($results | Where-Object Status -eq "FAIL").Count
    $report = [pscustomobject]@{
        Status = $(if ($failure -or $failedCount -gt 0) { "FAIL" } else { "PASS" })
        RunId = $runId
        Test = "WinForms UI journey"
        Executable = $resolvedExe
        ProcessId = $(if ($script:Process) { $script:Process.Id } else { $null })
        StartupWindowTitle = $(if ($mainWindow) { try { $mainWindow.Current.Name } catch { $null } } else { $null })
        MutationsAllowed = [bool]$AllowMutations
        Manifest = (Resolve-Path -LiteralPath $ManifestPath).Path
        ScenarioId = $scenarioId; ScenarioTitle = $scenarioTitle; ScenarioDescription = $scenarioDescription
        TestData = $testData; TestDataText = $testDataText
        Startup = $startup
        FailureSummary = $failure
        StartedAt = $journeyStartedAt
        CompletedAt = (Get-Date).ToString("o")
        StepsPassed = @($results | Where-Object { $_.Status -eq "PASS" -and $_.Step -gt 0 }).Count
        StepsFailed = $failedCount
        Results = @($results)
    }
    $reportPath = Join-Path $out "pctp-ui-journey-report.json"
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    try { New-PctpHtmlReport $report (Join-Path $out "pctp-ui-journey-report.html") } catch { Write-Warning "HTML report failed: $($_.Exception.Message)" }
    $report | Format-List Status, Test, StartupWindowTitle, StepsPassed, StepsFailed, Manifest, FailureSummary | Out-String | Write-Host
    Write-Host "Report: $reportPath"
    Write-Host "HTML:   $(Join-Path $out 'pctp-ui-journey-report.html')"

    $leaveOpen = ($report.Status -eq "FAIL" -and $KeepOpenOnFailure)
    if ($CloseOnFinish -and -not $leaveOpen -and $script:Process) {
        try {
            if (-not $script:Process.HasExited) { [void]$script:Process.CloseMainWindow(); [void]$script:Process.WaitForExit(5000) }
        } catch { }
        # Main_APP can show confirmation dialogs on close; do not leave zombie instances for the next journey.
        Update-PctpPids
        foreach ($id in @($script:Pids)) { try { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue } catch { } }
    }
}
if ($report.Status -eq "FAIL") { Write-Host "FAIL: UI journey failed: $failure"; exit 1 }
Write-Host "PASS: all manifest UI steps completed."
exit 0