/*
 Migration 042: component warehouse permission catalog.
 Adds explicit RBAC entries for supplier receipt, component stock,
 component demand, component issue and component traceability.
 Does not grant permissions to existing users/roles automatically.
 SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54200, 'Migration 042 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54201, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '041')
    THROW 54202, 'Migration 041 must be applied before 042.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '042')
    RETURN;
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NULL
    THROW 54203, 'SEC.FunctionCatalog is missing. Apply security migrations first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT SEC.FunctionCatalog(FunctionCode, ModuleCode, FunctionName, SortOrder)
    SELECT v.FunctionCode, v.ModuleCode, v.FunctionName, v.SortOrder
    FROM (VALUES
        ('WAREHOUSE.COMPONENT_RECEIPT', 'WAREHOUSE', N'Nhận linh kiện từ nhà cung cấp / QR', 25),
        ('WAREHOUSE.COMPONENT_STOCK', 'WAREHOUSE', N'Tồn kho linh kiện PART theo LOT / vị trí', 35),
        ('MES.COMPONENT_DEMAND', 'MES', N'Tạo và theo dõi nhu cầu linh kiện theo BOM', 85),
        ('REPORT.COMPONENT_TRACEABILITY', 'REPORT', N'Truy xuất LOT linh kiện và nguồn cung', 145)
    ) v(FunctionCode, ModuleCode, FunctionName, SortOrder)
    WHERE NOT EXISTS (
        SELECT 1 FROM SEC.FunctionCatalog f WHERE f.FunctionCode = v.FunctionCode
    );

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES('042', N'Add explicit component warehouse and demand RBAC function catalog entries');

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
    'WAREHOUSE.COMPONENT_RECEIPT',
    'WAREHOUSE.COMPONENT_STOCK',
    'MES.COMPONENT_DEMAND',
    'MES.COMPONENT_ISSUE',
    'REPORT.COMPONENT_TRACEABILITY'
)
ORDER BY SortOrder, FunctionCode;
GO
