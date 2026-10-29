<# Offline unit checks for the profiling helpers in PctpLiveData.Common.ps1 (no SQL Server). Exit 0 = pass. PS 5.1 compatible. #>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpLiveData.Common.ps1')
$script:fail = 0
function Check([string]$Name, [bool]$Ok) { if ($Ok) { Write-Host "PASS  $Name" } else { Write-Host "FAIL  $Name"; $script:fail++ } }

Check 'mask: digits and letters become shapes'    ((Get-PctpValueMask 'AB-12/cd:7') -eq 'AA-99/aa:9')
Check 'mask: fixed-width LOT keeps its length'     ((Get-PctpValueMask '26102900012310999').Length -eq 17)
Check 'mask: long values are truncated with ~'     ((Get-PctpValueMask ('1' * 100) 10) -eq ('9' * 10 + '~'))
Check 'mask: null stays null'                      ($null -eq (Get-PctpValueMask $null))
Check 'mask never contains the original digits'    ((Get-PctpValueMask 'LOT2026').IndexOfAny([char[]]'2056') -lt 0)

Check 'key: left20 / left13 / trim+upper' ((Get-PctpKeyNormalized ' abcdefghijklmnopqrstuvwxyz ' 'left20') -eq 'ABCDEFGHIJKLMNOPQRST' -and (Get-PctpKeyNormalized 'abcdefghijklmnopq' 'left13') -eq 'ABCDEFGHIJKLM' -and (Get-PctpKeyNormalized ' a ' 'exact') -eq 'A')
Check 'key: blank and DBNull are null'              (($null -eq (Get-PctpKeyNormalized '   ' 'exact')) -and ($null -eq (Get-PctpKeyNormalized ([System.DBNull]::Value) 'exact')))

$lot20 = '26102900012310999999'
$left = @(($lot20 + '00010010'), ($lot20 + '00020010'), 'ZZZZZZZZZZZZZZZZZZZZ0001')
$right = @($lot20)
$jx = Get-PctpJoinRate $left $right 'exact'; $j20 = Get-PctpJoinRate $left $right 'left20'
Check 'join: exact finds nothing when LOT28 vs LOT20' ($jx.matched_rows -eq 0)
Check 'join: left20 matches both LOT28 labels'        ($j20.matched_rows -eq 2 -and $j20.left_rows -eq 3 -and $j20.match_pct -eq 66.7 -and $j20.distinct_keys_matched -eq 1)
Check 'join: empty right gives 0%'                    ((Get-PctpJoinRate $left @() 'exact').match_pct -eq 0)

$p = Get-PctpColumnProfile 'STATUS' 'String' @('OK', 'OK', 'NG', $null, [System.DBNull]::Value) $true
Check 'profile: counts rows, nulls, distinct'         ($p.rows -eq 5 -and $p.nulls -eq 2 -and $p.distinct -eq 2)
Check 'profile: whitelisted enum lists values'        ($p.enum_values -match 'OK x2')
$q = Get-PctpColumnProfile 'LOT' 'String' @('AB12', 'CD34') $false
Check 'profile: non-enum column emits shapes only'    (($null -eq $q.enum_values) -and $q.top_masks -match 'AA99 x2' -and $q.top_masks -notmatch 'AB12')
$n = Get-PctpColumnProfile 'SOLUONG' 'Int16' @([int16]5, [int16]20) $false
Check 'profile: numeric min/max'                       ($n.num_min -eq 5 -and $n.num_max -eq 20)
$d = Get-PctpColumnProfile 'NGAYGIAO' 'DateTime' @([datetime]'2026-10-28 08:00', [datetime]'2026-10-28 17:30') $false
Check 'profile: date min/max'                          ($d.date_min -eq '2026-10-28 08:00' -and $d.date_max -eq '2026-10-28 17:30')
# ---- profile v2 helpers (synthetic data only)
Check 'lot list: splits, trims, upper-cases'          ((@(Split-PctpLotList ' ab-1 , cd-22 ,')).Count -eq 2 -and (Split-PctpLotList 'ab-1,cd-22')[1] -eq 'CD-22')
Check 'lot list: blank/DBNull give no elements'       (@(Split-PctpLotList '  ').Count -eq 0 -and @(Split-PctpLotList ([System.DBNull]::Value)).Count -eq 0)
Check 'lot root: first 13 chars, short kept'          ((Get-PctpLotRoot '1234567890123-045') -eq '1234567890123' -and (Get-PctpLotRoot '12345') -eq '12345')

