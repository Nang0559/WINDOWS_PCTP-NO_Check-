-- g6-delivery-hvn cleanup: child/result tables first. Every statement is scoped by @Prefix (item code / Note / CUA).
-- Rows written by a real delivery scan (LUUPHIEUGIAOHANG, LUUDOCQRCODE, DOCQRCODE, STOCKTP) carry the QC item code.
-- NOT covered (no prefix link): dbo.PCTPStockMovementGuard claims and dbo.FVN_DeliveryCommand* ledger rows written by a real scan.
DELETE FROM dbo.LUUDOCQRCODE WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.DOCQRCODE WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.LUUPHIEUGIAOHANG WHERE MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%' OR CUA = @Prefix;
DELETE FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%' OR CUA = @Prefix OR MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
DELETE FROM dbo.B20Item WHERE Code LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%';
