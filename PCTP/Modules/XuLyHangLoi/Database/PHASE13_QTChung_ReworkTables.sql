/*
    PHASE 13 - QT Chung Rework persistence tables
    Fixes: Invalid object name 'FVN_TraHangQTChungXuat'.

    Run this script against the same database used by PCTP/WMS.
    Idempotent: existing tables are left intact; missing tables are created.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'dbo.FVN_TraHangQTChungXuat', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_TraHangQTChungXuat
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_TraHangQTChungXuat PRIMARY KEY,
        PhieuXuLyBatThuongId INT NOT NULL,
        SlotIdNguon INT NOT NULL,
        LotXuat NVARCHAR(100) NOT NULL,
        LoaiXuat NVARCHAR(30) NULL,
        MaHang NVARCHAR(100) NOT NULL,
        SoLuongXuat INT NOT NULL,
        TonTruoc INT NOT NULL,
        TonSau INT NOT NULL,
        NgayXuat DATETIME NOT NULL CONSTRAINT DF_FVN_TraHangQTChungXuat_NgayXuat DEFAULT (GETDATE()),
        NguoiXuat NVARCHAR(100) NULL,
        LyDo NVARCHAR(500) NULL,
        Note NVARCHAR(MAX) NULL,
        CONSTRAINT CK_FVN_TraHangQTChungXuat_SoLuong CHECK (SoLuongXuat > 0)
    );
    CREATE INDEX IX_FVN_TraHangQTChungXuat_Phieu_Ngay
        ON dbo.FVN_TraHangQTChungXuat(PhieuXuLyBatThuongId, NgayXuat, Id);
END;
GO

IF OBJECT_ID(N'dbo.FVN_TraHangQTChungGiao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_TraHangQTChungGiao
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_TraHangQTChungGiao PRIMARY KEY,
        PhieuXuLyBatThuongId INT NOT NULL,
        LotGiao NVARCHAR(100) NULL,
        MaHang NVARCHAR(100) NULL,
        SoLuongGiao INT NOT NULL,
        ThoiGian DATETIME NOT NULL CONSTRAINT DF_FVN_TraHangQTChungGiao_ThoiGian DEFAULT (GETDATE()),
        NgayGiao NVARCHAR(100) NULL,
        NguoiNhan NVARCHAR(100) NULL,
        BoPhanNhan NVARCHAR(100) NULL,
        SoPhieuGiaoNhan NVARCHAR(100) NULL,
        Note NVARCHAR(MAX) NULL,
        CONSTRAINT CK_FVN_TraHangQTChungGiao_SoLuong CHECK (SoLuongGiao > 0)
    );
    CREATE INDEX IX_FVN_TraHangQTChungGiao_Phieu_ThoiGian
        ON dbo.FVN_TraHangQTChungGiao(PhieuXuLyBatThuongId, ThoiGian, Id);
END;
GO

IF OBJECT_ID(N'dbo.FVN_TraHangQTChungQC', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_TraHangQTChungQC
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_TraHangQTChungQC PRIMARY KEY,
        PhieuXuLyBatThuongId INT NOT NULL,
        SoLuongDaRework INT NOT NULL,
        SoLuongOK INT NOT NULL,
        SoLuongNG INT NOT NULL,
        DaKiemTraTem BIT NOT NULL CONSTRAINT DF_FVN_TraHangQTChungQC_DaKiemTraTem DEFAULT (0),
        ThoiGian DATETIME NOT NULL CONSTRAINT DF_FVN_TraHangQTChungQC_ThoiGian DEFAULT (GETDATE()),
        NguoiQC NVARCHAR(100) NULL,
        KetLuan NVARCHAR(100) NULL,
        Note NVARCHAR(MAX) NULL,
        CONSTRAINT CK_FVN_TraHangQTChungQC_SoLuong CHECK (SoLuongDaRework >= 0 AND SoLuongOK >= 0 AND SoLuongNG >= 0)
    );
    CREATE INDEX IX_FVN_TraHangQTChungQC_Phieu_ThoiGian
        ON dbo.FVN_TraHangQTChungQC(PhieuXuLyBatThuongId, ThoiGian DESC, Id DESC);
END;
GO

IF OBJECT_ID(N'dbo.FVN_TraHangQTChungNhapNG', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_TraHangQTChungNhapNG
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_TraHangQTChungNhapNG PRIMARY KEY,
        PhieuXuLyBatThuongId INT NOT NULL,
        SlotIdOK INT NULL,
        SlotIdNG INT NULL,
        LotNhapLai NVARCHAR(100) NOT NULL,
        MaHang NVARCHAR(100) NOT NULL,
        SoLuongNG INT NOT NULL,
        SlotIdNhap INT NULL,
        NgayNhap DATETIME NOT NULL CONSTRAINT DF_FVN_TraHangQTChungNhapNG_NgayNhap DEFAULT (GETDATE()),
        NguoiNhap NVARCHAR(100) NULL,
        LyDo NVARCHAR(500) NULL,
        Note NVARCHAR(MAX) NULL,
        CONSTRAINT CK_FVN_TraHangQTChungNhapNG_SoLuong CHECK (SoLuongNG > 0)
    );
    CREATE INDEX IX_FVN_TraHangQTChungNhapNG_Phieu_Ngay
        ON dbo.FVN_TraHangQTChungNhapNG(PhieuXuLyBatThuongId, NgayNhap, Id);
END;
GO

COMMIT TRANSACTION;
GO

PRINT N'PHASE13_QTChung_ReworkTables completed.';
GO
