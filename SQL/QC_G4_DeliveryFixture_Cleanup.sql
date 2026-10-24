/* Removes ONLY the G4 fixture row (STT 32001 + Note QC_G4_ACCEPTANCE_FIXTURE) and proves none remain. Test DB only. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'B7R2_FCC' THROW 51001, N'Wrong database: this cleanup is for B7R2_FCC only.', 1;
IF @@SERVERNAME LIKE N'%BRAVO%' THROW 51002, N'Production server refused.', 1;

BEGIN TRANSACTION;
DELETE FROM dbo.TMPPHIEUGIAOHANG WHERE STT = 32001 AND Note = N'QC_G4_ACCEPTANCE_FIXTURE';
COMMIT TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE Note = N'QC_G4_ACCEPTANCE_FIXTURE') AS RemainingTmp,
    (SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG WHERE CUA = N'QCG4' AND TRUYEN = N'QCG4-FIXTURE') AS LedgerRowsFromFixture;
-- Both columns must be 0. LedgerRowsFromFixture > 0 would mean a procedure write was NOT rolled back: stop and report.
