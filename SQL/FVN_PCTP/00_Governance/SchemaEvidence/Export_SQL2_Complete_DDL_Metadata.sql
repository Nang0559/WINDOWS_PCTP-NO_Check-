/*
    PCTP / WMS schema metadata supplement for SQL2.

    READ-ONLY: SELECT statements only. No USE, DDL, DML, temp-table writes, or
    procedure execution. Run in the DB57/B7R2_FCC context only if authorized.
    Export each result set to a separate CSV/text file and retain the headers.
    This supplements SQL2 column metadata; it is not a deployment script.
*/
SET NOCOUNT ON;

-- Result set 1: user-defined alias types. Needed to map types such as LongCodeType
-- to their actual SQL Server base type without guessing.
SELECT
    s.name AS SchemaName,
    t.name AS TypeName,
    bt.name AS BaseTypeName,
    t.max_length AS MaxLengthBytes,
    t.precision AS [Precision],
    t.scale AS Scale,
    t.is_nullable AS IsNullable,
    t.is_user_defined AS IsUserDefined,
    t.is_assembly_type AS IsAssemblyType
FROM sys.types AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.types AS bt
    ON bt.user_type_id = t.system_type_id
   AND bt.user_type_id = bt.system_type_id
WHERE t.is_user_defined = 1
ORDER BY s.name, t.name;

-- Result set 2: all table indexes and ordered key/included columns.
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
    ic.is_descending_key AS IsDescendingKey,
    c.name AS ColumnName
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.index_id > 0
ORDER BY s.name, t.name, i.index_id, ic.is_included_column, ic.key_ordinal, ic.index_column_id;

-- Result set 3: foreign keys, column pairs, actions and trust/disabled state.
SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn,
    fk.name AS ForeignKeyName,
    fkc.constraint_column_id AS ColumnOrdinal,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate,
    fk.is_disabled AS IsDisabled,
    fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
JOIN sys.columns AS pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
JOIN sys.columns AS rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

-- Result set 4: CHECK constraints.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    cc.name AS ConstraintName,
    cc.definition AS Definition,
    cc.is_disabled AS IsDisabled,
    cc.is_not_trusted AS IsNotTrusted,
    cc.is_not_for_replication AS IsNotForReplication
FROM sys.check_constraints AS cc
JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name, cc.name;

-- Result set 5: DEFAULT constraints. The definition is preserved as SQL text.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    dc.name AS ConstraintName,
    dc.definition AS Definition
FROM sys.default_constraints AS dc
JOIN sys.tables AS t ON t.object_id = dc.parent_object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
ORDER BY s.name, t.name, c.column_id;

-- Result set 6: object source definitions. Export to a separate file; do not execute
-- the result text as part of this metadata query.
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type AS ObjectType,
    o.type_desc AS ObjectTypeDescription,
    sm.definition AS SqlDefinition
FROM sys.sql_modules AS sm
JOIN sys.objects AS o ON o.object_id = sm.object_id
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.type IN ('P', 'V', 'FN', 'IF', 'TF', 'TR')
ORDER BY s.name, o.type_desc, o.name;
