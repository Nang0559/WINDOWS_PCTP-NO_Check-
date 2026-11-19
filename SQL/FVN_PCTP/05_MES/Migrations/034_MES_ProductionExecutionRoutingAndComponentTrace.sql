/*
 Migration 034: MES production execution, operation routing, LOT continuity and bidirectional component trace.
 SQL Server 2016 compatible. Requires 033 and the canonical MES inventory posting adapter (025).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53400,'Migration 034 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53401,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='032')
 THROW 53402,'Migration 032 must be applied before 034.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='033')
 THROW 53403,'Migration 033 must be applied before 034.',1;
IF OBJECT_ID(N'MES.FVN_ComponentLot',N'U') IS NULL OR OBJECT_ID(N'MES.FVN_SupplierIdentity',N'U') IS NULL
 THROW 53404,'Canonical component LOT and supplier identity tables from migrations 029/031/032 are required.',1;
IF OBJECT_ID(N'dbo.usp_FVN_PCTP_PostStockMovement',N'P') IS NULL
 THROW 53403,'Migration 025 stock posting adapter is required.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='034') RETURN;

BEGIN TRY
 BEGIN TRANSACTION;

 /* The earlier generic order table is retained as the canonical order identity.
    IFS BOM snapshots are still the source for component rows; no Bravo source is used. */
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'BomRevisionId') IS NOT NULL
    ALTER TABLE MES.FVN_ProductionOrder ALTER COLUMN BomRevisionId bigint NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'ShiftCode') IS NULL
    ALTER TABLE MES.FVN_ProductionOrder ADD ShiftCode nvarchar(20) NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'ProductionLineCode') IS NULL
    ALTER TABLE MES.FVN_ProductionOrder ADD ProductionLineCode nvarchar(30) NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'ProductionDate') IS NULL
    ALTER TABLE MES.FVN_ProductionOrder ADD ProductionDate date NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'DepartmentCode') IS NULL
    ALTER TABLE MES.FVN_ProductionOrder ADD DepartmentCode nvarchar(4) NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'RoutingId') IS NULL
    ALTER TABLE MES.FVN_ProductionOrder ADD RoutingId bigint NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionOrder',N'LotLineCode') IS NULL
    ALTER TABLE MES.FVN_ProductionOrder ADD LotLineCode varchar(3) NULL;

 IF OBJECT_ID(N'MES.FVN_ProductionLotCodeMap',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionLotCodeMap(
  SiteCode nvarchar(30) NOT NULL, PlanProductionLineCode nvarchar(30) NOT NULL,
  LotLineCode varchar(3) NOT NULL, DepartmentCode varchar(4) NOT NULL,
  IsActive bit NOT NULL CONSTRAINT DF_FVN_ProductionLotCodeMap_Active DEFAULT(1),
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionLotCodeMap_Created DEFAULT(SYSUTCDATETIME()),
  CreatedBy nvarchar(100) NULL,
  CONSTRAINT PK_FVN_ProductionLotCodeMap PRIMARY KEY(SiteCode,PlanProductionLineCode),
  CONSTRAINT FK_FVN_ProductionLotCodeMap_Site FOREIGN KEY(SiteCode) REFERENCES dbo.FVN_PCTPSite(SiteCode),
  CONSTRAINT CK_FVN_ProductionLotCodeMap_Line CHECK(LEN(LotLineCode)=3),
  CONSTRAINT CK_FVN_ProductionLotCodeMap_Dept CHECK(LEN(DepartmentCode)=4)
 );

 IF COL_LENGTH(N'MES.FVN_ComponentDemandLine',N'SupplyMode') IS NULL
  EXEC(N'ALTER TABLE MES.FVN_ComponentDemandLine ADD SupplyMode varchar(8) NOT NULL CONSTRAINT DF_FVN_ComponentDemandLine_SupplyMode DEFAULT(''BUY'')');
 IF COL_LENGTH(N'MES.FVN_ComponentDemandLine',N'SourceAreaCode') IS NULL
  EXEC(N'ALTER TABLE MES.FVN_ComponentDemandLine ADD SourceAreaCode varchar(8) NOT NULL CONSTRAINT DF_FVN_ComponentDemandLine_SourceArea DEFAULT(''PART'')');

 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'MES.FVN_ComponentDemandLine') AND name=N'CK_FVN_ComponentDemandLine_SupplyArea')
  EXEC(N'ALTER TABLE MES.FVN_ComponentDemandLine ADD CONSTRAINT CK_FVN_ComponentDemandLine_SupplyArea CHECK(SupplyMode IN(''BUY'',''MAKE'') AND SourceAreaCode IN(''PART'',''WIP'',''FG''))');

 IF OBJECT_ID(N'MES.FVN_ProductionPartPolicy',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionPartPolicy(
  SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL,
  OutputLotType varchar(8) NOT NULL, IsActive bit NOT NULL CONSTRAINT DF_FVN_ProductionPartPolicy_Active DEFAULT(1),
  UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionPartPolicy_Updated DEFAULT(SYSUTCDATETIME()),
  UpdatedBy nvarchar(100) NULL,
  CONSTRAINT PK_FVN_ProductionPartPolicy PRIMARY KEY(SiteCode,PartNo),
  CONSTRAINT FK_FVN_ProductionPartPolicy_Part FOREIGN KEY(SiteCode,PartNo) REFERENCES IFS.FVN_InventoryPart(SiteCode,PartNo),
  CONSTRAINT CK_FVN_ProductionPartPolicy_Type CHECK(OutputLotType IN('WIP','FG'))
 );

 EXEC(N'UPDATE d SET SupplyMode=''MAKE'',SourceAreaCode=p.OutputLotType
  FROM MES.FVN_ComponentDemandLine d
  JOIN MES.FVN_ProductionPartPolicy p ON p.SiteCode=d.SiteCode
   AND p.PartNo=d.ComponentPartNo AND p.IsActive=1');

 IF OBJECT_ID(N'MES.FVN_ProductionRouting',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionRouting(
  RoutingId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionRouting PRIMARY KEY,
  SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL,
  RevisionNo nvarchar(30) NOT NULL, EffectiveFrom date NOT NULL, EffectiveTo date NULL,
  IsActive bit NOT NULL CONSTRAINT DF_FVN_ProductionRouting_Active DEFAULT(1),
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionRouting_Created DEFAULT(SYSUTCDATETIME()),
  CreatedBy nvarchar(100) NOT NULL,
  CONSTRAINT FK_FVN_ProductionRouting_Part FOREIGN KEY(SiteCode,PartNo) REFERENCES IFS.FVN_InventoryPart(SiteCode,PartNo),
  CONSTRAINT UQ_FVN_ProductionRouting_Revision UNIQUE(SiteCode,PartNo,RevisionNo),
  CONSTRAINT CK_FVN_ProductionRouting_Dates CHECK(EffectiveTo IS NULL OR EffectiveTo>=EffectiveFrom)
 );

 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_FVN_ProductionOrder_Routing')
  ALTER TABLE MES.FVN_ProductionOrder ADD CONSTRAINT FK_FVN_ProductionOrder_Routing
   FOREIGN KEY(RoutingId) REFERENCES MES.FVN_ProductionRouting(RoutingId);

 IF OBJECT_ID(N'MES.FVN_ProductionRoutingStep',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionRoutingStep(
  RoutingStepId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionRoutingStep PRIMARY KEY,
  RoutingId bigint NOT NULL, OperationNo nvarchar(40) NOT NULL, OperationName nvarchar(120) NOT NULL,
  SequenceNo int NOT NULL, TransferMode varchar(12) NOT NULL,
  IsFinalOutput bit NOT NULL CONSTRAINT DF_FVN_RoutingStep_Final DEFAULT(0),
  IsActive bit NOT NULL CONSTRAINT DF_FVN_RoutingStep_Active DEFAULT(1),
  CONSTRAINT FK_FVN_RoutingStep_Routing FOREIGN KEY(RoutingId) REFERENCES MES.FVN_ProductionRouting(RoutingId),
  CONSTRAINT UQ_FVN_RoutingStep_Sequence UNIQUE(RoutingId,SequenceNo),
  CONSTRAINT UQ_FVN_RoutingStep_Operation UNIQUE(RoutingId,OperationNo),
  CONSTRAINT CK_FVN_RoutingStep_Sequence CHECK(SequenceNo>0),
  CONSTRAINT CK_FVN_RoutingStep_Transfer CHECK(TransferMode IN('DIRECT','VIA_WIP'))
 );

 IF OBJECT_ID(N'MES.FVN_ProductionLot',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionLot(
  ProductionLotId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionLot PRIMARY KEY,
  SiteCode nvarchar(30) NOT NULL, ProductionOrderId bigint NOT NULL,
  MaterialLotId bigint NOT NULL, IsFinishedProduct bit NOT NULL,
  GoodQty decimal(19,6) NOT NULL, NgQty decimal(19,6) NOT NULL CONSTRAINT DF_FVN_ProductionLot_NG DEFAULT(0),
  FinalGoodQty decimal(19,6) NULL, FinalNgQty decimal(19,6) NULL,
  ProducedAtUtc datetime2(3) NOT NULL, CreatedBy nvarchar(100) NOT NULL,
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionLot_Created DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT FK_FVN_ProductionLot_Order FOREIGN KEY(ProductionOrderId) REFERENCES MES.FVN_ProductionOrder(ProductionOrderId),
  CONSTRAINT FK_FVN_ProductionLot_MaterialLot FOREIGN KEY(MaterialLotId) REFERENCES MES.FVN_MaterialLot(MaterialLotId),
  CONSTRAINT UQ_FVN_ProductionLot_MaterialLot UNIQUE(MaterialLotId),
  CONSTRAINT CK_FVN_ProductionLot_Qty CHECK(GoodQty>0 AND NgQty>=0)
 );

 IF OBJECT_ID(N'MES.FVN_ProductionOperationExecution',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionOperationExecution(
  OperationExecutionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionOperationExecution PRIMARY KEY,
  ProductionLotId bigint NOT NULL, RoutingStepId bigint NOT NULL,
  InputQty decimal(19,6) NOT NULL, GoodQty decimal(19,6) NOT NULL, NgQty decimal(19,6) NOT NULL,
  InputSlotId int NULL, OutputSlotId int NULL,
  InputInventoryTransactionId bigint NULL, OutputInventoryTransactionId bigint NULL,
  StartedAtUtc datetime2(3) NULL, CompletedAtUtc datetime2(3) NOT NULL,
  OperatorCode nvarchar(100) NOT NULL, Remarks nvarchar(500) NULL,
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_OperationExecution_Created DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT FK_FVN_OperationExecution_Lot FOREIGN KEY(ProductionLotId) REFERENCES MES.FVN_ProductionLot(ProductionLotId),
  CONSTRAINT FK_FVN_OperationExecution_Step FOREIGN KEY(RoutingStepId) REFERENCES MES.FVN_ProductionRoutingStep(RoutingStepId),
  CONSTRAINT FK_FVN_OperationExecution_InputTxn FOREIGN KEY(InputInventoryTransactionId) REFERENCES MES.FVN_InventoryTransaction(InventoryTransactionId),
  CONSTRAINT FK_FVN_OperationExecution_OutputTxn FOREIGN KEY(OutputInventoryTransactionId) REFERENCES MES.FVN_InventoryTransaction(InventoryTransactionId),
  CONSTRAINT UQ_FVN_OperationExecution_LotStep UNIQUE(ProductionLotId,RoutingStepId),
  CONSTRAINT CK_FVN_OperationExecution_Qty CHECK(InputQty>=0 AND GoodQty>=0 AND NgQty>=0 AND GoodQty+NgQty<=InputQty)
 );

 IF OBJECT_ID(N'MES.FVN_ProductionOperationTransfer',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionOperationTransfer(
  OperationTransferId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ProductionOperationTransfer PRIMARY KEY,
  ProductionLotId bigint NOT NULL, FromExecutionId bigint NOT NULL, ToRoutingStepId bigint NOT NULL,
  TransferMode varchar(12) NOT NULL, TransferQty decimal(19,6) NOT NULL,
  ReceivedQty decimal(19,6) NOT NULL CONSTRAINT DF_FVN_OperationTransfer_Received DEFAULT(0),
  FromSlotId int NULL, ToSlotId int NULL, InventoryTransactionId bigint NULL,
  Status varchar(12) NOT NULL CONSTRAINT DF_FVN_OperationTransfer_Status DEFAULT('PENDING'),
  HandedOffAtUtc datetime2(3) NOT NULL, HandedOffBy nvarchar(100) NOT NULL,
  ReceivedAtUtc datetime2(3) NULL, ReceivedBy nvarchar(100) NULL,
  CONSTRAINT FK_FVN_OperationTransfer_Lot FOREIGN KEY(ProductionLotId) REFERENCES MES.FVN_ProductionLot(ProductionLotId),
  CONSTRAINT FK_FVN_OperationTransfer_From FOREIGN KEY(FromExecutionId) REFERENCES MES.FVN_ProductionOperationExecution(OperationExecutionId),
  CONSTRAINT FK_FVN_OperationTransfer_To FOREIGN KEY(ToRoutingStepId) REFERENCES MES.FVN_ProductionRoutingStep(RoutingStepId),
  CONSTRAINT FK_FVN_OperationTransfer_Txn FOREIGN KEY(InventoryTransactionId) REFERENCES MES.FVN_InventoryTransaction(InventoryTransactionId),
  CONSTRAINT UQ_FVN_OperationTransfer_From UNIQUE(FromExecutionId),
  CONSTRAINT CK_FVN_OperationTransfer_Mode CHECK(TransferMode IN('DIRECT','VIA_WIP')),
  CONSTRAINT CK_FVN_OperationTransfer_Status CHECK(Status IN('PENDING','RECEIVED','CANCELLED')),
  CONSTRAINT CK_FVN_OperationTransfer_Qty CHECK(TransferQty>0 AND ReceivedQty>=0 AND ReceivedQty<=TransferQty)
 );

 IF OBJECT_ID(N'MES.FVN_ProductionLotTraceIndex',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionLotTraceIndex(
  ProductionLotId bigint NOT NULL, ProductionOrderId bigint NOT NULL,
  MaterialLotId bigint NOT NULL, LastOperationExecutionId bigint NULL,
  UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ProductionLotTraceIndex_Updated DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT PK_FVN_ProductionLotTraceIndex PRIMARY KEY(ProductionLotId),
  CONSTRAINT FK_FVN_ProductionLotTraceIndex_Lot FOREIGN KEY(ProductionLotId) REFERENCES MES.FVN_ProductionLot(ProductionLotId),
  CONSTRAINT FK_FVN_ProductionLotTraceIndex_Order FOREIGN KEY(ProductionOrderId) REFERENCES MES.FVN_ProductionOrder(ProductionOrderId),
  CONSTRAINT FK_FVN_ProductionLotTraceIndex_MaterialLot FOREIGN KEY(MaterialLotId) REFERENCES MES.FVN_MaterialLot(MaterialLotId),
  CONSTRAINT FK_FVN_ProductionLotTraceIndex_Execution FOREIGN KEY(LastOperationExecutionId) REFERENCES MES.FVN_ProductionOperationExecution(OperationExecutionId)
 );

 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FVN_ProductionOrder') AND name=N'UX_FVN_ProductionOrder_LotIdentity')
  EXEC(N'CREATE UNIQUE INDEX UX_FVN_ProductionOrder_LotIdentity
   ON MES.FVN_ProductionOrder(SiteCode,ProductPartNo,ProductionDate,ShiftCode,ProductionLineCode,DepartmentCode)
   WHERE ProductionDate IS NOT NULL AND ShiftCode IS NOT NULL AND ProductionLineCode IS NOT NULL AND DepartmentCode IS NOT NULL');

 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FVN_FinishedLotGenealogy') AND name=N'IX_FVN_Genealogy_ComponentLot')
 CREATE INDEX IX_FVN_Genealogy_ComponentLot ON MES.FVN_FinishedLotGenealogy(ComponentLotId,FinishedLotId) INCLUDE(ProductionOrderId,ConsumedQty,MaterialExecutionEventId);
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FVN_FinishedLotGenealogy') AND name=N'IX_FVN_Genealogy_FinishedLot')
 CREATE INDEX IX_FVN_Genealogy_FinishedLot ON MES.FVN_FinishedLotGenealogy(FinishedLotId,ComponentLotId) INCLUDE(ProductionOrderId,ConsumedQty,MaterialExecutionEventId);

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_GenerateComponentDemandV2
  @ProductionPlanLineId bigint,@CreatedBy nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  DECLARE @SiteCode nvarchar(30),@ProductPartNo nvarchar(100),@PlanQty decimal(19,6),@PlanDate date;
  SELECT @SiteCode=l.SiteCode,@ProductPartNo=l.ProductPartNo,@PlanQty=l.PlannedQty,
   @PlanDate=ISNULL(l.ProductionDate,p.PlanDate)
  FROM MES.FVN_ProductionPlanLine l JOIN MES.FVN_ProductionPlan p ON p.ProductionPlanId=l.ProductionPlanId
  WHERE l.ProductionPlanLineId=@ProductionPlanLineId;
  IF @SiteCode IS NULL THROW 53405,''Production plan line not found.'',1;
  IF EXISTS(SELECT 1 FROM MES.FVN_ComponentDemandHeader WHERE ProductionPlanLineId=@ProductionPlanLineId)
   THROW 53406,''Component demand has already been generated for this plan line.'',1;
  DECLARE @BomCount int;
  SELECT @BomCount=COUNT(*) FROM IFS.FVN_BomHeader h
  JOIN IFS.FVN_BomAlternative a ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo
   AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
  WHERE h.SiteCode=@SiteCode AND h.PartNo=@ProductPartNo
   AND h.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
   AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)));
  IF @BomCount=0 THROW 53407,''No effective IFS BOM exists for this product/date.'',1;
  IF @BomCount>1 THROW 53408,''More than one effective IFS BOM alternative exists.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @Eng nvarchar(24),@BomType nvarchar(80),@Alt nvarchar(80),@DemandId bigint;
   SELECT TOP(1) @Eng=h.EngChgLevel,@BomType=h.BomTypeDb,@Alt=a.AlternativeNo
   FROM IFS.FVN_BomHeader h JOIN IFS.FVN_BomAlternative a
    ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
   WHERE h.SiteCode=@SiteCode AND h.PartNo=@ProductPartNo
    AND h.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
    AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)));
   INSERT MES.FVN_ComponentDemandHeader(SiteCode,ProductionPlanLineId,ProductPartNo,PlannedQty,
    BomEngChgLevel,BomTypeDb,AlternativeNo,CreatedBy)
   VALUES(@SiteCode,@ProductionPlanLineId,@ProductPartNo,@PlanQty,@Eng,@BomType,@Alt,@CreatedBy);
   SET @DemandId=SCOPE_IDENTITY();
   ;WITH Required AS(
    SELECT c.ComponentPartNo,
     SUM(CONVERT(decimal(19,6),c.QtyPerAssembly*@PlanQty)) RequiredQty,
     MAX(CONVERT(nvarchar(40),ip.UnitMeas)) UomCode,
     MAX(CONVERT(nvarchar(40),c.OperationNo)) OperationNo,
     CASE WHEN MAX(CASE WHEN pol.PartNo IS NOT NULL THEN 1 ELSE 0 END)=1 THEN ''MAKE'' ELSE ''BUY'' END SupplyMode,
     CASE WHEN MAX(CASE WHEN pol.PartNo IS NOT NULL THEN 1 ELSE 0 END)=1
      THEN MAX(pol.OutputLotType) ELSE ''PART'' END SourceAreaCode
    FROM IFS.FVN_BomComponent c
    JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=c.ComponentContract AND ip.PartNo=c.ComponentPartNo
    LEFT JOIN MES.FVN_ProductionPartPolicy pol ON pol.SiteCode=@SiteCode
     AND pol.PartNo=c.ComponentPartNo AND pol.IsActive=1
    WHERE c.SiteCode=@SiteCode AND c.PartNo=@ProductPartNo AND c.EngChgLevel=@Eng
     AND c.BomTypeDb=@BomType AND c.AlternativeNo=@Alt
     AND c.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
     AND (c.EffectiveOutDate IS NULL OR c.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)))
     AND c.QtyPerAssembly>0
    GROUP BY c.ComponentPartNo
   )
   INSERT MES.FVN_ComponentDemandLine(ComponentDemandId,SiteCode,ComponentPartNo,RequiredQty,
    PartAvailableQty,NetRequiredQty,UomCode,OperationNo,SupplyMode,SourceAreaCode)
   SELECT @DemandId,@SiteCode,r.ComponentPartNo,r.RequiredQty,ISNULL(stock.AvailableQty,0),
    CASE WHEN r.RequiredQty-ISNULL(stock.AvailableQty,0)>0 THEN r.RequiredQty-ISNULL(stock.AvailableQty,0) ELSE 0 END,
    r.UomCode,r.OperationNo,r.SupplyMode,r.SourceAreaCode
   FROM Required r
   OUTER APPLY(
    SELECT SUM(b.OnHandQty-b.ReservedQty) AvailableQty
    FROM MES.FVN_InventoryBalance b
    JOIN MES.FVN_IfsInventoryLocation loc ON loc.SiteCode=b.SiteCode AND loc.LocationNo=b.LocationNo
    JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=loc.SiteCode AND sm.LocationNo=loc.LocationNo
     AND sm.IsPrimary=1 AND sm.IsActive=1
    JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode=sm.SiteCode AND ar.SlotId=sm.SlotId
     AND ar.AreaCode=r.SourceAreaCode AND ar.IsActive=1
    WHERE b.SiteCode=@SiteCode AND b.PartNo=r.ComponentPartNo
      AND b.StockState=CASE WHEN r.SourceAreaCode=''WIP'' THEN ''WIP'' ELSE ''AVAILABLE'' END
   ) stock;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentDemandLine WHERE ComponentDemandId=@DemandId)
    THROW 53409,''IFS BOM has no usable inventory component rows for this Site.'',1;
   UPDATE MES.FVN_ComponentDemandHeader SET Status=''FULFILLED''
    WHERE ComponentDemandId=@DemandId AND NOT EXISTS(
     SELECT 1 FROM MES.FVN_ComponentDemandLine WHERE ComponentDemandId=@DemandId AND NetRequiredQty>0);
   COMMIT;
   SELECT @DemandId ComponentDemandId,COUNT(*) ComponentCount
   FROM MES.FVN_ComponentDemandLine WHERE ComponentDemandId=@DemandId;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ComponentDemand_ListV2 @ProductionPlanLineId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT h.ComponentDemandId,h.Status DemandStatus,l.ComponentDemandLineId,
   l.ComponentPartNo,ip.Description ComponentDescription,l.RequiredQty,l.PartAvailableQty,
   l.NetRequiredQty,l.IssuedQty,
   CASE WHEN l.NetRequiredQty-l.IssuedQty>0 THEN l.NetRequiredQty-l.IssuedQty ELSE 0 END OutstandingQty,
   l.UomCode,l.OperationNo,l.SupplyMode,l.SourceAreaCode
  FROM MES.FVN_ComponentDemandHeader h
  JOIN MES.FVN_ComponentDemandLine l ON l.ComponentDemandId=h.ComponentDemandId
  LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=l.SiteCode AND ip.PartNo=l.ComponentPartNo
  WHERE h.ProductionPlanLineId=@ProductionPlanLineId
  ORDER BY l.ComponentPartNo;
 END');

 EXEC(N'
 CREATE TRIGGER MES.tr_FVN_ComponentIssueLine_MakeGuard
 ON MES.FVN_ComponentIssueLine
 AFTER INSERT
 AS
 BEGIN
  SET NOCOUNT ON;
  IF EXISTS(SELECT 1 FROM inserted i
    JOIN MES.FVN_ComponentDemandLine d ON d.ComponentDemandLineId=i.ComponentDemandLineId
    WHERE d.SupplyMode=''MAKE'' OR d.SourceAreaCode<>''PART'')
   THROW 53404,''Manufactured subassembly LOTs are already WIP; do not issue them from PART. Consume the selected WIP LOT against the production order to record genealogy.'',1;
 END');

 /* Add FG as a logical slot area; physical inventory remains in the same MES ledger. */
 IF OBJECT_ID(N'dbo.FVN_InventoryAreaSlot',N'U') IS NOT NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_InventoryAreaSlot') AND name=N'CK_FVN_InventoryAreaSlot_Area')
   ALTER TABLE dbo.FVN_InventoryAreaSlot DROP CONSTRAINT CK_FVN_InventoryAreaSlot_Area;
  ALTER TABLE dbo.FVN_InventoryAreaSlot ADD CONSTRAINT CK_FVN_InventoryAreaSlot_Area CHECK(AreaCode IN('PART','WIP','FG'));
 END;

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionOrder_CreateFromPlan
  @ProductionPlanLineId bigint,@OrderNo nvarchar(80),@CreatedBy nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF NULLIF(LTRIM(RTRIM(@OrderNo)),N'''') IS NULL THROW 53410,''OrderNo is required.'',1;
  IF NULLIF(LTRIM(RTRIM(@CreatedBy)),N'''') IS NULL THROW 53411,''CreatedBy is required.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @Site nvarchar(30),@Part nvarchar(100),@Qty decimal(19,6),@Uom nvarchar(30),
    @PlanDate date,@Shift nvarchar(20),@PlanLine nvarchar(30),@LotLine varchar(3),@Dept varchar(4),
    @OrderId bigint,@BomCount int,@Eng nvarchar(24),@BomType nvarchar(80),@Alt nvarchar(80),
    @RoutingId bigint,@RoutingCount int;
   SELECT @Site=l.SiteCode,@Part=l.ProductPartNo,@Qty=l.PlannedQty,@Uom=l.UomCode,
    @PlanDate=ISNULL(l.ProductionDate,p.PlanDate),@Shift=l.ShiftCode,@PlanLine=l.ProductionLineCode
   FROM MES.FVN_ProductionPlanLine l JOIN MES.FVN_ProductionPlan p ON p.ProductionPlanId=l.ProductionPlanId
   WHERE l.ProductionPlanLineId=@ProductionPlanLineId;
   IF @Site IS NULL THROW 53412,''Production plan line not found.'',1;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionPartPolicy WHERE SiteCode=@Site AND PartNo=@Part AND IsActive=1)
    THROW 53418,''Production output type policy (WIP/FG) is not configured for this part/site.'',1;
   SELECT @LotLine=LotLineCode,@Dept=DepartmentCode FROM MES.FVN_ProductionLotCodeMap
    WHERE SiteCode=@Site AND PlanProductionLineCode=@PlanLine AND IsActive=1;
   IF @LotLine IS NULL THROW 53413,''Production line to LOT line/department mapping is not configured.'',1;
   IF ISNULL(LEN(@Shift),0)<>2 THROW 53414,''Production ShiftCode must be exactly two characters for LOT creation.'',1;
   SELECT @RoutingCount=COUNT(*),@RoutingId=MIN(RoutingId)
   FROM MES.FVN_ProductionRouting WHERE SiteCode=@Site AND PartNo=@Part AND IsActive=1
    AND EffectiveFrom<=@PlanDate AND (EffectiveTo IS NULL OR EffectiveTo>=@PlanDate);
   IF @RoutingCount=0 THROW 53428,''No effective production routing is configured for this product/date.'',1;
   IF @RoutingCount>1 THROW 53429,''More than one effective production routing exists for this product/date.'',1;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionRoutingStep WHERE RoutingId=@RoutingId AND IsActive=1)
    THROW 53430,''The effective production routing has no active operation steps.'',1;
   IF (SELECT COUNT(*) FROM MES.FVN_ProductionRoutingStep WHERE RoutingId=@RoutingId AND IsActive=1 AND IsFinalOutput=1)<>1
    OR NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionRoutingStep x WHERE x.RoutingId=@RoutingId AND x.IsActive=1 AND x.IsFinalOutput=1
      AND x.SequenceNo=(SELECT MAX(y.SequenceNo) FROM MES.FVN_ProductionRoutingStep y WHERE y.RoutingId=@RoutingId AND y.IsActive=1))
    THROW 53431,''Routing must have exactly one final output step at the last sequence.'',1;
   SELECT @BomCount=COUNT(*) FROM IFS.FVN_BomHeader h JOIN IFS.FVN_BomAlternative a
    ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
   WHERE h.SiteCode=@Site AND h.PartNo=@Part AND h.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
    AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)));
   IF @BomCount=0 THROW 53415,''No effective IFS BOM exists for this product and date.'',1;
   IF @BomCount>1 THROW 53416,''More than one effective IFS BOM exists; resolve alternative before order creation.'',1;
   SELECT TOP(1) @Eng=h.EngChgLevel,@BomType=h.BomTypeDb,@Alt=a.AlternativeNo
   FROM IFS.FVN_BomHeader h JOIN IFS.FVN_BomAlternative a
    ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
   WHERE h.SiteCode=@Site AND h.PartNo=@Part AND h.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
    AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)));
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionOrder WHERE SiteCode=@Site AND OrderNo=@OrderNo)
    THROW 53417,''Production order number already exists.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionOrder
     WHERE SiteCode=@Site AND ProductPartNo=@Part AND ProductionDate=@PlanDate
      AND ShiftCode=@Shift AND ProductionLineCode=@PlanLine AND DepartmentCode=@Dept)
    THROW 53419,''The 20-character LOT key is identical for this product/date/shift/line/department. Combine plan quantity into one production order instead of creating a second order.'',1;
   INSERT MES.FVN_ProductionOrder(SiteCode,OrderNo,ProductPartNo,BomRevisionId,ProductionPlanLineId,
    PlannedQty,CompletedQty,UomCode,Status,ShiftCode,ProductionLineCode,ProductionDate,DepartmentCode,RoutingId,LotLineCode)
   VALUES(@Site,@OrderNo,@Part,NULL,@ProductionPlanLineId,@Qty,0,@Uom,''CREATED'',@Shift,@PlanLine,@PlanDate,@Dept,@RoutingId,@LotLine);
   SET @OrderId=SCOPE_IDENTITY();
   INSERT MES.FVN_ProductionOrderMaterial(ProductionOrderId,BomComponentId,SiteCode,ComponentPartNo,
    RequiredQty,UomCode,OperationNo,IsBackflush)
   SELECT @OrderId,NULL,@Site,c.ComponentPartNo,
    SUM(CONVERT(decimal(19,6),c.QtyPerAssembly*@Qty)),MAX(CONVERT(nvarchar(30),ip.UnitMeas)),
    MAX(CONVERT(nvarchar(40),c.OperationNo)),0
   FROM IFS.FVN_BomComponent c
   JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=c.ComponentContract AND ip.PartNo=c.ComponentPartNo
   WHERE c.SiteCode=@Site AND c.PartNo=@Part AND c.EngChgLevel=@Eng AND c.BomTypeDb=@BomType
    AND c.AlternativeNo=@Alt AND c.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
    AND (c.EffectiveOutDate IS NULL OR c.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)))
    AND c.QtyPerAssembly>0
   GROUP BY c.ComponentPartNo,c.OperationNo;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOrderMaterial WHERE ProductionOrderId=@OrderId)
    THROW 53432,''IFS BOM has no component rows for this production order.'',1;
   UPDATE MES.FVN_ProductionOrder SET Status=''RELEASED'',ReleasedAtUtc=SYSUTCDATETIME(),ReleasedBy=@CreatedBy
    WHERE ProductionOrderId=@OrderId;
   COMMIT;
   SELECT @OrderId ProductionOrderId,@OrderNo OrderNo,@Part ProductPartNo,@Qty PlannedQty;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionOrder_MaterialList @ProductionOrderId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT ProductionOrderMaterialId,ComponentPartNo,RequiredQty,UomCode,OperationNo,IsBackflush
  FROM MES.FVN_ProductionOrderMaterial
  WHERE ProductionOrderId=@ProductionOrderId
  ORDER BY OperationNo,ComponentPartNo;
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionLot_Create
  @ProductionOrderId bigint,@ProducedAtUtc datetime2(3),@GoodQty decimal(19,6),
  @NgQty decimal(19,6),@IsFinishedProduct bit,@CreatedBy nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @GoodQty<=0 OR @NgQty<0 THROW 53420,''GoodQty must be positive and NgQty cannot be negative.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @Site nvarchar(30),@Part nvarchar(100),@Shift nvarchar(20),@PlanLine nvarchar(30),
    @Dept nvarchar(4),@ProdDate date,@PlannedQty decimal(19,6),@LotLine varchar(3),@LotDept varchar(4),
    @Lot char(20),@ItemId5 char(5),@MaterialLotId bigint,@ProductionLotId bigint,@Status varchar(20);
   SELECT @Site=SiteCode,@Part=ProductPartNo,@Shift=ShiftCode,@PlanLine=ProductionLineCode,
    @Dept=DepartmentCode,@ProdDate=ProductionDate,@PlannedQty=PlannedQty,@Status=Status,
    @LotLine=LotLineCode,@LotDept=DepartmentCode
   FROM MES.FVN_ProductionOrder WITH(UPDLOCK,HOLDLOCK) WHERE ProductionOrderId=@ProductionOrderId;
   IF @Site IS NULL THROW 53421,''Production order not found.'',1;
   IF @Status NOT IN(''RELEASED'',''RUNNING'') THROW 53422,''Only released/running order can create production LOT.'',1;
   IF @GoodQty+@NgQty>@PlannedQty THROW 53426,''Production output quantity exceeds the order planned quantity.'',1;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionPartPolicy WHERE SiteCode=@Site AND PartNo=@Part
      AND IsActive=1 AND OutputLotType=CASE WHEN @IsFinishedProduct=1 THEN ''FG'' ELSE ''WIP'' END)
    THROW 53425,''IsFinishedProduct does not match the configured WIP/FG policy for this part.'',1;
   EXEC MES.usp_FVN_BuildProductionLotNo @RequestDate=@ProdDate,@SiteCode=@Site,@PartNo=@Part,
    @ShiftCode=@Shift,@LineCode=@LotLine,@DepartmentCode=@LotDept,@LotNo=@Lot OUTPUT,@ItemId5=@ItemId5 OUTPUT;
   SELECT @MaterialLotId=MaterialLotId FROM MES.FVN_MaterialLot WITH(UPDLOCK,HOLDLOCK)
    WHERE SiteCode=@Site AND PartNo=@Part AND LotNo=@Lot;
   IF @MaterialLotId IS NULL
   BEGIN
    INSERT MES.FVN_MaterialLot(SiteCode,PartNo,LotNo,LotType,ManufacturedDate,LotStatus,SourceSystem,SourceReference)
    VALUES(@Site,@Part,@Lot,CASE WHEN @IsFinishedProduct=1 THEN ''FG'' ELSE ''WIP'' END,
     @ProdDate,''AVAILABLE'',''MES'',N''PRODUCTION_ORDER:''+CONVERT(nvarchar(30),@ProductionOrderId));
    SET @MaterialLotId=SCOPE_IDENTITY();
   END
   ELSE IF NOT EXISTS(SELECT 1 FROM MES.FVN_MaterialLot WHERE MaterialLotId=@MaterialLotId
      AND LotType=CASE WHEN @IsFinishedProduct=1 THEN ''FG'' ELSE ''WIP'' END)
    THROW 53423,''Existing canonical LOT has a conflicting LOT type.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionLot WHERE MaterialLotId=@MaterialLotId)
   BEGIN
    SELECT @ProductionLotId=ProductionLotId FROM MES.FVN_ProductionLot WHERE MaterialLotId=@MaterialLotId;
    IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionLot WHERE ProductionLotId=@ProductionLotId
       AND ProductionOrderId=@ProductionOrderId AND GoodQty=@GoodQty AND NgQty=@NgQty)
     THROW 53424,''Production LOT already exists with different order or quantities.'',1;
   END
   ELSE
   BEGIN
    INSERT MES.FVN_ProductionLot(SiteCode,ProductionOrderId,MaterialLotId,IsFinishedProduct,GoodQty,NgQty,ProducedAtUtc,CreatedBy)
    VALUES(@Site,@ProductionOrderId,@MaterialLotId,@IsFinishedProduct,@GoodQty,@NgQty,@ProducedAtUtc,@CreatedBy);
    SET @ProductionLotId=SCOPE_IDENTITY();
   END;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionLotTraceIndex WHERE ProductionLotId=@ProductionLotId)
    INSERT MES.FVN_ProductionLotTraceIndex(ProductionLotId,ProductionOrderId,MaterialLotId)
    VALUES(@ProductionLotId,@ProductionOrderId,@MaterialLotId);
   UPDATE MES.FVN_ProductionOrder SET Status=''RUNNING'' WHERE ProductionOrderId=@ProductionOrderId;
   COMMIT;
   SELECT @ProductionLotId ProductionLotId,@MaterialLotId MaterialLotId,@Lot LotNo,@GoodQty GoodQty,@NgQty NgQty;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionOperation_Complete
  @ProductionLotId bigint,@OperationNo nvarchar(40),@InputQty decimal(19,6),
  @GoodQty decimal(19,6),@NgQty decimal(19,6),@InputSlotId int=NULL,@OutputSlotId int=NULL,
  @OperatorCode nvarchar(100),@Remarks nvarchar(500)=NULL,@IdempotencyKey nvarchar(180)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @InputQty<=0 OR @GoodQty<0 OR @NgQty<0 OR @GoodQty+@NgQty>@InputQty
   THROW 53430,''Operation quantities are invalid.'',1;
  IF NULLIF(LTRIM(RTRIM(@OperatorCode)),N'''') IS NULL OR NULLIF(LTRIM(RTRIM(@IdempotencyKey)),N'''') IS NULL
   THROW 53431,''Operator and idempotency key are required.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @OrderId bigint,@Site nvarchar(30),@Part nvarchar(100),@RoutingId bigint,@StepId bigint,
    @Seq int,@NextStepId bigint,@Mode varchar(12),@TxnIn bigint,@TxnOut bigint,
    @LotId bigint,@OrderStatus varchar(20),@PlanLine nvarchar(30),@IsFinishedProduct bit,
    @IsFinalStep bit,@OperationMaterialLotId bigint,@OperationReferenceNo nvarchar(80),
    @InputIdempotencyKey nvarchar(180),@OutputIdempotencyKey nvarchar(180);
   SELECT @OrderId=l.ProductionOrderId,@Site=l.SiteCode,@Part=o.ProductPartNo,
    @OrderStatus=o.Status,@PlanLine=o.ProductionLineCode,@IsFinishedProduct=l.IsFinishedProduct
   FROM MES.FVN_ProductionLot l JOIN MES.FVN_ProductionOrder o ON o.ProductionOrderId=l.ProductionOrderId
   WHERE l.ProductionLotId=@ProductionLotId;
   IF @OrderId IS NULL THROW 53432,''Production LOT not found.'',1;
   IF @OrderStatus NOT IN(''RELEASED'',''RUNNING'') THROW 53433,''Production order is not executable.'',1;
   SELECT @RoutingId=o.RoutingId FROM MES.FVN_ProductionOrder o WHERE o.ProductionOrderId=@OrderId;
   IF @RoutingId IS NULL THROW 53434,''Production order has no snapshotted routing.'',1;
   SELECT @StepId=RoutingStepId,@Seq=SequenceNo,@Mode=TransferMode,@IsFinalStep=IsFinalOutput
   FROM MES.FVN_ProductionRoutingStep WHERE RoutingId=@RoutingId AND OperationNo=@OperationNo AND IsActive=1;
   IF @StepId IS NULL THROW 53435,''Operation is not part of the effective routing.'',1;
   SELECT @NextStepId=RoutingStepId FROM MES.FVN_ProductionRoutingStep
    WHERE RoutingId=@RoutingId AND SequenceNo=(SELECT MIN(SequenceNo) FROM MES.FVN_ProductionRoutingStep
      WHERE RoutingId=@RoutingId AND SequenceNo>@Seq AND IsActive=1) AND IsActive=1;
   IF (@NextStepId IS NULL AND @IsFinalStep=0) OR (@NextStepId IS NOT NULL AND @IsFinalStep=1)
    THROW 53445,''Routing final-operation flag does not match the configured sequence.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationExecution WHERE ProductionLotId=@ProductionLotId AND RoutingStepId=@StepId)
    THROW 53436,''This operation has already been completed for this production LOT.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionRoutingStep prev
     WHERE prev.RoutingId=@RoutingId AND prev.SequenceNo<@Seq AND prev.IsActive=1
      AND NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationExecution x
       WHERE x.ProductionLotId=@ProductionLotId AND x.RoutingStepId=prev.RoutingStepId))
    THROW 53437,''Previous operation has not been completed.'',1;
   IF @Seq>1
   BEGIN
    DECLARE @PrevMode varchar(12),@PrevExecutionId bigint;
    SELECT TOP(1) @PrevMode=ps.TransferMode,@PrevExecutionId=ex.OperationExecutionId
    FROM MES.FVN_ProductionRoutingStep ps
    JOIN MES.FVN_ProductionOperationExecution ex ON ex.RoutingStepId=ps.RoutingStepId AND ex.ProductionLotId=@ProductionLotId
    WHERE ps.RoutingId=@RoutingId AND ps.SequenceNo<@Seq ORDER BY ps.SequenceNo DESC;
    IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionOperationTransfer
      WHERE FromExecutionId=@PrevExecutionId AND ProductionLotId=@ProductionLotId
       AND Status=''PENDING'' AND TransferQty>=@InputQty)
     THROW 53444,''Operation handoff is missing or input quantity exceeds the transferred quantity.'',1;
    IF @PrevMode=''VIA_WIP''
    BEGIN
     IF @InputSlotId IS NULL THROW 53438,''Previous step requires WIP; input Slot is mandatory.'',1;
     IF NOT EXISTS(SELECT 1 FROM dbo.FVN_InventoryAreaSlot WHERE SiteCode=@Site AND SlotId=@InputSlotId AND AreaCode=''WIP'' AND IsActive=1)
      THROW 53439,''Input Slot is not configured as active WIP.'',1;
     SELECT @OperationMaterialLotId=MaterialLotId FROM MES.FVN_ProductionLot WHERE ProductionLotId=@ProductionLotId;
     SET @OperationReferenceNo=CONVERT(nvarchar(80),@ProductionLotId);
     SET @InputIdempotencyKey=@IdempotencyKey+N''-IN'';
     EXEC dbo.usp_FVN_PCTP_PostStockMovement @SiteCode=@Site,@PartNo=@Part,
      @MaterialLotId=@OperationMaterialLotId,@MovementType=''ISSUE'',@Quantity=@InputQty,@ReferenceType=''PRODUCTION_OPERATION'',
      @IdempotencyKey=@InputIdempotencyKey,@ActorCode=@OperatorCode,@InventoryTransactionId=@TxnIn OUTPUT,
      @FromSlotId=@InputSlotId,@FromStockState=''WIP'',@ReferenceNo=@OperationReferenceNo,
      @ReferenceLineNo=@Seq,@TransactionAtUtc=NULL;
    END
    ELSE IF @InputSlotId IS NOT NULL THROW 53440,''Previous step is DIRECT; do not issue from WIP.'',1;
   END
   IF ((@NextStepId IS NOT NULL AND @Mode=''VIA_WIP'') OR (@NextStepId IS NULL AND @IsFinishedProduct=0))
   BEGIN
    IF @OutputSlotId IS NULL THROW 53441,''This operation requires output WIP Slot.'',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.FVN_InventoryAreaSlot WHERE SiteCode=@Site AND SlotId=@OutputSlotId AND AreaCode=''WIP'' AND IsActive=1)
     THROW 53442,''Output Slot is not configured as active WIP.'',1;
    SELECT @OperationMaterialLotId=MaterialLotId FROM MES.FVN_ProductionLot WHERE ProductionLotId=@ProductionLotId;
    SET @OperationReferenceNo=CONVERT(nvarchar(80),@ProductionLotId);
    IF @GoodQty>0
    BEGIN
     SET @OutputIdempotencyKey=@IdempotencyKey+N''-OUT'';
     EXEC dbo.usp_FVN_PCTP_PostStockMovement @SiteCode=@Site,@PartNo=@Part,
      @MaterialLotId=@OperationMaterialLotId,@MovementType=''PROD_RECEIPT'',@Quantity=@GoodQty,@ReferenceType=''OPERATION_WIP_OUTPUT'',
      @IdempotencyKey=@OutputIdempotencyKey,@ActorCode=@OperatorCode,@InventoryTransactionId=@TxnOut OUTPUT,
      @ToSlotId=@OutputSlotId,@ToStockState=''WIP'',@ReferenceNo=@OperationReferenceNo,
      @ReferenceLineNo=@Seq,@TransactionAtUtc=NULL;
    END;
   END
   ELSE IF @OutputSlotId IS NOT NULL THROW 53443,''This operation does not require a WIP output Slot.'',1;
   INSERT MES.FVN_ProductionOperationExecution(ProductionLotId,RoutingStepId,InputQty,GoodQty,NgQty,
    InputSlotId,OutputSlotId,InputInventoryTransactionId,OutputInventoryTransactionId,
    CompletedAtUtc,OperatorCode,Remarks)
   VALUES(@ProductionLotId,@StepId,@InputQty,@GoodQty,@NgQty,@InputSlotId,@OutputSlotId,
    @TxnIn,@TxnOut,SYSUTCDATETIME(),@OperatorCode,@Remarks);
   SET @LotId=SCOPE_IDENTITY();
   IF @Seq>1
   BEGIN
    UPDATE tr SET Status=''RECEIVED'',ReceivedQty=@InputQty,ReceivedAtUtc=SYSUTCDATETIME(),ReceivedBy=@OperatorCode
    FROM MES.FVN_ProductionOperationTransfer tr
    JOIN MES.FVN_ProductionOperationExecution prev ON prev.OperationExecutionId=tr.FromExecutionId
    WHERE tr.ProductionLotId=@ProductionLotId
      AND (@PrevMode=''DIRECT'' OR tr.FromSlotId=@InputSlotId)
      AND prev.RoutingStepId IN(
      SELECT RoutingStepId FROM MES.FVN_ProductionRoutingStep WHERE RoutingId=@RoutingId AND SequenceNo<@Seq
       AND SequenceNo=(SELECT MAX(SequenceNo) FROM MES.FVN_ProductionRoutingStep WHERE RoutingId=@RoutingId AND SequenceNo<@Seq))
      AND tr.Status=''PENDING'';
   END;
   IF @NextStepId IS NOT NULL AND @GoodQty>0
    INSERT MES.FVN_ProductionOperationTransfer(ProductionLotId,FromExecutionId,ToRoutingStepId,
     TransferMode,TransferQty,FromSlotId,InventoryTransactionId,Status,HandedOffAtUtc,HandedOffBy)
    VALUES(@ProductionLotId,@LotId,@NextStepId,@Mode,@GoodQty,@OutputSlotId,@TxnOut,''PENDING'',SYSUTCDATETIME(),@OperatorCode);
   UPDATE MES.FVN_ProductionLot SET FinalGoodQty=@GoodQty,FinalNgQty=ISNULL(FinalNgQty,0)+@NgQty
    WHERE ProductionLotId=@ProductionLotId;
   IF @NextStepId IS NULL
    UPDATE o SET Status=''COMPLETED'',CompletedQty=@GoodQty
    FROM MES.FVN_ProductionOrder o WHERE o.ProductionOrderId=@OrderId;
   UPDATE MES.FVN_ProductionLotTraceIndex SET LastOperationExecutionId=@LotId,UpdatedAtUtc=SYSUTCDATETIME()
    WHERE ProductionLotId=@ProductionLotId;
   COMMIT;
   SELECT @LotId OperationExecutionId,@TxnIn InputInventoryTransactionId,@TxnOut OutputInventoryTransactionId;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionMaterial_Consume
  @ProductionOrderMaterialId bigint,@ProductionLotId bigint,@MaterialLotId bigint,
  @FromSlotId int,@Quantity decimal(19,6),@OperatorCode nvarchar(100),
  @IdempotencyKey nvarchar(180),@QrRawValue nvarchar(1000)=NULL
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @Quantity<=0 THROW 53450,''Consumption quantity must be positive.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @OrderId bigint,@Site nvarchar(30),@ComponentPart nvarchar(100),@FinishedLotId bigint,
    @FinishedPart nvarchar(100),@Uom nvarchar(40),@Txn bigint,@Event bigint,@StockState varchar(16),
    @ProductionLotReference nvarchar(80);
   SELECT @OrderId=m.ProductionOrderId,@Site=m.SiteCode,@ComponentPart=m.ComponentPartNo,@Uom=m.UomCode
   FROM MES.FVN_ProductionOrderMaterial m WITH(UPDLOCK,HOLDLOCK)
   WHERE m.ProductionOrderMaterialId=@ProductionOrderMaterialId;
   IF @OrderId IS NULL THROW 53451,''Production order material line not found.'',1;
   SELECT @FinishedLotId=l.MaterialLotId,@FinishedPart=o.ProductPartNo
   FROM MES.FVN_ProductionLot l JOIN MES.FVN_ProductionOrder o ON o.ProductionOrderId=l.ProductionOrderId
   WHERE l.ProductionLotId=@ProductionLotId AND l.ProductionOrderId=@OrderId;
   IF @FinishedLotId IS NULL THROW 53452,''Production LOT does not belong to the selected order.'',1;
   SELECT @StockState=CASE WHEN a.AreaCode IN(''PART'',''FG'') THEN ''AVAILABLE'' WHEN a.AreaCode=''WIP'' THEN ''WIP'' END
   FROM dbo.FVN_InventoryAreaSlot a WHERE a.SiteCode=@Site AND a.SlotId=@FromSlotId AND a.IsActive=1;
   IF @StockState IS NULL THROW 53453,''Source Slot must be configured as PART or WIP.'',1;
   SET @ProductionLotReference=CONVERT(nvarchar(80),@ProductionLotId);
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_InventoryBalance b
     JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=b.SiteCode AND sm.LocationNo=b.LocationNo
      AND sm.SlotId=@FromSlotId AND sm.IsPrimary=1 AND sm.IsActive=1
     WHERE b.SiteCode=@Site AND b.PartNo=@ComponentPart AND b.MaterialLotId=@MaterialLotId
      AND b.StockState=@StockState AND b.OnHandQty-b.ReservedQty>=@Quantity)
    THROW 53454,''Selected component LOT does not have sufficient stock in the selected Slot.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_MaterialExecutionEvent WHERE IdempotencyKey=@IdempotencyKey)
    THROW 53455,''Consumption idempotency key has already been used.'',1;
   EXEC dbo.usp_FVN_PCTP_PostStockMovement @SiteCode=@Site,@PartNo=@ComponentPart,
    @MaterialLotId=@MaterialLotId,@MovementType=''ISSUE'',@Quantity=@Quantity,
    @ReferenceType=''PRODUCTION_CONSUME'',@IdempotencyKey=@IdempotencyKey,
    @ActorCode=@OperatorCode,@InventoryTransactionId=@Txn OUTPUT,@FromSlotId=@FromSlotId,
    @FromStockState=@StockState,@ReferenceNo=@ProductionLotReference,
    @ReferenceLineNo=@ProductionOrderMaterialId,@TransactionAtUtc=NULL;
   INSERT MES.FVN_MaterialExecutionEvent(ProductionOrderMaterialId,InventoryTransactionId,EventType,
    EventAtUtc,OperatorCode,QrRawValue,IdempotencyKey)
   VALUES(@ProductionOrderMaterialId,@Txn,''CONSUME'',SYSUTCDATETIME(),@OperatorCode,@QrRawValue,@IdempotencyKey);
   SET @Event=SCOPE_IDENTITY();
   INSERT MES.FVN_FinishedLotGenealogy(SiteCode,ProductionOrderId,FinishedLotId,ComponentLotId,
    MaterialExecutionEventId,ConsumedQty,UomCode)
   VALUES(@Site,@OrderId,@FinishedLotId,@MaterialLotId,@Event,@Quantity,@Uom);
   COMMIT;
   SELECT @Event MaterialExecutionEventId,@Txn InventoryTransactionId,@FinishedLotId FinishedLotId,
    @MaterialLotId ComponentLotId,@Quantity ConsumedQty;
  END TRY
  BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ComponentLot_Trace
  @LotNo nvarchar(120),@Direction varchar(12)=''UPSTREAM'',@SiteCode nvarchar(30)=NULL
 AS
 BEGIN
  SET NOCOUNT ON;
  IF @Direction NOT IN(''UPSTREAM'',''DOWNSTREAM'') THROW 53460,''Direction must be UPSTREAM or DOWNSTREAM.'',1;
  DECLARE @RootLotId bigint,@RootCount int;
  SELECT @RootCount=COUNT(*) FROM MES.FVN_MaterialLot
   WHERE LotNo=@LotNo AND (@SiteCode IS NULL OR SiteCode=@SiteCode);
  IF @RootCount=0 THROW 53461,''LOT was not found in the MES canonical LOT master.'',1;
  IF @RootCount>1 THROW 53462,''LOT exists at multiple Sites; provide SiteCode to disambiguate.'',1;
  SELECT @RootLotId=MaterialLotId FROM MES.FVN_MaterialLot
   WHERE LotNo=@LotNo AND (@SiteCode IS NULL OR SiteCode=@SiteCode);
  ;WITH Trace AS(
   SELECT 0 AS LevelNo,@RootLotId AS RootLotId,
    CAST(NULL AS bigint) AS FromLotId, @RootLotId AS ToLotId,
    CAST(NULL AS decimal(19,6)) AS ConsumedQty,CAST(''ROOT'' AS varchar(12)) AS RelationType,
    CAST(NULL AS bigint) AS ProductionOrderId,CAST(NULL AS bigint) AS MaterialExecutionEventId,
    CAST(''|''+CONVERT(varchar(30),@RootLotId)+''|'' AS varchar(max)) AS Path
   UNION ALL
   SELECT t.LevelNo+1,t.RootLotId,
    CASE WHEN @Direction=''UPSTREAM'' THEN g.FinishedLotId ELSE g.ComponentLotId END,
    CASE WHEN @Direction=''UPSTREAM'' THEN g.ComponentLotId ELSE g.FinishedLotId END,
    g.ConsumedQty,CAST(''CONSUMED'' AS varchar(12)),g.ProductionOrderId,g.MaterialExecutionEventId,
    CAST(t.Path+CONVERT(varchar(30),CASE WHEN @Direction=''UPSTREAM'' THEN g.ComponentLotId ELSE g.FinishedLotId END)+''|'' AS varchar(max))
   FROM Trace t
   JOIN MES.FVN_FinishedLotGenealogy g ON
    ( (@Direction=''UPSTREAM'' AND g.FinishedLotId=t.ToLotId)
      OR (@Direction=''DOWNSTREAM'' AND g.ComponentLotId=t.ToLotId) )
   WHERE t.LevelNo<50
    AND CHARINDEX(''|''+CONVERT(varchar(30),CASE WHEN @Direction=''UPSTREAM'' THEN g.ComponentLotId ELSE g.FinishedLotId END)+''|'',t.Path)=0
  )
  SELECT t.LevelNo,t.RelationType,
   f.LotNo AS FromLotNo,fp.PartNo AS FromPartNo,
   z.LotNo AS ToLotNo,zp.PartNo AS ToPartNo,t.ConsumedQty,
   po.OrderNo,pm.OperationNo,ev.EventAtUtc,ev.OperatorCode,
   f.SiteCode AS FromSiteCode,z.SiteCode AS ToSiteCode,z.LotType AS ToLotType,z.LotStatus AS ToLotStatus,
   componentLot.SupplierCode,supplier.SupplierName,
   CASE WHEN z.LotType IN(''WIP'',''FG'') THEN ''MANUFACTURED'' ELSE ''PURCHASED_OR_EXTERNAL'' END AS ToLotSource
  FROM Trace t
  LEFT JOIN MES.FVN_MaterialLot f ON f.MaterialLotId=t.FromLotId
  LEFT JOIN MES.FVN_MaterialLot z ON z.MaterialLotId=t.ToLotId
  LEFT JOIN MES.FVN_ProductionOrder po ON po.ProductionOrderId=t.ProductionOrderId
  LEFT JOIN MES.FVN_IfsInventoryPart fp ON fp.SiteCode=f.SiteCode AND fp.PartNo=f.PartNo
  LEFT JOIN MES.FVN_IfsInventoryPart zp ON zp.SiteCode=z.SiteCode AND zp.PartNo=z.PartNo
  LEFT JOIN MES.FVN_ComponentLot componentLot ON componentLot.MaterialLotId=
   CASE WHEN @Direction=''UPSTREAM'' THEN z.MaterialLotId ELSE f.MaterialLotId END
  LEFT JOIN MES.FVN_SupplierIdentity supplier ON supplier.SupplierCode=componentLot.SupplierCode
  LEFT JOIN MES.FVN_MaterialExecutionEvent ev ON ev.MaterialExecutionEventId=t.MaterialExecutionEventId
  LEFT JOIN MES.FVN_ProductionOrderMaterial pm ON pm.ProductionOrderMaterialId=ev.ProductionOrderMaterialId
  ORDER BY t.LevelNo,t.FromLotId,t.ToLotId
  OPTION(MAXRECURSION 50);
 END');

 EXEC(N'
 CREATE VIEW dbo.vFVN_MES_ProductionReceiptWorklist
 AS
 SELECT
  CONVERT(int,pl.ProductionLotId) AS STT,
  ml.LotNo AS FIND,ml.LotNo AS LOT_NO,
  CAST(NULL AS nvarchar(100)) AS MODEL,ip.Description AS TEN_SAN_PHAM,
  ml.PartNo AS MA_SAN_PHAM,
  TRY_CONVERT(int,LEFT(ISNULL(o.ShiftCode,N''''),1)) AS CA_SAN_XUAT,
  ISNULL(o.ProductionDate,ml.ManufacturedDate) AS NGAY_SAN_XUAT,
  CONVERT(int,CASE WHEN ISNULL(pl.FinalGoodQty,pl.GoodQty)>2147483647 THEN 2147483647 ELSE ISNULL(pl.FinalGoodQty,pl.GoodQty) END) AS SL_DA_SAN_XUAT,
  CONVERT(int,ISNULL(rcv.ReceivedQty,0)) AS SL_DA_NHAP,
  CONVERT(int,0) AS SL_DA_TRA,CAST(NULL AS nvarchar(250)) AS LY_DO_TRA,
  CONVERT(int,ISNULL(bal.OnHandQty,0)) AS TON_KHO_TP,
  CASE WHEN ISNULL(rcv.ReceivedQty,0)>=ISNULL(pl.FinalGoodQty,pl.GoodQty) THEN 1 ELSE 0 END AS KET_THUC_LOT,
  o.SiteCode,o.OrderNo,pl.ProductionLotId,pl.MaterialLotId,ISNULL(pl.FinalGoodQty,pl.GoodQty) GoodQty,ISNULL(pl.FinalNgQty,pl.NgQty) NgQty,
  ml.LotType,ml.LotStatus
 FROM MES.FVN_ProductionLot pl
 JOIN MES.FVN_MaterialLot ml ON ml.MaterialLotId=pl.MaterialLotId
 JOIN MES.FVN_ProductionOrder o ON o.ProductionOrderId=pl.ProductionOrderId
 LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=ml.SiteCode AND ip.PartNo=ml.PartNo
 OUTER APPLY(SELECT SUM(b.OnHandQty) AS OnHandQty
  FROM MES.FVN_InventoryBalance b WHERE b.SiteCode=ml.SiteCode AND b.PartNo=ml.PartNo
   AND b.MaterialLotId=ml.MaterialLotId AND b.StockState=''AVAILABLE'') bal
 OUTER APPLY(SELECT SUM(t.Quantity) AS ReceivedQty
  FROM MES.FVN_InventoryTransaction t WHERE t.SiteCode=ml.SiteCode AND t.PartNo=ml.PartNo
   AND t.MaterialLotId=ml.MaterialLotId AND t.MovementType=''PROD_RECEIPT''
   AND t.ReferenceType=''NHAP_TP'') rcv
 WHERE pl.IsFinishedProduct=1 AND o.Status=''COMPLETED'';');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('034',N'Production execution routing, preserved LOT genealogy and bidirectional traceability');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO
