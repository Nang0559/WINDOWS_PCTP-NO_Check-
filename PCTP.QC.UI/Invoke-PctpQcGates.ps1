<#
 Evidence runner for Gate G2 (unit/regression) and G3 (SQL integration), stored per commit.
 Run from the repository root on Windows:
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G3 -SqlUser <user>      # password prompted, never stored
 Output: artifacts\gates\<short-commit>\G2\... and \G3\... plus gate-summary.json
 A gate is reported PASS only when the process exit code is 0 AND the log shows a real count.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('G2', 'G3')][string]$Gate,
    [string]$SqlUser,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$commit = (& git rev-parse HEAD).Trim()
$short = $commit.Substring(0, 7)
$dirty = [bool](& git status --porcelain)
$out = Join-Path $root "artifacts\gates\$short\$Gate"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$log = Join-Path $out "$Gate.log"
$started = Get-Date

if ($Gate -eq 'G2') {
    & dotnet test (Join-Path $root 'PCTP.QC.Tests\PCTP.QC.Tests.csproj') --configuration $Configuration `
        --logger "trx;LogFileName=pctp-qc-tests.trx" --results-directory $out 2>&1 | Tee-Object -FilePath $log
    $exit = $LASTEXITCODE
    $text = Get-Content -LiteralPath $log -Raw
    $m = [regex]::Match($text, '(?i)(Passed!|Failed!)\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)')
    $counts = if ($m.Success) { [pscustomobject]@{ Failed = [int]$m.Groups[2].Value; Passed = [int]$m.Groups[3].Value; Skipped = [int]$m.Groups[4].Value; Total = [int]$m.Groups[5].Value } } else { $null }
    $status = if ($exit -eq 0 -and $counts -and $counts.Total -gt 0 -and $counts.Failed -eq 0) { 'PASS' } else { 'FAIL' }
}
else {
    if (-not $SqlUser) { throw 'G3 needs -SqlUser. The password is prompted and not written anywhere.' }
    $secure = Read-Host -Prompt "SQL password for $SqlUser" -AsSecureString
    $pwd = [System.Net.NetworkCredential]::new('', $secure).Password
    $proj = Join-Path $root 'PCTP.QC.SqlIntegration\PCTP.QC.SqlIntegration.csproj'
    # Preflight first, then the full repository/fixture run. Both outputs are kept.
    & dotnet run --project $proj --configuration $Configuration -- --user $SqlUser --password $pwd --preflight 2>&1 |
        Tee-Object -FilePath (Join-Path $out 'G3-preflight.log')
    $preExit = $LASTEXITCODE
    & dotnet run --project $proj --configuration $Configuration -- --user $SqlUser --password $pwd 2>&1 |
        Tee-Object -FilePath $log
    $exit = $LASTEXITCODE
    $pwd = $null
    $counts = $null
    # Schema PASS alone is not Gate G3: require the full run to succeed too.
    $status = if ($preExit -eq 0 -and $exit -eq 0) { 'PASS' } else { 'FAIL' }
}

$summary = [pscustomobject]@{
    Gate = $Gate; Status = $status; Commit = $commit; WorkingTreeDirty = $dirty
    StartedAt = $started.ToString('o'); CompletedAt = (Get-Date).ToString('o')
    ExitCode = $exit; Counts = $counts; Log = $log
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'gate-summary.json') -Encoding UTF8
$summary | Format-List | Out-String | Write-Host
if ($dirty) { Write-Warning 'Working tree has uncommitted changes: this evidence does not match the commit hash exactly.' }
if ($status -ne 'PASS') { exit 1 }