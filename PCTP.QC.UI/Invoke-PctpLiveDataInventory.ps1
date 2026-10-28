[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path (Get-Location).Path 'artifacts\live-data-inventory'),
    [string]$TargetDatabase = 'B7R2_FCC',
    [string]$ScopeCsv = (Join-Path $PSScriptRoot '..\docs\SCHEMA_COVERAGE_EFFECTIVE.csv'),
    [switch]$SkipFirewallCheck
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')

function Export-Query([string]$ConnectionString, [string]$Sql, [string]$Path, [switch]$Optional) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $cmd.CommandTimeout = 60
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)
        $table | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
    } catch {
        if (-not $Optional) { throw }
        Write-Warning "Optional query failed ($Path): $($_.Exception.Message)"
    } finally { $conn.Dispose() }
}

function Export-Inventory([string]$Label, [string]$ConnectionString, [string]$Root, [string]$Database, [string]$LoginName, [object[]]$ScopeObjects) {
    $tableNames = @($ScopeObjects | Where-Object { $_.kind -eq 'table' } | ForEach-Object { $_.name.ToLowerInvariant() } | Sort-Object -Unique)
    $procedureNames = @($ScopeObjects | Where-Object { $_.kind -eq 'proc' } | ForEach-Object { $_.name.ToLowerInvariant() } | Sort-Object -Unique)
    $viewNames = @($ScopeObjects | Where-Object { $_.kind -eq 'view' } | ForEach-Object { $_.name.ToLowerInvariant() } | Sort-Object -Unique)
    $functionNames = @($ScopeObjects | Where-Object { $_.kind -eq 'function' } | ForEach-Object { $_.name.ToLowerInvariant() } | Sort-Object -Unique)
    if ($tableNames.Count -eq 0) { throw 'PCTP scope contains no allowlisted tables; refusing broad inventory.' }

    # Scope values originate in the checked-in allowlist. Validate before interpolating SQL literals.
    foreach ($name in (@($tableNames) + @($procedureNames) + @($viewNames) + @($functionNames))) {
        if ($name -notmatch '^[a-z0-9_]+$') { throw "Unsafe object name in PCTP scope: $name" }
    }
    $tableList = ($tableNames | ForEach-Object { "N'$_'" }) -join ','
    $procedureList = if ($procedureNames.Count -gt 0) { ($procedureNames | ForEach-Object { "N'$_'" }) -join ',' } else { "N'__NO_PCTP_PROCEDURES__'" }
    $viewList = if ($viewNames.Count -gt 0) { ($viewNames | ForEach-Object { "N'$_'" }) -join ',' } else { "N'__NO_PCTP_VIEWS__'" }
    $functionList = if ($functionNames.Count -gt 0) { ($functionNames | ForEach-Object { "N'$_'" }) -join ',' } else { "N'__NO_PCTP_FUNCTIONS__'" }

    $dir = Join-Path $Root $Label
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $queries = [ordered]@{
        tables = "SELECT s.name schema_name,t.name table_name,t.temporal_type,t.is_memory_optimized FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 AND s.name=N'dbo' AND LOWER(t.name) IN ($tableList) ORDER BY s.name,t.name;"
        columns = "SELECT s.name schema_name,t.name table_name,c.column_id,c.name column_name,ty.name data_type,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition default_definition FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE t.is_ms_shipped=0 AND s.name=N'dbo' AND LOWER(t.name) IN ($tableList) ORDER BY s.name,t.name,c.column_id;"
        primary_keys = "SELECT s.name schema_name,t.name table_name,k.name constraint_name,c.name column_name,ic.key_ordinal FROM sys.key_constraints k JOIN sys.tables t ON t.object_id=k.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=k.unique_index_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id WHERE k.type='PK' AND s.name=N'dbo' AND LOWER(t.name) IN ($tableList) ORDER BY s.name,t.name,ic.key_ordinal;"
        foreign_keys = "SELECT ps.name parent_schema,pt.name parent_table,pc.name parent_column,rs.name referenced_schema,rt.name referenced_table,rc.name referenced_column,fk.name foreign_key_name,fk.is_disabled,fk.is_not_trusted,fkc.constraint_column_id FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id JOIN sys.tables pt ON pt.object_id=fkc.parent_object_id JOIN sys.schemas ps ON ps.schema_id=pt.schema_id JOIN sys.columns pc ON pc.object_id=pt.object_id AND pc.column_id=fkc.parent_column_id JOIN sys.tables rt ON rt.object_id=fkc.referenced_object_id JOIN sys.schemas rs ON rs.schema_id=rt.schema_id JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fkc.referenced_column_id WHERE (ps.name=N'dbo' AND LOWER(pt.name) IN ($tableList)) OR (rs.name=N'dbo' AND LOWER(rt.name) IN ($tableList)) ORDER BY ps.name,pt.name,fk.name,fkc.constraint_column_id;"
        indexes = "SELECT s.name schema_name,t.name table_name,i.name index_name,i.is_primary_key,i.is_unique,i.is_disabled,i.type_desc FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 AND s.name=N'dbo' AND i.index_id>0 AND LOWER(t.name) IN ($tableList) ORDER BY s.name,t.name,i.index_id;"
        procedures = "SELECT s.name schema_name,p.name procedure_name,p.create_date,p.modify_date FROM sys.procedures p JOIN sys.schemas s ON s.schema_id=p.schema_id WHERE p.is_ms_shipped=0 AND s.name=N'dbo' AND LOWER(p.name) IN ($procedureList) ORDER BY s.name,p.name;"
        views = "SELECT s.name schema_name,v.name view_name,v.create_date,v.modify_date FROM sys.views v JOIN sys.schemas s ON s.schema_id=v.schema_id WHERE v.is_ms_shipped=0 AND s.name=N'dbo' AND LOWER(v.name) IN ($viewList) ORDER BY s.name,v.name;"
        functions = "SELECT s.name schema_name,o.name function_name,o.type_desc,o.create_date,o.modify_date FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0 AND o.type IN ('FN','IF','TF','FS','FT') AND s.name=N'dbo' AND LOWER(o.name) IN ($functionList) ORDER BY s.name,o.name;"
        triggers = "SELECT OBJECT_SCHEMA_NAME(tr.parent_id) schema_name,OBJECT_NAME(tr.parent_id) table_name,tr.name trigger_name,tr.is_disabled,tr.is_instead_of_trigger FROM sys.triggers tr WHERE tr.is_ms_shipped=0 AND OBJECT_SCHEMA_NAME(tr.parent_id)=N'dbo' AND LOWER(OBJECT_NAME(tr.parent_id)) IN ($tableList) ORDER BY schema_name,table_name,tr.name;"
        procedure_modules = "SELECT s.name schema_name,p.name procedure_name,LEN(m.definition) definition_length,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) definition_sha256 FROM sys.procedures p JOIN sys.schemas s ON s.schema_id=p.schema_id JOIN sys.sql_modules m ON m.object_id=p.object_id WHERE p.is_ms_shipped=0 AND s.name=N'dbo' AND LOWER(p.name) IN ($procedureList) ORDER BY s.name,p.name;"
        row_counts = "SELECT s.name schema_name,t.name table_name,SUM(COALESCE(p.rows,0)) approximate_rows FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id LEFT JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1) WHERE t.is_ms_shipped=0 AND s.name=N'dbo' AND LOWER(t.name) IN ($tableList) GROUP BY s.name,t.name ORDER BY s.name,t.name;"
    }
    foreach ($key in $queries.Keys) {
        Write-Host "Inventory $Label / $key (PCTP allowlist only)"
        Export-Query $ConnectionString $queries[$key] (Join-Path $dir "$key.csv") -Optional:($key -eq 'procedure_modules')
    }
    [pscustomobject]@{
        environment=$Label; database=$Database; login=$LoginName; git_sha=$env:GITHUB_SHA
        run_id=$env:GITHUB_RUN_ID; captured_utc=[DateTime]::UtcNow.ToString('o')
        mode='READ_ONLY_METADATA_ONLY'; row_contents_exported=$false; ddl_or_dml_executed=$false
        scope_tables=$tableNames.Count; scope_procedures=$procedureNames.Count; scope_views=$viewNames.Count; scope_functions=$functionNames.Count; full_database_inventory=$false
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'inventory-manifest.json') -Encoding UTF8
}

