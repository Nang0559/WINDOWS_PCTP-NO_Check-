/*
 Migration 062: receiving and label-log hardening.

 1. MES.usp_FVN_ComponentReceipt_Post (replaces the 032 version; same signature and result set):
    - a request whose IdempotencyKey was already posted with the SAME site / QR / quantity now returns the original
      document (receipt, line, inventory transaction) instead of failing, so a client that lost the connection after the
      commit can safely scan again. Before, the retry failed with 53227 (lot already received) or 53231.
    - the same key with different content is rejected (53233).
    - the duplicate check runs before the quantity check and is repeated after the lot lock, so two concurrent identical
      requests produce one posting and one replay.
 2. MES.usp_FVN_ComponentPO_LabelLog (replaces the 061 version): INITIAL_PRINT is recorded once per LOT (a second user or a
    double click no longer adds a second first-print row); REPRINT always records. Serialized per PO. Returns the number
    of rows really logged.
 Apply after migration 061. SQL Server 2016 SP1+ (CREATE OR ALTER).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54600, 'Migration 062 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '061')
    THROW 54601, 'Migration 061 must be applied before 062.', 1;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentReceipt_Post
 @SiteCode nvarchar(30),@ReceiptNo nvarchar(80),@QrPayload nvarchar(500),
 @Quantity decimal(19,6),@TargetSlotId int,@ReceivedBy nvarchar(100),
 @IdempotencyKey nvarchar(180),@ReceivedAtUtc datetime2(3)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@SiteCode)),N'') IS NULL THROW 53220,'SiteCode is required.',1;
 IF NULLIF(LTRIM(RTRIM(@ReceiptNo)),N'') IS NULL THROW 53221,'ReceiptNo is required.',1;
 IF NULLIF(LTRIM(RTRIM(@QrPayload)),N'') IS NULL THROW 53222,'QR payload is required.',1;
 IF @Quantity<=0 THROW 53223,'Receive quantity must be positive.',1;
 IF @TargetSlotId IS NULL OR @TargetSlotId<=0 THROW 53224,'Target Slot is required.',1;
 IF NULLIF(LTRIM(RTRIM(@ReceivedBy)),N'') IS NULL THROW 53225,'ReceivedBy is required.',1;
 IF NULLIF(LTRIM(RTRIM(@IdempotencyKey)),N'') IS NULL THROW 53226,'IdempotencyKey is required.',1;
 SET @ReceivedAtUtc=ISNULL(@ReceivedAtUtc,SYSUTCDATETIME());
/* Replay of an already-posted request returns the original document; the same key with different content is rejected. */
IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey)
BEGIN
 IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey AND SiteCode=@SiteCode AND QrPayload=@QrPayload AND ReceivedQty=@Quantity)
 BEGIN
  SELECT TOP (1) ComponentReceiptId,ComponentReceiptLineId,InventoryTransactionId,ComponentLotId,MaterialLotId,ReceivedQty
  FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey;
  RETURN;
 END;
 THROW 53233,'This IdempotencyKey was already used by a different receiving request.',1;
