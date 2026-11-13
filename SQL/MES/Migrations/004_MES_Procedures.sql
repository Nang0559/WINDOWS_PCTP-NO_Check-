/*
 Migration 004: controlled inventory posting, FIFO candidate query and trace procedures.
 The inventory transaction ledger is the sole stock movement source of truth.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='003') THROW 51000,'Run migration 003 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='004') RETURN;

IF OBJECT_ID(N'MES.usp_PostInventoryMovement',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_PostInventoryMovement
 @SiteCode nvarchar(30), @PartNo nvarchar(100), @MaterialLotId bigint,
 @MovementType varchar(20), @Quantity decimal(19,6), @UomCode nvarchar(30),
 @FromLocationNo nvarchar(100)=NULL, @ToLocationNo nvarchar(100)=NULL,
 @FromStockState varchar(16)=NULL, @ToStockState varchar(16)=NULL,
 @ReferenceType varchar(30), @ReferenceNo nvarchar(100)=NULL, @ReferenceLineNo int=NULL,
 @IdempotencyKey nvarchar(180), @TransactionAtUtc datetime2(3), @ActorCode nvarchar(100),
 @InventoryTransactionId bigint OUTPUT
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF @Quantity<=0 THROW 51100,''Quantity must be greater than zero.'',1;
 IF NULLIF(LTRIM(RTRIM(@IdempotencyKey)),N'''') IS NULL THROW 51101,''IdempotencyKey is required.'',1;
 IF NULLIF(LTRIM(RTRIM(@ActorCode)),N'''') IS NULL THROW 51102,''ActorCode is required.'',1;
 IF @MovementType NOT IN(''RECEIPT'',''MOVE'',''ISSUE'',''RETURN'',''SCRAP'',''ADJUST_IN'',''ADJUST_OUT'',''PROD_RECEIPT'',''QUARANTINE'',''RELEASE'')
  THROW 51103,''Unsupported movement type.'',1;
 IF @MovementType IN(''RECEIPT'',''RETURN'',''PROD_RECEIPT'',''ADJUST_IN'') AND @ToLocationNo IS NULL
  THROW 51104,''Destination location is required.'',1;
 IF @MovementType IN(''ISSUE'',''SCRAP'',''ADJUST_OUT'') AND @FromLocationNo IS NULL
  THROW 51105,''Source location is required.'',1;
 IF @MovementType IN(''MOVE'',''QUARANTINE'',''RELEASE'') AND (@FromLocationNo IS NULL OR @ToLocationNo IS NULL)
  THROW 51106,''Both source and destination locations are required.'',1;

 SET @FromStockState=ISNULL(@FromStockState,''AVAILABLE'');
 SET @ToStockState=ISNULL(@ToStockState,CASE WHEN @MovementType=''QUARANTINE'' THEN ''QUARANTINE'' ELSE ''AVAILABLE'' END);
 SET @TransactionAtUtc=ISNULL(@TransactionAtUtc,SYSUTCDATETIME());

 BEGIN TRY
  BEGIN TRANSACTION;
  SELECT @InventoryTransactionId=InventoryTransactionId
  FROM MES.InventoryTransaction WITH(UPDLOCK,HOLDLOCK)
  WHERE IdempotencyKey=@IdempotencyKey;
  IF @InventoryTransactionId IS NOT NULL
  BEGIN
   COMMIT TRANSACTION;
   RETURN;
  END;

  IF NOT EXISTS(SELECT 1 FROM MES.MaterialLot WITH(HOLDLOCK)
                WHERE MaterialLotId=@MaterialLotId AND SiteCode=@SiteCode AND PartNo=@PartNo)
   THROW 51107,''Material lot does not match SiteCode and PartNo.'',1;

  IF @MovementType IN(''ISSUE'',''SCRAP'',''ADJUST_OUT'',''MOVE'',''QUARANTINE'',''RELEASE'')
  BEGIN
   UPDATE MES.InventoryBalance WITH(UPDLOCK,HOLDLOCK)
      SET OnHandQty=OnHandQty-@Quantity, UpdatedAtUtc=SYSUTCDATETIME()
    WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@MaterialLotId
      AND LocationNo=@FromLocationNo AND StockState=@FromStockState
      AND OnHandQty-ReservedQty>=@Quantity;
   IF @@ROWCOUNT<>1 THROW 51108,''Insufficient available stock or source balance not found.'',1;
  END;

  IF @MovementType IN(''RECEIPT'',''RETURN'',''PROD_RECEIPT'',''ADJUST_IN'',''MOVE'',''QUARANTINE'',''RELEASE'')
  BEGIN
   UPDATE MES.InventoryBalance WITH(UPDLOCK,HOLDLOCK)
      SET OnHandQty=OnHandQty+@Quantity, UpdatedAtUtc=SYSUTCDATETIME()
    WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@MaterialLotId
      AND LocationNo=@ToLocationNo AND StockState=@ToStockState;
   IF @@ROWCOUNT=0
    INSERT MES.InventoryBalance(SiteCode,PartNo,MaterialLotId,LocationNo,StockState,OnHandQty,ReservedQty)
    VALUES(@SiteCode,@PartNo,@MaterialLotId,@ToLocationNo,@ToStockState,@Quantity,0);
  END;

  INSERT MES.InventoryTransaction
   (SiteCode,PartNo,MaterialLotId,MovementType,Quantity,UomCode,FromLocationNo,ToLocationNo,
    FromStockState,ToStockState,ReferenceType,ReferenceNo,ReferenceLineNo,IdempotencyKey,
    TransactionAtUtc,ActorCode)
  VALUES
   (@SiteCode,@PartNo,@MaterialLotId,@MovementType,@Quantity,@UomCode,@FromLocationNo,@ToLocationNo,
    CASE WHEN @FromLocationNo IS NULL THEN NULL ELSE @FromStockState END,
    CASE WHEN @ToLocationNo IS NULL THEN NULL ELSE @ToStockState END,
    @ReferenceType,@ReferenceNo,@ReferenceLineNo,@IdempotencyKey,@TransactionAtUtc,@ActorCode);
  SET @InventoryTransactionId=SCOPE_IDENTITY();
  COMMIT TRANSACTION;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
 END CATCH
END');

IF OBJECT_ID(N'MES.usp_GetFifoCandidates',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_GetFifoCandidates
 @SiteCode nvarchar(30), @PartNo nvarchar(100), @LocationNo nvarchar(100)=NULL,
 @AsOfDate date=NULL, @IncludeExpired bit=0
AS
BEGIN
 SET NOCOUNT ON;
 SET @AsOfDate=ISNULL(@AsOfDate,CONVERT(date,SYSUTCDATETIME()));
 SELECT l.MaterialLotId,l.LotNo,l.ManufacturedDate,l.ExpiryDate,
        b.LocationNo,b.OnHandQty,b.ReservedQty,
        b.OnHandQty-b.ReservedQty AS AvailableQty,
        p.InventoryUom
 FROM MES.InventoryBalance b
 JOIN MES.MaterialLot l ON l.SiteCode=b.SiteCode AND l.PartNo=b.PartNo AND l.MaterialLotId=b.MaterialLotId
 JOIN MES.IfsInventoryPart p ON p.SiteCode=b.SiteCode AND p.PartNo=b.PartNo
 WHERE b.SiteCode=@SiteCode AND b.PartNo=@PartNo
   AND (@LocationNo IS NULL OR b.LocationNo=@LocationNo)
   AND b.StockState=''AVAILABLE'' AND b.OnHandQty>b.ReservedQty
   AND l.LotStatus=''AVAILABLE''
   AND (@IncludeExpired=1 OR l.ExpiryDate IS NULL OR l.ExpiryDate>=@AsOfDate)
 ORDER BY CASE WHEN l.ExpiryDate IS NULL THEN 1 ELSE 0 END,
          l.ExpiryDate,
          CASE WHEN l.ManufacturedDate IS NULL THEN 1 ELSE 0 END,
          l.ManufacturedDate,l.MaterialLotId;
END');

IF OBJECT_ID(N'MES.usp_GetForwardGenealogy',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_GetForwardGenealogy
 @SiteCode nvarchar(30), @FinishedLotNo nvarchar(120)
AS
BEGIN
 SET NOCOUNT ON;
 SELECT fg.LotNo AS FinishedLotNo,fg.PartNo AS FinishedPartNo,
        cg.LotNo AS ComponentLotNo,cg.PartNo AS ComponentPartNo,
        g.ConsumedQty,g.UomCode,g.CreatedAtUtc,
        po.OrderNo,ee.EventAtUtc,ee.OperatorCode,
        it.ReferenceType,it.ReferenceNo,it.ReferenceLineNo,
        it.InventoryTransactionId
 FROM MES.MaterialLot fg
 JOIN MES.FinishedLotGenealogy g ON g.FinishedLotId=fg.MaterialLotId
 JOIN MES.MaterialLot cg ON cg.MaterialLotId=g.ComponentLotId
 JOIN MES.ProductionOrder po ON po.ProductionOrderId=g.ProductionOrderId
 JOIN MES.MaterialExecutionEvent ee ON ee.MaterialExecutionEventId=g.MaterialExecutionEventId
 JOIN MES.InventoryTransaction it ON it.InventoryTransactionId=ee.InventoryTransactionId
 WHERE fg.SiteCode=@SiteCode AND fg.LotNo=@FinishedLotNo
 ORDER BY ee.EventAtUtc,g.FinishedLotGenealogyId;
END');

IF OBJECT_ID(N'MES.usp_GetBackwardGenealogy',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_GetBackwardGenealogy
 @SiteCode nvarchar(30), @ComponentLotNo nvarchar(120)
AS
BEGIN
 SET NOCOUNT ON;
 SELECT cg.LotNo AS ComponentLotNo,cg.PartNo AS ComponentPartNo,
        fg.LotNo AS FinishedLotNo,fg.PartNo AS FinishedPartNo,
        g.ConsumedQty,g.UomCode,g.CreatedAtUtc,
        po.OrderNo,ee.EventAtUtc,ee.OperatorCode,
        it.ReferenceType,it.ReferenceNo,it.ReferenceLineNo,
        it.InventoryTransactionId
 FROM MES.MaterialLot cg
 JOIN MES.FinishedLotGenealogy g ON g.ComponentLotId=cg.MaterialLotId
 JOIN MES.MaterialLot fg ON fg.MaterialLotId=g.FinishedLotId
 JOIN MES.ProductionOrder po ON po.ProductionOrderId=g.ProductionOrderId
 JOIN MES.MaterialExecutionEvent ee ON ee.MaterialExecutionEventId=g.MaterialExecutionEventId
 JOIN MES.InventoryTransaction it ON it.InventoryTransactionId=ee.InventoryTransactionId
 WHERE cg.SiteCode=@SiteCode AND cg.LotNo=@ComponentLotNo
 ORDER BY ee.EventAtUtc,g.FinishedLotGenealogyId;
END');

IF OBJECT_ID(N'MES.usp_ApproveBomRevision',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_ApproveBomRevision
 @BomRevisionId bigint, @ApprovedBy nvarchar(100)
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@ApprovedBy)),N'''') IS NULL THROW 51200,''ApprovedBy is required.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  IF NOT EXISTS(SELECT 1 FROM MES.BomRevision WITH(UPDLOCK,HOLDLOCK)
                WHERE BomRevisionId=@BomRevisionId AND BomStatus IN(''DRAFT'',''PENDING''))
   THROW 51201,''BOM revision is not in an approvable state.'',1;
  IF NOT EXISTS(SELECT 1 FROM MES.BomComponent WHERE BomRevisionId=@BomRevisionId)
   THROW 51202,''Cannot approve an empty BOM.'',1;
  UPDATE MES.BomRevision
    SET BomStatus=''APPROVED'',ApprovedAtUtc=SYSUTCDATETIME(),ApprovedBy=@ApprovedBy
    WHERE BomRevisionId=@BomRevisionId;
  COMMIT TRANSACTION;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
 END CATCH
END');

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('004',N'Inventory posting, FIFO and genealogy procedures');
GO
