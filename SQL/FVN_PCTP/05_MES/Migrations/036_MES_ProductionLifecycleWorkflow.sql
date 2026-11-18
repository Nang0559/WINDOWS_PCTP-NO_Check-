/*
 Migration 036: production lifecycle workflow (plan/order/operation), audit and timeline.
 SQL Server 2016 compatible. Reuses dbo.FVN_sys_WorkflowTransitions and the canonical MES tables.
 Apply after 035. This migration does not post inventory or create a second stock ledger.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53600,'Migration 036 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53601,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='035')
 THROW 53602,'Migration 035 must be applied before 036.',1;
IF OBJECT_ID(N'dbo.FVN_sys_WorkflowTransitions',N'U') IS NULL
 THROW 53603,'Shared workflow transition table is required.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='036') RETURN;
BEGIN TRY
 BEGIN TRANSACTION;

 IF COL_LENGTH(N'MES.FVN_ProductionPlan',N'WorkflowStatusCode') IS NULL
  ALTER TABLE MES.FVN_ProductionPlan ADD WorkflowStatusCode int NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'WorkflowStatusCode') IS NULL
  ALTER TABLE MES.FVN_ProductionOrder ADD WorkflowStatusCode int NULL;

 UPDATE MES.FVN_ProductionPlan SET WorkflowStatusCode=
  CASE Status WHEN 'IMPORTED' THEN 0 WHEN 'VALIDATED' THEN 10 WHEN 'FIRM' THEN 20
   WHEN 'CANCELLED' THEN 900 ELSE 0 END
 WHERE WorkflowStatusCode IS NULL;
 UPDATE MES.FVN_ProductionOrder SET WorkflowStatusCode=
  CASE Status WHEN 'CREATED' THEN 0 WHEN 'RELEASED' THEN 10 WHEN 'RUNNING' THEN 20
   WHEN 'PAUSED' THEN 30 WHEN 'COMPLETED' THEN 40 WHEN 'CLOSED' THEN 50
   WHEN 'CANCELLED' THEN 900 ELSE 0 END
 WHERE WorkflowStatusCode IS NULL;

 IF OBJECT_ID(N'MES.FVN_ProductionOperationWorkflow',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionOperationWorkflow(
  ProductionLotId bigint NOT NULL, RoutingStepId bigint NOT NULL,
  StatusCode int NOT NULL CONSTRAINT DF_FVN_ProductionOperationWorkflow_Status DEFAULT(0),
  UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionOperationWorkflow_Updated DEFAULT(SYSUTCDATETIME()),
  UpdatedBy nvarchar(100) NULL, RowVersion rowversion NOT NULL,
  CONSTRAINT PK_FVN_ProductionOperationWorkflow PRIMARY KEY(ProductionLotId,RoutingStepId),
  CONSTRAINT FK_FVN_ProductionOperationWorkflow_Lot FOREIGN KEY(ProductionLotId) REFERENCES MES.FVN_ProductionLot(ProductionLotId),
  CONSTRAINT FK_FVN_ProductionOperationWorkflow_Step FOREIGN KEY(RoutingStepId) REFERENCES MES.FVN_ProductionRoutingStep(RoutingStepId),
  CONSTRAINT CK_FVN_ProductionOperationWorkflow_Status CHECK(StatusCode IN(0,10,20,30,40,50,900))
 );

 INSERT MES.FVN_ProductionOperationWorkflow(ProductionLotId,RoutingStepId,StatusCode)
 SELECT pl.ProductionLotId,rs.RoutingStepId,
  CASE WHEN ex.OperationExecutionId IS NOT NULL THEN 50
       WHEN rs.SequenceNo=1 THEN 0 ELSE 0 END
 FROM MES.FVN_ProductionLot pl
 JOIN MES.FVN_ProductionOrder po ON po.ProductionOrderId=pl.ProductionOrderId
 JOIN MES.FVN_ProductionRoutingStep rs ON rs.RoutingId=po.RoutingId AND rs.IsActive=1
 LEFT JOIN MES.FVN_ProductionOperationExecution ex
  ON ex.ProductionLotId=pl.ProductionLotId AND ex.RoutingStepId=rs.RoutingStepId
 WHERE NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationWorkflow w
  WHERE w.ProductionLotId=pl.ProductionLotId AND w.RoutingStepId=rs.RoutingStepId);

 IF OBJECT_ID(N'MES.FVN_ProductionWorkflowEvent',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionWorkflowEvent(
  WorkflowEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionWorkflowEvent PRIMARY KEY,
  EntityType varchar(20) NOT NULL, EntityId bigint NOT NULL, RelatedId bigint NULL,
  ProcessCode varchar(40) NOT NULL, FromStatus int NOT NULL, ToStatus int NOT NULL,
  ActionName nvarchar(80) NOT NULL, Reason nvarchar(500) NULL,
  ActorCode nvarchar(100) NOT NULL, OccurredAtUtc datetime2(3) NOT NULL
   CONSTRAINT DF_FVN_ProductionWorkflowEvent_Date DEFAULT(SYSUTCDATETIME()),
  CorrelationId uniqueidentifier NULL,
  CONSTRAINT CK_FVN_ProductionWorkflowEvent_Entity CHECK(EntityType IN('PLAN','ORDER','OPERATION'))
 );
 CREATE INDEX IX_FVN_ProductionWorkflowEvent_Entity
  ON MES.FVN_ProductionWorkflowEvent(EntityType,EntityId,OccurredAtUtc,WorkflowEventId);

 IF OBJECT_ID(N'MES.FVN_ProductionOperationEvent',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionOperationEvent(
  OperationEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionOperationEvent PRIMARY KEY,
  ProductionLotId bigint NOT NULL, RoutingStepId bigint NOT NULL,
  EventType varchar(30) NOT NULL, Quantity decimal(19,6) NULL,
  ReferenceId bigint NULL, Remarks nvarchar(500) NULL,
  ActorCode nvarchar(100) NOT NULL,
  OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionOperationEvent_Date DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT FK_FVN_ProductionOperationEvent_Lot FOREIGN KEY(ProductionLotId) REFERENCES MES.FVN_ProductionLot(ProductionLotId),
  CONSTRAINT FK_FVN_ProductionOperationEvent_Step FOREIGN KEY(RoutingStepId) REFERENCES MES.FVN_ProductionRoutingStep(RoutingStepId)
 );
 CREATE INDEX IX_FVN_ProductionOperationEvent_Lot
  ON MES.FVN_ProductionOperationEvent(ProductionLotId,RoutingStepId,OccurredAtUtc);

 /* State codes are stable API values; labels remain configurable in shared transition data. */
 DECLARE @Seed TABLE(ProcessCode varchar(40),FromStatus int,ToStatus int,ActionName nvarchar(80),Description nvarchar(250));
 INSERT @Seed VALUES
 ('MES_PRODUCTION_PLAN',0,10,'VALIDATE','Validate plan lines, dates, site, shift and effective product data'),
 ('MES_PRODUCTION_PLAN',10,20,'FIRM','Snapshot effective BOM/routing and generate material demand'),
 ('MES_PRODUCTION_PLAN',0,900,'CANCEL','Cancel imported plan'),
 ('MES_PRODUCTION_PLAN',10,900,'CANCEL','Cancel validated plan'),
 ('MES_PRODUCTION_ORDER',0,10,'RELEASE','Release a firm production order'),
 ('MES_PRODUCTION_ORDER',10,20,'START','Start production order'),
 ('MES_PRODUCTION_ORDER',20,30,'PAUSE','Pause production order'),
 ('MES_PRODUCTION_ORDER',30,20,'RESUME','Resume production order'),
 ('MES_PRODUCTION_ORDER',20,40,'COMPLETE','Complete after all operations and quantities reconcile'),
 ('MES_PRODUCTION_ORDER',40,50,'CLOSE','Close after material, label, NG and receipt reconciliation'),
 ('MES_PRODUCTION_ORDER',0,900,'CANCEL','Cancel order before execution'),
 ('MES_PRODUCTION_ORDER',10,900,'CANCEL','Cancel released order before execution'),
 ('MES_PRODUCTION_OPERATION',0,10,'PREPARE','Begin operation preparation'),
 ('MES_PRODUCTION_OPERATION',10,20,'READY','Materials and labels are prepared'),
 ('MES_PRODUCTION_OPERATION',20,30,'START','Start operation'),
 ('MES_PRODUCTION_OPERATION',30,40,'PAUSE','Pause operation'),
 ('MES_PRODUCTION_OPERATION',40,30,'RESUME','Resume operation'),
 ('MES_PRODUCTION_OPERATION',30,50,'COMPLETE','Complete after actual production and quality reconciliation'),
 ('MES_PRODUCTION_OPERATION',0,900,'CANCEL','Cancel operation before execution'),
 ('MES_PRODUCTION_OPERATION',10,900,'CANCEL','Cancel operation before execution'),
 ('MES_PRODUCTION_OPERATION',20,900,'CANCEL','Cancel operation before execution');
 INSERT dbo.FVN_sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
 SELECT s.ProcessCode,s.FromStatus,s.ToStatus,s.ActionName,s.Description,1
 FROM @Seed s
 WHERE NOT EXISTS(SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions t
  WHERE t.ProcessCode=s.ProcessCode AND t.FromStatus=s.FromStatus AND t.ToStatus=s.ToStatus AND t.IsActive=1);

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionWorkflow_Transition
  @EntityType varchar(20),@EntityId bigint,@RelatedId bigint=NULL,
  @FromStatus int,@ToStatus int,@ActorCode nvarchar(100),@Reason nvarchar(500)=NULL,
  @CorrelationId uniqueidentifier=NULL
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @EntityType NOT IN(''PLAN'',''ORDER'',''OPERATION'') THROW 53610,''Unsupported workflow entity type.'',1;
  IF @EntityId<=0 OR NULLIF(LTRIM(RTRIM(@ActorCode)),N'''') IS NULL THROW 53611,''Entity and actor are required.'',1;
  DECLARE @ProcessCode varchar(40)=CASE @EntityType WHEN ''PLAN'' THEN ''MES_PRODUCTION_PLAN''
   WHEN ''ORDER'' THEN ''MES_PRODUCTION_ORDER'' ELSE ''MES_PRODUCTION_OPERATION'' END;
  IF NOT EXISTS(SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions
   WHERE ProcessCode=@ProcessCode AND FromStatus=@FromStatus AND ToStatus=@ToStatus AND IsActive=1)
   THROW 53612,''Transition is not configured in the shared workflow table.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   IF @EntityType=''PLAN''
   BEGIN
    IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionPlan WITH(UPDLOCK,HOLDLOCK)
      WHERE ProductionPlanId=@EntityId AND ISNULL(WorkflowStatusCode,0)=@FromStatus)
     THROW 53613,''Plan not found or current state has changed.'',1;
    IF @ToStatus=10 AND NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionPlanLine WHERE ProductionPlanId=@EntityId)
     THROW 53614,''Cannot validate an empty production plan.'',1;
    IF @ToStatus=20
    BEGIN
     IF EXISTS(SELECT 1 FROM MES.FVN_ProductionPlanLine l
       WHERE l.ProductionPlanId=@EntityId AND
        (NULLIF(l.SiteCode,N'''') IS NULL OR NULLIF(l.ProductPartNo,N'''') IS NULL OR l.PlannedQty<=0))
      THROW 53615,''Plan contains invalid line data.'',1;
     IF EXISTS(SELECT 1 FROM MES.FVN_ProductionPlanLine l
       WHERE l.ProductionPlanId=@EntityId AND NOT EXISTS(
        SELECT 1 FROM MES.FVN_ComponentDemandHeader d WHERE d.ProductionPlanLineId=l.ProductionPlanLineId))
      THROW 53616,''Component demand must be generated for every plan line before FIRM.'',1;
    END;
    UPDATE MES.FVN_ProductionPlan SET WorkflowStatusCode=@ToStatus,
     Status=CASE @ToStatus WHEN 10 THEN ''VALIDATED'' WHEN 20 THEN ''FIRM'' WHEN 900 THEN ''CANCELLED'' ELSE Status END
    WHERE ProductionPlanId=@EntityId;
   END
   ELSE IF @EntityType=''ORDER''
   BEGIN
    IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOrder WITH(UPDLOCK,HOLDLOCK)
      WHERE ProductionOrderId=@EntityId AND ISNULL(WorkflowStatusCode,CASE Status WHEN ''CREATED'' THEN 0 WHEN ''RELEASED'' THEN 10 WHEN ''RUNNING'' THEN 20 ELSE 0 END)=@FromStatus)
     THROW 53617,''Order not found or current state has changed.'',1;
    IF @ToStatus=40 AND EXISTS(SELECT 1 FROM MES.FVN_ProductionLot pl
      JOIN MES.FVN_ProductionRoutingStep rs ON rs.RoutingId=(SELECT RoutingId FROM MES.FVN_ProductionOrder WHERE ProductionOrderId=@EntityId)
      WHERE pl.ProductionOrderId=@EntityId AND NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationExecution ex
       WHERE ex.ProductionLotId=pl.ProductionLotId AND ex.RoutingStepId=rs.RoutingStepId AND rs.IsActive=1))
     THROW 53618,''Cannot complete order while a production LOT has unfinished routing operations.'',1;
    IF @ToStatus=50 AND EXISTS(SELECT 1 FROM MES.FVN_ProductionLabel l
      JOIN MES.FVN_ProductionLot pl ON pl.ProductionLotId=l.ProductionLotId
      WHERE pl.ProductionOrderId=@EntityId AND l.Status IN(''RESERVED'',''PRINTED''))
     THROW 53619,''Void or apply every unused printed/reserved label before closing the order.'',1;
    UPDATE MES.FVN_ProductionOrder SET WorkflowStatusCode=@ToStatus,
     Status=CASE @ToStatus WHEN 10 THEN ''RELEASED'' WHEN 20 THEN ''RUNNING'' WHEN 30 THEN ''PAUSED''
      WHEN 40 THEN ''COMPLETED'' WHEN 50 THEN ''CLOSED'' WHEN 900 THEN ''CANCELLED'' ELSE Status END
    WHERE ProductionOrderId=@EntityId;
   END
   ELSE
   BEGIN
    IF @RelatedId IS NULL THROW 53620,''RoutingStepId is required for operation workflow.'',1;
    IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationWorkflow WITH(UPDLOCK,HOLDLOCK)
      WHERE ProductionLotId=@EntityId AND RoutingStepId=@RelatedId AND StatusCode=@FromStatus)
     THROW 53621,''Operation not found or current state has changed.'',1;
    IF @ToStatus=20
    BEGIN
     IF EXISTS(SELECT 1 FROM MES.FVN_ProductionRoutingStep rs
       JOIN MES.FVN_ProductionOrder po ON po.RoutingId=rs.RoutingId
       JOIN MES.FVN_ProductionOperationWorkflow w ON w.ProductionLotId=@EntityId AND w.RoutingStepId=rs.RoutingStepId
       WHERE po.ProductionOrderId=(SELECT ProductionOrderId FROM MES.FVN_ProductionLot WHERE ProductionLotId=@EntityId)
        AND rs.SequenceNo<(SELECT SequenceNo FROM MES.FVN_ProductionRoutingStep WHERE RoutingStepId=@RelatedId)
        AND w.StatusCode<>50)
      THROW 53622,''Previous operation is not complete.'',1;
     IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionLabel WHERE ProductionLotId=@EntityId
       AND RoutingStepId=@RelatedId AND Status IN(''RESERVED'',''PRINTED'',''APPLIED''))
      THROW 53623,''Prepare per-unit labels for this operation before READY.'',1;
    END;
    IF @ToStatus=50 AND NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationExecution
      WHERE ProductionLotId=@EntityId AND RoutingStepId=@RelatedId)
      THROW 53624,''Operation completion requires a recorded actual execution.'',1;
    UPDATE MES.FVN_ProductionOperationWorkflow SET StatusCode=@ToStatus,
     UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ActorCode
    WHERE ProductionLotId=@EntityId AND RoutingStepId=@RelatedId;
   END;
   INSERT MES.FVN_ProductionWorkflowEvent(EntityType,EntityId,RelatedId,ProcessCode,
    FromStatus,ToStatus,ActionName,Reason,ActorCode,CorrelationId)
   SELECT @EntityType,@EntityId,@RelatedId,@ProcessCode,@FromStatus,@ToStatus,
    ActionName,@Reason,@ActorCode,@CorrelationId
   FROM dbo.FVN_sys_WorkflowTransitions
   WHERE ProcessCode=@ProcessCode AND FromStatus=@FromStatus AND ToStatus=@ToStatus AND IsActive=1;
   COMMIT;
   SELECT @EntityType EntityType,@EntityId EntityId,@RelatedId RelatedId,
    @FromStatus FromStatus,@ToStatus ToStatus,@ProcessCode ProcessCode;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');
 
 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionWorkflow_Timeline
  @EntityType varchar(20),@EntityId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  ;WITH Timeline AS(
   SELECT e.WorkflowEventId,e.EntityType,e.EntityId,e.RelatedId,e.ProcessCode,
    e.FromStatus,e.ToStatus,e.ActionName,e.Reason,e.ActorCode,e.OccurredAtUtc,e.CorrelationId
   FROM MES.FVN_ProductionWorkflowEvent e
   WHERE e.EntityType=@EntityType AND e.EntityId=@EntityId
   UNION ALL
   SELECT -oe.OperationEventId,''OPERATION'',oe.ProductionLotId,oe.RoutingStepId,
    ''MES_PRODUCTION_OPERATION'',CAST(NULL AS int),CAST(NULL AS int),oe.EventType,
    oe.Remarks,oe.ActorCode,oe.OccurredAtUtc,CAST(NULL AS uniqueidentifier)
   FROM MES.FVN_ProductionOperationEvent oe
   WHERE @EntityType=''OPERATION'' AND oe.ProductionLotId=@EntityId
  )
  SELECT WorkflowEventId,EntityType,EntityId,RelatedId,ProcessCode,FromStatus,ToStatus,
   ActionName,Reason,ActorCode,OccurredAtUtc,CorrelationId
  FROM Timeline ORDER BY OccurredAtUtc,WorkflowEventId;
 END');

 /* Keep legacy plan/order writers aligned with the workflow projection.
    The trigger only translates established business status values; it never creates stock movements. */
 EXEC(N'
 CREATE TRIGGER MES.tr_FVN_ProductionPlan_WorkflowSync
 ON MES.FVN_ProductionPlan
 AFTER INSERT,UPDATE
 AS
 BEGIN
  SET NOCOUNT ON;
  ;WITH M AS(
   SELECT i.ProductionPlanId,
    CASE i.Status WHEN ''IMPORTED'' THEN 0 WHEN ''VALIDATED'' THEN 10 WHEN ''FIRM'' THEN 20
     WHEN ''CANCELLED'' THEN 900 ELSE i.WorkflowStatusCode END TargetStatus,
    d.WorkflowStatusCode PreviousStatus,i.WorkflowStatusCode CurrentStatus
   FROM inserted i LEFT JOIN deleted d ON d.ProductionPlanId=i.ProductionPlanId
  )
  UPDATE p SET WorkflowStatusCode=m.TargetStatus
  FROM MES.FVN_ProductionPlan p JOIN M m ON m.ProductionPlanId=p.ProductionPlanId
  WHERE m.TargetStatus IS NOT NULL AND ISNULL(m.CurrentStatus,-1)<>m.TargetStatus;
 END');

 EXEC(N'
 CREATE TRIGGER MES.tr_FVN_ProductionOrder_WorkflowSync
 ON MES.FVN_ProductionOrder
 AFTER INSERT,UPDATE
 AS
 BEGIN
  SET NOCOUNT ON;
  DECLARE @Changed TABLE(ProductionOrderId bigint,FromStatus int,ToStatus int,ActorCode nvarchar(100));
  ;WITH M AS(
   SELECT i.ProductionOrderId,
    CASE i.Status WHEN ''CREATED'' THEN 0 WHEN ''RELEASED'' THEN 10 WHEN ''RUNNING'' THEN 20
     WHEN ''PAUSED'' THEN 30 WHEN ''COMPLETED'' THEN 40 WHEN ''CLOSED'' THEN 50
     WHEN ''CANCELLED'' THEN 900 ELSE i.WorkflowStatusCode END TargetStatus,
    d.WorkflowStatusCode PreviousStatus,i.WorkflowStatusCode CurrentStatus,
    i.ProductionOrderId
   FROM inserted i LEFT JOIN deleted d ON d.ProductionOrderId=i.ProductionOrderId
  )
  UPDATE p SET WorkflowStatusCode=m.TargetStatus
  OUTPUT inserted.ProductionOrderId,deleted.WorkflowStatusCode,inserted.WorkflowStatusCode
   INTO @Changed(ProductionOrderId,FromStatus,ToStatus)
  FROM MES.FVN_ProductionOrder p JOIN M m ON m.ProductionOrderId=p.ProductionOrderId
  WHERE m.TargetStatus IS NOT NULL AND ISNULL(m.CurrentStatus,-1)<>m.TargetStatus;

  INSERT MES.FVN_ProductionWorkflowEvent(EntityType,EntityId,ProcessCode,FromStatus,ToStatus,
   ActionName,ActorCode)
  SELECT ''ORDER'',c.ProductionOrderId,''MES_PRODUCTION_ORDER'',ISNULL(c.FromStatus,0),c.ToStatus,
   CASE c.ToStatus WHEN 10 THEN ''RELEASE'' WHEN 20 THEN ''START'' WHEN 30 THEN ''PAUSE''
    WHEN 40 THEN ''COMPLETE'' WHEN 50 THEN ''CLOSE'' WHEN 900 THEN ''CANCEL'' ELSE ''SYNC'' END,
   COALESCE(NULLIF(po.ReleasedBy,N''''),po.CreatedBy,ORIGINAL_LOGIN())
  FROM @Changed c JOIN MES.FVN_ProductionOrder po ON po.ProductionOrderId=c.ProductionOrderId
  WHERE c.FromStatus IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions t
   WHERE t.ProcessCode=''MES_PRODUCTION_ORDER'' AND t.FromStatus=c.FromStatus
    AND t.ToStatus=c.ToStatus AND t.IsActive=1);
 END');

 /* A newly created production LOT receives one independent state per configured routing step. */
 EXEC(N'
 CREATE TRIGGER MES.tr_FVN_ProductionLot_WorkflowInit
 ON MES.FVN_ProductionLot
 AFTER INSERT
 AS
 BEGIN
  SET NOCOUNT ON;
  INSERT MES.FVN_ProductionOperationWorkflow(ProductionLotId,RoutingStepId,StatusCode)
  SELECT i.ProductionLotId,rs.RoutingStepId,0
  FROM inserted i JOIN MES.FVN_ProductionOrder po ON po.ProductionOrderId=i.ProductionOrderId
  JOIN MES.FVN_ProductionRoutingStep rs ON rs.RoutingId=po.RoutingId AND rs.IsActive=1
  WHERE NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationWorkflow w
   WHERE w.ProductionLotId=i.ProductionLotId AND w.RoutingStepId=rs.RoutingStepId);
 END');

 /* The actual execution procedure remains the sole writer of production facts.
    This trigger synchronizes its completed fact into the workflow projection. */
 EXEC(N'
 CREATE TRIGGER MES.tr_FVN_ProductionOperationExecution_WorkflowSync
 ON MES.FVN_ProductionOperationExecution
 AFTER INSERT
 AS
 BEGIN
  SET NOCOUNT ON;
  DECLARE @StateChanges TABLE(ProductionLotId bigint,RoutingStepId bigint,FromStatus int,ToStatus int,ActorCode nvarchar(100));
  UPDATE w SET StatusCode=50,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=i.OperatorCode
  OUTPUT inserted.ProductionLotId,inserted.RoutingStepId,deleted.StatusCode,inserted.StatusCode,i.OperatorCode
   INTO @StateChanges(ProductionLotId,RoutingStepId,FromStatus,ToStatus,ActorCode)
  FROM MES.FVN_ProductionOperationWorkflow w
  JOIN inserted i ON i.ProductionLotId=w.ProductionLotId AND i.RoutingStepId=w.RoutingStepId
  WHERE w.StatusCode<>50;

  INSERT MES.FVN_ProductionWorkflowEvent(EntityType,EntityId,RelatedId,ProcessCode,
   FromStatus,ToStatus,ActionName,ActorCode)
  SELECT 'OPERATION',c.ProductionLotId,c.RoutingStepId,'MES_PRODUCTION_OPERATION',
   c.FromStatus,c.ToStatus,t.ActionName,c.ActorCode
  FROM @StateChanges c
  JOIN dbo.FVN_sys_WorkflowTransitions t ON t.ProcessCode='MES_PRODUCTION_OPERATION'
   AND t.FromStatus=c.FromStatus AND t.ToStatus=c.ToStatus AND t.IsActive=1;

  INSERT MES.FVN_ProductionOperationEvent(ProductionLotId,RoutingStepId,EventType,Quantity,ReferenceId,ActorCode)
  SELECT i.ProductionLotId,i.RoutingStepId,'ACTUAL_COMPLETED',i.GoodQty+i.NgQty,i.OperationExecutionId,i.OperatorCode
  FROM inserted i;
 END');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('036',N'Production lifecycle workflow, transition audit and timeline');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