END;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @ComponentLotId bigint,@MaterialLotId bigint,@PartNo nvarchar(100),
   @LotSite nvarchar(30),@IssuedQty decimal(18,3),@ReceivedQty decimal(18,3),
   @ReceiptId bigint,@ReceiptLineId bigint,@TxnId bigint,@SlotId int,@POId bigint;
  SELECT @ComponentLotId=ComponentLotId,@MaterialLotId=MaterialLotId,
   @PartNo=PartNo,@LotSite=SiteCode,@IssuedQty=IssuedQty,@ReceivedQty=ReceivedQty
  FROM MES.FVN_ComponentLot WITH(UPDLOCK,HOLDLOCK)
  WHERE QrPayload=@QrPayload AND Status IN('ISSUED','PARTIALLY_RECEIVED');
  IF @ComponentLotId IS NULL
  BEGIN
   IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey AND SiteCode=@SiteCode AND QrPayload=@QrPayload AND ReceivedQty=@Quantity)
   BEGIN
    COMMIT TRANSACTION;
    SELECT TOP (1) ComponentReceiptId,ComponentReceiptLineId,InventoryTransactionId,ComponentLotId,MaterialLotId,ReceivedQty
  FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey;
    RETURN;
   END;
   THROW 53227,'QR is not a valid active MES component LOT label.',1;
  END;
  IF @LotSite<>@SiteCode THROW 53228,'QR SiteCode does not match receiving site.',1;
  IF @MaterialLotId IS NULL THROW 53229,'Component LOT is not linked to canonical MES MaterialLot.',1;
  IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey)
  BEGIN
   IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey AND SiteCode=@SiteCode AND QrPayload=@QrPayload AND ReceivedQty=@Quantity)
   BEGIN
    COMMIT TRANSACTION;
    SELECT TOP (1) ComponentReceiptId,ComponentReceiptLineId,InventoryTransactionId,ComponentLotId,MaterialLotId,ReceivedQty
  FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey;
    RETURN;
   END;
   THROW 53233,'This IdempotencyKey was already used by a different receiving request.',1;
  END;
  IF @ReceivedQty+@Quantity>@IssuedQty
   THROW 53230,'Receive quantity exceeds the remaining issued quantity.',1;
  
  SELECT @POId=h.PurchaseOrderId
  FROM MES.FVN_ComponentPurchaseOrder h
  JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderId=h.PurchaseOrderId
  WHERE l.PurchaseOrderLineId=(SELECT PurchaseOrderLineId FROM MES.FVN_ComponentLot WHERE ComponentLotId=@ComponentLotId);

  IF NOT EXISTS(SELECT 1 FROM dbo.FVN_SlotMesLocation
   WHERE SiteCode=@SiteCode AND SlotId=@TargetSlotId AND IsPrimary=1 AND IsActive=1)
   THROW 53232,'Target Slot has no active primary MES location mapping for this Site.',1;

  SELECT @ReceiptId=ComponentReceiptId FROM MES.FVN_ComponentReceipt WITH(UPDLOCK,HOLDLOCK)
   WHERE SiteCode=@SiteCode AND ReceiptNo=@ReceiptNo;
  IF @ReceiptId IS NULL
  BEGIN
   INSERT MES.FVN_ComponentReceipt(SiteCode,ReceiptNo,ReceivedAtUtc,ReceivedBy,Status)
   VALUES(@SiteCode,@ReceiptNo,@ReceivedAtUtc,@ReceivedBy,'POSTED');
   SET @ReceiptId=SCOPE_IDENTITY();
  END;

  INSERT MES.FVN_ComponentReceiptLine(ComponentReceiptId,ComponentLotId,MaterialLotId,
   SiteCode,PartNo,ReceivedQty,TargetSlotId,IdempotencyKey,QrPayload,ReceivedAtUtc)
  VALUES(@ReceiptId,@ComponentLotId,@MaterialLotId,@SiteCode,@PartNo,@Quantity,
   @TargetSlotId,@IdempotencyKey,@QrPayload,@ReceivedAtUtc);
  SET @ReceiptLineId=SCOPE_IDENTITY();

  EXEC dbo.usp_FVN_PCTP_PostStockMovement
   @SiteCode=@SiteCode,@PartNo=@PartNo,@MaterialLotId=@MaterialLotId,
   @MovementType='RECEIPT',@Quantity=@Quantity,@ReferenceType='COMPONENT_PO_RECEIPT',
   @IdempotencyKey=@IdempotencyKey,@ActorCode=@ReceivedBy,
   @InventoryTransactionId=@TxnId OUTPUT,@ToSlotId=@TargetSlotId,
   @ToStockState='AVAILABLE',@ReferenceNo=@ReceiptNo,
   @ReferenceLineNo=@ReceiptLineId,@TransactionAtUtc=@ReceivedAtUtc;

  UPDATE MES.FVN_ComponentReceiptLine SET InventoryTransactionId=@TxnId
   WHERE ComponentReceiptLineId=@ReceiptLineId;
  UPDATE MES.FVN_ComponentLot SET ReceivedQty=ReceivedQty+@Quantity,
   Status=CASE WHEN ReceivedQty+@Quantity>=IssuedQty THEN 'RECEIVED' ELSE 'PARTIALLY_RECEIVED' END
   WHERE ComponentLotId=@ComponentLotId;
  UPDATE MES.FVN_MaterialLot SET LotStatus='AVAILABLE'
   WHERE MaterialLotId=@MaterialLotId AND LotStatus IN('BLOCKED','RECEIVED');
  UPDATE l SET ReceivedQty=l.ReceivedQty+@Quantity
   FROM MES.FVN_ComponentPurchaseOrderLine AS l
   JOIN MES.FVN_ComponentLot AS c ON c.PurchaseOrderLineId=l.PurchaseOrderLineId
   WHERE c.ComponentLotId=@ComponentLotId;
  UPDATE h SET Status=CASE
     WHEN NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine x
       WHERE x.PurchaseOrderId=h.PurchaseOrderId AND x.ReceivedQty<x.OrderQty) THEN 'RECEIVED'
     WHEN EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine x
       WHERE x.PurchaseOrderId=h.PurchaseOrderId AND x.ReceivedQty>0) THEN 'PARTIALLY_RECEIVED'
     ELSE h.Status END,
     UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ReceivedBy
   FROM MES.FVN_ComponentPurchaseOrder h WHERE h.PurchaseOrderId=@POId;
  COMMIT TRANSACTION;
  SELECT @ReceiptId AS ComponentReceiptId,@ReceiptLineId AS ComponentReceiptLineId,
   @TxnId AS InventoryTransactionId,@ComponentLotId AS ComponentLotId,
   @MaterialLotId AS MaterialLotId,@Quantity AS ReceivedQty;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
 END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPO_LabelLog
    @PurchaseOrderId bigint,
    @ActionCode      varchar(20),
    @Reason          nvarchar(300) = NULL,
    @RequestedBy     nvarchar(100),
    @LotIdsJson      nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @ActionCode NOT IN ('INITIAL_PRINT', 'REPRINT') THROW 54521, 'ActionCode must be INITIAL_PRINT or REPRINT.', 1;
    IF @ActionCode = 'REPRINT' AND NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL THROW 54522, 'Reprint reason is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@RequestedBy)), N'') IS NULL THROW 54523, 'RequestedBy is required.', 1;
    DECLARE @Ids TABLE (ComponentLotId bigint PRIMARY KEY);
    INSERT @Ids SELECT DISTINCT CONVERT(bigint, [value]) FROM OPENJSON(@LotIdsJson);
    IF NOT EXISTS (SELECT 1 FROM @Ids) THROW 54524, 'No LOT to log.', 1;
    IF EXISTS (SELECT 1 FROM @Ids i WHERE NOT EXISTS (
                   SELECT 1 FROM MES.FVN_ComponentLot c
                     JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = c.PurchaseOrderLineId
                    WHERE c.ComponentLotId = i.ComponentLotId AND l.PurchaseOrderId = @PurchaseOrderId AND c.Status <> 'CANCELLED'))
        THROW 54525, 'A LOT does not belong to this purchase order or is cancelled.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        /* Serialize on the PO so two users printing at once cannot both record the first print of the same LOT. */
        DECLARE @Lock int = (SELECT TOP (1) 1 FROM MES.FVN_ComponentPurchaseOrder WITH (UPDLOCK, HOLDLOCK) WHERE PurchaseOrderId = @PurchaseOrderId);
        INSERT MES.FVN_ComponentLotLabelHistory (ComponentLotId, ActionCode, CopyCount, Reason, RequestedBy)
        SELECT i.ComponentLotId, @ActionCode, 1, NULLIF(LTRIM(RTRIM(@Reason)), N''), @RequestedBy
          FROM @Ids i
         WHERE @ActionCode = 'REPRINT'
            OR NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentLotLabelHistory y
                            WHERE y.ComponentLotId = i.ComponentLotId AND y.ActionCode = 'INITIAL_PRINT');
        DECLARE @Logged int = @@ROWCOUNT;
        COMMIT TRANSACTION;
        SELECT @Logged AS LoggedLots;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '062')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('062', N'Component receipt idempotent replay, single INITIAL_PRINT per LOT');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '062';
GO
