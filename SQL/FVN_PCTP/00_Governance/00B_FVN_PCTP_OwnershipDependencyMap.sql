/*
    00B - FVN_PCTP Ownership & Dependency Map
    Source: B7R2_FCC
    SQL Server: 2016+
    Purpose:
      1) Produce a consolidated, READ-ONLY object ownership review matrix.
      2) Expose declared FK edges and static SQL module dependencies.
      3) Flag module definitions that mention legacy database names.
      4) Give conservative classification HINTS; never auto-approve migration.

    SAFETY:
      - Run ONLY while connected to B7R2_FCC.
      - This script performs SELECTs only. It creates no tables/temp tables and
        changes no schema, permissions, or business data.
      - ProposedOwnership is a review hint, NOT an approved ownership decision.
      - Do not generate/copy target DDL from this output until ReviewStatus is
        explicitly approved by the system owner and application owner.
      - Dynamic SQL, application-generated SQL, jobs, linked servers and external
        integrations may not appear in sys.sql_expression_dependencies.
*/
SET NOCOUNT ON;

DECLARE @CurrentDatabase sysname;
SET @CurrentDatabase = DB_NAME();

IF @CurrentDatabase <> N'B7R2_FCC'
BEGIN
    RAISERROR(N'Safety stop: 00B must run against B7R2_FCC. Current database: %s',
              16, 1, @CurrentDatabase);
    RETURN;
END;

PRINT N'00B OWNERSHIP & DEPENDENCY MAP - READ ONLY';
PRINT N'Source database: ' + @CurrentDatabase;
PRINT N'SQL version: ' + CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion'));

