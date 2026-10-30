[CmdletBinding()]
param(
 [string]$OutputDirectory=(Join-Path (Get-Location).Path 'artifacts\db57-to-db14-promotion'),
 [string]$ScopeCsv=(Join-Path $PSScriptRoot '..\docs\SCHEMA_COVERAGE_EFFECTIVE.csv'),
 [string]$TargetDatabase='B7R2_FCC',
 [switch]$SkipFirewallCheck
)
$ErrorActionPreference='Stop'; . (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')
if(!(Test-Path $ScopeCsv)){throw "Coverage CSV not found: $ScopeCsv"}
$scope=@(Get-PctpScopeObjects $ScopeCsv); if(@($scope).Count -eq 0){throw 'Empty PCTP scope.'}
if(-not $SkipFirewallCheck){Assert-PctpNoStaleIsolationRules}
$src=Get-PctpLiveConnection -VariableName 'PCTP_QC_SOURCE_SQL_CONNECTION' -Role Source
$tgt=Get-PctpLiveConnection -VariableName 'PCTP_QC_TARGET_SQL_CONNECTION' -Role Target -Catalog $TargetDatabase
$null=Assert-PctpSourceReadOnly $src 'SQL14 source'; $null=Get-PctpLoginPrivileges $tgt
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
function Q([string]$cs,[string]$sql){$c=New-Object System.Data.SqlClient.SqlConnection($cs);try{$c.Open();$cmd=$c.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=120;$a=New-Object System.Data.SqlClient.SqlDataAdapter($cmd);$t=New-Object System.Data.DataTable;[void]$a.Fill($t);return $t}finally{$c.Dispose()}}
$names=($scope|ForEach-Object{"'$($_.name.Replace("'","''"))'"})-join ','
$sql=@"
SELECT s.name schema_name,o.name object_name,o.type,o.type_desc,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),COALESCE(m.definition,''))),2) definition_sha256,LEN(m.definition) definition_length
FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id
WHERE s.name=N'dbo' AND o.is_ms_shipped=0 AND LOWER(o.name) IN ($names) AND o.type IN ('U','P','V','FN','IF','TF','TR');
"@
$db14=Q $src $sql; $db57=Q $tgt $sql
$m14=@{};foreach($r in $db14.Rows){$m14[$r.object_name.ToLowerInvariant()]=$r};$m57=@{};foreach($r in $db57.Rows){$m57[$r.object_name.ToLowerInvariant()]=$r}
$manifest=New-Object System.Collections.Generic.List[object]
foreach($n in ($m14.Keys+$m57.Keys|Sort-Object -Unique)){
 $a=$m14[$n];$b=$m57[$n]
 if($null -eq $a -and $null -ne $b){$finding='MISSING_ON_DB14';$action='REVIEW_CREATE'}
 elseif($null -ne $a -and $null -eq $b){$finding='EXTRA_ON_DB14';$action='DO_NOT_PROMOTE'}
 elseif($a.definition_sha256 -ne $b.definition_sha256 -and $a.type -ne 'U'){$finding='DEFINITION_DIFFERS';$action='REVIEW_ALTER'}
 else{continue}
 $manifest.Add([pscustomobject]@{schema='dbo';object_name=$n;object_type=if($b){$b.type_desc}else{$a.type_desc};finding=$finding;db14_hash=if($a){$a.definition_sha256}else{''};db57_hash=if($b){$b.definition_sha256}else{''};action=$action})
}
$manifest|Export-Csv (Join-Path $OutputDirectory 'db57_to_db14_definition_drift.csv') -NoTypeInformation -Encoding UTF8
$manifest|Export-Csv (Join-Path $OutputDirectory 'db57_to_db14_promotion_manifest.csv') -NoTypeInformation -Encoding UTF8
@('-- PCTP DB57 -> DB14 promotion review plan','-- READ-ONLY GENERATION; NO DDL/DML WAS EXECUTED.','-- REVIEW_CREATE/REVIEW_ALTER entries require human approval.','-- No business rows are promoted by this pipeline.') | Set-Content (Join-Path $OutputDirectory 'db57_to_db14_apply.sql') -Encoding UTF8
foreach($m in $manifest){Add-Content (Join-Path $OutputDirectory 'db57_to_db14_apply.sql') "-- $($m.finding): dbo.$($m.object_name) [$($m.object_type)] DB14=$($m.db14_hash) DB57=$($m.db57_hash)" -Encoding UTF8}
Write-Host "DB57 -> DB14 promotion plan generated: $($manifest.Count) findings. No business rows and no DDL/DML executed."
