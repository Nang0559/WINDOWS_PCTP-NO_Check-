/*
    PHASE 12 - QT CHUNG RETURN-DETAIL LINK CONTRACT

    FVN_PhieuXuLyBatThuong.PhieuTraHangCTId is the required parent link to the
    exact FVN_PhieuTraHangCT row that created the processing ticket.
    PHASE11 is a separate, optional reverse link on FVN_PhieuTraHangCT and does
    not replace this required column.

    Run against the PCTP database after the base QT Chung schema is installed.
*/
IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong', N'U') IS NULL
    THROW 51012, 'FVN_PhieuXuLyBatThuong chưa tồn tại. Hãy chạy migration QT Chung core trước.', 1;
GO

IF COL_LENGTH(N'dbo.FVN_PhieuXuLyBatThuong', N'PhieuTraHangCTId') IS NULL
    THROW 51013, 'Thiếu cột bắt buộc FVN_PhieuXuLyBatThuong.PhieuTraHangCTId. Không thể chạy PHASE12 an toàn khi chưa xác định schema gốc.', 1;
GO

IF EXISTS
(
    SELECT PhieuTraHangCTId
    FROM dbo.FVN_PhieuXuLyBatThuong
    GROUP BY PhieuTraHangCTId
    HAVING COUNT_BIG(*) > 1
)
    THROW 51014, 'Có nhiều phiếu xử lý cùng tham chiếu một PhieuTraHangCTId. Hãy xử lý dữ liệu trùng trước khi tạo unique index.', 1;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong')
      AND name = N'UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId'
)
BEGIN
    CREATE UNIQUE INDEX UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId
        ON dbo.FVN_PhieuXuLyBatThuong(PhieuTraHangCTId);
END;
GO
