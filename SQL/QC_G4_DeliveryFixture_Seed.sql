/*
  G4 acceptance fixture for the disposable CI database.

  This script owns ONLY deterministic QC rows. It does not read or copy
  business data from DB57/DB14. It is safe only because the workflow runs it
  against PCTP_QC_CI after the DB57-derived schema has been created.

  Identity: STT=32001 + Note=QC_G4_ACCEPTANCE_FIXTURE.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PCTP_QC_CI'
    THROW 51001, N'Wrong database: G4 fixture must run only on PCTP_QC_CI.', 1;
IF @@SERVERNAME LIKE N'%BRAVO%'
    THROW 51002, N'Production server refused.', 1;

DECLARE @Stt smallint = 32001,
        @Lot nvarchar(50) = N'26071303124101051499',
        @Part nvarchar(50) = N'QC-G4-ITEM-001',
        @Qty smallint = 10;

IF EXISTS (SELECT 1 FROM dbo.TMPPHIEUGIAOHANG WHERE STT = @Stt)
    THROW 51003, N'STT 32001 already exists. Run QC_G4_DeliveryFixture_Cleanup.sql first.', 1;

IF EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE LOT = @Lot)
    THROW 51004, N'Deterministic QC stock LOT already exists. Run cleanup or choose a new fixture identity.', 1;

BEGIN TRANSACTION;

INSERT INTO dbo.STOCKTP
    (LOT, PART, NAME, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP)
VALUES
    (@Lot, @Part, N'PCTP QC G4 fixture', 10, 32000, 32000, 0, N'QC_FIXTURE', @Part);

IF @@ROWCOUNT <> 1
    THROW 51005, N'Fixture stock insert did not affect exactly one row.', 1;

INSERT INTO dbo.TMPPHIEUGIAOHANG
    (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, NHAMAY, Note)
VALUES
    (@Stt, N'QCG4', N'QCG4-FIXTURE', @Part, N'QC G4 acceptance fixture', @Lot + N'-' + CONVERT(nvarchar(10), @Qty),
     N'PCS', @Qty, CAST(GETDATE() AS date), N'23:59', N'PENDING', 'QCG4', N'QC_G4_ACCEPTANCE_FIXTURE');

IF @@ROWCOUNT <> 1
    THROW 51006, N'Fixture TMP insert did not affect exactly one row.', 1;

COMMIT TRANSACTION;

SELECT STT, LOT, MAHANG, SOLUONG, STATUS, NGAYGIAO, NHAMAY, Note
FROM dbo.TMPPHIEUGIAOHANG
WHERE STT = @Stt AND Note = N'QC_G4_ACCEPTANCE_FIXTURE';