if (-not (Test-Path -LiteralPath $ScopeCsv)) {
    throw "PCTP scope CSV not found: $ScopeCsv. Refusing to inventory the entire database."
}
$scope = @(Get-PctpScopeObjects $ScopeCsv)
$tableScopeCount = @($scope | Where-Object { $_.kind -eq 'table' }).Count
if ($tableScopeCount -eq 0) { throw 'PCTP scope CSV resolved to zero tables; refusing broad inventory.' }
$names = @{}
foreach ($obj in $scope) { $names[$obj.name] = $obj.kind }
Write-Host "PCTP allowlist loaded: $tableScopeCount tables, $(@($scope | Where-Object { $_.kind -eq 'proc' }).Count) procedures, $(@($scope | Where-Object { $_.kind -eq 'view' }).Count) views and $(@($scope | Where-Object { $_.kind -eq 'function' }).Count) functions. Unrelated database objects will not be queried."

if (-not $SkipFirewallCheck) { Assert-PctpNoStaleIsolationRules }
$source = Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
$target = Get-PctpLiveConnection -VariableName 'PCTP_QC_TARGET_SQL_CONNECTION' -Role Target -Catalog $TargetDatabase
$sourceLogin = Assert-PctpSourceReadOnly $source 'SQL14 source'
$targetLogin = Assert-PctpSourceReadOnly $target 'SQL57 metadata target'
Write-Host 'PASS: SQL14 source and SQL57 metadata target logins are read-only (no sysadmin/db_owner/write/execute/alter permission).'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Export-Inventory 'source_sql14' $source $OutputDirectory 'B7R2_FCC' $sourceLogin $scope
Export-Inventory 'target_sql57' $target $OutputDirectory $TargetDatabase $targetLogin $scope