-------------------------------------------------------------------------------
-- RESULT 1: One row per user object, with conservative ownership hint.
-------------------------------------------------------------------------------
;WITH TableRows AS
(
    SELECT p.object_id,
           SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) AS ApproxRowCount
    FROM sys.partitions AS p
    GROUP BY p.object_id
),
FkCounts AS
(
    SELECT parent_object_id AS object_id, COUNT(*) AS OutgoingFkCount
    FROM sys.foreign_keys
    GROUP BY parent_object_id
),
ReferencedByCounts AS
(
    SELECT referenced_object_id AS object_id, COUNT(*) AS IncomingFkCount
    FROM sys.foreign_keys
    GROUP BY referenced_object_id
),
DependencyCounts AS
(
    SELECT d.referencing_id AS object_id, COUNT(*) AS StaticDependencyCount
    FROM sys.sql_expression_dependencies AS d
    GROUP BY d.referencing_id
)
SELECT
    DB_NAME() AS SourceDatabase,
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    CASE WHEN o.type = 'U' THEN tr.ApproxRowCount ELSE NULL END AS ApproxRowCount,
    COALESCE(fc.OutgoingFkCount, 0) AS OutgoingFkCount,
    COALESCE(rbc.IncomingFkCount, 0) AS IncomingFkCount,
    COALESCE(dc.StaticDependencyCount, 0) AS StaticDependencyCount,
    CASE
        WHEN o.name LIKE N'B00%' OR o.name LIKE N'B10%'
          OR o.name LIKE N'B20%' OR o.name LIKE N'B30%'
            THEN N'BRAVO7_OWNED_CANDIDATE'
        WHEN o.name IN (N'Warehouse', N'Rack', N'Slot')
            THEN N'SHARED_REFERENCE_CANDIDATE'
        WHEN o.name LIKE N'TMP%' OR o.name LIKE N'%TEST%'
          OR o.name LIKE N'test%' OR o.name LIKE N'_z%'
            THEN N'DERIVED_OR_TEST_CANDIDATE'
        WHEN o.name IN
        (
            N'PCTP_InternalLocation', N'PCTP_InternalStock',
            N'PCTP_InternalTransfer', N'PCTP_InternalReturn',
            N'PCTPStockMovementGuard', N'FVN_DeliveryCommandLedger',
            N'FVN_DeliveryCommandQr', N'FVN_HangChoGiao',
            N'FVN_HangChoGiaoPickDetail', N'FVN_ItemFifoConfig',
            N'FVN_ItemFifoConfig_History', N'FVN_PCTPQuarantineReleaseAudit',
            N'FVN_PhieuKhachTra', N'FVN_PhieuKhachTraItem',
            N'FVN_PhieuTraHang', N'FVN_PhieuTraHangCT',
            N'FVN_PhieuXuLyBatThuong', N'FVN_PhieuXuLyBatThuongAffectedLot',
            N'FVN_PhieuXuLyBatThuongQCInitial', N'FVN_PXLB_Compensation',
            N'FVN_PXLB_CompensationMovement', N'FVN_PXLB_Disposition',
            N'FVN_PXLB_ReworkMovement', N'FVN_PXLB_ReworkPlan',
            N'FVN_PXLB_ReworkQC', N'FVN_PXLB_WorkflowAudit',
            N'FVN_TraHangQTChung_Giao', N'FVN_TraHangQTChung_NhapNG',
            N'FVN_TraHangQTChung_QC', N'FVN_TraHangQTChung_Xuat',
            N'FVN_TraHangQTChungGiao', N'FVN_TraHangQTChungNhapNG',
            N'FVN_TraHangQTChungQC', N'FVN_TraHangQTChungXuat',
            N'FVN_sys_WorkflowTransitions'
        )
            THEN N'PCTP_OWNED_CANDIDATE'
        WHEN o.name LIKE N'FVN[_]%'
            THEN N'FVN_CUSTOM_SCOPE_REVIEW'
        WHEN o.name IN
        (
            N'SlotLot', N'STOCKTP', N'StockHistory', N'DOCQRCODE',
            N'DOCQRCODE_SP', N'SP_DOCQRCODE', N'YMVN_DOCQRCODE',
            N'LUUPHIEUGIAOHANG', N'IFSPHIEUGIAOHANG',
            N'IFSPHIEUGIAOHANG_SP', N'IFSPHIEUGIAOHANG_100002',
            N'IFSPHIEUGIAOHANG_100003'
        )
            THEN N'PCTP_LEGACY_SCOPE_REVIEW'
        WHEN o.name LIKE N'F03%'
            THEN N'FVN_PLATFORM_SCOPE_REVIEW'
        ELSE N'UNCLASSIFIED_REVIEW'
    END AS ProposedOwnership,
    CASE
        WHEN o.name LIKE N'B00%' OR o.name LIKE N'B10%'
          OR o.name LIKE N'B20%' OR o.name LIKE N'B30%'
            THEN N'B7R2_FCC'
        WHEN o.name IN (N'PCTP_InternalLocation', N'PCTP_InternalStock',
                        N'PCTP_InternalTransfer', N'PCTP_InternalReturn',
                        N'PCTPStockMovementGuard')
            THEN N'FVN_PCTP_CANDIDATE'
        ELSE N'UNDECIDED'
    END AS ProposedTarget,
    N'PENDING_REVIEW' AS ReviewStatus,
    CASE
        WHEN o.name LIKE N'B00%' OR o.name LIKE N'B10%'
          OR o.name LIKE N'B20%' OR o.name LIKE N'B30%'
            THEN N'BRAVO7 naming convention; confirm application write ownership'
        WHEN o.name IN (N'Warehouse', N'Rack', N'Slot')
            THEN N'Warehouse/Rack/Slot FK chain; confirm source-of-truth and write owner'
        WHEN o.name LIKE N'TMP%' OR o.name LIKE N'%TEST%'
          OR o.name LIKE N'test%' OR o.name LIKE N'_z%'
            THEN N'Temporary/test-like name; verify retention, replay and runtime use'
        WHEN o.name LIKE N'FVN[_]%'
            THEN N'FVN custom prefix; inspect feature owner, callers and SQL writes'
        WHEN o.name LIKE N'F03%'
            THEN N'FVN platform namespace; determine PCTP vs shared platform ownership'
        WHEN o.name IN (N'SlotLot', N'STOCKTP', N'StockHistory',
                        N'DOCQRCODE', N'DOCQRCODE_SP', N'SP_DOCQRCODE',
                        N'YMVN_DOCQRCODE', N'LUUPHIEUGIAOHANG',
                        N'IFSPHIEUGIAOHANG', N'IFSPHIEUGIAOHANG_SP')
            THEN N'Legacy PCTP candidate; trace actual writers/readers and BRAVO7 dependencies'
        ELSE N'No reliable ownership evidence from object name alone'
    END AS EvidenceNote
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN TableRows AS tr ON tr.object_id = o.object_id
LEFT JOIN FkCounts AS fc ON fc.object_id = o.object_id
LEFT JOIN ReferencedByCounts AS rbc ON rbc.object_id = o.object_id
LEFT JOIN DependencyCounts AS dc ON dc.object_id = o.object_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('U','V','P','FN','IF','TF','TR')
ORDER BY ProposedOwnership, s.name, o.type_desc, o.name;

