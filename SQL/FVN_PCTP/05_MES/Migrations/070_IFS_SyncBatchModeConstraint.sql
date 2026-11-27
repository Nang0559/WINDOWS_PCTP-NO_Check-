/*
 Migration 070: normalize the IFS sync batch mode constraint.
 The selective sync runtime sends FULL or SELECTIVE. Migration 037 was intended
 to allow SELECTIVE, but an existing database can retain an older constraint.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57000, 'Migration 070 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL
    THROW 57001, 'IFS.FVN_SyncBatch is missing. Apply the FVN IFS sync migrations first.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 57002, 'MES.FVN_SchemaMigration is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '069')
    THROW 57003, 'Migration 069 must be applied before migration 070.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '070')
    RETURN;

IF EXISTS
(
    SELECT 1
    FROM IFS.FVN_SyncBatch
    WHERE SyncMode IS NULL
       OR SyncMode NOT IN ('FULL', 'INCREMENTAL', 'SELECTIVE')
)
    THROW 57004, 'IFS.FVN_SyncBatch contains a SyncMode outside FULL/INCREMENTAL/SELECTIVE. Review existing batch data before applying migration 070.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncBatch')
          AND name = N'CK_IFS_Batch_Mode'
    )
        ALTER TABLE IFS.FVN_SyncBatch DROP CONSTRAINT CK_IFS_Batch_Mode;

    ALTER TABLE IFS.FVN_SyncBatch WITH CHECK
        ADD CONSTRAINT CK_IFS_Batch_Mode
        CHECK (SyncMode IN ('FULL','INCREMENTAL','SELECTIVE'));

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('070', N'IFS SyncBatch mode constraint supports selective sync');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '070';
GO
