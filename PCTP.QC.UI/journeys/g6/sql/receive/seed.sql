-- Live DB57 G6 receive fixture. The seed clones one valid B30AccDoc row so ERP-required columns/defaults stay valid,
-- but gives this run its own Model + LOT/QR identity. Only the cloned row is inserted; B20Item is read-only.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Qty int = 8;
DECLARE @DocDate datetime = GETDATE();
DECLARE @Model nvarchar(30) = @Prefix;
DECLARE @ShiftCode nvarchar(10) = N'9';
DECLARE @MachinesCode nvarchar(10) = SUBSTRING(@RunId, 4, 4);
DECLARE @GearCode int = 0;
DECLARE @ItemCode nvarchar(50);
DECLARE @ItemId int;
DECLARE @BaseLot nvarchar(50);
DECLARE @FullLot nvarchar(50);
DECLARE @Qr nvarchar(400);
DECLARE @Columns nvarchar(max);
DECLARE @Expressions nvarchar(max);
DECLARE @InsertSql nvarchar(max);
DECLARE @InsertedRows int = 0;

IF OBJECT_ID(N'dbo.B30AccDoc', N'U') IS NULL
    THROW 51001, N'G6 receive blocked: dbo.B30AccDoc is missing.', 1;
IF OBJECT_ID(N'dbo.B20Item', N'U') IS NULL
    THROW 51002, N'G6 receive blocked: dbo.B20Item is missing.', 1;
IF OBJECT_ID(N'dbo.vNhapTP', N'V') IS NULL
    THROW 51003, N'G6 receive blocked: dbo.vNhapTP is missing.', 1;
IF OBJECT_ID(N'dbo.PCTPStockMovementGuard', N'U') IS NULL
    THROW 51011, N'G6 receive blocked: dbo.PCTPStockMovementGuard is missing; the real receiving transaction requires the movement idempotency guard.', 1;

IF EXISTS (SELECT 1 FROM dbo.B30AccDoc WHERE Model = @Prefix)
    THROW 51004, N'G6 receive blocked: this run prefix already exists in B30AccDoc; refusing to overwrite/reuse it.', 1;

-- Select a real active item and a valid production row only as a template for required ERP columns.
-- Do not mutate the selected source row or the B20Item master.
SELECT TOP (1) a.*, i.Id AS __PCTPQC_ItemId
INTO #PCTPQC_Source
FROM dbo.B30AccDoc AS a
INNER JOIN dbo.B20Item AS i ON i.Code = a.ReceiptItemCode
WHERE ISNULL(a.IsActive, 0) <> 0
  AND ISNULL(a.ReceiptQuantity, 0) > 0
  AND ISNULL(a.ItemLotCodeTP, N'') <> N''
  AND ISNULL(i.IsActive, 1) = 1
  AND ISNULL(i.IsGroup, 0) = 0
  AND i.Id BETWEEN 1 AND 99999
ORDER BY a.DocDate DESC;

