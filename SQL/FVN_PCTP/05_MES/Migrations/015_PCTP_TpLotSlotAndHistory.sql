/*
 Migration 015: preserve PCTP TP LOT, SlotLot and historical stock evidence.
 Prerequisite: 014.
 SQL Server 2016.
 Quantity authority remains MES.FVN_InventoryBalance / FVN_InventoryTransaction.
 FVN_STOCKTP and FVN_SlotLot quantity columns are legacy operational projections
 retained for reconciliation/transition; they must not be used as an independent ledger.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51500, 'Migration 015 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 51501, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '014')
    THROW 51502, 'Migration 014 must be applied before migration 015.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '015')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Preserve remaining physical Slot attributes used by the current UI.
       Item/LOT/Quantity here are display snapshots only; balances come from MES. */
    IF COL_LENGTH(N'dbo.FVN_Slot', N'ItemCode') IS NULL
        ALTER TABLE dbo.FVN_Slot ADD ItemCode nvarchar(100) NULL;
    IF COL_LENGTH(N'dbo.FVN_Slot', N'LotNo') IS NULL
        ALTER TABLE dbo.FVN_Slot ADD LotNo nvarchar(400) NULL;
    IF COL_LENGTH(N'dbo.FVN_Slot', N'Quantity') IS NULL
        ALTER TABLE dbo.FVN_Slot ADD Quantity int NULL;
    IF COL_LENGTH(N'dbo.FVN_Slot', N'ImportDate') IS NULL
        ALTER TABLE dbo.FVN_Slot ADD ImportDate datetime NULL;
    IF COL_LENGTH(N'dbo.FVN_Slot', N'TemCode') IS NULL
        ALTER TABLE dbo.FVN_Slot ADD TemCode nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.FVN_Slot', N'QrData') IS NULL
        ALTER TABLE dbo.FVN_Slot ADD QrData nvarchar(max) NULL;

    IF OBJECT_ID(N'dbo.FVN_STOCKTP', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_STOCKTP
        (
            SiteCode nvarchar(30) NOT NULL,
            LOT nvarchar(100) NOT NULL,
            PART nvarchar(100) NULL,
            CASX smallint NULL,
            NGAYSX smalldatetime NULL,
            SLSX int NULL,
            NGAYNHAP smalldatetime NULL,
            SLNHAP int NULL,
            NGAYXUAT smalldatetime NULL,
            SLXUAT int NULL,
            SLCONLAI int NOT NULL CONSTRAINT DF_FVN_STOCKTP_SLCONLAI DEFAULT (0),
            SLCONLAITMP int NULL,
            SATUS smallint NULL,
            NGAYNHAP1 smalldatetime NULL,
            SLNHAP1 int NULL,
            NGAYNHAP2 smalldatetime NULL,
            SLNHAP2 int NULL,
            NGAYNHAP3 smalldatetime NULL,
            SLNHAP3 int NULL,
            NGAYNHAP4 smalldatetime NULL,
            SLNHAP4 int NULL,
            NGAYNHAP5 smalldatetime NULL,
            SLNHAP5 int NULL,
            STATUSNHAP nvarchar(100) NULL,
            SP nvarchar(200) NULL,
            LineCodes nvarchar(20) NULL,
            DeptCode nvarchar(20) NULL,
            MaterialLotId bigint NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_STOCKTP_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_STOCKTP PRIMARY KEY (SiteCode, LOT),
            CONSTRAINT FK_FVN_STOCKTP_IFSPart FOREIGN KEY (SiteCode, PART)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_STOCKTP_MESLot FOREIGN KEY (SiteCode, PART, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT CK_FVN_STOCKTP_Remaining CHECK (SLCONLAI >= 0)
        );
        CREATE INDEX IX_FVN_STOCKTP_PartLot ON dbo.FVN_STOCKTP (SiteCode, PART, LOT);
        CREATE INDEX IX_FVN_STOCKTP_MESLot ON dbo.FVN_STOCKTP (MaterialLotId) WHERE MaterialLotId IS NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.FVN_SlotLot', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_SlotLot
        (
            SlotLotId int NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            SlotId int NOT NULL,
            ItemCode nvarchar(100) NOT NULL,
            LotNo nvarchar(400) NOT NULL,
            Quantity int NOT NULL,
            TemCode nvarchar(400) NULL,
            QrData nvarchar(max) NULL,
            ImportDate datetime NULL,
            CreatedDate datetime NOT NULL CONSTRAINT DF_FVN_SlotLot_Created DEFAULT (GETDATE()),
            NgaySX nvarchar(40) NULL,
            SoPhieuTong nvarchar(100) NULL,
            MaPhieu nvarchar(200) NULL,
            ParentSoPhieu nvarchar(100) NULL,
            PhieuStatus tinyint NOT NULL CONSTRAINT DF_FVN_SlotLot_PhieuStatus DEFAULT (0),
            MaterialLotId bigint NULL,
            CONSTRAINT PK_FVN_SlotLot PRIMARY KEY (SlotLotId),
            CONSTRAINT UQ_FVN_SlotLot_SiteId UNIQUE (SiteCode, SlotLotId),
            CONSTRAINT FK_FVN_SlotLot_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_SlotLot_IFSPart FOREIGN KEY (SiteCode, ItemCode)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_SlotLot_MESLot FOREIGN KEY (SiteCode, ItemCode, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT CK_FVN_SlotLot_Quantity CHECK (Quantity >= 0)
        );
        CREATE INDEX IX_FVN_SlotLot_Slot ON dbo.FVN_SlotLot (SiteCode, SlotId, ItemCode, LotNo);
        CREATE INDEX IX_FVN_SlotLot_Lot ON dbo.FVN_SlotLot (SiteCode, ItemCode, LotNo);
        CREATE INDEX IX_FVN_SlotLot_MaPhieu ON dbo.FVN_SlotLot (MaPhieu) WHERE MaPhieu IS NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.FVN_StockHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_StockHistory
        (
            Id int NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            ActionType varchar(30) NOT NULL,
            ItemCode nvarchar(100) NULL,
            TemCode nvarchar(200) NULL,
            LotNo nvarchar(400) NULL,
            Quantity int NULL,
            [Date] datetime NULL,
            SlotId int NULL,
            PerformedBy nvarchar(200) NULL,
            QrData nvarchar(max) NULL,
            MaPhieu nvarchar(100) NULL,
            FromSlotId int NULL,
            ToSlotId int NULL,
            MaterialLotId bigint NULL,
            InventoryTransactionId bigint NULL,
            LegacySourceId int NULL,
            CONSTRAINT PK_FVN_StockHistory PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_StockHistory_IFSPart FOREIGN KEY (SiteCode, ItemCode)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_StockHistory_MESLot FOREIGN KEY (SiteCode, ItemCode, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_StockHistory_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT FK_FVN_StockHistory_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_StockHistory_FromSlot FOREIGN KEY (SiteCode, FromSlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_StockHistory_ToSlot FOREIGN KEY (SiteCode, ToSlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId)
        );
        CREATE INDEX IX_FVN_StockHistory_SiteDate ON dbo.FVN_StockHistory (SiteCode, [Date] DESC, Id DESC);
        CREATE INDEX IX_FVN_StockHistory_Lot ON dbo.FVN_StockHistory (SiteCode, ItemCode, LotNo);
        CREATE UNIQUE INDEX UX_FVN_StockHistory_LegacySource
            ON dbo.FVN_StockHistory (LegacySourceId) WHERE LegacySourceId IS NOT NULL;
        CREATE UNIQUE INDEX UX_FVN_StockHistory_InventoryTxn
            ON dbo.FVN_StockHistory (InventoryTransactionId) WHERE InventoryTransactionId IS NOT NULL;
    END;

    /* IFS owns item name/UOM. This view keeps the familiar STOCKTP result shape
       without storing a second description or a second on-hand quantity. */
    EXEC(N'
    CREATE VIEW dbo.vFVN_STOCKTP
    AS
    SELECT
        s.LOT, s.PART, ip.Description AS NAME, s.CASX, s.NGAYSX, s.SLSX,
        s.NGAYNHAP, s.SLNHAP, s.NGAYXUAT, s.SLXUAT, s.SLCONLAI,
        pp.ModelCode AS MODEL, s.SLCONLAITMP, s.SATUS,
        s.NGAYNHAP1, s.SLNHAP1, s.NGAYNHAP2, s.SLNHAP2,
        s.NGAYNHAP3, s.SLNHAP3, s.NGAYNHAP4, s.SLNHAP4,
        s.NGAYNHAP5, s.SLNHAP5, s.STATUSNHAP, s.SP,
        s.LineCodes, s.DeptCode, s.SiteCode, s.MaterialLotId
    FROM dbo.FVN_STOCKTP AS s
    LEFT JOIN IFS.FVN_InventoryPart AS ip
        ON ip.SiteCode = s.SiteCode AND ip.PartNo = s.PART
    LEFT JOIN dbo.FVN_PartOperationalProfile AS pp
        ON pp.SiteCode = s.SiteCode AND pp.PartNo = s.PART;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('015', N'PCTP TP LOT, SlotLot and stock history evidence');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Quantity columns in these PCTP tables are retained for legacy evidence only.
   Current stock must be read from MES.FVN_InventoryBalance. */
SELECT
    N'MES.FVN_InventoryBalance' AS QuantityAuthority,
    N'dbo.FVN_STOCKTP.SLCONLAI; dbo.FVN_SlotLot.Quantity; dbo.FVN_Slot.Quantity' AS LegacyProjectionColumns;
GO
