/* Removes ONLY the deterministic G4 fixture. Never run against DB57/DB14. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'PCTP_QC_CI' THROW 51001, N'Wrong database: G4 cleanup is for PCTP_QC_CI only.', 1;
IF @@SERVERNAME LIKE N'%BRAVO%' THROW 51002, N'Production server refused.', 1;

-- The workflow invokes cleanup with if: always(). If schema coverage/deployment failed
-- before fixture seeding, these tables may not exist yet. Skip safely so cleanup does
-- not mask the original CI failure with "Invalid object name".
IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANG', N'U') IS NULL
   OR OBJECT_ID(N'dbo.STOCKTP', N'U') IS NULL
   OR OBJECT_ID(N'dbo.LUUPHIEUGIAOHANG', N'U') IS NULL
BEGIN
    PRINT N'SKIP: G4 fixture cleanup; required tables were not deployed (fixture was not seeded).';
    RETURN;
END;

BEGIN TRANSACTION;
DELETE FROM dbo.TMPPHIEUGIAOHANG
WHERE STT = 32001 AND Note = N'QC_G4_ACCEPTANCE_FIXTURE';

DELETE FROM dbo.STOCKTP
WHERE LOT = N'26071303124101051499'
  AND PART = N'QC-G4-ITEM-001'
  AND STATUSNHAP = N'QC_FIXTURE';
COMMIT TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE Note = N'QC_G4_ACCEPTANCE_FIXTURE') AS RemainingTmp,
    (SELECT COUNT(*) FROM dbo.STOCKTP WHERE LOT = N'26071303124101051499' AND PART = N'QC-G4-ITEM-001' AND STATUSNHAP = N'QC_FIXTURE') AS RemainingStock,
    (SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG WHERE CUA = N'QCG4' AND TRUYEN = N'QCG4-FIXTURE') AS LedgerRowsFromFixture;

IF EXISTS (SELECT 1 FROM dbo.TMPPHIEUGIAOHANG WHERE Note = N'QC_G4_ACCEPTANCE_FIXTURE')
    THROW 51003, N'G4 TMP fixture cleanup failed.', 1;
IF EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE LOT = N'26071303124101051499' AND PART = N'QC-G4-ITEM-001' AND STATUSNHAP = N'QC_FIXTURE')
    THROW 51004, N'G4 STOCK fixture cleanup failed.', 1;
IF EXISTS (SELECT 1 FROM dbo.LUUPHIEUGIAOHANG WHERE CUA = N'QCG4' AND TRUYEN = N'QCG4-FIXTURE')
    THROW 51005, N'G4 procedure write was not rolled back.', 1;
