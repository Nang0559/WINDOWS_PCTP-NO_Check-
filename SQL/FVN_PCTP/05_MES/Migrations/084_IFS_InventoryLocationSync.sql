/*
 Migration 084: IFS INVENTORY_LOCATION synchronization (IFSAPP.INVENTORY_LOCATION -> MES.FVN_IfsInventoryLocation).

 Why: the "Map Slot kho <-> MES Location (IFS)" screen (migration 083) and Component receiving only accept a Location that
 exists and is active in MES.FVN_IfsInventoryLocation, but no sync entity ever filled that table (the sync form offers
 PART_CATALOG ... WAREHOUSE, SUPPLIER, PRODUCTION_LINE, SITE, WORK_CENTER only). WAREHOUSE only fills MES.FVN_IfsWarehouse,
 which is a reference list, not the physical Kho / Rack / Slot of PCTP (dbo.FVN_Warehouse / FVN_Rack / FVN_Slot).

 Adds:
   - IFS.FVN_SyncBatch.InventoryLocationRows
   - CK_IFS_Stage_Entity accepts 'INVENTORY_LOCATION'
   - IFS.usp_ApplyInventoryLocationBatch : separate apply (IFS.usp_ApplySyncBatch is NOT modified). Must run AFTER
     IFS.usp_ApplySyncBatch because every location needs its IFS warehouse row in MES.FVN_IfsWarehouse (FK_MES_IfsLoc_Wh).
     Location groups referenced by a location are created in MES.FVN_IfsLocationGroup (FK_MES_IfsLoc_Group).
     Locations that disappear from IFS for a Site read in the batch are set IsActive = 0 (never deleted: slots keep their
     history and the Map Slot screen shows them as LOCATION_INACTIVE).
   - MES.FVN_IfsWarehouse / FVN_IfsLocation*.SyncBatchId stays NULL on purpose: those columns reference the legacy
     MES.IfsSyncBatch, not IFS.FVN_SyncBatch (same rule as migration 027).

 IFS column mapping (payload JSON of SELECT * FROM IFSAPP.INVENTORY_LOCATION; first existing key wins):
   LocationNo        <- staged PartNo (= LOCATION_NO)
   WarehouseCode     <- WAREHOUSE_ID, WAREHOUSE
   LocationGroupCode <- LOCATION_GROUP
   LocationType      <- LOCATION_TYPE_DB, LOCATION_TYPE
   Description       <- LOCATION_NAME, DESCRIPTION
 If your IFS version uses other column names the apply stops with error 57841 / 57842 and lists the payload keys.

 SQL Server 2016 compatible. Apply after 083 (requires 027, 037, 068, 069).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57840, 'Migration 084 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '083')
    THROW 57843, 'Migration 083 must be registered before 084.', 1;
IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL OR OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_IfsInventoryLocation', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_IfsWarehouse', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_IfsLocationGroup', N'U') IS NULL
    THROW 57844, 'IFS sync / warehouse / location tables are missing (migrations 013, 027, 069).', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '084')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'InventoryLocationRows') IS NULL
        EXEC(N'ALTER TABLE IFS.FVN_SyncBatch ADD InventoryLocationRows int NOT NULL CONSTRAINT DF_IFS_FVN_SyncBatch_InvLocRows DEFAULT(0);');

    IF EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncStageRow') AND name = N'CK_IFS_Stage_Entity')
        ALTER TABLE IFS.FVN_SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;
    ALTER TABLE IFS.FVN_SyncStageRow WITH CHECK ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (EntityName IN ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE','BOM_COMPONENT',
                        'WAREHOUSE','SUPPLIER','PRODUCTION_LINE','SITE','WORK_CENTER','INVENTORY_LOCATION'));

    IF OBJECT_ID(N'IFS.usp_ApplyInventoryLocationBatch', N'P') IS NOT NULL
        DROP PROCEDURE IFS.usp_ApplyInventoryLocationBatch;
    EXEC(N'
    CREATE PROCEDURE IFS.usp_ApplyInventoryLocationBatch
        @SyncBatchId bigint
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;

            DECLARE @Lock int;
            EXEC @Lock = sys.sp_getapplock @Resource = ''IFS_INVENTORY_LOCATION_SYNC'', @LockMode = ''Exclusive'',
                @LockOwner = ''Transaction'', @LockTimeout = 0;
            IF @Lock < 0 THROW 57845, ''Another Inventory Location synchronization is already applying.'', 1;

            IF NOT EXISTS (SELECT 1 FROM IFS.FVN_SyncBatch WITH (UPDLOCK, HOLDLOCK)
                           WHERE SyncBatchId = @SyncBatchId AND Status IN (''STAGED'', ''APPLIED''))
                THROW 57846, ''Sync batch must be STAGED (or already APPLIED) before Inventory Location apply.'', 1;

            IF EXISTS (SELECT 1 FROM IFS.FVN_SyncStageRow
                       WHERE SyncBatchId = @SyncBatchId AND EntityName = ''INVENTORY_LOCATION''
                       GROUP BY SiteCode, PartNo HAVING COUNT(*) > 1)
                THROW 57847, ''Duplicate (CONTRACT, LOCATION_NO) found in Inventory Location staging.'', 1;

            DECLARE @Src TABLE
            (
                SiteCode nvarchar(30) NOT NULL, LocationNo nvarchar(100) NOT NULL,
                WarehouseCode nvarchar(60) NULL, LocationGroupCode nvarchar(60) NULL,
                LocationType nvarchar(30) NULL, Description nvarchar(250) NULL,
                SourceRowKey nvarchar(250) NULL, SourceHash varbinary(32) NULL,
                PRIMARY KEY (SiteCode, LocationNo)
            );

            INSERT @Src (SiteCode, LocationNo, WarehouseCode, LocationGroupCode, LocationType, Description, SourceRowKey, SourceHash)
            SELECT s.SiteCode, s.PartNo,
                   NULLIF(LTRIM(RTRIM(COALESCE(JSON_VALUE(s.PayloadJson, ''$.WAREHOUSE_ID''), JSON_VALUE(s.PayloadJson, ''$.WAREHOUSE'')))), N''''),
                   NULLIF(LTRIM(RTRIM(JSON_VALUE(s.PayloadJson, ''$.LOCATION_GROUP''))), N''''),
                   LEFT(COALESCE(JSON_VALUE(s.PayloadJson, ''$.LOCATION_TYPE_DB''), JSON_VALUE(s.PayloadJson, ''$.LOCATION_TYPE'')), 30),
                   LEFT(COALESCE(JSON_VALUE(s.PayloadJson, ''$.LOCATION_NAME''), JSON_VALUE(s.PayloadJson, ''$.DESCRIPTION'')), 250),
                   LEFT(s.SourceRowKey, 250), s.SourceHash
            FROM IFS.FVN_SyncStageRow s
            WHERE s.SyncBatchId = @SyncBatchId AND s.EntityName = ''INVENTORY_LOCATION'';

            IF EXISTS (SELECT 1 FROM @Src WHERE WarehouseCode IS NULL)
                THROW 57841, ''INVENTORY_LOCATION payload has no WAREHOUSE_ID / WAREHOUSE value. Check the column names of IFSAPP.INVENTORY_LOCATION in IFS.FVN_SyncStageRow.PayloadJson.'', 1;

            /* Each location needs its IFS warehouse (synchronized by the WAREHOUSE entity, applied before this procedure). */
            IF EXISTS (SELECT 1 FROM @Src x
                       WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_IfsWarehouse w
                                         WHERE w.SiteCode = x.SiteCode AND w.WarehouseCode = x.WarehouseCode))
                THROW 57842, ''Some locations reference a warehouse that is not in MES.FVN_IfsWarehouse. Synchronize WAREHOUSE together with INVENTORY_LOCATION.'', 1;

            /* Location groups referenced by locations (no extra IFS read: code only, description stays NULL). */
            INSERT MES.FVN_IfsLocationGroup (SiteCode, LocationGroupCode, Description, IsActive)
            SELECT DISTINCT x.SiteCode, x.LocationGroupCode, x.LocationGroupCode, 1
            FROM @Src x
            WHERE x.LocationGroupCode IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM MES.FVN_IfsLocationGroup g
                              WHERE g.SiteCode = x.SiteCode AND g.LocationGroupCode = x.LocationGroupCode);

            MERGE MES.FVN_IfsInventoryLocation AS t
            USING @Src AS src
               ON t.SiteCode = src.SiteCode AND t.LocationNo = src.LocationNo
            WHEN MATCHED THEN UPDATE SET
                WarehouseCode = src.WarehouseCode, LocationGroupCode = src.LocationGroupCode,
                LocationType = src.LocationType, Description = src.Description, IsActive = 1,
                SourceRowKey = src.SourceRowKey, SourceHash = src.SourceHash, SyncedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN INSERT
                (SiteCode, LocationNo, WarehouseCode, LocationGroupCode, LocationType, Description, IsActive, SourceRowKey, SourceHash)
                VALUES (src.SiteCode, src.LocationNo, src.WarehouseCode, src.LocationGroupCode, src.LocationType,
                        src.Description, 1, src.SourceRowKey, src.SourceHash);

            /* Locations that vanished from IFS for the Sites read in this batch: deactivated, never deleted. */
            UPDATE l SET IsActive = 0, SyncedAtUtc = SYSUTCDATETIME()
            FROM MES.FVN_IfsInventoryLocation l
            WHERE l.IsActive = 1
              AND EXISTS (SELECT 1 FROM @Src z WHERE z.SiteCode = l.SiteCode)
              AND NOT EXISTS (SELECT 1 FROM @Src z WHERE z.SiteCode = l.SiteCode AND z.LocationNo = l.LocationNo);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('084', N'IFS INVENTORY_LOCATION sync entity into MES.FVN_IfsInventoryLocation');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '084';
SELECT SiteCode, COUNT(*) AS IfsLocations, SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveLocations
FROM MES.FVN_IfsInventoryLocation GROUP BY SiteCode ORDER BY SiteCode;
GO
