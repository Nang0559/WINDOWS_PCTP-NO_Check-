/* =====================================================================
   NHAP_TP_HIS — ngừng sử dụng (code không còn đọc/ghi bảng này)
   Chống nhập trùng nay dựa trên:
     - SlotLot.QrData                               (tồn hiện tại)
     - StockHistory(ActionType='IMPORT').QrData     (nhật ký, không bị xoá khi xuất hàng)
   CHẠY TỪNG BƯỚC, KHÔNG TỰ ĐỘNG DROP.
   ===================================================================== */

/* ---- 1. Quy mô dữ liệu ---- */
SELECT
    (SELECT COUNT(*) FROM NHAP_TP_HIS)                                        AS NhapTpHis_Rows,
    (SELECT COUNT(*) FROM StockHistory WHERE ActionType = 'IMPORT')           AS StockHistory_Import_Rows,
    (SELECT COUNT(*) FROM StockHistory WHERE ActionType = 'IMPORT'
                                         AND (QrData IS NULL OR QrData = '')) AS StockHistory_Import_ThieuQr,
    (SELECT COUNT(*) FROM SlotLot WHERE QrData IS NOT NULL AND QrData <> '')  AS SlotLot_CoQr;

/* ---- 2. Case trong NHAP_TP_HIS mà StockHistory IMPORT KHÔNG có (tem cũ có thể nhập trùng lại) ----
   Case cũ = RawLotNo + SoPhieuTong  =  phần tử [0] + phần tử [4] của QR (tách theo ':').
   Cần SQL Server 2016+ (JSON_VALUE). Nếu 0 dòng => an toàn để ngừng dùng bảng.            */
;WITH H AS (
    SELECT
        JSON_VALUE('["' + REPLACE(REPLACE(QrData, '\', '\\'), ':', '","') + '"]', '$[0]')
      + JSON_VALUE('["' + REPLACE(REPLACE(QrData, '\', '\\'), ':', '","') + '"]', '$[4]') AS CaseNo
    FROM StockHistory
    WHERE ActionType = 'IMPORT' AND QrData IS NOT NULL AND QrData <> ''
)
SELECT c.LOTCASE
FROM NHAP_TP_HIS c
WHERE NOT EXISTS (SELECT 1 FROM H WHERE H.CaseNo = c.LOTCASE);

/* ---- 3. Sau khi đã chạy ổn định một thời gian, đổi tên trước khi xoá hẳn ----
-- EXEC sp_rename 'NHAP_TP_HIS', 'NHAP_TP_HIS_OLD';
-- DROP TABLE NHAP_TP_HIS_OLD;
*/
