-- g6-delivery-hvn-multilot seed (DB57 live-data harness): profile-shaped delivery fixture, all keyed by @Prefix.
-- Shape taken from the SQL14 profile of 2026-10-28 (counts only, no raw values):
--   * pending TMPPHIEUGIAOHANG lines come in 3 shapes: 1 LOT (70%), 2 LOT (26%), 3 LOT (4%); the multi-LOT shape is
--     stored as 'LOT20-qty,LOT20-qty' in TMPPHIEUGIAOHANG.LOT and copied unchanged to LUUPHIEUGIAOHANG.LOT by the proc;
--   * STOCKTP has exactly ONE row per LOT root (first 13 chars = YYMMDD + item Id(5) + shift + gear), LOT stored as 20 chars;
--   * LUU <-> LUUQR <-> STOCKTP join on MAHANG + LEFT(LOT,13); STT is NOT a join key (LUUQR.STT is the scan-record number);
--   * STOCKTP always satisfies SLNHAP - SLXUAT = SLCONLAI, SLCONLAI >= 0, SLXUAT <= SLNHAP.
-- This seed writes ONLY prerequisites (item, STOCKTP lots, pending TMP lines). DOCQRCODE / LUUPHIEUGIAOHANG / LUUDOCQRCODE are
-- produced by the real UI scan through Usp_Qrcode_Update_Stock2405 (default tables TMPPHIEUGIAOHANG / DOCQRCODE = HVN path).
-- SLCONLAITMP and SATUS are working columns of other flows and are intentionally left at their defaults.
-- NOTE: the runner counts affected rows, so do NOT add SET NOCOUNT ON here. Expected affected rows: 3 items + 6 lots + 3 lines = 12.

IF @@SERVERNAME LIKE N'%BRAVO%' THROW 52200, N'g6-delivery-hvn-multilot seed: production server refused.', 1;
IF @Prefix NOT LIKE N'QCUI[_]%' OR LEN(@Prefix) < 10 THROW 52201, N'g6-delivery-hvn-multilot seed: @Prefix must look like QCUI_<runId>.', 1;
IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANG', N'U') IS NULL OR OBJECT_ID(N'dbo.STOCKTP', N'U') IS NULL OR OBJECT_ID(N'dbo.B20Item', N'U') IS NULL
    THROW 52202, N'g6-delivery-hvn-multilot seed: TMPPHIEUGIAOHANG / STOCKTP / B20Item not found on this database.', 1;

DECLARE @ItemA nvarchar(25) = @Prefix + N'_M1';   -- 1-LOT line
DECLARE @ItemB nvarchar(25) = @Prefix + N'_M2';   -- 2-LOT line
DECLARE @ItemC nvarchar(25) = @Prefix + N'_M3';   -- 3-LOT line
IF EXISTS (SELECT 1 FROM dbo.B20Item WHERE Code IN (@ItemA, @ItemB, @ItemC)) THROW 52203, N'g6-delivery-hvn-multilot seed: items already exist for this prefix.', 1;

INSERT INTO dbo.B20Item (Code, Name, Model, Unit) VALUES (@ItemA, N'QC fixture ' + @Prefix, N'QCMODEL', N'PCS');
INSERT INTO dbo.B20Item (Code, Name, Model, Unit) VALUES (@ItemB, N'QC fixture ' + @Prefix, N'QCMODEL', N'PCS');
INSERT INTO dbo.B20Item (Code, Name, Model, Unit) VALUES (@ItemC, N'QC fixture ' + @Prefix, N'QCMODEL', N'PCS');
DECLARE @IdA int = (SELECT Id FROM dbo.B20Item WHERE Code = @ItemA);
DECLARE @IdB int = (SELECT Id FROM dbo.B20Item WHERE Code = @ItemB);
DECLARE @IdC int = (SELECT Id FROM dbo.B20Item WHERE Code = @ItemC);
IF @IdA IS NULL OR @IdB IS NULL OR @IdC IS NULL OR @IdA > 99999 OR @IdB > 99999 OR @IdC > 99999
    THROW 52204, N'g6-delivery-hvn-multilot seed: item Id missing or does not fit the 5-digit LOT field.', 1;

