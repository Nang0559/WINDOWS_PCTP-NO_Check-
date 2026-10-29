# SQL helpers for G6 journeys (dot-source only, used by Invoke-PctpUiJourney.ps1).
# Windows PowerShell 5.1 compatible. Credentials come from PCTP_QC_SQL_USER / PCTP_QC_SQL_PASSWORD
# (process-scoped, set by Invoke-PctpQcGates.ps1) and are never written to disk or to reports.
#
# Safety contract (fail-closed):
#   * Production (192.168.200.14 / BRAVO) is always rejected.
#   * Only 192.168.200.57/B7R2_FCC is accepted, unless PCTP_QC_ISOLATED_TARGET="server/database" names
#     another disposable copy explicitly. That value is never allowed to be production either.
#   * sql-seed and sql-cleanup statements MUST reference @Prefix (QCUI_<runId>), so every mutation is
#     scoped to rows this run created. TRUNCATE / DROP DATABASE / DROP TABLE on non-# tables are rejected.
#   * sql-cleanup must be followed by a verification query returning Remaining = 0.
#
# Isolated mode (PCTP_QC_REQUIRE_ISOLATED=1, set by Invoke-PctpQcGates.ps1 -Isolated and always in CI):
#   * DB57 (192.168.200.57 / B7R2_FCC) is rejected as well as production;
#   * the only accepted target is the local PCTP_QC_* database named by PCTP_QC_SQL_CONNECTION;
#   * PCTP.exe is forced onto that database by QcSqlOverride, so no operator confirmation is accepted.

$script:PctpSqlConnection = $null
$script:PctpSqlTarget = $null

$script:PctpDefaultServer = '192.168.200.57'
$script:PctpDefaultDatabase = 'B7R2_FCC'

function Test-PctpIsolatedMode { return ($env:PCTP_QC_REQUIRE_ISOLATED -eq '1') }

# Parses and validates PCTP_QC_SQL_CONNECTION. Returns @{ Server; Database; ConnectionString }; throws when unsafe.
function Get-PctpIsolatedConnection {
    $cs = $env:PCTP_QC_SQL_CONNECTION
    if ([string]::IsNullOrWhiteSpace($cs)) { throw 'Isolated mode needs PCTP_QC_SQL_CONNECTION (run Start-PctpIsolatedSql.ps1 first).' }
    if ($cs -match '192\.168\.200\.(57|14)' -or $cs -match '(?i)BRAVO' -or $cs -match '(?i)B7R2_FCC') {
        throw 'PCTP_QC_SQL_CONNECTION mentions DB57 / production / B7R2_FCC and is rejected.'
    }
    $b = New-Object System.Data.Common.DbConnectionStringBuilder
    # PowerShell treats DbConnectionStringBuilder as a dictionary: '$b.ConnectionString = x' would set a KEY, so call the setter.
    $b.set_ConnectionString($cs)
    $server = $null; $database = $null
    foreach ($k in @('data source', 'server', 'address', 'addr', 'network address')) { if ($b.ContainsKey($k)) { $server = [string]$b[$k]; break } }
    foreach ($k in @('initial catalog', 'database')) { if ($b.ContainsKey($k)) { $database = [string]$b[$k]; break } }
    if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) { throw 'PCTP_QC_SQL_CONNECTION must name both server and database.' }
    $h = $server.Trim() -replace '^(?i)tcp:', ''
    $cut = $h.IndexOfAny(@([char]'\', [char]','))
    if ($cut -ge 0) { $h = $h.Substring(0, $cut) }
    if (@('localhost', '127.0.0.1', '.', '(local)', '(localdb)') -notcontains $h.Trim().ToLowerInvariant()) { throw "Isolated server '$server' is not local to this machine." }
    if ($database -notmatch '^(?i)PCTP_QC_') { throw "Isolated database '$database' must start with PCTP_QC_." }
    return @{ Server = $server.Trim(); Database = $database.Trim(); ConnectionString = $cs }
}

function Test-PctpSqlTarget([string]$server, [string]$database) {
    if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) {
        throw 'SQL target must specify both server and database.'
    }
    if ($server -match '192\.168\.200\.14' -or $server -match 'BRAVO') {
        throw "SQL target '$server' is the production server and is always rejected."
    }
    $pair = ('{0}/{1}' -f $server.Trim(), $database.Trim())
    if (Test-PctpIsolatedMode) {
        $iso = Get-PctpIsolatedConnection
        if ($pair -ieq ('{0}/{1}' -f $iso.Server, $iso.Database)) { return }
        throw "Isolated mode: SQL target '$pair' is not the fixture '$($iso.Server)/$($iso.Database)'. DB57 is not allowed."
    }
    # Explicit live acceptance mode: DB57 is the agreed test environment and may receive run-scoped QC rows.
    # SQL14/BRAVO is rejected above; every mutation still requires @Prefix and cleanup verification.
    if ($env:PCTP_QC_LIVE_MODE -eq '1') {
        if ($server.Trim() -eq $script:PctpDefaultServer -and ($database.Trim() -ieq 'B7R2_FCC' -or $database.Trim() -match '^B7R2_FCC_QC[A-Za-z0-9_]*$')) { return }
        throw "Live-data mode: SQL target '$pair' is rejected. Only $($script:PctpDefaultServer)/B7R2_FCC or B7R2_FCC_QC* is allowed."
    }
    $default = ('{0}/{1}' -f $script:PctpDefaultServer, $script:PctpDefaultDatabase)
    if ($pair -ieq $default) { return }
    $explicit = $env:PCTP_QC_ISOLATED_TARGET
    if ($explicit -and ($pair -ieq $explicit.Trim())) { return }
    throw "SQL target '$pair' is not allowed. Default is '$default'. To use another disposable copy, set PCTP_QC_ISOLATED_TARGET='$pair' explicitly."
}

