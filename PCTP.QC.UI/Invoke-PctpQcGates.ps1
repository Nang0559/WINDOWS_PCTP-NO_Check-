<#
 Evidence runner for the six PCTP QC gates, stored per commit under artifacts\gates\<short-commit>\<Gate>\.
 Run from the repository root on Windows:

   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G1                                   # traceability matrix (needs python 3)
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2                                   # unit/regression, needs .trx
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G3 -SqlUser <user>                   # SQL integration (tempdb fixtures)
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G4 -SqlUser <user>                   # real Usp_Qrcode_Update_Stock2405, rollback-only; needs G3 PASS on the same commit
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G5 -ExePath <PCTP.exe>               # 5 UI journeys
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <PCTP.exe> -SqlUser <user> -AllowMutations -ConfirmAppUsesIsolatedDb

 Isolated mode (what CI uses for G5/G6; G3/G4 run in the sql-engine-integration job on the SQL Server container):
   .\PCTP.QC.UI\Start-PctpIsolatedSql.ps1 -BundleDirectory <bundle>      # sets PCTP_QC_SQL_CONNECTION (local PCTP_QC_* DB)
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G5 -ExePath <PCTP.exe> -Isolated
   .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <PCTP.exe> -Isolated -AllowMutations
 -Isolated blocks DB57 / production / IFS egress for this run, forces PCTP.exe onto the fixture database
 (QcSqlOverride) and needs no SQL user or password. Journeys that are not configured return BLOCKED, never PASS.

 A gate is reported PASS only when its documented condition is proven by the logs of THIS run.
 SQL password: taken from PCTP_QC_SQL_PASSWORD if set, otherwise prompted. It is handed to child
 processes through process-scoped environment variables only (never on a command line, never on disk).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('G1', 'G2', 'G3', 'G4', 'G5', 'G6')][string]$Gate,
    [string]$SqlUser,
    [string]$Configuration = 'Release',
    # G5 / G6
    [string]$ExePath,
    [string]$ExpectedWindowTitleRegex = 'PCTP|FCC|Auto Ware',
    # G4: pin the dedicated pending delivery row (TMPPHIEUGIAOHANG.STT) used by the acceptance scenario.
    [int]$DeliveryAcceptanceStt = 0,
    # G6
    [switch]$AllowMutations,
    [switch]$ConfirmAppUsesIsolatedDb,
    # Explicit live acceptance mode against the agreed SQL57 test database.
    [switch]$LiveDb57,
    # Isolated CI mode against a disposable local fixture.
    [switch]$Isolated,
    [string]$SqlServer = '192.168.200.57',
    [string]$SqlDatabase = 'B7R2_FCC'
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
$counts = $null
$exit = 0
$status = 'FAIL'
$notes = New-Object System.Collections.Generic.List[string]
$scope = $null

# CI must never take the DB57 path: G3/G4 are covered by the SQL Server container job, G5/G6 need -Isolated.
if ($env:GITHUB_ACTIONS -eq 'true') {
    if ($Gate -in @('G3', 'G4')) { Write-Host "FAIL: $Gate in CI runs in the sql-engine-integration job (SQL Server container), not through this script against DB57."; exit 1 }
    if ($Gate -in @('G5', 'G6') -and -not ($Isolated -or $LiveDb57)) { Write-Host "FAIL: $Gate in CI requires -Isolated or explicit -LiveDb57 acceptance mode."; exit 1 }
}
$isolationActive = $false
$isolationScript = Join-Path $PSScriptRoot 'Test-PctpIsolation.ps1'

function Enter-Isolation {
    . (Join-Path $PSScriptRoot 'PctpSql.Common.ps1')
    $env:PCTP_QC_REQUIRE_ISOLATED = '1'
    $env:PCTP_QC_ISOLATED_FIXTURE = '1'
    $iso = Get-PctpIsolatedConnection            # throws unless a local PCTP_QC_* fixture (never DB57/DB14)
    $script:SqlServer = $iso.Server
    $script:SqlDatabase = $iso.Database
    $programs = @($ExePath)
    Invoke-Native { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $isolationScript -Mode Block -ProgramPath $programs } (Join-Path $out 'isolation.log')
    if ($LASTEXITCODE -eq 3) { throw 'BLOCKED: cannot enforce network isolation here (needs an elevated session with Windows Firewall).' }
    if ($LASTEXITCODE -ne 0) { throw 'Isolation check failed: DB57 / production / IFS is reachable (see isolation.log).' }
    $script:isolationActive = $true
    $script:scope = "Isolated fixture $($iso.Server)/$($iso.Database); DB57/DB14/IFS egress blocked and asserted for this run."
}

