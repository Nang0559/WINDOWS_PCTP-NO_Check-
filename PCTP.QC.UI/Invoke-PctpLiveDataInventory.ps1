[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path (Get-Location).Path 'artifacts\live-data-inventory'),
    # Catalog to inventory on SQL57. B7R2_FCC (shared) or a dedicated B7R2_FCC_QC* test catalog.
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
function Export-Inventory([string]$Label, [string]$ConnectionString, [string]$Root, [string]$Database, [string]$LoginName) {
    $dir = Join-Path $Root $Label
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $queries = [ordered]@{
        tables = "SELECT s.name schema_name,t.name table_name,t.temporal_type,t.is_memory_optimized FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;"
        columns = "SELECT s.name schema_name,t.name table_name,c.column_id,c.name column_name,ty.name data_type,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,dc.definition default_definition FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.column_id;"
        primary_keys = "SELECT s.name schema_name,t.name table_name,k.name constraint_name,c.name column_name,ic.key_ordinal FROM sys.key_constraints k JOIN sys.tables t ON t.object_id=k.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=k.unique_index_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id WHERE k.type='PK' ORDER BY s.name,t.name,ic.key_ordinal;"
        foreign_keys = "SELECT ps.name parent_schema,pt.name parent_table,pc.name parent_column,rs.name referenced_schema,rt.name referenced_table,rc.name referenced_column,fk.name foreign_key_name,fk.is_disabled,fk.is_not_trusted,fkc.constraint_column_id FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id JOIN sys.tables pt ON pt.object_id=fkc.parent_object_id JOIN sys.schemas ps ON ps.schema_id=pt.schema_id JOIN sys.columns pc ON pc.object_id=pt.object_id AND pc.column_id=fkc.parent_column_id JOIN sys.tables rt ON rt.object_id=fkc.referenced_object_id JOIN sys.schemas rs ON rs.schema_id=rt.schema_id JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fkc.referenced_column_id ORDER BY ps.name,pt.name,fk.name,fkc.constraint_column_id;"
        indexes = "SELECT s.name schema_name,t.name table_name,i.name index_name,i.is_primary_key,i.is_unique,i.is_disabled,i.type_desc FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 AND i.index_id>0 ORDER BY s.name,t.name,i.index_id;"
        procedures = "SELECT s.name schema_name,p.name procedure_name,p.create_date,p.modify_date FROM sys.procedures p JOIN sys.schemas s ON s.schema_id=p.schema_id WHERE p.is_ms_shipped=0 ORDER BY s.name,p.name;"
        triggers = "SELECT OBJECT_SCHEMA_NAME(tr.parent_id) schema_name,OBJECT_NAME(tr.parent_id) table_name,tr.name trigger_name,tr.is_disabled,tr.is_instead_of_trigger FROM sys.triggers tr WHERE tr.is_ms_shipped=0 ORDER BY schema_name,table_name,tr.name;"
        procedure_modules = "SELECT s.name schema_name,p.name procedure_name,LEN(m.definition) definition_length,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) definition_sha256 FROM sys.procedures p JOIN sys.schemas s ON s.schema_id=p.schema_id JOIN sys.sql_modules m ON m.object_id=p.object_id WHERE p.is_ms_shipped=0 ORDER BY s.name,p.name;"
        row_counts = "SELECT s.name schema_name,t.name table_name,SUM(COALESCE(p.rows,0)) approximate_rows FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id LEFT JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1) WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name;"
    }
    foreach ($key in $queries.Keys) {
        Write-Host "Inventory $Label / $key"
        Export-Query $ConnectionString $queries[$key] (Join-Path $dir "$key.csv") -Optional:($key -eq 'procedure_modules')
    }
    [pscustomobject]@{ environment=$Label; database=$Database; login=$LoginName; git_sha=$env:GITHUB_SHA; run_id=$env:GITHUB_RUN_ID; captured_utc=[DateTime]::UtcNow.ToString('o'); mode='READ_ONLY_METADATA_ONLY'; row_contents_exported=$false; ddl_or_dml_executed=$false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'inventory-manifest.json') -Encoding UTF8
}
if (-not $SkipFirewallCheck) { Assert-PctpNoStaleIsolationRules }
$source = Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
$target = Get-PctpLiveConnection -VariableName 'PCTP_QC_TARGET_SQL_CONNECTION' -Role Target -Catalog $TargetDatabase
$sourceLogin = Assert-PctpSourceReadOnly $source
$targetLogin = [string](Invoke-PctpScalarRows $target 'SELECT SUSER_SNAME() AS login_name').Rows[0]['login_name']
Write-Host 'PASS: SQL14 login is read-only (no sysadmin/db_owner/write/execute/alter permission).'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Export-Inventory 'source_sql14' $source $OutputDirectory 'B7R2_FCC' $sourceLogin
Export-Inventory 'target_sql57' $target $OutputDirectory $TargetDatabase $targetLogin

