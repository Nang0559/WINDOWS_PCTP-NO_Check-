/*
 Migration 035: one unique product label per unit, reserved when preparing each routing operation.
 SQL Server 2016 compatible. Run after 034.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53500,'Migration 035 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53501,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='034')
 THROW 53502,'Migration 034 must be applied before 035.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='035') RETURN;

BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'MES.FVN_ProductionLabel',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionLabel(
  ProductionLabelId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionLabel PRIMARY KEY,
  ProductionLotId bigint NOT NULL,
  RoutingStepId bigint NOT NULL,
  LabelSequence int NOT NULL,
  LabelCode varchar(16) NOT NULL,
  QrPayload nvarchar(300) NOT NULL,
  Status varchar(12) NOT NULL CONSTRAINT DF_FVN_ProductionLabel_Status DEFAULT('RESERVED'),
  ReservedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionLabel_Reserved DEFAULT(SYSUTCDATETIME()),
  ReservedBy nvarchar(100) NOT NULL,
  PrintedAtUtc datetime2(3) NULL,
  PrintedBy nvarchar(100) NULL,
  AppliedAtUtc datetime2(3) NULL,
  AppliedBy nvarchar(100) NULL,
  VoidedAtUtc datetime2(3) NULL,
  VoidedBy nvarchar(100) NULL,
  CONSTRAINT FK_FVN_ProductionLabel_Lot FOREIGN KEY(ProductionLotId) REFERENCES MES.FVN_ProductionLot(ProductionLotId),
  CONSTRAINT FK_FVN_ProductionLabel_Step FOREIGN KEY(RoutingStepId) REFERENCES MES.FVN_ProductionRoutingStep(RoutingStepId),
  CONSTRAINT UQ_FVN_ProductionLabel_Unit UNIQUE(ProductionLotId,RoutingStepId,LabelSequence),
  CONSTRAINT UQ_FVN_ProductionLabel_Code UNIQUE(LabelCode),
  CONSTRAINT UQ_FVN_ProductionLabel_Qr UNIQUE(QrPayload),
  CONSTRAINT CK_FVN_ProductionLabel_Status CHECK(Status IN('RESERVED','PRINTED','APPLIED','VOID'))
 );

 IF OBJECT_ID(N'MES.FVN_ProductionLabelEvent',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionLabelEvent(
  ProductionLabelEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionLabelEvent PRIMARY KEY,
  ProductionLabelId bigint NOT NULL,
  EventType varchar(12) NOT NULL,
  EventAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionLabelEvent_At DEFAULT(SYSUTCDATETIME()),
  ActorCode nvarchar(100) NOT NULL,
  Remarks nvarchar(300) NULL,
  CONSTRAINT FK_FVN_ProductionLabelEvent_Label FOREIGN KEY(ProductionLabelId) REFERENCES MES.FVN_ProductionLabel(ProductionLabelId),
  CONSTRAINT CK_FVN_ProductionLabelEvent_Type CHECK(EventType IN('RESERVE','PRINT','REPRINT','APPLY','VOID'))
 );

 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FVN_ProductionLabel') AND name=N'IX_FVN_ProductionLabel_LotStepStatus')
 CREATE INDEX IX_FVN_ProductionLabel_LotStepStatus
 ON MES.FVN_ProductionLabel(ProductionLotId,RoutingStepId,Status) INCLUDE(LabelCode,LabelSequence);

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionLabel_Prepare
  @ProductionLotId bigint,@OperationNo nvarchar(40),@TargetQty int,@ActorCode nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @TargetQty<=0 THROW 53510,''Target label quantity must be a positive integer.'',1;
  IF NULLIF(LTRIM(RTRIM(@ActorCode)),N'''') IS NULL THROW 53511,''Actor is required.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @OrderId bigint,@RoutingId bigint,@StepId bigint,@Seq int,@AvailableQty decimal(19,6),
    @Existing int,@Need int,@LotNo nvarchar(120),@Site nvarchar(30),@Part nvarchar(100),@OrderStatus varchar(20);
   SELECT @OrderId=pl.ProductionOrderId,@LotNo=ml.LotNo,@Site=ml.SiteCode,@Part=ml.PartNo,
    @OrderStatus=o.Status,@RoutingId=o.RoutingId,@AvailableQty=ISNULL(pl.FinalGoodQty,pl.GoodQty)
   FROM MES.FVN_ProductionLot pl WITH(UPDLOCK,HOLDLOCK)
   JOIN MES.FVN_MaterialLot ml ON ml.MaterialLotId=pl.MaterialLotId
   JOIN MES.FVN_ProductionOrder o ON o.ProductionOrderId=pl.ProductionOrderId
   WHERE pl.ProductionLotId=@ProductionLotId;
   IF @OrderId IS NULL THROW 53512,''Production LOT not found.'',1;
   IF @OrderStatus NOT IN(''RELEASED'',''RUNNING'') THROW 53513,''Only a released/running order can prepare process labels.'',1;
   SELECT @StepId=RoutingStepId,@Seq=SequenceNo
   FROM MES.FVN_ProductionRoutingStep
   WHERE RoutingId=@RoutingId AND OperationNo=@OperationNo AND IsActive=1;
   IF @StepId IS NULL THROW 53514,''Operation does not belong to the order routing.'',1;
   IF @Seq>1 AND EXISTS(
    SELECT 1 FROM MES.FVN_ProductionRoutingStep prev
    WHERE prev.RoutingId=@RoutingId AND prev.IsActive=1 AND prev.SequenceNo<@Seq
     AND NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationExecution ex
      WHERE ex.ProductionLotId=@ProductionLotId AND ex.RoutingStepId=prev.RoutingStepId))
    THROW 53515,''Previous operation has not been completed; labels cannot be prepared for this process.'',1;
   IF @TargetQty>FLOOR(@AvailableQty) THROW 53516,''Label quantity exceeds the available whole-unit production LOT quantity.'',1;
   SELECT @Existing=COUNT(*) FROM MES.FVN_ProductionLabel WITH(UPDLOCK,HOLDLOCK)
    WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId AND Status<>''VOID'';
   IF @TargetQty<@Existing THROW 53517,''Target quantity is below already reserved labels. Void unused labels explicitly instead.'',1;
   SET @Need=@TargetQty-@Existing;
   IF @Need>0
   BEGIN
    DECLARE @Base int;
    SELECT @Base=ISNULL(MAX(LabelSequence),0) FROM MES.FVN_ProductionLabel WITH(UPDLOCK,HOLDLOCK)
     WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId;
    ;WITH N AS(
     SELECT 1 AS n UNION ALL SELECT n+1 FROM N WHERE n<@Need
    )
    INSERT MES.FVN_ProductionLabel(ProductionLotId,RoutingStepId,LabelSequence,LabelCode,QrPayload,ReservedBy)
    SELECT @ProductionLotId,@StepId,@Base+n,
     ''L''+RIGHT(REPLICATE(''0'',15)+CONVERT(varchar(15),NEXT VALUE FOR MES.FVN_ProductionLabelSequence),15),
     N''FVN2|''+N''L''+RIGHT(REPLICATE(''0'',15)+CONVERT(varchar(15),NEXT VALUE FOR MES.FVN_ProductionLabelSequence),15)
      +N''|''+@LotNo+N''|''+@OperationNo,
     @ActorCode
    FROM N OPTION(MAXRECURSION 0);
    INSERT MES.FVN_ProductionLabelEvent(ProductionLabelId,EventType,ActorCode)
    SELECT ProductionLabelId,''RESERVE'',@ActorCode FROM MES.FVN_ProductionLabel
    WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId AND Status=''RESERVED''
     AND ReservedAtUtc>=DATEADD(SECOND,-5,SYSUTCDATETIME());
   END;
   COMMIT;
   SELECT l.ProductionLabelId,l.LabelCode,l.QrPayload,l.Status,ml.LotNo,
    rs.OperationNo,rs.OperationName,l.LabelSequence
   FROM MES.FVN_ProductionLabel l
   JOIN MES.FVN_ProductionLot pl ON pl.ProductionLotId=l.ProductionLotId
   JOIN MES.FVN_MaterialLot ml ON ml.MaterialLotId=pl.MaterialLotId
   JOIN MES.FVN_ProductionRoutingStep rs ON rs.RoutingStepId=l.RoutingStepId
   WHERE l.ProductionLotId=@ProductionLotId AND l.RoutingStepId=@StepId AND l.Status<>''VOID''
   ORDER BY l.LabelSequence;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionLabel_Print
  @ProductionLotId bigint,@OperationNo nvarchar(40),@ActorCode nvarchar(100),@IsReprint bit=0
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @StepId bigint;
   SELECT @StepId=RoutingStepId FROM MES.FVN_ProductionRoutingStep rs
   JOIN MES.FVN_ProductionLot pl ON pl.ProductionLotId=@ProductionLotId
   JOIN MES.FVN_ProductionOrder o ON o.ProductionOrderId=pl.ProductionOrderId AND o.RoutingId=rs.RoutingId
   WHERE rs.OperationNo=@OperationNo AND rs.IsActive=1;
   IF @StepId IS NULL THROW 53520,''Operation does not belong to this production LOT.'',1;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionLabel WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId AND Status<>''VOID'')
    THROW 53521,''No labels reserved. Prepare labels before printing.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionLabel WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId AND Status=''APPLIED'')
    THROW 53522,''Some labels are already applied; use reprint only for remaining printed labels.'',1;
   UPDATE MES.FVN_ProductionLabel SET Status=''PRINTED'',PrintedAtUtc=SYSUTCDATETIME(),PrintedBy=@ActorCode
   WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId AND Status=''RESERVED'';
   INSERT MES.FVN_ProductionLabelEvent(ProductionLabelId,EventType,ActorCode)
   SELECT ProductionLabelId,CASE WHEN @IsReprint=1 THEN ''REPRINT'' ELSE ''PRINT'' END,@ActorCode
   FROM MES.FVN_ProductionLabel WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId
    AND Status=''PRINTED'';
   COMMIT;
   SELECT l.ProductionLabelId,l.LabelCode,l.QrPayload,l.Status,ml.LotNo,
    rs.OperationNo,rs.OperationName,l.LabelSequence
   FROM MES.FVN_ProductionLabel l
   JOIN MES.FVN_ProductionLot pl ON pl.ProductionLotId=l.ProductionLotId
   JOIN MES.FVN_MaterialLot ml ON ml.MaterialLotId=pl.MaterialLotId
   JOIN MES.FVN_ProductionRoutingStep rs ON rs.RoutingStepId=l.RoutingStepId
   WHERE l.ProductionLotId=@ProductionLotId AND l.RoutingStepId=@StepId AND l.Status=''PRINTED''
   ORDER BY l.LabelSequence;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionLabel_Apply
  @LabelCode varchar(16),@OperationNo nvarchar(40),@ActorCode nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  DECLARE @LabelId bigint,@StepId bigint,@ActualOperation nvarchar(40),@Status varchar(12);
  BEGIN TRY
   BEGIN TRANSACTION;
   SELECT @LabelId=ProductionLabelId,@StepId=RoutingStepId,@Status=Status
   FROM MES.FVN_ProductionLabel WITH(UPDLOCK,HOLDLOCK) WHERE LabelCode=@LabelCode;
   IF @LabelId IS NULL THROW 53530,''Label code not found.'',1;
   SELECT @ActualOperation=OperationNo FROM MES.FVN_ProductionRoutingStep WHERE RoutingStepId=@StepId;
   IF @ActualOperation<>@OperationNo THROW 53531,''Label belongs to a different process.'',1;
   IF @Status=''APPLIED'' THROW 53532,''This product label has already been applied.'',1;
   IF @Status<>''PRINTED'' THROW 53533,''Only printed labels can be applied.'',1;
   UPDATE MES.FVN_ProductionLabel SET Status=''APPLIED'',AppliedAtUtc=SYSUTCDATETIME(),AppliedBy=@ActorCode
   WHERE ProductionLabelId=@LabelId;
   INSERT MES.FVN_ProductionLabelEvent(ProductionLabelId,EventType,ActorCode)
   VALUES(@LabelId,''APPLY'',@ActorCode);
   COMMIT;
   SELECT @LabelId ProductionLabelId,@LabelCode LabelCode,@ActualOperation OperationNo,''APPLIED'' Status;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionLabel_Void
  @LabelCode varchar(16),@ActorCode nvarchar(100),@Reason nvarchar(300)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @LabelId bigint,@Status varchar(12);
   SELECT @LabelId=ProductionLabelId,@Status=Status FROM MES.FVN_ProductionLabel WITH(UPDLOCK,HOLDLOCK)
    WHERE LabelCode=@LabelCode;
   IF @LabelId IS NULL THROW 53540,''Label code not found.'',1;
   IF @Status=''APPLIED'' THROW 53541,''Applied label cannot be voided.'',1;
   IF @Status=''VOID'' THROW 53542,''Label is already void.'',1;
   IF NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL THROW 53543,''Void reason is required.'',1;
   UPDATE MES.FVN_ProductionLabel SET Status=''VOID'',VoidedAtUtc=SYSUTCDATETIME(),VoidedBy=@ActorCode
    WHERE ProductionLabelId=@LabelId;
   INSERT MES.FVN_ProductionLabelEvent(ProductionLabelId,EventType,ActorCode,Remarks)
   VALUES(@LabelId,''VOID'',@ActorCode,@Reason);
   COMMIT;
   SELECT @LabelId ProductionLabelId,@LabelCode LabelCode,''VOID'' Status;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('035',N'Per-unit production labels reserved and printed at each process preparation');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
