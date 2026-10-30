USE [B7R2_FCC]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- dbo.vNhapTP: danh sách thành phẩm chờ nhập kho (màn hình NhapKho).
-- KET_THUC_LOT tính từ SATUS. Lot đã khóa (SATUS = 1) vẫn hiện trong view để tầng C# so sánh
-- SLSX mới (MES) với SLSX đã lưu và cho phép "Mở lại Lot" (DongBoSLSXVaMoLaiNeuThayDoi).
-- SLSX_DA_LUU = STOCKTP.SLSX (sản lượng SX đã lưu lúc kho nhập). Lot đã khóa mà SL_DA_SAN_XUAT (MES)
-- khác SLSX_DA_LUU nghĩa là kế hoạch SX đã đổi -> có thể mở lại. Cột này nằm CUỐI danh sách cột.
CREATE OR ALTER VIEW [dbo].[vNhapTP]
AS
SELECT 
    ROW_NUMBER() OVER (ORDER BY NGAY_SAN_XUAT DESC) AS STT, 
    (LOT_NO + LY_DO_TRA) AS FIND, 
    *
FROM (
    -- ===== NHÁNH 1: phiếu SX chờ nhập =====
    SELECT 
        RTRIM(tt.ItemLot) AS LOT_NO, 
        COALESCE(NULLIF(RTRIM(tt.Model), ''), NULLIF(RTRIM(s.Model), ''), '') AS Model,
        tt.Name AS TEN_SAN_PHAM, 
        tt.ReceiptItemCode AS MA_SAN_PHAM, 
        RTRIM(tt.ShiftCode) AS CA_SAN_XUAT, 
        tt.DocDate AS NGAY_SAN_XUAT, 
        tt.Qty AS SL_DA_SAN_XUAT, 
        s.ngaynhap AS NGAY_NHAP, 
        ISNULL(s.slnhap, 0) AS SL_DA_NHAP, 
        '' AS SL_DA_TRA, 
        '' AS LY_DO_TRA, 
        s.slconlai AS TON_KHO_TP, 
        '' AS SL_SE_NHAP, 
        CASE WHEN ISNULL(s.Satus, 0) = 1 THEN '1' ELSE '0' END AS KET_THUC_LOT,
        ISNULL(s.SLSX, 0) AS SLSX_DA_LUU
    FROM (
        SELECT 
            t.ItemLot, t.DocDate, t.ShiftCode, t.Model, 
            t.ReceiptItemCode, t.Name, SUM(t.QuantityT) AS Qty
        FROM (
            SELECT 
                SUBSTRING(SX.ItemLotCodeTP,0,21) AS ItemLot, 
                SX.DocDate, SX.ShiftCode, 
                -- Model từ danh mục hàng (vB20Item), dự phòng SX.Model
                COALESCE(NULLIF(RTRIM(K.Model), ''), 
                         MAX(NULLIF(RTRIM(SX.Model), ''))) AS Model,
                SX.ItemLotCodeTP, SX.ReceiptItemCode, K.Name, 
                CAST(SUM(SX.ReceiptQuantity) AS INT) AS QuantityT
            FROM 
                dbo.B30AccDoc AS SX
                INNER JOIN dbo.vB20Item AS K ON SX.ReceiptItemCode = K.Code
            WHERE 
                SX.ItemLotCodeTP <> '' 
                AND SX.IsActive <> 0 
                AND SX.ReceiptQuantity > 0
            GROUP BY 
                SX.DocDate, SX.ShiftCode, SX.ItemLotCodeTP,
                SX.ReceiptItemCode, K.Name, K.Model, K.Id, SX.MachinesCode, SX.GearCode
        ) AS T
        GROUP BY t.ItemLot, t.DocDate, t.ShiftCode, t.Model, t.ReceiptItemCode, t.Name
    ) AS TT
    LEFT JOIN dbo.Stocktp AS S ON tt.ItemLot = s.LOT
    WHERE 
        (ISNULL(s.slnhap, 0) = 0 OR ISNULL(s.slnhap, 0) <> tt.Qty)
    
    UNION ALL
    
    -- ===== NHÁNH 2: hàng trả về =====
    SELECT 
        RTRIM(D.Lot) AS ItemLot, 
        COALESCE(NULLIF(RTRIM(I.Model), ''), NULLIF(RTRIM(D.Model), ''), '') AS Model,
        RTRIM(D.Name), 
        RTRIM(D.part) AS ReceiptItem, RTRIM(D.casx) AS ShiftCode, 
        RTRIM(D.ngaysx) AS Docdate, D.SLSX AS Qty, 
        D.ngaynhap, D.slnhap, E.slconlai, RTRIM(E.ly_do_ng), 
        D.slconlai, '' AS SL_SE_NHAP, 
        CASE WHEN ISNULL(D.Satus, 0) = 1 THEN '1' ELSE '0' END AS KET_THUC_LOT,
        ISNULL(D.SLSX, 0) AS SLSX_DA_LUU
    FROM 
        Stocktp AS D
        INNER JOIN (
            SELECT lot AS lottra, SUM(slconlai) AS slconlai, ly_do_ng
            FROM dbo.stocktptrahang
            WHERE status <> 1 AND slconlai > 0
            GROUP BY lot, ly_do_ng
        ) AS E ON D.lot = E.lottra
        LEFT JOIN dbo.vB20Item AS I ON I.Code = D.part
    
    UNION ALL
    
    -- ===== NHÁNH 3: tồn kho TP của mã không còn SX =====
    SELECT 
        RTRIM(K.Lot) AS ItemLot, 
        COALESCE(NULLIF(RTRIM(I.Model), ''), NULLIF(RTRIM(K.Model), ''), '') AS Model,
        RTRIM(K.Name), 
        RTRIM(K.part) AS ReceiptItem, RTRIM(K.casx) AS ShiftCode, 
        RTRIM(K.ngaysx) AS Docdate, K.SLSX AS Qty, 
        K.ngaynhap, K.slnhap, SUM(T.sltra) AS SL_DATRA, 
        '' AS ly_do_ng, K.slconlai, '' AS SL_SE_NHAP, 
        CASE WHEN ISNULL(K.SATUS, 0) = 1 THEN '1' ELSE '0' END AS KET_THUC_LOT,
        ISNULL(K.SLSX, 0) AS SLSX_DA_LUU
    FROM 
        STOCKTP AS K
        LEFT JOIN STOCKTPTRAHANG AS T ON K.LOT = T.LOT
        LEFT JOIN dbo.vB20Item AS I ON I.Code = K.part
    WHERE 
        K.SLCONLAI > 0 
        AND K.PART IN (
            SELECT ReceiptItemCode FROM B30AccDoc
            WHERE IsActive = 0 
              AND ReceiptItemCode NOT IN (
                  SELECT ReceiptItemCode FROM B30AccDoc WHERE IsActive = 1
              )
        )
    GROUP BY 
        K.Lot, K.Model, I.Model, K.Name, K.part, K.casx, K.ngaysx,
        K.SLSX, K.ngaynhap, K.slnhap, K.slconlai, K.SATUS
) AS TTK
GO