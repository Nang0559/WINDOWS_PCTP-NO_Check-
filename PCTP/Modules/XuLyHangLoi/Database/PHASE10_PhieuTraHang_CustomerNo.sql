/* ============================================================
   PHIEU TRA HANG - BO ENUM NguonKhachTra, DUNG CustomerNo
   ============================================================
   Truoc: FVN_PhieuTraHang.NguonKhachTra (INT: 1=HVN, 2=YMVN, 3=HTN)
   Sau  : FVN_PhieuTraHang.CustomerNo    (NVARCHAR(20): khoa CustomerTableConfig,
                                          VD 100001 / 100002 / 100003)

   Nguyen tac:
   - Chay script NAY TRUOC khi chay ban app moi.
   - Giu nguyen cot NguonKhachTra cu (app moi khong doc/ghi nua) de co the
     rollback va de view/bao cao cu (neu co) khong bi loi.
     Sau khi xac nhan het cho dung, co the DROP cot cu (xem cuoi file).
   - Script chay lai nhieu lan duoc (idempotent).
   ============================================================ */

IF COL_LENGTH(N'dbo.FVN_PhieuTraHang', N'CustomerNo') IS NULL
BEGIN
    ALTER TABLE dbo.FVN_PhieuTraHang ADD CustomerNo NVARCHAR(20) NULL;
END
GO

-- Backfill tu cot enum cu (chi dong chua co CustomerNo).
IF COL_LENGTH(N'dbo.FVN_PhieuTraHang', N'NguonKhachTra') IS NOT NULL
BEGIN
    EXEC(N'
    UPDATE dbo.FVN_PhieuTraHang
    SET CustomerNo = CASE NguonKhachTra
                         WHEN 1 THEN N''100001''  -- HVN
                         WHEN 2 THEN N''100002''  -- YMVN
                         WHEN 3 THEN N''100003''  -- HTN
                     END
    WHERE CustomerNo IS NULL
      AND NguonKhachTra IS NOT NULL;');
END
GO

-- Kiem tra: phieu KhachTra con thieu CustomerNo (ky vong 0 dong).
-- SELECT Id, SoPhieu, Nguon, CustomerNo FROM dbo.FVN_PhieuTraHang
-- WHERE Nguon = 2 AND CustomerNo IS NULL;   -- NguonXuLyBatThuong.KhachTra = 2

/* Sau khi da chay on dinh (tuy chon):
IF COL_LENGTH(N'dbo.FVN_PhieuTraHang', N'NguonKhachTra') IS NOT NULL
    ALTER TABLE dbo.FVN_PhieuTraHang DROP COLUMN NguonKhachTra;
*/
