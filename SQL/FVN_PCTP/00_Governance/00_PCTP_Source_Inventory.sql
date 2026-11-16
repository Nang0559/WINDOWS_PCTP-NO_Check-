/*
PCTP -> FVN_PCTP | SQL 00 - SOURCE DATABASE INVENTORY
SQL Server 2016+
READ-ONLY: does not create, alter, drop, or modify source objects/data.

Run this in SSMS with the CURRENT DATABASE set to the existing PCTP source.
Do not run against B7R2_FCC. Export every result grid as CSV/Excel.
SQL Agent jobs are instance-level and are not fully covered here.
Encrypted module definitions may not be visible through sys.sql_modules.
*/
SET NOCOUNT ON;

SELECT
    DB_NAME() AS SourceDatabase,
    d.compatibility_level, d.collation_name, d.recovery_model_desc,
    d.page_verify_option_desc, d.is_read_only, d.is_trustworthy_on,
    d.is_broker_enabled, d.is_cdc_enabled, d.is_encrypted,
    SERVERPROPERTY('ProductVersion') AS SqlServerProductVersion,
    SERVERPROPERTY('Edition') AS SqlServerEdition
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

-- 01. All user objects
SELECT s.name AS SchemaName, o.name AS ObjectName, o.type, o.type_desc,
       o.create_date, o.modify_date, o.is_ms_shipped
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0
ORDER BY s.name, o.type_desc, o.name;

-- 02. Object counts
SELECT s.name AS SchemaName, o.type, o.type_desc, COUNT_BIG(*) AS ObjectCount
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0
GROUP BY s.name, o.type, o.type_desc
ORDER BY s.name, o.type_desc;

-- 03. Table columns, identity, computed and default definitions
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName,
       c.column_id, c.name AS ColumnName, TYPE_SCHEMA_NAME(ty.schema_id) AS TypeSchema,
       ty.name AS DataType, c.max_length, c.precision, c.scale, c.is_nullable,
       c.is_identity, ic.seed_value AS IdentitySeed, ic.increment_value AS IdentityIncrement,
       c.is_computed, cc.definition AS ComputedDefinition,
       dc.name AS DefaultConstraintName, dc.definition AS DefaultDefinition,
       c.collation_name, c.is_rowguidcol, c.is_sparse
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE t.is_ms_shipped = 0
ORDER BY SCHEMA_NAME(t.schema_id), t.name, c.column_id;

-- 04. PK, unique constraints, FK, CHECK constraints
SELECT N'PRIMARY_OR_UNIQUE' AS ConstraintType, SCHEMA_NAME(t.schema_id) AS SchemaName,
       t.name AS TableName, kc.name AS ConstraintName, kc.type_desc,
       i.name AS SupportingIndex, NULL AS ReferencedSchema, NULL AS ReferencedTable,
       NULL AS ConstraintDefinition, kc.is_disabled
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
LEFT JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
UNION ALL
SELECT N'FOREIGN_KEY', SCHEMA_NAME(pt.schema_id), pt.name, fk.name, N'FOREIGN_KEY',
       NULL, SCHEMA_NAME(rt.schema_id), rt.name,
       N'ON DELETE ' + fk.delete_referential_action_desc + N'; ON UPDATE ' + fk.update_referential_action_desc,
       fk.is_disabled
FROM sys.foreign_keys fk
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
UNION ALL
SELECT N'CHECK', SCHEMA_NAME(t.schema_id), t.name, ck.name, N'CHECK_CONSTRAINT',
       NULL, NULL, NULL, ck.definition, ck.is_disabled
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id
ORDER BY SchemaName, TableName, ConstraintType, ConstraintName;

-- 05. FK column mapping (including composite keys)
SELECT fk.name AS ForeignKeyName, SCHEMA_NAME(pt.schema_id) AS ParentSchema,
       pt.name AS ParentTable, pc.name AS ParentColumn,
       SCHEMA_NAME(rt.schema_id) AS ReferencedSchema, rt.name AS ReferencedTable,
       rc.name AS ReferencedColumn, fkc.constraint_column_id AS ColumnOrdinal,
       fk.is_disabled, fk.is_not_trusted
FROM sys.foreign_key_columns fkc
JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables pt ON pt.object_id = fkc.parent_object_id
JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY ParentSchema, ParentTable, ForeignKeyName, ColumnOrdinal;

-- 06. Indexes and indexed columns
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName, i.name AS IndexName,
       i.type_desc, i.is_primary_key, i.is_unique, i.is_unique_constraint, i.is_disabled,
       i.has_filter, i.filter_definition, ic.key_ordinal, ic.index_column_id,
       ic.is_included_column, c.name AS ColumnName, ic.is_descending_key
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
LEFT JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
LEFT JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE t.is_ms_shipped = 0 AND i.index_id > 0
ORDER BY SchemaName, TableName, IndexName, ic.key_ordinal, ic.index_column_id;

-- 07. Full definitions of views, procedures, functions and DML triggers
SELECT SCHEMA_NAME(o.schema_id) AS SchemaName, o.name AS ObjectName, o.type, o.type_desc,
       o.create_date, o.modify_date, OBJECTPROPERTYEX(o.object_id, 'IsEncrypted') AS IsEncrypted,
       sm.uses_ansi_nulls, sm.uses_quoted_identifier, sm.is_schema_bound,
       sm.definition AS SqlDefinition
FROM sys.objects o
LEFT JOIN sys.sql_modules sm ON sm.object_id = o.object_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('V','P','FN','IF','TF','TR')
ORDER BY SCHEMA_NAME(o.schema_id), o.type_desc, o.name;

