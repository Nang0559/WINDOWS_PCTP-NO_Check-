/* ============================================================================
   PHASE14 - SlotLot.PhieuStatus = 3 (NG cách ly) cho hàng NG sau Rework
   ----------------------------------------------------------------------------
   Quy tắc tồn kho chuẩn:
     PhieuStatus = 0 : tồn vật lý/current stock
     PhieuStatus = 1 : Split, chỉ trace lịch sử
     PhieuStatus = 2 : ExportedOut, đã xuất khỏi kho
     PhieuStatus = 3 : NG/quarantine

   Vì vậy STOCKTP.SLCONLAI và Slot.Quantity chỉ đối chiếu SlotLot có
   PhieuStatus = 0. Không dùng <> 3 vì status 1/2 không còn là tồn vật lý.

   Hàng NG sau Rework nằm vật lý ở slot NG nhưng KHÔNG thuộc STOCKTP ->
   PhieuStatus = 3.

   CÁCH DÙNG: NÊN BACKUP bảng SlotLot. Chạy 1 (kiểm tra ràng buộc) ->
   2 (xem dòng NG cũ) -> 3 (chuyển, mặc định ROLLBACK; đổi thành COMMIT
   khi đã kiểm tra).
   ============================================================================ */

/* ---- 1. Có CHECK constraint nào chặn PhieuStatus = 3 không? ---------------- */
SELECT cc.name, cc.definition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.SlotLot')
  AND cc.definition LIKE N'%PhieuStatus%';
-- Nếu có và không cho phép 3: DROP rồi tạo lại, ví dụ
-- ALTER TABLE dbo.SlotLot DROP CONSTRAINT <tên>;
-- ALTER TABLE dbo.SlotLot ADD CONSTRAINT CK_SlotLot_PhieuStatus CHECK (PhieuStatus IN (0,1,2,3));
GO

/* ---- 2. Dòng NG cũ (đang PhieuStatus = 0) khớp với bản ghi nhập NG --------- */
SELECT n.Id AS NhapNGId, n.PhieuXuLyBatThuongId, n.SlotIdNG, n.LotNhapLai, n.SoLuongNG, n.NgayNhap,
       sl.SlotLotId, sl.Quantity, sl.CreatedDate
FROM dbo.FVN_TraHangQTChungNhapNG n
JOIN dbo.SlotLot sl
  ON sl.SlotId = n.SlotIdNG
 AND sl.LotNo = n.LotNhapLai
 AND sl.ItemCode = n.MaHang
 AND sl.Quantity = n.SoLuongNG
 AND sl.PhieuStatus = 0
 AND sl.CreatedDate BETWEEN DATEADD(MINUTE, -2, n.NgayNhap) AND DATEADD(MINUTE, 2, n.NgayNhap)
ORDER BY n.Id;
GO

/* ---- 3. Chuyển sang PhieuStatus = 3 (mặc định ROLLBACK) -------------------- */
BEGIN TRANSACTION;

UPDATE sl SET sl.PhieuStatus = 3
FROM dbo.SlotLot sl
JOIN dbo.FVN_TraHangQTChungNhapNG n
  ON sl.SlotId = n.SlotIdNG AND sl.LotNo = n.LotNhapLai AND sl.ItemCode = n.MaHang
 AND sl.Quantity = n.SoLuongNG AND sl.PhieuStatus = 0
 AND sl.CreatedDate BETWEEN DATEADD(MINUTE, -2, n.NgayNhap) AND DATEADD(MINUTE, 2, n.NgayNhap);
PRINT CONCAT(N'Đã chuyển ', @@ROWCOUNT, N' dòng SlotLot sang PhieuStatus = 3.');

/* Tính lại header Slot: chỉ current stock PhieuStatus = 0. */
UPDATE s
SET s.Quantity   = ISNULL(x.TotalQty, 0),
    s.IsOccupied = CASE WHEN ISNULL(x.TotalQty, 0) > 0 THEN 1 ELSE 0 END
FROM dbo.Slot s
OUTER APPLY (
    SELECT SUM(Quantity) AS TotalQty
    FROM dbo.SlotLot
    WHERE SlotId = s.SlotId AND PhieuStatus = 0
) x
WHERE s.SlotId IN (
    SELECT DISTINCT SlotIdNG
    FROM dbo.FVN_TraHangQTChungNhapNG
    WHERE SlotIdNG IS NOT NULL
);

ROLLBACK TRANSACTION;   -- đổi thành COMMIT TRANSACTION khi đã kiểm tra
GO