function Assert-LiveDb57Target {
    if ($SqlServer.Trim() -ne '192.168.200.57' -or $SqlDatabase.Trim() -ne 'B7R2_FCC') { throw 'Live DB57 acceptance is pinned to 192.168.200.57/B7R2_FCC.' }
    if (-not $ExePath -or -not (Test-Path -LiteralPath ($ExePath + '.config') -PathType Leaf)) { throw 'Live DB57 acceptance requires PCTP.exe.config.' }
    $cfgText = Get-Content -LiteralPath ($ExePath + '.config') -Raw
    if ($cfgText -match '192\.168\.200\.14|BRAVO') { throw 'PCTP.exe.config points to SQL14/BRAVO; refusing production.' }
    if ($cfgText -notmatch '192\.168\.200\.57') { throw 'PCTP.exe.config must explicitly target SQL57 (192.168.200.57).' }
    $env:PCTP_QC_LIVE_MODE = '1'
}

function Set-SqlEnvironment {
    if (-not $SqlUser) { throw "$Gate needs -SqlUser. The password is read from PCTP_QC_SQL_PASSWORD or prompted, and is not written anywhere." }
    $plain = $env:PCTP_QC_SQL_PASSWORD
    if ([string]::IsNullOrEmpty($plain)) {
        $secure = Read-Host -Prompt "SQL password for $SqlUser" -AsSecureString
        $plain = [System.Net.NetworkCredential]::new('', $secure).Password
    }
    $env:PCTP_QC_SQL_USER = $SqlUser
    $env:PCTP_QC_SQL_PASSWORD = $plain
    if ($LiveDb57) {
        Assert-LiveDb57Target
        $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
        $b['Data Source'] = $SqlServer; $b['Initial Catalog'] = $SqlDatabase
        $b['User ID'] = $SqlUser; $b['Password'] = $plain; $b['Connect Timeout'] = 10
        $conn = New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
        try { $conn.Open(); $cmd = $conn.CreateCommand(); $cmd.CommandText = 'SELECT DB_NAME()'; if ([string]$cmd.ExecuteScalar() -ne 'B7R2_FCC') { throw 'DB57 preflight connected to a database other than B7R2_FCC.' }; Write-Host 'PASS: SQL57/B7R2_FCC connection preflight.' } finally { $conn.Dispose() }
    }
}

function Clear-SqlEnvironment {
    Remove-Item Env:PCTP_QC_SQL_USER -ErrorAction SilentlyContinue
    Remove-Item Env:PCTP_QC_SQL_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:PCTP_QC_DEL_ACCEPTANCE_STT -ErrorAction SilentlyContinue
}

# Windows PowerShell 5.1 turns native stderr into a terminating error under $ErrorActionPreference='Stop',
# which truncated logs (e.g. G4 lost all but the first FAIL line). Run native tools with Continue and keep every line.
function Invoke-Native([scriptblock]$Command, [string]$LogPath) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Command 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath $LogPath }
    finally { $ErrorActionPreference = $prev }
}

function Read-GateSummary([string]$gateName) {
    $p = Join-Path $root "artifacts\gates\$short\$gateName\gate-summary.json"
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { return $null }
    try { return (Get-Content -LiteralPath $p -Raw | ConvertFrom-Json) } catch { return $null }
}

