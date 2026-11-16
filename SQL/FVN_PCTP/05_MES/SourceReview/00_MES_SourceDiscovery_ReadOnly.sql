/*
    PCTP MES Phase 0 — read-only source discovery
    Purpose:
      - Inventory candidate existing SQL Server objects for raw materials, PO,
        receiving, LOT, QR, slots, production and finished goods.
      - Inspect columns, keys and foreign keys before designing new MES tables.
    Safety:
      - SELECT-only; does not create, update, delete or execute dynamic SQL.
      - Run against the PCTP application database, not the IFS Oracle database.
*/

SET NOCOUNT ON;

DECLARE @CandidateObjects TABLE
(
    ObjectId int NOT NULL PRIMARY KEY,
    SchemaName sysname NOT NULL,
    ObjectName sysname NOT NULL,
    ObjectType nvarchar(60) NOT NULL
);

INSERT INTO @CandidateObjects (ObjectId, SchemaName, ObjectName, ObjectType)
SELECT o.object_id, s.name, o.name, o.type_desc
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
  AND
  (
       o.name LIKE '%LOT%'
    OR o.name LIKE '%QR%'
    OR o.name LIKE '%STOCK%'
    OR o.name LIKE '%INVENTORY%'
    OR o.name LIKE '%MATERIAL%'
    OR o.name LIKE '%PURCHASE%'
    OR o.name LIKE '%ORDER%'
    OR o.name LIKE '%RECEIV%'
    OR o.name LIKE '%BOM%'
    OR o.name LIKE '%STRUCT%'
    OR o.name LIKE '%SLOT%'
    OR o.name LIKE '%SANXUAT%'
    OR o.name LIKE '%PRODUCTION%'
    OR o.name LIKE '%NHAPKHO%'
    OR o.name LIKE '%NHAPTP%'
  );

-- Result 1: candidate objects.
SELECT SchemaName, ObjectName, ObjectType, ObjectId
FROM @CandidateObjects
ORDER BY ObjectType, SchemaName, ObjectName;

-- Result 2: candidate columns with type, nullability and identity metadata.
SELECT
    c.SchemaName,
    c.ObjectName,
    c.ObjectType,
    col.column_id AS ColumnOrdinal,
    col.name AS ColumnName,
    typ.name AS DataType,
    CASE
        WHEN typ.name IN ('nvarchar', 'nchar') AND col.max_length > 0 THEN col.max_length / 2
        WHEN typ.name IN ('varchar', 'char', 'varbinary', 'binary') THEN col.max_length
        ELSE col.max_length
    END AS MaxLength,
    col.precision AS NumericPrecision,
    col.scale AS NumericScale,
    col.is_nullable AS IsNullable,
    col.is_identity AS IsIdentity,
    dc.definition AS DefaultDefinition
FROM @CandidateObjects c
INNER JOIN sys.columns col ON col.object_id = c.ObjectId
INNER JOIN sys.types typ ON typ.user_type_id = col.user_type_id
LEFT JOIN sys.default_constraints dc
    ON dc.parent_object_id = col.object_id
   AND dc.parent_column_id = col.column_id
WHERE
       col.name LIKE '%LOT%'
    OR col.name LIKE '%QR%'
    OR col.name LIKE '%PART%'
    OR col.name LIKE '%ITEM%'
    OR col.name LIKE '%QTY%'
    OR col.name LIKE '%QUANTITY%'
    OR col.name LIKE '%SL%'
    OR col.name LIKE '%PO%'
    OR col.name LIKE '%ORDER%'
    OR col.name LIKE '%DOC%'
    OR col.name LIKE '%STATUS%'
    OR col.name LIKE '%SLOT%'
    OR col.name LIKE '%WAREHOUSE%'
    OR col.name LIKE '%LOCATION%'
    OR col.name LIKE '%BOM%'
    OR col.name LIKE '%UOM%'
    OR col.name LIKE '%UNIT%'
    OR col.name LIKE '%TEMCODE%'
    OR col.name LIKE '%REFERENCE%'
    OR col.name LIKE '%PARENT%'
ORDER BY c.SchemaName, c.ObjectName, col.column_id;

-- Result 3: primary/unique keys and index columns.
SELECT
    c.SchemaName,
    c.ObjectName,
    i.name AS IndexName,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.is_disabled AS IsDisabled,
    ic.key_ordinal AS KeyOrdinal,
    ic.is_included_column AS IsIncludedColumn,
    col.name AS ColumnName
FROM @CandidateObjects c
INNER JOIN sys.indexes i
    ON i.object_id = c.ObjectId
   AND i.index_id > 0
INNER JOIN sys.index_columns ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
INNER JOIN sys.columns col
    ON col.object_id = ic.object_id
   AND col.column_id = ic.column_id
ORDER BY c.SchemaName, c.ObjectName, i.name, ic.key_ordinal, ic.index_column_id;

-- Result 4: declared foreign-key relationships among candidate objects.
SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn,
    fk.name AS ForeignKeyName,
    fk.is_disabled AS IsDisabled,
    fkc.constraint_column_id AS KeyOrdinal
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc
    ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
INNER JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
INNER JOIN sys.columns pc
    ON pc.object_id = fkc.parent_object_id
   AND pc.column_id = fkc.parent_column_id
INNER JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
INNER JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
INNER JOIN sys.columns rc
    ON rc.object_id = fkc.referenced_object_id
   AND rc.column_id = fkc.referenced_column_id
WHERE EXISTS (SELECT 1 FROM @CandidateObjects x WHERE x.ObjectId = pt.object_id)
   OR EXISTS (SELECT 1 FROM @CandidateObjects x WHERE x.ObjectId = rt.object_id)
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

-- Result 5: candidate object definitions for views/procedures/functions.
-- Review definitions manually; this query does not execute them.
SELECT
    c.SchemaName,
    c.ObjectName,
    sm.definition AS SqlDefinition
FROM @CandidateObjects c
INNER JOIN sys.sql_modules sm ON sm.object_id = c.ObjectId
WHERE c.ObjectType <> 'USER_TABLE'
ORDER BY c.SchemaName, c.ObjectName;
