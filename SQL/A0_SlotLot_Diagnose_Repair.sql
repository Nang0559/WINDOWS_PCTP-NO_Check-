/* ============================================================================
   A0 (KHO_AO_NHAP_LOAT / RACK_AO) — chẩn đoán & khôi phục SlotLot bị ghi lại bởi phiên bản cũ
   ----------------------------------------------------------------------------
   Nguyên nhân: SlotService.SaveLots / LegacyStockSlotRepositoryAdapter.AddLot|TakeLot cũ và
   SlotRepository.MoveLot cũ xoá toàn bộ SlotLot của Slot rồi INSERT lại chỉ từ QRInfo
   → các dòng bị ghi lại mất QrData / MaPhieu / NgaySX / SoPhieuTong (và đổi SlotLotId).
   Hệ quả: PhieuTrackingRepository.ExistsQrData không còn nhận ra tem đã nhập.

   CÁCH DÙNG:  chạy từng phần 1 → 4 (chỉ SELECT, an toàn). Phần 5 là KHÔI PHỤC — mặc định ROLLBACK,
   xem kết quả rồi đổi ROLLBACK thành COMMIT khi đã hài lòng. NÊN BACKUP bảng SlotLot trước.
   ============================================================================ */

DECLARE @WH   NVARCHAR(100) = N'KHO_AO_NHAP_LOAT';   -- BulkImportConfig.WarehouseName
DECLARE @RACK NVARCHAR(100) = N'RACK_AO';            -- BulkImportConfig.RackName

DECLARE @SlotId INT = (
    SELECT TOP 1 s.SlotId
    FROM Slot s
    JOIN Rack r      ON r.RackId = s.RackId
    JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
    WHERE w.Name = @WH AND r.RackName = @RACK);

SELECT @SlotId AS SlotId_A0;

/* ---- 1. Tổng quan A0 ---------------------------------------------------- */
SELECT
    COUNT(*)                                                   AS TongDong,
    SUM(CASE WHEN QrData  IS NULL OR QrData  = '' THEN 1 ELSE 0 END) AS ThieuQrData,
    SUM(CASE WHEN MaPhieu IS NULL OR MaPhieu = '' THEN 1 ELSE 0 END) AS ThieuMaPhieu,
    SUM(CASE WHEN TemCode IS NULL OR TemCode = '' THEN 1 ELSE 0 END) AS ThieuTemCode,
    SUM(Quantity)                                              AS TongSL
FROM SlotLot
WHERE SlotId = @SlotId AND PhieuStatus = 0;

/* ---- 2. Các dòng A0 đã mất QrData (ExistsQrData không nhận ra tem này) ---- */
SELECT SlotLotId, LotNo, ItemCode, Quantity, TemCode, MaPhieu, ImportDate, CreatedDate
FROM SlotLot
WHERE SlotId = @SlotId AND PhieuStatus = 0 AND (QrData IS NULL OR QrData = '')
ORDER BY ImportDate, SlotLotId;

/* ---- 3. Nghi ghi đôi: cùng Slot+LOT+Item+SL, 1 dòng có QrData và 1 dòng không --------- */
SELECT a.SlotLotId AS DongCoQr, b.SlotLotId AS DongThua, a.LotNo, a.ItemCode, a.Quantity
FROM SlotLot a
JOIN SlotLot b
  ON b.SlotId = a.SlotId AND b.LotNo = a.LotNo AND b.ItemCode = a.ItemCode
 AND b.Quantity = a.Quantity AND b.SlotLotId <> a.SlotLotId
WHERE a.SlotId = @SlotId AND a.PhieuStatus = 0 AND b.PhieuStatus = 0
  AND a.QrData IS NOT NULL AND a.QrData <> ''
  AND (b.QrData IS NULL OR b.QrData = '');

/* ---- 4. Đối chiếu tồn theo LOT: STOCKTP.SLCONLAI vs tổng SlotLot hoạt động -------- */
SELECT s.LOT, s.PART, ISNULL(s.SLCONLAI,0) AS SL_STOCKTP, ISNULL(x.Tong,0) AS SL_SLOT,
       ISNULL(s.SLCONLAI,0) - ISNULL(x.Tong,0) AS ChenhLech
FROM STOCKTP s
LEFT JOIN (SELECT LotNo, SUM(Quantity) AS Tong FROM SlotLot WHERE PhieuStatus = 0 GROUP BY LotNo) x
       ON x.LotNo = s.LOT
WHERE ISNULL(s.SLCONLAI,0) > 0 AND ISNULL(s.SLCONLAI,0) <> ISNULL(x.Tong,0)
ORDER BY s.LOT;

/* ---- 5. KHÔI PHỤC QrData/MaPhieu/TemCode từ StockHistory (ActionType='IMPORT') ----------
   Chỉ gán khi khớp DUY NHẤT 1-1 theo (LotNo, ItemCode, Quantity) và QR đó chưa tồn tại ở SlotLot nào.
   Các trường hợp mơ hồ được bỏ qua — phải xử lý tay (kết quả ở phần 2).                        */
BEGIN TRAN;

;WITH H AS (
    SELECT DISTINCT LotNo, ItemCode, Quantity, QrData, MaPhieu, TemCode
    FROM StockHistory
    WHERE ActionType = 'IMPORT' AND ToSlotId = @SlotId
      AND QrData IS NOT NULL AND QrData <> ''
      AND NOT EXISTS (SELECT 1 FROM SlotLot x WHERE x.QrData = StockHistory.QrData)
),
P AS (
    SELECT r.SlotLotId, h.QrData, h.MaPhieu, h.TemCode,
           COUNT(*) OVER (PARTITION BY r.SlotLotId) AS UngVienMoiDong,
           COUNT(*) OVER (PARTITION BY h.QrData)    AS DongMoiQr
    FROM SlotLot r
    JOIN H h ON h.LotNo = r.LotNo AND h.ItemCode = r.ItemCode AND h.Quantity = r.Quantity
    WHERE r.SlotId = @SlotId AND r.PhieuStatus = 0 AND (r.QrData IS NULL OR r.QrData = '')
)
UPDATE sl
SET sl.QrData  = p.QrData,
    sl.MaPhieu = ISNULL(NULLIF(sl.MaPhieu, ''), p.MaPhieu),
    sl.TemCode = ISNULL(NULLIF(sl.TemCode, ''), p.TemCode)
FROM SlotLot sl
JOIN P p ON p.SlotLotId = sl.SlotLotId
WHERE p.UngVienMoiDong = 1 AND p.DongMoiQr = 1;

SELECT @@ROWCOUNT AS SoDongDaKhoiPhuc;

ROLLBACK TRAN;   -- ← đổi thành COMMIT TRAN khi đã kiểm tra kết quả
