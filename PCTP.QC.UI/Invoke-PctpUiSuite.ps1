[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $false)][string]$OutputDirectory = ".\artifacts\pctp-ui-suite",
    [Parameter(Mandatory = $false)][string]$ExpectedWindowTitleRegex = "PCTP|FCC|Auto Ware",
    [Parameter(Mandatory = $false)][int]$StartupTimeoutSeconds = 45,
    [Parameter(Mandatory = $false)][int]$StepTimeoutSeconds = 45,
    # Run a subset, e.g. -JourneyFilter receive-navigation (name without .json; wildcards allowed).
    [Parameter(Mandatory = $false)][string]$JourneyFilter = "*",
    [Parameter(Mandatory = $false)][switch]$ContinueOnFailure,
    [Parameter(Mandatory = $false)][switch]$KeepOpenOnFailure
)
$ErrorActionPreference = "Stop"
if ($env:OS -ne "Windows_NT") { throw "PCTP UI suite must run on Windows." }
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "Executable not found: $ExePath" }

$exe = (Resolve-Path -LiteralPath $ExePath).Path
$out = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $out | Out-Null
$runner = Join-Path $PSScriptRoot "Invoke-PctpUiJourney.ps1"
$journeyRoot = Join-Path $PSScriptRoot "journeys"
$manifests = @("receive-navigation.json", "delivery-hvn-navigation.json", "rework-navigation.json", "qc-direction-navigation.json", "qc-final-navigation.json")
$manifests = @($manifests | Where-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) -like $JourneyFilter })
if ($manifests.Count -eq 0) { throw "No journey matches -JourneyFilter '$JourneyFilter'." }
$extra = @()
if ($ContinueOnFailure) { $extra += "-ContinueOnFailure" }
if ($KeepOpenOnFailure) { $extra += "-KeepOpenOnFailure" }
$results = New-Object System.Collections.Generic.List[object]
foreach ($manifestName in $manifests) {
    $manifest = Join-Path $journeyRoot $manifestName
    $name = [System.IO.Path]::GetFileNameWithoutExtension($manifestName)
    $journeyOut = Join-Path $out $name
    New-Item -ItemType Directory -Force -Path $journeyOut | Out-Null
    Write-Host "=== PCTP UI journey: $name ==="
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ExePath $exe -ManifestPath $manifest -OutputDirectory $journeyOut -ExpectedWindowTitleRegex $ExpectedWindowTitleRegex -StartupTimeoutSeconds $StartupTimeoutSeconds -StepTimeoutSeconds $StepTimeoutSeconds -CloseOnFinish @extra
    $exitCode = $LASTEXITCODE
    $reportPath = Join-Path $journeyOut "pctp-ui-journey-report.json"
    $report = $null
    if (Test-Path -LiteralPath $reportPath) { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json }
    $results.Add([pscustomobject]@{
        Journey = $name
        Status = if ($exitCode -eq 0 -and $report -and $report.Status -eq "PASS") { "PASS" } else { "FAIL" }
        ExitCode = $exitCode
        StepsPassed = if ($report) { $report.StepsPassed } else { 0 }
        StepsFailed = if ($report) { $report.StepsFailed } else { 0 }
        Report = $reportPath
        Html = (Join-Path $journeyOut "pctp-ui-journey-report.html")
        Failure = if ($report) { $report.FailureSummary } else { "No report was written (runner crashed before startup)." }
        Screenshots = @(Get-ChildItem -LiteralPath $journeyOut -Filter "*.png" -File -ErrorAction SilentlyContinue).Count
    })
}
$summary = [pscustomobject]@{
    Test = "PCTP read-only UI navigation suite"
    Executable = $exe
    Status = if (@($results | Where-Object Status -eq "FAIL").Count -eq 0) { "PASS" } else { "FAIL" }
    JourneysPassed = @($results | Where-Object Status -eq "PASS").Count
    JourneysFailed = @($results | Where-Object Status -eq "FAIL").Count
    CompletedAt = (Get-Date).ToString("o")
    Results = @($results)
}
$summaryPath = Join-Path $out "pctp-ui-suite-report.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$evidenceRunner = Join-Path $PSScriptRoot "Invoke-PctpUiEvidenceSummary.ps1"
if (Test-Path -LiteralPath $evidenceRunner) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $evidenceRunner -EvidenceDirectory $out -OutputDirectory $out
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Evidence summary reported failed journeys."
        $summary.Status = "FAIL"
    }
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$index = New-Object System.Collections.Generic.List[string]
$index.Add('<!DOCTYPE html><html><head><meta charset="utf-8"><title>PCTP UI suite</title><style>body{font-family:Segoe UI,Arial;margin:16px}td,th{border:1px solid #ccc;padding:6px}table{border-collapse:collapse}.PASS{color:#0a7a0a}.FAIL{color:#c00}</style></head><body>')
$index.Add("<h2>PCTP UI suite: <span class='$($summary.Status)'>$($summary.Status)</span></h2><table><tr><th>Journey</th><th>Status</th><th>Steps ok/fail</th><th>Screenshots</th><th>Failure</th></tr>")
foreach ($r in $results) {
    $rel = "$($r.Journey)/pctp-ui-journey-report.html"
    $index.Add("<tr><td><a href='$rel'>$($r.Journey)</a></td><td class='$($r.Status)'>$($r.Status)</td><td>$($r.StepsPassed)/$($r.StepsFailed)</td><td>$($r.Screenshots)</td><td>$([System.Net.WebUtility]::HtmlEncode([string]$r.Failure))</td></tr>")
}
$index.Add("</table></body></html>")
$index | Set-Content -LiteralPath (Join-Path $out "index.html") -Encoding UTF8
Write-Host "Open: $(Join-Path $out 'index.html')"
$summary | Format-List Test, Status, JourneysPassed, JourneysFailed, Executable | Out-String | Write-Host
Write-Host "Suite report: $summaryPath"
if ($summary.Status -ne "PASS") { exit 1 }