function New-T([string[]]$cols) { $t = New-Object System.Data.DataTable; foreach ($c in $cols) { [void]$t.Columns.Add($c) }; return , $t }
$luu = New-T @('STT', 'MAHANG', 'LOT', 'SOLUONG')
[void]$luu.Rows.Add('1', 'P-A', '1111111111111-001', '100')
[void]$luu.Rows.Add('1', 'P-A', '1111111111111-002,2222222222222-001', '50')
[void]$luu.Rows.Add('2', 'P-B', '3333333333333-001', '70')
[void]$luu.Rows.Add('3', 'P-C', '', '5')
$qr = New-T @('STT', 'MAHANGFCC', 'LOTFCC', 'SLTEMHVN')
[void]$qr.Rows.Add('1', 'P-A', '1111111111111', '60'); [void]$qr.Rows.Add('1', 'P-A', '1111111111111', '40')
[void]$qr.Rows.Add('9', 'P-B', '3333333333333', '70')

$lk = Get-PctpRowKeyLists $luu @('MAHANG', 'LOT:lotroots')
Check 'row keys: multi-LOT row yields one key per root' (@($lk[1]).Count -eq 2 -and @($lk[1])[0] -eq 'P-A|1111111111111')
Check 'row keys: row with empty part yields no key'     (@($lk[3]).Count -eq 0)
$rk = Get-PctpRowKeyLists $qr @('MAHANGFCC', 'LOTFCC:lotroots')
$j = Get-PctpMultiKeyJoinRate $lk $rk
Check 'multi-key join: any vs all (2 of 3 keyed rows hit any, 2 hit all)' ($j.left_rows -eq 3 -and $j.left_rows_without_key -eq 1 -and $j.any_key_match_pct -eq 100 -and $j.all_keys_match_pct -eq 66.7)
$j2 = Get-PctpMultiKeyJoinRate (Get-PctpRowKeyLists $luu @('STT', 'MAHANG', 'LOT:lotroots')) (Get-PctpRowKeyLists $qr @('STT', 'MAHANGFCC', 'LOTFCC:lotroots'))
Check 'multi-key join: adding STT to the key lowers the match' ($j2.any_key_match_pct -lt $j.any_key_match_pct)

$b = Get-PctpGroupSizeBuckets @(1, 1, 1, 2, 3, 3)
Check 'group sizes: 3 groups sized 3/1/2 -> buckets'   ($b -match 'groups=3' -and $b -match '1 x1' -and $b -match '2 x1' -and $b -match '3-5 x1')
Check 'list length buckets'                            ((Get-PctpListLengthBuckets @('A', 'A,B', 'A,B', '')) -eq '1 LOT x1 | 2 LOT x2')

$lkk = @('K1', 'K1', 'K2', 'K3'); $lq = @(100, 50, 70, 5)
$rkk = @('K1', 'K2', 'K4'); $rq = @(150, 60, 9)
$c = Compare-PctpGroupSums $lkk $lq $rkk $rq
Check 'group sums: equal / left greater / left-only / right-only' ($c.keys_both -eq 2 -and $c.sums_equal -eq 1 -and $c.left_greater -eq 1 -and $c.left_only_keys -eq 1 -and $c.right_only_keys -eq 1)

if ($script:fail -gt 0) { Write-Host "$script:fail check(s) failed."; exit 1 }
Write-Host 'All profile helper checks passed.'; exit 0