-- LOT20 = YYMMDD + Id(5) + Shift(1) + Gear(1) + Line(3) + Machine(4). Root13 = first 13 chars must be unique per STOCKTP row, so lots of
-- the SAME item differ by production date (today, today-1, today-2) and every lot of this run carries the new item Id.
DECLARE @D0 datetime = CAST(CAST(GETDATE() AS date) AS datetime);
DECLARE @D1 datetime = DATEADD(DAY, -1, @D0);
DECLARE @D2 datetime = DATEADD(DAY, -2, @D0);
DECLARE @LotA1 nvarchar(20) = CONVERT(char(6), @D0, 12) + RIGHT(N'00000' + CAST(@IdA AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
DECLARE @LotB1 nvarchar(20) = CONVERT(char(6), @D0, 12) + RIGHT(N'00000' + CAST(@IdB AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
DECLARE @LotB2 nvarchar(20) = CONVERT(char(6), @D1, 12) + RIGHT(N'00000' + CAST(@IdB AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
DECLARE @LotC1 nvarchar(20) = CONVERT(char(6), @D0, 12) + RIGHT(N'00000' + CAST(@IdC AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
DECLARE @LotC2 nvarchar(20) = CONVERT(char(6), @D1, 12) + RIGHT(N'00000' + CAST(@IdC AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
DECLARE @LotC3 nvarchar(20) = CONVERT(char(6), @D2, 12) + RIGHT(N'00000' + CAST(@IdC AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
IF EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE LOT IN (@LotA1, @LotB1, @LotB2, @LotC1, @LotC2, @LotC3))
    THROW 52205, N'g6-delivery-hvn-multilot seed: STOCKTP already has a QC LOT of this run.', 1;

-- Stock (SLSX = SLNHAP = SLCONLAI, SLXUAT = 0, so SLNHAP - SLXUAT = SLCONLAI holds before the scan).
-- Each line asks for LESS than the stock on some lots so that SLCONLAI stays > 0 after the scan (like the live day: 112 lots, mixed remainders).
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP) VALUES (@LotA1, @ItemA, N'QC fixture ' + @Prefix, @D0, 10, 10, 10, 0, N'QC_FIXTURE', @ItemA);
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP) VALUES (@LotB1, @ItemB, N'QC fixture ' + @Prefix, @D0, 10, 10, 10, 0, N'QC_FIXTURE', @ItemB);
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP) VALUES (@LotB2, @ItemB, N'QC fixture ' + @Prefix, @D1, 8, 8, 8, 0, N'QC_FIXTURE', @ItemB);
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP) VALUES (@LotC1, @ItemC, N'QC fixture ' + @Prefix, @D0, 5, 5, 5, 0, N'QC_FIXTURE', @ItemC);
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP) VALUES (@LotC2, @ItemC, N'QC fixture ' + @Prefix, @D1, 5, 5, 5, 0, N'QC_FIXTURE', @ItemC);
INSERT INTO dbo.STOCKTP (LOT, PART, NAME, NGAYSX, SLSX, SLNHAP, SLCONLAI, SLXUAT, STATUSNHAP, SP) VALUES (@LotC3, @ItemC, N'QC fixture ' + @Prefix, @D2, 4, 4, 4, 0, N'QC_FIXTURE', @ItemC);

-- STT is smallint and not an identity: take 3 consecutive free values in the reserved QC range (30000..32500).
DECLARE @Stt int = ISNULL((SELECT MAX(STT) FROM dbo.TMPPHIEUGIAOHANG WHERE STT BETWEEN 30000 AND 32500), 29999) + 1;
IF @Stt + 2 > 32500 THROW 52206, N'g6-delivery-hvn-multilot seed: reserved STT range 30000..32500 is exhausted.', 1;

-- Pending lines. TMP.LOT = 'LOT20-qty' per element, comma separated; SOLUONG = sum of the element quantities.
--   line 1: 1 LOT  (10)            line 2: 2 LOT (6 + 4 = 10)            line 3: 3 LOT (3 + 3 + 2 = 8)
INSERT INTO dbo.TMPPHIEUGIAOHANG (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, NHAMAY, Note)
VALUES (@Stt, @Prefix, @Prefix, @ItemA, N'QC fixture ' + @Prefix, @LotA1 + N'-10', N'PCS', 10, CAST(GETDATE() AS date), N'23:59', N'PENDING', 'QCUI', @Prefix + N'_DEL1');
INSERT INTO dbo.TMPPHIEUGIAOHANG (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, NHAMAY, Note)
VALUES (@Stt + 1, @Prefix, @Prefix, @ItemB, N'QC fixture ' + @Prefix, @LotB1 + N'-6,' + @LotB2 + N'-4', N'PCS', 10, CAST(GETDATE() AS date), N'23:59', N'PENDING', 'QCUI', @Prefix + N'_DEL2');
INSERT INTO dbo.TMPPHIEUGIAOHANG (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, NHAMAY, Note)
VALUES (@Stt + 2, @Prefix, @Prefix, @ItemC, N'QC fixture ' + @Prefix, @LotC1 + N'-3,' + @LotC2 + N'-3,' + @LotC3 + N'-2', N'PCS', 8, CAST(GETDATE() AS date), N'23:59', N'PENDING', 'QCUI', @Prefix + N'_DEL3');


-- Return one row of run-local QR values to the UI runner. The runner captures only the declared aliases;
-- values are never written to logs. These are inputs for real UI scan steps, not pre-seeded business results.
;WITH Q0 AS (
    SELECT t.STT,
           RTRIM(s.LOT) + N'0001' + RIGHT(N'0000' + CAST(x.Qty AS nvarchar(4)), 4) + N':' + RTRIM(s.PART) + N':' +
             CONVERT(nvarchar(10), s.NGAYSX, 103) + N':' + CAST(x.Qty AS nvarchar(10)) AS Qr,
           ROW_NUMBER() OVER (ORDER BY t.STT, s.NGAYSX DESC, s.LOT) AS rn
    FROM dbo.STOCKTP s
    JOIN dbo.TMPPHIEUGIAOHANG t
      ON t.MAHANG = s.PART
     AND t.Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
     AND t.LOT LIKE N'%' + RTRIM(s.LOT) + N'-%'
    CROSS APPLY (SELECT CHARINDEX(RTRIM(s.LOT) + N'-', t.LOT) AS P) p
    CROSS APPLY (SELECT TRY_CAST(SUBSTRING(t.LOT, p.P + 21,
                        CHARINDEX(N',', t.LOT + N',', p.P) - (p.P + 21)) AS int) AS Qty) x
    WHERE s.PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
), Q AS (
    SELECT MAX(CASE WHEN rn = 1 THEN Qr END) AS Qr1,
           MAX(CASE WHEN rn = 2 THEN Qr END) AS Qr2,
           MAX(CASE WHEN rn = 3 THEN Qr END) AS Qr3,
           MAX(CASE WHEN rn = 4 THEN Qr END) AS Qr4,
           MAX(CASE WHEN rn = 5 THEN Qr END) AS Qr5,
           MAX(CASE WHEN rn = 6 THEN Qr END) AS Qr6,
           COUNT(*) AS QrCount
    FROM Q0
)
SELECT CAST(12 AS int) AS SeedRowsAffected, Qr1, Qr2, Qr3, Qr4, Qr5, Qr6
FROM Q
WHERE QrCount = 6;
