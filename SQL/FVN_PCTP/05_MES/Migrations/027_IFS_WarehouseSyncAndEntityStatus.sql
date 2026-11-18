/*
 Migration 027: include IFSAPP.WAREHOUSE in the existing one-way IFS sync.
 Prerequisites: migrations 000..026, including 012 and 013.
 SQL Server 2016 compatible. Oracle remains read-only; extraction is performed by PCTP.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52700, 'Migration 027 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52701, 'MES.FVN_SchemaMigration is missing. Run migrations 000..026 first.', 1;

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '026')
    THROW 52702, 'Migration 026 must be applied before migration 027.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '027')
    RETURN;

IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 52703, 'IFS sync objects are missing. Apply migrations 012 and 013 first.', 1;

IF OBJECT_ID(N'MES.FVN_IfsWarehouse', N'U') IS NULL
    THROW 52704, 'MES.FVN_IfsWarehouse is missing. Apply migrations 001 and 013 first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Per-batch warehouse read count. Existing batches remain valid and default to zero. */
    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'WarehouseRows') IS NULL
    BEGIN
        ALTER TABLE IFS.FVN_SyncBatch
            ADD WarehouseRows int NOT NULL
                CONSTRAINT DF_IFS_Batch_Warehouse DEFAULT (0);
    END;

    /* PartNo is the existing generic source business-key slot.
       For WAREHOUSE rows only, the service stores WAREHOUSE_ID in this column. */
    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncStageRow')
          AND name = N'CK_IFS_Stage_Entity'
    )
        ALTER TABLE IFS.FVN_SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;

    ALTER TABLE IFS.FVN_SyncStageRow WITH CHECK
        ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (
            EntityName IN
            ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE','BOM_COMPONENT','WAREHOUSE')
        );

    /* IFS DESCRIPTION is VARCHAR2(800); OBJVERSION is VARCHAR2(56). */
    ALTER TABLE MES.FVN_IfsWarehouse ALTER COLUMN Description nvarchar(800) NULL;
    ALTER TABLE IFS.FVN_SyncStageRow ALTER COLUMN SourceObjVersion nvarchar(56) NULL;

    /*
      Extend the existing transactional apply procedure. The warehouse projection
      deliberately leaves SyncBatchId NULL because MES.FVN_IfsWarehouse.SyncBatchId
      references MES.FVN_IfsSyncBatch, not IFS.FVN_SyncBatch.
    */
    DECLARE @ApplyDefinition nvarchar(max);
    DECLARE @InsertAt int;
    DECLARE @WarehouseMerge nvarchar(max);

    SET @ApplyDefinition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));

    IF @ApplyDefinition IS NULL
        THROW 52705, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    IF CHARINDEX(N'MERGE MES.FVN_IfsWarehouse AS T', @ApplyDefinition) = 0
    BEGIN
        SET @InsertAt = CHARINDEX
        (
            N'UPDATE IFS.SyncBatch',
            @ApplyDefinition,
            CHARINDEX(N'MERGE MES.FVN_IfsInventoryPart', @ApplyDefinition)
        );

        IF @InsertAt = 0
            THROW 52706, 'Could not locate the final batch update in IFS.usp_ApplySyncBatch.', 1;

        SET @WarehouseMerge = N'
        MERGE MES.FVN_IfsWarehouse AS T
        USING
        (
            SELECT
                s.SiteCode,
                JSON_VALUE(s.PayloadJson, ''$.WAREHOUSE_ID'') AS WarehouseCode,
                JSON_VALUE(s.PayloadJson, ''$.DESCRIPTION'') AS Description,
                s.SourceRowKey,
                s.SourceHash
            FROM IFS.SyncStageRow AS s
            WHERE s.SyncBatchId = @SyncBatchId
              AND s.EntityName = ''WAREHOUSE''
        ) AS S
          ON T.SiteCode = S.SiteCode
         AND T.WarehouseCode = S.WarehouseCode
        WHEN MATCHED THEN UPDATE SET
            Description = S.Description,
            IsActive = 1,
            SourceRowKey = S.SourceRowKey,
            SourceHash = S.SourceHash
        WHEN NOT MATCHED THEN INSERT
            (SiteCode, WarehouseCode, Description, IsActive, SourceRowKey, SourceHash)
        VALUES
            (S.SiteCode, S.WarehouseCode, S.Description, 1, S.SourceRowKey, S.SourceHash)
        OUTPUT ''MES_IFS_WAREHOUSE'', $action INTO @Changes;

        ';

        SET @ApplyDefinition = STUFF(@ApplyDefinition, @InsertAt, 0, @WarehouseMerge);
    END;

    SET @ApplyDefinition = REPLACE
    (
        @ApplyDefinition,
        N'CREATE PROCEDURE',
        N'ALTER PROCEDURE'
    );

    IF CHARINDEX(N'CREATE PROCEDURE', @ApplyDefinition) > 0
        THROW 52707, 'Could not convert IFS.usp_ApplySyncBatch to ALTER PROCEDURE.', 1;

    EXEC sys.sp_executesql @ApplyDefinition;

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('027', N'IFS WAREHOUSE synchronization and per-entity status');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Verification */
SELECT
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.max_length
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'IFS.FVN_SyncBatch')
  AND c.name = N'WarehouseRows';

SELECT
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.max_length
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'MES.FVN_IfsWarehouse')
  AND c.name IN (N'SiteCode', N'WarehouseCode', N'Description', N'IsActive', N'SourceRowKey', N'SourceHash');

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '027';
GO
