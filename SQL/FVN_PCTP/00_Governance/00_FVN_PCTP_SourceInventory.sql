/*
    FVN_PCTP rebuild - source inventory (READ ONLY)
    Source database: B7R2_FCC
    Purpose: capture the current catalog metadata before separating PCTP-owned
             objects from BRAVO7-owned/shared objects.

    SAFETY:
      - Run this script ONLY while connected to B7R2_FCC.
      - It does not create, alter, delete, or update any database object or row.
      - It deliberately uses SQL Server 2012+ syntax (no STRING_AGG).
      - Save/export every result set for the migration review.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Evaluate DB_NAME() once into a variable. RAISERROR arguments must be
-- constants or variables on SQL Server; passing DB_NAME() directly can fail.
DECLARE @CurrentDatabase sysname;
SET @CurrentDatabase = DB_NAME();

IF @CurrentDatabase <> N'B7R2_FCC'
BEGIN
    RAISERROR(N'Safety stop: this inventory must run against B7R2_FCC. Current database: %s', 16, 1, @CurrentDatabase);
    RETURN;
END;

PRINT N'FVN_PCTP SOURCE INVENTORY - READ ONLY';
PRINT N'Server: ' + CONVERT(nvarchar(256), SERVERPROPERTY('ServerName'));
PRINT N'Database: ' + DB_NAME();
PRINT N'SQL version: ' + CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion'));

-- 1. Database and compatibility metadata.
SELECT
    DB_NAME() AS DatabaseName,
    d.compatibility_level,
    d.collation_name,
    d.recovery_model_desc,
    d.is_read_only,
    d.state_desc,
    SERVERPROPERTY('ProductVersion') AS ProductVersion,
    SERVERPROPERTY('Edition') AS SqlEdition
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

-- 2. All user tables, with a conservative classification hint only.
--    A hint is NOT an ownership decision; review dependencies and application usage.
SELECT
    s.name AS SchemaName,
    t.name AS ObjectName,
    CASE
        WHEN t.name LIKE N'FVN[_]%' THEN N'FVN_CUSTOM_CANDIDATE'
        WHEN t.name IN (
            N'SlotLot', N'STOCKTP', N'StockHistory',
            N'DOCQRCODE', N'DOCQRCODE_SP', N'SP_DOCQRCODE', N'YMVN_DOCQRCODE',
            N'TMPPHIEUGIAOHANG', N'TMPPHIEUGIAOHANGDB',
            N'TMPPHIEUGIAOHANG_100002', N'TMPPHIEUGIAOHANG_100003',
            N'TMPPHIEUGIAOHANG_SP', N'LUUPHIEUGIAOHANG',
            N'IFSPHIEUGIAOHANG', N'IFSPHIEUGIAOHANG_SP',
            N'IFSPHIEUGIAOHANG_100002', N'IFSPHIEUGIAOHANG_100003'
        ) THEN N'PCTP_LEGACY_CANDIDATE'
        ELSE N'UNCLASSIFIED_REVIEW'
    END AS ClassificationHint,
    t.create_date,
    t.modify_date,
    SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) AS ApproxRowCount
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name, t.create_date, t.modify_date
ORDER BY ClassificationHint, s.name, t.name;

-- 3. Columns for every user table and view.
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    c.column_id,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE
        WHEN ty.name IN (N'nvarchar', N'nchar') AND c.max_length <> -1 THEN c.max_length / 2
        WHEN ty.name IN (N'varchar', N'char', N'varbinary', N'binary') AND c.max_length <> -1 THEN c.max_length
        ELSE c.max_length
    END AS MaxLength,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    c.is_computed,
    dc.definition AS DefaultDefinition,
    cc.definition AS ComputedDefinition
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
JOIN sys.columns AS c ON c.object_id = o.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id AND cc.column_id = c.column_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('U', 'V')
ORDER BY s.name, o.name, c.column_id;

-- 4. Primary keys, unique constraints, and foreign keys.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    kc.name AS ConstraintName,
    kc.type_desc AS ConstraintType,
    i.name AS SupportingIndex,
    ic.key_ordinal,
    c.name AS ColumnName
FROM sys.key_constraints AS kc
JOIN sys.tables AS t ON t.object_id = kc.parent_object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
ORDER BY s.name, t.name, kc.name, ic.key_ordinal;

SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    fk.name AS ForeignKeyName,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn,
    fkc.constraint_column_id,
    fk.delete_referential_action_desc,
    fk.update_referential_action_desc,
    fk.is_disabled,
    fk.is_not_trusted
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
JOIN sys.columns AS pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
JOIN sys.columns AS rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

-- 5. Indexes (including included columns and filter definitions).
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc,
    i.is_primary_key,
    i.is_unique,
    i.is_unique_constraint,
    i.is_disabled,
    i.has_filter,
    i.filter_definition,
    ic.key_ordinal,
    ic.is_included_column,
    c.name AS ColumnName,
    ic.is_descending_key
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.index_id > 0
  AND i.is_hypothetical = 0
ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id;

-- 6. Check constraints, triggers, sequences, synonyms, and user-defined types.
SELECT
    s.name AS SchemaName, t.name AS TableName,
    cc.name AS ConstraintName, cc.definition,
    cc.is_disabled, cc.is_not_trusted
FROM sys.check_constraints AS cc
JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name, cc.name;

SELECT
    OBJECT_SCHEMA_NAME(tr.parent_id) AS ParentSchema,
    OBJECT_NAME(tr.parent_id) AS ParentObject,
    tr.name AS TriggerName,
    tr.type_desc,
    tr.is_disabled,
    OBJECT_DEFINITION(tr.object_id) AS TriggerDefinition
FROM sys.triggers AS tr
WHERE tr.is_ms_shipped = 0
ORDER BY ParentSchema, ParentObject, tr.name;

SELECT
    SCHEMA_NAME(schema_id) AS SchemaName,
    name AS SequenceName,
    TYPE_NAME(user_type_id) AS DataType,
    start_value, increment, minimum_value, maximum_value, is_cycling
FROM sys.sequences
ORDER BY SchemaName, SequenceName;

SELECT
    SCHEMA_NAME(schema_id) AS SchemaName,
    name AS SynonymName,
    base_object_name
FROM sys.synonyms
ORDER BY SchemaName, SynonymName;

SELECT
    SCHEMA_NAME(schema_id) AS SchemaName,
    name AS TypeName,
    is_user_defined,
    is_table_type,
    TYPE_NAME(system_type_id) AS SystemType,
    max_length, precision, scale
FROM sys.types
WHERE is_user_defined = 1
ORDER BY SchemaName, TypeName;

-- 7. Views, procedures, functions and their dependencies.
--    Definitions are included for non-system modules so the migration can
--    identify hard-coded database names and external BRAVO7 dependencies.
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    o.create_date,
    o.modify_date,
    OBJECTPROPERTY(o.object_id, 'IsSchemaBound') AS IsSchemaBound,
    m.uses_ansi_nulls,
    m.uses_quoted_identifier,
    m.is_recompiled,
    m.definition AS ModuleDefinition
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules AS m ON m.object_id = o.object_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('P','V','FN','IF','TF','FS','FT')
ORDER BY s.name, o.type_desc, o.name;

SELECT
    OBJECT_SCHEMA_NAME(d.referencing_id) AS ReferencingSchema,
    OBJECT_NAME(d.referencing_id) AS ReferencingObject,
    o.type_desc AS ReferencingType,
    d.referenced_server_name,
    d.referenced_database_name,
    d.referenced_schema_name,
    d.referenced_entity_name,
    d.is_schema_bound_reference,
    d.is_ambiguous
FROM sys.sql_expression_dependencies AS d
JOIN sys.objects AS o ON o.object_id = d.referencing_id
WHERE o.is_ms_shipped = 0
ORDER BY ReferencingSchema, ReferencingObject, d.referenced_database_name,
         d.referenced_schema_name, d.referenced_entity_name;

-- 8. Explicit object list referenced by the PCTP SQL deployment scripts.
--    Missing objects are shown too; this is a review checklist, not a create list.
DECLARE @PctpObjects TABLE (ObjectName sysname NOT NULL PRIMARY KEY);
INSERT INTO @PctpObjects (ObjectName) VALUES
(N'IFSPHIEUGIAOHANG_100002'),(N'IFSPHIEUGIAOHANG'),(N'IFSPHIEUGIAOHANGView'),
(N'TMPPHIEUGIAOHANGView_DK_PC21'),(N'SP_DOCQRCODE'),(N'YMVN_DOCQRCODE'),
(N'DOCQRCODE_SP'),(N'DOCQRCODE'),(N'PCTPStockMovementGuard'),
(N'IFSPHIEUGIAOHANG_100003'),(N'IFSPHIEUGIAOHANG_SP'),
(N'FVN_TraHangQTChungXuat'),(N'FVN_sys_WorkflowTransitions'),
(N'FVN_Purchase_Order_HVNDB'),(N'FVN_PCTPQuarantineReleaseAudit'),
(N'FVN_DeliveryCommandLedger'),(N'FVN_DeliveryCommandQr'),
(N'LUUPHIEUGIAOHANG'),(N'FVN_TraHangQTChungQC'),
(N'FVN_PhieuXuLyBatThuong'),(N'SlotLot'),(N'STOCKTP'),
(N'FVN_HangChoGiaoPickDetail'),(N'StockHistory'),
(N'FVN_TraHangQTChungNhapNG'),(N'FVN_TraHangQTChungGiao'),
(N'FVN_PhieuTraHangCT'),(N'FVN_PhieuTraHang'),
(N'FVN_PXLB_WorkflowAudit'),(N'FVN_PXLB_ReworkMovement'),
(N'FVN_PXLB_CompensationMovement'),(N'FVN_PXLB_Compensation'),
(N'FVN_PXLB_Disposition'),(N'FVN_PXLB_ReworkQC'),
(N'FVN_PXLB_ReworkPlan'),(N'FVN_PhieuXuLyBatThuongQCInitial'),
(N'FVN_PhieuXuLyBatThuongAffectedLot'),(N'TMPPHIEUGIAOHANGDB_IFS'),
(N'FVN_Purchase_Order_DB_HD'),(N'FVN_Purchase_Order_DB_CT'),
(N'TMPPHIEUGIAOHANGDB'),(N'FVN_ItemFifoConfig_History'),
(N'FVN_ItemFifoConfig'),(N'TMPLOTGHEP'),(N'PhieuXuLyBatThuong');

SELECT
    p.ObjectName,
    CASE WHEN o.object_id IS NULL THEN 0 ELSE 1 END AS ExistsInSource,
    o.type_desc AS ObjectType,
    s.name AS SchemaName,
    o.create_date,
    o.modify_date
FROM @PctpObjects AS p
LEFT JOIN sys.objects AS o
    ON o.name = p.ObjectName AND o.is_ms_shipped = 0
LEFT JOIN sys.schemas AS s ON s.schema_id = o.schema_id
ORDER BY p.ObjectName;

PRINT N'Inventory completed. No database objects or business rows were changed.';
GO
