/*
  Bù MaPhieu cho các dòng StockHistory do xử lý hàng lỗi (rework) đã ghi TRƯỚC khi sửa code.
  Trước đây số phiếu tham chiếu (XLBT#<id>) chỉ nằm ở cột TemCode, MaPhieu để trống nên báo cáo
  Lịch sử kho / Truy vết không có "Số phiếu" cho các dòng này. Script chép TemCode -> MaPhieu.
  - Chỉ đụng dòng: ActionType thuộc nhóm rework, TemCode dạng 'XLBT#<số>', MaPhieu đang NULL/rỗng.
  - Chạy lại an toàn. Chạy trên đúng DB app đang dùng. Backup trước khi chạy.
*/
SET XACT_ABORT ON;
GO
SELECT DB_NAME() AS DangChayTrenDB;

SELECT ActionType, COUNT(*) AS SoDongSeDuocBu
FROM dbo.StockHistory
WHERE ActionType IN ('REWORK_EXPORT','REWORK_NG_IMPORT','NHAP_LAI_SAU_REWORK','REWORK_CANCEL_RETURN')
  AND TemCode LIKE 'XLBT#%'
  AND (MaPhieu IS NULL OR LTRIM(RTRIM(MaPhieu)) = '')
GROUP BY ActionType;

BEGIN TRAN;
UPDATE dbo.StockHistory
SET MaPhieu = TemCode
WHERE ActionType IN ('REWORK_EXPORT','REWORK_NG_IMPORT','NHAP_LAI_SAU_REWORK','REWORK_CANCEL_RETURN')
  AND TemCode LIKE 'XLBT#%'
  AND (MaPhieu IS NULL OR LTRIM(RTRIM(MaPhieu)) = '');
PRINT CONCAT(N'Đã cập nhật ', @@ROWCOUNT, N' dòng.');
COMMIT;
GO
