/*
    00C - Application Usage & Ownership Evidence (READ ONLY)
    Repository: Nang0559/WINDOWS_PCTP-NO_Check-
    Branch: feature/mes-product-genealogy-timeline
    Source database: B7R2_FCC

    PURPOSE
      Collect SQL-side evidence for the six PCTP operational tables reviewed
      in SQL/00C_ApplicationUsageOwnershipReview.md:
        dbo.LUUPHIEUGIAOHANG
        dbo.STOCKTP
        dbo.SlotLot
        dbo.StockHistory
        dbo.IFSPHIEUGIAOHANG
        dbo.IFSPHIEUGIAOHANG_SP

    SAFETY
      - Run ONLY while connected to B7R2_FCC.
      - SELECT-only against source catalogs and msdb metadata.
      - No CREATE/ALTER/DROP/INSERT/UPDATE/DELETE against persistent objects.
      - Does not copy data, change permissions, or change application settings.
      - A SQL text hit is evidence to inspect, NOT proof that the module is active.
      - No hit does NOT prove there is no dependency (dynamic SQL, encrypted modules,
        application-generated SQL, external schedulers and linked servers may hide it).
      - Export every result set separately and retain the exact source snapshot date.

    SQL Server compatibility: SQL Server 2012+ (no STRING_AGG).
*/
SET NOCOUNT ON;

DECLARE @CurrentDatabase sysname;
SET @CurrentDatabase = DB_NAME();

IF @CurrentDatabase <> N'B7R2_FCC'
BEGIN
    RAISERROR(N'Safety stop: 00C must run against B7R2_FCC. Current database: %s',
              16, 1, @CurrentDatabase);
    RETURN;
END;

DECLARE @Targets TABLE
(
    TableName sysname NOT NULL PRIMARY KEY
);

INSERT INTO @Targets (TableName)
VALUES
    (N'LUUPHIEUGIAOHANG'),
    (N'STOCKTP'),
    (N'SlotLot'),
    (N'StockHistory'),
    (N'IFSPHIEUGIAOHANG'),
    (N'IFSPHIEUGIAOHANG_SP');

PRINT N'00C APPLICATION USAGE & OWNERSHIP EVIDENCE - READ ONLY';
PRINT N'Server: ' + CONVERT(nvarchar(256), SERVERPROPERTY('ServerName'));
PRINT N'Database: ' + DB_NAME();
PRINT N'SQL version: ' + CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion'));
PRINT N'Capture time: ' + CONVERT(nvarchar(30), GETDATE(), 126);

-------------------------------------------------------------------------------
-- RESULT 00: Run manifest and target list.
-------------------------------------------------------------------------------
SELECT
    DB_NAME() AS SourceDatabase,
    CONVERT(nvarchar(256), SERVERPROPERTY('ServerName')) AS ServerName,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    GETDATE() AS CapturedAt,
    N'READ_ONLY' AS ExecutionMode,
    N'Application usage evidence; no ownership or migration approval implied' AS Purpose;

SELECT TableName AS TargetTable
FROM @Targets
ORDER BY TableName;

-------------------------------------------------------------------------------
-- RESULT 01: Target table existence, row estimate and physical properties.
-------------------------------------------------------------------------------
SELECT
    DB_NAME() AS SourceDatabase,
    s.name AS SchemaName,
    t.name AS TableName,
    t.object_id AS ObjectId,
    t.create_date AS CreatedAt,
    t.modify_date AS ModifiedAt,
    SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) AS ApproxRowCount,
    t.temporal_type_desc AS TemporalType,
    t.is_memory_optimized AS IsMemoryOptimized,
    t.is_tracked_by_cdc AS IsTrackedByCdc,
    CASE WHEN t.object_id IS NULL THEN N'MISSING' ELSE N'FOUND' END AS ObjectStatus
FROM @Targets AS x
LEFT JOIN sys.tables AS t ON t.name = x.TableName
LEFT JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id
GROUP BY s.name, t.name, t.object_id, t.create_date, t.modify_date,
         t.temporal_type_desc, t.is_memory_optimized, t.is_tracked_by_cdc
ORDER BY x.TableName, s.name;

-------------------------------------------------------------------------------
-- RESULT 02: Exact column contract for the six tables.
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnOrdinal,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE
        WHEN ty.name IN (N'nvarchar', N'nchar') AND c.max_length <> -1 THEN c.max_length / 2
        WHEN ty.name IN (N'varchar', N'char', N'varbinary', N'binary') AND c.max_length <> -1 THEN c.max_length
        ELSE c.max_length
    END AS MaxLength,
    c.precision AS NumericPrecision,
    c.scale AS NumericScale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    c.is_computed AS IsComputed,
    c.is_rowguidcol AS IsRowGuid,
    c.collation_name AS CollationName,
    dc.definition AS DefaultDefinition,
    cc.definition AS ComputedDefinition
