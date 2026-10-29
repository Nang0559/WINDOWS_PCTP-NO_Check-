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
if ($script:fail -gt 0) { Write-Host "$script:fail check(s) failed."; exit 1 }
Write-Host 'All profile helper checks passed.'; exit 0
