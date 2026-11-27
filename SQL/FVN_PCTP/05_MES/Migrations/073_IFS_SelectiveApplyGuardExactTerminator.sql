/*
 Migration 073: fix guard removal terminator matching.
 Migration 072 expected ", 1;" but the deployed procedure uses ",1;".
 Use the exact actual THROW terminator after THROW 51202; the message's
 internal semicolon is deliberately not used as a delimiter.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57300, 'Migration 073 may run only in FVN_PCTP.', 1;

IF OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 57301, 'FVN IFS staging/apply objects are missing.', 1;

IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 57302, 'MES.FVN_SchemaMigration is missing.', 1;

IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '073')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Definition nvarchar(max);
    DECLARE @Start int;
    DECLARE @ThrowAt int;
    DECLARE @TerminatorAt int;
    DECLARE @RemoveLength int;

    SET @Definition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));
    IF @Definition IS NULL
        THROW 57303, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    SET @Start = CHARINDEX(
        N'IF NOT EXISTS(SELECT 1 FROM IFS.FVN_SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''PART_CATALOG'')',
        @Definition);

    IF @Start > 0
    BEGIN
        SET @ThrowAt = CHARINDEX(N'THROW 51202', @Definition, @Start);
        IF @ThrowAt = 0
            THROW 57304, 'Mandatory-entity guard found but THROW 51202 is missing.', 1;

        /* Actual procedure syntax is THROW 51202,'message',1; */
        SET @TerminatorAt = CHARINDEX(N',1;', @Definition, @ThrowAt);
        IF @TerminatorAt = 0
            THROW 57305, 'Cannot find exact THROW 51202 terminator (,1;).', 1;

        SET @RemoveLength = @TerminatorAt + LEN(N',1;') - @Start;
        SET @Definition = STUFF(@Definition, @Start, @RemoveLength, N'');
        SET @Definition = REPLACE(@Definition, N'CREATE PROCEDURE', N'ALTER PROCEDURE');

        IF CHARINDEX(N'CREATE PROCEDURE', @Definition) > 0
            THROW 57306, 'Could not convert apply procedure to ALTER PROCEDURE.', 1;

        EXEC sys.sp_executesql @Definition;
    END
    ELSE IF CHARINDEX(N'THROW 51202', @Definition) > 0
    BEGIN
        THROW 57307, 'THROW 51202 remains but guard signature differs; inspect procedure before retrying.', 1;
    END;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('073', N'IFS selective apply guard exact terminator');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion IN ('071','072','073');
GO
