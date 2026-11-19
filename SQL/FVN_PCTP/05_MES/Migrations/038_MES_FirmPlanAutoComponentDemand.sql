/*
 Migration 038: FIRM production plans automatically generate component demand.
 Requires MES migrations 033 and 036.
 SQL Server 2016 compatible. The demand creation runs inside the same transaction
 as the plan status change, so a missing/ambiguous IFS BOM rolls back FIRM.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 53800, 'Migration 038 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 53801, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '033')
    THROW 53802, 'Migration 033 must be applied before 038.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '036')
    THROW 53803, 'Migration 036 must be applied before 038.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '038')
    RETURN;
IF OBJECT_ID(N'MES.usp_FVN_GenerateComponentDemand', N'P') IS NULL
    THROW 53804, 'Base component-demand generator from migration 033 is missing.', 1;
IF OBJECT_ID(N'MES.FVN_ComponentDemandHeader', N'U') IS NULL
    THROW 53805, 'Component-demand tables are missing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /*
      Idempotent entry point used by the plan-FIRM trigger and the UI.
      Lock the plan line before checking the unique demand key so two concurrent
      transitions/retries cannot create duplicate demand documents.
    */
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_GenerateComponentDemandV2
        @ProductionPlanLineId bigint,
        @CreatedBy nvarchar(100)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @ProductionPlanLineId <= 0
            THROW 53810, ''ProductionPlanLineId must be positive.'', 1;
        IF NULLIF(LTRIM(RTRIM(@CreatedBy)), N'''') IS NULL
            THROW 53811, ''CreatedBy is required.'', 1;

        DECLARE @DemandId bigint, @PlanStatus varchar(20), @WorkflowStatusCode int;

        BEGIN TRY
            BEGIN TRANSACTION;

            SELECT @PlanStatus = p.Status,
                   @WorkflowStatusCode = p.WorkflowStatusCode
            FROM MES.FVN_ProductionPlanLine l WITH (UPDLOCK, HOLDLOCK)
            JOIN MES.FVN_ProductionPlan p WITH (UPDLOCK, HOLDLOCK)
              ON p.ProductionPlanId = l.ProductionPlanId
            WHERE l.ProductionPlanLineId = @ProductionPlanLineId;

            IF @PlanStatus IS NULL
                THROW 53812, ''Production plan line was not found.'', 1;
            IF ISNULL(@WorkflowStatusCode, 0) <> 20 AND @PlanStatus <> ''FIRM''
                THROW 53813, ''Component demand is generated only after the production plan becomes FIRM.'', 1;

            SELECT @DemandId = ComponentDemandId
            FROM MES.FVN_ComponentDemandHeader WITH (UPDLOCK, HOLDLOCK)
            WHERE ProductionPlanLineId = @ProductionPlanLineId;

            IF @DemandId IS NULL
            BEGIN
                EXEC MES.usp_FVN_GenerateComponentDemand
                    @ProductionPlanLineId = @ProductionPlanLineId,
                    @CreatedBy = @CreatedBy;

                SELECT @DemandId = ComponentDemandId
                FROM MES.FVN_ComponentDemandHeader
                WHERE ProductionPlanLineId = @ProductionPlanLineId;
            END;

            IF @DemandId IS NULL
                THROW 53814, ''Component demand generator returned without creating a demand header.'', 1;

            COMMIT TRANSACTION;

            SELECT @DemandId AS ComponentDemandId,
                   COUNT(*) AS ComponentCount
            FROM MES.FVN_ComponentDemandLine
            WHERE ComponentDemandId = @DemandId;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END');

    /*
      A plan entering FIRM creates one BOM demand per plan line.
      This trigger participates in the caller transaction; any BOM resolution
      error aborts the status transition instead of leaving a FIRM plan without
      component requirements.
    */
    EXEC(N'
    CREATE TRIGGER MES.tr_FVN_ProductionPlan_GenerateComponentDemand
    ON MES.FVN_ProductionPlan
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;

        DECLARE @PlanId bigint, @Actor nvarchar(100), @LineId bigint;

        DECLARE firm_plans CURSOR LOCAL FAST_FORWARD FOR
            SELECT i.ProductionPlanId, COALESCE(NULLIF(i.CreatedBy, N''''), N''MES'')
            FROM inserted i
            LEFT JOIN deleted d ON d.ProductionPlanId = i.ProductionPlanId
            WHERE (i.Status = ''FIRM'' OR i.WorkflowStatusCode = 20)
              AND (ISNULL(d.WorkflowStatusCode, 0) <> 20 OR ISNULL(d.Status, '''') <> ''FIRM'');

        OPEN firm_plans;
        FETCH NEXT FROM firm_plans INTO @PlanId, @Actor;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE plan_lines CURSOR LOCAL FAST_FORWARD FOR
                SELECT ProductionPlanLineId
                FROM MES.FVN_ProductionPlanLine
                WHERE ProductionPlanId = @PlanId
                ORDER BY [LineNo], ProductionPlanLineId;

            OPEN plan_lines;
            FETCH NEXT FROM plan_lines INTO @LineId;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                EXEC MES.usp_FVN_GenerateComponentDemandV2
                    @ProductionPlanLineId = @LineId,
                    @CreatedBy = @Actor;
                FETCH NEXT FROM plan_lines INTO @LineId;
            END;
            CLOSE plan_lines;
            DEALLOCATE plan_lines;

            FETCH NEXT FROM firm_plans INTO @PlanId, @Actor;
        END;
        CLOSE firm_plans;
        DEALLOCATE firm_plans;
    END');

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('038', N'FIRM plan automatically generates idempotent IFS BOM component demand');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
