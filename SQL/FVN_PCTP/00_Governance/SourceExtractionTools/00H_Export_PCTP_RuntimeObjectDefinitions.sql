/*
    00H — PCTP runtime object definition export
    Source: B7R2_FCC
    Target design: FVN_PCTP
    SQL Server: 2016+

    SAFETY:
      - Run ONLY while connected to B7R2_FCC.
      - READ-ONLY metadata extraction. No DDL/DML, no temp objects, no source changes.
      - Export every result set to files and retain the result order.
      - This script is an evidence collector, NOT a migration script.
*/
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF DB_NAME() <> N'B7R2_FCC'
BEGIN
    ;THROW 51000, '00H must be run while connected to B7R2_FCC. No changes were made.', 1;
END;

DECLARE @TargetObjects TABLE
(
    ObjectName sysname NOT NULL PRIMARY KEY
);

INSERT INTO @TargetObjects (ObjectName)
VALUES
(N'B20Dept'),
(N'B20Gear'),
(N'B20ImageStore'),
(N'B20Item'),
(N'B20ItemQuyCach'),
(N'DOCQRCODE'),
(N'DOCQRCODE_100003'),
(N'DOCQRCODE_SP'),
(N'FVN_DeliveryCommandLedger'),
(N'FVN_DeliveryCommandQr'),
(N'FVN_HangChoGiao'),
(N'FVN_HangChoGiaoPickDetail'),
(N'FVN_ItemFifoConfig'),
(N'FVN_ItemFifoConfig_History'),
(N'FVN_PCTPQuarantineReleaseAudit'),
(N'FVN_PhieuTraHang'),
(N'FVN_PhieuTraHangCT'),
(N'FVN_PhieuXuLyBatThuong'),
(N'FVN_PhieuXuLyBatThuongAffectedLot'),
(N'FVN_PhieuXuLyBatThuongQCInitial'),
(N'FVN_Purchase_Order_HVNDB'),
(N'FVN_PXLB_Compensation'),
(N'FVN_PXLB_CompensationMovement'),
(N'FVN_PXLB_Disposition'),
(N'FVN_PXLB_ReworkMovement'),
(N'FVN_PXLB_ReworkPlan'),
(N'FVN_PXLB_ReworkQC'),
(N'FVN_PXLB_WorkflowAudit'),
(N'FVN_sys_WorkflowTransitions'),
(N'FVN_TraHangQTChungGiao'),
(N'FVN_TraHangQTChungNhapNG'),
(N'FVN_TraHangQTChungQC'),
(N'FVN_TraHangQTChungXuat'),
(N'InspectionConfig'),
(N'InspectionLog'),
(N'LUUDOCQRCODE'),
(N'LUUPHIEUGIAOHANG'),
(N'PURCHASE_ORDER_HTN'),
(N'PURCHASE_ORDER_YMVN'),
(N'QRCODE_CHANGETIME'),
(N'Rack'),
(N'Slot'),
(N'SlotLot'),
(N'SP_DOCQRCODE'),
(N'SP_TMPPHIEUGIAOHANG'),
(N'StockHistory'),
(N'STOCKTP'),
(N'tbl_QR_ComparePart'),
(N'tbl_QR_May_DocQR'),
(N'TMPLOTGHEP'),
(N'TMPLOTGHEPTest'),
(N'TMPLOTTACH'),
(N'FVN_Purchase_Order_DB_HD'),
(N'TMPPHIEUGIAOHANG'),
(N'TMPPHIEUGIAOHANG_100002'),
(N'TMPPHIEUGIAOHANG_100003'),
(N'TMPPHIEUGIAOHANG_SP'),
(N'FVN_Purchase_Order_DB_CT'),
(N'Warehouse'),
(N'YMVN_DOCQRCODE'),
(N'YMVN_TMPPHIEUGIAOHANG'),
(N'IFSPHIEUGIAOHANG'),
(N'IFSPHIEUGIAOHANG_SP'),
(N'IFSPHIEUGIAOHANG_100002'),
(N'IFSPHIEUGIAOHANG_100003'),
(N'IFSPHIEUGIAOHANGView'),
(N'PCTPStockMovementGuard'),
(N'TMPPHIEUGIAOHANGDB'),
(N'TMPPHIEUGIAOHANGDB_IFS'),
(N'TMPPHIEUGIAOHANGView_DK_PC21'),
(N'tbl_DocTem'),
(N'sys_WorkflowStatuses');

-------------------------------------------------------------------------------
-- RESULT 1 — Exact column contracts for PCTP tables and known runtime extras
-------------------------------------------------------------------------------
SELECT
    DB_NAME() AS SourceDatabase,
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnId,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLengthBytes,
    c.precision AS [Precision],
    c.scale AS [Scale],
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    ic.seed_value AS IdentitySeed,
    ic.increment_value AS IdentityIncrement,
    c.is_computed AS IsComputed,
    cc.definition AS ComputedDefinition,
    dc.definition AS DefaultDefinition,
    c.collation_name AS CollationName
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.columns AS c ON c.object_id = t.object_id
INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.identity_columns AS ic
    ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id AND cc.column_id = c.column_id
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE EXISTS
(
    SELECT 1 FROM @TargetObjects AS x
    WHERE x.ObjectName = t.name
)
ORDER BY s.name, t.name, c.column_id;

