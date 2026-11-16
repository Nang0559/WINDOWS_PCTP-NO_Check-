/*
 Migration 008: reservation lifecycle and actual-consumption genealogy posting.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='007') THROW 51000,'Run migration 007 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='008') RETURN;

IF OBJECT_ID(N'MES.usp_ReserveMaterial',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_ReserveMaterial
 @ProductionOrderMaterialId bigint,@MaterialLotId bigint,@LocationNo nvarchar(100),
 @Quantity decimal(19,6),@IdempotencyKey nvarchar(180),@CreatedBy nvarchar(100),
 @MaterialReservationId bigint OUTPUT
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Quantity<=0 THROW 51800,''Reservation quantity must be positive.'',1;
 IF NULLIF(LTRIM(RTRIM(@CreatedBy)),N'''') IS NULL THROW 51806,''CreatedBy is required.'',1;
 IF NULLIF(LTRIM(RTRIM(@IdempotencyKey)),N'''') IS NULL THROW 51804,''Reservation IdempotencyKey is required.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  SELECT @MaterialReservationId=MaterialReservationId
   FROM MES.MaterialReservation WITH(UPDLOCK,HOLDLOCK) WHERE IdempotencyKey=@IdempotencyKey;
  IF @MaterialReservationId IS NOT NULL
  BEGIN
   IF EXISTS(SELECT 1 FROM MES.MaterialReservation WHERE MaterialReservationId=@MaterialReservationId
    AND (ProductionOrderMaterialId<>@ProductionOrderMaterialId OR MaterialLotId<>@MaterialLotId
     OR LocationNo<>@LocationNo OR ReservedQty<>@Quantity))
    THROW 51805,''Reservation IdempotencyKey was reused with different input.'',1;
   COMMIT; RETURN;
  END;
  DECLARE @SiteCode nvarchar(30),@PartNo nvarchar(100);
  SELECT @SiteCode=SiteCode,@PartNo=ComponentPartNo
   FROM MES.ProductionOrderMaterial WITH(HOLDLOCK)
   WHERE ProductionOrderMaterialId=@ProductionOrderMaterialId;
  IF @SiteCode IS NULL THROW 51801,''Production order material not found.'',1;
  IF NOT EXISTS(SELECT 1 FROM MES.MaterialLot WHERE MaterialLotId=@MaterialLotId AND SiteCode=@SiteCode AND PartNo=@PartNo AND LotStatus IN(''RECEIVED'',''AVAILABLE''))
   THROW 51802,''Lot is not available for this production component.'',1;
  UPDATE MES.InventoryBalance WITH(UPDLOCK,HOLDLOCK)
   SET ReservedQty=ReservedQty+@Quantity,UpdatedAtUtc=SYSUTCDATETIME()
   WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@MaterialLotId
    AND LocationNo=@LocationNo AND StockState=''AVAILABLE''
    AND OnHandQty-ReservedQty>=@Quantity;
  IF @@ROWCOUNT<>1 THROW 51803,''Insufficient available stock to reserve.'',1;
  INSERT MES.MaterialReservation(ProductionOrderMaterialId,SiteCode,PartNo,MaterialLotId,LocationNo,
   StockState,ReservedQty,IdempotencyKey,CreatedBy)
  VALUES(@ProductionOrderMaterialId,@SiteCode,@PartNo,@MaterialLotId,@LocationNo,''AVAILABLE'',@Quantity,@IdempotencyKey,@CreatedBy);
  SET @MaterialReservationId=SCOPE_IDENTITY();
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END');

IF OBJECT_ID(N'MES.usp_ConsumeMaterialReservation',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_ConsumeMaterialReservation
 @MaterialReservationId bigint,@Quantity decimal(19,6),@EventAtUtc datetime2(3),
 @OperatorCode nvarchar(100),@IdempotencyKey nvarchar(180),
 @InventoryTransactionId bigint OUTPUT,@MaterialExecutionEventId bigint OUTPUT,
 @QrRawValue nvarchar(1000)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Quantity<=0 THROW 51810,''Consumption quantity must be positive.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  SELECT @InventoryTransactionId=InventoryTransactionId
   FROM MES.InventoryTransaction WITH(UPDLOCK,HOLDLOCK) WHERE IdempotencyKey=@IdempotencyKey;
  IF @InventoryTransactionId IS NOT NULL
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM MES.InventoryTransaction WHERE InventoryTransactionId=@InventoryTransactionId
     AND MovementType=''ISSUE'' AND Quantity=@Quantity)
    THROW 51814,''Consumption IdempotencyKey was reused with different input.'',1;
   SELECT @MaterialExecutionEventId=MaterialExecutionEventId
    FROM MES.MaterialExecutionEvent WHERE InventoryTransactionId=@InventoryTransactionId AND EventType=''CONSUME'';
   IF @MaterialExecutionEventId IS NULL THROW 51815,''Existing issue transaction has no consumption event.'',1;
   COMMIT; RETURN;
  END;
  DECLARE @ProductionOrderMaterialId bigint,@SiteCode nvarchar(30),@PartNo nvarchar(100),
   @MaterialLotId bigint,@LocationNo nvarchar(100),@Remaining decimal(19,6);
  SELECT @ProductionOrderMaterialId=ProductionOrderMaterialId,@SiteCode=SiteCode,@PartNo=PartNo,
   @MaterialLotId=MaterialLotId,@LocationNo=LocationNo,
   @Remaining=ReservedQty-ConsumedQty-ReleasedQty
  FROM MES.MaterialReservation WITH(UPDLOCK,HOLDLOCK)
  WHERE MaterialReservationId=@MaterialReservationId AND Status IN(''ACTIVE'',''PARTIAL'');
  IF @ProductionOrderMaterialId IS NULL THROW 51811,''Active material reservation not found.'',1;
  IF @Quantity>@Remaining THROW 51812,''Consumption exceeds remaining reservation.'',1;
  UPDATE MES.InventoryBalance WITH(UPDLOCK,HOLDLOCK)
   SET OnHandQty=OnHandQty-@Quantity,ReservedQty=ReservedQty-@Quantity,UpdatedAtUtc=SYSUTCDATETIME()
   WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@MaterialLotId
    AND LocationNo=@LocationNo AND StockState=''AVAILABLE''
    AND OnHandQty>=@Quantity AND ReservedQty>=@Quantity;
  IF @@ROWCOUNT<>1 THROW 51813,''Balance does not contain the reserved quantity.'',1;
  INSERT MES.InventoryTransaction(SiteCode,PartNo,MaterialLotId,MovementType,Quantity,UomCode,
   FromLocationNo,FromStockState,ReferenceType,ReferenceNo,IdempotencyKey,TransactionAtUtc,ActorCode)
  SELECT @SiteCode,@PartNo,@MaterialLotId,''ISSUE'',@Quantity,om.UomCode,
   @LocationNo,''AVAILABLE'',''PRODUCTION'',po.OrderNo,@IdempotencyKey,
   ISNULL(@EventAtUtc,SYSUTCDATETIME()),@OperatorCode
  FROM MES.ProductionOrderMaterial om
  JOIN MES.ProductionOrder po ON po.ProductionOrderId=om.ProductionOrderId
  WHERE om.ProductionOrderMaterialId=@ProductionOrderMaterialId;
  SET @InventoryTransactionId=SCOPE_IDENTITY();
  INSERT MES.MaterialExecutionEvent(ProductionOrderMaterialId,InventoryTransactionId,EventType,
   EventAtUtc,OperatorCode,QrRawValue,IdempotencyKey)
  VALUES(@ProductionOrderMaterialId,@InventoryTransactionId,''CONSUME'',
   ISNULL(@EventAtUtc,SYSUTCDATETIME()),@OperatorCode,@QrRawValue,@IdempotencyKey);
  SET @MaterialExecutionEventId=SCOPE_IDENTITY();
  UPDATE MES.MaterialReservation
   SET ConsumedQty=ConsumedQty+@Quantity,
    Status=CASE WHEN ConsumedQty+@Quantity+ReleasedQty=ReservedQty
                THEN CASE WHEN ConsumedQty+@Quantity=ReservedQty THEN ''CONSUMED'' ELSE ''RELEASED'' END
                ELSE ''PARTIAL'' END
   WHERE MaterialReservationId=@MaterialReservationId;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END');

IF OBJECT_ID(N'MES.usp_ReleaseMaterialReservation',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_ReleaseMaterialReservation
 @MaterialReservationId bigint,@ReleasedBy nvarchar(100)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @SiteCode nvarchar(30),@PartNo nvarchar(100),@LotId bigint,@LocationNo nvarchar(100),
   @Remaining decimal(19,6),@CurrentStatus varchar(16);
  SELECT @SiteCode=SiteCode,@PartNo=PartNo,@LotId=MaterialLotId,@LocationNo=LocationNo,
   @Remaining=ReservedQty-ConsumedQty-ReleasedQty,@CurrentStatus=Status
  FROM MES.MaterialReservation WITH(UPDLOCK,HOLDLOCK)
  WHERE MaterialReservationId=@MaterialReservationId;
  IF @CurrentStatus=''RELEASED'' BEGIN COMMIT; RETURN; END;
  IF @SiteCode IS NULL OR @CurrentStatus NOT IN(''ACTIVE'',''PARTIAL'') THROW 51820,''Active reservation not found.'',1;
  IF @Remaining>0
  BEGIN
   UPDATE MES.InventoryBalance WITH(UPDLOCK,HOLDLOCK)
    SET ReservedQty=ReservedQty-@Remaining,UpdatedAtUtc=SYSUTCDATETIME()
    WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@LotId
     AND LocationNo=@LocationNo AND StockState=''AVAILABLE'' AND ReservedQty>=@Remaining;
   IF @@ROWCOUNT<>1 THROW 51821,''Reserved balance reconciliation failed.'',1;
  END;
  UPDATE MES.MaterialReservation
   SET ReleasedQty=ReleasedQty+@Remaining,Status=''RELEASED'',ReleasedAtUtc=SYSUTCDATETIME(),ReleasedBy=@ReleasedBy
   WHERE MaterialReservationId=@MaterialReservationId;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END');

IF OBJECT_ID(N'MES.usp_LinkConsumedLotToFinishedLot',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_LinkConsumedLotToFinishedLot
 @FinishedLotId bigint,@MaterialExecutionEventId bigint,@ConsumedQty decimal(19,6),@UomCode nvarchar(30)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @ConsumedQty<=0 THROW 51830,''Genealogy quantity must be positive.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @ComponentLotId bigint,@ProductionOrderId bigint,@EventQty decimal(19,6),
   @SiteCode nvarchar(30),@FinishedSite nvarchar(30),@FinishedType varchar(12);
  SELECT @ComponentLotId=it.MaterialLotId,@EventQty=it.Quantity,@ProductionOrderId=po.ProductionOrderId,
   @SiteCode=it.SiteCode
  FROM MES.MaterialExecutionEvent ee
  JOIN MES.InventoryTransaction it ON it.InventoryTransactionId=ee.InventoryTransactionId
  JOIN MES.ProductionOrderMaterial om ON om.ProductionOrderMaterialId=ee.ProductionOrderMaterialId
  JOIN MES.ProductionOrder po ON po.ProductionOrderId=om.ProductionOrderId
  WHERE ee.MaterialExecutionEventId=@MaterialExecutionEventId AND ee.EventType=''CONSUME'';
  SELECT @FinishedSite=SiteCode,@FinishedType=LotType
   FROM MES.MaterialLot WHERE MaterialLotId=@FinishedLotId;
  IF @ComponentLotId IS NULL THROW 51831,''Consumption event not found.'',1;
  IF @FinishedSite IS NULL OR @FinishedType<>''FG'' OR @FinishedSite<>@SiteCode
   THROW 51832,''Finished lot must be an FG lot in the same site.'',1;
  IF @ConsumedQty>@EventQty THROW 51833,''Genealogy quantity exceeds the consumption event.'',1;
  IF EXISTS(SELECT 1 FROM MES.FinishedLotGenealogy WITH(UPDLOCK,HOLDLOCK)
   WHERE MaterialExecutionEventId=@MaterialExecutionEventId AND FinishedLotId=@FinishedLotId AND ComponentLotId=@ComponentLotId)
   UPDATE MES.FinishedLotGenealogy SET ConsumedQty=ConsumedQty+@ConsumedQty
    WHERE MaterialExecutionEventId=@MaterialExecutionEventId AND FinishedLotId=@FinishedLotId AND ComponentLotId=@ComponentLotId;
  ELSE
   INSERT MES.FinishedLotGenealogy(SiteCode,ProductionOrderId,FinishedLotId,ComponentLotId,
    MaterialExecutionEventId,ConsumedQty,UomCode)
   VALUES(@SiteCode,@ProductionOrderId,@FinishedLotId,@ComponentLotId,
    @MaterialExecutionEventId,@ConsumedQty,@UomCode);
  IF EXISTS(SELECT 1 FROM MES.FinishedLotGenealogy WHERE MaterialExecutionEventId=@MaterialExecutionEventId GROUP BY MaterialExecutionEventId HAVING SUM(ConsumedQty)>@EventQty)
   THROW 51834,''Total genealogy allocation exceeds the consumption event quantity.'',1;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END');

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('008',N'Material reservation and actual genealogy procedures');
GO
