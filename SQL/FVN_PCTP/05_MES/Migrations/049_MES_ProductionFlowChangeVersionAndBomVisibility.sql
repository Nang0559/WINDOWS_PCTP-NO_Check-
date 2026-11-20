/*
 Migration 049: correct BOM visibility and add database change-version signal.
 This is an ALTER migration because 047 may already be registered on DB57.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 54900,'Migration 049 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 54901,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='047')
 THROW 54902,'Migration 047 must be applied before 049.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='049') RETURN;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID(N'MES.FVN_ProductionFlowChangeVersion',N'U') IS NULL
 BEGIN
  CREATE TABLE MES.FVN_ProductionFlowChangeVersion
  (
   ChangeVersionId tinyint NOT NULL CONSTRAINT PK_FVN_ProductionFlowChangeVersion PRIMARY KEY,
   ChangeVersion bigint NOT NULL,
   ChangedAtUtc datetime2(3) NOT NULL,
   CONSTRAINT CK_FVN_ProductionFlowChangeVersion_Singleton CHECK(ChangeVersionId=1)
  );
  INSERT MES.FVN_ProductionFlowChangeVersion(ChangeVersionId,ChangeVersion,ChangedAtUtc)
  VALUES(1,1,SYSUTCDATETIME());
 END;
 EXEC(N'
 ALTER PROCEDURE MES.usp_FVN_ProductionFlow_Dashboard
  @SiteCode nvarchar(30),
  @FromDate date=NULL,
  @ToDate date=NULL,
  @DepartmentScopeXml xml
 AS
 BEGIN
  SET NOCOUNT ON;
  IF NULLIF(LTRIM(RTRIM(@SiteCode)),N'''') IS NULL
   THROW 54710,''SiteCode is required.'',1;
  IF @DepartmentScopeXml IS NULL
   THROW 54711,''Department scope is required.'',1;

  DECLARE @Scope TABLE(DepartmentCode nvarchar(4) NOT NULL PRIMARY KEY);
  INSERT @Scope(DepartmentCode)
  SELECT DISTINCT N.value(''.'',''nvarchar(4)'')
  FROM @DepartmentScopeXml.nodes(''/Scopes/Department'') X(N);

  IF NOT EXISTS(SELECT 1 FROM @Scope)
   THROW 54712,''No authorized department scope was supplied.'',1;

  ;WITH PlanFlow AS
  (
   SELECT l.ProductionPlanLineId,p.ProductionPlanId,p.SiteCode,p.PlanNo,
    ISNULL(l.ProductionDate,p.PlanDate) ProductionDate,l.ShiftCode,l.ProductionLineCode,
    map.DepartmentCode,l.ProductPartNo,ip.Description ProductDescription,
    l.PlannedQty,l.UomCode,p.Status PlanStatus,p.WorkflowStatusCode PlanWorkflowStatusCode,
    CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM @Scope s WHERE s.DepartmentCode=N''*'')
      THEN 0
      WHEN EXISTS(SELECT 1 FROM @Scope s WHERE s.DepartmentCode=map.DepartmentCode)
      THEN 0 ELSE 1 END) IsRelatedDepartment
   FROM MES.FVN_ProductionPlanLine l
   JOIN MES.FVN_ProductionPlan p ON p.ProductionPlanId=l.ProductionPlanId
   LEFT JOIN MES.FVN_ProductionLotCodeMap map
    ON map.SiteCode=l.SiteCode AND map.PlanProductionLineCode=l.ProductionLineCode AND map.IsActive=1
   LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=l.SiteCode AND ip.PartNo=l.ProductPartNo
   WHERE p.SiteCode=@SiteCode
    AND (@FromDate IS NULL OR ISNULL(l.ProductionDate,p.PlanDate)>=@FromDate)
    AND (@ToDate IS NULL OR ISNULL(l.ProductionDate,p.PlanDate)<=@ToDate)
    AND p.Status NOT IN(''CANCELLED'',''CLOSED'')
    AND
    (
     EXISTS(SELECT 1 FROM @Scope s WHERE s.DepartmentCode=N''*'')
     OR EXISTS(SELECT 1 FROM @Scope s WHERE s.DepartmentCode=map.DepartmentCode)
     OR EXISTS
      (SELECT 1 FROM MES.FVN_ProductionDepartmentRelation r
       JOIN @Scope s ON s.DepartmentCode=r.OwnerDepartmentCode
       WHERE r.SiteCode=p.SiteCode AND r.RelatedDepartmentCode=map.DepartmentCode
        AND r.IsActive=1
        AND (r.OwnerProductPartNo IS NULL OR EXISTS
         (SELECT 1
          FROM MES.FVN_ProductionPlanLine relationOwnerLine
          JOIN MES.FVN_ProductionPlan relationOwnerPlan
           ON relationOwnerPlan.ProductionPlanId=relationOwnerLine.ProductionPlanId
          JOIN MES.FVN_ProductionLotCodeMap relationOwnerMap
           ON relationOwnerMap.SiteCode=relationOwnerLine.SiteCode
           AND relationOwnerMap.PlanProductionLineCode=relationOwnerLine.ProductionLineCode
           AND relationOwnerMap.IsActive=1
          WHERE relationOwnerPlan.SiteCode=p.SiteCode
           AND relationOwnerMap.DepartmentCode=r.OwnerDepartmentCode
           AND relationOwnerLine.ProductPartNo=r.OwnerProductPartNo
           AND relationOwnerPlan.Status NOT IN(''CANCELLED'',''CLOSED'')))
        AND (r.RelatedProductPartNo IS NULL OR r.RelatedProductPartNo=l.ProductPartNo))
     OR EXISTS
      (
       SELECT 1
       FROM MES.FVN_ProductionPlanLine ownerLine
       JOIN MES.FVN_ProductionPlan ownerPlan
        ON ownerPlan.ProductionPlanId=ownerLine.ProductionPlanId
       JOIN MES.FVN_ProductionLotCodeMap ownerMap
        ON ownerMap.SiteCode=ownerLine.SiteCode
        AND ownerMap.PlanProductionLineCode=ownerLine.ProductionLineCode
        AND ownerMap.IsActive=1
       JOIN @Scope ownerScope ON ownerScope.DepartmentCode=ownerMap.DepartmentCode
       JOIN IFS.FVN_BomComponent component
        ON component.SiteCode=ownerLine.SiteCode
        AND component.PartNo=ownerLine.ProductPartNo
        AND component.ComponentPartNo=l.ProductPartNo
       WHERE ownerPlan.SiteCode=p.SiteCode
        AND
        (
         NOT EXISTS
          (SELECT 1 FROM MES.FVN_ComponentDemandHeader selectedDemand
           WHERE selectedDemand.ProductionPlanLineId=ownerLine.ProductionPlanLineId
            AND selectedDemand.Status<>N''CANCELLED'')
         OR EXISTS
          (SELECT 1 FROM MES.FVN_ComponentDemandHeader selectedDemand
           WHERE selectedDemand.ProductionPlanLineId=ownerLine.ProductionPlanLineId
            AND selectedDemand.Status<>N''CANCELLED''
            AND selectedDemand.BomEngChgLevel=component.EngChgLevel
            AND selectedDemand.BomTypeDb=component.BomTypeDb
            AND selectedDemand.AlternativeNo=component.AlternativeNo)
        )
        AND ownerPlan.Status NOT IN(''CANCELLED'',''CLOSED'')
        AND (@FromDate IS NULL OR ISNULL(ownerLine.ProductionDate,ownerPlan.PlanDate)>=@FromDate)
        AND (@ToDate IS NULL OR ISNULL(ownerLine.ProductionDate,ownerPlan.PlanDate)<=@ToDate)
        AND component.EffectiveInDate<=CAST(ISNULL(ownerLine.ProductionDate,ownerPlan.PlanDate) AS datetime2(0))
        AND (component.EffectiveOutDate IS NULL OR component.EffectiveOutDate>=CAST(ISNULL(ownerLine.ProductionDate,ownerPlan.PlanDate) AS datetime2(0)))
      )
    )
  )
  SELECT pf.ProductionPlanLineId,pf.ProductionPlanId,pf.SiteCode,pf.PlanNo,
   pf.ProductionDate,pf.ShiftCode,pf.ProductionLineCode,pf.DepartmentCode,
   pf.ProductPartNo,pf.ProductDescription,pf.PlannedQty,pf.UomCode,
   pf.PlanStatus,pf.PlanWorkflowStatusCode,pf.IsRelatedDepartment,
   dem.ComponentDemandId,dem.DemandStatus,ISNULL(dem.ComponentCount,0) ComponentCount,
   ISNULL(dem.RequiredQty,0) RequiredComponentQty,ISNULL(dem.IssuedQty,0) IssuedComponentQty,
   ISNULL(dem.OutstandingQty,0) OutstandingComponentQty,
   ord.ProductionOrderId,ord.OrderNo,ord.OrderStatus,ord.OrderWorkflowStatusCode,
   ISNULL(ord.OrderPlannedQty,0) OrderPlannedQty,ISNULL(ord.CompletedQty,0) CompletedQty,
   ISNULL(lot.ProductionLotCount,0) ProductionLotCount,ISNULL(lot.ProducedQty,0) ProducedQty,lot.LatestProductionLotId,
   ISNULL(op.OperationCount,0) OperationCount,ISNULL(op.CompletedOperationCount,0) CompletedOperationCount,
   ISNULL(op.PausedOperationCount,0) PausedOperationCount,
   evt.LastEventAtUtc,evt.LastEventAction,evt.LastEventActor,
   CASE
    WHEN pf.PlanWorkflowStatusCode<20 AND pf.PlanStatus<>N''FIRM'' THEN N''PLAN''
    WHEN dem.ComponentDemandId IS NULL OR dem.OutstandingQty>0 THEN N''COMPONENTS''
    WHEN ord.ProductionOrderId IS NULL OR ISNULL(lot.ProductionLotCount,0)=0 THEN N''ORDER''
    WHEN op.OperationCount>op.CompletedOperationCount THEN N''PRODUCTION''
    WHEN ord.OrderStatus IN(N''COMPLETED'',N''CLOSED'') OR ord.OrderWorkflowStatusCode IN(40,50) THEN N''DONE''
    ELSE N''PRODUCTION'' END FlowStage,
   CASE
    WHEN pf.PlanWorkflowStatusCode<20 AND pf.PlanStatus<>N''FIRM'' THEN N''Kế hoạch chưa FIRM''
    WHEN dem.ComponentDemandId IS NULL THEN N''Chưa có yêu cầu linh kiện''
    WHEN dem.OutstandingQty>0 THEN N''Còn thiếu linh kiện: ''+CONVERT(nvarchar(40),dem.OutstandingQty)
    WHEN ord.ProductionOrderId IS NULL THEN N''Chưa tạo lệnh sản xuất''
    WHEN ord.OrderStatus=N''CREATED'' THEN N''Lệnh đã tạo, đang chờ RELEASE''
    WHEN ISNULL(lot.ProductionLotCount,0)=0 THEN N''Lệnh đã RELEASE, đang chờ tạo LOT sản xuất''
    WHEN ord.OrderStatus=N''PAUSED'' OR op.PausedOperationCount>0 THEN N''Lệnh / công đoạn đang tạm dừng''
    WHEN op.OperationCount>op.CompletedOperationCount THEN N''Còn công đoạn chưa hoàn thành''
    ELSE N''Không có điểm chờ được suy ra'' END WaitingReason,
   CASE
    WHEN pf.PlanWorkflowStatusCode<20 AND pf.PlanStatus<>N''FIRM'' THEN N''PLANNING''
    WHEN dem.ComponentDemandId IS NULL OR dem.OutstandingQty>0 THEN N''PART''
    WHEN ord.ProductionOrderId IS NULL OR ord.OrderStatus=N''CREATED'' OR ISNULL(lot.ProductionLotCount,0)=0 THEN N''PRODUCTION''
    WHEN ord.OrderStatus=N''PAUSED'' OR op.PausedOperationCount>0 OR op.OperationCount>op.CompletedOperationCount THEN N''PRODUCTION''
    ELSE NULL END WaitingAt
  FROM PlanFlow pf
  OUTER APPLY
  (
   SELECT TOP(1) h.ComponentDemandId,h.Status DemandStatus,
    COUNT_BIG(l.ComponentDemandLineId) ComponentCount,
    SUM(l.RequiredQty) RequiredQty,SUM(l.IssuedQty) IssuedQty,
    SUM(CASE WHEN l.NetRequiredQty-l.IssuedQty>0 THEN l.NetRequiredQty-l.IssuedQty ELSE 0 END) OutstandingQty
   FROM MES.FVN_ComponentDemandHeader h
   LEFT JOIN MES.FVN_ComponentDemandLine l ON l.ComponentDemandId=h.ComponentDemandId
   WHERE h.ProductionPlanLineId=pf.ProductionPlanLineId
    AND h.Status<>N''CANCELLED''
   GROUP BY h.ComponentDemandId,h.Status
  ) dem
  OUTER APPLY
  (
   SELECT TOP(1) o.ProductionOrderId,o.OrderNo,o.Status OrderStatus,o.WorkflowStatusCode OrderWorkflowStatusCode,
    o.PlannedQty OrderPlannedQty,o.CompletedQty
   FROM MES.FVN_ProductionOrder o
   WHERE o.ProductionPlanLineId=pf.ProductionPlanLineId
   ORDER BY o.ProductionOrderId DESC
  ) ord
  OUTER APPLY
  (
   SELECT COUNT_BIG(*) ProductionLotCount,SUM(ISNULL(pl.FinalGoodQty,pl.GoodQty)) ProducedQty,MAX(pl.ProductionLotId) LatestProductionLotId
   FROM MES.FVN_ProductionLot pl WHERE pl.ProductionOrderId=ord.ProductionOrderId
  ) lot
  OUTER APPLY
  (
   SELECT COUNT_BIG(*) OperationCount,
    SUM(CASE WHEN w.StatusCode=50 THEN CONVERT(bigint,1) ELSE CONVERT(bigint,0) END) CompletedOperationCount,
    SUM(CASE WHEN w.StatusCode=40 THEN CONVERT(bigint,1) ELSE CONVERT(bigint,0) END) PausedOperationCount
   FROM MES.FVN_ProductionLot pl
   JOIN MES.FVN_ProductionOperationWorkflow w ON w.ProductionLotId=pl.ProductionLotId
   WHERE pl.ProductionOrderId=ord.ProductionOrderId
  ) op
  OUTER APPLY
  (
   SELECT TOP(1) e.OccurredAtUtc LastEventAtUtc,e.ActionName LastEventAction,e.ActorCode LastEventActor
   FROM MES.FVN_ProductionWorkflowEvent e
   WHERE (e.EntityType=''PLAN'' AND e.EntityId=pf.ProductionPlanId)
      OR (ord.ProductionOrderId IS NOT NULL AND e.EntityType=''ORDER'' AND e.EntityId=ord.ProductionOrderId)
   ORDER BY e.OccurredAtUtc DESC,e.WorkflowEventId DESC
  ) evt
  ORDER BY pf.ProductionDate,pf.ShiftCode,pf.DepartmentCode,pf.PlanNo,pf.ProductPartNo;
 END')
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Plan','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Plan;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Plan ON MES.FVN_ProductionPlan AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_PlanLine','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_PlanLine;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_PlanLine ON MES.FVN_ProductionPlanLine AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Demand','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Demand;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Demand ON MES.FVN_ComponentDemandHeader AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_DemandLine','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_DemandLine;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_DemandLine ON MES.FVN_ComponentDemandLine AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Order','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Order;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Order ON MES.FVN_ProductionOrder AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Lot','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Lot;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Lot ON MES.FVN_ProductionLot AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Operation','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Operation;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Operation ON MES.FVN_ProductionOperationWorkflow AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_LotCodeMap','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_LotCodeMap;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_LotCodeMap ON MES.FVN_ProductionLotCodeMap AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Bom','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Bom;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Bom ON IFS.FVN_BomComponent AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_DepartmentRelation','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_DepartmentRelation;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_DepartmentRelation ON MES.FVN_ProductionDepartmentRelation AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 IF OBJECT_ID(N'MES.tr_FVN_FlowVersion_Event','TR') IS NOT NULL DROP TRIGGER MES.tr_FVN_FlowVersion_Event;
 EXEC(N'CREATE TRIGGER MES.tr_FVN_FlowVersion_Event ON MES.FVN_ProductionWorkflowEvent AFTER INSERT,UPDATE,DELETE AS BEGIN SET NOCOUNT ON; UPDATE MES.FVN_ProductionFlowChangeVersion SET ChangeVersion=ChangeVersion+1,ChangedAtUtc=SYSUTCDATETIME() WHERE ChangeVersionId=1; END');
 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('049',N'Fix MES BOM flow visibility and add database change version polling');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