# Compare columns only from the scoped exports; no unrelated table metadata is exported.
function Read-Cols([string]$label) {
    Import-Csv -LiteralPath (Join-Path $OutputDirectory "$label\columns.csv") -Encoding UTF8 |
        Where-Object { $names.ContainsKey($_.table_name.ToLowerInvariant()) }
}
$src = @{}
foreach ($col in (Read-Cols 'source_sql14')) { $src["$($col.table_name.ToLowerInvariant()).$($col.column_name.ToLowerInvariant())"] = $col }
$tgt = @{}
foreach ($col in (Read-Cols 'target_sql57')) { $tgt["$($col.table_name.ToLowerInvariant()).$($col.column_name.ToLowerInvariant())"] = $col }
$drift = New-Object System.Collections.Generic.List[object]
foreach ($key in $src.Keys) {
    if (-not $tgt.ContainsKey($key)) {
        $drift.Add([pscustomobject]@{ object=$src[$key].table_name; column=$src[$key].column_name; finding='MISSING_ON_TARGET'; source=$src[$key].data_type; target='' })
        continue
    }
    $a=$src[$key]; $b=$tgt[$key]
    if ($a.data_type -ne $b.data_type -or $a.max_length -ne $b.max_length -or $a.precision -ne $b.precision -or $a.scale -ne $b.scale -or $a.is_nullable -ne $b.is_nullable) {
        $drift.Add([pscustomobject]@{ object=$a.table_name; column=$a.column_name; finding='TYPE_OR_NULLABILITY_DIFFERS'; source="$($a.data_type)($($a.max_length),$($a.precision),$($a.scale)) null=$($a.is_nullable)"; target="$($b.data_type)($($b.max_length),$($b.precision),$($b.scale)) null=$($b.is_nullable)" })
    }
}
foreach ($key in $tgt.Keys) {
    if (-not $src.ContainsKey($key)) {
        $drift.Add([pscustomobject]@{ object=$tgt[$key].table_name; column=$tgt[$key].column_name; finding='EXTRA_ON_TARGET'; source=''; target=$tgt[$key].data_type })
    }
}
$drift | Sort-Object object,column | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'drift_columns_pctp_scope.csv') -NoTypeInformation -Encoding UTF8
Write-Host "PCTP scope objects: $($scope.Count); scoped column drift findings: $($drift.Count)."
Write-Host "PASS: read-only inventory saved to $OutputDirectory. No business rows exported; no DDL/DML executed."
