<#
 Builds (or tears down) a disposable SQL Server database for the G5/G6 UI gates on a Windows runner.
 Windows PowerShell 5.1 compatible (uses System.Data.SqlClient, no sqlcmd / Invoke-Sqlcmd dependency).

 Why: creating a database in CI does not make the WinForms app use it. This script (1) finds a SQL engine that
 lives ON THIS MACHINE, (2) drops/creates PCTP_QC_* only, (3) deploys the schema + procedures from the exact
 commit being tested (the bundle produced by the sql-engine-integration job), (4) publishes the connection string
 as PCTP_QC_SQL_CONNECTION. PCTP.exe honours that variable (QcSqlOverride) and the gates refuse to run without it.

 Exit codes: 0 ok, 2 invalid input/safety refusal, 3 BLOCKED (no local SQL engine available), 1 other failure.

   .\Start-PctpIsolatedSql.ps1 -BundleDirectory $env:RUNNER_TEMP\bundle -EnvFile $env:GITHUB_ENV
   .\Start-PctpIsolatedSql.ps1 -Teardown
#>
[CmdletBinding()]
param(
    [string]$BundleDirectory,
    [string]$Database = 'PCTP_QC_UI',
    [string]$Server,
    [string]$LocalDbInstance = 'PCTPQC',
    [string]$EnvFile,
    [string]$UiFixtureDirectory = (Join-Path $PSScriptRoot 'fixtures\ui'),
    [string]$EvidenceDirectory = '.\artifacts\isolated-sql',
    [switch]$Teardown
)
$ErrorActionPreference = 'Stop'

function Stop-Blocked([string]$message) { Write-Host "BLOCKED: $message"; exit 3 }
function Stop-Refused([string]$message) { Write-Host "REFUSED: $message"; exit 2 }

if ($Database -notmatch '^PCTP_QC_[A-Za-z0-9_]+$') { Stop-Refused "Database '$Database' must match PCTP_QC_<name>." }

