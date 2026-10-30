[CmdletBinding()]
param(
 [string]$RepositoryRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
 [string]$InventoryCsv=(Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'docs\PCTP_SQL_Runtime_Inventory_EFFECTIVE.csv'),
 [string]$CoverageCsv=(Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'docs\SCHEMA_COVERAGE_EFFECTIVE.csv')
)
$ErrorActionPreference='Stop'
if(!(Test-Path $InventoryCsv)){throw 'RUN-1 runtime inventory is missing.'}
if(!(Test-Path $CoverageCsv)){throw 'RUN-2 effective coverage is missing.'}
$inv=@(Import-Csv $InventoryCsv | Where-Object { $_.'Loại đối tượng' -in @('table','proc','view','function','Bảng','Procedure','View','Function') })
$cov=@(Import-Csv $CoverageCsv)
$ok=@{}; foreach($r in $cov){if($r.class -like 'OK-*' -and $r.kind -in @('table','proc','view','function')){$ok["$($r.kind)|$($r.object.ToLowerInvariant())"]=$true}}
$missing=New-Object System.Collections.Generic.List[object]
foreach($r in $inv){$kind=switch -Regex ($r.'Loại đối tượng'){ '^(table|Bảng)$'{'table'} '^(proc|Procedure)$'{'proc'} '^(view|View)$'{'view'} default{'function'} }; $key="$kind|$($r.'Tên đối tượng'.ToLowerInvariant())"; if(!$ok.ContainsKey($key)){$missing.Add([pscustomobject]@{kind=$kind;object=$r.'Tên đối tượng';caller=$r.'Repository/caller'})}}
$duplicates=@($cov | Where-Object {$_.class -like 'OK-*'} | Group-Object { "$($_.kind)|$($_.object.ToLowerInvariant())" } | Where-Object Count -gt 1)
$badClass=@($cov | Where-Object {$_.in_runtime_inventory -eq 'có' -and $_.class -notlike 'OK-*' -and $_.kind -in @('table','proc','view','function')})
if($missing.Count -gt 0 -or $duplicates.Count -gt 0 -or $badClass.Count -gt 0){
 $out=Join-Path $RepositoryRoot 'artifacts\schema-coverage-gate'; New-Item -ItemType Directory -Force -Path $out|Out-Null
 $missing|Export-Csv (Join-Path $out 'missing-from-coverage.csv') -NoTypeInformation -Encoding UTF8
 $duplicates|Select-Object Name,Count|Export-Csv (Join-Path $out 'duplicate-coverage.csv') -NoTypeInformation -Encoding UTF8
 $badClass|Select-Object object,kind,class|Export-Csv (Join-Path $out 'non-ok-runtime.csv') -NoTypeInformation -Encoding UTF8
 throw "SCHEMA_COVERAGE_EFFECTIVE is incomplete: missing=$($missing.Count), duplicate_keys=$($duplicates.Count), non_ok_runtime=$($badClass.Count)."
}
Write-Host "PASS: effective schema coverage is complete for $($inv.Count) runtime SQL objects."
