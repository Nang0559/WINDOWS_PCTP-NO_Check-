<#
Source-driven DB57 fixture builder.

Flow:
  1. Invoke-PctpLiveOrderFixture.ps1 reads one real order from SQL14 (read-only) into runner temp JSON.
  2. This script maps the real item/LOT/quantity shape into a NEW QCUI_<run> identity on SQL57.
  3. It preserves LOT structure (date/shift/gear/line/machine) while replacing the item-id segment with the new DB57 item Id.
  4. It creates STOCKTP and TMPPHIEUGIAOHANG prerequisites only. DOCQRCODE/LUUPHIEUGIAOHANG/LUUDOCQRCODE are created by the real application scan.

Never writes SQL14. SQL57 is hard-pinned and every mutation contains @Prefix.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceFixture,
    [Parameter(Mandatory=$true)][string]$SqlUser,
    [string]$Prefix = ('QCUI_' + [guid]::NewGuid().ToString('N').Substring(0,8)),
    [string]$Server = '192.168.200.57',
    [string]$Database = 'B7R2_FCC',
    [switch]$ConfirmWrites,
    [ValidateRange(5,300)][int]$CommandTimeoutSeconds = 90
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'PctpSql.Common.ps1')

if(-not $ConfirmWrites){ throw 'DB57 source-driven seed writes data: pass -ConfirmWrites.' }
if($Server -ne '192.168.200.57' -or $Database -ne 'B7R2_FCC'){ throw 'Target is pinned to SQL57 / B7R2_FCC.' }
if($Prefix -notmatch '^QCUI_[0-9A-Za-z]{8}$'){ throw 'Prefix must look like QCUI_<8 letters/digits>.' }
Assert-PctpTestServerName $Server $Database
if(-not(Test-Path -LiteralPath $SourceFixture -PathType Leaf)){ throw "Source fixture not found: $SourceFixture" }
$fixture=Get-Content -LiteralPath $SourceFixture -Raw -Encoding UTF8 | ConvertFrom-Json
if($fixture.source -ne 'SQL14-read-only'){ throw 'Fixture source marker is invalid.' }
if(@($fixture.rows).Count -lt 1){ throw 'Source fixture contains no order rows.' }

function SqlQuote([string]$v){ if($null -eq $v){return 'NULL'}; return "N'" + $v.Replace("'","''") + "'" }
function Root13([string]$lot){ $v=$lot.Trim().ToUpperInvariant(); if($v.Length -ge 13){return $v.Substring(0,13)}; return $v }
function MapLot([string]$sourceLot,[int]$newItemId){
    $v=$sourceLot.Trim().ToUpperInvariant()
    if($v.Length -lt 20){ throw "Source LOT '$v' is shorter than the verified 20-char LOT key." }
    $base=$v.Substring(0,20)
    # Verified LOT key: YYMMDD + item-id(5) + shift + gear + line(3) + machine(4).
    return $base.Substring(0,6) + $newItemId.ToString('00000') + $base.Substring(11)
}
function ParseLotQty([string]$token){
    $p=$token.Trim(); $dash=$p.LastIndexOf('-')
    if($dash -le 0){ throw "Source LOT element '$p' has no LOT-quantity separator." }
    $lot=$p.Substring(0,$dash).Trim(); $qty=0
    if(-not [int]::TryParse($p.Substring($dash+1),[ref]$qty) -or $qty -le 0){ throw "Source LOT element '$p' has invalid quantity." }
    return [pscustomobject]@{Lot=$lot; Qty=$qty}
}

# Normalise each LUU row into lot elements. If the source row has a single bare LOT, use the row quantity.
$sourceRows=@($fixture.rows)
$distinctItems=@($sourceRows | ForEach-Object { [string]$_.MAHANG } | Where-Object {$_} | Select-Object -Unique)
if($distinctItems.Count -gt 20){ throw "Selected order has $($distinctItems.Count) item codes; refusing an oversized fixture." }

