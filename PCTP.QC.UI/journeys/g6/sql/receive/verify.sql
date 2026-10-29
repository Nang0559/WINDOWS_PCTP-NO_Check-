-- g6-receive: Remaining must be 0 after cleanup.
SELECT
    (SELECT COUNT(*) FROM dbo.StockHistory WHERE ItemCode LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.SlotLot WHERE ItemCode LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.Slot s JOIN dbo.Rack r ON r.RackId = s.RackId WHERE r.RackName LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.Rack WHERE RackName LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.Warehouse WHERE Name LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.STOCKTP WHERE PART LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.B30AccDoc WHERE ReceiptItemCode LIKE @Prefix + N'%')
  + (SELECT COUNT(*) FROM dbo.B20Item WHERE Code LIKE @Prefix + N'%') AS Remaining;
