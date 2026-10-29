-- g6-delivery-hvn: read-only postconditions after the real QR input/Enter UI path.
-- The fixture has one LOT and one carton QR; all result rows must be created by the application/procedure.
SELECT
    (SELECT COUNT(*) FROM dbo.STOCKTP s
      WHERE s.PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
        AND (s.SLNHAP - s.SLXUAT <> s.SLCONLAI OR s.SLCONLAI < 0 OR s.SLXUAT > s.SLNHAP)) AS StockViolations,
    (SELECT COUNT(*) FROM dbo.STOCKTP
      WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockRows,
    (SELECT ISNULL(SUM(CAST(SLXUAT AS bigint)), 0) FROM dbo.STOCKTP
      WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockIssued,
    (SELECT ISNULL(SUM(CAST(SLCONLAI AS bigint)), 0) FROM dbo.STOCKTP
      WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS StockRemaining,
    (SELECT COUNT(*) FROM dbo.LUUPHIEUGIAOHANG
      WHERE MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS DeliveredRows,
    (SELECT ISNULL(SUM(CAST(SOLUONG AS bigint)), 0) FROM dbo.LUUPHIEUGIAOHANG
      WHERE MAHANG LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS DeliveredQty,
    (SELECT COUNT(*) FROM dbo.LUUDOCQRCODE
      WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS QrRows,
    (SELECT ISNULL(SUM(CAST(SLTEMFCC AS bigint)), 0) FROM dbo.LUUDOCQRCODE
      WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%') AS QrQty,
    (SELECT COUNT(*) FROM dbo.LUUDOCQRCODE q
      WHERE q.MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
        AND NOT EXISTS (
            SELECT 1 FROM dbo.STOCKTP s
            WHERE s.PART = q.MAHANGFCC AND q.LOTFCC LIKE N'%' + LEFT(s.LOT, 13) + N'%'
        )) AS QrWithoutStockViolations;
