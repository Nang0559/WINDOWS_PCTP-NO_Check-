[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path (Get-Location).Path 'artifacts\live-data-inventory'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

function Get-ValidatedConnection([string]$VariableName, [string]$ExpectedHost, [string]$Catalog, [switch]$RequireBravo) {
    $raw = [Environment]::GetEnvironmentVariable($VariableName, 'Process')
    if ([string]::IsNullOrWhiteSpace($raw)) { throw "Missing environment variable $VariableName." }
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($raw)
    if ($b.DataSource -notlike "*$ExpectedHost*") { throw "$VariableName must target the approved SQL host." }
    if ($b.InitialCatalog -ne $Catalog) { throw "$VariableName must target catalog $Catalog." }
    if ($RequireBravo -and $b.DataSource -notmatch '(?i)BRAVO') { throw "$VariableName must target the BRAVO instance." }
    $b.ApplicationName = 'PCTP-QC-LiveData-Inventory-ReadOnly'
    $b.ConnectTimeout = 10
    return $b.ConnectionString
}
function Export-Query([string]$ConnectionString, [string]$Sql, [string]$Path) {
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
    } finally { $conn.Dispose() }
}
function Export-Inventory([string]$Label, [string]$ConnectionString, [string]$Root) {
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
        row_counts = "SELECT s.name schema_name,t.name table_name,SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) approximate_rows FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id LEFT JOIN sys.dm_db_partition_stats p ON p.object_id=t.object_id WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name;"
    }
    foreach ($key in $queries.Keys) {
        Write-Host "Inventory $Label / $key"
        Export-Query $ConnectionString $queries[$key] (Join-Path $dir "$key.csv")
    }
    [pscustomobject]@{ environment=$Label; database='B7R2_FCC'; captured_utc=[DateTime]::UtcNow.ToString('o'); mode='READ_ONLY_METADATA_ONLY'; row_contents_exported=$false; ddl_or_dml_executed=$false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dir 'inventory-manifest.json') -Encoding UTF8
}
$source = Get-ValidatedConnection 'PCTP_QC_SOURCE_SQL_CONNECTION' '192.168.200.14' 'B7R2_FCC' -RequireBravo
$target = Get-ValidatedConnection 'PCTP_QC_TARGET_SQL_CONNECTION' '192.168.200.57' 'B7R2_FCC'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Export-Inventory 'source_sql14' $source $OutputDirectory
Export-Inventory 'target_sql57' $target $OutputDirectory
Write-Host "PASS: read-only inventory saved to $OutputDirectory. No business rows exported; no DDL/DML executed."
