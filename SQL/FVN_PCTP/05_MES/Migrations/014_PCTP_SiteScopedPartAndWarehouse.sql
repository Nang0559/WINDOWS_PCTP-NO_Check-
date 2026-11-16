/*
 Migration 014: PCTP site-scoped part identity, PCTP-only extensions and warehouse hierarchy.
 Prerequisite: migrations 000..013 completed in FVN_PCTP.
 SQL Server 2016.
 All part references use (SiteCode, PartNo) -> IFS.FVN_InventoryPart.
 No Bravo B20* master is recreated. No inventory quantity ledger is created here.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51400, 'Migration 014 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 51401, 'Run MES migrations 000..013 before migration 014.', 1;

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '013')
    THROW 51402, 'Migration 013 must be applied before migration 014.', 1;

IF OBJECT_ID(N'IFS.FVN_InventoryPart', N'U') IS NULL
    THROW 51403, 'IFS.FVN_InventoryPart is required; apply IFS sync migrations first.', 1;

IF COL_LENGTH(N'IFS.FVN_InventoryPart', N'SiteCode') IS NULL
   OR COL_LENGTH(N'IFS.FVN_InventoryPart', N'PartNo') IS NULL
    THROW 51404, 'IFS.FVN_InventoryPart does not expose the approved (SiteCode, PartNo) key.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '014')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Explicit PCTP site allow-list. Do not auto-enable every IFS site. */
    IF OBJECT_ID(N'dbo.FVN_PCTPSite', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PCTPSite
        (
            SiteCode nvarchar(30) NOT NULL,
            SiteName nvarchar(120) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_PCTPSite_Active DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_PCTPSite_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PCTPSite PRIMARY KEY (SiteCode)
        );
    END;

    /* Preserve source numeric identity keys for WarehouseId, RackId and SlotId. */
    IF OBJECT_ID(N'dbo.FVN_Warehouse', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Warehouse
        (
            SiteCode nvarchar(30) NOT NULL,
            WarehouseId int IDENTITY(1,1) NOT NULL,
            Name nvarchar(200) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_Warehouse_IsActive DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_Warehouse_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            ModifiedAtUtc datetime2(3) NULL,
            ModifiedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_Warehouse PRIMARY KEY (SiteCode, WarehouseId),
            CONSTRAINT FK_FVN_Warehouse_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_Rack', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Rack
        (
            SiteCode nvarchar(30) NOT NULL,
            RackId int IDENTITY(1,1) NOT NULL,
            WarehouseId int NOT NULL,
            RackName nvarchar(200) NOT NULL,
            RackRowCount int NULL,
            ColumnCount int NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_Rack_IsActive DEFAULT (1),
            CONSTRAINT PK_FVN_Rack PRIMARY KEY (SiteCode, RackId),
            CONSTRAINT FK_FVN_Rack_Warehouse FOREIGN KEY (SiteCode, WarehouseId)
                REFERENCES dbo.FVN_Warehouse (SiteCode, WarehouseId),
            CONSTRAINT CK_FVN_Rack_Dimensions CHECK
                ((RackRowCount IS NULL OR RackRowCount >= 0) AND (ColumnCount IS NULL OR ColumnCount >= 0))
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_Slot', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Slot
        (
            SiteCode nvarchar(30) NOT NULL,
            SlotId int IDENTITY(1,1) NOT NULL,
            RackId int NOT NULL,
            SlotNumber int NOT NULL,
            IsOccupied bit NOT NULL CONSTRAINT DF_FVN_Slot_IsOccupied DEFAULT (0),
            Capacity int NULL,
            RowIndex int NULL,
            ColumnIndex int NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_Slot_IsActive DEFAULT (1),
            CONSTRAINT PK_FVN_Slot PRIMARY KEY (SiteCode, SlotId),
            CONSTRAINT UQ_FVN_Slot_RackNumber UNIQUE (SiteCode, RackId, SlotNumber),
            CONSTRAINT FK_FVN_Slot_Rack FOREIGN KEY (SiteCode, RackId)
                REFERENCES dbo.FVN_Rack (SiteCode, RackId),
            CONSTRAINT CK_FVN_Slot_Number CHECK (SlotNumber >= 0),
            CONSTRAINT CK_FVN_Slot_Capacity CHECK (Capacity IS NULL OR Capacity >= 0)
        );
    END;

    /* Map each PCTP physical slot to a real IFS/MES location. SlotId is NOT LocationNo. */
    IF OBJECT_ID(N'dbo.FVN_SlotMesLocation', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_SlotMesLocation
        (
            SiteCode nvarchar(30) NOT NULL,
            SlotId int NOT NULL,
            LocationNo nvarchar(100) NOT NULL,
            IsPrimary bit NOT NULL CONSTRAINT DF_FVN_SlotMesLocation_Primary DEFAULT (1),
            IsActive bit NOT NULL CONSTRAINT DF_FVN_SlotMesLocation_Active DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_SlotMesLocation_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_SlotMesLocation PRIMARY KEY (SiteCode, SlotId, LocationNo),
            CONSTRAINT FK_FVN_SlotMesLocation_Slot FOREIGN KEY (SiteCode, SlotId)
                REFERENCES dbo.FVN_Slot (SiteCode, SlotId),
            CONSTRAINT FK_FVN_SlotMesLocation_MESLocation FOREIGN KEY (SiteCode, LocationNo)
                REFERENCES MES.FVN_IfsInventoryLocation (SiteCode, LocationNo)
        );
        CREATE UNIQUE INDEX UX_FVN_SlotMesLocation_Primary
            ON dbo.FVN_SlotMesLocation (SiteCode, SlotId)
            WHERE IsPrimary = 1 AND IsActive = 1;
    END;

    /* Old B20Item.Id was embedded in some five-character QR payloads.
       This is a technical cross-reference, not a duplicate item master. */
    IF OBJECT_ID(N'dbo.FVN_PartIdentity', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PartIdentity
        (
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            LegacyB20ItemId int NULL,
            LegacyQrItemCode char(5) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_PartIdentity_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PartIdentity PRIMARY KEY (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PartIdentity_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
         );
    END;

    CREATE UNIQUE INDEX UX_FVN_Warehouse_LegacyId ON dbo.FVN_Warehouse(LegacyWarehouseId) WHERE LegacyWarehouseId IS NOT NULL;
    CREATE UNIQUE INDEX UX_FVN_Rack_LegacyId ON dbo.FVN_Rack(LegacyRackId) WHERE LegacyRackId IS NOT NULL;
    CREATE UNIQUE INDEX UX_FVN_Slot_LegacyId ON dbo.FVN_Slot(LegacySlotId) WHERE LegacySlotId IS NOT NULL;
    CREATE UNIQUE INDEX UX_FVN_PartIdentity_LegacyId ON dbo.FVN_PartIdentity(LegacyB20ItemId) WHERE LegacyB20ItemId IS NOT NULL;
    CREATE UNIQUE INDEX UX_FVN_PartIdentity_QrCode ON dbo.FVN_PartIdentity(LegacyQrItemCode) WHERE LegacyQrItemCode IS NOT NULL;

    /* Only PCTP-owned attributes absent from the IFS master belong here. */
    IF OBJECT_ID(N'dbo.FVN_PartOperationalProfile', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PartOperationalProfile
        (
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            ModelCode nvarchar(48) NULL,
            DefaultPackageQty decimal(15,4) NULL,
            MaxPackageQty decimal(15,4) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_PartProfile_Active DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_PartProfile_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            ModifiedAtUtc datetime2(3) NULL,
            ModifiedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PartOperationalProfile PRIMARY KEY (SiteCode, PartNo),
            CONSTRAINT FK_FVN_PartProfile_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT CK_FVN_PartProfile_PackageQty CHECK
                ((DefaultPackageQty IS NULL OR DefaultPackageQty > 0) AND (MaxPackageQty IS NULL OR MaxPackageQty > 0))
        );
    END;

    /* Customer-specific package/LOT behavior formerly stored in B20ItemQuyCach.
       The unique key guarantees one active rule per site/part/customer. */
    IF OBJECT_ID(N'dbo.FVN_PartCustomerRule', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_PartCustomerRule
        (
            PartCustomerRuleId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            CustomerCode nvarchar(60) NOT NULL,
            PackageQty decimal(15,4) NULL,
            MaxPackageQty decimal(15,4) NULL,
            LotNormalizationRule varchar(30) NOT NULL CONSTRAINT DF_FVN_PartCustomerRule_LotRule DEFAULT ('NONE'),
            LotTrimRightLength tinyint NULL,
            BuiltinOrder int NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_PartCustomerRule_Active DEFAULT (1),
            EffectiveFrom datetime2(0) NULL,
            EffectiveTo datetime2(0) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_PartCustomerRule_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_PartCustomerRule PRIMARY KEY (PartCustomerRuleId),
            CONSTRAINT FK_FVN_PartCustomerRule_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT CK_FVN_PartCustomerRule_LotRule CHECK
                (LotNormalizationRule IN ('NONE','TRIM_RIGHT')),
            CONSTRAINT CK_FVN_PartCustomerRule_Trim CHECK
                ((LotNormalizationRule = 'NONE' AND LotTrimRightLength IS NULL)
                 OR (LotNormalizationRule = 'TRIM_RIGHT' AND LotTrimRightLength BETWEEN 1 AND 50)),
            CONSTRAINT CK_FVN_PartCustomerRule_Dates CHECK
                (EffectiveTo IS NULL OR EffectiveFrom IS NULL OR EffectiveTo > EffectiveFrom),
            CONSTRAINT CK_FVN_PartCustomerRule_Qty CHECK
                ((PackageQty IS NULL OR PackageQty > 0) AND (MaxPackageQty IS NULL OR MaxPackageQty > 0))
        );
        CREATE UNIQUE INDEX UX_FVN_PartCustomerRule_Active
            ON dbo.FVN_PartCustomerRule (SiteCode, PartNo, CustomerCode)
            WHERE IsActive = 1 AND EffectiveTo IS NULL;
    END;

    /* Gear is a PCTP LOT/QR concept, not an IFS part. */
    IF OBJECT_ID(N'dbo.FVN_Gear', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_Gear
        (
            GearCode int NOT NULL,
            GearName nvarchar(20) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_Gear_Active DEFAULT (1),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_Gear_Created DEFAULT (SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NULL,
            ModifiedAtUtc datetime2(3) NULL,
            ModifiedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_Gear PRIMARY KEY (GearCode),
            CONSTRAINT UQ_FVN_Gear_Name UNIQUE (GearName)
        );
    END;

    /* Read-only part lookup: IFS owns description, UOM and status. */
    EXEC(N'
    CREATE VIEW dbo.vFVN_PCTP_Part
    AS
    SELECT
        ip.SiteCode,
        ip.PartNo,
        ip.Description,
        ip.UnitMeas,
        ip.PartStatus,
        ip.PartProductCode,
        ip.PartProductFamily,
        ip.PlannerBuyer,
        pc.PartMainGroup,
        pc.LotTrackingCodeDb,
        pc.SerialTrackingCodeDb,
        pi.LegacyB20ItemId,
        pi.LegacyQrItemCode,
        pp.ModelCode,
        pp.DefaultPackageQty,
        pp.MaxPackageQty,
        ip.LastSeenBatchId,
        ip.SyncedAtUtc
    FROM IFS.FVN_InventoryPart AS ip
    LEFT JOIN IFS.FVN_PartCatalog AS pc
        ON pc.PartNo = ip.PartNo
    LEFT JOIN dbo.FVN_PartIdentity AS pi
        ON pi.SiteCode = ip.SiteCode AND pi.PartNo = ip.PartNo
    LEFT JOIN dbo.FVN_PartOperationalProfile AS pp
        ON pp.SiteCode = ip.SiteCode AND pp.PartNo = ip.PartNo;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('014', N'PCTP site-scoped part identity and warehouse hierarchy');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Verification: no PCTP part-extension row may be keyed by PartNo alone. */
SELECT
    OBJECT_SCHEMA_NAME(parent_object_id) AS SchemaName,
    OBJECT_NAME(parent_object_id) AS TableName,
    name AS ForeignKeyName
FROM sys.foreign_keys
WHERE name IN
(
    N'FK_FVN_PartIdentity_IFSPart',
    N'FK_FVN_PartProfile_IFSPart',
    N'FK_FVN_PartCustomerRule_IFSPart'
);
GO
