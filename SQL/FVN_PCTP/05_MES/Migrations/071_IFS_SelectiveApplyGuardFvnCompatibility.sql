/*
 Migration 071: remove the legacy mandatory-five-entities guard from the
 FVN-prefixed IFS apply procedure.

 Migration 037 was intended to remove this guard for SELECTIVE batches, but
 databases where the procedure was later rewritten/aligned by migration 069
 can retain the guard with FVN_SyncStageRow names. A SITE-only or WORK_CENTER
 batch then fails after its dedicated apply procedure succeeds.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57100, 'Migration 071 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 57101, 'FVN IFS staging/apply objects are missing. Apply migration 069 first.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 57102, 'MES.FVN_SchemaMigration is missing.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '071')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Definition nvarchar(max);
    DECLARE @Start int;
    DECLARE @GuardThrow int;
    DECLARE @End int;

    SET @Definition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));
    IF @Definition IS NULL
        THROW 57103, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    /* Locate the guard using the canonical FVN staging name. */
    SET @Start = CHARINDEX(
        N'IF NOT EXISTS(SELECT 1 FROM IFS.FVN_SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''PART_CATALOG'')',
        @Definition);

    IF @Start > 0
    BEGIN
        SET @GuardThrow = CHARINDEX(N'THROW 51202', @Definition, @Start);
        IF @GuardThrow = 0
            THROW 57104, 'Found mandatory-entity guard but cannot locate THROW 51202.', 1;

        SET @End = CHARINDEX(N';', @Definition, @GuardThrow);
        IF @End = 0
            THROW 57105, 'Cannot locate the end of mandatory-entity guard.', 1;

        SET @Definition = STUFF(@Definition, @Start, @End - @Start + 1, N'');
        SET @Definition = REPLACE(@Definition, N'CREATE PROCEDURE', N'ALTER PROCEDURE');

        IF CHARINDEX(N'CREATE PROCEDURE', @Definition) > 0
            THROW 57106, 'Could not convert apply procedure to ALTER PROCEDURE.', 1;

        EXEC sys.sp_executesql @Definition;
    END
    ELSE
    BEGIN
        /* Guard may already have been removed by migration 037. */
        IF CHARINDEX(N'THROW 51202', @Definition) > 0
            THROW 57107, 'Procedure contains THROW 51202 but guard signature did not match; inspect procedure before retrying.', 1;
    END;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('071', N'IFS selective apply guard FVN compatibility');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '071';
GO