# Drift report limited to the PCTP-only scope, so ~500 unrelated tables of the shared B7R2 database are ignored.
if (Test-Path -LiteralPath $ScopeCsv) {
    $scope = Get-PctpScopeObjects $ScopeCsv
    $names = @{}; foreach ($o in $scope) { $names[$o.name] = $o.kind }
    function Read-Cols([string]$label) {
        Import-Csv -LiteralPath (Join-Path $OutputDirectory "$label\columns.csv") -Encoding UTF8 |
            Where-Object { $names.ContainsKey($_.table_name.ToLowerInvariant()) }
    }
    $src = @{}; foreach ($c in (Read-Cols 'source_sql14')) { $src["$($c.table_name.ToLowerInvariant()).$($c.column_name.ToLowerInvariant())"] = $c }
    $tgt = @{}; foreach ($c in (Read-Cols 'target_sql57')) { $tgt["$($c.table_name.ToLowerInvariant()).$($c.column_name.ToLowerInvariant())"] = $c }
    $drift = New-Object System.Collections.Generic.List[object]
    foreach ($k in $src.Keys) {
        if (-not $tgt.ContainsKey($k)) { $drift.Add([pscustomobject]@{ object = $src[$k].table_name; column = $src[$k].column_name; finding = 'MISSING_ON_TARGET'; source = $src[$k].data_type; target = '' }); continue }
        $a = $src[$k]; $b = $tgt[$k]
        if ($a.data_type -ne $b.data_type -or $a.max_length -ne $b.max_length -or $a.precision -ne $b.precision -or $a.scale -ne $b.scale -or $a.is_nullable -ne $b.is_nullable) {
            $drift.Add([pscustomobject]@{ object = $a.table_name; column = $a.column_name; finding = 'TYPE_OR_NULLABILITY_DIFFERS'; source = "$($a.data_type)($($a.max_length),$($a.precision),$($a.scale)) null=$($a.is_nullable)"; target = "$($b.data_type)($($b.max_length),$($b.precision),$($b.scale)) null=$($b.is_nullable)" })
        }
    }
    foreach ($k in $tgt.Keys) { if (-not $src.ContainsKey($k)) { $drift.Add([pscustomobject]@{ object = $tgt[$k].table_name; column = $tgt[$k].column_name; finding = 'EXTRA_ON_TARGET'; source = ''; target = $tgt[$k].data_type }) } }
    $drift | Sort-Object object, column | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'drift_columns_pctp_scope.csv') -NoTypeInformation -Encoding UTF8
    Write-Host "Scope objects: $($scope.Count); column drift findings: $($drift.Count) (see drift_columns_pctp_scope.csv)."
} else { Write-Warning "Scope CSV not found ($ScopeCsv); drift report skipped." }
Write-Host "PASS: read-only inventory saved to $OutputDirectory. No business rows exported; no DDL/DML executed."
