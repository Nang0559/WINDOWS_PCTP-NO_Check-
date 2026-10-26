/*
    PHASE 2 - AFFECTED LOT SNAPSHOT

    Chạy script này trên database PCTP trước khi gọi
    IAffectedLotTraceService.TruyVetLOT(...).

    Mỗi lần truy vết lại một phiếu, service sẽ replace toàn bộ snapshot
    trong cùng một transaction. Các quantity QC/rework/disposition ở bảng
    này được khởi tạo = 0; Phase 3 sẽ cập nhật chúng theo kết quả QC.
*/

IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongAffectedLot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PhieuXuLyBatThuongAffectedLot
    (
        Id                    INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId  INT NOT NULL,
        SourceType            INT NOT NULL,
        SourceReference       NVARCHAR(250) NULL,
        SlotId                INT NULL,
        LotNo                 NVARCHAR(100) NOT NULL,
        MaSanPham             NVARCHAR(100) NOT NULL,
        Model                 NVARCHAR(100) NULL,

        SoLuongAnhHuong       INT NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_AnhHuong DEFAULT (0),
        SoLuongDaKiemTra      INT NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_DaKiemTra DEFAULT (0),
        SoLuongOK              INT NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_OK DEFAULT (0),
        SoLuongNG              INT NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_NG DEFAULT (0),
        SoLuongRework          INT NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_Rework DEFAULT (0),
        SoLuongLoaiBo          INT NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_LoaiBo DEFAULT (0),

        SnapshotAt             DATETIME NOT NULL,
        SnapshotBy             NVARCHAR(100) NULL,

        CONSTRAINT PK_FVN_PhieuXuLyBatThuongAffectedLot
            PRIMARY KEY CLUSTERED (Id),

        CONSTRAINT FK_FVN_PXLBTAffectedLot_Phieu
            FOREIGN KEY (PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),

        CONSTRAINT CK_FVN_PXLBTAffectedLot_NonNegative
            CHECK
            (
                SoLuongAnhHuong >= 0 AND
                SoLuongDaKiemTra >= 0 AND
                SoLuongOK >= 0 AND
                SoLuongNG >= 0 AND
                SoLuongRework >= 0 AND
                SoLuongLoaiBo >= 0
            )
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_FVN_PXLBTAffectedLot_Phieu'
      AND object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongAffectedLot')
)
BEGIN
    CREATE INDEX IX_FVN_PXLBTAffectedLot_Phieu
        ON dbo.FVN_PhieuXuLyBatThuongAffectedLot
        (PhieuXuLyBatThuongId, SourceType, Id);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_FVN_PXLBTAffectedLot_TraceKey'
      AND object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongAffectedLot')
)
BEGIN
    CREATE INDEX IX_FVN_PXLBTAffectedLot_TraceKey
        ON dbo.FVN_PhieuXuLyBatThuongAffectedLot
        (MaSanPham, LotNo, SourceType);
END;
GO