FROM @Targets AS x
JOIN sys.tables AS t ON t.name = x.TableName
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id AND cc.column_id = c.column_id
ORDER BY t.name, c.column_id;

-------------------------------------------------------------------------------
-- RESULT 03: Indexes, PKs, unique constraints and FK edges touching targets.
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.is_unique_constraint AS IsUniqueConstraint,
    i.is_disabled AS IsDisabled,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    ic.key_ordinal AS KeyOrdinal,
    ic.is_included_column AS IsIncludedColumn,
    c.name AS ColumnName
FROM @Targets AS x
JOIN sys.tables AS t ON t.name = x.TableName
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.index_id > 0
ORDER BY t.name, i.name, ic.key_ordinal, ic.index_column_id;

SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    fk.name AS ForeignKeyName,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn,
    fkc.constraint_column_id AS ColumnOrdinal,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate,
    fk.is_disabled AS IsDisabled,
    fk.is_not_trusted AS IsNotTrusted,
    CASE
        WHEN pt.name IN (SELECT TableName FROM @Targets)
         AND rt.name IN (SELECT TableName FROM @Targets) THEN N'TARGET_TO_TARGET'
        WHEN pt.name IN (SELECT TableName FROM @Targets) THEN N'TARGET_TO_OTHER'
        ELSE N'OTHER_TO_TARGET'
    END AS RelationshipScope
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
JOIN sys.columns AS pc
    ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
JOIN sys.columns AS rc
    ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
WHERE pt.name IN (SELECT TableName FROM @Targets)
   OR rt.name IN (SELECT TableName FROM @Targets)
ORDER BY pt.name, fk.name, fkc.constraint_column_id;

-------------------------------------------------------------------------------
-- RESULT 04: Check/default constraints and triggers attached to target tables.
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    cc.name AS ConstraintName,
    cc.definition AS ConstraintDefinition,
    cc.is_disabled AS IsDisabled,
    cc.is_not_trusted AS IsNotTrusted
FROM @Targets AS x
JOIN sys.tables AS t ON t.name = x.TableName
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.check_constraints AS cc ON cc.parent_object_id = t.object_id
ORDER BY t.name, cc.name;

SELECT
    s.name AS TableSchema,
    t.name AS TableName,
    tr.name AS TriggerName,
    tr.is_disabled AS IsDisabled,
    tr.is_instead_of_trigger AS IsInsteadOfTrigger,
    tr.is_not_for_replication AS IsNotForReplication,
    OBJECTPROPERTY(tr.object_id, 'ExecIsFirstInsertTrigger') AS IsFirstInsertTrigger,
    OBJECTPROPERTY(tr.object_id, 'ExecIsLastInsertTrigger') AS IsLastInsertTrigger,
    m.definition AS TriggerDefinition
FROM @Targets AS x
JOIN sys.tables AS t ON t.name = x.TableName
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.triggers AS tr ON tr.parent_id = t.object_id
LEFT JOIN sys.sql_modules AS m ON m.object_id = tr.object_id
ORDER BY t.name, tr.name;

-------------------------------------------------------------------------------
-- RESULT 05: SQL modules whose definitions mention one or more target names.
-- Search is intentionally broad; inspect the full definition and callers.
-------------------------------------------------------------------------------
SELECT
    s.name AS ModuleSchema,
    o.name AS ModuleName,
    o.type_desc AS ModuleType,
    o.create_date AS CreatedAt,
    o.modify_date AS ModifiedAt,
    x.TableName AS MatchedTargetTable,
    CASE WHEN CHARINDEX(N'INSERT', UPPER(m.definition)) > 0 THEN 1 ELSE 0 END AS HasInsertKeyword,
    CASE WHEN CHARINDEX(N'UPDATE', UPPER(m.definition)) > 0 THEN 1 ELSE 0 END AS HasUpdateKeyword,
    CASE WHEN CHARINDEX(N'DELETE', UPPER(m.definition)) > 0 THEN 1 ELSE 0 END AS HasDeleteKeyword,
    CASE WHEN CHARINDEX(N'MERGE', UPPER(m.definition)) > 0 THEN 1 ELSE 0 END AS HasMergeKeyword,
    CASE
        WHEN CHARINDEX(N'SP_EXECUTESQL', UPPER(m.definition)) > 0
          OR CHARINDEX(N'EXEC(', UPPER(m.definition)) > 0
          OR CHARINDEX(N'EXECUTE(', UPPER(m.definition)) > 0
          OR CHARINDEX(N'QUOTENAME', UPPER(m.definition)) > 0
        THEN 1 ELSE 0
    END AS PossibleDynamicSql,
    CASE WHEN m.definition IS NULL THEN 1 ELSE 0 END AS DefinitionUnavailable,
    m.definition AS ModuleDefinition
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules AS m ON m.object_id = o.object_id
CROSS JOIN @Targets AS x
WHERE o.is_ms_shipped = 0
  AND o.type IN ('P','V','FN','IF','TF','TR')
  AND
  (
      CHARINDEX(x.TableName, ISNULL(m.definition, N'')) > 0
      OR CHARINDEX(N'[' + x.TableName + N']', ISNULL(m.definition, N'')) > 0
  )
