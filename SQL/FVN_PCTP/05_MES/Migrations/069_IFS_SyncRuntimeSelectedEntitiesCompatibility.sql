/*
 Migration 069: align IFS sync runtime with the FVN-prefixed staging schema.

 The database already uses IFS.FVN_SyncBatch / IFS.FVN_SyncStageRow.
 The C# runtime and the base apply procedure historically referenced the
 unprefixed names. This migration adds missing runtime columns to the FVN
 batch, expands its staging entity constraint, and redirects the apply
 procedure to the FVN tables. It does not recreate or require legacy tables.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 56900, 'Migration 069 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
    THROW 56901, 'IFS.FVN_SyncBatch / IFS.FVN_SyncStageRow are missing. Apply the FVN IFS sync migrations (027, 028, 037, 067, 068) first.', 1;

IF OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 56902, 'IFS.usp_ApplySyncBatch is missing.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 56903, 'MES.FVN_SchemaMigration is missing.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '069')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'SelectedEntities') IS NULL
        ALTER TABLE IFS.FVN_SyncBatch ADD SelectedEntities nvarchar(500) NULL;

    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'SiteRows') IS NULL
        ALTER TABLE IFS.FVN_SyncBatch ADD SiteRows int NOT NULL
            CONSTRAINT DF_IFS_FVN_SyncBatch_SiteRows_069 DEFAULT(0);

    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'WorkCenterRows') IS NULL
        ALTER TABLE IFS.FVN_SyncBatch ADD WorkCenterRows int NOT NULL
            CONSTRAINT DF_IFS_FVN_SyncBatch_WorkCenterRows_069 DEFAULT(0);

    IF EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncStageRow')
                 AND name = N'CK_IFS_Stage_Entity')
        ALTER TABLE IFS.FVN_SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;

    ALTER TABLE IFS.FVN_SyncStageRow WITH CHECK ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (EntityName IN ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE',
                        'BOM_COMPONENT','WAREHOUSE','SUPPLIER','PRODUCTION_LINE','SITE','WORK_CENTER'));

    /*
      The apply procedure was created by migration 012 using legacy staging
      names. Point all batch/stage references at the FVN tables, while keeping
      its existing FVN snapshot MERGEs and transaction logic unchanged.
    */
    DECLARE @Definition nvarchar(max);
    SET @Definition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));
    IF @Definition IS NULL
        THROW 56904, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    SET @Definition = REPLACE(@Definition, N'IFS.SyncBatch', N'IFS.FVN_SyncBatch');
    SET @Definition = REPLACE(@Definition, N'IFS.SyncStageRow', N'IFS.FVN_SyncStageRow');
    SET @Definition = REPLACE(@Definition, N'CREATE PROCEDURE', N'ALTER PROCEDURE');

    IF CHARINDEX(N'CREATE PROCEDURE', @Definition) > 0
        THROW 56905, 'Could not convert IFS.usp_ApplySyncBatch to ALTER PROCEDURE.', 1;

    EXEC sys.sp_executesql @Definition;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('069', N'IFS sync runtime alignment to FVN staging tables');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '069';
GO
