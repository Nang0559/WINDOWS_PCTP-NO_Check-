/*
 Migration 048: production order creation requires fulfilled component demand.
 Requires migration 047. The order-creation procedure remains the only write path.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 54800,'Migration 048 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 54801,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='047')
 THROW 54802,'Migration 047 must be applied before 048.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='048') RETURN;

BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'MES.tr_FVN_ProductionOrder_RequireFulfilledDemand',N'TR') IS NOT NULL
  DROP TRIGGER MES.tr_FVN_ProductionOrder_RequireFulfilledDemand;

 EXEC(N'
 CREATE TRIGGER MES.tr_FVN_ProductionOrder_RequireFulfilledDemand
 ON MES.FVN_ProductionOrder
 AFTER INSERT
 AS
 BEGIN
  SET NOCOUNT ON;

  IF EXISTS
  (
   SELECT 1
   FROM inserted i
   WHERE NOT EXISTS
   (
    SELECT 1 FROM MES.FVN_ComponentDemandHeader h
    WHERE h.ProductionPlanLineId=i.ProductionPlanLineId
      AND h.Status<>''CANCELLED''
   )
   OR EXISTS
   (
    SELECT 1
    FROM MES.FVN_ComponentDemandHeader h
    JOIN MES.FVN_ComponentDemandLine l ON l.ComponentDemandId=h.ComponentDemandId
    WHERE h.ProductionPlanLineId=i.ProductionPlanLineId
      AND h.Status<>''CANCELLED''
      AND l.NetRequiredQty>l.IssuedQty
   )
  )
   THROW 54810,''Production order cannot be created until its FIRM plan component demand is fully fulfilled.'',1;
 END');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('048',N'Require fulfilled BOM component demand before production order insert');

 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