function Test-LocalServer([string]$s) {
    $h = $s.Trim() -replace '^(?i)tcp:', ''
    $cut = $h.IndexOfAny(@([char]'\', [char]','))
    if ($cut -ge 0) { $h = $h.Substring(0, $cut) }
    return (@('localhost', '127.0.0.1', '.', '(local)', '(localdb)') -contains $h.Trim().ToLowerInvariant())
}

function Resolve-LocalSqlServer {
    if ($Server) {
        if (-not (Test-LocalServer $Server)) { Stop-Refused "Server '$Server' is not local to this machine." }
        return $Server
    }
    $localDb = Get-Command 'SqlLocalDB.exe' -ErrorAction SilentlyContinue
    if ($localDb) {
        $existing = @(& SqlLocalDB.exe info) -contains $LocalDbInstance
        if (-not $existing) { & SqlLocalDB.exe create $LocalDbInstance | Out-Host }
        & SqlLocalDB.exe start $LocalDbInstance | Out-Host
        return "(localdb)\$LocalDbInstance"
    }
    $svc = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
    if ($svc) {
        if ($svc.Status -ne 'Running') { Start-Service -Name 'MSSQL$SQLEXPRESS' }
        return 'localhost\SQLEXPRESS'
    }
    return $null
}

function New-ConnString([string]$srv, [string]$db) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $srv
    $b['Initial Catalog'] = $db
    $b['Integrated Security'] = $true
    $b['Connect Timeout'] = 15
    $b['Application Name'] = 'PCTP-QC-Isolated-UI'
    return $b.ConnectionString
}

function Invoke-SqlBatches([string]$connString, [string]$sqlText, [string]$label) {
    # sqlcmd-style GO splitter: a line containing only GO (optionally with a count / comment).
    $batches = [regex]::Split($sqlText, '(?im)^[ \t]*GO[ \t]*(?:\d+)?[ \t]*(?:--.*)?\r?$')
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    $conn.Open()
    try {
        $n = 0
        foreach ($batch in $batches) {
            if ([string]::IsNullOrWhiteSpace($batch)) { continue }
            $n++
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $batch
            $cmd.CommandTimeout = 120
            try { [void]$cmd.ExecuteNonQuery() }
            catch { throw "$label batch #$n failed: $($_.Exception.Message)" }
        }
    } finally { $conn.Dispose() }
}

function Assert-FileIsIsolationSafe([string]$path) {
    $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    if ($text -match '(?im)^\s*USE\s+\[?(?!PCTP_QC_)[A-Za-z0-9_]+') { Stop-Refused "$path switches to a non-PCTP_QC_ database (USE ...)." }
    if ($text -match '192\.168\.200\.(57|14)' -or $text -match '(?i)\bBRAVO\b') { Stop-Refused "$path references DB57 / production." }
    if ($text -match '(?i)sp_addlinkedserver|OPENROWSET|OPENDATASOURCE') { Stop-Refused "$path creates cross-server access." }
}

$srv = Resolve-LocalSqlServer
if ($Teardown) {
    if (-not $srv) { Write-Host 'Teardown: no local SQL engine, nothing to drop.'; exit 0 }
    $master = New-ConnString $srv 'master'
    Invoke-SqlBatches $master "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" 'teardown'
    if ($srv -like '(localdb)*') { & SqlLocalDB.exe stop $LocalDbInstance -k | Out-Host; & SqlLocalDB.exe delete $LocalDbInstance | Out-Host }
    Write-Host "Teardown complete: $srv/$Database"
    exit 0
}

if (-not $BundleDirectory -or -not (Test-Path -LiteralPath $BundleDirectory -PathType Container)) { Stop-Refused 'BundleDirectory is required (artifact pctp-isolated-sql-bundle).' }
$orderFile = Join-Path $BundleDirectory 'deploy-order.txt'
if (-not (Test-Path -LiteralPath $orderFile -PathType Leaf)) { Stop-Refused "deploy-order.txt missing in $BundleDirectory." }
if (-not $srv) { Stop-Blocked 'No SQL engine on this machine (neither SqlLocalDB.exe nor service MSSQL$SQLEXPRESS). Install SQL Server Express/LocalDB on the runner; G5/G6 will not fall back to DB57.' }

Write-Host "Isolated SQL engine: $srv ; database: $Database"
$master = New-ConnString $srv 'master'
Invoke-SqlBatches $master "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END; CREATE DATABASE [$Database];" 'create-database'
$target = New-ConnString $srv $Database

$files = @(Get-Content -LiteralPath $orderFile -Encoding UTF8 | Where-Object { $_.Trim() -and -not $_.StartsWith('#') })
foreach ($name in $files) {
    $p = Join-Path $BundleDirectory $name.Trim()
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Bundle file missing: $name" }
    Assert-FileIsIsolationSafe $p
    Write-Host "INFO: deploying $name"
    Invoke-SqlBatches $target (Get-Content -LiteralPath $p -Raw -Encoding UTF8) $name
}
if (Test-Path -LiteralPath $UiFixtureDirectory -PathType Container) {
    foreach ($f in @(Get-ChildItem -LiteralPath $UiFixtureDirectory -Filter '*.sql' -File | Sort-Object Name)) {
        Assert-FileIsIsolationSafe $f.FullName
        Write-Host "INFO: UI fixture $($f.Name)"
        Invoke-SqlBatches $target (Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8) $f.Name
    }
}

# Fail at the deployment boundary: the objects the UI journeys and G6 assertions rely on must exist.
$verify = @"
SET NOCOUNT ON;
DECLARE @missing nvarchar(max) = N'';
SELECT @missing = @missing + t.n + N' ' FROM (VALUES (N'STOCKTP'),(N'LUUPHIEUGIAOHANG'),(N'LUUDOCQRCODE'),(N'TMPPHIEUGIAOHANG'),(N'DOCQRCODE'),(N'Slot'),(N'SlotLot')) AS t(n)
WHERE OBJECT_ID(N'dbo.' + t.n, N'U') IS NULL;
IF LEN(@missing) > 0 THROW 52101, N'Isolated schema deploy is missing tables', 1;
IF OBJECT_ID(N'dbo.vNhapTP', N'V') IS NULL THROW 52102, N'Isolated schema deploy is missing view dbo.vNhapTP', 1;
SELECT COUNT(*) AS vNhapTP_rows FROM dbo.vNhapTP;
"@
Invoke-SqlBatches $target $verify 'verify-schema'

$evidence = [System.IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
[pscustomobject]@{ Server = $srv; Database = $Database; Auth = 'Integrated'; FilesDeployed = $files; CreatedAt = (Get-Date).ToString('o') } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'isolated-sql.json') -Encoding UTF8

$env:PCTP_QC_SQL_CONNECTION = $target
$env:PCTP_QC_ISOLATED_FIXTURE = '1'
if ($EnvFile) {
    # BOM-less append: Windows PowerShell 5.1 'Add-Content -Encoding UTF8' would write a BOM into an empty GITHUB_ENV file.
    [System.IO.File]::AppendAllText($EnvFile, ("PCTP_QC_SQL_CONNECTION=$target`n" + "PCTP_QC_ISOLATED_FIXTURE=1`n"), (New-Object System.Text.UTF8Encoding($false)))
}
Write-Host "PASS: isolated database ready ($srv/$Database). No password is used (Integrated Security)."
