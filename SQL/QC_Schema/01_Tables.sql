/*
  PCTP QC ephemeral schema — extracted verbatim from the supplied DB57 schema export.
  Source: SQL_sql.sql
  Source SHA-256: cc21dbc1ebf16f62e3b3fe5d24ff1eff1ce73f88cd34d090408a1bff591c7785
  Scope: dependency-closed tables used by G4 delivery acceptance and G6 stock/slot journeys.
  Business data is intentionally NOT copied. Fixtures are seeded separately.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

CREATE TABLE [dbo].[STOCKTP](
	[LOT] [nvarchar](50) NOT NULL,
	[PART] [nvarchar](50) NULL,
	[NAME] [nvarchar](100) NULL,
	[CASX] [smallint] NULL,
	[NGAYSX] [smalldatetime] NULL,
	[SLSX] [int] NULL,
	[NGAYNHAP] [smalldatetime] NULL,
	[SLNHAP] [int] NULL,
	[NGAYXUAT] [smalldatetime] NULL,
	[SLXUAT] [int] NULL,
	[SLCONLAI] [int] NOT NULL,
	[MODEL] [nvarchar](30) NULL,
	[SLCONLAITMP] [int] NULL,
	[SATUS] [smallint] NULL,
	[NGAYNHAP1] [smalldatetime] NULL,
	[SLNHAP1] [int] NULL,
	[NGAYNHAP2] [smalldatetime] NULL,
	[SLNHAP2] [int] NULL,
	[NGAYNHAP3] [smalldatetime] NULL,
	[SLNHAP3] [int] NULL,
	[NGAYNHAP4] [smalldatetime] NULL,
	[SLNHAP4] [int] NULL,
	[NGAYNHAP5] [smalldatetime] NULL,
	[SLNHAP5] [int] NULL,
	[STATUSNHAP] [nvarchar](50) NULL,
	[SP] [nvarchar](100) NULL,
	[LineCodes] [nvarchar](10) NULL,
	[DeptCode] [nvarchar](10) NULL,
 CONSTRAINT [PK_STOCKTP] PRIMARY KEY CLUSTERED ([LOT] ASC)
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[LUUPHIEUGIAOHANG](
	[STT] [smallint] NULL,
	[CUA] [nvarchar](50) NULL,
	[TRUYEN] [nvarchar](50) NULL,
	[MAHANG] [nvarchar](50) NULL,
	[TENHANG] [nvarchar](60) NULL,
	[LOT] [nvarchar](500) NULL,
	[DV] [nvarchar](50) NULL,
	[SOLUONG] [int] NULL,
	[NGAYGIAO] [smalldatetime] NULL,
	[GIOGIAO] [nvarchar](50) NULL,
	[STATUS] [nvarchar](50) NULL,
	[TTPHIEU] [nvarchar](50) NULL,
	[NHAMAY] [nvarchar](60) NULL,
	[GIOGIAOFCC] [nvarchar](50) NULL,
	[GearYMVN] [nvarchar](50) NULL,
	[HOP] [int] NULL,
	[STATUSDOC] [nchar](50) NULL,
	[Note] [nvarchar](250) NULL,
	[TGLUU] [timestamp] NULL,
	[PO_NO] [nvarchar](200) NULL,
	[PO_ITEM] [nvarchar](200) NULL,
 CONSTRAINT [UQ_LUUPHIEU] UNIQUE NONCLUSTERED ([STT] ASC,[MAHANG] ASC,[NGAYGIAO] ASC,[CUA] ASC,[TRUYEN] ASC,[SOLUONG] ASC,[GIOGIAO] ASC,[NHAMAY] ASC)
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[DOCQRCODE](
	[LOTFCC] [nvarchar](500) NULL,[MAHANGFCC] [nvarchar](60) NULL,[SLTEMFCC] [smallint] NULL,[LOTHVN] [nvarchar](500) NULL,[MAHANGHVN] [nvarchar](60) NULL,[SLTEMHVN] [smallint] NULL,[STATUS] [smallint] NULL,[MAFCC] [nvarchar](50) NULL,[STT] [smallint] NULL,[KETQUA] [nvarchar](30) NULL,[CUA] [nvarchar](10) NULL,[TRUYEN] [nvarchar](50) NULL,[GIO] [nvarchar](50) NULL,[STTBAN] [int] NULL,[SUALOTHVN] [nchar](30) NULL,[TGLUU] [timestamp] NULL,[FindTem] [nvarchar](50) NULL
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[DOCQRCODE_SP](
	[LOTFCC] [nvarchar](500) NULL,[MAHANGFCC] [nvarchar](60) NULL,[SLTEMFCC] [smallint] NULL,[LOTHVN] [nvarchar](500) NULL,[MAHANGHVN] [nvarchar](60) NULL,[SLTEMHVN] [smallint] NULL,[STATUS] [smallint] NULL,[MAFCC] [nvarchar](50) NULL,[STT] [smallint] NULL,[KETQUA] [nvarchar](30) NULL,[CUA] [nvarchar](10) NULL,[TRUYEN] [nvarchar](50) NULL,[GIO] [nvarchar](50) NULL,[STTBAN] [int] NULL,[SUALOTHVN] [nchar](30) NULL,[TGLUU] [timestamp] NULL,[FindTem] [nvarchar](50) NULL
) ON [PRIMARY]
GO

-- DOCQRCODE_100003 is customer-specific; generated from the DB57 SQL2 inventory in 02_Generated_From_DB57.sql.


CREATE TABLE [dbo].[LUUDOCQRCODE](
	[LOTFCC] [nvarchar](500) NULL,[MAHANGFCC] [nvarchar](100) NULL,[SLTEMFCC] [smallint] NULL,[LOTHVN] [nvarchar](500) NULL,[MAHANGHVN] [nvarchar](100) NULL,[SLTEMHVN] [smallint] NULL,[STATUS] [smallint] NULL,[MAFCC] [nvarchar](50) NULL,[STT] [smallint] NULL,[KETQUA] [nvarchar](50) NULL,[NGAYXUAT] [smalldatetime] NULL,[GIOXUAT] [nvarchar](200) NULL,[NHAMAY] [nvarchar](200) NULL,[GIOGIAO] [nvarchar](50) NULL,[TGLUU] [timestamp] NULL,[TGLuuDuLieu] [datetime] NULL
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[TMPPHIEUGIAOHANG](
	[STT] [smallint] NULL,[CUA] [nvarchar](50) NULL,[TRUYEN] [nvarchar](50) NULL,[MAHANG] [nvarchar](50) NULL,[TENHANG] [nvarchar](50) NULL,[LOT] [nvarchar](500) NULL,[DV] [nvarchar](50) NULL,[SOLUONG] [smallint] NULL,[NGAYGIAO] [smalldatetime] NULL,[GIOGIAO] [nvarchar](50) NULL,[STATUS] [nvarchar](50) NULL,[TTPHIEU] [nvarchar](50) NULL,[NHAMAY] [varchar](50) NULL,[ADDNM] [int] NULL,[HOP] [int] NULL,[STATUSDOC] [nchar](50) NULL,[Note] [nvarchar](250) NULL,[PO_NO] [nvarchar](200) NULL,[PO_ITEM] [nvarchar](200) NULL
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[TMPPHIEUGIAOHANG_SP](
	[STT] [smallint] NULL,[CUA] [nvarchar](50) NULL,[TRUYEN] [nvarchar](50) NULL,[MAHANG] [nvarchar](50) NULL,[TENHANG] [nvarchar](50) NULL,[LOT] [nvarchar](500) NULL,[DV] [nvarchar](50) NULL,[SOLUONG] [smallint] NULL,[NGAYGIAO] [smalldatetime] NULL,[GIOGIAO] [nvarchar](50) NULL,[STATUS] [nvarchar](50) NULL,[TTPHIEU] [nvarchar](50) NULL,[NHAMAY] [varchar](50) NULL,[ADDNM] [int] NULL,[HOP] [int] NULL,[STATUSDOC] [nchar](50) NULL,[Note] [nvarchar](250) NULL,[PO_NO] [nvarchar](200) NULL,[PO_ITEM] [nvarchar](200) NULL
) ON [PRIMARY]
GO

-- TMPPHIEUGIAOHANG_100003 is customer-specific; generated from the DB57 SQL2 inventory in 02_Generated_From_DB57.sql.


CREATE TABLE [dbo].[Warehouse](
	[WarehouseId] [int] IDENTITY(1,1) NOT NULL,[Name] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED ([WarehouseId] ASC)) ON [PRIMARY]
GO

CREATE TABLE [dbo].[Rack](
	[RackId] [int] IDENTITY(1,1) NOT NULL,[WarehouseId] [int] NULL,[RackName] [nvarchar](100) NOT NULL,[RackRowCount] [int] NULL,[ColumnCount] [int] NULL,
PRIMARY KEY CLUSTERED ([RackId] ASC)) ON [PRIMARY]
GO

CREATE TABLE [dbo].[Slot](
	[SlotId] [int] IDENTITY(1,1) NOT NULL,[RackId] [int] NULL,[SlotNumber] [int] NOT NULL,[IsOccupied] [bit] NOT NULL,[ItemCode] [nvarchar](max) NULL,[LotNo] [nvarchar](max) NULL,[Quantity] [int] NULL,[ImportDate] [datetime] NULL,[TemCode] [nvarchar](max) NULL,[Capacity] [int] NULL,[RowIndex] [int] NULL,[ColumnIndex] [int] NULL,[QrData] [nvarchar](max) NULL,
 CONSTRAINT [PK__Slot__0A124AAF6CE91084] PRIMARY KEY CLUSTERED ([SlotId] ASC)) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[SlotLot](
	[SlotLotId] [int] IDENTITY(1,1) NOT NULL,[SlotId] [int] NOT NULL,[ItemCode] [nvarchar](100) NOT NULL,[LotNo] [nvarchar](200) NOT NULL,[Quantity] [int] NOT NULL,[TemCode] [nvarchar](200) NULL,[QrData] [nvarchar](max) NULL,[ImportDate] [datetime] NULL,[CreatedDate] [datetime] NOT NULL,[NgaySX] [nvarchar](20) NULL,[SoPhieuTong] [nvarchar](50) NULL,[MaPhieu] [nvarchar](100) NULL,[ParentSoPhieu] [nvarchar](50) NULL,[PhieuStatus] [tinyint] NOT NULL,
PRIMARY KEY CLUSTERED ([SlotLotId] ASC)) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[DONHANG](
	[cua] [nchar](10) NULL,[mahang] [nchar](50) NULL,[tenhang] [nchar](60) NULL,[gioxuat] [int] NULL,[truyen] [nchar](10) NULL,[Oty] [int] NULL,[slxuat] [int] NULL,[NgayXuat] [smalldatetime] NULL
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[GIAO_HANG](
	[LOT] [nvarchar](50) NULL,[NGAYGIAO] [smalldatetime] NULL,[GIOGIAO] [nvarchar](50) NULL,[SLGIAO] [smallint] NULL,[STATUS] [smallint] NULL,[GIOBAN] [datetime] NULL
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Rack] WITH CHECK ADD FOREIGN KEY([WarehouseId]) REFERENCES [dbo].[Warehouse] ([WarehouseId]);
GO
ALTER TABLE [dbo].[Slot] WITH CHECK ADD CONSTRAINT [FK__Slot__RackId__286302EC] FOREIGN KEY([RackId]) REFERENCES [dbo].[Rack] ([RackId]);
GO
ALTER TABLE [dbo].[Slot] CHECK CONSTRAINT [FK__Slot__RackId__286302EC];
GO
ALTER TABLE [dbo].[SlotLot] WITH CHECK ADD CONSTRAINT [FK_SlotLot_Slot] FOREIGN KEY([SlotId]) REFERENCES [dbo].[Slot] ([SlotId]);
GO
ALTER TABLE [dbo].[SlotLot] CHECK CONSTRAINT [FK_SlotLot_Slot];
GO
