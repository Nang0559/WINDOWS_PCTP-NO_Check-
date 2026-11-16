/*
 Migration 020: queued delivery/pick detail, FIFO policy and special DB purchase-order snapshots.
 Prerequisite: 019.
 Source evidence: 00H runtime object export table definitions for
 FVN_HangChoGiao, FVN_HangChoGiaoPickDetail, FVN_ItemFifoConfig,
 FVN_ItemFifoConfig_History, FVN_Purchase_Order_DB_HD/CT,
 FVN_Purchase_Order_HVNDB and DOCQRCODE.
 SQL Server 2016. Site-specific part references use (SiteCode, PartNo).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52000, 'Migration 020 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52001, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '019')
    THROW 52002, 'Migration 019 must be applied before migration 020.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '020')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Standard QR document stream. DOCQRCODE source has no natural PK;
       add a technical key while retaining the full observed payload. */
    IF OBJECT_ID(N'dbo.FVN_DocQrCode', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_DocQrCode
        (
            QrRowId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            LOTFCC nvarchar(1000) NULL,
            MAHANGFCC nvarchar(60) NULL,
            SLTEMFCC smallint NULL,
            LOTHVN nvarchar(500) NULL,
            MAHANGHVN nvarchar(60) NULL,
            SLTEMHVN smallint NULL,
            [STATUS] smallint NULL,
            MAFCC nvarchar(50) NULL,
            STT smallint NULL,
            KETQUA nvarchar(30) NULL,
            CUA nvarchar(10) NULL,
            TRUYEN nvarchar(50) NULL,
            GIO nvarchar(50) NULL,
            STTBAN int NULL,
            SUALOTHVN nchar(30) NULL,
            TGLUU rowversion NOT NULL,
            FindTem nvarchar(50) NULL,
            SOPHIEU nvarchar(50) NULL,
            PartNoFcc nvarchar(100) NULL,
            PartNoHvn nvarchar(100) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocQrCode_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_DocQrCode PRIMARY KEY (QrRowId),
            CONSTRAINT FK_FVN_DocQrCode_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_DocQrCode_FccPart FOREIGN KEY (SiteCode, PartNoFcc)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_DocQrCode_HvnPart FOREIGN KEY (SiteCode, PartNoHvn)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo)
        );
        CREATE INDEX IX_FVN_DocQrCode_Lot ON dbo.FVN_DocQrCode (SiteCode, LOTFCC, LOTHVN);
        CREATE INDEX IX_FVN_DocQrCode_MaFcc ON dbo.FVN_DocQrCode (SiteCode, MAFCC);
        CREATE INDEX IX_FVN_DocQrCode_SoPhieu ON dbo.FVN_DocQrCode (SiteCode, SOPHIEU);
    END;

    /* Align the special DB QR stream to the complete DOCQRCODE payload. */
    IF COL_LENGTH(N'dbo.FVN_DocQrCodeDb', N'SOPHIEU') IS NULL
        ALTER TABLE dbo.FVN_DocQrCodeDb ADD SOPHIEU nvarchar(50) NULL;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_DocQrCodeDb') AND name=N'IX_FVN_DocQrCodeDb_Lot')
        DROP INDEX IX_FVN_DocQrCodeDb_Lot ON dbo.FVN_DocQrCodeDb;
    IF COL_LENGTH(N'dbo.FVN_DocQrCodeDb', N'LOTFCC') IS NOT NULL
        ALTER TABLE dbo.FVN_DocQrCodeDb ALTER COLUMN LOTFCC nvarchar(1000) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_DocQrCodeDb') AND name=N'IX_FVN_DocQrCodeDb_Lot')
        CREATE INDEX IX_FVN_DocQrCodeDb_Lot ON dbo.FVN_DocQrCodeDb (SiteCode, LOTFCC, LOTHVN);

    IF OBJECT_ID(N'dbo.FVN_HangChoGiao', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_HangChoGiao
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            MaHang nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            LotThung nvarchar(50) NULL,
            LotGoc nvarchar(50) NULL,
            MaterialLotId bigint NULL,
            SoLuong int NOT NULL,
            SlotIdNguon int NULL,
            LoaiYeuCauGiao nvarchar(30) NOT NULL,
            PhieuKhachTraId int NULL,
            PhieuXuLyBatThuongId int NULL,
            DinhDanhPhieuGiao nvarchar(200) NULL,
            TrangThai nvarchar(30) NOT NULL,
            NgayXuatKho datetime NULL,
            NguoiXuatKho nvarchar(50) NULL,
            NgayGiao datetime NULL,
            NguoiGiao nvarchar(50) NULL,
            Note nvarchar(250) NULL,
            InventoryTransactionId bigint NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_HangChoGiao_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_HangChoGiao PRIMARY KEY (Id),
            CONSTRAINT UQ_FVN_HangChoGiao_SiteId UNIQUE (SiteCode, Id),
            CONSTRAINT FK_FVN_HangChoGiao_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_HangChoGiao_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_HangChoGiao_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_HangChoGiao_Slot FOREIGN KEY (SiteCode, SlotIdNguon)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_HangChoGiao_PXLB FOREIGN KEY (SiteCode, PhieuXuLyBatThuongId)
                REFERENCES dbo.FVN_PhieuXuLyBatThuong (SiteCode, Id),
            CONSTRAINT FK_FVN_HangChoGiao_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_HangChoGiao_Qty CHECK (SoLuong > 0)
        );
        CREATE INDEX IX_FVN_HangChoGiao_Status ON dbo.FVN_HangChoGiao (SiteCode, TrangThai, Id);
        CREATE INDEX IX_FVN_HangChoGiao_PXLB ON dbo.FVN_HangChoGiao (SiteCode, PhieuXuLyBatThuongId, Id);
        CREATE INDEX IX_FVN_HangChoGiao_PartLot ON dbo.FVN_HangChoGiao (SiteCode, PartNo, LotThung, LotGoc);
    END;

    IF OBJECT_ID(N'dbo.FVN_HangChoGiaoPickDetail', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_HangChoGiaoPickDetail
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            HangChoGiaoId int NOT NULL,
            SlotIdNguon int NOT NULL,
            ItemCode nvarchar(100) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            LotNo nvarchar(500) NOT NULL,
            MaterialLotId bigint NULL,
            SlotLotId int NULL,
            Quantity int NOT NULL,
            TemCode nvarchar(500) NULL,
            QrData nvarchar(max) NULL,
            ImportDate datetime NULL,
            PhieuStatus tinyint NOT NULL,
            CreatedAt datetime NOT NULL CONSTRAINT DF_FVN_HangChoGiaoPickDetail_Created DEFAULT (GETDATE()),
            InventoryTransactionId bigint NULL,
            CONSTRAINT PK_FVN_HangChoGiaoPickDetail PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_HangChoGiaoPickDetail_HangCho FOREIGN KEY (SiteCode, HangChoGiaoId)
                REFERENCES dbo.FVN_HangChoGiao (SiteCode, Id),
            CONSTRAINT FK_FVN_HangChoGiaoPickDetail_Slot FOREIGN KEY (SiteCode, SlotIdNguon)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_HangChoGiaoPickDetail_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_HangChoGiaoPickDetail_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_HangChoGiaoPickDetail_SlotLot FOREIGN KEY (SiteCode, SlotLotId)
                REFERENCES dbo.FVN_SlotLot (SiteCode, SlotLotId),
            CONSTRAINT FK_FVN_HangChoGiaoPickDetail_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_HangChoGiaoPickDetail_Qty CHECK (Quantity > 0)
        );
        CREATE INDEX IX_FVN_HangChoGiaoPickDetail_HangChoGiaoId
            ON dbo.FVN_HangChoGiaoPickDetail (SiteCode, HangChoGiaoId, Id);
        CREATE INDEX IX_FVN_HangChoGiaoPickDetail_Lot
            ON dbo.FVN_HangChoGiaoPickDetail (SiteCode, PartNo, LotNo);
    END;

    IF OBJECT_ID(N'dbo.FVN_ItemFifoConfig', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_ItemFifoConfig
        (
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            EnforceFifo bit NOT NULL,
            UpdatedAt datetime NULL,
            UpdatedBy nvarchar(50) NULL,
            CONSTRAINT PK_FVN_ItemFifoConfig PRIMARY KEY (SiteCode, PartNo),
            CONSTRAINT FK_FVN_ItemFifoConfig_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_ItemFifoConfig_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_ItemFifoConfig_History
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            EnforceFifoCu bit NULL,
            EnforceFifoMoi bit NOT NULL,
            ThoiGian datetime NOT NULL,
            NguoiThucHien nvarchar(50) NULL,
            CONSTRAINT PK_FVN_ItemFifoConfig_History PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_ItemFifoConfig_History_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo)
        );
        CREATE INDEX IX_FVN_ItemFifoConfig_History_PartTime
            ON dbo.FVN_ItemFifoConfig_History (SiteCode, PartNo, ThoiGian DESC);
    END;

    IF OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_HD', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Purchase_Order_DB_HD
        (
            SiteCode nvarchar(30) NOT NULL,
            IDP int NOT NULL,
            NAME nvarchar(250) NULL,
            TRANGTHAIGIAO bit NULL,
            Note nvarchar(250) NULL,
            NGAYLAP smalldatetime NULL,
            NHAMAY int NOT NULL,
            NHAMAYNAME nvarchar(50) NULL,
            CONSTRAINT PK_FVN_Purchase_Order_DB_HD PRIMARY KEY (SiteCode, IDP),
            CONSTRAINT FK_FVN_Purchase_Order_DB_HD_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_Purchase_Order_DB_CT', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Purchase_Order_DB_CT
        (
            ID int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            IDP int NULL,
            GGFCC nvarchar(50) NULL,
            STT smallint NULL,
            CUA nvarchar(50) NULL,
            TRUYEN nvarchar(50) NULL,
            MAHANG nvarchar(50) NULL,
            TENHANG nvarchar(50) NULL,
            LOT nvarchar(200) NULL,
            DV nvarchar(50) NULL,
            SOLUONG smallint NULL,
            NGAYGIAO smalldatetime NULL,
            GIOGIAO nvarchar(50) NULL,
            STATUS nvarchar(50) NULL,
            TTPHIEU nvarchar(50) NULL,
            NHAMAY varchar(50) NULL,
            ADDNM int NULL,
            HOP int NULL,
            STATUSDOC nvarchar(50) NULL,
            NGAYNHAN smalldatetime NULL,
            TTNHAN bit NULL,
            Note nvarchar(250) NULL,
            CONSTRAINT PK_FVN_Purchase_Order_DB_CT PRIMARY KEY (ID),
            CONSTRAINT FK_FVN_Purchase_Order_DB_CT_Header FOREIGN KEY (SiteCode, IDP)
                REFERENCES dbo.FVN_Purchase_Order_DB_HD (SiteCode, IDP),
            CONSTRAINT FK_FVN_Purchase_Order_DB_CT_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode)
        );
        CREATE INDEX IX_FVN_Purchase_Order_DB_CT_Header
            ON dbo.FVN_Purchase_Order_DB_CT (SiteCode, IDP, STT);
    END;

    IF OBJECT_ID(N'dbo.FVN_Purchase_Order_HVNDB', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Purchase_Order_HVNDB
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            Oder_no nvarchar(20) NULL,
            Part_no nvarchar(100) NULL,
            PartNo nvarchar(100) NULL,
            Part_name nvarchar(120) NULL,
            NgayGiao smalldatetime NULL,
            Slgiao int NULL,
            QCDG int NULL,
            Gear nvarchar(160) NULL,
            CUA nchar(20) NULL,
            IsDelivered bit NULL,
            DeliveredDate datetime NULL,
            CONSTRAINT PK_FVN_Purchase_Order_HVNDB PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_Purchase_Order_HVNDB_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_Purchase_Order_HVNDB_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo)
        );
        CREATE INDEX IX_FVN_Purchase_Order_HVNDB_NgayGiao
            ON dbo.FVN_Purchase_Order_HVNDB (SiteCode, NgayGiao);
        CREATE INDEX IX_FVN_Purchase_Order_HVNDB_OrderPart
            ON dbo.FVN_Purchase_Order_HVNDB (SiteCode, Oder_no, PartNo);
    END;

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('020', N'PCTP queued delivery FIFO and purchase-order snapshots');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '020';
GO