function Open-PctpSql([string]$server, [string]$database) {
    Test-PctpSqlTarget $server $database
    if (Test-PctpIsolatedMode) {
        $iso = Get-PctpIsolatedConnection
        $conn = New-Object System.Data.SqlClient.SqlConnection($iso.ConnectionString)
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = 'SET LOCK_TIMEOUT 5000;'
        [void]$cmd.ExecuteNonQuery()
        $script:PctpSqlConnection = $conn
        $script:PctpSqlTarget = ('{0}/{1}' -f $iso.Server, $iso.Database)
        return
    }
    $user = $env:PCTP_QC_SQL_USER
    $pwd = $env:PCTP_QC_SQL_PASSWORD
    if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($pwd)) {
        throw 'SQL steps need PCTP_QC_SQL_USER and PCTP_QC_SQL_PASSWORD (Invoke-PctpQcGates.ps1 sets them for the run).'
    }
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $server
    $b['Initial Catalog'] = $database
    $b['User ID'] = $user
    $b['Password'] = $pwd
    $b['Connect Timeout'] = 5
    $b['Application Name'] = 'PCTP-QC-G6'
    $conn = New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SET LOCK_TIMEOUT 5000;'
    [void]$cmd.ExecuteNonQuery()
    $script:PctpSqlConnection = $conn
    $script:PctpSqlTarget = ('{0}/{1}' -f $server, $database)
}

function Close-PctpSql {
    if ($script:PctpSqlConnection) {
        try { $script:PctpSqlConnection.Close(); $script:PctpSqlConnection.Dispose() } catch { }
    }
    $script:PctpSqlConnection = $null
}

