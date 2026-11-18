/*
 Migration 033: production-by-shift planning, IFS BOM demand, PART -> WIP issue.
 SQL Server 2016. IFS remains read-only; all planning and material issue documents are MES-owned.
 Requires MES migrations 000..032 and IFS BOM sync 012.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53300,'Migration 033 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53301,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='032')
 THROW 53302,'Migration 032 must be applied before 033.',1;
IF OBJECT_ID(N'IFS.FVN_BomHeader',N'U') IS NULL OR OBJECT_ID(N'IFS.FVN_BomAlternative',N'U') IS NULL
 OR OBJECT_ID(N'IFS.FVN_BomComponent',N'U') IS NULL
 THROW 53303,'Synchronized IFS BOM header/alternative/component snapshots are required.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='033') RETURN;
BEGIN TRY
 BEGIN TRANSACTION;

 IF OBJECT_ID(N'MES.FVN_ProductionShift',N'U') IS NULL
 CREATE TABLE MES.FVN_ProductionShift(
  SiteCode nvarchar(30) NOT NULL, ShiftCode nvarchar(20) NOT NULL,
  ShiftName nvarchar(100) NOT NULL, StartTime time(0) NOT NULL, EndTime time(0) NOT NULL,
  CrossesMidnight bit NOT NULL CONSTRAINT DF_FVN_ProductionShift_Cross DEFAULT(0),
  IsActive bit NOT NULL CONSTRAINT DF_FVN_ProductionShift_Active DEFAULT(1),
  CONSTRAINT PK_FVN_ProductionShift PRIMARY KEY(SiteCode,ShiftCode),
  CONSTRAINT FK_FVN_ProductionShift_Site FOREIGN KEY(SiteCode) REFERENCES dbo.FVN_PCTPSite(SiteCode),
  CONSTRAINT CK_FVN_ProductionShift_Time CHECK(StartTime<>EndTime)
 );

 IF COL_LENGTH(N'MES.FVN_ProductionPlanLine',N'ShiftCode') IS NULL
  ALTER TABLE MES.FVN_ProductionPlanLine ADD ShiftCode nvarchar(20) NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionPlanLine',N'ProductionLineCode') IS NULL
  ALTER TABLE MES.FVN_ProductionPlanLine ADD ProductionLineCode nvarchar(30) NULL;
 IF COL_LENGTH(N'MES.FVN_ProductionPlanLine',N'ProductionDate') IS NULL
  ALTER TABLE MES.FVN_ProductionPlanLine ADD ProductionDate date NULL;
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_FVN_PlanLine_Shift')
  ALTER TABLE MES.FVN_ProductionPlanLine WITH NOCHECK ADD CONSTRAINT FK_FVN_PlanLine_Shift
   FOREIGN KEY(SiteCode,ShiftCode) REFERENCES MES.FVN_ProductionShift(SiteCode,ShiftCode);

 IF OBJECT_ID(N'dbo.FVN_InventoryAreaSlot',N'U') IS NULL
 CREATE TABLE dbo.FVN_InventoryAreaSlot(
  SiteCode nvarchar(30) NOT NULL, SlotId int NOT NULL, AreaCode varchar(8) NOT NULL,
  IsActive bit NOT NULL CONSTRAINT DF_FVN_InventoryAreaSlot_Active DEFAULT(1),
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_InventoryAreaSlot_Created DEFAULT(SYSUTCDATETIME()),
  CreatedBy nvarchar(100) NULL,
  CONSTRAINT PK_FVN_InventoryAreaSlot PRIMARY KEY(SiteCode,SlotId),
  CONSTRAINT FK_FVN_InventoryAreaSlot_Slot FOREIGN KEY(SiteCode,SlotId) REFERENCES dbo.FVN_Slot(SiteCode,SlotId),
  CONSTRAINT CK_FVN_InventoryAreaSlot_Area CHECK(AreaCode IN('PART','WIP'))
 );

 IF OBJECT_ID(N'MES.FVN_ComponentDemandHeader',N'U') IS NULL
 CREATE TABLE MES.FVN_ComponentDemandHeader(
  ComponentDemandId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ComponentDemandHeader PRIMARY KEY,
  SiteCode nvarchar(30) NOT NULL, ProductionPlanLineId bigint NOT NULL,
  ProductPartNo nvarchar(100) NOT NULL, PlannedQty decimal(19,6) NOT NULL,
  BomEngChgLevel nvarchar(24) NOT NULL, BomTypeDb nvarchar(80) NOT NULL, AlternativeNo nvarchar(80) NOT NULL,
  Status varchar(16) NOT NULL CONSTRAINT DF_FVN_ComponentDemand_Status DEFAULT('OPEN'),
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ComponentDemand_Created DEFAULT(SYSUTCDATETIME()),
  CreatedBy nvarchar(100) NOT NULL,
  CONSTRAINT UQ_FVN_ComponentDemand_PlanLine UNIQUE(ProductionPlanLineId),
  CONSTRAINT FK_FVN_ComponentDemand_PlanLine FOREIGN KEY(ProductionPlanLineId) REFERENCES MES.FVN_ProductionPlanLine(ProductionPlanLineId),
  CONSTRAINT CK_FVN_ComponentDemand_Status CHECK(Status IN('OPEN','PARTIAL','FULFILLED','CANCELLED'))
 );

 IF OBJECT_ID(N'MES.FVN_ComponentDemandLine',N'U') IS NULL
 CREATE TABLE MES.FVN_ComponentDemandLine(
  ComponentDemandLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ComponentDemandLine PRIMARY KEY,
  ComponentDemandId bigint NOT NULL, SiteCode nvarchar(30) NOT NULL, ComponentPartNo nvarchar(100) NOT NULL,
  RequiredQty decimal(19,6) NOT NULL, PartAvailableQty decimal(19,6) NOT NULL CONSTRAINT DF_FVN_Demand_PartAvail DEFAULT(0),
  NetRequiredQty decimal(19,6) NOT NULL, IssuedQty decimal(19,6) NOT NULL CONSTRAINT DF_FVN_Demand_Issued DEFAULT(0),
  UomCode nvarchar(40) NOT NULL, OperationNo nvarchar(40) NULL,
  CONSTRAINT FK_FVN_ComponentDemandLine_Header FOREIGN KEY(ComponentDemandId) REFERENCES MES.FVN_ComponentDemandHeader(ComponentDemandId),
  CONSTRAINT FK_FVN_ComponentDemandLine_Part FOREIGN KEY(SiteCode,ComponentPartNo) REFERENCES IFS.FVN_InventoryPart(SiteCode,PartNo),
  CONSTRAINT UQ_FVN_ComponentDemandLine_Part UNIQUE(ComponentDemandId,ComponentPartNo),
  CONSTRAINT CK_FVN_ComponentDemandLine_Qty CHECK(RequiredQty>0 AND PartAvailableQty>=0 AND NetRequiredQty>=0 AND IssuedQty>=0)
 );

 IF OBJECT_ID(N'MES.FVN_ComponentIssue',N'U') IS NULL
 CREATE TABLE MES.FVN_ComponentIssue(
  ComponentIssueId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ComponentIssue PRIMARY KEY,
  SiteCode nvarchar(30) NOT NULL, IssueNo nvarchar(80) NOT NULL,
  ComponentDemandId bigint NOT NULL, Status varchar(16) NOT NULL CONSTRAINT DF_FVN_ComponentIssue_Status DEFAULT('POSTED'),
  IssuedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ComponentIssue_Date DEFAULT(SYSUTCDATETIME()),
  IssuedBy nvarchar(100) NOT NULL,
  CONSTRAINT UQ_FVN_ComponentIssue_No UNIQUE(SiteCode,IssueNo),
  CONSTRAINT FK_FVN_ComponentIssue_Demand FOREIGN KEY(ComponentDemandId) REFERENCES MES.FVN_ComponentDemandHeader(ComponentDemandId),
  CONSTRAINT CK_FVN_ComponentIssue_Status CHECK(Status IN('POSTED','CANCELLED'))
 );

 IF OBJECT_ID(N'MES.FVN_ComponentIssueLine',N'U') IS NULL
 CREATE TABLE MES.FVN_ComponentIssueLine(
  ComponentIssueLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_ComponentIssueLine PRIMARY KEY,
  ComponentIssueId bigint NOT NULL, ComponentDemandLineId bigint NOT NULL,
  MaterialLotId bigint NOT NULL, PartNo nvarchar(100) NOT NULL, Quantity decimal(19,6) NOT NULL,
  FromSlotId int NOT NULL, ToSlotId int NOT NULL, InventoryTransactionId bigint NOT NULL,
  IdempotencyKey nvarchar(180) NOT NULL,
  CONSTRAINT UQ_FVN_ComponentIssueLine_Idempotency UNIQUE(IdempotencyKey),
  CONSTRAINT FK_FVN_ComponentIssueLine_Header FOREIGN KEY(ComponentIssueId) REFERENCES MES.FVN_ComponentIssue(ComponentIssueId),
  CONSTRAINT FK_FVN_ComponentIssueLine_Demand FOREIGN KEY(ComponentDemandLineId) REFERENCES MES.FVN_ComponentDemandLine(ComponentDemandLineId),
  CONSTRAINT FK_FVN_ComponentIssueLine_Lot FOREIGN KEY(MaterialLotId) REFERENCES MES.FVN_MaterialLot(MaterialLotId),
  CONSTRAINT FK_FVN_ComponentIssueLine_Txn FOREIGN KEY(InventoryTransactionId) REFERENCES MES.FVN_InventoryTransaction(InventoryTransactionId),
  CONSTRAINT CK_FVN_ComponentIssueLine_Qty CHECK(Quantity>0)
 );

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionPlan_ImportRow
  @SiteCode nvarchar(30),@PlanNo nvarchar(80),@PlanDate date,@LineNo int,
  @ShiftCode nvarchar(20),@ProductionLineCode nvarchar(30),@ProductPartNo nvarchar(100),
  @PlannedQty decimal(19,6),@UomCode nvarchar(40),@PlannedStartUtc datetime2(0)=NULL,
  @PlannedEndUtc datetime2(0)=NULL,@SourceFileName nvarchar(260)=NULL,@CreatedBy nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @LineNo<=0 OR @PlannedQty<=0 THROW 53310,''Line number and planned quantity must be positive.'',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode=@SiteCode AND IsActive=1)
   THROW 53311,''Site is not registered or inactive for PCTP.'',1;
  IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionShift WHERE SiteCode=@SiteCode AND ShiftCode=@ShiftCode AND IsActive=1)
   THROW 53312,''ShiftCode is not configured for this Site.'',1;
  IF NOT EXISTS(SELECT 1 FROM IFS.FVN_InventoryPart WHERE SiteCode=@SiteCode AND PartNo=@ProductPartNo AND UnitMeas=@UomCode)
   THROW 53313,''Product part or UOM does not match the IFS site master.'',1;
  IF @PlannedEndUtc IS NOT NULL AND @PlannedStartUtc IS NOT NULL AND @PlannedEndUtc<@PlannedStartUtc
   THROW 53314,''Plan end time must be after start time.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @PlanId bigint;
   SELECT @PlanId=ProductionPlanId FROM MES.FVN_ProductionPlan WITH(UPDLOCK,HOLDLOCK)
    WHERE SiteCode=@SiteCode AND PlanNo=@PlanNo;
   IF @PlanId IS NULL
   BEGIN
    INSERT MES.FVN_ProductionPlan(SiteCode,PlanNo,PlanDate,SourceFileName,Status,CreatedBy)
    VALUES(@SiteCode,@PlanNo,@PlanDate,@SourceFileName,''IMPORTED'',@CreatedBy);
    SET @PlanId=SCOPE_IDENTITY();
   END
   ELSE IF EXISTS(SELECT 1 FROM MES.FVN_ProductionPlan WHERE ProductionPlanId=@PlanId AND PlanDate<>@PlanDate)
    THROW 53315,''PlanNo already exists with a different PlanDate.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ProductionPlanLine WHERE ProductionPlanId=@PlanId AND [LineNo]=@LineNo)
    THROW 53316,''Duplicate LineNo in this production plan.'',1;
   INSERT MES.FVN_ProductionPlanLine(ProductionPlanId,[LineNo],SiteCode,ProductPartNo,PlannedQty,UomCode,
    PlannedStartUtc,PlannedEndUtc,ShiftCode,ProductionLineCode,ProductionDate)
   VALUES(@PlanId,@LineNo,@SiteCode,@ProductPartNo,@PlannedQty,@UomCode,
    @PlannedStartUtc,@PlannedEndUtc,@ShiftCode,@ProductionLineCode,@PlanDate);
   COMMIT;
   SELECT @PlanId AS ProductionPlanId;
  END TRY
  BEGIN CATCH
   IF XACT_STATE()<>0 ROLLBACK;
   THROW;
  END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_GenerateComponentDemand
  @ProductionPlanLineId bigint,@CreatedBy nvarchar(100)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  DECLARE @SiteCode nvarchar(30),@ProductPartNo nvarchar(100),@PlanQty decimal(19,6),@PlanDate date;
  SELECT @SiteCode=l.SiteCode,@ProductPartNo=l.ProductPartNo,@PlanQty=l.PlannedQty,
   @PlanDate=ISNULL(l.ProductionDate,p.PlanDate)
  FROM MES.FVN_ProductionPlanLine l JOIN MES.FVN_ProductionPlan p ON p.ProductionPlanId=l.ProductionPlanId
  WHERE l.ProductionPlanLineId=@ProductionPlanLineId;
  IF @SiteCode IS NULL THROW 53320,''Production plan line not found.'',1;
  IF EXISTS(SELECT 1 FROM MES.FVN_ComponentDemandHeader WHERE ProductionPlanLineId=@ProductionPlanLineId)
   THROW 53321,''Component demand has already been generated for this plan line.'',1;
  DECLARE @BomCount int;
  SELECT @BomCount=COUNT(*)
  FROM IFS.FVN_BomHeader h
  JOIN IFS.FVN_BomAlternative a ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo
   AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
  WHERE h.SiteCode=@SiteCode AND h.PartNo=@ProductPartNo
   AND h.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
   AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)));
  IF @BomCount=0 THROW 53322,''No effective IFS BOM alternative exists for the planned product/date.'',1;
  IF @BomCount>1 THROW 53323,''More than one effective IFS BOM alternative exists; engineering selection is required.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @EngChgLevel nvarchar(24),@BomTypeDb nvarchar(80),@AlternativeNo nvarchar(80),@DemandId bigint;
   SELECT TOP(1) @EngChgLevel=h.EngChgLevel,@BomTypeDb=h.BomTypeDb,@AlternativeNo=a.AlternativeNo
   FROM IFS.FVN_BomHeader h JOIN IFS.FVN_BomAlternative a
    ON a.SiteCode=h.SiteCode AND a.PartNo=h.PartNo AND a.EngChgLevel=h.EngChgLevel AND a.BomTypeDb=h.BomTypeDb
   WHERE h.SiteCode=@SiteCode AND h.PartNo=@ProductPartNo
    AND h.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
    AND (h.EffectiveOutDate IS NULL OR h.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)));
   INSERT MES.FVN_ComponentDemandHeader(SiteCode,ProductionPlanLineId,ProductPartNo,PlannedQty,
    BomEngChgLevel,BomTypeDb,AlternativeNo,CreatedBy)
   VALUES(@SiteCode,@ProductionPlanLineId,@ProductPartNo,@PlanQty,@EngChgLevel,@BomTypeDb,@AlternativeNo,@CreatedBy);
   SET @DemandId=SCOPE_IDENTITY();

   ;WITH Required AS(
    SELECT c.ComponentPartNo,
     SUM(CONVERT(decimal(19,6),c.QtyPerAssembly*@PlanQty)) AS RequiredQty,
     MAX(CONVERT(nvarchar(40),ip.UnitMeas)) AS UomCode,
     MAX(CONVERT(nvarchar(40),c.OperationNo)) AS OperationNo
    FROM IFS.FVN_BomComponent c
    JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=c.ComponentContract AND ip.PartNo=c.ComponentPartNo
    WHERE c.SiteCode=@SiteCode AND c.PartNo=@ProductPartNo
     AND c.EngChgLevel=@EngChgLevel AND c.BomTypeDb=@BomTypeDb AND c.AlternativeNo=@AlternativeNo
     AND c.EffectiveInDate<=CAST(@PlanDate AS datetime2(0))
     AND (c.EffectiveOutDate IS NULL OR c.EffectiveOutDate>=CAST(@PlanDate AS datetime2(0)))
     AND c.QtyPerAssembly>0
    GROUP BY c.ComponentPartNo
   )
   INSERT MES.FVN_ComponentDemandLine(ComponentDemandId,SiteCode,ComponentPartNo,RequiredQty,
    PartAvailableQty,NetRequiredQty,UomCode,OperationNo)
   SELECT @DemandId,@SiteCode,r.ComponentPartNo,r.RequiredQty,
    ISNULL(stock.AvailableQty,0),
    CASE WHEN r.RequiredQty-ISNULL(stock.AvailableQty,0)>0 THEN r.RequiredQty-ISNULL(stock.AvailableQty,0) ELSE 0 END,
    r.UomCode,r.OperationNo
   FROM Required r
   OUTER APPLY(
    SELECT SUM(b.OnHandQty-b.ReservedQty) AS AvailableQty
    FROM MES.FVN_InventoryBalance b
    JOIN MES.FVN_IfsInventoryLocation loc ON loc.SiteCode=b.SiteCode AND loc.LocationNo=b.LocationNo
    JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=loc.SiteCode AND sm.LocationNo=loc.LocationNo AND sm.IsPrimary=1 AND sm.IsActive=1
    JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode=sm.SiteCode AND ar.SlotId=sm.SlotId AND ar.AreaCode=''PART'' AND ar.IsActive=1
    WHERE b.SiteCode=@SiteCode AND b.PartNo=r.ComponentPartNo AND b.StockState=''AVAILABLE''
   ) stock;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentDemandLine WHERE ComponentDemandId=@DemandId)
    THROW 53324,''IFS BOM has no usable inventory component rows for this Site.'',1;
   COMMIT;
   SELECT @DemandId AS ComponentDemandId,COUNT(*) AS ComponentCount
   FROM MES.FVN_ComponentDemandLine WHERE ComponentDemandId=@DemandId;
  END TRY
  BEGIN CATCH
   IF XACT_STATE()<>0 ROLLBACK;
   THROW;
  END CATCH
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ComponentIssue_Post
  @SiteCode nvarchar(30),@IssueNo nvarchar(80),@ComponentDemandLineId bigint,
  @MaterialLotId bigint,@FromSlotId int,@ToSlotId int,@Quantity decimal(19,6),
  @IssuedBy nvarchar(100),@IdempotencyKey nvarchar(180)
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @Quantity<=0 THROW 53330,''Issue quantity must be positive.'',1;
  IF NULLIF(LTRIM(RTRIM(@IssuedBy)),N'''') IS NULL THROW 53331,''IssuedBy is required.'',1;
  BEGIN TRY
   BEGIN TRANSACTION;
   IF EXISTS(SELECT 1 FROM MES.FVN_ComponentIssueLine WHERE IdempotencyKey=@IdempotencyKey)
   BEGIN
    IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentIssueLine
      WHERE IdempotencyKey=@IdempotencyKey AND ComponentDemandLineId=@ComponentDemandLineId
       AND MaterialLotId=@MaterialLotId AND FromSlotId=@FromSlotId AND ToSlotId=@ToSlotId AND Quantity=@Quantity)
     THROW 53337,''IdempotencyKey was reused with different component issue input.'',1;
    SELECT ComponentIssueLineId,InventoryTransactionId,Quantity FROM MES.FVN_ComponentIssueLine WHERE IdempotencyKey=@IdempotencyKey;
    COMMIT; RETURN;
   END;
   DECLARE @DemandId bigint,@PartNo nvarchar(100),@Outstanding decimal(19,6),@TxnId bigint,
    @IssueId bigint,@LineNo int,@Uom nvarchar(40);
   SELECT @DemandId=ComponentDemandId,@PartNo=ComponentPartNo,@Outstanding=NetRequiredQty-IssuedQty
   FROM MES.FVN_ComponentDemandLine WITH(UPDLOCK,HOLDLOCK)
   WHERE ComponentDemandLineId=@ComponentDemandLineId AND SiteCode=@SiteCode;
   IF @DemandId IS NULL THROW 53332,''Demand line was not found for Site.'',1;
   IF @Quantity>@Outstanding THROW 53333,''Issue quantity exceeds outstanding net demand.'',1;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_InventoryBalance b
     JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode=b.SiteCode AND sm.LocationNo=b.LocationNo
      AND sm.SlotId=@FromSlotId AND sm.IsPrimary=1 AND sm.IsActive=1
     JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode=sm.SiteCode AND ar.SlotId=sm.SlotId
      AND ar.AreaCode=''PART'' AND ar.IsActive=1
     WHERE b.SiteCode=@SiteCode AND b.PartNo=@PartNo AND b.MaterialLotId=@MaterialLotId
      AND b.StockState=''AVAILABLE'' AND b.OnHandQty-b.ReservedQty>=@Quantity)
    THROW 53334,''Source PART Slot/LOT has insufficient available stock.'',1;
   IF NOT EXISTS(SELECT 1 FROM dbo.FVN_SlotMesLocation sm
     JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode=sm.SiteCode AND ar.SlotId=sm.SlotId
     WHERE sm.SiteCode=@SiteCode AND sm.SlotId=@ToSlotId AND sm.IsPrimary=1 AND sm.IsActive=1
      AND ar.AreaCode=''WIP'' AND ar.IsActive=1)
    THROW 53335,''Destination slot is not mapped to the WIP area.'',1;
   SELECT @Uom=UnitMeas FROM IFS.FVN_InventoryPart WHERE SiteCode=@SiteCode AND PartNo=@PartNo;
   SELECT @IssueId=ComponentIssueId FROM MES.FVN_ComponentIssue WITH(UPDLOCK,HOLDLOCK)
    WHERE SiteCode=@SiteCode AND IssueNo=@IssueNo;
   IF @IssueId IS NULL
   BEGIN
    INSERT MES.FVN_ComponentIssue(SiteCode,IssueNo,ComponentDemandId,IssuedBy)
    VALUES(@SiteCode,@IssueNo,@DemandId,@IssuedBy);
    SET @IssueId=SCOPE_IDENTITY();
   END
   ELSE IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentIssue WHERE ComponentIssueId=@IssueId AND ComponentDemandId=@DemandId)
    THROW 53336,''IssueNo already belongs to a different demand.'',1;
   EXEC dbo.usp_FVN_PCTP_PostStockMovement
    @SiteCode=@SiteCode,@PartNo=@PartNo,@MaterialLotId=@MaterialLotId,@MovementType=''MOVE'',
    @Quantity=@Quantity,@ReferenceType=''COMPONENT_TO_WIP'',@IdempotencyKey=@IdempotencyKey,
    @ActorCode=@IssuedBy,@InventoryTransactionId=@TxnId OUTPUT,@FromSlotId=@FromSlotId,
    @ToSlotId=@ToSlotId,@FromStockState=''AVAILABLE'',@ToStockState=''WIP'',
    @ReferenceNo=@IssueNo,@ReferenceLineNo=@ComponentDemandLineId,@TransactionAtUtc=NULL;
   INSERT MES.FVN_ComponentIssueLine(ComponentIssueId,ComponentDemandLineId,MaterialLotId,PartNo,
    Quantity,FromSlotId,ToSlotId,InventoryTransactionId,IdempotencyKey)
   VALUES(@IssueId,@ComponentDemandLineId,@MaterialLotId,@PartNo,@Quantity,
    @FromSlotId,@ToSlotId,@TxnId,@IdempotencyKey);
   UPDATE MES.FVN_ComponentDemandLine SET IssuedQty=IssuedQty+@Quantity
    WHERE ComponentDemandLineId=@ComponentDemandLineId;
   UPDATE h SET Status=CASE WHEN NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentDemandLine x
      WHERE x.ComponentDemandId=h.ComponentDemandId AND x.IssuedQty<x.NetRequiredQty)
     THEN ''FULFILLED'' WHEN EXISTS(SELECT 1 FROM MES.FVN_ComponentDemandLine x
      WHERE x.ComponentDemandId=h.ComponentDemandId AND x.IssuedQty>0) THEN ''PARTIAL'' ELSE h.Status END
    FROM MES.FVN_ComponentDemandHeader h WHERE h.ComponentDemandId=@DemandId;
   COMMIT;
   SELECT @IssueId AS ComponentIssueId,@TxnId AS InventoryTransactionId,@Quantity AS IssuedQty;
  END TRY
  BEGIN CATCH
   IF XACT_STATE()<>0 ROLLBACK;
   THROW;
  END CATCH
 END');


 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ProductionPlan_List
  @SiteCode nvarchar(30)=NULL,@FromDate date=NULL,@ToDate date=NULL
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT l.ProductionPlanLineId,p.ProductionPlanId,p.SiteCode,p.PlanNo,
   ISNULL(l.ProductionDate,p.PlanDate) AS ProductionDate,l.ShiftCode,l.ProductionLineCode,
   l.ProductPartNo,ip.Description AS ProductDescription,l.PlannedQty,l.UomCode,
   l.PlannedStartUtc,l.PlannedEndUtc,p.Status
  FROM MES.FVN_ProductionPlanLine l
  JOIN MES.FVN_ProductionPlan p ON p.ProductionPlanId=l.ProductionPlanId
  LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=l.SiteCode AND ip.PartNo=l.ProductPartNo
  WHERE (@SiteCode IS NULL OR p.SiteCode=@SiteCode)
   AND (@FromDate IS NULL OR ISNULL(l.ProductionDate,p.PlanDate)>=@FromDate)
   AND (@ToDate IS NULL OR ISNULL(l.ProductionDate,p.PlanDate)<=@ToDate)
   AND p.Status NOT IN(''CANCELLED'',''CLOSED'')
  ORDER BY ISNULL(l.ProductionDate,p.PlanDate),p.SiteCode,l.ShiftCode,l.ProductionLineCode,p.PlanNo,l.[LineNo];
 END');

 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ComponentDemand_List
  @ProductionPlanLineId bigint
 AS
 BEGIN
  SET NOCOUNT ON;
  SELECT h.ComponentDemandId,h.Status AS DemandStatus,l.ComponentDemandLineId,
   l.ComponentPartNo,ip.Description AS ComponentDescription,l.RequiredQty,
   l.PartAvailableQty,l.NetRequiredQty,l.IssuedQty,
   CASE WHEN l.NetRequiredQty-l.IssuedQty>0 THEN l.NetRequiredQty-l.IssuedQty ELSE 0 END AS OutstandingQty,
   l.UomCode,l.OperationNo
  FROM MES.FVN_ComponentDemandHeader h
  JOIN MES.FVN_ComponentDemandLine l ON l.ComponentDemandId=h.ComponentDemandId
  LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode=l.SiteCode AND ip.PartNo=l.ComponentPartNo
  WHERE h.ProductionPlanLineId=@ProductionPlanLineId
  ORDER BY l.ComponentPartNo;
 END');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('033',N'Production shifts, IFS BOM component demand and PART to WIP issue');
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
GO
