-- g6-receive seed (DB57 live-data harness).
-- Creates ONE finished-goods production document pair that appears in dbo.vNhapTP, plus two empty QC slots.
-- Every row is keyed by @Prefix (QCUI_<runId8>): item code, rack and warehouse names all start with it,
-- so cleanup.sql can remove exactly this run's data. No existing business row is read-modified.
-- Chain: B20Item -> B30AccDoc (x2) -> vNhapTP worklist row -> (scan tem tong) -> STOCKTP / SlotLot.
-- NOTE: the runner counts affected rows, so do NOT add SET NOCOUNT ON here.

IF @@SERVERNAME LIKE N'%BRAVO%' THROW 52000, N'g6-receive seed: production server refused.', 1;
IF @Prefix NOT LIKE N'QCUI[_]%' OR LEN(@Prefix) < 10 THROW 52001, N'g6-receive seed: @Prefix must look like QCUI_<runId>.', 1;
IF OBJECT_ID(N'dbo.B30AccDoc', N'U') IS NULL THROW 52004, N'g6-receive seed: dbo.B30AccDoc not found on this database.', 1;
IF OBJECT_ID(N'dbo.vNhapTP', N'V') IS NULL THROW 52005, N'g6-receive seed: dbo.vNhapTP not found on this database.', 1;

DECLARE @Item nvarchar(25) = @Prefix + N'_FG';
IF EXISTS (SELECT 1 FROM dbo.B20Item WHERE Code = @Item) THROW 52002, N'g6-receive seed: item already exists for this prefix.', 1;

-- 1) Item master. Every other B20Item column has a default (see SQL/QC_Schema/02_Generated_From_DB57.sql).
INSERT INTO dbo.B20Item (Code, Name, Model, Unit)
VALUES (@Item, N'QC fixture ' + @Prefix, N'QCMODEL', N'PCS');

DECLARE @ItemId int = (SELECT Id FROM dbo.B20Item WHERE Code = @Item);
IF @ItemId IS NULL OR @ItemId > 99999 THROW 52003, N'g6-receive seed: item Id missing or does not fit the 5-digit LOT field.', 1;

-- 2) LOT key = YYMMDD + Id(5) + Shift(1) + Gear(1) + Line(3) + Machine(4) = 20 chars (see LotCodeHelper).
--    Printed tem LOT = 20-char key + Counter(4) + Qty(4) = 28 chars. Unique because the item Id is new.
DECLARE @Today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
DECLARE @Lot20 nvarchar(20) = CONVERT(char(6), @Today, 12) + RIGHT(N'00000' + CAST(@ItemId AS nvarchar(10)), 5) + N'1' + N'0' + N'999' + N'9999';
IF EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE LOT = @Lot20) THROW 52006, N'g6-receive seed: STOCKTP already has this QC LOT.', 1;

-- 3) Production documents read by vNhapTP. The real DDL of dbo.B30AccDoc is not in the repo, so the INSERT
--    is generated from sys.columns: known columns get explicit values, any other NOT NULL column without a
--    default gets a neutral value for its type; unsupported types stop the seed with a clear message.
DECLARE @explicit TABLE (ColName sysname NOT NULL PRIMARY KEY, ValExpr nvarchar(100) NOT NULL);
INSERT INTO @explicit (ColName, ValExpr) VALUES
    (N'DocDate', N'@DocDate'), (N'ShiftCode', N'@Shift'), (N'Model', N'@Model'),
    (N'ReceiptItemCode', N'@Item'), (N'ReceiptQuantity', N'@Qty'), (N'IsActive', N'1'),
    (N'MachinesCode', N'@Machine'), (N'GearCode', N'0'), (N'ItemLotCodeTP', N'@LotTP');

