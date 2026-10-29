<#
Read-only DB14 fixture extractor.

Purpose:
  * Find the newest LUUPHIEUGIAOHANG delivery day.
  * Select one real delivery group (LUU.STT) that has the strongest LOT/QR/STOCKTP coverage.
  * Keep the raw source rows ONLY in the runner's temporary JSON file; never upload them as an artifact.
  * The resulting fixture is consumed by the DB57 seed step and is never written back to DB14.

Safety:
  * Source connection must be 192.168.200.14\BRAVO / B7R2_FCC.
  * Login is proven SELECT/metadata-only before any business SELECT.
  * No INSERT/UPDATE/DELETE/EXEC is issued to SQL14.
#>
[CmdletBinding()]
param(
    [string]$OutputFile = (Join-Path $env:RUNNER_TEMP 'pctp-live-order-fixture\source-fixture.json'),
    [int]$MaxCandidateGroups = 50
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')

$source = Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
[void](Assert-PctpSourceReadOnly $source 'SQL14 source')

$dir = Split-Path -Parent $OutputFile
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$conn = New-Object System.Data.SqlClient.SqlConnection($source)
$conn.Open()

function Read-Table([string]$table, [string]$where, [hashtable]$params, [int]$Top = 10000) {
    $cc = $conn.CreateCommand()
    $cc.CommandText = "SELECT c.name FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(N'dbo.$table') AND ty.name <> 'timestamp' ORDER BY c.column_id"
    $names = New-Object System.Collections.Generic.List[string]
    $r = $cc.ExecuteReader(); while ($r.Read()) { [void]$names.Add([string]$r[0]) }; $r.Close()
    if ($names.Count -eq 0) { throw "dbo.$table not found on SQL14." }
    $cmd = $conn.CreateCommand(); $cmd.CommandTimeout = 120
    $cmd.CommandText = "SELECT TOP ($Top) " + (($names | ForEach-Object { ConvertTo-PctpBracket $_ }) -join ',') + " FROM dbo.$table" + $(if ($where) { " WHERE $where" } else { '' })
    foreach ($k in $params.Keys) { [void]$cmd.Parameters.AddWithValue($k, $params[$k]) }
    $dt = New-Object System.Data.DataTable
    [void](New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($dt)
    return ,$dt
}

function Get-Cell($row, [string]$name) {
    if (-not $row.Table.Columns.Contains($name)) { return $null }
    $v = $row[$name]
    if ($v -is [System.DBNull]) { return $null }
    return [string]$v
}
function Get-Num($row, [string]$name) {
    if (-not $row.Table.Columns.Contains($name)) { return 0.0 }
    $v = $row[$name]
    if ($v -is [System.DBNull]) { return 0.0 }
    return [double]$v
}
function Root13([string]$lot) { if ([string]::IsNullOrWhiteSpace($lot)) { return $null }; $v=$lot.Trim().ToUpperInvariant(); if($v.Length -gt 13){return $v.Substring(0,13)}; return $v }
function SplitLots([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return @() }
    return @($value.Split(',') | ForEach-Object { $p=$_.Trim(); if($p){ $p } })
}

try {
    $latest = $conn.CreateCommand()
    $latest.CommandText = "SELECT CAST(MAX(NGAYGIAO) AS date) FROM dbo.LUUPHIEUGIAOHANG WHERE NGAYGIAO <= GETDATE()"
    $day = $latest.ExecuteScalar()
    if ($day -is [System.DBNull] -or $null -eq $day) { throw 'No usable LUUPHIEUGIAOHANG delivery day was found.' }
    $from = [datetime]$day; $to = $from.AddDays(1)

    $luu = Read-Table 'LUUPHIEUGIAOHANG' 'NGAYGIAO >= @f AND NGAYGIAO < @t' @{ '@f'=$from; '@t'=$to } 100000
    $qr = Read-Table 'LUUDOCQRCODE' 'NGAYXUAT >= @f AND NGAYXUAT < @t' @{ '@f'=$from; '@t'=$to } 100000

    $groups = @($luu.Rows | Group-Object { Get-Cell $_ 'STT' } | Where-Object { $_.Name -and $_.Name -ne '' } | Sort-Object Count -Descending | Select-Object -First $MaxCandidateGroups)
    if ($groups.Count -eq 0) { throw "No LUU.STT groups found for $($from.ToString('yyyy-MM-dd'))." }

    $best = $null
    foreach ($g in $groups) {
        $items = New-Object System.Collections.Generic.List[object]
        $qrMatches = 0
        $lots = New-Object System.Collections.Generic.List[string]
        foreach ($r in $g.Group) {
            $item = Get-Cell $r 'MAHANG'
            foreach ($part in (SplitLots (Get-Cell $r 'LOT'))) {
                $root = Root13 $part
                if ($root) { [void]$lots.Add($root) }
            }
            $qrMatches += @($qr.Rows | Where-Object {
                (Get-Cell $_ 'MAHANGFCC') -eq $item -or (Get-Cell $_ 'MAHANGHVN') -eq $item
            } | Where-Object {
                $qrRoot = Root13 ((Get-Cell $_ 'LOTFCC')); if(-not $qrRoot){$qrRoot=Root13 (Get-Cell $_ 'LOTHVN')}; $qrRoot -in @($lots)
            }).Count
        }
        $score = ($qrMatches * 1000) + ($g.Count * 10) + @($lots | Select-Object -Unique).Count
        if ($null -eq $best -or $score -gt $best.Score) { $best=[pscustomobject]@{ Group=$g; Score=$score; QrMatches=$qrMatches; Lots=@($lots | Select-Object -Unique) } }
    }

    $orderRows = @($best.Group.Group)
    $lotRoots = @($best.Lots)
    $fixtureRows = New-Object System.Collections.Generic.List[object]
    foreach ($r in $orderRows) {
        $item = Get-Cell $r 'MAHANG'
        $lotValue = Get-Cell $r 'LOT'
        $qty = Get-Num $r 'SOLUONG'
        $lotParts = @()
        foreach ($part in (SplitLots $lotValue)) {
            $lotParts += [pscustomobject]@{ SourceLot=$part; Root13=(Root13 $part) }
        }
        $matchedQr = @($qr.Rows | Where-Object {
            $m = Get-Cell $_ 'MAHANGFCC'; if(-not $m){$m=Get-Cell $_ 'MAHANGHVN'}
            if($m -ne $item){ return $false }
            $q = Root13 (Get-Cell $_ 'LOTFCC'); if(-not $q){$q=Root13 (Get-Cell $_ 'LOTHVN')}
            return $q -in @($lotParts.Root13)
        } | ForEach-Object {
            [pscustomobject]@{
                STT=(Get-Cell $_ 'STT'); MAHANGFCC=(Get-Cell $_ 'MAHANGFCC'); MAHANGHVN=(Get-Cell $_ 'MAHANGHVN');
                LOTFCC=(Get-Cell $_ 'LOTFCC'); LOTHVN=(Get-Cell $_ 'LOTHVN');
                SLTEMFCC=(Get-Num $_ 'SLTEMFCC'); SLTEMHVN=(Get-Num $_ 'SLTEMHVN')
            }
        })
        [void]$fixtureRows.Add([pscustomobject]@{ STT=(Get-Cell $r 'STT'); MAHANG=$item; SOLUONG=$qty; LOT=$lotValue; Lots=$lotParts; QrRows=$matchedQr })
    }

    if ($fixtureRows.Count -eq 0) { throw 'Selected delivery group produced no fixture rows.' }
    $doc = [ordered]@{
        schemaVersion=1
        source='SQL14-read-only'
        deliveryDate=$from.ToString('yyyy-MM-dd')
        selectionKey='LUUPHIEUGIAOHANG.STT'
        sourceOrderKey=(Get-Cell $orderRows[0] 'STT')
        score=$best.Score
        qrMatches=$best.QrMatches
        rows=@($fixtureRows)
    }
    $json = $doc | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($OutputFile, $json, (New-Object Text.UTF8Encoding($false)))

    Write-Host "PASS: selected DB14 delivery fixture date=$($doc.deliveryDate) orderKey=$($doc.sourceOrderKey) rows=$($fixtureRows.Count) qrMatches=$($best.QrMatches)."
    Write-Host "Fixture kept only at: $OutputFile"
    Write-Host 'No DB14 data was modified.'
} finally { $conn.Dispose() }
