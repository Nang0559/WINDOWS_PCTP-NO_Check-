-- g6-receive cleanup: child tables first. Every statement is scoped by @Prefix (item code / rack / warehouse name).
-- Rows created by a real UI scan (STOCKTP, SlotLot, StockHistory) carry the QC item code, so they are removed too.
DELETE FROM dbo.StockHistory WHERE ItemCode LIKE @Prefix + N'%';
DELETE FROM dbo.SlotLot WHERE ItemCode LIKE @Prefix + N'%' OR SlotId IN (SELECT s.SlotId FROM dbo.Slot s JOIN dbo.Rack r ON r.RackId = s.RackId WHERE r.RackName LIKE @Prefix + N'%');
DELETE FROM dbo.Slot WHERE RackId IN (SELECT RackId FROM dbo.Rack WHERE RackName LIKE @Prefix + N'%');
DELETE FROM dbo.Rack WHERE RackName LIKE @Prefix + N'%';
DELETE FROM dbo.Warehouse WHERE Name LIKE @Prefix + N'%';
DELETE FROM dbo.STOCKTP WHERE PART LIKE @Prefix + N'%';
DELETE FROM dbo.B30AccDoc WHERE ReceiptItemCode LIKE @Prefix + N'%';
DELETE FROM dbo.B20Item WHERE Code LIKE @Prefix + N'%';
