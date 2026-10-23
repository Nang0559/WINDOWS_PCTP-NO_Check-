[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $false)][string]$OutputDirectory = ".\artifacts\pctp-ui-suite",
    [Parameter(Mandatory = $false)][string]$ExpectedWindowTitleRegex = "PCTP|FCC|Auto Ware",
    [Parameter(Mandatory = $false)][int]$StartupTimeoutSeconds = 45,
    [Parameter(Mandatory = $false)][int]$StepTimeoutSeconds = 45
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
$results = New-Object System.Collections.Generic.List[object]
foreach ($manifestName in $manifests) {
    $manifest = Join-Path $journeyRoot $manifestName
    $name = [System.IO.Path]::GetFileNameWithoutExtension($manifestName)
    $journeyOut = Join-Path $out $name
    New-Item -ItemType Directory -Force -Path $journeyOut | Out-Null
    Write-Host "=== PCTP UI journey: $name ==="
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ExePath $exe -ManifestPath $manifest -OutputDirectory $journeyOut -ExpectedWindowTitleRegex $ExpectedWindowTitleRegex -StartupTimeoutSeconds $StartupTimeoutSeconds -StepTimeoutSeconds $StepTimeoutSeconds -CloseOnFinish
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
$summary | Format-List Test, Status, JourneysPassed, JourneysFailed, Executable | Out-String | Write-Host
Write-Host "Suite report: $summaryPath"
if ($summary.Status -ne "PASS") { exit 1 }
