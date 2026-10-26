<#
 Deploys the delivery stored procedures from SQL\ to the ISOLATED test database so that Gate G4's
 --delivery-preflight can pass:
   * dbo.Usp_Qrcode_Update_Stock2405  (PCTP_DELIVERY + sp_getapplock + savepoint + @LockOwner='Transaction')
   * dbo.Usp_Qrcode_Update_Stock_SP   (PCTP_DELIVERY_FIFO_SP + savepoint + UPDLOCK,HOLDLOCK + @slCan <= 0 + transaction boundary)

   .\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -ConfirmIsolatedDb            # deploy both
   .\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -WhatIfOnly                   # compare only, no write
   .\PCTP.QC.UI\Deploy-PctpDeliveryProcedure.ps1 -SqlUser <user> -Procedure Stock_SP -ConfirmIsolatedDb   # one procedure

 Safety (fail-closed):
   * Target is validated by Test-PctpSqlTarget (PctpSql.Common.ps1): production 192.168.200.14 / BRAVO is always
     rejected; only 192.168.200.57/B7R2_FCC (or PCTP_QC_ISOLATED_TARGET) is accepted.
   * Deploying needs -ConfirmIsolatedDb (it replaces stored procedures on a shared test DB).
   * Every deployed definition is saved to artifacts\deploy\<timestamp>\<proc>.before.sql BEFORE any change (rollback copy).
   * Each procedure is changed in ONE transaction and committed only if all of its markers are present afterwards.
   * Password: PCTP_QC_SQL_PASSWORD or prompt; never written to disk or a command line.
 Windows PowerShell 5.1 compatible.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SqlUser,
    [string]$SqlServer = '192.168.200.57',
    [string]$SqlDatabase = 'B7R2_FCC',
    [ValidateSet('All', 'Stock2405', 'Stock_SP')][string]$Procedure = 'All',
    [switch]$ConfirmIsolatedDb,
    [switch]$WhatIfOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PctpSql.Common.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

# Markers must stay identical to PCTP.QC.SqlIntegration\Program.cs VerifyDeliveryProcedureReadinessAsync.
$targets = @(
    @{
        Key     = 'Stock2405'
        Name    = 'Usp_Qrcode_Update_Stock2405'
        File    = 'SQL\Usp_Qrcode_Update_Stock2405.sql'
        Markers = @('PCTP_DELIVERY:', 'sp_getapplock', 'SAVE TRANSACTION UspQrcodeUpdateStock2405', "@LockOwner = N'Transaction'")
    },
    @{
        Key     = 'Stock_SP'
        Name    = 'Usp_Qrcode_Update_Stock_SP'
        File    = 'SQL\Usp_Qrcode_Update_Stock_SP.sql'
        Markers = @('PCTP_DELIVERY_FIFO_SP', 'SAVE TRANSACTION PctpFifoDeliverySp', 'UPDLOCK, HOLDLOCK', '@slCan <= 0', 'BEGIN TRANSACTION', 'COMMIT TRANSACTION', 'ROLLBACK TRANSACTION')
    }
)
if ($Procedure -ne 'All') { $targets = @($targets | Where-Object { $_.Key -eq $Procedure }) }

