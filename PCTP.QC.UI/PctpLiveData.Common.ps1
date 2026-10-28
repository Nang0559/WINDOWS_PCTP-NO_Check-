# Shared guards for the live-data test harness (dot-source only).
# Windows PowerShell 5.1 compatible. Secrets arrive as process environment variables and are never logged.
#
# Roles:
#   Source = SQL14 (192.168.200.14\BRAVO / B7R2_FCC): SELECT/metadata only, proven by a permission probe.
#   Target = SQL57 (192.168.200.57): metadata phase may read B7R2_FCC; any phase that WRITES must use a
#            dedicated test catalog (B7R2_FCC_QC*), never the shared B7R2_FCC.
Add-Type -AssemblyName System.Data

$script:LiveSourceHost = '192.168.200.14'
$script:LiveTargetHost = '192.168.200.57'
$script:LiveSharedCatalog = 'B7R2_FCC'
$script:LiveTestCatalogPattern = '^B7R2_FCC_QC[A-Za-z0-9_]*$'

function Get-PctpHostName([string]$DataSource) {
    $h = ($DataSource.Trim() -replace '^(?i)(tcp|np|lpc):', '')
    $cut = $h.IndexOfAny([char[]]@('\', ','))
    if ($cut -ge 0) { $h = $h.Substring(0, $cut) }
    return $h.Trim()
}

function Get-PctpLiveConnection {
    param(
        [Parameter(Mandatory)][string]$VariableName,
        [Parameter(Mandatory)][ValidateSet('Source', 'Target')][string]$Role,
        [string]$Catalog,
        [switch]$AllowWrites
    )
    $raw = [Environment]::GetEnvironmentVariable($VariableName, 'Process')
    if ([string]::IsNullOrWhiteSpace($raw)) { throw "Missing environment variable $VariableName." }
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($raw)
    $expectedHost = if ($Role -eq 'Source') { $script:LiveSourceHost } else { $script:LiveTargetHost }
    if ((Get-PctpHostName $b.DataSource) -ne $expectedHost) { throw "$VariableName must target host $expectedHost exactly." }
    if ($Role -eq 'Source') {
        if ($b.DataSource -notmatch '(?i)\\BRAVO$') { throw "$VariableName must target the BRAVO instance." }
        if ($b.InitialCatalog -ne $script:LiveSharedCatalog) { throw "$VariableName must target catalog $($script:LiveSharedCatalog)." }
        if ($AllowWrites) { throw 'The SQL14 source is read-only; -AllowWrites is not valid for the Source role.' }
    } else {
        if ($Catalog -and $b.InitialCatalog -ne $Catalog) { throw "$VariableName must target catalog $Catalog." }
        if ($AllowWrites -and $b.InitialCatalog -notmatch $script:LiveTestCatalogPattern) { throw "Write phases may only target a dedicated test catalog matching $($script:LiveTestCatalogPattern); '$($b.InitialCatalog)' is rejected." }
        if (-not $AllowWrites -and $b.InitialCatalog -ne $script:LiveSharedCatalog -and $b.InitialCatalog -notmatch $script:LiveTestCatalogPattern) { throw "Unexpected catalog '$($b.InitialCatalog)' on the target." }
    }
    $b['Application Name'] = if ($Role -eq 'Source') { 'PCTP-QC-LiveData-Source-ReadOnly' } else { 'PCTP-QC-LiveData-Target' }
    $b['Connect Timeout'] = 10
    if ($Role -eq 'Source' -or -not $AllowWrites) { $b['ApplicationIntent'] = 'ReadOnly' }
    return $b.ConnectionString
}

function Invoke-PctpScalarRows([string]$ConnectionString, [string]$Sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $conn.Open(); $cmd = $conn.CreateCommand(); $cmd.CommandText = $Sql; $cmd.CommandTimeout = 60
        $t = New-Object System.Data.DataTable
        [void](New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($t)
        return , $t
    } finally { $conn.Dispose() }
}

function Assert-PctpSourceReadOnly([string]$ConnectionString, [string]$Label = 'SQL14 source') {
    $sql = @"
SELECT SUSER_SNAME() AS login_name,
       CAST(ISNULL(IS_SRVROLEMEMBER('sysadmin'),0) AS int) AS is_sysadmin,
       CAST(ISNULL(IS_MEMBER('db_owner'),0) AS int) AS is_db_owner,
       CAST(ISNULL(IS_MEMBER('db_datawriter'),0) AS int) AS is_datawriter,
       CAST(ISNULL(IS_MEMBER('db_ddladmin'),0) AS int) AS is_ddladmin,
       CAST(ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','INSERT'),0) AS int) AS can_insert,
       CAST(ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','UPDATE'),0) AS int) AS can_update,
       CAST(ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','DELETE'),0) AS int) AS can_delete,
       CAST(ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','EXECUTE'),0) AS int) AS can_execute,
       CAST(ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),0) AS int) AS can_alter;
"@
    $row = (Invoke-PctpScalarRows $ConnectionString $sql).Rows[0]
    $bad = @('is_sysadmin', 'is_db_owner', 'is_datawriter', 'is_ddladmin', 'can_insert', 'can_update', 'can_delete', 'can_execute', 'can_alter') | Where-Object { [int]$row[$_] -ne 0 }
    if ($bad) { throw "$Label login is not read-only (found: $($bad -join ', ')). Use a SELECT/VIEW DEFINITION-only login." }
    return [string]$row['login_name']
}

function Assert-PctpNoStaleIsolationRules {
    if (-not (Get-Command Get-NetFirewallRule -ErrorAction SilentlyContinue)) { Write-Host 'NOTE: NetSecurity cmdlets unavailable; stale-rule check skipped.'; return }
    $rules = @(Get-NetFirewallRule -DisplayName 'PCTP-QC-ISOLATION-*' -ErrorAction SilentlyContinue)
    if ($rules.Count -gt 0) { throw "Found $($rules.Count) leftover PCTP-QC-ISOLATION firewall rule(s). Run '.\PCTP.QC.UI\Test-PctpIsolation.ps1 -Mode Unblock' from an elevated session, then retry." }
}

function New-PctpLiveRunId { return ('LD' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '_' + ([guid]::NewGuid().ToString('N').Substring(0, 6))) }

function Get-PctpScopeObjects([string]$CoverageCsv) {
    $rows = Import-Csv -LiteralPath $CoverageCsv -Encoding UTF8 | Where-Object { $_.class -like 'OK-*' -and $_.kind -in @('table', 'proc', 'view', 'function') }
    return @($rows | ForEach-Object { [pscustomobject]@{ name = $_.object.ToLowerInvariant(); kind = $_.kind } })
}
