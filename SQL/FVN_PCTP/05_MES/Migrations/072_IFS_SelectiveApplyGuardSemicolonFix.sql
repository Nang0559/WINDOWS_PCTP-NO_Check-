/*
 Migration 072: safely remove the legacy mandatory-entity guard.
 Fixes migration 071's delimiter bug: the THROW message itself contains
 a semicolon ("no rows; batch rejected"), so the end must be located
 after the THROW state terminator, not at the first semicolon.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57200, 'Migration 072 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 57201, 'FVN IFS staging/apply objects are missing.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 57202, 'MES.FVN_SchemaMigration is missing.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '072')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Definition nvarchar(max);
    DECLARE @Start int;
    DECLARE @ThrowAt int;
    DECLARE @TerminatorAt int;

    SET @Definition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));
    IF @Definition IS NULL
        THROW 57203, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    SET @Start = CHARINDEX(
        N'IF NOT EXISTS(SELECT 1 FROM IFS.FVN_SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''PART_CATALOG'')',
        @Definition);

    IF @Start > 0
    BEGIN
        SET @ThrowAt = CHARINDEX(N'THROW 51202', @Definition, @Start);
        IF @ThrowAt = 0
            THROW 57204, 'Mandatory-entity guard found but THROW 51202 is missing.', 1;

        /* Find the THROW statement terminator after its state argument. */
        SET @TerminatorAt = CHARINDEX(N', 1;', @Definition, @ThrowAt);
        IF @TerminatorAt = 0
            THROW 57205, 'Cannot find the end of THROW 51202; inspect procedure definition.', 1;

        SET @Definition = STUFF(@Definition, @Start, @TerminatorAt + 4 - @Start, N'');
        SET @Definition = REPLACE(@Definition, N'CREATE PROCEDURE', N'ALTER PROCEDURE');

        IF CHARINDEX(N'CREATE PROCEDURE', @Definition) > 0
            THROW 57206, 'Could not convert apply procedure to ALTER PROCEDURE.', 1;

        EXEC sys.sp_executesql @Definition;
    END
    ELSE IF CHARINDEX(N'THROW 51202', @Definition) > 0
    BEGIN
        THROW 57207, 'THROW 51202 remains but guard signature differs; inspect procedure before retrying.', 1;
    END;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('072', N'IFS selective apply guard semicolon fix');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion IN ('071', '072');
GO
