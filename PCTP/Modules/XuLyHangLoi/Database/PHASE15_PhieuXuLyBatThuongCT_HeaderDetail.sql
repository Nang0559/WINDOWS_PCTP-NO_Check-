/*
    PHASE 15 - PHIEU XU LY BAT THUONG: HEADER + DETAIL

    Moi phieu tra hang chi tao MOT phieu xu ly bat thuong (header).
    Cac dong Slot/LOT cua phieu tra thanh dong detail trong
    FVN_PhieuXuLyBatThuongCT (chi de biet hang dang o vi tri nao trong kho).
    QC dinh huong 1 lan o header; buoc 5-8 chi dung tong so luong o header.

    Chay tren DB PCTP sau PHASE11/PHASE12. Script an toan khi chay lai.
*/
IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong', N'U') IS NULL
    THROW 51020, 'FVN_PhieuXuLyBatThuong chua ton tai.', 1;
GO

IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongCT', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PhieuXuLyBatThuongCT
    (
        Id                    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_PhieuXuLyBatThuongCT PRIMARY KEY,
        PhieuXuLyBatThuongId  int NOT NULL,
        PhieuTraHangCTId      int NOT NULL,
        SlotIdNguon           int NULL,
        SlotLotIdNguon        int NULL,
        LotNguon              nvarchar(50)  NULL,
        MaSanPham             nvarchar(50)  NULL,
        SoLuongLoi            int NOT NULL CONSTRAINT DF_FVN_PhieuXuLyBatThuongCT_SoLuongLoi DEFAULT (0),
        LyDoNg                nvarchar(500) NULL,
        CreatedAt             datetime NOT NULL CONSTRAINT DF_FVN_PhieuXuLyBatThuongCT_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_FVN_PhieuXuLyBatThuongCT_Header
            FOREIGN KEY (PhieuXuLyBatThuongId) REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id)
    );
END
GO

-- Mot dong phieu tra chi thuoc mot dong detail.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongCT')
                 AND name = N'UX_FVN_PhieuXuLyBatThuongCT_PhieuTraHangCTId')
    CREATE UNIQUE INDEX UX_FVN_PhieuXuLyBatThuongCT_PhieuTraHangCTId
        ON dbo.FVN_PhieuXuLyBatThuongCT(PhieuTraHangCTId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongCT')
                 AND name = N'IX_FVN_PhieuXuLyBatThuongCT_Header')
    CREATE INDEX IX_FVN_PhieuXuLyBatThuongCT_Header
        ON dbo.FVN_PhieuXuLyBatThuongCT(PhieuXuLyBatThuongId);
GO

-- Backfill: moi phieu cu (1 phieu = 1 dong phieu tra) -> 1 dong detail.
IF COL_LENGTH(N'dbo.FVN_PhieuXuLyBatThuong', N'PhieuTraHangCTId') IS NOT NULL
BEGIN
    EXEC(N'
    INSERT INTO dbo.FVN_PhieuXuLyBatThuongCT
        (PhieuXuLyBatThuongId, PhieuTraHangCTId, SlotIdNguon, LotNguon, MaSanPham, SoLuongLoi, CreatedAt)
    SELECT p.Id, p.PhieuTraHangCTId, p.SlotIdNguon, COALESCE(p.LotNguon, p.SoLoLoi), p.MaSanPham,
           p.SoLuongLoi, p.CreatedAt
    FROM dbo.FVN_PhieuXuLyBatThuong p
    WHERE p.PhieuTraHangCTId IS NOT NULL
      AND p.PhieuTraHangCTId > 0
      AND NOT EXISTS (SELECT 1 FROM dbo.FVN_PhieuXuLyBatThuongCT d
                      WHERE d.PhieuTraHangCTId = p.PhieuTraHangCTId);');
END
GO

-- Header khong con gan voi mot dong phieu tra duy nhat:
-- bo unique index cua PHASE12 va cho phep PhieuTraHangCTId NULL (cot legacy).
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong')
             AND name = N'UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId')
    DROP INDEX UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId ON dbo.FVN_PhieuXuLyBatThuong;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong')
             AND name = N'PhieuTraHangCTId' AND is_nullable = 0)
    ALTER TABLE dbo.FVN_PhieuXuLyBatThuong ALTER COLUMN PhieuTraHangCTId int NULL;
GO
