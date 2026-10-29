<#
 Manual DB57 seed tool: creates (and later removes) the same run-scoped QC data the G6 journeys use,
 but LEAVES it in place so you can walk the real process by hand (receive screen -> scan QR -> slot -> delivery).

   # create data + print the QR strings to scan/type
   .\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Seed -ConfirmWrites -SqlUser <user>
   # print the QR strings again for an existing prefix
   .\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Qr -Prefix QCUI_ab12cd34 -SqlUser <user>
   # remove everything for that prefix and verify Remaining = 0
   .\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Cleanup -Prefix QCUI_ab12cd34 -ConfirmWrites -SqlUser <user>

 Password: PCTP_QC_SQL_PASSWORD, or prompted. Target is pinned to 192.168.200.57 / B7R2_FCC; SQL14/BRAVO is refused.
 The SQL files are shared with G6 (journeys\g6\sql\<Journey>\), so both paths create identical data.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Seed', 'Qr', 'Cleanup', 'Verify')][string]$Action,
    [string]$Journey = 'receive',
    [string]$Prefix,
    [string]$SqlUser = $env:PCTP_QC_SQL_USER,
    [string]$Server = '192.168.200.57',
    [string]$Database = 'B7R2_FCC',
    [switch]$ConfirmWrites
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpSql.Common.ps1')

if ($Server -ne '192.168.200.57' -or $Database -ne 'B7R2_FCC') { throw 'Invoke-PctpQcSeed is pinned to 192.168.200.57/B7R2_FCC.' }
if ($Action -in @('Seed', 'Cleanup') -and -not $ConfirmWrites) { throw "$Action writes to DB57: pass -ConfirmWrites." }
if ([string]::IsNullOrWhiteSpace($SqlUser)) { throw 'Pass -SqlUser (or set PCTP_QC_SQL_USER).' }
$password = $env:PCTP_QC_SQL_PASSWORD
if ([string]::IsNullOrEmpty($password)) {
    $secure = Read-Host -Prompt "SQL password for $SqlUser" -AsSecureString
    $password = [System.Net.NetworkCredential]::new('', $secure).Password
}
if ($Action -eq 'Seed' -and [string]::IsNullOrWhiteSpace($Prefix)) { $Prefix = 'QCUI_' + [guid]::NewGuid().ToString('N').Substring(0, 8) }
if ([string]::IsNullOrWhiteSpace($Prefix)) { throw "-Prefix is required for $Action (example: QCUI_ab12cd34)." }
if ($Prefix -notmatch '^QCUI_[0-9A-Za-z]{8}$') { throw 'Prefix must look like QCUI_<8 letters/digits>.' }

$dir = Join-Path $PSScriptRoot "journeys\g6\sql\$Journey"
function Read-Sql([string]$name) {
    $p = Join-Path $dir "$name.sql"
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "SQL file not found: $p" }
    return (Get-Content -LiteralPath $p -Raw -Encoding UTF8)
}

$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source'] = $Server; $b['Initial Catalog'] = $Database; $b['User ID'] = $SqlUser; $b['Password'] = $password; $b['Connect Timeout'] = 15
$conn = New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)

function New-Cmd([string]$sql, $tx) {
    $c = $conn.CreateCommand(); $c.CommandText = $sql; $c.CommandTimeout = 60
    if ($tx) { $c.Transaction = $tx }
    [void]$c.Parameters.AddWithValue('@Prefix', $Prefix)
    [void]$c.Parameters.AddWithValue('@RunId', $Prefix.Substring(5))
    return $c
}
function Invoke-Write([string]$name) {
    $sql = Read-Sql $name
    Assert-PctpMutationScoped $sql $name
    $tx = $conn.BeginTransaction()
    try { $n = (New-Cmd $sql $tx).ExecuteNonQuery(); $tx.Commit(); return $n } catch { try { $tx.Rollback() } catch { }; throw }
}
function Show-Rows([string]$name) {
    $r = (New-Cmd (Read-Sql $name) $null).ExecuteReader()
    try {
        $t = New-Object System.Data.DataTable; $t.Load($r)
        $t | Format-Table -AutoSize -Wrap | Out-String -Width 300 | Write-Host
        return $t
    } finally { if (-not $r.IsClosed) { $r.Close() } }
}

$conn.Open()
try {
    if ((New-Cmd 'SELECT DB_NAME()' $null).ExecuteScalar() -ne 'B7R2_FCC') { throw 'Connected to a database other than B7R2_FCC.' }
    switch ($Action) {
        'Seed' {
            $n = Invoke-Write 'seed'
            Write-Host "PASS: seeded prefix $Prefix ($n rows affected). Data is LEFT IN PLACE; remove it with -Action Cleanup -Prefix $Prefix."
            [void](Show-Rows 'assert')
            [void](Show-Rows 'qr')
        }
        'Qr' { [void](Show-Rows 'qr') }
        'Verify' { [void](Show-Rows 'verify') }
        'Cleanup' {
            [void](Invoke-Write 'cleanup')
            $t = Show-Rows 'verify'
            if ([int]$t.Rows[0]['Remaining'] -ne 0) { throw "Cleanup incomplete: Remaining = $($t.Rows[0]['Remaining'])." }
            Write-Host "PASS: prefix $Prefix removed, Remaining = 0."
        }
    }
} finally { $conn.Dispose() }
