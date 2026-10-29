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
# ---- profile-shaped multi-LOT delivery fixture (journeys\g6\sql\delivery-hvn-multilot): shape + read-only guarantees
$mlDir = Join-Path $sqlRoot 'delivery-hvn-multilot'
function Get-SqlCode([string]$Path) { return ((Get-Content -LiteralPath $Path -Raw -Encoding UTF8) -split "`n" | Where-Object { $_ -notmatch '^\s*--' }) -join "`n" }
if (Test-Path -LiteralPath $mlDir) {
    $seedCode = Get-SqlCode (Join-Path $mlDir 'seed.sql')
    Check 'multilot seed: writes prerequisites only (no LUU*/DOCQRCODE inserts)' ($seedCode -notmatch '(?i)INSERT\s+INTO\s+dbo\.(LUUPHIEUGIAOHANG|LUUDOCQRCODE|DOCQRCODE)\b')
    Check 'multilot seed: SLCONLAITMP and SATUS stay at defaults'               ($seedCode -notmatch '(?i)SLCONLAITMP|\bSATUS\b')
    Check 'multilot seed: 3 pending lines, 6 lots'                              (([regex]::Matches($seedCode, '(?i)INSERT\s+INTO\s+dbo\.TMPPHIEUGIAOHANG')).Count -eq 3 -and ([regex]::Matches($seedCode, '(?i)INSERT\s+INTO\s+dbo\.STOCKTP')).Count -eq 6)
    Check 'multilot seed: 2-LOT and 3-LOT lines use comma-separated LOT-qty'    (([regex]::Matches($seedCode, "N'-\d+,'")).Count -eq 3)
    foreach ($ro in @('qr', 'assert', 'assert-after-scan', 'verify')) {
        $code = Get-SqlCode (Join-Path $mlDir "$ro.sql")
        Check "multilot ${ro}.sql is read-only" ($code -notmatch '(?i)\b(INSERT|UPDATE|DELETE|DROP|TRUNCATE|ALTER|CREATE|EXEC)\b')
    }
    $afterCode = Get-SqlCode (Join-Path $mlDir 'assert-after-scan.sql')
    Check 'multilot post-scan: no SLCONLAITMP/SATUS/LOTHVN/GIO* in the checks'  ($afterCode -notmatch '(?i)SLCONLAITMP|\bSATUS\b|GIOGIAOFCC|GIOXUAT|\.LOTHVN')
    Check 'multilot post-scan: joins on 13-char root, not on STT'               ($afterCode -match 'LEFT\(LOT, 13\)' -and $afterCode -notmatch '(?i)\.STT\s*=')
}
# ---- real UI QR scan contract for the profile-shaped delivery journey
$deliveryManifestPath = Join-Path $PSScriptRoot 'journeys\\g6\\g6-delivery-hvn-multilot.json'
if (Test-Path -LiteralPath $deliveryManifestPath) {
    $deliveryManifest = Get-Content -LiteralPath $deliveryManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $deliverySteps = @($deliveryManifest.steps)
    $seedStep = @($deliverySteps | Where-Object { $_.id -eq 'seed' } | Select-Object -First 1)
    Check 'multilot journey: seed captures run-local QR inputs without logging them' (($seedStep.Count -eq 1) -and (@($seedStep[0].captureColumns) -contains 'SeedRowsAffected') -and (@($seedStep[0].captureColumns) -contains 'Qr1') -and (@($seedStep[0].captureColumns) -contains 'Qr6'))
    Check 'multilot journey: six QR values enter the real UI control' ((@($deliverySteps | Where-Object { $_.action -eq 'set-value' -and $_.automationId -eq 'txt_DOCQRCODE' }).Count -eq 6))
    Check 'multilot journey: six QR values trigger the real Enter handler' ((@($deliverySteps | Where-Object { $_.action -eq 'press-enter' -and $_.automationId -eq 'txt_DOCQRCODE' }).Count -eq 6))
    $postScan = @($deliverySteps | Where-Object { $_.id -eq 'assert-after-ui' } | Select-Object -First 1)
    Check 'multilot journey: assertion runs after scan and checks stock/QR/delivery invariants' (($postScan.Count -eq 1) -and ($postScan[0].sqlFile -eq 'sql/delivery-hvn-multilot/assert-after-scan.sql') -and ($postScan[0].expectColumns.StockViolations -eq 0) -and ($postScan[0].expectColumns.QrWithoutStockViolations -eq 0))
}
# ---- single-line delivery also must execute the real QR UI path (not merely show a worklist)
$singleDeliveryPath = Join-Path $PSScriptRoot 'journeys\\g6\\g6-delivery-hvn.json'
if (Test-Path -LiteralPath $singleDeliveryPath) {
    $single = Get-Content -LiteralPath $singleDeliveryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $singleSteps = @($single.steps)
    $singleSeed = @($singleSteps | Where-Object { $_.id -eq 'seed' } | Select-Object -First 1)
    $singleAssert = @($singleSteps | Where-Object { $_.id -eq 'assert-after-ui' } | Select-Object -First 1)
    Check 'single-line delivery: seed captures generated QR input' (($singleSeed.Count -eq 1) -and (@($singleSeed[0].captureColumns) -contains 'Qr1') -and (@($singleSeed[0].captureColumns) -contains 'SeedRowsAffected'))
    Check 'single-line delivery: QR input + Enter are automated' ((@($singleSteps | Where-Object { $_.id -eq 'enter-carton-qr' -and $_.action -eq 'set-value' -and $_.automationId -eq 'txt_DOCQRCODE' }).Count -eq 1) -and (@($singleSteps | Where-Object { $_.id -eq 'submit-carton-qr' -and $_.action -eq 'press-enter' -and $_.automationId -eq 'txt_DOCQRCODE' }).Count -eq 1))
    Check 'single-line delivery: read-only post-scan assertions are wired' (($singleAssert.Count -eq 1) -and ($singleAssert[0].sqlFile -eq 'sql/delivery-hvn/assert-after-scan.sql') -and ($singleAssert[0].expectColumns.DeliveredQty -eq 10))
}
if ($script:fail -gt 0) { Write-Host "$script:fail check(s) failed."; exit 1 }
Write-Host 'All G6 SQL guard checks passed.'; exit 0