IF NOT EXISTS (SELECT 1 FROM #PCTPQC_Source)
    THROW 51005, N'G6 receive blocked: no active B30AccDoc/B20Item row is available as a safe schema-compatible template.', 1;

SELECT TOP (1)
    @ItemCode = UPPER(LTRIM(RTRIM(ReceiptItemCode))),
    @ItemId = __PCTPQC_ItemId
FROM #PCTPQC_Source;

-- LOT layout: YYMMDD + ItemId(5) + Shift(1) + Gear(1) + run suffix(7) + counter(4) + qty(4).
-- vNhapTP exposes the first 20 characters as LOT_NO/FIND. The full 28-char value is the input QR LOT.
SET @BaseLot = CONVERT(char(6), @DocDate, 12)
    + RIGHT('00000' + CONVERT(varchar(5), @ItemId), 5)
    + @ShiftCode + CONVERT(char(1), @GearCode)
    + LEFT(@RunId, 3) + SUBSTRING(@RunId, 4, 4);
SET @FullLot = @BaseLot + SUBSTRING(@RunId, 8, 4) + RIGHT('0000' + CONVERT(varchar(4), @Qty), 4);
SET @Qr = @FullLot + N':' + @ItemCode + N':' + CONVERT(char(10), @DocDate, 103)
    + N':' + CONVERT(nvarchar(10), @Qty) + N':1:' + @Prefix;

IF LEN(@BaseLot) <> 20 OR LEN(@FullLot) <> 28
    THROW 51006, N'G6 receive blocked: generated LOT format is not 20/28 characters.', 1;
IF EXISTS (SELECT 1 FROM dbo.STOCKTP WHERE LOT = @BaseLot)
   OR EXISTS (SELECT 1 FROM dbo.SlotLot WHERE LotNo = @BaseLot)
   OR EXISTS (SELECT 1 FROM dbo.B30AccDoc WHERE ItemLotCodeTP = @FullLot)
    THROW 51007, N'G6 receive blocked: generated LOT/QR collides with existing business data.', 1;

-- Clone every writable column from the valid source row, overriding only the columns needed
-- to make this row unique and visible to vNhapTP. Identity/computed/rowversion columns are omitted.
IF EXISTS (
    SELECT required.ColumnName
    FROM (VALUES (N'DocDate'),(N'ShiftCode'),(N'Model'),(N'ReceiptItemCode'),
                 (N'ReceiptQuantity'),(N'IsActive'),(N'MachinesCode'),(N'GearCode'),(N'ItemLotCodeTP')) required(ColumnName)
    WHERE NOT EXISTS (
        SELECT 1 FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.B30AccDoc') AND c.name = required.ColumnName
    )
)
    THROW 51008, N'G6 receive blocked: B30AccDoc is missing one of the expected vNhapTP columns.', 1;

SELECT @Columns = STUFF((
    SELECT N',' + QUOTENAME(c.name)
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.B30AccDoc')
      AND c.is_identity = 0 AND c.is_computed = 0 AND c.system_type_id <> 189
    ORDER BY c.column_id
    FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N'');

SELECT @Expressions = STUFF((
    SELECT N',' + CASE c.name
        WHEN N'DocDate' THEN N'@DocDate'
        WHEN N'ShiftCode' THEN N'@ShiftCode'
        WHEN N'Model' THEN N'@Model'
        WHEN N'ReceiptItemCode' THEN N'@ItemCode'
        WHEN N'ReceiptQuantity' THEN N'@Qty'
        WHEN N'IsActive' THEN N'CAST(1 AS bit)'
        WHEN N'MachinesCode' THEN N'@MachinesCode'
        WHEN N'GearCode' THEN N'@GearCode'
        WHEN N'ItemLotCodeTP' THEN N'@FullLot'
        ELSE N'src.' + QUOTENAME(c.name)
    END
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.B30AccDoc')
      AND c.is_identity = 0 AND c.is_computed = 0 AND c.system_type_id <> 189
    ORDER BY c.column_id
    FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N'');

IF NULLIF(@Columns, N'') IS NULL OR NULLIF(@Expressions, N'') IS NULL
    THROW 51009, N'G6 receive blocked: unable to build the schema-aware insert.', 1;

SET @InsertSql = N'INSERT INTO dbo.B30AccDoc (' + @Columns + N')
    SELECT ' + @Expressions + N' FROM #PCTPQC_Source AS src;
    SET @InsertedRows = @@ROWCOUNT;';

EXEC sys.sp_executesql
    @InsertSql,
    N'@DocDate datetime, @ShiftCode nvarchar(10), @Model nvarchar(30), @ItemCode nvarchar(50),
      @Qty int, @MachinesCode nvarchar(10), @GearCode int, @FullLot nvarchar(50), @InsertedRows int OUTPUT',
    @DocDate=@DocDate, @ShiftCode=@ShiftCode, @Model=@Model, @ItemCode=@ItemCode,
    @Qty=@Qty, @MachinesCode=@MachinesCode, @GearCode=@GearCode, @FullLot=@FullLot,
    @InsertedRows=@InsertedRows OUTPUT;

IF @InsertedRows <> 1
    THROW 51010, N'G6 receive blocked: expected exactly one synthetic B30AccDoc row.', 1;
IF NOT EXISTS (
    SELECT 1 FROM dbo.vNhapTP
    WHERE FIND=@BaseLot AND MA_SAN_PHAM=@ItemCode
      AND ISNULL(SL_DA_SAN_XUAT,0)=@Qty AND ISNULL(SL_DA_NHAP,0)=0
)
    THROW 51012, N'G6 receive blocked: the synthetic row did not appear as one pending row in vNhapTP.', 1;

SELECT @InsertedRows AS SeedRowsAffected,
       @ItemCode AS itemCode,
       @BaseLot AS baseLot,
       @FullLot AS fullLot,
       @Qr AS qr,
       CONVERT(char(10), @DocDate, 103) AS productionDate,
       @Qty AS quantity;
