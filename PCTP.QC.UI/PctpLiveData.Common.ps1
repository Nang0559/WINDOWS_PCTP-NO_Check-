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

# Exact host match (a -like "*192.168.200.14*" check would also accept 192.168.200.140).
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
    }
    else {
        if ($Catalog -and $b.InitialCatalog -ne $Catalog) { throw "$VariableName must target catalog $Catalog." }
        if ($AllowWrites -and $b.InitialCatalog -notmatch $script:LiveTestCatalogPattern) {
            throw "Write phases may only target a dedicated test catalog matching $($script:LiveTestCatalogPattern); '$($b.InitialCatalog)' is rejected."
        }
        if (-not $AllowWrites -and $b.InitialCatalog -ne $script:LiveSharedCatalog -and $b.InitialCatalog -notmatch $script:LiveTestCatalogPattern) {
            throw "Unexpected catalog '$($b.InitialCatalog)' on the target."
        }
    }
    # Indexer (canonical keywords) works in Windows PowerShell 5.1 and PowerShell 7 alike.
    $b['Application Name'] = if ($Role -eq 'Source') { 'PCTP-QC-LiveData-Source-ReadOnly' } else { 'PCTP-QC-LiveData-Target' }
    $b['Connect Timeout'] = 10
    if ($Role -eq 'Source' -or -not $AllowWrites) { $b['ApplicationIntent'] = 'ReadOnly' }
    return $b.ConnectionString
}

function Invoke-PctpScalarRows([string]$ConnectionString, [string]$Sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand(); $cmd.CommandText = $Sql; $cmd.CommandTimeout = 60
        $t = New-Object System.Data.DataTable
        [void](New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($t)
        return , $t
    } finally { $conn.Dispose() }
}

# Reports what a login can do in its current database. Used by Assert-PctpSourceReadOnly (SQL14: fail closed) and, for the
# SQL57 inventory target only, by the explicit -AllowPrivilegedTarget opt-in (recorded in the manifest, never silent).
function Get-PctpLoginPrivileges([string]$ConnectionString) {
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
    $bad = @('is_sysadmin', 'is_db_owner', 'is_datawriter', 'is_ddladmin', 'can_insert', 'can_update', 'can_delete', 'can_execute', 'can_alter') |
        Where-Object { [int]$row[$_] -ne 0 }
    return [pscustomobject]@{ Login = [string]$row['login_name']; Privileges = @($bad) }
}

# Fail-closed proof that the login cannot change data or schema. Returns the login name for the manifest.
function Assert-PctpSourceReadOnly([string]$ConnectionString, [string]$Label = 'SQL14 source') {
    $p = Get-PctpLoginPrivileges $ConnectionString
    if ($p.Privileges.Count -gt 0) { throw "$Label login is not read-only (found: $($p.Privileges -join ', ')). Use a SELECT/VIEW DEFINITION-only login." }
    return $p.Login
}

# The G5/G6 isolation job adds program-scoped firewall rules for powershell.exe. If one is left behind
# (job killed before Unblock) every SQL14/SQL57 connection from this runner silently times out.
function Assert-PctpNoStaleIsolationRules {
    if (-not (Get-Command Get-NetFirewallRule -ErrorAction SilentlyContinue)) { Write-Host 'NOTE: NetSecurity cmdlets unavailable; stale-rule check skipped.'; return }
    $rules = @(Get-NetFirewallRule -DisplayName 'PCTP-QC-ISOLATION-*' -ErrorAction SilentlyContinue)
    if ($rules.Count -gt 0) {
        throw "Found $($rules.Count) leftover PCTP-QC-ISOLATION firewall rule(s). Run '.\PCTP.QC.UI\Test-PctpIsolation.ps1 -Mode Unblock' from an elevated session, then retry."
    }
}

# Quotes a SQL identifier ("]" doubled), so column names read from sys.columns can be placed in a SELECT list safely.
function ConvertTo-PctpBracket([string]$Name) {
    if ([string]::IsNullOrEmpty($Name)) { throw 'ConvertTo-PctpBracket: empty identifier.' }
    return '[' + $Name.Replace(']', ']]') + ']'
}

# Splits a list into batches of at most $Size items and returns them as ONE array of arrays (leading comma), so
# "foreach ($b in (Get-PctpInBatches $keys 800))" always iterates real arrays - also when there is a single batch
# (PowerShell would otherwise unroll it into scalars and "$b[$i]" would index characters). Empty input -> no iterations.
function Get-PctpInBatches($Items, [int]$Size = 800) {
    if ($Size -lt 1) { throw 'Get-PctpInBatches: batch size must be >= 1.' }
    $all = @($Items)
    $batches = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $all.Count; $i += $Size) {
        $end = [Math]::Min($i + $Size, $all.Count) - 1
        $batches.Add(@($all[$i..$end]))
    }
    return , $batches.ToArray()
}

