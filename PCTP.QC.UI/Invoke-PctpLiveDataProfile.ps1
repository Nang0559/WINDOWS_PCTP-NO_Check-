<#
 Step 1 (SQL14, read-only): profile the newest delivery day so the synthetic fixture copies FORMAT, RELATIONS and INVARIANTS,
 never the business data itself.
   * Reads LUUPHIEUGIAOHANG / LUUDOCQRCODE for one day plus the directly related STOCKTP, TMPPHIEUGIAOHANG and DOCQRCODE rows
     into memory only (SELECT; the login is proven read-only first).
   * Artifacts contain: counts, value SHAPES (digits->9, letters->A), length/range statistics, short status enums, join match
     rates for candidate relations, and invariants. No raw part codes, LOTs, QR strings or customer values are written.
 Windows PowerShell 5.1 compatible. Secret: PCTP_QC_SOURCE_SQL_CONNECTION.
#>
[CmdletBinding()]
param(
    [datetime]$Date,                                   # default: newest NGAYGIAO in LUUPHIEUGIAOHANG that is not in the future
    [string]$OutputDirectory = (Join-Path (Get-Location).Path 'artifacts\live-data-profile'),
    [int]$MaxRowsPerTable = 100000,
    [switch]$SkipFirewallCheck
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')
if (-not $SkipFirewallCheck) { Assert-PctpNoStaleIsolationRules }
$source = Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
$login = Assert-PctpSourceReadOnly $source 'SQL14 source'
Write-Host 'PASS: SQL14 login is read-only.'

$conn = New-Object System.Data.SqlClient.SqlConnection($source); $conn.Open()
function Get-Scalar([string]$sql) { $c = $conn.CreateCommand(); $c.CommandText = $sql; $c.CommandTimeout = 60; return $c.ExecuteScalar() }
function Read-Table([string]$table, [string]$where, [hashtable]$params) {
    # Column list from metadata (rowversion/timestamp excluded); $table and $where are literals from this file only.
    $cc = $conn.CreateCommand(); $cc.CommandText = "SELECT c.name FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(N'dbo.$table') AND ty.name NOT IN ('timestamp') ORDER BY c.column_id"
    $names = New-Object System.Collections.Generic.List[string]; $r = $cc.ExecuteReader(); while ($r.Read()) { $names.Add([string]$r[0]) }; $r.Close()
    if ($names.Count -eq 0) { throw "Table dbo.$table not found on SQL14." }
    $cmd = $conn.CreateCommand(); $cmd.CommandTimeout = 300
    $cmd.CommandText = "SELECT TOP ($($MaxRowsPerTable + 1)) " + (($names | ForEach-Object { ConvertTo-PctpBracket $_ }) -join ',') + " FROM [dbo].[$table]" + $(if ($where) { " WHERE $where" } else { '' })
    foreach ($k in $params.Keys) { [void]$cmd.Parameters.AddWithValue($k, $params[$k]) }
    $dt = New-Object System.Data.DataTable; [void](New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($dt)
    if ($dt.Rows.Count -gt $MaxRowsPerTable) { throw "dbo.$table returned more than $MaxRowsPerTable rows for the window; narrow it or raise -MaxRowsPerTable." }
    return , $dt
}
function Read-TableByKeys([string]$table, [string]$keyExpr, $keys) {
    # keyExpr is a literal such as 'LOT' or 'LEFT(LOT,13)'; keys are bound as parameters in batches of 800.
    $all = New-Object System.Data.DataTable
    foreach ($batch in (Get-PctpInBatches @($keys) 800)) {
        $ph = @(); $p = @{}; for ($i = 0; $i -lt $batch.Count; $i++) { $ph += "@k$i"; $p["@k$i"] = [string]$batch[$i] }
        $part = Read-Table $table "$keyExpr IN ($($ph -join ','))" $p
        if ($all.Columns.Count -eq 0) { $all = $part.Clone() }
        foreach ($row in $part.Rows) { [void]$all.ImportRow($row) }
    }
    return , $all
}
function Col($dt, [string]$name) { if ($dt.Columns.Contains($name)) { return @($dt.Rows | ForEach-Object { $_[$name] }) } else { return @() } }

try {
    # ---- 1) which day
    if (-not $PSBoundParameters.ContainsKey('Date')) {
        $d = Get-Scalar "SELECT CAST(MAX(NGAYGIAO) AS date) FROM dbo.LUUPHIEUGIAOHANG WHERE NGAYGIAO <= GETDATE()"
        if ($d -is [System.DBNull] -or $null -eq $d) { throw 'LUUPHIEUGIAOHANG has no rows with NGAYGIAO <= now.' }
        $Date = [datetime]$d
    }
    $from = $Date.Date; $to = $from.AddDays(1); $win = @{ '@f' = $from; '@t' = $to }
    Write-Host "Profiling delivery day $($from.ToString('yyyy-MM-dd'))"
    $latestQr = Get-Scalar "SELECT CAST(MAX(NGAYXUAT) AS date) FROM dbo.LUUDOCQRCODE WHERE NGAYXUAT <= GETDATE()"

    # ---- 2) read the day (memory only)
    $luu = Read-Table 'LUUPHIEUGIAOHANG' 'NGAYGIAO >= @f AND NGAYGIAO < @t' $win
    $luuQr = Read-Table 'LUUDOCQRCODE' 'NGAYXUAT >= @f AND NGAYXUAT < @t' $win
    $tmp = Read-Table 'TMPPHIEUGIAOHANG' 'NGAYGIAO >= @f AND NGAYGIAO < @t' $win
    $docQr = Read-Table 'DOCQRCODE' $null @{}
    $lots = @((Col $luu 'LOT') + (Col $luuQr 'LOTFCC') + (Col $luuQr 'LOTHVN') | ForEach-Object { Get-PctpKeyNormalized $_ 'exact' } | Where-Object { $_ } | Select-Object -Unique)
    $lots20 = @($lots | ForEach-Object { Get-PctpKeyNormalized $_ 'left20' } | Select-Object -Unique)
    $roots13 = @($lots | ForEach-Object { Get-PctpKeyNormalized $_ 'left13' } | Select-Object -Unique)
    $stock = Read-TableByKeys 'STOCKTP' 'LOT' (@($lots) + @($lots20) | Select-Object -Unique)
    $stockRoot = Read-TableByKeys 'STOCKTP' 'LEFT(LOT,13)' $roots13
    foreach ($row in $stockRoot.Rows) { if (-not ($stock.Rows | Where-Object { $_['LOT'] -ceq $row['LOT'] })) { [void]$stock.ImportRow($row) } }
    $sets = [ordered]@{ LUUPHIEUGIAOHANG = $luu; LUUDOCQRCODE = $luuQr; TMPPHIEUGIAOHANG = $tmp; DOCQRCODE = $docQr; STOCKTP = $stock }
    foreach ($k in $sets.Keys) { Write-Host ("{0,-18} {1,8} rows" -f $k, $sets[$k].Rows.Count) }

    # ---- 3) column profiles (shapes only)
    $enumCols = @('STATUS', 'TTPHIEU', 'NHAMAY', 'KETQUA', 'DV', 'STATUSDOC', 'SATUS', 'STATUSNHAP')
    $profiles = New-Object System.Collections.Generic.List[object]
    foreach ($k in $sets.Keys) {
        $dt = $sets[$k]
        foreach ($c in $dt.Columns) {
            $pr = Get-PctpColumnProfile $c.ColumnName $c.DataType.Name (Col $dt $c.ColumnName) ($enumCols -contains $c.ColumnName.ToUpperInvariant())
            Add-Member -InputObject $pr -NotePropertyName table -NotePropertyValue $k
            $profiles.Add($pr)
        }
    }

    # ---- 4) candidate relations (match rates, no values)
    $cands = @(
        @{ n = 'LUU.STT = LUUQR.STT';            l = @($luu, 'STT');         r = @($luuQr, 'STT');       m = @('exact') },
        @{ n = 'LUU.LOT ~ LUUQR.LOTFCC';         l = @($luu, 'LOT');         r = @($luuQr, 'LOTFCC');    m = @('exact', 'left20', 'left13') },
        @{ n = 'LUU.LOT ~ LUUQR.LOTHVN';         l = @($luu, 'LOT');         r = @($luuQr, 'LOTHVN');    m = @('exact', 'left20', 'left13') },
        @{ n = 'LUU.MAHANG = LUUQR.MAHANGFCC';   l = @($luu, 'MAHANG');      r = @($luuQr, 'MAHANGFCC'); m = @('exact') },
        @{ n = 'LUU.MAHANG = LUUQR.MAHANGHVN';   l = @($luu, 'MAHANG');      r = @($luuQr, 'MAHANGHVN'); m = @('exact') },
        @{ n = 'LUUQR.LOTFCC ~ STOCKTP.LOT';     l = @($luuQr, 'LOTFCC');    r = @($stock, 'LOT');       m = @('exact', 'left20', 'left13') },
        @{ n = 'LUU.LOT ~ STOCKTP.LOT';          l = @($luu, 'LOT');         r = @($stock, 'LOT');       m = @('exact', 'left20', 'left13') },
        @{ n = 'LUU.MAHANG = STOCKTP.PART';      l = @($luu, 'MAHANG');      r = @($stock, 'PART');      m = @('exact') },
        @{ n = 'TMP.STT = LUU.STT';              l = @($tmp, 'STT');         r = @($luu, 'STT');         m = @('exact') },
        @{ n = 'TMP.LOT ~ LUU.LOT';              l = @($tmp, 'LOT');         r = @($luu, 'LOT');         m = @('exact', 'left20', 'left13') },
        @{ n = 'TMP.LOT ~ STOCKTP.LOT';          l = @($tmp, 'LOT');         r = @($stock, 'LOT');       m = @('exact', 'left20', 'left13') },
        @{ n = 'DOCQR.LOTFCC ~ LUUQR.LOTFCC';    l = @($docQr, 'LOTFCC');    r = @($luuQr, 'LOTFCC');    m = @('exact', 'left20', 'left13') }
    )
    $joins = New-Object System.Collections.Generic.List[object]
    foreach ($c in $cands) {
        foreach ($mode in $c.m) {
            $lv = Col $c.l[0] $c.l[1]; $rv = Col $c.r[0] $c.r[1]
            if ($lv.Count -eq 0 -or $rv.Count -eq 0) { $joins.Add([pscustomobject]@{ relation = $c.n; key_mode = $mode; left_rows = $lv.Count; matched_rows = 0; match_pct = 0; distinct_keys_matched = 0; right_distinct_keys = 0; note = 'empty side or column missing' }); continue }
            $jr = Get-PctpJoinRate $lv $rv $mode
            $joins.Add([pscustomobject]@{ relation = $c.n; key_mode = $mode; left_rows = $jr.left_rows; matched_rows = $jr.matched_rows; match_pct = $jr.match_pct; distinct_keys_matched = $jr.distinct_keys_matched; right_distinct_keys = $jr.right_distinct_keys; note = '' })
        }
    }

    # ---- 5) invariants (aggregates only)
    $inv = New-Object System.Collections.Generic.List[object]
    function Add-Inv([string]$name, $value) { $inv.Add([pscustomobject]@{ invariant = $name; value = $value }) }
    function Sum($vals) { $s = 0.0; foreach ($v in $vals) { if ($null -ne $v -and $v -isnot [System.DBNull]) { $s += [double]$v } }; return $s }
    Add-Inv 'day' $from.ToString('yyyy-MM-dd'); Add-Inv 'latest_LUUDOCQRCODE_NGAYXUAT_date' $(if ($latestQr -is [System.DBNull]) { '' } else { ([datetime]$latestQr).ToString('yyyy-MM-dd') })
    Add-Inv 'LUU rows' $luu.Rows.Count; Add-Inv 'LUU distinct STT' (@(Col $luu 'STT' | Select-Object -Unique).Count); Add-Inv 'LUU distinct LOT' (@(Col $luu 'LOT' | Select-Object -Unique).Count)
    Add-Inv 'LUU sum SOLUONG' (Sum (Col $luu 'SOLUONG')); Add-Inv 'LUU rows SOLUONG<=0' @(Col $luu 'SOLUONG' | Where-Object { $_ -isnot [System.DBNull] -and [double]$_ -le 0 }).Count
    Add-Inv 'LUUQR rows' $luuQr.Rows.Count; Add-Inv 'LUUQR distinct LOTFCC' (@(Col $luuQr 'LOTFCC' | Select-Object -Unique).Count); Add-Inv 'LUUQR sum SLTEMFCC' (Sum (Col $luuQr 'SLTEMFCC')); Add-Inv 'LUUQR sum SLTEMHVN' (Sum (Col $luuQr 'SLTEMHVN'))
    # per STT: delivered quantity vs scanned labels
    $byStt = @{}
    foreach ($r in $luu.Rows) { $k = [string]$r['STT']; if (-not $byStt.ContainsKey($k)) { $byStt[$k] = @(0.0, 0.0) }; if ($r['SOLUONG'] -isnot [System.DBNull]) { $byStt[$k][0] += [double]$r['SOLUONG'] } }
    foreach ($r in $luuQr.Rows) { $k = [string]$r['STT']; if ($byStt.ContainsKey($k) -and $r['SLTEMFCC'] -isnot [System.DBNull]) { $byStt[$k][1] += [double]$r['SLTEMFCC'] } }
    Add-Inv 'STT with LUU qty == LUUQR SLTEMFCC sum' @($byStt.Values | Where-Object { $_[0] -eq $_[1] }).Count
    Add-Inv 'STT with LUU qty != LUUQR SLTEMFCC sum' @($byStt.Values | Where-Object { $_[0] -ne $_[1] }).Count
    Add-Inv 'STOCKTP matched rows' $stock.Rows.Count
    Add-Inv 'STOCKTP rows SLCONLAI<0' @(Col $stock 'SLCONLAI' | Where-Object { $_ -isnot [System.DBNull] -and [double]$_ -lt 0 }).Count
    Add-Inv 'STOCKTP rows SLXUAT>0' @(Col $stock 'SLXUAT' | Where-Object { $_ -isnot [System.DBNull] -and [double]$_ -gt 0 }).Count
    Add-Inv 'TMP rows (pending that day)' $tmp.Rows.Count; Add-Inv 'DOCQRCODE rows (queue now)' $docQr.Rows.Count
} finally { $conn.Dispose() }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$profiles | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'column_profiles.csv') -NoTypeInformation -Encoding UTF8
$joins | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'join_candidates.csv') -NoTypeInformation -Encoding UTF8
$inv | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'invariants.csv') -NoTypeInformation -Encoding UTF8
$md = New-Object System.Collections.Generic.List[string]
$md.Add("# Live-data profile (SQL14, read-only) - day $($from.ToString('yyyy-MM-dd'))"); $md.Add('')
$md.Add('No raw business values are in this report: value shapes use 9 for digits and A/a for letters.'); $md.Add('')
$md.Add('## Best join per relation'); $md.Add('')
foreach ($g in ($joins | Group-Object relation)) { $b = $g.Group | Sort-Object match_pct -Descending | Select-Object -First 1; $md.Add("- $($g.Name): best key mode **$($b.key_mode)** = $($b.match_pct)% of $($b.left_rows) rows") }
$md.Add(''); $md.Add('## Invariants'); $md.Add('')
foreach ($i in $inv) { $md.Add("- $($i.invariant): $($i.value)") }
Set-Content -LiteralPath (Join-Path $OutputDirectory 'profile.md') -Value $md -Encoding UTF8
[pscustomobject]@{ mode = 'READ_ONLY_PROFILE'; day = $from.ToString('yyyy-MM-dd'); login = $login; git_sha = $env:GITHUB_SHA; run_id = $env:GITHUB_RUN_ID; captured_utc = [DateTime]::UtcNow.ToString('o'); raw_values_exported = $false; enum_columns_listed = $enumCols } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'profile-manifest.json') -Encoding UTF8
Write-Host "PASS: profile saved to $OutputDirectory (shapes, counts and match rates only)."