function Test-PctpAppTargetsIsolatedDb([string]$exePath, [string]$server, [switch]$confirmed) {
    # The runner cannot see the connection the app really uses. Read the app config (best effort),
    # refuse production, and otherwise require an explicit human confirmation.
    if (Test-PctpIsolatedMode) {
        # Not best-effort and not operator-confirmed: PCTP.exe (QcSqlOverride) throws at startup unless the
        # connection from PCTP_QC_SQL_CONNECTION is a local PCTP_QC_* fixture, and every SQLPROVIDER field uses it.
        $iso = Get-PctpIsolatedConnection
        if ($server.Trim() -ine $iso.Server) { throw "Isolated mode: journey SQL server '$server' differs from the fixture '$($iso.Server)'." }
        return 'qc-override-enforced-by-app'
    }
    $cfg = $exePath + '.config'
    $text = ''
    if (Test-Path -LiteralPath $cfg -PathType Leaf) { $text = Get-Content -LiteralPath $cfg -Raw -ErrorAction SilentlyContinue }
    if ($text -match '192\.168\.200\.14' -or $text -match 'BRAVO') {
        throw "App config '$cfg' references the production server. G6 mutations are refused."
    }
    if ($text -and $text.Contains($server)) { return 'app-config-matches' }
    if ($confirmed) { return 'operator-confirmed' }
    throw "Cannot verify that PCTP.exe uses $server (config '$cfg' does not mention it). Point the app at the isolated DB, then re-run with -ConfirmAppUsesIsolatedDb."
}

function Get-PctpStepSql($step, [string]$manifestDir) {
    if ($step.sqlFile) {
        $p = Join-Path $manifestDir ([string]$step.sqlFile)
        if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Step '$($step.id)': sqlFile not found: $p" }
        return (Get-Content -LiteralPath $p -Raw -Encoding UTF8)
    }
    if ($step.sql) { return [string]$step.sql }
    throw "Step '$($step.id)': needs 'sql' or 'sqlFile'."
}

function Assert-PctpMutationScoped([string]$sql, [string]$stepId) {
    if ($sql -notmatch '@Prefix') { throw "Step '$stepId': mutating SQL must reference @Prefix so it only touches rows seeded by this run." }
    if ($sql -match '(?is)\bTRUNCATE\b') { throw "Step '$stepId': TRUNCATE is not allowed." }
    if ($sql -match '(?is)\bDROP\s+(DATABASE|SCHEMA|PROCEDURE|VIEW|INDEX)\b') { throw "Step '$stepId': DROP is not allowed." }
    foreach ($m in [regex]::Matches($sql, '(?is)\bDROP\s+TABLE\s+(IF\s+EXISTS\s+)?(\[?[^\s;]+)')) {
        if (-not $m.Groups[2].Value.TrimStart('[').StartsWith('#')) { throw "Step '$stepId': DROP TABLE is only allowed on # temp tables." }
    }
    # A DELETE/UPDATE without any WHERE could touch rows we did not create.
    foreach ($m in [regex]::Matches($sql, '(?is)\b(DELETE\s+FROM|UPDATE)\b(.*?)(;|$)')) {
        if ($m.Groups[2].Value -notmatch '(?is)\bWHERE\b') { throw "Step '$stepId': DELETE/UPDATE without WHERE is not allowed." }
    }
}

function New-PctpSqlCommand($conn, [string]$sql, $tx, $step, [string]$prefix, [string]$runId) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 15
    if ($tx) { $cmd.Transaction = $tx }
    [void]$cmd.Parameters.AddWithValue('@Prefix', $prefix)
    [void]$cmd.Parameters.AddWithValue('@RunId', $runId)
    if ($step.parameters) {
        foreach ($p in $step.parameters.PSObject.Properties) {
            if ($p.Name -in @('Prefix', 'RunId')) { throw "Step '$($step.id)': parameter '$($p.Name)' is reserved." }
            [void]$cmd.Parameters.AddWithValue('@' + $p.Name, $p.Value)
        }
    }
    return $cmd
}

function Compare-PctpValue($actual, $expected) {
    if ($null -eq $actual -or $actual -is [System.DBNull]) { return ($null -eq $expected) }
    $a = 0.0; $e = 0.0
    if ([decimal]::TryParse([string]$actual, [ref]$a) -and [decimal]::TryParse([string]$expected, [ref]$e)) { return ($a -eq $e) }
    return ([string]$actual).Trim() -ceq ([string]$expected).Trim()
}

