[CmdletBinding()]
param(
 [string]$OutputDirectory=(Join-Path (Get-Location).Path 'artifacts\db57-to-db14-promotion'),
 [string]$ScopeCsv=(Join-Path $PSScriptRoot '..\docs\SCHEMA_COVERAGE_EFFECTIVE.csv'),
 [switch]$SkipFirewallCheck
)
$ErrorActionPreference='Stop'; . (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')
if(!(Test-Path $ScopeCsv)){throw "Coverage CSV not found: $ScopeCsv"}
$scope=@(Get-PctpScopeObjects $ScopeCsv); if(@($scope).Count -eq 0){throw 'Empty PCTP scope.'}
if(-not $SkipFirewallCheck){Assert-PctpNoStaleIsolationRules}
$src=Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
$tgt=Get-PctpLiveConnection -VariableName 'PCTP_QC_TARGET_SQL_CONNECTION' -Role Target -Catalog 'B7R2_FCC'
$null=Assert-PctpSourceReadOnly $src 'SQL14 source'; $null=Get-PctpLoginPrivileges $tgt
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
function Q([string]$cs,[string]$sql){$c=New-Object System.Data.SqlClient.SqlConnection($cs);try{$c.Open();$cmd=$c.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=120;$a=New-Object System.Data.SqlClient.SqlDataAdapter($cmd);$t=New-Object System.Data.DataTable;[void]$a.Fill($t);return $t}finally{$c.Dispose()}}
$names=($scope|ForEach-Object{"'$($_.name.Replace("'","''"))'"})-join ','
$sql=@"
SELECT s.name schema_name,o.name object_name,o.type,o.type_desc,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),COALESCE(m.definition,''))),2) definition_sha256,LEN(m.definition) definition_length
FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id
WHERE s.name=N'dbo' AND o.is_ms_shipped=0 AND LOWER(o.name) IN ($names) AND o.type IN ('U','P','V','FN','IF','TF','TR');
"@
$S=Q $src $sql; $T=Q $tgt $sql
$sm=@{};foreach($r in $S.Rows){$sm[$r.object_name.ToLowerInvariant()]=$r};$tm=@{};foreach($r in $T.Rows){$tm[$r.object_name.ToLowerInvariant()]=$r}
$manifest=New-Object System.Collections.Generic.List[object]
foreach($n in ($sm.Keys+$tm.Keys|Sort-Object -Unique)){
 $a=$sm[$n];$b=$tm[$n]; if($null -eq $a){$finding='EXTRA_ON_DB14'}elseif($null -eq $b){$finding='MISSING_ON_DB14'}elseif($a.definition_sha256 -ne $b.definition_sha256 -and $a.type -ne 'U'){$finding='DEFINITION_DIFFERS'}else{continue}
 $manifest.Add([pscustomobject]@{schema='dbo';object_name=$n;object_type=if($a){$a.type_desc}else{$b.type_desc};finding=$finding;db14_hash=if($a){$a.definition_sha256}else{''};db57_hash=if($b){$b.definition_sha256}else{''};action=if($finding -eq 'MISSING_ON_DB14'){'REVIEW_CREATE'}elseif($finding -eq 'DEFINITION_DIFFERS'){'REVIEW_ALTER'}else{'DO_NOT_PROMOTE'})
}
$manifest|Export-Csv (Join-Path $OutputDirectory 'db57_to_db14_definition_drift.csv') -NoTypeInformation -Encoding UTF8
$manifest|Export-Csv (Join-Path $OutputDirectory 'db57_to_db14_promotion_manifest.csv') -NoTypeInformation -Encoding UTF8
# Generate a review-only SQL file. It intentionally contains no executable DDL for changed definitions or tables.
@('-- PCTP DB57 -> DB14 promotion plan','-- READ-ONLY GENERATION; NO DDL/DML WAS EXECUTED.','-- Missing objects are REVIEW_CREATE; definition changes are REVIEW_ALTER.','-- Column/PK/FK/index/trigger changes must be reviewed against live dependencies before application.') | Set-Content (Join-Path $OutputDirectory 'db57_to_db14_apply.sql') -Encoding UTF8
foreach($m in $manifest){Add-Content (Join-Path $OutputDirectory 'db57_to_db14_apply.sql') "-- $($m.finding): dbo.$($m.object_name) [$($m.object_type)] DB14=$($m.db14_hash) DB57=$($m.db57_hash)" -Encoding UTF8}
Write-Host "Promotion plan generated: $($manifest.Count) findings. No business rows and no DDL/DML executed."
