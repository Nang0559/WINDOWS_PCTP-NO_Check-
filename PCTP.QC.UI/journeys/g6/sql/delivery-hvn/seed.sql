-- g6-delivery-hvn seed (DB57 live-data harness): ONE pending delivery line + its stock LOT, all keyed by @Prefix.
-- Chain after a real scan (not automated): TMPPHIEUGIAOHANG line + QR tem thung -> Usp_Qrcode_Update_Stock2405
--   -> STOCKTP (SLCONLAI down) + LUUPHIEUGIAOHANG + LUUDOCQRCODE; DOCQRCODE rows are consumed by the procedure.
-- Item code uses the suffix _DL so it never collides with the receive journey (_FG) for the same prefix.
-- NOTE: the runner counts affected rows, so do NOT add SET NOCOUNT ON here.

IF @@SERVERNAME LIKE N'%BRAVO%' THROW 52100, N'g6-delivery-hvn seed: production server refused.', 1;
IF @Prefix NOT LIKE N'QCUI[_]%' OR LEN(@Prefix) < 10 THROW 52101, N'g6-delivery-hvn seed: @Prefix must look like QCUI_<runId>.', 1;
IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANG', N'U') IS NULL OR OBJECT_ID(N'dbo.STOCKTP', N'U') IS NULL OR OBJECT_ID(N'dbo.B20Item', N'U') IS NULL
    THROW 52102, N'g6-delivery-hvn seed: TMPPHIEUGIAOHANG / STOCKTP / B20Item not found on this database.', 1;

DECLARE @Item nvarchar(25) = @Prefix + N'_DL';
IF EXISTS (SELECT 1 FROM dbo.B20Item WHERE Code = @Item) THROW 52103, N'g6-delivery-hvn seed: item already exists for this prefix.', 1;

INSERT INTO dbo.B20Item (Code, Name, Model, Unit) VALUES (@Item, N'QC fixture ' + @Prefix, N'QCMODEL', N'PCS');
DECLARE @ItemId int = (SELECT Id FROM dbo.B20Item WHERE Code = @Item);
IF @ItemId IS NULL OR @ItemId > 99999 THROW 52104, N'g6-delivery-hvn seed: item Id missing or does not fit the 5-digit LOT field.', 1;

-- LOT key (20 chars) = YYMMDD + Id(5) + Shift(1) + Gear(1) + Line(3) + Machine(4); unique because the item Id is new.
DECLARE @Today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
DECLARE @Lot20 nvarchar(20) = CONVERT(char(6), @Today, 12) + RIGHT(N'00000' + CAST(@ItemId AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
IF EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE LOT = @Lot20) THROW 52105, N'g6-delivery-hvn seed: STOCKTP already has this QC LOT.', 1;

DECLARE @Qty int = 10;
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP)
VALUES (@Lot20, @Item, N'QC fixture ' + @Prefix, @Today, @Qty, @Qty, @Qty, 0, N'QC_FIXTURE', @Item);

-- STT is smallint and not an identity: take the next free value in a reserved QC range (30000..32500).
DECLARE @Stt int = ISNULL((SELECT MAX(STT) FROM dbo.TMPPHIEUGIAOHANG WHERE STT BETWEEN 30000 AND 32500), 29999) + 1;
IF @Stt > 32500 THROW 52106, N'g6-delivery-hvn seed: reserved STT range 30000..32500 is exhausted.', 1;

-- Same shape as SQL/QC_G4_DeliveryFixture_Seed.sql (verified against the DB57-derived schema): LOT column = LOT20 + '-' + qty.
INSERT INTO dbo.TMPPHIEUGIAOHANG (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, NHAMAY, Note)
VALUES (@Stt, @Prefix, @Prefix, @Item, N'QC fixture ' + @Prefix, N'',
        N'PCS', @Qty, CAST(GETDATE() AS date), N'23:59', N'PENDING', 'QCUI', @Prefix + N'_DEL');


-- Capture the run-scoped carton QR for the real UI. This returns input only; no delivery/QR result table is written here.
SELECT CAST(3 AS int) AS SeedRowsAffected,
       RTRIM(@Lot20) + N'0001' + RIGHT(N'0000' + CAST(@Qty AS nvarchar(4)), 4) + N':' + RTRIM(@Item) + N':' +
       CONVERT(nvarchar(10), @Today, 103) + N':' + CAST(@Qty AS nvarchar(10)) AS Qr1;
