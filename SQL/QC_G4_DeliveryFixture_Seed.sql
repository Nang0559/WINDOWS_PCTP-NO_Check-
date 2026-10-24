/*
  G4 acceptance fixture: ONE pending delivery row for the isolated test DB (192.168.200.57 / B7R2_FCC).
  Run in SSMS on the TEST database only. Never on 192.168.200.14\BRAVO.

  Row identity: STT = 32001 and Note = 'QC_G4_ACCEPTANCE_FIXTURE'. Remove it afterwards with
  SQL/QC_G4_DeliveryFixture_Cleanup.sql. The G4 scenario itself rolls back every procedure write,
  so only this one TMP row exists while the gate runs.

  Then:  .\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G4 -SqlUser <user> -DeliveryAcceptanceStt 32001
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'B7R2_FCC' THROW 51001, N'Wrong database: this fixture is for B7R2_FCC only.', 1;
IF @@SERVERNAME LIKE N'%BRAVO%' THROW 51002, N'Production server refused.', 1;

DECLARE @Stt smallint = 32001,
        @Lot nvarchar(100) = N'26071303124101051400',   -- physical STOCKTP LOT, SLCONLAI = 32000
        @Part nvarchar(100) = N'22201-KYHN-A400',
        @Qty smallint = 10;

IF EXISTS (SELECT 1 FROM dbo.TMPPHIEUGIAOHANG WHERE STT = @Stt)
    THROW 51003, N'STT 32001 already exists in TMPPHIEUGIAOHANG. Run the cleanup script first.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE RTRIM(LOT) = @Lot AND RTRIM(PART) = @Part AND SLCONLAI >= @Qty)
    THROW 51004, N'Stock LOT/PART for the fixture is missing or too low. Pick another LOT with SLCONLAI >= 10.', 1;

BEGIN TRANSACTION;

INSERT INTO dbo.TMPPHIEUGIAOHANG
    (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, NHAMAY, Note)
VALUES
    (@Stt, N'QCG4', N'QCG4-FIXTURE', @Part, N'QC G4 acceptance fixture', @Lot + N'-' + CONVERT(nvarchar(10), @Qty),
     N'PCS', @Qty, CAST(GETDATE() AS date), N'23:59', N'PENDING', 'QCG4', N'QC_G4_ACCEPTANCE_FIXTURE');

IF @@ROWCOUNT <> 1 THROW 51005, N'Fixture insert did not affect exactly one row.', 1;
COMMIT TRANSACTION;

SELECT STT, LOT, MAHANG, SOLUONG, STATUS, NGAYGIAO, NHAMAY, Note
FROM dbo.TMPPHIEUGIAOHANG WHERE STT = @Stt AND Note = N'QC_G4_ACCEPTANCE_FIXTURE';