try {
    switch ($Gate) {
        'G1' {
            $py = $null; $pyArgs = @()
            foreach ($cand in @('py', 'python', 'python3')) {
                $cmd = Get-Command $cand -ErrorAction SilentlyContinue
                if ($cmd) { $py = $cmd.Source; if ($cand -eq 'py') { $pyArgs = @('-3') }; break }
            }
            if (-not $py) { throw 'G1 needs Python 3 on PATH (py / python / python3) to run Build-TraceabilityMatrix.py.' }
            $script = Join-Path $root 'PCTP.QC.UI\Build-TraceabilityMatrix.py'
            Invoke-Native { & $py @pyArgs $script --out $out --docs (Join-Path $root 'docs') } $log
            $exit = $LASTEXITCODE
            $g1 = Join-Path $out 'gate-summary.json'
            if (Test-Path -LiteralPath $g1) { $counts = (Get-Content -LiteralPath $g1 -Raw | ConvertFrom-Json).Counts }
            # PASS only with zero GAP: every row has code file, SP/SQL, transaction boundary and test.
            $status = if ($exit -eq 0 -and $counts -and $counts.codeRows -gt 0 -and $counts.gaps -eq 0) { 'PASS' } else { 'FAIL' }
            if ($status -eq 'FAIL') { $notes.Add("See $out\gaps.csv for every GAP (one line per missing column).") }
        }

        'G2' {
            Invoke-Native { & dotnet test (Join-Path $root 'PCTP.QC.Tests\PCTP.QC.Tests.csproj') --configuration $Configuration --logger "trx;LogFileName=pctp-qc-tests.trx" --results-directory $out } $log
            $exit = $LASTEXITCODE
            $text = Get-Content -LiteralPath $log -Raw
            $m = [regex]::Match($text, '(?i)(Passed!|Failed!)\s*-\s*Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)')
            $counts = if ($m.Success) { [pscustomobject]@{ Failed = [int]$m.Groups[2].Value; Passed = [int]$m.Groups[3].Value; Skipped = [int]$m.Groups[4].Value; Total = [int]$m.Groups[5].Value } } else { $null }
            $trx = @(Get-ChildItem -LiteralPath $out -Filter '*.trx' -File -ErrorAction SilentlyContinue)
            $status = if ($exit -eq 0 -and $counts -and $counts.Total -gt 0 -and $counts.Failed -eq 0 -and $trx.Count -gt 0) { 'PASS' } else { 'FAIL' }
        }

        'G3' {
            if ($Isolated) { throw 'G3/G4 isolated runs are the sql-engine-integration CI job (SQL Server container).' }
            Set-SqlEnvironment
            $proj = Join-Path $root 'PCTP.QC.SqlIntegration\PCTP.QC.SqlIntegration.csproj'
            # Credentials reach the runner through PCTP_QC_SQL_USER / PCTP_QC_SQL_PASSWORD (Program.GetOption fallback).
            Invoke-Native { & dotnet run --project $proj --configuration $Configuration -- --preflight } (Join-Path $out 'G3-preflight.log')
            $preExit = $LASTEXITCODE
            Invoke-Native { & dotnet run --project $proj --configuration $Configuration } $log
            $exit = $LASTEXITCODE
            # Schema PASS alone is not Gate G3: require the full run to succeed too.
            $status = if ($preExit -eq 0 -and $exit -eq 0) { 'PASS' } else { 'FAIL' }
            $scope = 'tempdb / session-local fixtures on the fixed test server; no dbo business row is written.'
        }

        'G4' {
            if ($Isolated) { throw 'G4 isolated runs are the sql-engine-integration CI job (SQL Server container).' }
            # G4 = G3's rollback/duplicate/concurrency scenarios + the REAL stored procedure with SQL postconditions.
            $g3 = Read-GateSummary 'G3'
            if (-not $g3 -or $g3.Status -ne 'PASS' -or $g3.Commit -ne $commit) {
                $notes.Add("G4 requires a G3 PASS recorded for this exact commit ($short). Run -Gate G3 first.")
                throw "G3 evidence missing or not PASS for commit $short."
            }
            Set-SqlEnvironment
            if ($DeliveryAcceptanceStt -gt 0) { $env:PCTP_QC_DEL_ACCEPTANCE_STT = [string]$DeliveryAcceptanceStt }
            $proj = Join-Path $root 'PCTP.QC.SqlIntegration\PCTP.QC.SqlIntegration.csproj'
            Invoke-Native { & dotnet run --project $proj --configuration $Configuration -- --delivery-preflight } (Join-Path $out 'G4-delivery-preflight.log')
            $preExit = $LASTEXITCODE
            if ($preExit -ne 0) { $notes.Add('Delivery preflight failed: a deployed delivery procedure (Usp_Qrcode_Update_Stock2405 and/or Usp_Qrcode_Update_Stock_SP; see the FAIL lines in G4.log) is outdated. This describes the database you ran against, not the repository: the CI job sql-engine-integration deploys the procedures from the tested commit into a disposable SQL Server and re-checks the same markers. Prefer that result over deploying to DB57.') }
            Invoke-Native { & dotnet run --project $proj --configuration $Configuration -- --delivery-acceptance } $log
            $exit = $LASTEXITCODE
            $text = Get-Content -LiteralPath $log -Raw
            # The scenario prints this only after success-then-rollback, shortage-then-rollback and replay checks all passed.
            $proven = $text -match 'PASS:\s*DEL-003/DEL-004/DEL-007'
            $status = if ($preExit -eq 0 -and $exit -eq 0 -and $proven) { 'PASS' } else { 'FAIL' }
            if ($exit -eq 0 -and -not $proven) { $notes.Add('Exit code was 0 but the acceptance PASS line is absent: not counted as evidence.') }
            $scope = 'Usp_Qrcode_Update_Stock2405 executed inside rollback-only transactions on the fixed test DB (success, shortage, replay) + G3 rollback/duplicate/concurrency. PhieuKhoRepository orchestration end-to-end is NOT covered by this gate run.'
        }

        'G5' {
            if (-not $ExePath) { throw 'G5 needs -ExePath <PCTP.exe>.' }
            if ($Isolated) { Enter-Isolation }
            elseif ($LiveDb57) { Assert-LiveDb57Target }
            $uiOut = Join-Path $out 'ui'
            $suite = Join-Path $PSScriptRoot 'Invoke-PctpUiSuite.ps1'
            Invoke-Native { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $suite -ExePath $ExePath -OutputDirectory $uiOut -ExpectedWindowTitleRegex $ExpectedWindowTitleRegex -ContinueOnFailure } $log
            $exit = $LASTEXITCODE
            $reportPath = Join-Path $uiOut 'pctp-ui-suite-report.json'
            if (-not (Test-Path -LiteralPath $reportPath)) { throw "Suite wrote no report: $reportPath" }
            $suiteReport = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            $expected = @('receive-navigation', 'delivery-hvn-navigation', 'rework-navigation', 'qc-direction-navigation', 'qc-final-navigation')
            $problems = New-Object System.Collections.Generic.List[string]
            foreach ($name in $expected) {
                $r = @($suiteReport.Results | Where-Object { $_.Journey -eq $name })
                if ($r.Count -ne 1) { $problems.Add("$name : not run"); continue }
                if ($r[0].Status -ne 'PASS') { $problems.Add("$name : $($r[0].Failure)"); continue }
                $jdir = Join-Path $uiOut $name
                $jrep = Join-Path $jdir 'pctp-ui-journey-report.json'
                if (-not (Test-Path -LiteralPath $jrep)) { $problems.Add("$name : report missing"); continue }
                if ([datetime]((Get-Content -LiteralPath $jrep -Raw | ConvertFrom-Json).CompletedAt) -lt $started) { $problems.Add("$name : report is older than this run"); continue }
                $png = @(Get-ChildItem -LiteralPath $jdir -Filter '*.png' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $started })
                $tree = @(Get-ChildItem -LiteralPath $jdir -Filter '*-tree.json' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $started })
                if ($png.Count -eq 0) { $problems.Add("$name : no screenshot written by this run") }
                if ($tree.Count -eq 0) { $problems.Add("$name : no tree JSON written by this run") }
            }
            $counts = [pscustomobject]@{ JourneysPassed = $suiteReport.JourneysPassed; JourneysFailed = $suiteReport.JourneysFailed; Expected = $expected.Count }
            foreach ($p in $problems) { $notes.Add($p) }
            $status = if ($exit -eq 0 -and $problems.Count -eq 0 -and $suiteReport.JourneysPassed -eq $expected.Count) { 'PASS' } else { 'FAIL' }
            $scope = 'Read-only navigation. PASS is not proof of SQL persistence (that is G6).' + $(if ($Isolated) { ' ' + $scope } else { '' })
        }

        'G6' {
            if (-not $ExePath) { throw 'G6 needs -ExePath <PCTP.exe>.' }
            if (-not $AllowMutations) { throw 'G6 seeds and deletes data: pass -AllowMutations (isolated DB only).' }
            if ($Isolated) { Enter-Isolation } else { Set-SqlEnvironment }
            $runner = Join-Path $PSScriptRoot 'Invoke-PctpUiJourney.ps1'
            $dir = Join-Path $PSScriptRoot 'journeys\g6'
            $business = @('g6-qc-direction', 'g6-qc-final', 'g6-receive', 'g6-delivery-hvn')
            $order = @('g6-selftest') + $business
            $rows = New-Object System.Collections.Generic.List[object]
            $blocked = $false
            foreach ($name in $order) {
                $manifestPath = Join-Path $dir "$name.json"
                if (-not (Test-Path -LiteralPath $manifestPath)) { $rows.Add([pscustomobject]@{ Journey = $name; Status = 'FAIL'; Detail = 'manifest missing' }); continue }
                $m = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($m.PSObject.Properties['configured'] -and $m.configured -eq $false) {
                    $rows.Add([pscustomobject]@{ Journey = $name; Status = 'BLOCKED'; Detail = [string]$m.configurationNeeded }); $blocked = $true; continue
                }
                $jOut = Join-Path $out $name
                $env:PCTP_QC_RUN_ID = [guid]::NewGuid().ToString('N')   # unique @Prefix per journey
                $args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $runner, '-ExePath', $ExePath, '-ManifestPath', $manifestPath,
                    '-OutputDirectory', $jOut, '-ExpectedWindowTitleRegex', $ExpectedWindowTitleRegex, '-AllowMutations', '-CloseOnFinish',
                    '-SqlServer', $SqlServer, '-SqlDatabase', $SqlDatabase)
                if (($ConfirmAppUsesIsolatedDb -or $LiveDb57) -and -not $Isolated) { $args2 += '-ConfirmAppUsesIsolatedDb' }
                Invoke-Native { & powershell.exe @args2 } (Join-Path $out "$name.log")
                $jExit = $LASTEXITCODE
                Remove-Item Env:PCTP_QC_RUN_ID -ErrorAction SilentlyContinue
                $rep = Join-Path $jOut 'pctp-ui-journey-report.json'
                $detail = ''
                $ok = $false
                if (Test-Path -LiteralPath $rep) {
                    $r = Get-Content -LiteralPath $rep -Raw | ConvertFrom-Json
                    $actions = @($r.Results | Where-Object { $_.Status -eq 'PASS' } | ForEach-Object { $_.Action })
                    $hasAll = ($actions -contains 'sql-seed') -and ($actions -contains 'sql-assert') -and ($actions -contains 'sql-cleanup')
                    $ok = ($jExit -eq 0 -and $r.Status -eq 'PASS' -and $hasAll)
                    if (-not $hasAll) { $detail = 'seed + assert + cleanup were not all PASS' } elseif (-not $ok) { $detail = [string]$r.FailureSummary }
                } else { $detail = 'no report written' }
                $rows.Add([pscustomobject]@{ Journey = $name; Status = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $detail })
                # Framework self-test must pass before any business journey is trusted.
                if ($name -eq 'g6-selftest' -and -not $ok) { break }
            }
            $rows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'g6-journeys.json') -Encoding UTF8
            $rows | Format-Table -AutoSize | Out-String | Tee-Object -FilePath $log | Write-Host
            foreach ($r2 in $rows) { if ($r2.Status -ne 'PASS') { $notes.Add("$($r2.Journey): $($r2.Status) $($r2.Detail)") } }
            $counts = [pscustomobject]@{ Passed = @($rows | Where-Object Status -eq 'PASS').Count; Blocked = @($rows | Where-Object Status -eq 'BLOCKED').Count; Failed = @($rows | Where-Object Status -eq 'FAIL').Count; Required = $order.Count }
            $status = if (@($rows | Where-Object Status -ne 'PASS').Count -eq 0 -and $rows.Count -eq $order.Count) { 'PASS' } else { 'FAIL' }
            # Missing seed/assert/cleanup manifests are BLOCKED (not skipped, not PASS) unless something actually failed.
            if ($status -eq 'FAIL' -and $blocked -and @($rows | Where-Object Status -eq 'FAIL').Count -eq 0) { $status = 'BLOCKED' }
            if ($blocked) { $notes.Add('BLOCKED journeys still need their seed/assert/cleanup SQL (see journeys\g6\README.md).') }
            $exit = if ($status -eq 'PASS') { 0 } else { 1 }
            if (-not $Isolated) { $scope = "UI + SQL + cleanup on $SqlServer/$SqlDatabase; seeded rows carry the QCUI_<runId> prefix." + $(if ($LiveDb57) { " Live DB57 mode checked the app config and SQL connection." } else { "" }) }
        }
    }
}
catch {
    $status = if ($_.Exception.Message -like 'BLOCKED:*') { 'BLOCKED' } else { 'FAIL' }
    if ($exit -eq 0) { $exit = 1 }
    $notes.Add($_.Exception.Message)
    Write-Host "FAIL: $($_.Exception.Message)"
}
finally {
    Remove-Item Env:PCTP_QC_REQUIRE_ISOLATED -ErrorAction SilentlyContinue
    if ($isolationActive) { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $isolationScript -Mode Unblock | Out-Host }
    Clear-SqlEnvironment
    Remove-Item Env:PCTP_QC_RUN_ID -ErrorAction SilentlyContinue
}

$summary = [pscustomobject]@{
    Gate = $Gate; Status = $status; Commit = $commit; WorkingTreeDirty = $dirty
    StartedAt = $started.ToString('o'); CompletedAt = (Get-Date).ToString('o')
    ExitCode = $exit; Counts = $counts; Scope = $scope; Notes = @($notes.ToArray()); Log = $log
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'gate-summary.json') -Encoding UTF8
$summary | Format-List | Out-String | Write-Host
if ($dirty) { Write-Warning 'Working tree has uncommitted changes: this evidence does not match the commit hash exactly.' }
if ($status -ne 'PASS') { exit 1 }
