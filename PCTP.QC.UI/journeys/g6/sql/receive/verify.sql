-- g6-receive: Remaining must be 0 after cleanup.
SELECT
    (SELECT COUNT(*) FROM dbo.StockHistory WHERE ItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.SlotLot WHERE ItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.Slot s JOIN dbo.Rack r ON r.RackId = s.RackId WHERE r.RackName LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.Rack WHERE RackName LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.Warehouse WHERE Name LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.B30AccDoc WHERE ReceiptItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%')
  + (SELECT COUNT(*) FROM dbo.B20Item WHERE Code LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS Remaining;