-------------------------------------------------------------------------------
-- RESULT 2 — Indexes, primary keys, unique constraints and included columns
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.index_id AS IndexId,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique_constraint AS IsUniqueConstraint,
    i.is_disabled AS IsDisabled,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    ic.key_ordinal AS KeyOrdinal,
    ic.is_descending_key AS IsDescendingKey,
    ic.is_included_column AS IsIncludedColumn,
    c.name AS ColumnName
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.index_id > 0
  AND i.is_hypothetical = 0
  AND EXISTS (SELECT 1 FROM @TargetObjects AS x WHERE x.ObjectName = t.name)
ORDER BY s.name, t.name, i.index_id, ic.key_ordinal, ic.index_column_id;

-------------------------------------------------------------------------------
-- RESULT 3 — Foreign keys (including external references)
-------------------------------------------------------------------------------
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
    fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
INNER JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
INNER JOIN sys.columns AS pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
INNER JOIN sys.columns AS rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
WHERE EXISTS (SELECT 1 FROM @TargetObjects AS x WHERE x.ObjectName = pt.name)
   OR EXISTS (SELECT 1 FROM @TargetObjects AS x WHERE x.ObjectName = rt.name)
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    chk.name AS CheckConstraintName,
    chk.definition AS CheckDefinition,
    chk.is_disabled AS IsDisabled,
    chk.is_not_trusted AS IsNotTrusted
FROM sys.check_constraints AS chk
INNER JOIN sys.tables AS t ON t.object_id = chk.parent_object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE EXISTS (SELECT 1 FROM @TargetObjects AS x WHERE x.ObjectName = t.name)
ORDER BY s.name, t.name, chk.name;

-------------------------------------------------------------------------------
-- RESULT 4 — CHECK constraints
-- RESULT 5 — Full SQL definitions for PCTP-linked views/procedures/functions/
--            and triggers. This is the full sys.sql_modules.definition, not the
--            200-character SQL2 metadata preview.
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type AS ObjectType,
    o.type_desc AS ObjectTypeDescription,
    m.uses_ansi_nulls AS UsesAnsiNulls,
    m.uses_quoted_identifier AS UsesQuotedIdentifier,
    m.is_schema_bound AS IsSchemaBound,
    m.definition AS FullSqlDefinition
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
INNER JOIN sys.sql_modules AS m ON m.object_id = o.object_id
WHERE o.type IN ('P', 'V', 'FN', 'IF', 'TF', 'TR')
  AND EXISTS
  (
      SELECT 1
      FROM @TargetObjects AS x
      WHERE CHARINDEX(x.ObjectName, m.definition) > 0
  )
ORDER BY o.type_desc, s.name, o.name;

-------------------------------------------------------------------------------
-- RESULT 6 — User-defined table types and their exact column contracts
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    tt.name AS TableTypeName,
    c.column_id AS ColumnId,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLengthBytes,
    c.precision AS [Precision],
    c.scale AS [Scale],
    c.is_nullable AS IsNullable
FROM sys.table_types AS tt
INNER JOIN sys.schemas AS s ON s.schema_id = tt.schema_id
INNER JOIN sys.columns AS c ON c.object_id = tt.type_table_object_id
INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE tt.name LIKE N'%SlotLot%'
   OR tt.name LIKE N'%QR%'
   OR tt.name LIKE N'%PCTP%'
ORDER BY s.name, tt.name, c.column_id;

-------------------------------------------------------------------------------
-- RESULT 7 — Triggers attached to PCTP target tables (full definitions)
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    tr.name AS TriggerName,
    tr.is_disabled AS IsDisabled,
    tr.is_instead_of_trigger AS IsInsteadOfTrigger,
    m.definition AS FullSqlDefinition
FROM sys.triggers AS tr
INNER JOIN sys.tables AS t ON t.object_id = tr.parent_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
LEFT JOIN sys.sql_modules AS m ON m.object_id = tr.object_id
WHERE EXISTS (SELECT 1 FROM @TargetObjects AS x WHERE x.ObjectName = t.name)
ORDER BY s.name, t.name, tr.name;

-------------------------------------------------------------------------------
-- RESULT 8 — Relevant synonyms (read-only inventory)
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    sy.name AS SynonymName,
    sy.base_object_name AS BaseObjectName
FROM sys.synonyms AS sy
INNER JOIN sys.schemas AS s ON s.schema_id = sy.schema_id
WHERE EXISTS
(
    SELECT 1 FROM @TargetObjects AS x
    WHERE sy.name = x.ObjectName
       OR sy.base_object_name LIKE N'%' + x.ObjectName + N'%'
)
ORDER BY s.name, sy.name;
