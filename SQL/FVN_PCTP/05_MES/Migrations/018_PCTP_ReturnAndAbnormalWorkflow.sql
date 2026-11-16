/*
 Migration 018: PCTP return documents and QT Chung / abnormal-processing workflow.
 Prerequisite: 017.
 Source contract reviewed against:
 - PCTP/Modules/XuLyHangLoi/Models/PhieuTraHang.cs
 - PCTP/Modules/XuLyHangLoi/Models/PhieuTraHangCT.cs
 - PCTP/Modules/XuLyHangLoi/Models/PhieuXuLyBatThuong.cs
 - PhieuTraHangRepository.cs / PhieuXuLyBatThuongRepository.cs
 - PHASE2, PHASE3, PHASE4_9 and PHASE13 legacy SQL
 SQL Server 2016.
 New part references use (SiteCode, PartNo). Quantity movements are posted to MES only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51800, 'Migration 018 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 51801, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '017')
    THROW 51802, 'Migration 017 must be applied before migration 018.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '018')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Return header. Status is the header-level state machine; QT Chung has its own state. */
    IF OBJECT_ID(N'dbo.FVN_PhieuTraHang', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PhieuTraHang
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            Nguon int NOT NULL,
            SoPhieu nvarchar(100) NOT NULL,
            CustomerNo nvarchar(20) NULL,
            SoPhieuKhach nvarchar(100) NULL,
            NgayPhatHanh datetime NULL,
            SlipNo nvarchar(100) NULL,
            Ca nvarchar(50) NULL,
            PhongBan nvarchar(100) NULL,
            LyDo nvarchar(1000) NULL,
            TenKhachHang nvarchar(200) NULL,
            BoPhanPhatHienLoi nvarchar(200) NULL,
            XacNhanBPPhatHienLoi nvarchar(200) NULL,
            XacNhanQCKhach nvarchar(200) NULL,
            XacNhanNhaCungCap nvarchar(200) NULL,
            NgayNhanKho datetime NULL,
            TongSoLuongNhan int NOT NULL CONSTRAINT DF_FVN_PhieuTraHang_TongSL DEFAULT (0),
            Status int NOT NULL CONSTRAINT DF_FVN_PhieuTraHang_Status DEFAULT (0),
            BoPhanNhanLai nvarchar(100) NULL,
            SoLuongGiaoLai int NULL,
            NgayGiaoLaiBoPhan datetime NULL,
            NguoiGiaoLaiBoPhan nvarchar(100) NULL,
            Note nvarchar(max) NULL,
            CreatedAt datetime NOT NULL CONSTRAINT DF_FVN_PhieuTraHang_Created DEFAULT (GETDATE()),
            CreatedBy nvarchar(100) NOT NULL,
            UpdatedAt datetime NULL,
            UpdatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PhieuTraHang PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_PhieuTraHang_SiteId UNIQUE (SiteCode, Id),
            CONSTRAINT UQ_FVN_PhieuTraHang_SoPhieu UNIQUE (SiteCode, SoPhieu),
            CONSTRAINT FK_FVN_PhieuTraHang_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT CK_FVN_PhieuTraHang_Nguon CHECK (Nguon IN (1,2)),
            CONSTRAINT CK_FVN_PhieuTraHang_Status CHECK (Status IN (0,10,20,30,75,80,100,900)),
            CONSTRAINT CK_FVN_PhieuTraHang_Qty CHECK (TongSoLuongNhan >= 0 AND (SoLuongGiaoLai IS NULL OR SoLuongGiaoLai >= 0))
        );
        CREATE INDEX IX_FVN_PhieuTraHang_NguonStatusCreated
            ON dbo.FVN_PhieuTraHang (SiteCode, Nguon, Status, CreatedAt DESC, Id DESC);
        CREATE INDEX IX_FVN_PhieuTraHang_Customer
            ON dbo.FVN_PhieuTraHang (SiteCode, CustomerNo, CreatedAt DESC);
    END;

    IF OBJECT_ID(N'dbo.FVN_PhieuTraHangCT', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PhieuTraHangCT
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuTraHangId int NOT NULL,
            SlotIdNguon int NULL,
            MaHang nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            TenHang nvarchar(384) NULL,
            LotNo nvarchar(400) NOT NULL,
            SoLuong int NOT NULL,
            LyDoNg nvarchar(1000) NULL,
            DinhDanhPhieuGiao nvarchar(250) NULL,
            PoNo nvarchar(400) NULL,
            NgayGiao datetime NULL,
            NhaMay nvarchar(120) NULL,
            PhieuXuLyBatThuongId int NULL,
            CreatedAt datetime NOT NULL CONSTRAINT DF_FVN_PhieuTraHangCT_Created DEFAULT (GETDATE()),
            CONSTRAINT PK_FVN_PhieuTraHangCT PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_PhieuTraHangCT_SiteId UNIQUE (SiteCode, Id),
            CONSTRAINT FK_FVN_PhieuTraHangCT_Header FOREIGN KEY (SiteCode, PhieuTraHangId)
                REFERENCES dbo.FVN_PhieuTraHang (SiteCode, Id),
            CONSTRAINT FK_FVN_PhieuTraHangCT_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_PhieuTraHangCT_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PhieuTraHangCT_Slot FOREIGN KEY (SiteCode, SlotIdNguon)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT CK_FVN_PhieuTraHangCT_Qty CHECK (SoLuong > 0)
        );
        CREATE INDEX IX_FVN_PhieuTraHangCT_Header
            ON dbo.FVN_PhieuTraHangCT (SiteCode, PhieuTraHangId, Id);
        CREATE INDEX IX_FVN_PhieuTraHangCT_PartLot
            ON dbo.FVN_PhieuTraHangCT (SiteCode, PartNo, LotNo);
    END;

    IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PhieuXuLyBatThuong
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            SoPhieu nvarchar(100) NOT NULL,
            Nguon int NOT NULL,
            PhieuTraHangCTId int NOT NULL,
            PhieuTraHangId int NULL,
            SoPhieuTraHangGoc nvarchar(100) NULL,
            PhieuKhachTraId int NULL,
            SlotIdNguon int NULL,
            LotNguon nvarchar(400) NULL,
            Model nvarchar(100) NULL,
            MaSanPham nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            SoLo nvarchar(100) NULL,
            SoLoLoi nvarchar(100) NULL,
            SoLuongLoi int NOT NULL CONSTRAINT DF_FVN_PhieuXuLyBatThuong_SoLuong DEFAULT (0),
            NoiDungBatThuong nvarchar(2000) NULL,
            PhanLoaiXuLy nvarchar(200) NULL,
            BoPhanPhatHanh nvarchar(200) NULL,
            Status int NOT NULL CONSTRAINT DF_FVN_PhieuXuLyBatThuong_Status DEFAULT (0),
            HuongXuLy int NOT NULL CONSTRAINT DF_FVN_PhieuXuLyBatThuong_Huong DEFAULT (0),
            NgayDinhHuong datetime NULL,
            NguoiDinhHuong nvarchar(100) NULL,
            LyDoHuy nvarchar(1000) NULL,
            NgayHuy datetime NULL,
            NguoiHuy nvarchar(100) NULL,
            CreatedAt datetime NOT NULL CONSTRAINT DF_FVN_PhieuXuLyBatThuong_Created DEFAULT (GETDATE()),
            CreatedBy nvarchar(100) NOT NULL,
            UpdatedAt datetime NULL,
            UpdatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PhieuXuLyBatThuong PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_PhieuXuLyBatThuong_SiteId UNIQUE (SiteCode, Id),
            CONSTRAINT UQ_FVN_PhieuXuLyBatThuong_SoPhieu UNIQUE (SiteCode, SoPhieu),
            CONSTRAINT UQ_FVN_PhieuXuLyBatThuong_ReturnDetail UNIQUE (SiteCode, PhieuTraHangCTId),
            CONSTRAINT FK_FVN_PhieuXuLyBatThuong_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_PhieuXuLyBatThuong_ReturnDetail FOREIGN KEY (SiteCode, PhieuTraHangCTId)
                REFERENCES dbo.FVN_PhieuTraHangCT (SiteCode, Id),
            CONSTRAINT FK_FVN_PhieuXuLyBatThuong_ReturnHeader FOREIGN KEY (SiteCode, PhieuTraHangId)
                REFERENCES dbo.FVN_PhieuTraHang (SiteCode, Id),
            CONSTRAINT FK_FVN_PhieuXuLyBatThuong_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PhieuXuLyBatThuong_Slot FOREIGN KEY (SiteCode, SlotIdNguon)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT CK_FVN_PhieuXuLyBatThuong_Nguon CHECK (Nguon IN (1,2)),
            CONSTRAINT CK_FVN_PhieuXuLyBatThuong_Status CHECK (Status IN (0,10,20,25,30,35,40,50,60,70,100,900)),
            CONSTRAINT CK_FVN_PhieuXuLyBatThuong_Huong CHECK (HuongXuLy IN (0,1,2,3)),
            CONSTRAINT CK_FVN_PhieuXuLyBatThuong_Qty CHECK (SoLuongLoi >= 0)
        );
        CREATE INDEX IX_FVN_PhieuXuLyBatThuong_Header
            ON dbo.FVN_PhieuXuLyBatThuong (SiteCode, PhieuTraHangId, Status, CreatedAt DESC);
        CREATE INDEX IX_FVN_PhieuXuLyBatThuong_PartLot
            ON dbo.FVN_PhieuXuLyBatThuong (SiteCode, PartNo, SoLoLoi);
    END;

    /* Reverse link is optional for old callers, but new insert path fills it atomically. */
    IF COL_LENGTH(N'dbo.FVN_PhieuTraHangCT', N'PhieuXuLyBatThuongId') IS NULL
        ALTER TABLE dbo.FVN_PhieuTraHangCT ADD PhieuXuLyBatThuongId int NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_PhieuTraHangCT_PXLB')
        ALTER TABLE dbo.FVN_PhieuTraHangCT
            ADD CONSTRAINT FK_FVN_PhieuTraHangCT_PXLB
            FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
            REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'IX_FVN_PhieuTraHangCT_PXLB')
        CREATE INDEX IX_FVN_PhieuTraHangCT_PXLB
            ON dbo.FVN_PhieuTraHangCT (SiteCode, PhieuXuLyBatThuongId);

    IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongAffectedLot', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PhieuXuLyBatThuongAffectedLot
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SourceType int NOT NULL,
            SourceReference nvarchar(250) NULL,
            SlotId int NULL,
            LotNo nvarchar(100) NOT NULL,
            MaSanPham nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NULL,
            Model nvarchar(100) NULL,
            SoLuongAnhHuong int NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_AnhHuong DEFAULT (0),
            SoLuongDaKiemTra int NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_DaKiemTra DEFAULT (0),
            SoLuongOK int NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_OK DEFAULT (0),
            SoLuongNG int NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_NG DEFAULT (0),
            SoLuongRework int NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_Rework DEFAULT (0),
            SoLuongLoaiBo int NOT NULL CONSTRAINT DF_FVN_PXLBTAffectedLot_LoaiBo DEFAULT (0),
            SnapshotAt datetime NOT NULL,
            SnapshotBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PhieuXuLyBatThuongAffectedLot PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLBTAffectedLot_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT FK_FVN_PXLBTAffectedLot_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PXLBTAffectedLot_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_PXLBTAffectedLot_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT CK_FVN_PXLBTAffectedLot_Source CHECK (SourceType IN (1,2,3)),
            CONSTRAINT CK_FVN_PXLBTAffectedLot_NonNegative CHECK
            (
                SoLuongAnhHuong >= 0 AND SoLuongDaKiemTra >= 0 AND SoLuongOK >= 0
                AND SoLuongNG >= 0 AND SoLuongRework >= 0 AND SoLuongLoaiBo >= 0
            )
        );
        CREATE INDEX IX_FVN_PXLBTAffectedLot_Phieu
            ON dbo.FVN_PhieuXuLyBatThuongAffectedLot (SiteCode, PhieuXuLyBatThuongId, SourceType, Id);
        CREATE INDEX IX_FVN_PXLBTAffectedLot_TraceKey
            ON dbo.FVN_PhieuXuLyBatThuongAffectedLot (SiteCode, PartNo, LotNo, SourceType);
    END;

    IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuongQCInitial', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PhieuXuLyBatThuongQCInitial
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SoLuongAnhHuong int NOT NULL,
            SoLuongDaKiemTra int NOT NULL,
            SoLuongOK int NOT NULL,
            SoLuongNG int NOT NULL,
            SoLuongRework int NOT NULL,
            SoLuongLoaiBoBanDau int NOT NULL,
            NoiDungKiemTra nvarchar(1000) NULL,
            KetLuan nvarchar(1000) NULL,
            ConfirmedAt datetime NULL,
            ConfirmedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PhieuXuLyBatThuongQCInitial PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLB_QCInitial_PXLB FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT UQ_FVN_PXLB_QCInitial_Phieu UNIQUE (SiteCode, PhieuXuLyBatThuongId),
            CONSTRAINT CK_FVN_PXLB_QCInitial_Qty_NonNegative CHECK
            (
                SoLuongAnhHuong >= 0 AND SoLuongDaKiemTra >= 0 AND SoLuongOK >= 0
                AND SoLuongNG >= 0 AND SoLuongRework >= 0 AND SoLuongLoaiBoBanDau >= 0
            ),
            CONSTRAINT CK_FVN_PXLB_QCInitial_OK_NG CHECK (SoLuongDaKiemTra = SoLuongOK + SoLuongNG),
            CONSTRAINT CK_FVN_PXLB_QCInitial_Rework_Disposition CHECK (SoLuongNG = SoLuongRework + SoLuongLoaiBoBanDau)
        );
    END;

    /* Independent rework / disposition / compensation child records. */
    IF OBJECT_ID(N'dbo.FVN_PXLB_ReworkPlan', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_ReworkPlan
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SoLuongKeHoach int NOT NULL,
            SoLuongDaXuat int NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_DaXuat DEFAULT (0),
            SoLuongDaGiao int NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_DaGiao DEFAULT (0),
            Status int NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_Status DEFAULT (0),
            CreatedAt datetime NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkPlan_CreatedAt DEFAULT (GETDATE()),
            CreatedBy nvarchar(100) NOT NULL,
            UpdatedAt datetime NULL,
            UpdatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PXLB_ReworkPlan PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_PXLB_ReworkPlan_SiteId UNIQUE (SiteCode, Id),
            CONSTRAINT FK_FVN_PXLB_ReworkPlan_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT UQ_FVN_PXLB_ReworkPlan_Phieu UNIQUE (SiteCode, PhieuXuLyBatThuongId),
            CONSTRAINT CK_FVN_PXLB_ReworkPlan_Qty CHECK
                (SoLuongKeHoach >= 0 AND SoLuongDaXuat >= 0 AND SoLuongDaGiao >= 0
                 AND SoLuongDaXuat <= SoLuongKeHoach AND SoLuongDaGiao <= SoLuongDaXuat)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_PXLB_ReworkMovement', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_ReworkMovement
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            ReworkPlanId int NOT NULL,
            MovementType int NOT NULL,
            SlotId int NULL,
            PartNo nvarchar(100) NOT NULL,
            LotNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NULL,
            SoLuong int NOT NULL,
            ThoiGian datetime NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkMovement_Time DEFAULT (GETDATE()),
            NguoiThucHien nvarchar(100) NOT NULL,
            GhiChu nvarchar(1000) NULL,
            InventoryTransactionId bigint NULL,
            CONSTRAINT PK_FVN_PXLB_ReworkMovement PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLB_ReworkMovement_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT FK_FVN_PXLB_ReworkMovement_Plan FOREIGN KEY (SiteCode, ReworkPlanId)
                REFERENCES dbo.FVN_PXLB_ReworkPlan (SiteCode, Id),
            CONSTRAINT FK_FVN_PXLB_ReworkMovement_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PXLB_ReworkMovement_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_PXLB_ReworkMovement_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_PXLB_ReworkMovement_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_PXLB_ReworkMovement_Qty CHECK (SoLuong > 0),
            CONSTRAINT CK_FVN_PXLB_ReworkMovement_Type CHECK (MovementType IN (1,2))
        );
        CREATE INDEX IX_FVN_PXLB_ReworkMovement_Phieu
            ON dbo.FVN_PXLB_ReworkMovement (SiteCode, PhieuXuLyBatThuongId, MovementType, Id);
    END;

    IF OBJECT_ID(N'dbo.FVN_PXLB_ReworkQC', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_ReworkQC
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SoLuongRework int NOT NULL,
            SoLuongOK int NOT NULL,
            SoLuongNG int NOT NULL,
            ConfirmedAt datetime NOT NULL CONSTRAINT DF_FVN_PXLB_ReworkQC_Time DEFAULT (GETDATE()),
            ConfirmedBy nvarchar(100) NOT NULL,
            KetLuan nvarchar(1000) NULL,
            CONSTRAINT PK_FVN_PXLB_ReworkQC PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLB_ReworkQC_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT UQ_FVN_PXLB_ReworkQC_Phieu UNIQUE (SiteCode, PhieuXuLyBatThuongId),
            CONSTRAINT CK_FVN_PXLB_ReworkQC_Qty CHECK
                (SoLuongRework >= 0 AND SoLuongOK >= 0 AND SoLuongNG >= 0 AND SoLuongRework = SoLuongOK + SoLuongNG)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_PXLB_Disposition', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_Disposition
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SoLuongLoaiBoBanDau int NOT NULL,
            SoLuongNGRework int NOT NULL,
            SoLuongLoaiBoCuoi int NOT NULL,
            Reason nvarchar(1000) NULL,
            ConfirmedAt datetime NOT NULL CONSTRAINT DF_FVN_PXLB_Disposition_Time DEFAULT (GETDATE()),
            ConfirmedBy nvarchar(100) NOT NULL,
            CONSTRAINT PK_FVN_PXLB_Disposition PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLB_Disposition_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT UQ_FVN_PXLB_Disposition_Phieu UNIQUE (SiteCode, PhieuXuLyBatThuongId),
            CONSTRAINT CK_FVN_PXLB_Disposition_Qty CHECK
                (SoLuongLoaiBoBanDau >= 0 AND SoLuongNGRework >= 0 AND SoLuongLoaiBoCuoi >= 0
                 AND SoLuongLoaiBoCuoi = SoLuongLoaiBoBanDau + SoLuongNGRework)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_PXLB_Compensation', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_Compensation
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            PhieuKhachTraId int NULL,
            OrderId int NULL,
            OrderLineId int NULL,
            OriginalDeliveryReference nvarchar(200) NULL,
            SoLuongYeuCau int NOT NULL,
            SoLuongDaGiao int NOT NULL CONSTRAINT DF_FVN_PXLB_Comp_DaGiao DEFAULT (0),
            Status int NOT NULL CONSTRAINT DF_FVN_PXLB_Comp_Status DEFAULT (0),
            SourceReference nvarchar(200) NULL,
            CreatedAt datetime NOT NULL CONSTRAINT DF_FVN_PXLB_Comp_CreatedAt DEFAULT (GETDATE()),
            CreatedBy nvarchar(100) NOT NULL,
            CompletedAt datetime NULL,
            CompletedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PXLB_Compensation PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_PXLB_Compensation_SiteId UNIQUE (SiteCode, Id),
            CONSTRAINT FK_FVN_PXLB_Compensation_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT UQ_FVN_PXLB_Compensation_Phieu UNIQUE (SiteCode, PhieuXuLyBatThuongId),
            CONSTRAINT CK_FVN_PXLB_Compensation_Qty CHECK
                (SoLuongYeuCau >= 0 AND SoLuongDaGiao >= 0 AND SoLuongDaGiao <= SoLuongYeuCau)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_PXLB_CompensationMovement', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_CompensationMovement
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            CompensationId int NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            LotNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NULL,
            SlotId int NULL,
            SoLuong int NOT NULL,
            ThoiGian datetime NOT NULL CONSTRAINT DF_FVN_PXLB_CompMove_Time DEFAULT (GETDATE()),
            NguoiThucHien nvarchar(100) NOT NULL,
            InventoryTransactionId bigint NULL,
            CONSTRAINT PK_FVN_PXLB_CompensationMovement PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLB_CompMove_Comp FOREIGN KEY (SiteCode, CompensationId)
                REFERENCES dbo.FVN_PXLB_Compensation (SiteCode, Id),
            CONSTRAINT FK_FVN_PXLB_CompMove_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PXLB_CompMove_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_PXLB_CompMove_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_PXLB_CompMove_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_PXLB_CompMove_Qty CHECK (SoLuong > 0)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_PXLB_WorkflowAudit', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PXLB_WorkflowAudit
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            FromStatus int NOT NULL,
            ToStatus int NOT NULL,
            ActionName nvarchar(100) NOT NULL,
            Actor nvarchar(100) NOT NULL,
            OccurredAt datetime NOT NULL CONSTRAINT DF_FVN_PXLB_WorkflowAudit_Time DEFAULT (GETDATE()),
            Note nvarchar(1000) NULL,
            CONSTRAINT PK_FVN_PXLB_WorkflowAudit PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_PXLB_WorkflowAudit_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id)
        );
        CREATE INDEX IX_FVN_PXLB_WorkflowAudit_Phieu
            ON dbo.FVN_PXLB_WorkflowAudit (SiteCode, PhieuXuLyBatThuongId, OccurredAt, Id);
    END;

    /* Existing QT Chung persistence tables (distinct from the optional PXLB projections). */
    IF OBJECT_ID(N'dbo.FVN_TraHangQTChungXuat', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_TraHangQTChungXuat
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SlotIdNguon int NOT NULL,
            LotXuat nvarchar(100) NOT NULL,
            LoaiXuat nvarchar(30) NULL,
            MaHang nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            SoLuongXuat int NOT NULL,
            TonTruoc int NOT NULL,
            TonSau int NOT NULL,
            NgayXuat datetime NOT NULL CONSTRAINT DF_FVN_TraHangQTChungXuat_NgayXuat DEFAULT (GETDATE()),
            NguoiXuat nvarchar(100) NULL,
            LyDo nvarchar(500) NULL,
            Note nvarchar(max) NULL,
            MaterialLotId bigint NULL,
            InventoryTransactionId bigint NULL,
            CONSTRAINT PK_FVN_TraHangQTChungXuat PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_TraHangQTChungXuat_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT FK_FVN_TraHangQTChungXuat_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_TraHangQTChungXuat_Lot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_TraHangQTChungXuat_Slot FOREIGN KEY (SiteCode, SlotIdNguon)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_TraHangQTChungXuat_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_TraHangQTChungXuat_Qty CHECK (SoLuongXuat > 0)
        );
        CREATE INDEX IX_FVN_TraHangQTChungXuat_Phieu_Ngay
            ON dbo.FVN_TraHangQTChungXuat (SiteCode, PhieuXuLyBatThuongId, NgayXuat, Id);
    END;

    IF OBJECT_ID(N'dbo.FVN_TraHangQTChungGiao', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_TraHangQTChungGiao
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            LotGiao nvarchar(100) NULL,
            MaHang nvarchar(100) NULL,
            SoLuongGiao int NOT NULL,
            ThoiGian datetime NOT NULL CONSTRAINT DF_FVN_TraHangQTChungGiao_ThoiGian DEFAULT (GETDATE()),
            NgayGiao nvarchar(100) NULL,
            NguoiNhan nvarchar(100) NULL,
            BoPhanNhan nvarchar(100) NULL,
            SoPhieuGiaoNhan nvarchar(100) NULL,
            Note nvarchar(max) NULL,
            PartNo nvarchar(100) NULL,
            MaterialLotId bigint NULL,
            CONSTRAINT PK_FVN_TraHangQTChungGiao PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_TraHangQTChungGiao_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT FK_FVN_TraHangQTChungGiao_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_TraHangQTChungGiao_Lot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT CK_FVN_TraHangQTChungGiao_Qty CHECK (SoLuongGiao > 0)
        );
        CREATE INDEX IX_FVN_TraHangQTChungGiao_Phieu_ThoiGian
            ON dbo.FVN_TraHangQTChungGiao (SiteCode, PhieuXuLyBatThuongId, ThoiGian, Id);
    END;

    IF OBJECT_ID(N'dbo.FVN_TraHangQTChungQC', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_TraHangQTChungQC
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SoLuongDaRework int NOT NULL,
            SoLuongOK int NOT NULL,
            SoLuongNG int NOT NULL,
            DaKiemTraTem bit NOT NULL CONSTRAINT DF_FVN_TraHangQTChungQC_DaKiemTraTem DEFAULT (0),
            ThoiGian datetime NOT NULL CONSTRAINT DF_FVN_TraHangQTChungQC_ThoiGian DEFAULT (GETDATE()),
            NguoiQC nvarchar(100) NULL,
            KetLuan nvarchar(100) NULL,
            Note nvarchar(max) NULL,
            CONSTRAINT PK_FVN_TraHangQTChungQC PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_TraHangQTChungQC_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT CK_FVN_TraHangQTChungQC_Qty CHECK
                (SoLuongDaRework >= 0 AND SoLuongOK >= 0 AND SoLuongNG >= 0 AND SoLuongDaRework = SoLuongOK + SoLuongNG)
        );
        CREATE INDEX IX_FVN_TraHangQTChungQC_Phieu_ThoiGian
            ON dbo.FVN_TraHangQTChungQC (SiteCode, PhieuXuLyBatThuongId, ThoiGian DESC, Id DESC);
    END;

    IF OBJECT_ID(N'dbo.FVN_TraHangQTChungNhapNG', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_TraHangQTChungNhapNG
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PhieuXuLyBatThuongId int NOT NULL,
            SlotIdOK int NULL,
            SlotIdNG int NULL,
            LotNhapLai nvarchar(100) NOT NULL,
            MaHang nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            SoLuongNG int NOT NULL,
            SlotIdNhap int NULL,
            NgayNhap datetime NOT NULL CONSTRAINT DF_FVN_TraHangQTChungNhapNG_NgayNhap DEFAULT (GETDATE()),
            NguoiNhap nvarchar(100) NULL,
            LyDo nvarchar(500) NULL,
            Note nvarchar(max) NULL,
            MaterialLotId bigint NULL,
            InventoryTransactionId bigint NULL,
            CONSTRAINT PK_FVN_TraHangQTChungNhapNG PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_Phieu FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_Lot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_SlotOK FOREIGN KEY (SiteCode, SlotIdOK)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_SlotNG FOREIGN KEY (SiteCode, SlotIdNG)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_SlotNhap FOREIGN KEY (SiteCode, SlotIdNhap)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_TraHangQTChungNhapNG_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_TraHangQTChungNhapNG_Qty CHECK (SoLuongNG > 0)
        );
        CREATE INDEX IX_FVN_TraHangQTChungNhapNG_Phieu_Ngay
            ON dbo.FVN_TraHangQTChungNhapNG (SiteCode, PhieuXuLyBatThuongId, NgayNhap, Id);
    END;

    IF OBJECT_ID(N'dbo.FVN_sys_WorkflowTransitions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_sys_WorkflowTransitions
        (
            Id int IDENTITY(1,1) NOT NULL,
            ProcessCode nvarchar(50) NOT NULL,
            FromStatus int NOT NULL,
            ToStatus int NOT NULL,
            ActionName nvarchar(100) NOT NULL,
            Description nvarchar(500) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_sys_WorkflowTransitions_Active DEFAULT (1),
            CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_FVN_sys_WorkflowTransitions_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_sys_WorkflowTransitions PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_sys_WorkflowTransitions UNIQUE (ProcessCode, FromStatus, ToStatus, ActionName)
        );
    END;

    /* QT_CHUNG transition seed, preserving the application enum values. */
    INSERT INTO dbo.FVN_sys_WorkflowTransitions (ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    SELECT v.ProcessCode,v.FromStatus,v.ToStatus,v.ActionName,v.Description,1
    FROM
    (
        VALUES
        (N'QT_CHUNG',10,20,N'QC_DINH_HUONG',N'QC xác định hướng xử lý'),
        (N'QT_CHUNG',20,25,N'TU_CHOI_GIAO_BU',N'QC xác nhận không phát sinh giao bù'),
        (N'QT_CHUNG',20,30,N'CHO_GIAO_BU',N'Tạo nghĩa vụ giao bù độc lập'),
        (N'QT_CHUNG',20,40,N'BAT_DAU_REWORK',N'Initial QC đã phân bổ Rework'),
        (N'QT_CHUNG',25,100,N'HOAN_TAT',N'Khiếu nại không có căn cứ, kết thúc không giao bù'),
        (N'QT_CHUNG',20,100,N'HOAN_TAT',N'Initial QC 100% OK'),
        (N'QT_CHUNG',30,35,N'HOAN_TAT_GIAO_BU',N'Đã giao đủ nghĩa vụ giao bù'),
        (N'QT_CHUNG',35,100,N'HOAN_TAT',N'Hoàn tất nhánh giao bù'),
        (N'QT_CHUNG',40,50,N'GIAO_SAN_XUAT',N'Đã xuất đủ Rework và giao sản xuất'),
        (N'QT_CHUNG',50,60,N'QC_REWORK',N'QC xác nhận kết quả sau Rework'),
        (N'QT_CHUNG',60,70,N'NHAP_LAI_NG',N'Có NG sau Rework cần nhập/Disposition'),
        (N'QT_CHUNG',60,100,N'HOAN_TAT',N'Rework QC 100% OK'),
        (N'QT_CHUNG',70,100,N'HOAN_TAT',N'Đã xử lý toàn bộ NG/Disposition')
    ) v(ProcessCode,FromStatus,ToStatus,ActionName,Description)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions t
        WHERE t.ProcessCode=v.ProcessCode AND t.FromStatus=v.FromStatus
          AND t.ToStatus=v.ToStatus AND t.ActionName=v.ActionName AND t.IsActive=1
    );

    INSERT INTO dbo.FVN_sys_WorkflowTransitions (ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    SELECT N'QT_CHUNG',v.StatusValue,900,N'HUY',N'Hủy phiếu do sai thao tác/trùng/sai nguồn',1
    FROM (VALUES(10),(20),(25),(30),(35),(40),(50),(60),(70)) v(StatusValue)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions t
        WHERE t.ProcessCode=N'QT_CHUNG' AND t.FromStatus=v.StatusValue
          AND t.ToStatus=900 AND t.IsActive=1
    );

    /* Report view follows the actual QT Chung persistence tables. */
    EXEC(N'
    CREATE VIEW dbo.vFVN_PXLB_ProcessReport
    AS
    SELECT
        p.Id AS PhieuXuLyBatThuongId,
        p.SoPhieu,
        p.Model,
        p.MaSanPham,
        p.Status,
        p.HuongXuLy,
        initialQc.SoLuongAnhHuong,
        initialQc.SoLuongOK AS InitialOK,
        initialQc.SoLuongNG AS InitialNG,
        initialQc.SoLuongRework,
        initialQc.SoLuongLoaiBoBanDau,
        ISNULL(xuat.ReworkDaXuat, 0) AS ReworkDaXuat,
        ISNULL(giao.ReworkDaGiao, 0) AS ReworkDaGiao,
        reworkQc.SoLuongOK AS ReworkOK,
        reworkQc.SoLuongNG AS ReworkNG,
        ISNULL(initialQc.SoLuongLoaiBoBanDau, 0) + ISNULL(nhapNg.TongNGNhapLai, 0) AS SoLuongLoaiBoCuoi,
        comp.SoLuongYeuCau AS CompensationRequired,
        comp.SoLuongDaGiao AS CompensationDelivered,
        CASE WHEN comp.Id IS NULL THEN 0 ELSE comp.SoLuongYeuCau - comp.SoLuongDaGiao END AS CompensationRemaining
    FROM dbo.FVN_PhieuXuLyBatThuong p
    LEFT JOIN dbo.FVN_PhieuXuLyBatThuongQCInitial initialQc
        ON initialQc.SiteCode=p.SiteCode AND initialQc.PhieuXuLyBatThuongId=p.Id
    OUTER APPLY
    (
        SELECT SUM(x.SoLuongXuat) AS ReworkDaXuat
        FROM dbo.FVN_TraHangQTChungXuat x
        WHERE x.SiteCode=p.SiteCode AND x.PhieuXuLyBatThuongId=p.Id
    ) xuat
    OUTER APPLY
    (
        SELECT SUM(g.SoLuongGiao) AS ReworkDaGiao
        FROM dbo.FVN_TraHangQTChungGiao g
        WHERE g.SiteCode=p.SiteCode AND g.PhieuXuLyBatThuongId=p.Id
    ) giao
    OUTER APPLY
    (
        SELECT TOP (1) q.SoLuongDaRework, q.SoLuongOK, q.SoLuongNG
        FROM dbo.FVN_TraHangQTChungQC q
        WHERE q.SiteCode=p.SiteCode AND q.PhieuXuLyBatThuongId=p.Id
        ORDER BY q.ThoiGian DESC, q.Id DESC
    ) reworkQc
    OUTER APPLY
    (
        SELECT SUM(n.SoLuongNG) AS TongNGNhapLai
        FROM dbo.FVN_TraHangQTChungNhapNG n
        WHERE n.SiteCode=p.SiteCode AND n.PhieuXuLyBatThuongId=p.Id
    ) nhapNg
    LEFT JOIN dbo.FVN_PXLB_Compensation comp
        ON comp.SiteCode=p.SiteCode AND comp.PhieuXuLyBatThuongId=p.Id;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('018', N'PCTP return and abnormal processing workflow');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Key business invariants are enforced by table constraints:
   - one abnormal-processing ticket per return detail;
   - QC inspected = OK + NG;
   - NG = Rework + initial disposition;
   - Rework = post-rework OK + NG;
   - final disposition = initial disposition + post-rework NG;
   - customer compensation is an independent obligation. */
SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '018';
GO
