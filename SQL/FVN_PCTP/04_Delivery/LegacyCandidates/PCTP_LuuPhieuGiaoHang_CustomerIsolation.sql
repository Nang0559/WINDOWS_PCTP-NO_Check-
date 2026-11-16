/*
  PCTP_LuuPhieuGiaoHang_CustomerIsolation.sql
  Branch: feature/wms-end-to-end-consistency

  Muc dich
  --------
  LUUPHIEUGIAOHANG la ledger DUNG CHUNG cho HVN (100001), YMVN (100002) va 100003.
  Phan tach khach hang chi dua vao cot NHAMAY (nam trong UQ_LUUPHIEU), con cac bang TMP/DOCQR
  tach rieng theo CustomerTableConfig. Script nay:
    1. Chi DOC: kiem tra tien dieu kien (cot, trung khoa, NHAMAY la gia tri nao) - khong sua du lieu.
    2. Them 1 index phuc vu truy van YMVN/merge LOT theo (CUA, MAHANG, NGAYGIAO) + GearYMVN.
  KHONG drop / KHONG doi UQ_LUUPHIEU / KHONG doi du lieu. Chay lai nhieu lan an toan (idempotent).
  Chay tren DB thu nghiem truoc, doc ket qua preflight, roi moi chay tren DB that.
*/
SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- 0. Tien dieu kien
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.LUUPHIEUGIAOHANG', N'U') IS NULL
    THROW 51000, N'Khong co dbo.LUUPHIEUGIAOHANG - dung database sai.', 1;

DECLARE @HasGear BIT = CASE WHEN COL_LENGTH(N'dbo.LUUPHIEUGIAOHANG', N'GearYMVN') IS NULL THEN 0 ELSE 1 END;
PRINT CONCAT(N'GearYMVN ton tai: ', @HasGear);

-------------------------------------------------------------------------------
-- 1. PREFLIGHT (read-only)
-------------------------------------------------------------------------------
-- 1a. Gia tri NHAMAY dang co trong ledger: phai khop NhaMayCase/TenNhaMay cua tung khach
--     (HVN: 'HON DA -VIET NAM- (NHA MAY ...)'; 100003: 'NHA MAY 100003'; YMVN: 'YAMAHA - VIET NAM').
SELECT NHAMAY, COUNT(*) AS SoDong, MIN(NGAYGIAO) AS TuNgay, MAX(NGAYGIAO) AS DenNgay
FROM dbo.LUUPHIEUGIAOHANG
GROUP BY NHAMAY
ORDER BY SoDong DESC;

-- 1b. Dong co NHAMAY rong/NULL => khong thuoc khach nao, UQ_LUUPHIEU khong phan biet duoc.
SELECT COUNT(*) AS DongNhamayRong
FROM dbo.LUUPHIEUGIAOHANG
WHERE NHAMAY IS NULL OR LTRIM(RTRIM(NHAMAY)) = N'';

-- 1c. YMVN: cac dong cung khoa UQ nhung khac GearYMVN se bi UQ_LUUPHIEU chan.
IF @HasGear = 1
EXEC sys.sp_executesql N'
SELECT TOP (200) STT, MAHANG, NGAYGIAO, CUA, TRUYEN, SOLUONG, GIOGIAO, NHAMAY,
       COUNT(*) AS SoDong, COUNT(DISTINCT ISNULL(GearYMVN, N'''')) AS SoGearKhacNhau
FROM dbo.LUUPHIEUGIAOHANG
GROUP BY STT, MAHANG, NGAYGIAO, CUA, TRUYEN, SOLUONG, GIOGIAO, NHAMAY
HAVING COUNT(*) > 1;';

-- 1d. GearYMVN dang chua gi? Neu thay gia tri giong TTPHIEU (xem ghi chu D2) thi 2 writer YMVN dang ghi lech nghia.
IF @HasGear = 1
EXEC sys.sp_executesql N'
SELECT TOP (50) ISNULL(GearYMVN, N'''') AS GearYMVN, NHAMAY, COUNT(*) AS SoDong
FROM dbo.LUUPHIEUGIAOHANG
GROUP BY ISNULL(GearYMVN, N''''), NHAMAY
ORDER BY SoDong DESC;';

-------------------------------------------------------------------------------
-- 2. Index bo sung (additive)
--    Truy van thuc te: GIAOHANGYMN.cs:269  (rtrim(CUA), MAHANG, NGAYGIAO, GearYMVN)
--                      TableOrderRepo.MergeLotTuBangRieng (NGAYGIAO, NHAMAY -> doc GearYMVN)
--    UQ_LUUPHIEU bat dau bang STT nen khong ho tro cac truy van tren.
-------------------------------------------------------------------------------
IF @HasGear = 1
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'dbo.LUUPHIEUGIAOHANG')
                     AND name = N'IX_LUU_CUA_MAHANG_NGAYGIAO')
BEGIN
    EXEC sys.sp_executesql N'
    CREATE NONCLUSTERED INDEX IX_LUU_CUA_MAHANG_NGAYGIAO
        ON dbo.LUUPHIEUGIAOHANG (CUA ASC, MAHANG ASC, NGAYGIAO ASC)
        INCLUDE (GearYMVN, LOT, NHAMAY, SOLUONG, GIOGIAO, STATUS)
        WITH (ONLINE = OFF);';
    PRINT N'Da tao IX_LUU_CUA_MAHANG_NGAYGIAO';
END
ELSE
    PRINT N'Bo qua IX_LUU_CUA_MAHANG_NGAYGIAO (da co hoac thieu cot GearYMVN)';
GO
