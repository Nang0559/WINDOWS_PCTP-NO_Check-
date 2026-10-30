[CmdletBinding()]
param(
 [string]$RepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
 [string]$InventoryCsv=(Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'docs\PCTP_SQL_Runtime_Inventory_EFFECTIVE.csv'),
 [string]$CoverageCsv=(Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'docs\SCHEMA_COVERAGE_EFFECTIVE.csv')
)
$ErrorActionPreference='Stop'
if(!(Test-Path $InventoryCsv)){throw "Runtime inventory missing: $InventoryCsv"}
$inv=@(Import-Csv $InventoryCsv | Where-Object { $_.'Loại đối tượng' -in @('table','proc','view','function','Bảng','Procedure','View','Function') })
if($inv.Count -eq 0){throw 'Runtime inventory contains no SQL schema objects.'}
$existing=@(); if(Test-Path $CoverageCsv){$existing=@(Import-Csv $CoverageCsv)}
$map=@{}; foreach($r in $existing){if($r.object){$map["$($r.kind)|$($r.object.ToLowerInvariant())"]=$r}}
$columns=@('object','kind','referenced_by_code','dependency_of_code','code_files','code_ops','on_db57_export','git_ddl_files','in_ci_base_schema','in_runtime_inventory','class')
foreach($i in $inv){
 $kind=switch -Regex ($i.'Loại đối tượng'){ '^(table|Bảng)$'{'table';break} '^(proc|Procedure)$'{'proc';break} '^(view|View)$'{'view';break} default{'function'} }
 $name=$i.'Tên đối tượng'; $key="$kind|$($name.ToLowerInvariant())"
 if(!$map.ContainsKey($key)){
   $map[$key]=[pscustomobject]@{object=$name.ToLowerInvariant();kind=$kind;referenced_by_code='có';dependency_of_code='';code_files=$i.'Repository/caller';code_ops=$i.'SQL thao tác';on_db57_export='chưa xác minh';git_ddl_files=$i.'Được tạo bởi script nào';in_ci_base_schema='chưa xác minh';in_runtime_inventory='có';class='OK-runtime-audit'}
 }
}
$rows=@($map.Values | Sort-Object kind,object)
foreach($r in $rows){ foreach($c in $columns){ if($null -eq $r.$c){$r | Add-Member -NotePropertyName $c -NotePropertyValue '' -Force} } }
$rows | Select-Object $columns | Export-Csv -LiteralPath $CoverageCsv -NoTypeInformation -Encoding UTF8
$unresolved=@($rows | Where-Object {$_.class -notlike 'OK-*'})
Write-Host "Effective coverage generated: $($rows.Count) objects; unresolved/non-OK rows: $($unresolved.Count)."
