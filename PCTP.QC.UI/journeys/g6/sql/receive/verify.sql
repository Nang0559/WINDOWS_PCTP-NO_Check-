-- Cleanup verification must return Remaining=0, even if the UI failed before seeding.
SELECT
  (SELECT COUNT(1) FROM dbo.B30AccDoc WHERE Model=@Prefix) +
  (SELECT COUNT(1) FROM dbo.STOCKTP WHERE SUBSTRING(LOT,14,7)=LEFT(@RunId,7)) +
  (SELECT COUNT(1) FROM dbo.SlotLot WHERE TemCode=@Prefix OR QrData LIKE N'%:'+@Prefix) +
  (SELECT COUNT(1) FROM dbo.StockHistory WHERE TemCode=@Prefix OR QrData LIKE N'%:'+@Prefix) +
  (SELECT COUNT(1) FROM dbo.PCTPStockMovementGuard WHERE ReferenceId=@Prefix) AS Remaining;
