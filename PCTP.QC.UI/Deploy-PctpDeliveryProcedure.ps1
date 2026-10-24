<#
 Deploys SQL\Usp_Qrcode_Update_Stock2405.sql to the ISOLATED test database so that Gate G4's
 --delivery-preflight (savepoint + PCTP_DELIVERY + sp_getapplock + @LockOwner='Transaction') can pass.

   .\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -ConfirmIsolatedDb            # deploy
   .\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -WhatIfOnly                   # compare only, no write

 Safety (fail-closed):
   * Target is validated by Test-PctpSqlTarget (PctpSql.Common.ps1): production 192.168.200.14 / BRAVO is always
     rejected; only 192.168.200.57/B7R2_FCC (or PCTP_QC_ISOLATED_TARGET) is accepted.
   * Deploying needs -ConfirmIsolatedDb (it replaces a stored procedure on a shared test DB).
   * The deployed definition is saved to artifacts\deploy\<timestamp>\ BEFORE any change (rollback copy).
   * The change runs in ONE transaction and is committed only if the 4 markers are present afterwards.
   * Password: PCTP_QC_SQL_PASSWORD or prompt; never written to disk or a command line.
 Windows PowerShell 5.1 compatible.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SqlUser,
    [string]$SqlServer = '192.168.200.57',
    [string]$SqlDatabase = 'B7R2_FCC',
    [switch]$ConfirmIsolatedDb,
    [switch]$WhatIfOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpSql.Common.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sqlFile = Join-Path $root 'SQL\Usp_Qrcode_Update_Stock2405.sql'
if (-not (Test-Path -LiteralPath $sqlFile -PathType Leaf)) { throw "Missing $sqlFile" }
$markers = @('PCTP_DELIVERY:', 'sp_getapplock', 'SAVE TRANSACTION UspQrcodeUpdateStock2405', "@LockOwner = N'Transaction'")

function Get-MissingMarkers([string]$text) {
    $m = New-Object System.Collections.Generic.List[string]
    foreach ($x in $markers) { if ($text.IndexOf($x, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { $m.Add($x) } }
    return ,$m.ToArray()
}

# 1) The repository file itself must be deployable before we touch the DB.
$newSql = Get-Content -LiteralPath $sqlFile -Raw -Encoding UTF8
if ($newSql -notmatch '(?im)^\s*CREATE\s+OR\s+ALTER\s+PROC(EDURE)?\s+\[?dbo\]?\.\[?Usp_Qrcode_Update_Stock2405\]?') { throw 'Repo SQL must start with CREATE OR ALTER PROC [dbo].[Usp_Qrcode_Update_Stock2405].' }
if ($newSql -match '(?im)^\s*GO\s*$') { throw 'Repo SQL contains GO; it must be a single batch.' }
if ($newSql.TrimStart().StartsWith('Text')) { throw "Repo SQL starts with stray 'Text' (SSMS header)." }
$bad = Get-MissingMarkers $newSql
if ($bad.Count -gt 0) { throw ('Repo SQL lacks required markers: ' + ($bad -join ' | ')) }

# 2) Connect (target guard + credentials).
if ([string]::IsNullOrEmpty($env:PCTP_QC_SQL_PASSWORD)) {
    $secure = Read-Host -Prompt "SQL password for $SqlUser" -AsSecureString
    $env:PCTP_QC_SQL_PASSWORD = [System.Net.NetworkCredential]::new('', $secure).Password
    $clearPwd = $true
}
$env:PCTP_QC_SQL_USER = $SqlUser
$status = 'FAIL'; $note = ''
$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
$outDir = Join-Path $root "artifacts\deploy\$stamp"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
try {
    Open-PctpSql $SqlServer $SqlDatabase
    $conn = $script:PctpSqlConnection

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.Usp_Qrcode_Update_Stock2405', N'P'));"
    $cmd.CommandTimeout = 15
    $cur = $cmd.ExecuteScalar()
    $curText = if ($cur -is [System.DBNull] -or $null -eq $cur) { '' } else { [string]$cur }
    [System.IO.File]::WriteAllText((Join-Path $outDir 'Usp_Qrcode_Update_Stock2405.before.sql'), $curText, (New-Object System.Text.UTF8Encoding($true)))
    $before = Get-MissingMarkers $curText
    Write-Host ("Deployed procedure present: {0}; missing markers before: {1}" -f ($curText.Length -gt 0), $(if ($before.Count) { $before -join ' | ' } else { 'none' }))

    if ($before.Count -eq 0 -and $curText.Length -gt 0) { $status = 'PASS'; $note = 'Already up to date; nothing deployed.' }
    elseif ($WhatIfOnly) { $status = 'FAIL'; $note = 'WhatIfOnly: deployed procedure is outdated, nothing changed.' }
    else {
        if (-not $ConfirmIsolatedDb) { throw 'Deploy replaces a stored procedure on a shared test DB: pass -ConfirmIsolatedDb.' }
        $tx = $conn.BeginTransaction()
        try {
            $c = $conn.CreateCommand(); $c.Transaction = $tx; $c.CommandTimeout = 60; $c.CommandText = $newSql
            [void]$c.ExecuteNonQuery()
            $v = $conn.CreateCommand(); $v.Transaction = $tx; $v.CommandTimeout = 15
            $v.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.Usp_Qrcode_Update_Stock2405', N'P'));"
            $after = [string]$v.ExecuteScalar()
            $miss = Get-MissingMarkers $after
            if ($miss.Count -gt 0) { throw ('Post-deploy verification failed, rolling back. Missing: ' + ($miss -join ' | ')) }
            $tx.Commit()
            $status = 'PASS'; $note = 'Deployed and verified; previous definition saved as Usp_Qrcode_Update_Stock2405.before.sql.'
        } catch { try { $tx.Rollback() } catch { }; throw }
    }
}
catch { $status = 'FAIL'; $note = $_.Exception.Message; Write-Host "FAIL: $note" }
finally {
    Close-PctpSql
    Remove-Item Env:PCTP_QC_SQL_USER -ErrorAction SilentlyContinue
    if ($clearPwd) { Remove-Item Env:PCTP_QC_SQL_PASSWORD -ErrorAction SilentlyContinue }
}
$commit = ''; try { $commit = (& git -C $root rev-parse HEAD).Trim() } catch { }
[pscustomobject]@{ Status = $status; Target = "$SqlServer/$SqlDatabase"; Commit = $commit; Note = $note; At = (Get-Date).ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outDir 'deploy-summary.json') -Encoding UTF8
Write-Host "$status : $note"
Write-Host "Evidence: $outDir"
Write-Host 'Next: commit, then re-run -Gate G2, -Gate G3, -Gate G4 on the NEW commit (evidence is per-commit).'
if ($status -ne 'PASS') { exit 1 }
