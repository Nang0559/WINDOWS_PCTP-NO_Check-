/*
 Migration 044: component warehouse (PART) workflow RBAC catalog entries.
 Adds explicit functions for the PART warehouse screen:
   WAREHOUSE.COMPONENT_PO        upload / validate / release / close / cancel component purchase orders
       VIEW    list orders and workflow history
       CREATE  upload Excel purchase order (creates or replaces a DRAFT order)
       CONFIRM validate order            (workflow 0  -> 10)
       APPROVE release order + issue QR LOT, close / short-close order (10 -> 20, 40/30 -> 50)
       CANCEL  cancel order before any receipt (0/10/20 -> 900)
   WAREHOUSE.COMPONENT_TRANSFER  move a component LOT between PART slots
       VIEW    open the transfer tab and see transfer history
       CONFIRM post the transfer
 Existing functions reused by the same screen (unchanged):
   WAREHOUSE.COMPONENT_STOCK       stock lookup by PartNo / LOT / Site / Slot (screen entry)
   WAREHOUSE.COMPONENT_RECEIPT     supplier receipt by QR
   MES.COMPONENT_DEMAND + MES.COMPONENT_ISSUE   issue PART -> WIP for production demand
   REPORT.COMPONENT_TRACEABILITY   LOT trace from component to finished goods
 Does not grant permissions to existing users/roles automatically; grant them in the
 security administration screen. SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54400, 'Migration 044 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54401, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '042')
    THROW 54402, 'Migration 042 must be applied before 044.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '043')
    THROW 54403, 'Migration 043 must be applied before 044.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '044')
    RETURN;
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NULL
    THROW 54404, 'SEC.FunctionCatalog is missing. Apply security migrations first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT SEC.FunctionCatalog(FunctionCode, ModuleCode, FunctionName, SortOrder)
    SELECT v.FunctionCode, v.ModuleCode, v.FunctionName, v.SortOrder
    FROM (VALUES
        ('WAREHOUSE.COMPONENT_PO', 'WAREHOUSE', N'Upload và theo dõi yêu cầu mua hàng linh kiện (PO)', 22),
        ('WAREHOUSE.COMPONENT_TRANSFER', 'WAREHOUSE', N'Điều chuyển LOT linh kiện PART giữa vị trí kho', 36)
    ) v(FunctionCode, ModuleCode, FunctionName, SortOrder)
    WHERE NOT EXISTS (
        SELECT 1 FROM SEC.FunctionCatalog f WHERE f.FunctionCode = v.FunctionCode
    );

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES('044', N'Add component PO workflow and component LOT transfer RBAC function catalog entries');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT FunctionCode, ModuleCode, FunctionName
FROM SEC.FunctionCatalog
WHERE FunctionCode IN (
    'WAREHOUSE.COMPONENT_PO',
    'WAREHOUSE.COMPONENT_RECEIPT',
    'WAREHOUSE.COMPONENT_STOCK',
    'WAREHOUSE.COMPONENT_TRANSFER',
    'MES.COMPONENT_DEMAND',
    'MES.COMPONENT_ISSUE',
    'REPORT.COMPONENT_TRACEABILITY'
)
ORDER BY SortOrder, FunctionCode;
GO