function Invoke-PctpSqlAssert($step, [string]$sql, [string]$prefix, [string]$runId) {
    $cmd = New-PctpSqlCommand $script:PctpSqlConnection $sql $null $step $prefix $runId
    $reader = $cmd.ExecuteReader()
    $row = $null
    try {
        if ($reader.Read()) {
            $row = @{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) { $row[$reader.GetName($i)] = $reader.GetValue($i) }
        }
    } finally { $reader.Close() }
    $checks = New-Object System.Collections.Generic.List[object]
    if ($step.expectNoRows -eq $true) {
        $ok = ($null -eq $row)
        $checks.Add([pscustomobject]@{ Target = $step.id; Property = 'rows'; Expected = 'no rows'; Actual = $(if ($row) { 'row returned' } else { 'no rows' }); Status = $(if ($ok) { 'PASS' } else { 'FAIL' }) })
    } else {
        if (-not $row) { throw "Step '$($step.id)': assertion query returned no row." }
        if (-not $step.expectColumns) { throw "Step '$($step.id)': sql-assert needs 'expectColumns' (or expectNoRows)." }
        foreach ($p in $step.expectColumns.PSObject.Properties) {
            if (-not $row.ContainsKey($p.Name)) {
                $checks.Add([pscustomobject]@{ Target = $step.id; Property = $p.Name; Expected = [string]$p.Value; Actual = '(column missing)'; Status = 'FAIL' })
                continue
            }
            $ok = Compare-PctpValue $row[$p.Name] $p.Value
            $checks.Add([pscustomobject]@{ Target = $step.id; Property = $p.Name; Expected = [string]$p.Value; Actual = [string]$row[$p.Name]; Status = $(if ($ok) { 'PASS' } else { 'FAIL' }) })
        }
    }
    $bad = @($checks | Where-Object { $_.Status -eq 'FAIL' })
    if ($bad.Count -gt 0) {
        throw ("Step '$($step.id)': SQL postcondition failed: " + (($bad | ForEach-Object { "$($_.Property) expected '$($_.Expected)' actual '$($_.Actual)'" }) -join ' ; '))
    }
    return $checks.ToArray()
}

function Invoke-PctpSqlStep($step, [string]$manifestDir, [string]$prefix, [string]$runId, [string]$kind) {
    if (-not $script:PctpSqlConnection) { throw "Step '$($step.id)': SQL connection is not open." }
    $sql = Get-PctpStepSql $step $manifestDir
    switch ($kind) {
        'assert' { return (Invoke-PctpSqlAssert $step $sql $prefix $runId) }
        'seed' {
            Assert-PctpMutationScoped $sql $step.id
            $tx = $script:PctpSqlConnection.BeginTransaction()
            try {
                $cmd = New-PctpSqlCommand $script:PctpSqlConnection $sql $tx $step $prefix $runId
                $rows = $cmd.ExecuteNonQuery()
                $min = if ($null -ne $step.expectRowsAtLeast) { [int]$step.expectRowsAtLeast } else { 1 }
                if ($rows -lt $min) { throw "Step '$($step.id)': seed affected $rows rows, expected at least $min." }
                $tx.Commit()
            } catch { try { $tx.Rollback() } catch { }; throw }
            return @([pscustomobject]@{ Target = $step.id; Property = 'rowsAffected'; Expected = ">= $min"; Actual = [string]$rows; Status = 'PASS' })
        }
        'cleanup' {
            Assert-PctpMutationScoped $sql $step.id
            if (-not $step.verifySql -and -not $step.verifySqlFile) { throw "Step '$($step.id)': sql-cleanup must declare verifySql / verifySqlFile that returns Remaining." }
            $tx = $script:PctpSqlConnection.BeginTransaction()
            try {
                $cmd = New-PctpSqlCommand $script:PctpSqlConnection $sql $tx $step $prefix $runId
                [void]$cmd.ExecuteNonQuery()
                $tx.Commit()
            } catch { try { $tx.Rollback() } catch { }; throw }
            $verify = [pscustomobject]@{ id = $step.id; sql = $step.verifySql; sqlFile = $step.verifySqlFile; parameters = $step.parameters; expectColumns = [pscustomobject]@{ Remaining = 0 } }
            return (Invoke-PctpSqlAssert $verify (Get-PctpStepSql $verify $manifestDir) $prefix $runId)
        }
        default { throw "Step '$($step.id)': unknown SQL kind '$kind'." }
    }
}