function Get-MissingMarkers([string]$text, [string[]]$markers) {
    $m = New-Object System.Collections.Generic.List[string]
    foreach ($x in $markers) { if ($text.IndexOf($x, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { $m.Add($x) } }
    return ,$m.ToArray()
}

# 1) Every repository file must be deployable before we touch the DB.
foreach ($t in $targets) {
    $path = Join-Path $root $t.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing $path" }
    $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $pattern = '(?im)^\s*CREATE\s+OR\s+ALTER\s+PROC(EDURE)?\s+\[?dbo\]?\.\[?' + [regex]::Escape($t.Name) + '\]?'
    if ($text -notmatch $pattern) { throw ('Repo SQL must start with CREATE OR ALTER PROC [dbo].[' + $t.Name + '].') }
    if ($text -match '(?im)^\s*GO\s*$') { throw ($t.File + ' contains GO; it must be a single batch.') }
    if ($text.TrimStart().StartsWith('Text')) { throw ($t.File + " starts with stray 'Text' (SSMS header).") }
    $bad = Get-MissingMarkers $text $t.Markers
    if ($bad.Count -gt 0) { throw ($t.File + ' lacks required markers: ' + ($bad -join ' | ')) }
    $t.Sql = $text
}

# 2) Connect (target guard + credentials).
if ([string]::IsNullOrEmpty($env:PCTP_QC_SQL_PASSWORD)) {
    $secure = Read-Host -Prompt "SQL password for $SqlUser" -AsSecureString
    $env:PCTP_QC_SQL_PASSWORD = [System.Net.NetworkCredential]::new('', $secure).Password
    $clearPwd = $true
}
$env:PCTP_QC_SQL_USER = $SqlUser
$status = 'FAIL'; $note = ''
$results = New-Object System.Collections.Generic.List[object]
$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
$outDir = Join-Path $root "artifacts\deploy\$stamp"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
try {
    Open-PctpSql $SqlServer $SqlDatabase
    $conn = $script:PctpSqlConnection

    foreach ($t in $targets) {
        $objectName = 'dbo.' + $t.Name
        $itemStatus = 'FAIL'; $itemNote = ''
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'$objectName', N'P'));"
            $cmd.CommandTimeout = 15
            $cur = $cmd.ExecuteScalar()
            $curText = if ($cur -is [System.DBNull] -or $null -eq $cur) { '' } else { [string]$cur }
            [System.IO.File]::WriteAllText((Join-Path $outDir ($t.Name + '.before.sql')), $curText, (New-Object System.Text.UTF8Encoding($true)))
            $before = Get-MissingMarkers $curText $t.Markers
            Write-Host ("{0}: present={1}; missing markers before: {2}" -f $t.Name, ($curText.Length -gt 0), $(if ($before.Count) { $before -join ' | ' } else { 'none' }))

            if ($before.Count -eq 0 -and $curText.Length -gt 0) { $itemStatus = 'PASS'; $itemNote = 'Already up to date; nothing deployed.' }
            elseif ($WhatIfOnly) { $itemStatus = 'FAIL'; $itemNote = 'WhatIfOnly: deployed procedure is outdated, nothing changed.' }
            else {
                if (-not $ConfirmIsolatedDb) { throw 'Deploy replaces a stored procedure on a shared test DB: pass -ConfirmIsolatedDb.' }
                $tx = $conn.BeginTransaction()
                try {
                    $c = $conn.CreateCommand(); $c.Transaction = $tx; $c.CommandTimeout = 60; $c.CommandText = $t.Sql
                    [void]$c.ExecuteNonQuery()
                    $v = $conn.CreateCommand(); $v.Transaction = $tx; $v.CommandTimeout = 15
                    $v.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'$objectName', N'P'));"
                    $after = [string]$v.ExecuteScalar()
                    $miss = Get-MissingMarkers $after $t.Markers
                    if ($miss.Count -gt 0) { throw ('Post-deploy verification failed, rolling back. Missing: ' + ($miss -join ' | ')) }
                    $tx.Commit()
                    $itemStatus = 'PASS'; $itemNote = 'Deployed and verified; previous definition saved as ' + $t.Name + '.before.sql.'
                } catch { try { $tx.Rollback() } catch { }; throw }
            }
        }
        catch { $itemStatus = 'FAIL'; $itemNote = $_.Exception.Message; Write-Host ("FAIL {0}: {1}" -f $t.Name, $itemNote) }
        $results.Add([pscustomobject]@{ Procedure = $t.Name; Status = $itemStatus; Note = $itemNote })
        Write-Host ("{0} : {1} : {2}" -f $itemStatus, $t.Name, $itemNote)
    }

    $failed = @($results | Where-Object { $_.Status -ne 'PASS' })
    if ($failed.Count -eq 0 -and $results.Count -gt 0) { $status = 'PASS'; $note = 'All delivery procedures are up to date on the isolated DB.' }
    else { $status = 'FAIL'; $note = (($failed | ForEach-Object { $_.Procedure + ': ' + $_.Note }) -join ' | ') }
}
catch { $status = 'FAIL'; $note = $_.Exception.Message; Write-Host "FAIL: $note" }
finally {
    Close-PctpSql
    Remove-Item Env:PCTP_QC_SQL_USER -ErrorAction SilentlyContinue
    if ($clearPwd) { Remove-Item Env:PCTP_QC_SQL_PASSWORD -ErrorAction SilentlyContinue }
}
$commit = ''; try { $commit = (& git -C $root rev-parse HEAD).Trim() } catch { }
[pscustomobject]@{ Status = $status; Target = "$SqlServer/$SqlDatabase"; Commit = $commit; Note = $note; Procedures = @($results); At = (Get-Date).ToString('o') } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outDir 'deploy-summary.json') -Encoding UTF8
Write-Host "$status : $note"
Write-Host "Evidence: $outDir"
Write-Host 'Next: commit, then re-run -Gate G2, -Gate G3, -Gate G4 on the NEW commit (evidence is per-commit).'
if ($status -ne 'PASS') { exit 1 }
