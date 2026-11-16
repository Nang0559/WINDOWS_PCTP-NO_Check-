/*
 Migration 017: QR/delivery command idempotency, quarantine audit and internal transfer documents.
 Prerequisite: 016.
 SQL Server 2016.
 These are business/audit records. MES inventory ledger remains the only quantity authority.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51700, 'Migration 017 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 51701, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '016')
    THROW 51702, 'Migration 016 must be applied before migration 017.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '017')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.FVN_DeliveryCommandLedger', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_DeliveryCommandLedger
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            BusinessKey nvarchar(200) NOT NULL,
            CustomerCode nvarchar(50) NOT NULL,
            DeliveryReference nvarchar(100) NOT NULL,
            PerformedBy nvarchar(100) NOT NULL,
            OccurredAt datetime2(0) NOT NULL,
            TotalQuantity int NOT NULL,
            PendingSttCsv nvarchar(1000) NOT NULL,
            DeliveryRowId bigint NULL,
            InventoryTransactionId bigint NULL,
            CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_FVN_DeliveryCommandLedger_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_DeliveryCommandLedger PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_DeliveryCommandLedger_Delivery FOREIGN KEY (DeliveryRowId)
                REFERENCES dbo.FVN_LUUPHIEUGIAOHANG (DeliveryRowId),
            CONSTRAINT FK_FVN_DeliveryCommandLedger_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_DeliveryCommandLedger_Qty CHECK (TotalQuantity > 0)
        );
        CREATE UNIQUE INDEX UX_FVN_DeliveryCommandLedger_BusinessKey
            ON dbo.FVN_DeliveryCommandLedger (SiteCode, BusinessKey);
        CREATE UNIQUE INDEX UX_FVN_DeliveryCommandLedger_Txn
            ON dbo.FVN_DeliveryCommandLedger (InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE INDEX IX_FVN_DeliveryCommandLedger_CustomerDate
            ON dbo.FVN_DeliveryCommandLedger (SiteCode, CustomerCode, OccurredAt DESC);
    END;

    IF OBJECT_ID(N'dbo.FVN_DeliveryCommandQr', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_DeliveryCommandQr
        (
            Id int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            LedgerId int NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            QrKey nvarchar(450) NOT NULL,
            DeliveryRowId bigint NULL,
            CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_FVN_DeliveryCommandQr_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_DeliveryCommandQr PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_DeliveryCommandQr_Ledger FOREIGN KEY (LedgerId)
                REFERENCES dbo.FVN_DeliveryCommandLedger (Id),
            CONSTRAINT FK_FVN_DeliveryCommandQr_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_DeliveryCommandQr_Delivery FOREIGN KEY (DeliveryRowId)
                REFERENCES dbo.FVN_LUUPHIEUGIAOHANG (DeliveryRowId)
        );
        CREATE UNIQUE INDEX UX_FVN_DeliveryCommandQr_QrKey
            ON dbo.FVN_DeliveryCommandQr (SiteCode, QrKey);
        CREATE INDEX IX_FVN_DeliveryCommandQr_Ledger
            ON dbo.FVN_DeliveryCommandQr (LedgerId, Id);
    END;

    /* Separate special DB-delivery QR session, retained as its own document stream. */
    IF OBJECT_ID(N'dbo.FVN_DocQrCodeDb', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_DocQrCodeDb
        (
            QrRowId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            LOTFCC nvarchar(500) NULL,
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
            PartNoFcc nvarchar(100) NULL,
            PartNoHvn nvarchar(100) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocQrCodeDb_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_DocQrCodeDb PRIMARY KEY (QrRowId),
            CONSTRAINT FK_FVN_DocQrCodeDb_FccPart FOREIGN KEY (SiteCode, PartNoFcc)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_DocQrCodeDb_HvnPart FOREIGN KEY (SiteCode, PartNoHvn)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo)
        );
        CREATE INDEX IX_FVN_DocQrCodeDb_Lot ON dbo.FVN_DocQrCodeDb (SiteCode, LOTFCC, LOTHVN);
        CREATE INDEX IX_FVN_DocQrCodeDb_MaFcc ON dbo.FVN_DocQrCodeDb (SiteCode, MAFCC);
    END;

    IF OBJECT_ID(N'dbo.FVN_PCTPQuarantineReleaseAudit', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PCTPQuarantineReleaseAudit
        (
            Id bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            SlotLotId int NOT NULL,
            SlotId int NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NULL,
            LotNo nvarchar(500) NOT NULL,
            Quantity int NOT NULL,
            InventoryTransactionId bigint NULL,
            PerformedBy nvarchar(200) NOT NULL,
            ReferenceId nvarchar(200) NOT NULL,
            Reason nvarchar(1000) NOT NULL,
            ReleasedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_PCTPQuarantineReleaseAudit_ReleasedAtUtc DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_PCTPQuarantineReleaseAudit PRIMARY KEY (Id),
            CONSTRAINT FK_FVN_QuarantineRelease_SlotLot FOREIGN KEY (SlotLotId)
                REFERENCES dbo.FVN_SlotLot (SlotLotId),
            CONSTRAINT FK_FVN_QuarantineRelease_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_QuarantineRelease_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_QuarantineRelease_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_QuarantineRelease_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_QuarantineRelease_Qty CHECK (Quantity > 0)
        );
        CREATE INDEX IX_FVN_QuarantineRelease_SlotLot
            ON dbo.FVN_PCTPQuarantineReleaseAudit (SiteCode, SlotLotId, ReleasedAtUtc DESC);
        CREATE UNIQUE INDEX UX_FVN_QuarantineRelease_Txn
            ON dbo.FVN_PCTPQuarantineReleaseAudit (InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.FVN_InternalLocation', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_InternalLocation
        (
            LocationId int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            LocationCode nvarchar(60) NOT NULL,
            LocationName nvarchar(150) NOT NULL,
            MESLocationNo nvarchar(100) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_InternalLocation_Active DEFAULT (1),
            CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_FVN_InternalLocation_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_InternalLocation PRIMARY KEY (LocationId),
            CONSTRAINT UQ_FVN_InternalLocation_Code UNIQUE (SiteCode, LocationCode),
            CONSTRAINT FK_FVN_InternalLocation_MESLocation FOREIGN KEY (SiteCode, MESLocationNo)
                REFERENCES MES.FVN_IfsInventoryLocation (SiteCode, LocationNo)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_InternalTransfer', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_InternalTransfer
        (
            TransferId int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            RequestKey uniqueidentifier NOT NULL,
            TransferNo nvarchar(40) NULL,
            LocationId int NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NOT NULL,
            LotNo nvarchar(200) NOT NULL,
            Quantity int NOT NULL,
            SourceSlotId int NOT NULL,
            InventoryTransactionId bigint NULL,
            Reason nvarchar(500) NOT NULL,
            PerformedBy nvarchar(100) NOT NULL,
            CreatedAt datetime2(0) NOT NULL,
            CONSTRAINT PK_FVN_InternalTransfer PRIMARY KEY (TransferId),
            CONSTRAINT FK_FVN_InternalTransfer_Location FOREIGN KEY (LocationId)
                REFERENCES dbo.FVN_InternalLocation (LocationId),
            CONSTRAINT FK_FVN_InternalTransfer_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_InternalTransfer_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT FK_FVN_InternalTransfer_SourceSlot FOREIGN KEY (SiteCode, SourceSlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_InternalTransfer_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_InternalTransfer_Qty CHECK (Quantity > 0)
        );
        CREATE UNIQUE INDEX UX_FVN_InternalTransfer_RequestKey
            ON dbo.FVN_InternalTransfer (RequestKey);
        CREATE UNIQUE INDEX UX_FVN_InternalTransfer_TransferNo
            ON dbo.FVN_InternalTransfer (SiteCode, TransferNo)
            WHERE TransferNo IS NOT NULL;
        CREATE INDEX IX_FVN_InternalTransfer_LotDate
            ON dbo.FVN_InternalTransfer (SiteCode, PartNo, LotNo, CreatedAt DESC);
    END;

    IF OBJECT_ID(N'dbo.FVN_InternalReturn', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_InternalReturn
        (
            ReturnId int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            RequestKey uniqueidentifier NOT NULL,
            ReturnNo nvarchar(40) NULL,
            TransferId int NOT NULL,
            TargetSlotId int NOT NULL,
            Quantity int NOT NULL,
            InventoryTransactionId bigint NULL,
            Reason nvarchar(500) NOT NULL,
            PerformedBy nvarchar(100) NOT NULL,
            CreatedAt datetime2(0) NOT NULL,
            CONSTRAINT PK_FVN_InternalReturn PRIMARY KEY (ReturnId),
            CONSTRAINT FK_FVN_InternalReturn_Transfer FOREIGN KEY (TransferId)
                REFERENCES dbo.FVN_InternalTransfer (TransferId),
            CONSTRAINT FK_FVN_InternalReturn_TargetSlot FOREIGN KEY (SiteCode, TargetSlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_InternalReturn_Txn FOREIGN KEY (InventoryTransactionId)
                REFERENCES MES.FVN_InventoryTransaction (InventoryTransactionId),
            CONSTRAINT CK_FVN_InternalReturn_Qty CHECK (Quantity > 0)
        );
        CREATE UNIQUE INDEX UX_FVN_InternalReturn_RequestKey
            ON dbo.FVN_InternalReturn (RequestKey);
        CREATE UNIQUE INDEX UX_FVN_InternalReturn_ReturnNo
            ON dbo.FVN_InternalReturn (SiteCode, ReturnNo)
            WHERE ReturnNo IS NOT NULL;
        CREATE INDEX IX_FVN_InternalReturn_TransferDate
            ON dbo.FVN_InternalReturn (TransferId, CreatedAt);
    END;

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('017', N'PCTP QR delivery idempotency quarantine and internal transfer documents');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Internal on-hand is intentionally projected from the MES ledger, never stored
   in a second PCTP_InternalStock table. */
SELECT N'MES.FVN_InventoryBalance' AS InternalStockQuantityAuthority,
       N'dbo.FVN_InternalTransfer / dbo.FVN_InternalReturn' AS BusinessDocuments;
GO
