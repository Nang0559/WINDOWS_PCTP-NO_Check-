-- g6-delivery-hvn-multilot: invariants AFTER the real UI scan (read-only; run with Invoke-PctpQcSeed.ps1 -Action Assert -Journey delivery-hvn-multilot).
-- HARD invariants (every column ending in Violations must be 0) were true on 100% of the live SQL14 rows of 2026-10-28:
--   StockViolations      : SLNHAP - SLXUAT = SLCONLAI, SLCONLAI >= 0, SLXUAT <= SLNHAP
--   LuuOverIssued        : lots where SUM(LUU.SOLUONG of single-LOT lines) > STOCKTP.SLXUAT   (live: 50/50 lots had SLXUAT >= SUM)
--   QrWithoutStockViolations : LUUDOCQRCODE rows of this run whose MAHANGFCC + LEFT(LOTFCC,13) has no STOCKTP row (live: 100%)
-- INFORMATION columns (NOT assertions; the live data shows they are not invariants):
--   LuuLots / LuuLotsWithQr  : lots delivered vs lots that have at least one QR row
--   QrFccQty / StockIssued   : SUM(SLTEMFCC) vs SUM(SLXUAT); live data: equal only for 8 of 50 keys, because QR may be scanned on other days
--   QrDuplicateRows          : LUUDOCQRCODE rows beyond the distinct business-column set; tests the 'QR rows duplicated when DOCQRCODE.GIO is empty and
--                              several TMP lines share MAHANG' hypothesis. Expected 0 here because each line has its own item and GIO is set by the screen.
-- NOT checked on purpose: LUU.SOLUONG = SUM(QR), SLCONLAITMP, SATUS, LUUQR.STT, GIOGIAOFCC/GIOXUAT, LOTHVN.
-- Multi-LOT lines are matched with LIKE on the 13-char root (digits only, so no wildcard escaping is needed).
;WITH S AS (
    SELECT LOT, PART, LEFT(LOT, 13) AS R13, SLNHAP, SLXUAT, SLCONLAI
    FROM dbo.STOCKTP WHERE PART LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
),
LU AS (
    SELECT S.R13, SUM(CAST(l.SOLUONG AS bigint)) AS LuuQty
    FROM S JOIN dbo.LUUPHIEUGIAOHANG l
      ON l.MAHANG = S.PART AND l.LOT LIKE N'%' + S.R13 + N'%' AND CHARINDEX(N',', l.LOT) = 0
    GROUP BY S.R13
),
QQ AS (
    SELECT S.R13, COUNT(*) AS QrRows, SUM(CAST(q.SLTEMFCC AS bigint)) AS QrQty
    FROM S JOIN dbo.LUUDOCQRCODE q
      ON q.MAHANGFCC = S.PART AND q.LOTFCC LIKE N'%' + S.R13 + N'%'
    GROUP BY S.R13
),
DEL AS (
    SELECT DISTINCT S.R13 FROM S JOIN dbo.LUUPHIEUGIAOHANG l
      ON l.MAHANG = S.PART AND l.LOT LIKE N'%' + S.R13 + N'%'
)
SELECT
    (SELECT COUNT(*) FROM S WHERE SLNHAP - SLXUAT <> SLCONLAI OR SLCONLAI < 0 OR SLXUAT > SLNHAP) AS StockViolations,
    (SELECT COUNT(*) FROM S JOIN LU ON LU.R13 = S.R13 WHERE LU.LuuQty > S.SLXUAT) AS LuuOverIssued,
    (SELECT COUNT(*) FROM dbo.LUUDOCQRCODE q
      WHERE q.MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
        AND NOT EXISTS (SELECT 1 FROM S WHERE S.PART = q.MAHANGFCC AND q.LOTFCC LIKE N'%' + S.R13 + N'%')) AS QrWithoutStockViolations,
    (SELECT COUNT(*) FROM DEL) AS LuuLots,
    (SELECT COUNT(*) FROM DEL JOIN QQ ON QQ.R13 = DEL.R13) AS LuuLotsWithQr,
    (SELECT ISNULL(SUM(QrQty), 0) FROM QQ) AS QrFccQty,
    (SELECT ISNULL(SUM(CAST(SLXUAT AS bigint)), 0) FROM S) AS StockIssued,
    (SELECT ISNULL(SUM(c - 1), 0) FROM (SELECT COUNT(*) AS c FROM dbo.LUUDOCQRCODE
        WHERE MAHANGFCC LIKE REPLACE(@Prefix, N'_', N'[_]') + N'%'
        GROUP BY LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN, MAFCC, STT, KETQUA) d) AS QrDuplicateRows;