function New-PctpLiveRunId { return ('LD' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '_' + ([guid]::NewGuid().ToString('N').Substring(0, 6))) }

function Get-PctpScopeObjects([string]$CoverageCsv) {
    # PCTP-only scope = objects classified OK-* in docs/SCHEMA_COVERAGE_EFFECTIVE.csv (tables, procs, views, functions).
    $rows = Import-Csv -LiteralPath $CoverageCsv -Encoding UTF8 | Where-Object { $_.class -like 'OK-*' -and $_.kind -in @('table', 'proc', 'view', 'function') }
    return @($rows | ForEach-Object { [pscustomobject]@{ name = $_.object.ToLowerInvariant(); kind = $_.kind } })
}


# ---------------------------------------------------------------- profiling helpers (pure; used by Invoke-PctpLiveDataProfile.ps1)
# Shape of a value without its content: digits -> 9, upper-case letters -> A, lower-case -> a, everything else kept.
function Get-PctpValueMask($Value, [int]$Max = 64) {
    if ($null -eq $Value) { return $null }
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in ([string]$Value).ToCharArray()) {
        if ([char]::IsDigit($ch)) { [void]$sb.Append('9') }
        elseif ([char]::IsLetter($ch)) { if ([char]::IsUpper($ch)) { [void]$sb.Append('A') } else { [void]$sb.Append('a') } }
        else { [void]$sb.Append($ch) }
        if ($sb.Length -ge $Max) { [void]$sb.Append('~'); break }
    }
    return $sb.ToString()
}

function Get-PctpKeyNormalized($Value, [string]$Mode) {
    if ($null -eq $Value -or $Value -is [System.DBNull]) { return $null }
    $v = ([string]$Value).Trim().ToUpperInvariant()
    if ($v.Length -eq 0) { return $null }
    switch ($Mode) {
        'exact' { return $v }
        'left20' { if ($v.Length -ge 20) { return $v.Substring(0, 20) } else { return $v } }
        'left13' { if ($v.Length -ge 13) { return $v.Substring(0, 13) } else { return $v } }
        default { throw "Unknown key mode '$Mode'." }
    }
}

# How many left values find a partner on the right once both sides are normalised the same way.
function Get-PctpJoinRate($LeftValues, $RightValues, [string]$Mode) {
    $right = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($r in $RightValues) { $k = Get-PctpKeyNormalized $r $Mode; if ($null -ne $k) { [void]$right.Add($k) } }
    $left = 0; $matched = 0; $matchedKeys = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($l in $LeftValues) {
        $k = Get-PctpKeyNormalized $l $Mode
        if ($null -eq $k) { continue }
        $left++
        if ($right.Contains($k)) { $matched++; [void]$matchedKeys.Add($k) }
    }
    $pct = 0.0; if ($left -gt 0) { $pct = [Math]::Round(100.0 * $matched / $left, 1) }
    return [pscustomobject]@{ left_rows = $left; matched_rows = $matched; match_pct = $pct; distinct_keys_matched = $matchedKeys.Count; right_distinct_keys = $right.Count }
}