$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$b['Data Source']=$Server; $b['Initial Catalog']=$Database; $b['User ID']=$SqlUser; $b['Password']=$env:PCTP_QC_SQL_PASSWORD; $b['Connect Timeout']=15; $b['Application Name']='PCTP-QC-SourceDrivenSeed'
if([string]::IsNullOrWhiteSpace($env:PCTP_QC_SQL_PASSWORD)){ throw 'PCTP_QC_SQL_PASSWORD is required.' }
$conn=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
$conn.Open()
try{
    Assert-PctpConnectedToTestServer $conn
    $tx=$conn.BeginTransaction()
    try{
        $cmd=$conn.CreateCommand(); $cmd.Transaction=$tx; $cmd.CommandTimeout=$CommandTimeoutSeconds
        [void]$cmd.Parameters.AddWithValue('@Prefix',$Prefix)
        [void]$cmd.Parameters.AddWithValue('@RunId',$Prefix.Substring(5))

        $cmd.CommandText="IF EXISTS(SELECT 1 FROM dbo.B20Item WHERE Code LIKE @Prefix + N'[_]%') THROW 52300,N'Source-driven seed prefix already exists in B20Item.',1; IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANG',N'U') IS NULL OR OBJECT_ID(N'dbo.STOCKTP',N'U') IS NULL THROW 52301,N'Missing TMPPHIEUGIAOHANG or STOCKTP.',1;"
        [void]$cmd.ExecuteNonQuery()

        $map=@{}
        foreach($srcItem in $distinctItems){
            $code=$Prefix+'_I'+($map.Count+1).ToString('00')
            $cmd.CommandText='INSERT INTO dbo.B20Item (Code,Name,Model,Unit) VALUES (@code,@name,@model,@unit); SELECT CAST(SCOPE_IDENTITY() AS int);'
            $cmd.Parameters.Clear(); [void]$cmd.Parameters.AddWithValue('@Prefix',$Prefix); [void]$cmd.Parameters.AddWithValue('@RunId',$Prefix.Substring(5));
            [void]$cmd.Parameters.AddWithValue('@code',$code); [void]$cmd.Parameters.AddWithValue('@name',('QC source fixture '+$Prefix)); [void]$cmd.Parameters.AddWithValue('@model','QCSOURCE'); [void]$cmd.Parameters.AddWithValue('@unit','PCS')
            $newId=[int]$cmd.ExecuteScalar(); if($newId -gt 99999){throw "New B20Item Id $newId does not fit LOT."}
            $map[$srcItem]=[pscustomobject]@{Code=$code;Id=$newId}
        }

        $sttBase=29999
        $cmd.CommandText="SELECT @stt=ISNULL(MAX(STT),29999)+1 FROM dbo.TMPPHIEUGIAOHANG WHERE STT BETWEEN 30000 AND 32500;"
        $cmd.Parameters.Clear(); [void]$cmd.Parameters.AddWithValue('@Prefix',$Prefix); [void]$cmd.Parameters.AddWithValue('@RunId',$Prefix.Substring(5)); $pStt=New-Object System.Data.SqlClient.SqlParameter('@stt',[System.Data.SqlDbType]::Int); $pStt.Direction=[System.Data.ParameterDirection]::Output; [void]$cmd.Parameters.Add($pStt); [void]$cmd.ExecuteNonQuery(); $sttBase=[int]$pStt.Value
        if($sttBase + $sourceRows.Count - 1 -gt 32500){throw 'Reserved QC STT range is exhausted.'}

        $rowIndex=0; $createdLots=New-Object System.Collections.Generic.List[object]
        foreach($row in $sourceRows){
            $srcItem=[string]$row.MAHANG; $item=$map[$srcItem]; if($null -eq $item){throw "No item map for '$srcItem'."}
            $tokens=@()
            if([string]$row.LOT -match ','){$tokens=@([string]$row.LOT -split ',')}else{$tokens=@([string]$row.LOT)}
            $parsed=New-Object System.Collections.Generic.List[object]
            foreach($token in $tokens){
                $q=ParseLotQty $token
                if($tokens.Count -eq 1 -and $token -notmatch '-\d+$'){$q=[pscustomobject]@{Lot=$q.Lot;Qty=[Math]::Max(1,[int][double]$row.SOLUONG)}}
                $mappedLot=MapLot $q.Lot $item.Id
                [void]$parsed.Add([pscustomobject]@{Lot=$mappedLot;Qty=$q.Qty})
            }
            $lotText=($parsed | ForEach-Object { $_.Lot+'-'+$_.Qty }) -join ','
            $total=($parsed | Measure-Object -Property Qty -Sum).Sum
            if($total -le 0){throw "Fixture row $rowIndex has no positive quantity."}

            foreach($x in $parsed){
                $stockQty=[Math]::Max([int]$x.Qty + 2, [int]$x.Qty)
                $cmd.CommandText='INSERT INTO dbo.STOCKTP (LOT,PART,NAME,NGAYSX,SLSX,SLNHAP,SLCONLAI,SLXUAT,STATUSNHAP,SP) VALUES (@lot,@part,@name,@date,@sls,@in,@remain,0,@status,@sp);'
                $cmd.Parameters.Clear(); [void]$cmd.Parameters.AddWithValue('@Prefix',$Prefix); [void]$cmd.Parameters.AddWithValue('@RunId',$Prefix.Substring(5)); [void]$cmd.Parameters.AddWithValue('@lot',$x.Lot); [void]$cmd.Parameters.AddWithValue('@part',$item.Code); [void]$cmd.Parameters.AddWithValue('@name',('QC source fixture '+$Prefix)); [void]$cmd.Parameters.AddWithValue('@date',[datetime]::ParseExact($x.Lot.Substring(0,6),'yyMMdd',[Globalization.CultureInfo]::InvariantCulture)); [void]$cmd.Parameters.AddWithValue('@sls',$stockQty); [void]$cmd.Parameters.AddWithValue('@in',$stockQty); [void]$cmd.Parameters.AddWithValue('@remain',$stockQty); [void]$cmd.Parameters.AddWithValue('@status','QC_SOURCE'); [void]$cmd.Parameters.AddWithValue('@sp',$item.Code); [void]$cmd.ExecuteNonQuery()
                [void]$createdLots.Add([pscustomobject]@{SourceLot=$x.Lot;FixtureLot=$x.Lot;Qty=$x.Qty;Stock=$stockQty;Item=$item.Code})
            }
            $stt=$sttBase+$rowIndex
            $cmd.CommandText='INSERT INTO dbo.TMPPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,NHAMAY,Note) VALUES (@stt,@prefix,@prefix,@item,@name,@lot,@dv,@qty,@day,@time,@status,@plant,@note);'
            $cmd.Parameters.Clear(); [void]$cmd.Parameters.AddWithValue('@Prefix',$Prefix); [void]$cmd.Parameters.AddWithValue('@RunId',$Prefix.Substring(5)); [void]$cmd.Parameters.AddWithValue('@stt',$stt); [void]$cmd.Parameters.AddWithValue('@prefix',$Prefix); [void]$cmd.Parameters.AddWithValue('@item',$item.Code); [void]$cmd.Parameters.AddWithValue('@name',('QC source fixture '+$Prefix)); [void]$cmd.Parameters.AddWithValue('@lot',$lotText); [void]$cmd.Parameters.AddWithValue('@dv','PCS'); [void]$cmd.Parameters.AddWithValue('@qty',$total); [void]$cmd.Parameters.AddWithValue('@day',[datetime]::Parse([string]$fixture.deliveryDate)); [void]$cmd.Parameters.AddWithValue('@time','23:59'); [void]$cmd.Parameters.AddWithValue('@status','PENDING'); [void]$cmd.Parameters.AddWithValue('@plant','QCUI'); [void]$cmd.Parameters.AddWithValue('@note',($Prefix+'_SRC_'+$rowIndex)); [void]$cmd.ExecuteNonQuery()
            $rowIndex++
        }
        $tx.Commit()
        Write-Host "PASS: source-driven DB57 seed prefix=$Prefix sourceDate=$($fixture.deliveryDate) sourceOrder=$($fixture.sourceOrderKey) rows=$($sourceRows.Count) items=$($distinctItems.Count) lots=$($createdLots.Count)."
        $createdLots | Format-Table -AutoSize | Out-String -Width 240 | Write-Host
    }catch{try{$tx.Rollback()}catch{};throw}
}finally{$conn.Dispose()}
