[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InputFile,
    [Parameter(Mandatory=$true)][string]$OutputFile
)
$ErrorActionPreference='Stop'
if(-not(Test-Path -LiteralPath $InputFile -PathType Leaf)){throw "Input fixture not found: $InputFile"}
$f=Get-Content -LiteralPath $InputFile -Raw -Encoding UTF8 | ConvertFrom-Json
if($f.source -ne 'SQL14-read-only'){throw 'Invalid source fixture marker.'}
foreach($row in @($f.rows)){
    $raw=[string]$row.LOT
    if([string]::IsNullOrWhiteSpace($raw)){throw "Order row $($row.STT) has empty LOT."}
    $parts=@($raw.Split(',') | ForEach-Object {$p=$_.Trim(); if($p){$p}})
    $normal=@()
    foreach($p in $parts){
        if($p -match '-\d+$'){$normal+=$p}
        else{$normal+=($p+'-'+[Math]::Max(1,[int][double]$row.SOLUONG))}
    }
    $row.LOT=($normal -join ',')
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputFile) | Out-Null
$f | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputFile -Encoding UTF8
Write-Host "PASS: normalized source fixture -> $OutputFile"