# Column profile that never emits raw values, except short low-cardinality enum columns the caller whitelists.
function Get-PctpColumnProfile([string]$Name, [string]$TypeName, $Values, [bool]$ListEnum = $false, [int]$EnumMax = 15) {
    $vals = @($Values | Where-Object { $null -ne $_ -and $_ -isnot [System.DBNull] })
    $total = @($Values).Count
    $o = [ordered]@{ column = $Name; type = $TypeName; rows = $total; nulls = ($total - $vals.Count); distinct = 0; len_min = $null; len_max = $null; num_min = $null; num_max = $null; date_min = $null; date_max = $null; top_masks = $null; enum_values = $null }
    if ($vals.Count -eq 0) { return [pscustomobject]$o }
    $first = $vals[0]
    $distinct = @($vals | ForEach-Object { [string]$_ } | Select-Object -Unique)
    $o.distinct = $distinct.Count
    if ($first -is [string]) {
        $lens = @($vals | ForEach-Object { $_.Trim().Length } | Measure-Object -Minimum -Maximum)
        $o.len_min = $lens[0].Minimum; $o.len_max = $lens[0].Maximum
        $masks = @{}
        foreach ($v in $vals) { $m = Get-PctpValueMask ($v.Trim()); if ($masks.ContainsKey($m)) { $masks[$m]++ } else { $masks[$m] = 1 } }
        $o.top_masks = (($masks.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 5 | ForEach-Object { "$($_.Key) x$($_.Value)" }) -join ' | ')
        if ($ListEnum -and $distinct.Count -le $EnumMax) { $o.enum_values = (($vals | ForEach-Object { $_.Trim() } | Group-Object | Sort-Object Count -Descending | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ' | ') }
    }
    elseif ($first -is [datetime]) { $m = $vals | Measure-Object -Minimum -Maximum; $o.date_min = $m.Minimum.ToString('yyyy-MM-dd HH:mm'); $o.date_max = $m.Maximum.ToString('yyyy-MM-dd HH:mm') }
    elseif ($first -is [ValueType] -and $first -isnot [bool] -and $first -isnot [guid]) { $m = $vals | Measure-Object -Minimum -Maximum; $o.num_min = $m.Minimum; $o.num_max = $m.Maximum
        if ($ListEnum -and $distinct.Count -le $EnumMax) { $o.enum_values = (($vals | Group-Object | Sort-Object Count -Descending | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ' | ') } }
    elseif ($first -is [bool]) { $o.enum_values = (($vals | Group-Object | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ' | ') }
    return [pscustomobject]$o
}

# ---------------------------------------------------------------------------------------------------------------------
# Profile v2: composite-key relations, group-size distributions and quantity relations. Every helper returns counts,
# percentages and buckets only - never a business value.
# ---------------------------------------------------------------------------------------------------------------------

# "L1,L2" (a delivery/label LOT field can hold several LOTs) -> normalised elements. Blank/null -> nothing. Callers wrap in @().
function Split-PctpLotList($Value) {
    if ($null -eq $Value -or $Value -is [System.DBNull]) { return @() }
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($p in ([string]$Value).Split(',')) { $t = $p.Trim().ToUpperInvariant(); if ($t.Length -gt 0) { $out.Add($t) } }
    return $out.ToArray()
}

# LOT root = first 13 characters (shorter values stay as they are).
function Get-PctpLotRoot([string]$Lot) { if ($Lot.Length -ge 13) { return $Lot.Substring(0, 13) } else { return $Lot } }

function Get-PctpCell($Row, [string]$Column) {
    if ($Row.Table.Columns.Contains($Column) -and $Row[$Column] -isnot [System.DBNull]) { return ([string]$Row[$Column]).Trim().ToUpperInvariant() }
    return ''
}

# One entry per row: the composite keys that row can produce. Column specs: 'COL' (value as is) or 'COL:lotroots' (every LOT in
# a comma list reduced to its 13-char root; the row yields one key per root). Rows with an empty part yield no key.
function Get-PctpRowKeyLists($Table, [string[]]$Specs) {
    $result = New-Object System.Collections.Generic.List[object]
    foreach ($row in $Table.Rows) {
        $combos = @('')
        $ok = $true
        foreach ($spec in $Specs) {
            $col = $spec; $mode = ''
            if ($spec.Contains(':')) { $col = $spec.Split(':')[0]; $mode = $spec.Split(':')[1] }
            $raw = Get-PctpCell $row $col
            if ($raw.Length -eq 0) { $ok = $false; break }
            $vals = @($raw)
            if ($mode -eq 'lotroots') { $vals = @(Split-PctpLotList $raw | ForEach-Object { Get-PctpLotRoot $_ } | Select-Object -Unique) }
            if ($vals.Count -eq 0) { $ok = $false; break }
            $next = New-Object System.Collections.Generic.List[string]
            foreach ($c in $combos) { foreach ($v in $vals) { $next.Add($(if ($c.Length -eq 0) { $v } else { $c + '|' + $v })) } }
            $combos = $next.ToArray()
        }
        $result.Add($(if ($ok) { ,$combos } else { ,@() }))
    }
    return ,$result.ToArray()
}

# Left rows (each with its key list) against a right key set: row matches ANY key / ALL keys.
function Get-PctpMultiKeyJoinRate($LeftKeyLists, $RightKeyLists) {
    $right = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($kl in $RightKeyLists) { foreach ($k in @($kl)) { [void]$right.Add([string]$k) } }
    $rows = 0; $any = 0; $all = 0; $noKey = 0
    foreach ($kl in $LeftKeyLists) {
        $keys = @($kl)
        if ($keys.Count -eq 0) { $noKey++; continue }
        $rows++
        $hit = @($keys | Where-Object { $right.Contains([string]$_) }).Count
        if ($hit -gt 0) { $any++ }
        if ($hit -eq $keys.Count) { $all++ }
    }
    $anyPct = 0.0; $allPct = 0.0
    if ($rows -gt 0) { $anyPct = [Math]::Round(100.0 * $any / $rows, 1); $allPct = [Math]::Round(100.0 * $all / $rows, 1) }
    return [pscustomobject]@{ left_rows = $rows; left_rows_without_key = $noKey; any_key_match_pct = $anyPct; all_keys_match_pct = $allPct; right_distinct_keys = $right.Count }
}

# How many rows share a group key, as buckets: "1 x12 | 2 x3 | 3-5 x1 | 6-10 x0 | 11+ x0" = number of GROUPS of that size.
function Get-PctpGroupSizeBuckets($Values) {
    $g = @{}
    foreach ($v in $Values) { if ($null -eq $v -or $v -is [System.DBNull]) { continue }; $k = [string]$v; if ($g.ContainsKey($k)) { $g[$k]++ } else { $g[$k] = 1 } }
    $b = [ordered]@{ '1' = 0; '2' = 0; '3-5' = 0; '6-10' = 0; '11-50' = 0; '51+' = 0 }
    foreach ($n in $g.Values) {
        if ($n -eq 1) { $b['1']++ } elseif ($n -eq 2) { $b['2']++ } elseif ($n -le 5) { $b['3-5']++ } elseif ($n -le 10) { $b['6-10']++ } elseif ($n -le 50) { $b['11-50']++ } else { $b['51+']++ }
    }
    return ('groups=' + $g.Count + '; size ' + (($b.GetEnumerator() | ForEach-Object { "$($_.Key) x$($_.Value)" }) -join ' | '))
}

# Element-count buckets of comma lists ("1 LOT x120 | 2 LOTs x15 | ...").
function Get-PctpListLengthBuckets($Values) {
    $b = @{}
    foreach ($v in $Values) { $n = @(Split-PctpLotList $v).Count; if ($n -eq 0) { continue }; $k = $(if ($n -ge 6) { '6+' } else { [string]$n }); if ($b.ContainsKey($k)) { $b[$k]++ } else { $b[$k] = 1 } }
    return (($b.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key) LOT x$($_.Value)" }) -join ' | ')
}

# Sum a quantity per key on both sides and compare group totals (counts only).
function Compare-PctpGroupSums($LeftKeys, $LeftQty, $RightKeys, $RightQty) {
    $l = @{}; $r = @{}
    for ($i = 0; $i -lt @($LeftKeys).Count; $i++) { $k = $LeftKeys[$i]; if (-not $k) { continue }; $q = $LeftQty[$i]; if ($q -is [System.DBNull] -or $null -eq $q) { continue }; if ($l.ContainsKey($k)) { $l[$k] += [double]$q } else { $l[$k] = [double]$q } }
    for ($i = 0; $i -lt @($RightKeys).Count; $i++) { $k = $RightKeys[$i]; if (-not $k) { continue }; $q = $RightQty[$i]; if ($q -is [System.DBNull] -or $null -eq $q) { continue }; if ($r.ContainsKey($k)) { $r[$k] += [double]$q } else { $r[$k] = [double]$q } }
    $both = 0; $eq = 0; $gt = 0; $lt = 0; $lo = 0; $ro = 0
    foreach ($k in $l.Keys) { if ($r.ContainsKey($k)) { $both++; if ($l[$k] -eq $r[$k]) { $eq++ } elseif ($l[$k] -gt $r[$k]) { $gt++ } else { $lt++ } } else { $lo++ } }
    foreach ($k in $r.Keys) { if (-not $l.ContainsKey($k)) { $ro++ } }
    return [pscustomobject]@{ keys_both = $both; sums_equal = $eq; left_greater = $gt; left_smaller = $lt; left_only_keys = $lo; right_only_keys = $ro }
}

# For quantity comparison: the key of rows that produced exactly one key; $null for rows with none or several (the quantity of a
# multi-LOT row cannot be split, so those rows are counted separately instead of being guessed).
function Get-PctpSingleKeys($KeyLists) {
    $out = New-Object System.Collections.Generic.List[object]
    foreach ($kl in $KeyLists) { $k = @($kl); if ($k.Count -eq 1) { $out.Add([string]$k[0]) } else { $out.Add($null) } }
    return ,$out.ToArray()
}
