/*
 Migration 013: standardize every physical user table in FVN_PCTP with FVN_ prefix.
 Run after 000..012. SQL Server 2016 compatible.
 Existing module/application references keep working through same-schema synonyms.
 New code and bulk-load APIs must use physical FVN_ table names.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51300, 'Migration 013 may run only in database FVN_PCTP.', 1;

IF OBJECT_ID(N'MES.SchemaMigration', N'U') IS NULL
   AND OBJECT_ID(N'MES.SchemaMigration', N'SN') IS NULL
    THROW 51301, 'MES migration ledger is missing. Run migrations 000..012 first.', 1;

IF NOT EXISTS (SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion = '012')
    THROW 51302, 'Migration 012 must be applied before migration 013.', 1;

IF EXISTS (SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion = '013')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE
        @SchemaName sysname,
        @OldName sysname,
        @NewName sysname,
        @OldQualified nvarchar(517),
        @NewQualified nvarchar(517),
        @Sql nvarchar(max);

    DECLARE TableCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.name, t.name
        FROM sys.tables AS t
        INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        WHERE UPPER(LEFT(t.name, 4)) <> N'FVN_'
        ORDER BY s.name, t.name;

    OPEN TableCursor;
    FETCH NEXT FROM TableCursor INTO @SchemaName, @OldName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @NewName = N'FVN_' + @OldName;
        SET @OldQualified = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@OldName);
        SET @NewQualified = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@NewName);

        IF LEN(@NewName) > 128
            THROW 51303, 'A table name is too long to add the FVN_ prefix.', 1;

        IF OBJECT_ID(@NewQualified) IS NOT NULL
            THROW 51304, 'Target FVN_ table name already exists; resolve the collision before migration.', 1;

        IF OBJECT_ID(@OldQualified, N'SN') IS NOT NULL
            THROW 51305, 'A legacy-name synonym already exists; resolve the object collision before migration.', 1;

        EXEC sys.sp_rename
            @objname = @OldQualified,
            @newname = @NewName,
            @objtype = N'OBJECT';

        SET @Sql =
            N'CREATE SYNONYM ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@OldName) +
            N' FOR ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@NewName) + N';';

        EXEC sys.sp_executesql @Sql;

        FETCH NEXT FROM TableCursor INTO @SchemaName, @OldName;
    END;

    CLOSE TableCursor;
    DEALLOCATE TableCursor;

    /*
       MERGE targets cannot rely on a compatibility synonym. Repoint the IFS
       synchronization MERGE targets to their physical FVN_ tables. Other
       ordinary SELECT/INSERT/UPDATE references may continue using synonyms.
    */
    DECLARE @ApplyProcedure nvarchar(max);
    SET @ApplyProcedure = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));

    IF @ApplyProcedure IS NULL
        THROW 51307, 'IFS.usp_ApplySyncBatch is missing or its definition is not visible.', 1;

    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'CREATE PROCEDURE', N'ALTER PROCEDURE');

    IF CHARINDEX(N'CREATE PROCEDURE', @ApplyProcedure) > 0
        THROW 51309, 'Could not convert IFS.usp_ApplySyncBatch to ALTER PROCEDURE.', 1;

    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'MERGE IFS.PartCatalog AS T', N'MERGE IFS.FVN_PartCatalog AS T');
    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'MERGE IFS.InventoryPart AS T', N'MERGE IFS.FVN_InventoryPart AS T');
    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'MERGE IFS.BomHeader AS T', N'MERGE IFS.FVN_BomHeader AS T');
    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'MERGE IFS.BomAlternative AS T', N'MERGE IFS.FVN_BomAlternative AS T');
    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'MERGE IFS.BomComponent AS T', N'MERGE IFS.FVN_BomComponent AS T');
    SET @ApplyProcedure = REPLACE(@ApplyProcedure, N'MERGE MES.IfsInventoryPart AS T', N'MERGE MES.FVN_IfsInventoryPart AS T');

    IF CHARINDEX(N'MERGE IFS.PartCatalog AS T', @ApplyProcedure) > 0
       OR CHARINDEX(N'MERGE IFS.InventoryPart AS T', @ApplyProcedure) > 0
       OR CHARINDEX(N'MERGE IFS.BomHeader AS T', @ApplyProcedure) > 0
       OR CHARINDEX(N'MERGE IFS.BomAlternative AS T', @ApplyProcedure) > 0
       OR CHARINDEX(N'MERGE IFS.BomComponent AS T', @ApplyProcedure) > 0
       OR CHARINDEX(N'MERGE MES.IfsInventoryPart AS T', @ApplyProcedure) > 0
        THROW 51308, 'Could not rewrite every IFS/MES MERGE target to its FVN_ physical table.', 1;

    EXEC sys.sp_executesql @ApplyProcedure;

    INSERT INTO MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('013', N'FVN_ physical table name standardization');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'TableCursor') >= 0
        CLOSE TableCursor;
    IF CURSOR_STATUS('local', 'TableCursor') > -3
        DEALLOCATE TableCursor;
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Verification: every physical user table in FVN_PCTP must now start with FVN_. */
SELECT s.name AS SchemaName, t.name AS PhysicalTableName
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE UPPER(LEFT(t.name, 4)) <> N'FVN_';

IF EXISTS
(
    SELECT 1
    FROM sys.tables AS t
    INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    WHERE UPPER(LEFT(t.name, 4)) <> N'FVN_'
)
    THROW 51306, 'FVN_ naming verification failed.', 1;

SELECT s.name AS SchemaName, sy.name AS CompatibilitySynonym,
       sy.base_object_name AS PhysicalTarget
FROM sys.synonyms AS sy
INNER JOIN sys.schemas AS s ON s.schema_id = sy.schema_id
ORDER BY s.name, sy.name;
GO
