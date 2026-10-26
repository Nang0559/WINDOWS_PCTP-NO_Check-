-- Tùy chọn: cột liên kết dòng phiếu trả → phiếu xử lý bất thường.
-- Code vẫn chạy khi chưa có cột này; có cột thì PhieuXuLyBatThuongRepository.Insert sẽ gắn liên kết.
IF COL_LENGTH('dbo.FVN_PhieuTraHangCT', 'PhieuXuLyBatThuongId') IS NULL
BEGIN
    ALTER TABLE dbo.FVN_PhieuTraHangCT
        ADD PhieuXuLyBatThuongId INT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId'
                 AND object_id = OBJECT_ID('dbo.FVN_PhieuTraHangCT'))
BEGIN
    CREATE INDEX IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId
        ON dbo.FVN_PhieuTraHangCT(PhieuXuLyBatThuongId);
END
GO
