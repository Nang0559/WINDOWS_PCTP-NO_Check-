-- g6-receive cleanup: child tables first. Every statement is scoped by @Prefix (item code / rack / warehouse name).
-- Rows created by a real UI scan (STOCKTP, SlotLot, StockHistory) carry the QC item code, so they are removed too.
DELETE FROM dbo.StockHistory WHERE ItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.SlotLot WHERE ItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%' OR SlotId IN (SELECT s.SlotId FROM dbo.Slot s JOIN dbo.Rack r ON r.RackId = s.RackId WHERE r.RackName LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%');
DELETE FROM dbo.Slot WHERE RackId IN (SELECT RackId FROM dbo.Rack WHERE RackName LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%');
DELETE FROM dbo.Rack WHERE RackName LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.Warehouse WHERE Name LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.B30AccDoc WHERE ReceiptItemCode LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.B20Item WHERE Code LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