ORDER BY x.TableName, o.type_desc, s.name, o.name;

-------------------------------------------------------------------------------
-- RESULT 06: Static dependencies in either direction for target objects.
-- SQL Server metadata does not fully discover dynamic SQL or client-side SQL.
-------------------------------------------------------------------------------
SELECT
    OBJECT_SCHEMA_NAME(d.referencing_id) AS ReferencingSchema,
    OBJECT_NAME(d.referencing_id) AS ReferencingObject,
    ro.type_desc AS ReferencingType,
    d.referenced_server_name AS ReferencedServer,
    d.referenced_database_name AS ReferencedDatabase,
    d.referenced_schema_name AS ReferencedSchema,
    d.referenced_entity_name AS ReferencedObject,
    d.is_schema_bound_reference AS IsSchemaBound,
    d.is_ambiguous AS IsAmbiguous,
    CASE
        WHEN d.referenced_entity_name IN (SELECT TableName FROM @Targets)
            THEN N'MODULE_REFERENCES_TARGET'
        WHEN OBJECT_NAME(d.referencing_id) IN (SELECT TableName FROM @Targets)
            THEN N'TARGET_REFERENCES_OBJECT'
        ELSE N'REVIEW'
    END AS DependencyDirection
FROM sys.sql_expression_dependencies AS d
JOIN sys.objects AS ro ON ro.object_id = d.referencing_id
WHERE d.referenced_entity_name IN (SELECT TableName FROM @Targets)
   OR OBJECT_NAME(d.referencing_id) IN (SELECT TableName FROM @Targets)
ORDER BY ReferencingSchema, ReferencingObject, ReferencedDatabase,
         ReferencedSchema, ReferencedObject;

-------------------------------------------------------------------------------
-- RESULT 07: SQL Agent job steps that mention target names.
-- This result may be empty or inaccessible if SQL Agent metadata is unavailable.
-------------------------------------------------------------------------------
BEGIN TRY
    SELECT
        j.name AS JobName,
        j.enabled AS IsJobEnabled,
        js.step_id AS StepId,
        js.step_name AS StepName,
        js.subsystem AS Subsystem,
        js.database_name AS ExecutionDatabase,
        js.command AS CommandText,
        x.TableName AS MatchedTargetTable
    FROM msdb.dbo.sysjobs AS j
    JOIN msdb.dbo.sysjobsteps AS js ON js.job_id = j.job_id
    CROSS JOIN @Targets AS x
    WHERE CHARINDEX(x.TableName, js.command) > 0
       OR CHARINDEX(N'[' + x.TableName + N']', js.command) > 0
    ORDER BY j.name, js.step_id, x.TableName;
END TRY
BEGIN CATCH
    SELECT
        N'UNAVAILABLE' AS ResultStatus,
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage,
        N'Ask DBA to export matching msdb job steps; do not treat this as no jobs.' AS RequiredAction;
END CATCH;

-------------------------------------------------------------------------------
-- RESULT 08: Synonyms and user-defined types relevant to the target workflows.
-------------------------------------------------------------------------------
SELECT
    SCHEMA_NAME(sn.schema_id) AS SchemaName,
    sn.name AS SynonymName,
    sn.base_object_name AS BaseObjectName
FROM sys.synonyms AS sn
WHERE
    EXISTS
    (
        SELECT 1 FROM @Targets AS x
        WHERE CHARINDEX(x.TableName, sn.base_object_name) > 0
    )
ORDER BY sn.name;

SELECT
    SCHEMA_NAME(ty.schema_id) AS SchemaName,
    ty.name AS TypeName,
    ty.is_user_defined AS IsUserDefined,
    ty.is_table_type AS IsTableType,
    TYPE_NAME(ty.system_type_id) AS SystemType,
    ty.max_length AS MaxLength,
    ty.precision AS NumericPrecision,
    ty.scale AS NumericScale
FROM sys.types AS ty
WHERE ty.is_user_defined = 1
ORDER BY SchemaName, TypeName;

PRINT N'00C completed. Export all result sets. No persistent database objects or business rows were changed.';
GO
