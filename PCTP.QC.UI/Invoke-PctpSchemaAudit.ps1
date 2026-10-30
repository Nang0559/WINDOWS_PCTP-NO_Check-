[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$OutputCsv = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'docs\PCTP_SQL_Runtime_Inventory_EFFECTIVE.csv')
)
$ErrorActionPreference='Stop'

$extensions=@('.cs','.sql','.ps1','.config','.json','.csproj')
$excluded=@('bin','obj','.git','TestResults','artifacts')
$files=Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -File | Where-Object {
    $extensions -contains $_.Extension.ToLowerInvariant() -and
    -not ($excluded | Where-Object { $_ -and $_.FullName -like "*\$_\*" })
}

$rows=New-Object System.Collections.Generic.List[object]
$seen=@{}
function Add-Object([string]$Name,[string]$Kind,[string]$File,[string]$Operation,[string]$Evidence,[string]$Runtime='Runtime') {
    if([string]::IsNullOrWhiteSpace($Name)){return}
    $n=$Name.Trim().Trim('[',']').Split('.')[-1]
    if($n -notmatch '^[A-Za-z_][A-Za-z0-9_]*$'){return}
    $k=$Kind.ToLowerInvariant(); if($k -notin @('table','proc','view','function')){return}
    $key="$k|$($n.ToLowerInvariant())"
    if(-not $seen.ContainsKey($key)){$seen[$key]=1; $rows.Add([pscustomobject]@{
        'Tên đối tượng'=$n; 'Loại đối tượng'=$k; 'Database/schema'='B7R2_FCC.dbo'; 'Repository/caller'=(Resolve-Path -LiteralPath $File -Relative).Path.Replace('\','/'); 'SQL thao tác'=$Operation; 'Khách hàng'='PCTP'; 'Được tạo bởi script nào'=$Evidence; 'Có trên DB57?'='Chưa xác minh'; 'Có trong schema Git?'='Chưa xác minh'; 'Runtime hay test-only'=$Runtime; 'Trạng thái xác minh'='AUDIT-EVIDENCE'
    })}
}

foreach($f in $files){
    $text=Get-Content -LiteralPath $f.FullName -Raw -ErrorAction SilentlyContinue
    if([string]::IsNullOrWhiteSpace($text)){continue}
    # SQL object references in FROM/JOIN/INSERT/UPDATE/DELETE/MERGE/EXEC.
    $patterns=@(
        @{rx='(?is)\b(?:FROM|JOIN|INTO|UPDATE|DELETE\s+FROM|MERGE\s+INTO)\s+(?:\[dbo\]\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?';op='FROM/JOIN/INTO/UPDATE/DELETE/MERGE';kind='table'},
        @{rx='(?is)\bEXEC(?:UTE)?\s+(?:\[dbo\]\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?';op='EXEC';kind='proc'},
        @{rx='(?is)\b(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?:\[dbo\]\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?';op='DDL';kind='proc'},
        @{rx='(?is)\b(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?VIEW\s+(?:\[dbo\]\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?';op='DDL';kind='view'},
        @{rx='(?is)\b(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?FUNCTION\s+(?:\[dbo\]\.)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?';op='DDL';kind='function'}
    )
    foreach($p in $patterns){ foreach($m in [regex]::Matches($text,$p.rx)){ Add-Object $m.Groups[1].Value $p.kind $f.FullName $p.op $f.FullName } }
    # Common dynamic table configuration literals.
    foreach($m in [regex]::Matches($text,'(?i)(?:OrderTable|TmpTable|DocQrTable|IfsTable|TableName)\s*[=:]\s*["'']([A-Za-z_][A-Za-z0-9_]*)["'']')){ Add-Object $m.Groups[1].Value 'table' $f.FullName 'DYNAMIC-CONFIG' $f.FullName }
}

# Keep known PCTP schema objects that are referenced by configuration but may not have literal SQL callers.
$known=@('FVN_Purchase_Order_HVNDB','Purchase_Order_HTN','Purchase_Order_YMVN','TMPPHIEUGIAOHANG','TMPPHIEUGIAOHANG_SP','TMPPHIEUGIAOHANG_100003','TMPPHIEUGIAOHANG_100002','SP_TMPPHIEUGIAOHANG','DOCQRCODE','DOCQRCODE_SP','DOCQRCODE_100003','YMVN_DOCQRCODE','SP_DOCQRCODE','IFSPHIEUGIAOHANG','IFSPHIEUGIAOHANG_SP','IFSPHIEUGIAOHANG_100003','IFSPHIEUGIAOHANG_100002','STOCKTP','Warehouse','Rack','Slot','SlotLot','InspectionConfig','InspectionLog','B20Item','FVN_ItemFifoConfig','FVN_ItemFifoConfig_History','StockHistory','vNhapTP','vB20Item')
foreach($n in $known){ if(-not $seen.ContainsKey("table|$($n.ToLowerInvariant())")){ Add-Object $n 'table' (Join-Path $RepositoryRoot 'PCTP') 'CONFIG-CATALOG' 'CustomerTableConfig/runtime catalog' } }

$dir=Split-Path -Parent $OutputCsv; New-Item -ItemType Directory -Force -Path $dir | Out-Null
$rows | Sort-Object 'Loại đối tượng','Tên đối tượng' | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
Write-Host "PCTP runtime schema audit complete: $($rows.Count) SQL/runtime objects -> $OutputCsv"
