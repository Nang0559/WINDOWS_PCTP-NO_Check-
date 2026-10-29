<# Offline checks (no SQL Server): G6 journey SQL stays prefix-scoped, LIKE patterns escape '_', configured journeys are complete,
   and the write-script server guard accepts only the approved test server. Exit 0 = pass. PS 5.1 compatible. #>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpSql.Common.ps1')
$script:fail = 0
function Check([string]$Name, [bool]$Ok) { if ($Ok) { Write-Host "PASS  $Name" } else { Write-Host "FAIL  $Name"; $script:fail++ } }
function Throws([scriptblock]$Sb) { try { & $Sb | Out-Null; return $false } catch { return $true } }

$sqlRoot = Join-Path $PSScriptRoot 'journeys\g6\sql'
$journeyFiles = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'journeys\g6') -Filter 'g6-*.json'
foreach ($jf in $journeyFiles) {
    $j = Get-Content -LiteralPath $jf.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $j.configured) { continue }
    $steps = @($j.steps)
    $seedSteps = @($steps | Where-Object { $_.action -eq 'sql-seed' }); $cleanSteps = @($steps | Where-Object { $_.action -eq 'sql-cleanup' })
    Check "$($j.name): seed has a cleanup" (($seedSteps.Count -eq 0) -or ($cleanSteps.Count -ge 1))
    foreach ($st in $steps) {
        foreach ($prop in @('sqlFile', 'verifySqlFile')) {
            if (-not $st.PSObject.Properties[$prop] -or -not $st.$prop) { continue }
            $path = Join-Path (Join-Path $PSScriptRoot 'journeys\g6') $st.$prop
            Check "$($j.name): $($st.$prop) exists" (Test-Path -LiteralPath $path)
            if (-not (Test-Path -LiteralPath $path)) { continue }
            $sql = Get-Content -LiteralPath $path -Raw -Encoding UTF8
            Check "$($j.name): $($st.$prop) is not a NOT-CONFIGURED template" ($sql -notmatch 'NOT CONFIGURED')
            if ($st.action -in @('sql-seed', 'sql-cleanup') -and $prop -eq 'sqlFile') {
                Check "$($j.name): $($st.$prop) passes mutation-scope guard" (-not (Throws { Assert-PctpMutationScoped $sql $st.id }))
            }
            Check "$($j.name): $($st.$prop) escapes '_' in LIKE @Prefix" ($sql -notmatch "(?i)LIKE\s+@Prefix\s*\+")
        }
    }
}
Check 'guard rejects an unscoped DELETE'   (Throws { Assert-PctpMutationScoped 'DELETE FROM dbo.STOCKTP;' 't' })
Check 'guard rejects TRUNCATE'             (Throws { Assert-PctpMutationScoped 'TRUNCATE TABLE dbo.STOCKTP -- @Prefix' 't' })

Check 'server guard: DB57/B7R2_FCC accepted'          (-not (Throws { Assert-PctpTestServerName '192.168.200.57' 'B7R2_FCC' }))
Check 'server guard: SQL14\BRAVO rejected'            (Throws { Assert-PctpTestServerName '192.168.200.14\BRAVO' 'B7R2_FCC' })
Check 'server guard: 192.168.200.140 rejected'        (Throws { Assert-PctpTestServerName '192.168.200.140' 'B7R2_FCC' })
Check 'server guard: other database rejected'         (Throws { Assert-PctpTestServerName '192.168.200.57' 'master' })
$env:PCTP_QC_ALLOWED_TEST_SERVER = '10.0.0.5'
Check 'server guard: env override is honoured'        (-not (Throws { Assert-PctpTestServerName '10.0.0.5' 'B7R2_FCC' }))
Check 'server guard: env override rejects DB57'       (Throws { Assert-PctpTestServerName '192.168.200.57' 'B7R2_FCC' })
Remove-Item Env:PCTP_QC_ALLOWED_TEST_SERVER -ErrorAction SilentlyContinue
if ($script:fail -gt 0) { Write-Host "$script:fail check(s) failed."; exit 1 }
Write-Host 'All G6 SQL guard checks passed.'; exit 0