-- 08. Parsed module dependencies
SELECT SCHEMA_NAME(o.schema_id) AS ReferencingSchema, o.name AS ReferencingObject,
       o.type_desc AS ReferencingType, d.referenced_server_name,
       d.referenced_database_name, d.referenced_schema_name, d.referenced_entity_name,
       d.referenced_id, d.is_schema_bound_reference, d.is_caller_dependent, d.is_ambiguous
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON o.object_id = d.referencing_id
WHERE o.is_ms_shipped = 0
ORDER BY ReferencingSchema, ReferencingObject, d.referenced_database_name,
         d.referenced_schema_name, d.referenced_entity_name;

-- 09. Database triggers
SELECT tr.name AS TriggerName, tr.type_desc, tr.parent_class_desc,
       OBJECT_SCHEMA_NAME(tr.parent_id) AS ParentSchema, OBJECT_NAME(tr.parent_id) AS ParentObject,
       tr.is_disabled, tr.is_instead_of_trigger, sm.definition AS SqlDefinition
FROM sys.triggers tr
LEFT JOIN sys.sql_modules sm ON sm.object_id = tr.object_id
WHERE tr.is_ms_shipped = 0
ORDER BY tr.parent_class_desc, ParentSchema, ParentObject, tr.name;

-- 10. Synonyms
SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS SynonymName, base_object_name AS BaseObjectName
FROM sys.synonyms ORDER BY SchemaName, SynonymName;

-- 11. Sequences
SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS SequenceName, TYPE_NAME(user_type_id) AS DataType,
       start_value, increment, minimum_value, maximum_value, is_cycling, is_cached, cache_size, current_value
FROM sys.sequences ORDER BY SchemaName, SequenceName;

-- 12. User-defined types and XML schema collections
SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TypeName, is_user_defined,
       is_table_type, is_assembly_type, TYPE_NAME(system_type_id) AS UnderlyingSystemType,
       max_length, precision, scale
FROM sys.types WHERE is_user_defined = 1 ORDER BY SchemaName, TypeName;

SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS XmlSchemaCollectionName
FROM sys.xml_schema_collections WHERE name <> N'sys' ORDER BY SchemaName, name;

-- 13. Approximate table row counts and allocated storage
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName,
       SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) AS ApproxRowCount,
       SUM(a.total_pages) * 8.0 / 1024 AS TotalMB, SUM(a.used_pages) * 8.0 / 1024 AS UsedMB
FROM sys.tables t
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units a ON a.container_id =
    CASE WHEN a.type IN (1,3) THEN p.hobt_id WHEN a.type = 2 THEN p.partition_id END
WHERE t.is_ms_shipped = 0
GROUP BY t.schema_id, t.name
ORDER BY SchemaName, TableName;

-- 14. Database principals, role membership and explicit permissions
SELECT dp.name AS PrincipalName, dp.type_desc, dp.authentication_type_desc,
       dp.default_schema_name, dp.create_date, dp.modify_date
FROM sys.database_principals dp
WHERE dp.principal_id > 4 AND dp.type <> 'R'
ORDER BY dp.type_desc, dp.name;

SELECT rp.name AS DatabaseRole, mp.name AS MemberName
FROM sys.database_role_members drm
JOIN sys.database_principals rp ON rp.principal_id = drm.role_principal_id
JOIN sys.database_principals mp ON mp.principal_id = drm.member_principal_id
ORDER BY rp.name, mp.name;

SELECT grantee.name AS Grantee, perm.state_desc, perm.permission_name, perm.class_desc,
       OBJECT_SCHEMA_NAME(perm.major_id) AS ObjectSchema, OBJECT_NAME(perm.major_id) AS ObjectName,
       minor.name AS ColumnName
FROM sys.database_permissions perm
JOIN sys.database_principals grantee ON grantee.principal_id = perm.grantee_principal_id
LEFT JOIN sys.columns minor ON minor.object_id = perm.major_id AND minor.column_id = perm.minor_id
ORDER BY Grantee, perm.class_desc, ObjectSchema, ObjectName, perm.permission_name;

-- 15. Extended properties / SQL metadata documentation
SELECT class_desc, major_id, minor_id, name AS PropertyName,
       CONVERT(nvarchar(4000), value) AS PropertyValue
FROM sys.extended_properties
ORDER BY class_desc, major_id, minor_id, name;

-- 16. Text scan for cross-database/server references (manual review required)
SELECT SCHEMA_NAME(o.schema_id) AS SchemaName, o.name AS ObjectName, o.type_desc,
       sm.definition AS SqlDefinition
FROM sys.objects o
JOIN sys.sql_modules sm ON sm.object_id = o.object_id
WHERE o.is_ms_shipped = 0 AND (
       sm.definition LIKE N'%].dbo.%'
    OR sm.definition LIKE N'%.dbo.%'
    OR sm.definition LIKE N'%OPENQUERY%'
    OR sm.definition LIKE N'%OPENDATASOURCE%'
    OR sm.definition LIKE N'%EXEC% AT %'
)
ORDER BY SchemaName, ObjectName;

-- 17. Modules with unavailable/encrypted definitions
SELECT SCHEMA_NAME(o.schema_id) AS SchemaName, o.name AS ObjectName, o.type_desc,
       OBJECTPROPERTYEX(o.object_id, 'IsEncrypted') AS IsEncrypted,
       CASE WHEN sm.definition IS NULL THEN N'Definition unavailable/encrypted/non-module'
            ELSE N'Definition available' END AS DefinitionStatus
FROM sys.objects o
LEFT JOIN sys.sql_modules sm ON sm.object_id = o.object_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('V','P','FN','IF','TF','TR')
ORDER BY SchemaName, ObjectName;

PRINT N'INVENTORY COMPLETE. Export every result set; do not send only screenshots.';