DECLARE @missing nvarchar(400) = STUFF((
    SELECT N',' + e.ColName FROM @explicit e
    WHERE NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.B30AccDoc') AND c.name = e.ColName)
    FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(400)'), 1, 1, N'');
DECLARE @msg nvarchar(2048);
IF @missing IS NOT NULL
BEGIN
    SET @msg = N'g6-receive seed: dbo.B30AccDoc lacks columns read by the harness: ' + @missing;
    THROW 52007, @msg, 1;
END;

DECLARE @cols nvarchar(max), @vals nvarchar(max), @unsupported nvarchar(400);
;WITH c AS (
    SELECT col.name, col.column_id,
           CASE WHEN ex.ColName IS NOT NULL THEN ex.ValExpr
                WHEN ty.name IN (N'char', N'varchar', N'nchar', N'nvarchar') THEN N''''''
                WHEN ty.name IN (N'int', N'bigint', N'smallint', N'tinyint', N'bit', N'decimal', N'numeric', N'float', N'real', N'money', N'smallmoney') THEN N'0'
                WHEN ty.name IN (N'datetime', N'smalldatetime', N'date', N'datetime2') THEN N'GETDATE()'
                WHEN ty.name = N'uniqueidentifier' THEN N'NEWID()'
                ELSE NULL END AS ValExpr
    FROM sys.columns col
    JOIN sys.types ty ON ty.user_type_id = col.system_type_id
    LEFT JOIN @explicit ex ON ex.ColName = col.name
    WHERE col.object_id = OBJECT_ID(N'dbo.B30AccDoc')
      AND col.is_identity = 0 AND col.is_computed = 0 AND ty.name <> N'timestamp'
      AND (ex.ColName IS NOT NULL OR (col.is_nullable = 0 AND col.default_object_id = 0))
)
SELECT
    @cols = STUFF((SELECT N',' + QUOTENAME(name) FROM c WHERE ValExpr IS NOT NULL ORDER BY column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N''),
    @vals = STUFF((SELECT N',' + ValExpr FROM c WHERE ValExpr IS NOT NULL ORDER BY column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N''),
    @unsupported = STUFF((SELECT N',' + name FROM c WHERE ValExpr IS NULL ORDER BY column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(400)'), 1, 1, N'');
IF @unsupported IS NOT NULL
BEGIN
    SET @msg = N'g6-receive seed: dbo.B30AccDoc has NOT NULL columns without default of an unsupported type: ' + @unsupported;
    THROW 52008, @msg, 1;
END;

DECLARE @sql nvarchar(max) = N'INSERT INTO dbo.B30AccDoc (' + @cols + N') VALUES (' + @vals + N');';
DECLARE @Qty numeric(15, 4) = 10;
DECLARE @n int = 1;
DECLARE @LotTP nvarchar(50);
WHILE @n <= 2
BEGIN
    SET @LotTP = @Lot20 + RIGHT(N'0000' + CAST(@n AS nvarchar(4)), 4) + RIGHT(N'0000' + CAST(CAST(@Qty AS int) AS nvarchar(4)), 4);
    EXEC sys.sp_executesql @sql,
        N'@DocDate datetime, @Shift nvarchar(10), @Model nvarchar(25), @Item nvarchar(25), @Qty numeric(15,4), @Machine nvarchar(10), @LotTP nvarchar(50)',
        @DocDate = @Today, @Shift = N'1', @Model = N'QCMODEL', @Item = @Item, @Qty = @Qty, @Machine = N'9999', @LotTP = @LotTP;
    SET @n = @n + 1;
END;

-- 4) Warehouse / rack / two empty slots, so the scan dialog has a target slot that belongs to this run.
INSERT INTO dbo.Warehouse (Name) VALUES (@Prefix + N'_WH');
DECLARE @WhId int = (SELECT WarehouseId FROM dbo.Warehouse WHERE Name = @Prefix + N'_WH');
INSERT INTO dbo.Rack (WarehouseId, RackName, RackRowCount, ColumnCount) VALUES (@WhId, @Prefix + N'_RK', 1, 2);
DECLARE @RackId int = (SELECT RackId FROM dbo.Rack WHERE RackName = @Prefix + N'_RK');
INSERT INTO dbo.Slot (RackId, SlotNumber, IsOccupied, Capacity, RowIndex, ColumnIndex)
VALUES (@RackId, 1, 0, 1000, 0, 0), (@RackId, 2, 0, 1000, 0, 1);


-- Capture two valid tem-tong QR inputs for the real FormEnterItemSV UI.
-- The returned values are inputs only; STOCKTP/SlotLot/StockHistory must be created by the real receiving service.
;WITH Q0 AS (
    SELECT RTRIM(a.ItemLotCodeTP) + N':' + RTRIM(a.ReceiptItemCode) + N':' +
           CONVERT(nvarchar(10), a.DocDate, 103) + N':' +
           CAST(CAST(a.ReceiptQuantity AS int) AS nvarchar(10)) + N':' +
           @Prefix + N'_T1:' + @Prefix + N'_P1 AS Qr,
           ROW_NUMBER() OVER (ORDER BY a.ItemLotCodeTP) AS rn
    FROM dbo.B30AccDoc a
    WHERE a.ReceiptItemCode = @Item
), Q AS (
    SELECT MAX(CASE WHEN rn = 1 THEN Qr END) AS Qr1,
           MAX(CASE WHEN rn = 2 THEN Qr END) AS Qr2,
           COUNT(*) AS QrCount
    FROM Q0
)
SELECT
    (SELECT COUNT(*) FROM dbo.B20Item WHERE Code = @Item)
  + (SELECT COUNT(*) FROM dbo.B30AccDoc WHERE ReceiptItemCode = @Item)
  + (SELECT COUNT(*) FROM dbo.Warehouse WHERE Name = @Prefix + N'_WH')
  + (SELECT COUNT(*) FROM dbo.Rack WHERE RackName = @Prefix + N'_RK')
  + (SELECT COUNT(*) FROM dbo.Slot s JOIN dbo.Rack r ON r.RackId = s.RackId WHERE r.RackName = @Prefix + N'_RK') AS SeedRowsAffected,
  Qr1, Qr2
FROM Q
WHERE QrCount = 2;
