-- g6-delivery-hvn-multilot: pre-scan postcondition (read-only). After a real scan these numbers change on purpose.
-- Fixture shape = profile shape: 3 lines (1/2/3 LOT), 6 lots, ONE STOCKTP row per LOT root13.
SELECT
    (SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS TmpRows,
    (SELECT ISNULL(SUM(SOLUONG), 0) FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS TmpQty,
    (SELECT COUNT(*) FROM dbo.TMPPHIEUGIAOHANG WHERE Note LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%' AND LOT LIKE N'%,%') AS TmpMultiLotRows,
    (SELECT COUNT(*) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockRows,
    (SELECT ISNULL(SUM(SLCONLAI), 0) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockRemaining,
    (SELECT COUNT(*) FROM (SELECT LEFT(LOT, 13) AS R13 FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
                           GROUP BY LEFT(LOT, 13) HAVING COUNT(*) > 1) d) AS DuplicateRoot13,
    (SELECT COUNT(*) FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
        AND (SLNHAP - SLXUAT <> SLCONLAI OR SLCONLAI < 0 OR SLXUAT > SLNHAP)) AS StockInvariantViolations,
    (SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG WHERE MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS DeliveredRows,
    (SELECT COUNT(*) FROM dbo.DOCQRCODE WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS PendingQrRows;
