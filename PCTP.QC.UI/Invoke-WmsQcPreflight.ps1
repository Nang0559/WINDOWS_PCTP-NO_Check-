[CmdletBinding()]
param(
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Checked {
    param([string]$File, [string[]]$Arguments)
    Write-Host ""
    Write-Host (">>> " + $File + " " + ($Arguments -join " ")) -ForegroundColor Cyan

    # Native tools such as Python unittest intentionally write progress to stderr.
    # Windows PowerShell can promote native stderr records to terminating errors
    # when ErrorActionPreference=Stop. Temporarily use Continue, then trust the
    # native process exit code as the success/failure contract.
    $previousErrorActionPreference = $ErrorActionPreference
    $exitCode = 0
    try {
        $ErrorActionPreference = "Continue"
        & $File @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($null -eq $exitCode -or $exitCode -ne 0) {
        throw "Command failed with exit code $exitCode : $File $($Arguments -join ' ')"
    }
}

$root = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $root) {
    throw "Run this script from inside the WINDOWS_PCTP-NO_Check- Git working tree."
}
Set-Location $root

$branch = (& git branch --show-current).Trim()
if ($branch -ne "feature/wms-end-to-end-consistency") {
    throw "Wrong branch '$branch'. Expected feature/wms-end-to-end-consistency."
}

$sha = (& git rev-parse HEAD).Trim()
$dirty = @(& git status --porcelain)
Write-Host "Repository : $root"
Write-Host "Branch     : $branch"
Write-Host "Commit     : $sha"
Write-Host "Dirty      : $($dirty.Count -gt 0)"

if ($dirty.Count -gt 0 -and -not $AllowDirty) {
    $dirty | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    throw "Working tree is dirty. Commit/stash changes before collecting release evidence. Use -AllowDirty only for a local diagnostic run."
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { throw "Python 3 is required but 'python' was not found on PATH." }
Invoke-Checked $python.Source @("--version")

# 1. Unit tests for SQL2 metadata generators and SQL batch checker.
Invoke-Checked $python.Source @("-m", "unittest", "discover", "-s", "SQL/QC_Schema/tools", "-p", "test_*.py", "-v")

# 2. Verify generated constraint DDL and its dependency report are in sync.
Invoke-Checked $python.Source @(
    "SQL/QC_Schema/tools/gen_schema_constraints.py",
    "--metadata", "SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt",
    "--coverage", "docs/SCHEMA_COVERAGE.csv",
    "--out", "SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql",
    "--report", "SQL/QC_Schema/04_SQL2_Metadata_Gaps.md",
    "--check"
)

# 3. Static sqlcmd-batch simulation only. This does NOT connect to SQL Server.
Invoke-Checked $python.Source @(
    "SQL/QC_Schema/tools/sql_batch_check.py",
    "--files",
    "SQL/QC_Schema/01_Tables.sql",
    "SQL/QC_Schema/05_Split.sql",
    "SQL/QC_Schema/02_Generated_From_DB57.sql",
    "SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql"
)

# 4. Generate repository-wide G1 traceability evidence.
# A non-zero exit means unresolved traceability gaps, not a SQL connection failure.
Invoke-Checked $python.Source @("PCTP.QC.UI/Build-TraceabilityMatrix.py")

Write-Host ""
Write-Host "PREFLIGHT PASS" -ForegroundColor Green
Write-Host "Commit: $sha"
Write-Host "Next: .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2"
Write-Host "Do not run deployment SQL against DB57/DB14 from this preflight."
