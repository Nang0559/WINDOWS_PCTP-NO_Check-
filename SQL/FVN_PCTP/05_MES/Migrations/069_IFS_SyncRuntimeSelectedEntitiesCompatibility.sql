/*
 Migration 069: IFS sync runtime compatibility fix for the application-owned legacy object names.

 The runtime in IfsSyncService/FormIfsSync writes and reads IFS.SyncBatch and IFS.SyncStageRow.
 Migration 037 added SelectedEntities to IFS.FVN_SyncBatch instead, so the connection check and
 BeginBatch can fail with Msg 207 on IFS.SyncBatch. This migration adds the runtime columns to
 the exact objects used by the application. It does not rename/drop data or modify the shared
 apply procedure.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 56900, 'Migration 069 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'IFS.SyncBatch', N'U') IS NULL
   OR OBJECT_ID(N'IFS.SyncStageRow', N'U') IS NULL
    THROW 56901, 'IFS.SyncBatch / IFS.SyncStageRow are missing. Apply IFS migration 012 first.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 56902, 'MES.FVN_SchemaMigration is missing.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '069')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'IFS.SyncBatch', N'SelectedEntities') IS NULL
        ALTER TABLE IFS.SyncBatch ADD SelectedEntities nvarchar(500) NULL;

    IF COL_LENGTH(N'IFS.SyncBatch', N'SiteRows') IS NULL
        ALTER TABLE IFS.SyncBatch ADD SiteRows int NOT NULL
            CONSTRAINT DF_IFS_SyncBatch_SiteRows_069 DEFAULT(0);

    IF COL_LENGTH(N'IFS.SyncBatch', N'WorkCenterRows') IS NULL
        ALTER TABLE IFS.SyncBatch ADD WorkCenterRows int NOT NULL
            CONSTRAINT DF_IFS_SyncBatch_WorkCenterRows_069 DEFAULT(0);

    IF EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'IFS.SyncStageRow')
                 AND name = N'CK_IFS_Stage_Entity')
        ALTER TABLE IFS.SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;

    ALTER TABLE IFS.SyncStageRow WITH CHECK ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (EntityName IN ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE',
                        'BOM_COMPONENT','WAREHOUSE','SUPPLIER','PRODUCTION_LINE','SITE','WORK_CENTER'));

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('069', N'IFS sync runtime SelectedEntities compatibility fix');

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
