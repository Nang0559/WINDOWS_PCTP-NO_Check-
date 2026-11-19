/*
 Migration 046: enforce FIRM-plan prerequisite at the database boundary.
 The production order UI/service checks this before calling the order procedure;
 this trigger closes the direct-SQL/bypass path as well.
 SQL Server 2016 compatible. Apply after migrations 036 and 038.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54600, 'Migration 046 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54601, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '036')
    THROW 54602, 'Migration 036 must be applied before 046.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '038')
    THROW 54603, 'Migration 038 must be applied before 046.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '046')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'MES.tr_FVN_ProductionOrder_RequireFirmPlan', N'TR') IS NOT NULL
        DROP TRIGGER MES.tr_FVN_ProductionOrder_RequireFirmPlan;

    EXEC(N'
    CREATE TRIGGER MES.tr_FVN_ProductionOrder_RequireFirmPlan
    ON MES.FVN_ProductionOrder
    AFTER INSERT
    AS
    BEGIN
        SET NOCOUNT ON;

        IF EXISTS
        (
            SELECT 1
            FROM inserted i
            LEFT JOIN MES.FVN_ProductionPlanLine l
                ON l.ProductionPlanLineId = i.ProductionPlanLineId
            LEFT JOIN MES.FVN_ProductionPlan p
                ON p.ProductionPlanId = l.ProductionPlanId
            WHERE i.ProductionPlanLineId IS NULL
               OR l.ProductionPlanLineId IS NULL
               OR (ISNULL(p.WorkflowStatusCode, 0) <> 20
                   AND ISNULL(p.Status, '''') <> ''FIRM'')
        )
            THROW 54610, ''Production orders can only be created from a FIRM production plan line.'', 1;
    END');

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('046', N'Enforce FIRM plan prerequisite for production order creation');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
