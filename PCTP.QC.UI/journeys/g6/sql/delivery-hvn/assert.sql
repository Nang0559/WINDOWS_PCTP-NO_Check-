-- g6-delivery-hvn: pre-scan postcondition (read-only). After a real scan these numbers change on purpose.
SELECT
    (SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS TmpRows,
    (SELECT ISNULL(SUM(SOLUONG), 0) FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS TmpQty,
    (SELECT COUNT(*) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockRows,
    (SELECT ISNULL(SUM(SLCONLAI), 0) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockRemaining,
    (SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG WHERE MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS DeliveredRows,
    (SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS PendingQrRows;
