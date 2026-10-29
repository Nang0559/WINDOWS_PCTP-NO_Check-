-- Assert the actual receiving UI committed exactly one run-scoped LOT/QR into stock.
SET NOCOUNT ON;
DECLARE @Source TABLE (BaseLot nvarchar(50), FullLot nvarchar(50), ItemCode nvarchar(50), DocDate datetime);
INSERT @Source(BaseLot, FullLot, ItemCode, DocDate)
SELECT LEFT(ItemLotCodeTP, 20), ItemLotCodeTP, UPPER(LTRIM(RTRIM(ReceiptItemCode))), DocDate
FROM dbo.B30AccDoc
WHERE Model = @Prefix AND IsActive <> 0 AND ItemLotCodeTP <> N'';

SELECT
  (SELECT COUNT(1) FROM dbo.B30AccDoc WHERE Model=@Prefix AND IsActive<>0) AS SourceRows,
  (SELECT COUNT(1) FROM dbo.STOCKTP s INNER JOIN @Source x ON x.BaseLot=s.LOT WHERE s.SLNHAP=8 AND s.SLCONLAI=8) AS StockRows,
  (SELECT COUNT(1) FROM dbo.SlotLot sl INNER JOIN @Source x ON x.BaseLot=sl.LotNo WHERE sl.TemCode=@Prefix AND sl.Quantity=8 AND sl.PhieuStatus=0) AS SlotLotRows,
  (SELECT COUNT(1) FROM dbo.StockHistory h INNER JOIN @Source x ON x.BaseLot=h.LotNo WHERE h.ActionType='IMPORT' AND h.TemCode=@Prefix) AS ImportHistoryRows,
  (SELECT ISNULL(MAX(s.SLNHAP),0) FROM dbo.STOCKTP s INNER JOIN @Source x ON x.BaseLot=s.LOT ) AS ReceivedQty,
  (SELECT ISNULL(MAX(s.SLCONLAI),0) FROM dbo.STOCKTP s INNER JOIN @Source x ON x.BaseLot=s.LOT ) AS RemainingQty,
  (SELECT COUNT(1) FROM dbo.PCTPStockMovementGuard g WHERE g.ReferenceType=N'NHAP_TP' AND g.ReferenceId=@Prefix) AS MovementGuardRows;
