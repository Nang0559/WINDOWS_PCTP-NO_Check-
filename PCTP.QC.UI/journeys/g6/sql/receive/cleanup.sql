-- Cleanup only rows tied to this run's unique Model / LOT / QR. Never touch master item data.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @FullLot nvarchar(50);
DECLARE @BaseLot nvarchar(50);
DECLARE @ItemCode nvarchar(50);
DECLARE @DocDate datetime;
DECLARE @Qr nvarchar(400);
DECLARE @SlotIds TABLE (SlotId int NOT NULL PRIMARY KEY);

SELECT TOP (1)
    @FullLot=ItemLotCodeTP,
    @BaseLot=LEFT(ItemLotCodeTP,20),
    @ItemCode=UPPER(LTRIM(RTRIM(ReceiptItemCode))),
    @DocDate=DocDate
FROM dbo.B30AccDoc
WHERE Model=@Prefix AND ItemLotCodeTP<>N''
ORDER BY DocDate DESC;

IF @FullLot IS NOT NULL
BEGIN
    SET @Qr=@FullLot+N':'+@ItemCode+N':'+CONVERT(char(10),@DocDate,103)+N':8:1:'+@Prefix;

    INSERT @SlotIds(SlotId)
    SELECT DISTINCT SlotId FROM dbo.SlotLot
    WHERE LotNo=@BaseLot OR QrData=@Qr;

    DELETE FROM dbo.StockHistory
    WHERE LotNo=@BaseLot OR QrData=@Qr OR TemCode=@Prefix;

    DELETE FROM dbo.SlotLot
    WHERE LotNo=@BaseLot OR QrData=@Qr OR TemCode=@Prefix;

    -- Rebuild only affected Slot summary columns from the surviving SlotLot rows.
    UPDATE s
    SET s.Quantity=ISNULL(x.TotalQty,0),
        s.IsOccupied=CASE WHEN ISNULL(x.TotalQty,0)>0 THEN 1 ELSE 0 END,
        s.ItemCode=CASE WHEN ISNULL(x.TotalQty,0)>0 THEN l.ItemCode ELSE NULL END,
        s.ImportDate=CASE WHEN ISNULL(x.TotalQty,0)>0 THEN x.MaxImportDate ELSE NULL END
    FROM dbo.Slot s
    INNER JOIN @SlotIds ids ON ids.SlotId=s.SlotId
    OUTER APPLY (
        SELECT SUM(Quantity) AS TotalQty, MAX(ImportDate) AS MaxImportDate
        FROM dbo.SlotLot
        WHERE SlotId=s.SlotId AND PhieuStatus=0
    ) x
    OUTER APPLY (
        SELECT TOP (1) ItemCode
        FROM dbo.SlotLot
        WHERE SlotId=s.SlotId AND PhieuStatus=0 AND Quantity>0
        ORDER BY ImportDate DESC, SlotLotId DESC
    ) l;

    DELETE FROM dbo.STOCKTP WHERE LOT=@BaseLot;

    IF OBJECT_ID(N'dbo.PCTPStockMovementGuard', N'U') IS NOT NULL
        DELETE FROM dbo.PCTPStockMovementGuard
        WHERE (ReferenceType=N'NHAP_TP' AND ReferenceId=@Prefix)
           OR QrHash=HASHBYTES('SHA2_256', CONVERT(varbinary(max), @Qr))
           OR TemHash=HASHBYTES('SHA2_256', CONVERT(varbinary(max), @Prefix));

    DELETE FROM dbo.B30AccDoc
    WHERE Model=@Prefix AND ItemLotCodeTP=@FullLot;
END;
