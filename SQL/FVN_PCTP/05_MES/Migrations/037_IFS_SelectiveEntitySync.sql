/*
 Migration 037: allow transactional, selective IFS entity synchronization.
 Prerequisites: migrations 012, 027 and 028.
 SQL Server 2016 compatible. IFS remains read-only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 53700, 'Migration 037 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 53701, 'MES.FVN_SchemaMigration is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '028')
    THROW 53702, 'Migration 028 must be applied before migration 037.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '037')
    RETURN;

IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 53703, 'IFS synchronization objects are missing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Store the explicit selection with the batch for audit/history. */
    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'SelectedEntities') IS NULL
        ALTER TABLE IFS.FVN_SyncBatch ADD SelectedEntities nvarchar(500) NULL;

    /* FULL remains unchanged; SELECTIVE records a batch that stages only selected entities. */
    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncBatch')
          AND name = N'CK_IFS_Batch_Mode'
    )
        ALTER TABLE IFS.FVN_SyncBatch DROP CONSTRAINT CK_IFS_Batch_Mode;

    ALTER TABLE IFS.FVN_SyncBatch WITH CHECK
        ADD CONSTRAINT CK_IFS_Batch_Mode
        CHECK (SyncMode IN ('FULL','INCREMENTAL','SELECTIVE'));

    /*
      The original procedure rejected any batch that did not contain all five
      core entities. Selective batches are safe because each MERGE already reads
      only its own EntityName; an empty source is a no-op. C# expands BOM child
      selections to include their parent entities, preserving FK dependencies.
    */
    DECLARE @Definition nvarchar(max);
    DECLARE @Start int;
    DECLARE @End int;
    DECLARE @EndToken nvarchar(100) = N'THROW 51202';

    SET @Definition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));
    IF @Definition IS NULL
        THROW 53704, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    SET @Start = CHARINDEX(
        N'IF NOT EXISTS(SELECT 1 FROM IFS.FVN_SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''PART_CATALOG'')',
        @Definition);
    SET @End = CHARINDEX(@EndToken, @Definition, @Start);

    IF @Start > 0 AND @End > @Start
    BEGIN
        SET @End = CHARINDEX(N';', @Definition, @End);
        IF @End = 0
            THROW 53705, 'Cannot locate the end of the required-entity guard.', 1;
        SET @Definition = STUFF(@Definition, @Start, @End - @Start + 1, N'');
    END
    ELSE IF @Start = 0
    BEGIN
        /* Idempotent compatibility: no action when the guard is already absent. */
        SET @Start = 0;
    END
    ELSE
        THROW 53706, 'Could not locate the complete required-entity guard.', 1;

    SET @Definition = REPLACE(@Definition, N'CREATE PROCEDURE', N'ALTER PROCEDURE');
    IF CHARINDEX(N'CREATE PROCEDURE', @Definition) > 0
        THROW 53707, 'Could not convert apply procedure to ALTER PROCEDURE.', 1;

    EXEC sys.sp_executesql @Definition;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('037', N'IFS selective entity synchronization');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '037';
GO