-------------------------------------------------------------------------------
-- RESULT 2: Declared FK graph. Any edge crossing a proposed ownership boundary
-- must be resolved before a table is moved to another database.
-------------------------------------------------------------------------------
SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentObject,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedObject,
    rc.name AS ReferencedColumn,
    fk.name AS ForeignKeyName,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate,
    fk.is_disabled AS IsDisabled,
    fk.is_not_trusted AS IsNotTrusted,
    CASE
        WHEN pt.name LIKE N'B00%' OR pt.name LIKE N'B10%'
          OR pt.name LIKE N'B20%' OR pt.name LIKE N'B30%' THEN N'BRAVO7'
        WHEN pt.name LIKE N'FVN[_]%' OR pt.name LIKE N'PCTP%' THEN N'FVN_CUSTOM'
        ELSE N'REVIEW'
    END AS ParentScopeHint,
    CASE
        WHEN rt.name LIKE N'B00%' OR rt.name LIKE N'B10%'
          OR rt.name LIKE N'B20%' OR rt.name LIKE N'B30%' THEN N'BRAVO7'
        WHEN rt.name LIKE N'FVN[_]%' OR rt.name LIKE N'PCTP%' THEN N'FVN_CUSTOM'
        ELSE N'REVIEW'
    END AS ReferencedScopeHint,
    N'REVIEW_CROSS_DATABASE_IMPACT' AS ReviewAction
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
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

-------------------------------------------------------------------------------
-- RESULT 3: Static SQL dependencies, including external database/server names.
-- This catalog view does not discover all dynamic SQL or application SQL.
-------------------------------------------------------------------------------
SELECT
    OBJECT_SCHEMA_NAME(d.referencing_id) AS ReferencingSchema,
    OBJECT_NAME(d.referencing_id) AS ReferencingObject,
    o.type_desc AS ReferencingType,
    d.referenced_server_name AS ReferencedServer,
    d.referenced_database_name AS ReferencedDatabase,
    d.referenced_schema_name AS ReferencedSchema,
    d.referenced_entity_name AS ReferencedObject,
    d.is_schema_bound_reference AS IsSchemaBound,
    d.is_ambiguous AS IsAmbiguous,
    CASE
        WHEN d.referenced_database_name IS NOT NULL
         AND d.referenced_database_name <> DB_NAME()
            THEN N'EXTERNAL_DATABASE_DEPENDENCY'
        ELSE N'SAME_DATABASE_OR_UNRESOLVED'
    END AS DependencyRisk
FROM sys.sql_expression_dependencies AS d
JOIN sys.objects AS o ON o.object_id = d.referencing_id
WHERE o.is_ms_shipped = 0
ORDER BY ReferencingSchema, ReferencingObject,
         d.referenced_server_name, d.referenced_database_name,
         d.referenced_schema_name, d.referenced_entity_name;

-------------------------------------------------------------------------------
-- RESULT 4: Module text flags for hard-coded database names and dynamic SQL.
-- This is a triage aid, not a complete SQL parser.
-------------------------------------------------------------------------------
SELECT
    s.name AS SchemaName,
    o.name AS ModuleName,
    o.type_desc AS ModuleType,
    o.modify_date AS ModifiedAt,
    CASE WHEN m.definition LIKE N'%B7R2_FCC%' THEN 1 ELSE 0 END AS Mentions_B7R2_FCC,
    CASE WHEN m.definition LIKE N'%FVN_PCTP%' THEN 1 ELSE 0 END AS Mentions_FVN_PCTP,
    CASE WHEN m.definition LIKE N'%FVN_MES%' THEN 1 ELSE 0 END AS Mentions_FVN_MES,
    CASE WHEN m.definition LIKE N'%sp_executesql%'
               OR m.definition LIKE N'%EXEC(%'
               OR m.definition LIKE N'%EXECUTE(%'
         THEN 1 ELSE 0 END AS PossibleDynamicSql,
    CASE WHEN m.definition IS NULL THEN 1 ELSE 0 END AS DefinitionUnavailable
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules AS m ON m.object_id = o.object_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('P','V','FN','IF','TF','TR')
  AND
  (
      m.definition LIKE N'%B7R2_FCC%'
      OR m.definition LIKE N'%FVN_PCTP%'
      OR m.definition LIKE N'%FVN_MES%'
      OR m.definition LIKE N'%sp_executesql%'
      OR m.definition LIKE N'%EXEC(%'
      OR m.definition LIKE N'%EXECUTE(%'
      OR m.definition IS NULL
  )
ORDER BY s.name, o.name;

-------------------------------------------------------------------------------
-- RESULT 5: User-defined types. Table types and aliases must be recreated
-- before dependent procedures/tables in the target database.
-------------------------------------------------------------------------------
SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TypeName,
    t.is_user_defined,
    t.is_table_type,
    TYPE_NAME(t.system_type_id) AS SystemType,
    t.max_length,
    t.precision,
    t.scale
FROM sys.types AS t
WHERE t.is_user_defined = 1
ORDER BY SchemaName, TypeName;

PRINT N'00B completed. All results are review evidence only; no objects or rows changed.';
GO
