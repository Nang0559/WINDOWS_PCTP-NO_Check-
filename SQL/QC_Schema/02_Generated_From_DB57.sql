/*
  STALE CHECKED-IN ARTIFACT — DO NOT DEPLOY.
  This file predates the PCTP-only runtime inventory scope and may contain unrelated
  B7R2_FCC dbo objects. Regenerate from docs/PCTP_SQL_Runtime_Inventory_A-K.csv,
  SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt and docs/SQL2_ColumnInventory.csv
  before use. CI regenerates the scoped artifact in its disposable runner.
*/
/*
 AUTO-GENERATED from SQL2 DB57 column metadata for allow-listed GAP-no-git-ddl tables.
 REVIEW REQUIRED: this file does not synthesize PK/unique indexes, FKs, CHECK constraints,
 triggers, permissions or procedure dependencies. Alias types require an explicit verified map.
 Apply only to disposable PCTP_QC_CI. Never apply to DB57 or production.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.B20Gear', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[B20Gear] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [BranchCode] nvarchar(3) NOT NULL DEFAULT (''),
        [Code] int NOT NULL,
        [Name] nvarchar(10) NOT NULL,
        [Name2] nvarchar(64) NOT NULL DEFAULT (''),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.B20Item', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[B20Item] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ParentId] int NOT NULL DEFAULT ((-1)),
        [IsGroup] bit NOT NULL DEFAULT ((0)),
        [BranchCode] varchar(3) NOT NULL DEFAULT (''),
        [Code] nvarchar(50) NOT NULL,
        [BarCode] nvarchar(24) NOT NULL DEFAULT (''),
        [Name] nvarchar(192) NOT NULL DEFAULT (''),
        [Name2] nvarchar(192) NOT NULL DEFAULT (''),
        [Unit] nvarchar(8) NOT NULL DEFAULT (''),
        [Unit0] nvarchar(8) NOT NULL DEFAULT (''),
        [ConvertRate0] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Unit1] nvarchar(8) NOT NULL DEFAULT (''),
        [ConvertRate1] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Unit2] nvarchar(8) NOT NULL DEFAULT (''),
        [ConvertRate2] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Weight] numeric(15,4) NOT NULL DEFAULT ((1)),
        [UnitCost] numeric(18,2) NOT NULL DEFAULT ((0)),
        [UnitPrice] numeric(18,2) NOT NULL DEFAULT ((0)),
        [MinCloseQty] numeric(15,4) NOT NULL DEFAULT ((0)),
        [MaxCloseQty] numeric(15,4) NOT NULL DEFAULT ((0)),
        [SalesTaxCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ExciseTaxCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ExciseTaxRate] numeric(8,4) NOT NULL DEFAULT ((0)),
        [ItemType] tinyint NOT NULL DEFAULT ((2)),
        [ItemCatgCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ItemGroupCode] nvarchar(16) NOT NULL DEFAULT (''),
        [ProductCostId] varchar(16) NOT NULL DEFAULT (''),
        [ProductItemCode] nvarchar(24) NOT NULL DEFAULT (''),
        [EnvCode] nvarchar(16) NOT NULL DEFAULT (''),
        [EnvTaxRate] numeric(15,5) NOT NULL DEFAULT ((1)),
        [Length] numeric(15,4) NOT NULL DEFAULT ((0)),
        [Width] numeric(15,4) NOT NULL DEFAULT ((0)),
        [Height] numeric(15,4) NOT NULL DEFAULT ((0)),
        [IsItemWithSerialNo] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithInstall] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithTransport] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithScale] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithLocation] tinyint NOT NULL DEFAULT ((0)),
        [IsItemWithLot] tinyint NOT NULL DEFAULT ((0)),
        [ShelfLife] tinyint NOT NULL DEFAULT ((0)),
        [ManufacturerCode] nvarchar(16) NOT NULL DEFAULT (''),
        [Comment] nvarchar(192) NOT NULL DEFAULT (''),
        [ClassCode1] nvarchar(16) NOT NULL DEFAULT (''),
        [ClassCode2] nvarchar(16) NOT NULL DEFAULT (''),
        [ClassCode3] nvarchar(16) NOT NULL DEFAULT (''),
        [IsActive] bit NOT NULL DEFAULT ((1)),
        [CreatedBy] int NOT NULL DEFAULT ((-1)),
        [CreatedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [ModifiedBy] int NOT NULL DEFAULT ((-1)),
        [ModifiedAt] datetime NOT NULL DEFAULT (getutcdate()),
        [timestamp] timestamp NOT NULL,
        [_ParentCode0] nvarchar(24) NULL,
        [Model] nvarchar(24) NOT NULL DEFAULT (''),
        [DeptCode] nvarchar(16) NULL,
        [LinesCode] nvarchar(16) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_HangChoGiao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[FVN_HangChoGiao] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [MaHang] nvarchar(50) NOT NULL,
        [LotThung] nvarchar(50) NULL,
        [LotGoc] nvarchar(50) NULL,
        [SoLuong] int NOT NULL,
        [SlotIdNguon] int NULL,
        [LoaiYeuCauGiao] nvarchar(30) NOT NULL,
        [PhieuKhachTraId] int NULL,
        [PhieuXuLyBatThuongId] int NULL,
        [DinhDanhPhieuGiao] nvarchar(200) NULL,
        [TrangThai] nvarchar(30) NOT NULL,
        [NgayXuatKho] datetime NULL,
        [NguoiXuatKho] nvarchar(50) NULL,
        [NgayGiao] datetime NULL,
        [NguoiGiao] nvarchar(50) NULL,
        [Note] nvarchar(250) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_ItemFifoConfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[FVN_ItemFifoConfig] (
        [ItemCode] nvarchar(60) NOT NULL,
        [EnforceFifo] bit NOT NULL DEFAULT ((0)),
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[FVN_ItemFifoConfig_History] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ItemCode] nvarchar(60) NOT NULL,
        [EnforceFifoCu] bit NULL,
        [EnforceFifoMoi] bit NOT NULL,
        [ThoiGian] datetime NOT NULL DEFAULT (getdate()),
        [NguoiThucHien] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PhieuTraHang', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[FVN_PhieuTraHang] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Nguon] int NOT NULL,
        [SoPhieu] nvarchar(50) NULL,
        [NguonKhachTra] int NULL,
        [SoPhieuKhach] nvarchar(50) NULL,
        [NgayPhatHanh] datetime NULL,
        [SlipNo] nvarchar(50) NULL,
        [Ca] nvarchar(10) NULL,
        [PhongBan] nvarchar(100) NULL,
        [LyDo] nvarchar(500) NULL,
        [TenKhachHang] nvarchar(200) NULL,
        [BoPhanPhatHienLoi] nvarchar(100) NULL,
        [XacNhanBPPhatHienLoi] nvarchar(100) NULL,
        [XacNhanQCKhach] nvarchar(100) NULL,
        [XacNhanNhaCungCap] nvarchar(100) NULL,
        [NgayNhanKho] datetime NULL,
        [TongSoLuongNhan] int NOT NULL DEFAULT ((0)),
        [Status] int NOT NULL DEFAULT ((0)),
        [BoPhanNhanLai] nvarchar(100) NULL,
        [SoLuongGiaoLai] int NULL,
        [NgayGiaoLaiBoPhan] datetime NULL,
        [NguoiGiaoLaiBoPhan] nvarchar(50) NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedAt] datetime NOT NULL DEFAULT (getdate()),
        [CreatedBy] nvarchar(50) NULL,
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(50) NULL,
        [CustomerNo] nvarchar(20) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PhieuTraHangCT', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[FVN_PhieuTraHangCT] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PhieuTraHangId] int NOT NULL,
        [SlotIdNguon] int NULL,
        [MaHang] nvarchar(50) NULL,
        [TenHang] nvarchar(200) NULL,
        [LotNo] nvarchar(100) NULL,
        [SoLuong] int NOT NULL DEFAULT ((0)),
        [LyDoNg] nvarchar(500) NULL,
        [DinhDanhPhieuGiao] nvarchar(100) NULL,
        [PoNo] nvarchar(50) NULL,
        [NgayGiao] datetime NULL,
        [NhaMay] nvarchar(100) NULL,
        [PhieuXuLyBatThuongId] int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[FVN_PhieuXuLyBatThuong] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SoPhieu] nvarchar(50) NOT NULL,
        [Nguon] int NOT NULL,
        [PhieuTraHangId] int NULL,
        [PhieuTraHangCTId] int NOT NULL,
        [SoPhieuTraHangGoc] nvarchar(50) NULL,
        [PhieuKhachTraId] int NULL,
        [SlotIdNguon] int NULL,
        [LotNguon] nvarchar(50) NULL,
        [Model] nvarchar(100) NULL,
        [MaSanPham] nvarchar(50) NULL,
        [SoLo] nvarchar(50) NULL,
        [SoLoLoi] nvarchar(50) NULL,
        [SoLuongLoi] int NOT NULL DEFAULT ((0)),
        [NoiDungBatThuong] nvarchar(500) NULL,
        [PhanLoaiXuLy] nvarchar(100) NULL,
        [BoPhanPhatHanh] nvarchar(100) NULL,
        [Status] int NOT NULL DEFAULT ((0)),
        [HuongXuLy] int NOT NULL DEFAULT ((0)),
        [NgayDinhHuong] datetime NULL,
        [NguoiDinhHuong] nvarchar(50) NULL,
        [CreatedAt] datetime NOT NULL DEFAULT (getdate()),
        [CreatedBy] nvarchar(50) NULL,
        [UpdatedAt] datetime NULL,
        [UpdatedBy] nvarchar(50) NULL,
        [LyDoHuy] nvarchar(500) NULL,
        [NgayHuy] datetime NULL,
        [NguoiHuy] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.IFSPHIEUGIAOHANG', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[IFSPHIEUGIAOHANG] (
        [STT] nvarchar(500) NULL,
        [FIND] nvarchar(500) NULL,
        [CUSTOMER_NO] nvarchar(500) NULL,
        [GIOGIAO] nvarchar(500) NULL,
        [SOLUONG] decimal(18,4) NULL,
        [DV] nvarchar(500) NULL,
        [ADDNM] nvarchar(500) NULL,
        [CUA] nvarchar(500) NULL,
        [MAHANG] nvarchar(500) NULL,
        [TENHANG] nvarchar(500) NULL,
        [CUSTOMER_PO_REL_NO] nvarchar(500) NULL,
        [TRUYEN] nvarchar(500) NULL,
        [ORDER_NO] nvarchar(500) NULL,
        [CATALOG_NO] nvarchar(500) NULL,
        [NGAYGIAO] nvarchar(500) NULL,
        [CUSTOMER_PO_NO] nvarchar(500) NULL,
        [HOP] nvarchar(500) NULL,
        [LOT] nvarchar(500) NULL,
        [DIA_CHI] nvarchar(500) NULL,
        [KGX] nvarchar(500) NULL,
        [STATUS] nvarchar(500) NULL,
        [STATUSDOC] nvarchar(500) NULL,
        [TTPHIEU] nvarchar(500) NULL,
        [GIOGIAOFCC] nvarchar(500) NULL,
        [SHIP_ADDR_NO] nvarchar(500) NULL,
        [NHAMAY] nvarchar(500) NULL,
        [NOTE] nvarchar(500) NULL,
        [PO_NO] nvarchar(500) NULL,
        [PO_ITEM] nvarchar(500) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.InspectionConfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[InspectionConfig] (
        [ConfigId] int IDENTITY(1,1) NOT NULL,
        [ItemCode] nvarchar(100) NOT NULL,
        [DefaultQty] int NOT NULL DEFAULT ((1)),
        [CheckItemCode] bit NULL DEFAULT ((1)),
        [CheckLotNo] bit NULL DEFAULT ((1)),
        [CheckNSX] bit NULL DEFAULT ((1)),
        [IsActive] bit NULL DEFAULT ((1)),
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime NULL DEFAULT (getdate())
    );
END;
GO

IF OBJECT_ID(N'dbo.InspectionLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[InspectionLog] (
        [LogId] int IDENTITY(1,1) NOT NULL,
        [InspectionCode] nvarchar(50) NOT NULL,
        [ItemCode] nvarchar(100) NULL,
        [TemCodeTong] nvarchar(200) NULL,
        [LotNoTong] nvarchar(200) NULL,
        [NSXTong] nvarchar(50) NULL,
        [SoLuongTong] int NULL,
        [MaPhieu] nvarchar(100) NULL,
        [BoxTemCode] nvarchar(200) NULL,
        [BoxLotNo] nvarchar(200) NULL,
        [BoxNSX] nvarchar(50) NULL,
        [IsMatch] bit NULL DEFAULT ((1)),
        [MismatchFields] nvarchar(500) NULL,
        [FinalResult] nvarchar(20) NULL,
        [CheckedAt] datetime NULL DEFAULT (getdate()),
        [CheckedBy] nvarchar(100) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.PURCHASE_ORDER_YMVN', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[PURCHASE_ORDER_YMVN] (
        [Oder_no] nvarchar(10) NULL,
        [Part_no] nvarchar(50) NULL,
        [Part_name] nvarchar(60) NULL,
        [NgayGiao] smalldatetime NULL,
        [Slgiao] int NULL,
        [QCDG] int NULL,
        [Gear] nvarchar(80) NULL,
        [CUA] nchar(10) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.QRCODE_CHANGETIME', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[QRCODE_CHANGETIME] (
        [GioFCCVP] nvarchar(50) NULL,
        [GIOHVN] nvarchar(5) NULL,
        [GIOFCCHN] nvarchar(50) NULL,
        [ID] int NULL,
        [GIOCBVP] nvarchar(50) NULL,
        [GIOCBHN] nvarchar(50) NULL,
        [DisplayName] nvarchar(50) NOT NULL DEFAULT ('Name')
    );
END;
GO

IF OBJECT_ID(N'dbo.StockHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[StockHistory] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ActionType] varchar(30) NOT NULL,
        [ItemCode] nvarchar(100) NULL,
        [TemCode] nvarchar(100) NULL,
        [LotNo] nvarchar(200) NULL,
        [Quantity] int NULL,
        [Date] datetime NULL,
        [SlotId] int NULL,
        [PerformedBy] nvarchar(100) NULL,
        [QrData] nvarchar(MAX) NULL,
        [MaPhieu] nvarchar(50) NULL,
        [FromSlotId] int NULL,
        [ToSlotId] int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.sys_WorkflowTransitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[sys_WorkflowTransitions] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ProcessCode] varchar(50) NOT NULL,
        [FromStatus] int NOT NULL,
        [ToStatus] int NOT NULL,
        [ActionName] nvarchar(150) NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT ((1))
    );
END;
GO

IF OBJECT_ID(N'dbo.tbl_QR_ComparePart', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[tbl_QR_ComparePart] (
        [STT] int NULL,
        [PartNo] nvarchar(50) NULL,
        [PartName] nvarchar(60) NULL,
        [PartNoCompare] nvarchar(50) NULL,
        [timeSet] smalldatetime NULL,
        [IsActive] bit NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TMPPHIEUGIAODBHD] (
        [IDP] int IDENTITY(1,1) NOT NULL,
        [NAME] nvarchar(250) NULL,
        [TRANGTHAIGIAO] bit NULL,
        [Note] nvarchar(250) NULL,
        [NGAYLAP] smalldatetime NULL,
        [NHAMAY] int NOT NULL,
        [NHAMAYNAME] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TMPPHIEUGIAOHANGDBCT] (
        [ID] int IDENTITY(1,1) NOT NULL,
        [IDP] int NOT NULL,
        [GGFCC] nvarchar(50) NULL,
        [STT] smallint NULL,
        [CUA] nvarchar(50) NULL,
        [TRUYEN] nvarchar(50) NULL,
        [MAHANG] nvarchar(50) NULL,
        [TENHANG] nvarchar(50) NULL,
        [LOT] nvarchar(200) NULL,
        [DV] nvarchar(50) NULL,
        [SOLUONG] smallint NULL,
        [NGAYGIAO] smalldatetime NULL,
        [GIOGIAO] nvarchar(50) NULL,
        [STATUS] nvarchar(50) NULL,
        [TTPHIEU] nvarchar(50) NULL,
        [NHAMAY] varchar(50) NULL,
        [ADDNM] int NULL,
        [HOP] int NULL,
        [STATUSDOC] nvarchar(50) NULL,
        [NGAYNHAN] smalldatetime NULL,
        [TTNHAN] bit NULL,
        [Note] nvarchar(250) NULL,
        [PO_NO] nvarchar(50) NULL,
        [PO_ITEM] nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.YMVN_DOCQRCODE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[YMVN_DOCQRCODE] (
        [LOTFCC] nchar(120) NULL,
        [MAHANGFCC] nchar(60) NULL,
        [SLTEMFCC] smallint NULL,
        [LOTHVN] nchar(30) NULL,
        [MAHANGHVN] nchar(60) NULL,
        [SLTEMHVN] smallint NULL,
        [STATUS] smallint NULL,
        [MAFCC] nchar(50) NULL,
        [STT] smallint NULL,
        [KETQUA] nchar(30) NULL,
        [CUA] nchar(10) NULL,
        [TRUYEN] nchar(10) NULL,
        [GIO] nchar(10) NULL,
        [STTBAN] int NULL,
        [SUALOTHVN] nchar(30) NULL,
        [Gear] nchar(1) NULL
    );
END;
GO

/* PK/index/FK/CHECK constraints are emitted from verified SQL2 metadata in 03_Generated_From_DB57_Constraints.sql. */
