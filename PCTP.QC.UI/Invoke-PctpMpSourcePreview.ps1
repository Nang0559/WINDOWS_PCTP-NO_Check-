[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path (Get-Location).Path 'artifacts\mp-source-preview'),
    [switch]$SkipFirewallCheck
)

# Read-only candidate profiling only. This script never exports row identifiers or business values.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')

if (-not $SkipFirewallCheck) { Assert-PctpNoStaleIsolationRules }
$raw = [Environment]::GetEnvironmentVariable('PCTP_QC_SOURCE_SQL_CONNECTION', 'Process')
if ([string]::IsNullOrWhiteSpace($raw)) { throw 'Missing PCTP_QC_SOURCE_SQL_CONNECTION.' }
$connectionString = Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
$login = Assert-PctpSourceReadOnly $connectionString

# Validate expected columns through metadata before querying data. The profile deliberately
# returns aggregate counts only; no LOT, PART, SP, names, order numbers, or employee values.
$schemaSql = @"
SELECT t.name AS table_name, c.name AS column_name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id
WHERE s.name=N'dbo'
  AND t.name IN (N'STOCKTP',N'B20Item',N'Purchase_Order_YMVN')
ORDER BY t.name,c.column_id;
"@
$schema = Invoke-PctpScalarRows $connectionString $schemaSql
$columns = @{}
foreach ($row in $schema.Rows) {
    $name = [string]$row['table_name']
    if (-not $columns.ContainsKey($name)) { $columns[$name] = @() }
    $columns[$name] += [string]$row['column_name']
}
foreach ($required in @('LOT','PART','SLCONLAI','SP')) {
    if (-not $columns.ContainsKey('STOCKTP') -or $columns['STOCKTP'] -notcontains $required) {
        throw "Source dbo.STOCKTP is missing required column '$required'; stop and review the mapping before proceeding."
    }
}
if (-not $columns.ContainsKey('B20Item') -or $columns['B20Item'] -notcontains 'Code') {
    throw 'Source dbo.B20Item.Code was not confirmed. Do not guess the item join; review schema metadata first.'
}

$profileSql = @"
SET NOCOUNT ON;
SELECT
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP) AS stocktp_rows,
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP WHERE SLCONLAI > 0) AS positive_remaining_rows,
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP WHERE SLCONLAI > 0 AND NULLIF(LTRIM(RTRIM(PART)),N'') IS NOT NULL) AS positive_with_part_rows,
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP WHERE SLCONLAI > 0 AND NULLIF(LTRIM(RTRIM(SP)),N'') IS NOT NULL) AS positive_with_sp_rows,
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP WHERE SLCONLAI > 0 AND NULLIF(LTRIM(RTRIM(PART)),N'') IS NOT NULL AND NULLIF(LTRIM(RTRIM(SP)),N'') IS NOT NULL) AS positive_with_part_and_sp_rows,
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP st WHERE st.SLCONLAI > 0 AND NULLIF(LTRIM(RTRIM(st.PART)),N'') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.B20Item i WHERE LTRIM(RTRIM(CONVERT(nvarchar(100),i.Code)))=LTRIM(RTRIM(st.PART)))) AS positive_matching_b20item_rows,
  (SELECT COUNT_BIG(*) FROM dbo.STOCKTP st WHERE st.SLCONLAI > 0 AND NULLIF(LTRIM(RTRIM(st.PART)),N'') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.B20Item i WHERE LTRIM(RTRIM(CONVERT(nvarchar(100),i.Code)))=LTRIM(RTRIM(st.PART))) AND EXISTS (SELECT 1 FROM dbo.Purchase_Order_YMVN po WHERE LTRIM(RTRIM(po.Part_no))=LTRIM(RTRIM(st.PART)))) AS positive_matching_item_and_po_rows;
"@
$profile = (Invoke-PctpScalarRows $connectionString $profileSql).Rows[0]

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$manifest = [ordered]@{
    generated_utc = [DateTime]::UtcNow.ToString('o')
    run_id = New-PctpLiveRunId
    source_database = 'B7R2_FCC'
    source_login = $login
    mode = 'READ_ONLY_AGGREGATE_PROFILE'
    row_identifiers_exported = $false
    business_values_exported = $false
    ddl_or_dml_executed = $false
    query_timeout_seconds = 60
    counts = [ordered]@{
        stocktp_rows = [long]$profile['stocktp_rows']
        positive_remaining_rows = [long]$profile['positive_remaining_rows']
        positive_with_part_rows = [long]$profile['positive_with_part_rows']
        positive_with_sp_rows = [long]$profile['positive_with_sp_rows']
        positive_with_part_and_sp_rows = [long]$profile['positive_with_part_and_sp_rows']
        positive_matching_b20item_rows = [long]$profile['positive_matching_b20item_rows']
        positive_matching_item_and_po_rows = [long]$profile['positive_matching_item_and_po_rows']
    }
}
$path = Join-Path $OutputDirectory 'mp-source-profile-manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8
Write-Host "PASS: SQL14 read-only aggregate profile completed. Login=$login; row identifiers and business values were not exported."
Write-Host ("Counts: STOCKTP={0}; positive={1}; positive+PART={2}; positive+SP={3}; positive+PART+SP={4}; positive matching B20Item={5}; positive matching B20Item and PO={6}" -f $manifest.counts.stocktp_rows,$manifest.counts.positive_remaining_rows,$manifest.counts.positive_with_part_rows,$manifest.counts.positive_with_sp_rows,$manifest.counts.positive_with_part_and_sp_rows,$manifest.counts.positive_matching_b20item_rows,$manifest.counts.positive_matching_item_and_po_rows)
