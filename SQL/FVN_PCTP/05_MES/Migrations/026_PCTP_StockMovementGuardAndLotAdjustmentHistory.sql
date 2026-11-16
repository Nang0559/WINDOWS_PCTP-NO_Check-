/*
 Migration 026: physical FVN_ tables for the two PCTP support tables that migrations 014-025 did not create:
   dbo.FVN_StockMovementGuard          (was dbo.PCTPStockMovementGuard)          - QR/TEM/reference idempotency claim
   dbo.FVN_StockLotAdjustmentHistory   (was dbo.PCTPStockLotAdjustmentHistory)  - audit of manual LOT reconciliation
 Prerequisite: 025. SQL Server 2016. Quantities are NOT stock authority; they record what the ledger was before/after.
 The adjustment history additionally carries SiteCode and the MES LOT identity.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52600, 'Migration 026 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52601, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '025')
    THROW 52602, 'Migration 025 must be applied before migration 026.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '026')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.FVN_StockMovementGuard', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_StockMovementGuard
        (
            GuardId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_StockMovementGuard PRIMARY KEY,
            ReferenceType nvarchar(80) NULL,
            ReferenceId nvarchar(200) NULL,
            QrHash varbinary(32) NULL,
            TemHash varbinary(32) NULL,
            CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_FVN_StockMovementGuard_CreatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT CK_FVN_StockMovementGuard_ReferencePair CHECK
            (
                (ReferenceType IS NULL AND ReferenceId IS NULL)
                OR (ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL)
            )
        );
        CREATE UNIQUE INDEX UX_FVN_StockMovementGuard_Reference
            ON dbo.FVN_StockMovementGuard (ReferenceType, ReferenceId)
            WHERE ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL;
        CREATE UNIQUE INDEX UX_FVN_StockMovementGuard_QrHash
            ON dbo.FVN_StockMovementGuard (QrHash) WHERE QrHash IS NOT NULL;
        CREATE UNIQUE INDEX UX_FVN_StockMovementGuard_TemHash
            ON dbo.FVN_StockMovementGuard (TemHash) WHERE TemHash IS NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.FVN_StockLotAdjustmentHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_StockLotAdjustmentHistory
        (
            RequestId uniqueidentifier NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            ItemCode nvarchar(100) NOT NULL,
            LotNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NULL,
            StockBefore int NOT NULL,   -- MES on-hand before (all locations, all states)
            StockAfter int NOT NULL,    -- MES on-hand after
            SlotBefore int NOT NULL,    -- FVN_SlotLot projection before
            SlotAfter int NOT NULL,     -- FVN_SlotLot projection after
            StockDelta int NOT NULL,
            SlotDelta int NOT NULL,
            DestinationSlotId int NULL,
            Reason nvarchar(1000) NOT NULL,
            Actor nvarchar(256) NOT NULL,
            CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_FVN_StockLotAdjustmentHistory_CreatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_FVN_StockLotAdjustmentHistory PRIMARY KEY CLUSTERED (RequestId),
            CONSTRAINT FK_FVN_StockLotAdjHistory_Site FOREIGN KEY (SiteCode) REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_StockLotAdjHistory_MESLot FOREIGN KEY (SiteCode, ItemCode, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId)
        );
        CREATE INDEX IX_FVN_StockLotAdjHistory_Lot
            ON dbo.FVN_StockLotAdjustmentHistory (SiteCode, ItemCode, LotNo, CreatedAt DESC);
    END;

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('026', N'PCTP stock movement guard and LOT adjustment history');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '026';
GO
