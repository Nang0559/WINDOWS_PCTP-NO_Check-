/* ============================================================
   PHASE 3 - INITIAL QC / QC PHAN LOAI BAN DAU
   ============================================================
   1 phieu = 1 ket qua QC ban dau.
   Chi tiet theo tung LOT nam trong FVN_PhieuXuLyBatThuongAffectedLot.

   Nguyen tac:
   - SoLuongDaKiemTra = SoLuongOK + SoLuongNG.
   - SoLuongNG = SoLuongRework + SoLuongLoaiBoBanDau.
   - Khong suy ra giao bu tu ket qua QC.
   ============================================================ */

IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongQCInitial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PhieuXuLyBatThuongQCInitial
    (
        Id INT IDENTITY(1,1) NOT NULL,
        PhieuXuLyBatThuongId INT NOT NULL,
        SoLuongAnhHuong INT NOT NULL,
        SoLuongDaKiemTra INT NOT NULL,
        SoLuongOK INT NOT NULL,
        SoLuongNG INT NOT NULL,
        SoLuongRework INT NOT NULL,
        SoLuongLoaiBoBanDau INT NOT NULL,
        NoiDungKiemTra NVARCHAR(1000) NULL,
        KetLuan NVARCHAR(1000) NULL,
        ConfirmedAt DATETIME NULL,
        ConfirmedBy NVARCHAR(100) NULL,
        CONSTRAINT PK_FVN_PhieuXuLyBatThuongQCInitial PRIMARY KEY (Id),
        CONSTRAINT FK_FVN_PXLB_QCInitial_PXLB
            FOREIGN KEY (PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong(Id),
        CONSTRAINT UQ_FVN_PXLB_QCInitial_Phieu UNIQUE (PhieuXuLyBatThuongId),
        CONSTRAINT CK_FVN_PXLB_QCInitial_Qty_NonNegative CHECK
        (
            SoLuongAnhHuong >= 0 AND
            SoLuongDaKiemTra >= 0 AND
            SoLuongOK >= 0 AND
            SoLuongNG >= 0 AND
            SoLuongRework >= 0 AND
            SoLuongLoaiBoBanDau >= 0
        ),
        CONSTRAINT CK_FVN_PXLB_QCInitial_OK_NG CHECK
        (
            SoLuongDaKiemTra = SoLuongOK + SoLuongNG
        ),
        CONSTRAINT CK_FVN_PXLB_QCInitial_Rework_Disposition CHECK
        (
            SoLuongNG = SoLuongRework + SoLuongLoaiBoBanDau
        )
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongQCInitial')
      AND name = N'IX_FVN_PXLB_QCInitial_Phieu'
)
BEGIN
    CREATE INDEX IX_FVN_PXLB_QCInitial_Phieu
        ON dbo.FVN_PhieuXuLyBatThuongQCInitial(PhieuXuLyBatThuongId);
END;
GO
