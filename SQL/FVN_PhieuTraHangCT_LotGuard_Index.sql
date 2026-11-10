-- Index phục vụ kiểm tra "LOT đang nằm trong phiếu chưa hoàn tất" và đối chiếu SL trả theo lần giao.
-- Khóa chống tạo trùng đồng thời dùng sp_getapplock trong code (không cần unique index);
-- index này chỉ giúp câu truy vấn nhanh. Chạy một lần trên B7R2_FCC.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FVN_PhieuTraHangCT_MaHang_LotNo' AND object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT'))
    CREATE NONCLUSTERED INDEX IX_FVN_PhieuTraHangCT_MaHang_LotNo
        ON dbo.FVN_PhieuTraHangCT (MaHang, LotNo)
        INCLUDE (PhieuTraHangId, SlotIdNguon, DinhDanhPhieuGiao, SoLuong);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FVN_PhieuTraHangCT_DinhDanh' AND object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT'))
    CREATE NONCLUSTERED INDEX IX_FVN_PhieuTraHangCT_DinhDanh
        ON dbo.FVN_PhieuTraHangCT (DinhDanhPhieuGiao)
        INCLUDE (PhieuTraHangId, MaHang, LotNo, SoLuong);
GO
