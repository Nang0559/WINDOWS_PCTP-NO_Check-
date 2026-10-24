[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [Parameter(Mandatory = $false)][string]$OutputDirectory,
    [Parameter(Mandatory = $false)][string]$JourneyFilter = "*"
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$out = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $root } else { [System.IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Force -Path $out | Out-Null
$reports = @(Get-ChildItem -LiteralPath $root -Filter "pctp-ui-journey-report.json" -File -Recurse |
    Where-Object { [System.IO.Path]::GetFileNameWithoutExtension([string]$_.Directory.Name) -like $JourneyFilter })
if ($reports.Count -eq 0) { throw "No journey reports matching '$JourneyFilter' found under '$root'. Run Invoke-PctpUiSuite.ps1 first." }
$items = New-Object System.Collections.Generic.List[object]
foreach ($file in $reports) {
    $report = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    $relative = $file.FullName.Substring($root.Length).TrimStart([char[]]@('\','/'))
    $screenshots = @(Get-ChildItem -LiteralPath $file.DirectoryName -Filter "*.png" -File -ErrorAction SilentlyContinue)
    $failed = @($report.Results | Where-Object { $_.Status -eq "FAIL" })
    $items.Add([pscustomobject]@{
        Journey = if ($report.Manifest) { [System.IO.Path]::GetFileNameWithoutExtension([string]$report.Manifest) } else { $file.Directory.Name }
        Status = if ($report.Status -eq "PASS" -and $failed.Count -eq 0) { "PASS" } else { "FAIL" }
        StepsPassed = [int]$report.StepsPassed
        StepsFailed = [int]$report.StepsFailed
        ScreenshotCount = $screenshots.Count
        Report = $relative
        Failure = (($failed | ForEach-Object { $_.Error }) -join " ; ")
    })
}
$rows = @($items | Sort-Object Journey)
$passed = @($rows | Where-Object Status -eq "PASS").Count
$failedCount = @($rows | Where-Object Status -eq "FAIL").Count
$status = if ($failedCount -eq 0) { "PASS" } else { "FAIL" }
$csvPath = Join-Path $out "pctp-ui-evidence-summary.csv"
$mdPath = Join-Path $out "pctp-ui-evidence-summary.md"
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# PCTP UI evidence summary")
$lines.Add("")
$lines.Add("- Overall navigation status: **$status**")
$lines.Add("- Journeys: $($rows.Count) | PASS: $passed | FAIL: $failedCount")
$lines.Add("- Generated: $(Get-Date -Format o)")
$lines.Add("")
$lines.Add("| Journey | Status | Steps PASS | Steps FAIL | Screenshots | Report | Failure |")
$lines.Add("|---|---:|---:|---:|---:|---|---|")
foreach ($row in $rows) {
    $failure = ([string]$row.Failure -replace '\|','/') -replace '\r?\n',' '
    $lines.Add("| $($row.Journey) | $($row.Status) | $($row.StepsPassed) | $($row.StepsFailed) | $($row.ScreenshotCount) | $($row.Report) | $failure |")
}
$lines.Add("")
$lines.Add("> PASS means only that the recorded UI journey completed. It is not proof of SQL persistence or a committed business transaction.")
$lines | Set-Content -LiteralPath $mdPath -Encoding UTF8
Write-Host "Overall: $status"
Write-Host "Journeys: $($rows.Count); PASS: $passed; FAIL: $failedCount"
Write-Host "Markdown: $mdPath"
Write-Host "CSV: $csvPath"
if ($failedCount -gt 0) { exit 1 }
