/*
 Migration 032: connect released component LOTs to the canonical MES material-lot
 and receive them through the existing PCTP -> MES inventory posting adapter.
 Prerequisites: migrations 000..031, including 025 and 031.
 SQL Server 2016 compatible. No direct inventory balance writes.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME()<>N'FVN_PCTP' THROW 53200,'Migration 032 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53201,'MES.FVN_SchemaMigration is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='031')
 THROW 53202,'Migration 031 must be applied before migration 032.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='025')
 THROW 53203,'Migration 025 stock posting adapter is required.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='032') RETURN;
IF OBJECT_ID(N'MES.FVN_MaterialLot',N'U') IS NULL
 OR OBJECT_ID(N'dbo.usp_FVN_PCTP_PostStockMovement',N'P') IS NULL
 OR OBJECT_ID(N'MES.FVN_ComponentLot',N'U') IS NULL
 OR OBJECT_ID(N'MES.FVN_ComponentPurchaseOrderLine',N'U') IS NULL
 THROW 53204,'Canonical MES LOT, component PO or stock adapter objects are missing.',1;

BEGIN TRY
 BEGIN TRANSACTION;

 IF COL_LENGTH(N'MES.FVN_ComponentLot',N'MaterialLotId') IS NULL
  ALTER TABLE MES.FVN_ComponentLot ADD MaterialLotId bigint NULL;

 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_MES_FVN_ComponentLot_MaterialLot')
  ALTER TABLE MES.FVN_ComponentLot WITH CHECK
   ADD CONSTRAINT FK_MES_FVN_ComponentLot_MaterialLot
   FOREIGN KEY(MaterialLotId) REFERENCES MES.FVN_MaterialLot(MaterialLotId);

 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FVN_ComponentLot')
               AND name=N'UX_MES_FVN_ComponentLot_MaterialLot')
  CREATE UNIQUE INDEX UX_MES_FVN_ComponentLot_MaterialLot
   ON MES.FVN_ComponentLot(MaterialLotId) WHERE MaterialLotId IS NOT NULL;

 IF OBJECT_ID(N'MES.FVN_ComponentReceipt',N'U') IS NULL
 CREATE TABLE MES.FVN_ComponentReceipt
 (
  ComponentReceiptId bigint IDENTITY(1,1) NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  ReceiptNo nvarchar(80) NOT NULL,
  ReceivedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceipt_Date DEFAULT(SYSUTCDATETIME()),
  ReceivedBy nvarchar(100) NOT NULL,
  Status varchar(16) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceipt_Status DEFAULT('POSTED'),
  CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceipt_Created DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT PK_MES_FVN_ComponentReceipt PRIMARY KEY(ComponentReceiptId),
  CONSTRAINT UQ_MES_FVN_ComponentReceipt UNIQUE(SiteCode,ReceiptNo),
  CONSTRAINT CK_MES_FVN_ComponentReceipt_Status CHECK(Status IN('POSTED','CANCELLED'))
 );

 IF OBJECT_ID(N'MES.FVN_ComponentReceiptLine',N'U') IS NULL
 CREATE TABLE MES.FVN_ComponentReceiptLine
 (
  ComponentReceiptLineId bigint IDENTITY(1,1) NOT NULL,
  ComponentReceiptId bigint NOT NULL,
  ComponentLotId bigint NOT NULL,
  MaterialLotId bigint NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  PartNo nvarchar(100) NOT NULL,
  ReceivedQty decimal(19,6) NOT NULL,
  TargetSlotId int NOT NULL,
  IdempotencyKey nvarchar(180) NOT NULL,
  InventoryTransactionId bigint NULL,
  QrPayload nvarchar(500) NOT NULL,
  ReceivedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceiptLine_Date DEFAULT(SYSUTCDATETIME()),
  CONSTRAINT PK_MES_FVN_ComponentReceiptLine PRIMARY KEY(ComponentReceiptLineId),
  CONSTRAINT UQ_MES_FVN_ComponentReceiptLine_Idempotency UNIQUE(IdempotencyKey),
  CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Header FOREIGN KEY(ComponentReceiptId)
   REFERENCES MES.FVN_ComponentReceipt(ComponentReceiptId),
  CONSTRAINT FK_MES_FVN_ComponentReceiptLine_ComponentLot FOREIGN KEY(ComponentLotId)
   REFERENCES MES.FVN_ComponentLot(ComponentLotId),
  CONSTRAINT FK_MES_FVN_ComponentReceiptLine_MaterialLot FOREIGN KEY(MaterialLotId)
   REFERENCES MES.FVN_MaterialLot(MaterialLotId),
  CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Txn FOREIGN KEY(InventoryTransactionId)
   REFERENCES MES.FVN_InventoryTransaction(InventoryTransactionId),
  CONSTRAINT CK_MES_FVN_ComponentReceiptLine_Qty CHECK(ReceivedQty>0)
 );
 CREATE INDEX IX_MES_FVN_ComponentReceiptLine_Lot
  ON MES.FVN_ComponentReceiptLine(ComponentLotId,ReceivedAtUtc);

 /* Backfill only if a component LOT was already issued by 031. */
 DECLARE @ComponentLotId bigint,@SiteCode nvarchar(30),@PartNo nvarchar(100),
         @LotNo nvarchar(120),@SupplierCode nvarchar(60),@MaterialLotId bigint;
 DECLARE lot_cursor CURSOR LOCAL FAST_FORWARD FOR
  SELECT ComponentLotId,SiteCode,PartNo,LotNo,SupplierCode
  FROM MES.FVN_ComponentLot WHERE MaterialLotId IS NULL;
 OPEN lot_cursor;
 FETCH NEXT FROM lot_cursor INTO @ComponentLotId,@SiteCode,@PartNo,@LotNo,@SupplierCode;
 WHILE @@FETCH_STATUS=0
 BEGIN
  SELECT @MaterialLotId=MaterialLotId FROM MES.FVN_MaterialLot
   WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND LotNo=@LotNo;
  IF @MaterialLotId IS NULL
  BEGIN
   INSERT MES.FVN_MaterialLot(SiteCode,PartNo,LotNo,LotType,SupplierLotNo,SupplierCode,
     LotStatus,SourceSystem,SourceReference)
   VALUES(@SiteCode,@PartNo,@LotNo,'RAW',@LotNo,@SupplierCode,'BLOCKED','MES',
     N'COMPONENT_PO:'+CONVERT(nvarchar(30),@ComponentLotId));
   SET @MaterialLotId=SCOPE_IDENTITY();
  END;
  UPDATE MES.FVN_ComponentLot SET MaterialLotId=@MaterialLotId
   WHERE ComponentLotId=@ComponentLotId;
  SET @MaterialLotId=NULL;
  FETCH NEXT FROM lot_cursor INTO @ComponentLotId,@SiteCode,@PartNo,@LotNo,@SupplierCode;
 END;
 CLOSE lot_cursor; DEALLOCATE lot_cursor;

 /* Replace release procedure: every released QR LOT is also a canonical MES material LOT. */
 EXEC(N'
 ALTER PROCEDURE MES.usp_FVN_ComponentPO_Release
  @PurchaseOrderId bigint,@ReleasedBy nvarchar(100),@LotIssueDate date=NULL
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF @PurchaseOrderId IS NULL OR @PurchaseOrderId<=0 THROW 53210,''PurchaseOrderId is required.'',1;
  IF NULLIF(LTRIM(RTRIM(@ReleasedBy)),N'''') IS NULL THROW 53211,''ReleasedBy is required.'',1;
  SET @LotIssueDate=ISNULL(@LotIssueDate,CONVERT(date,GETDATE()));
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @Status varchar(20),@SupplierCode nvarchar(60),@SiteCode nvarchar(30);
   SELECT @Status=Status,@SupplierCode=SupplierCode,@SiteCode=SiteCode
    FROM MES.FVN_ComponentPurchaseOrder WITH(UPDLOCK,HOLDLOCK)
    WHERE PurchaseOrderId=@PurchaseOrderId;
   IF @Status IS NULL THROW 53212,''Component purchase order was not found.'',1;
   IF @Status IN(''CANCELLED'',''CLOSED'') THROW 53213,''Closed/cancelled order cannot be released.'',1;
   IF @Status IN(''RELEASED'',''PARTIALLY_RECEIVED'',''RECEIVED'')
   BEGIN
    COMMIT TRANSACTION;
    SELECT l.ComponentLotId,l.MaterialLotId,l.LotNo,l.QrPayload,l.IssuedQty,
           p.LineNo,p.PartNo,p.PartDescription
     FROM MES.FVN_ComponentLot l JOIN MES.FVN_ComponentPurchaseOrderLine p
      ON p.PurchaseOrderLineId=l.PurchaseOrderLineId
     WHERE p.PurchaseOrderId=@PurchaseOrderId ORDER BY p.LineNo;
    RETURN;
   END;
   IF @Status<>''VALIDATED'' THROW 53214,''Only a VALIDATED MES order can be released.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine
     WHERE PurchaseOrderId=@PurchaseOrderId AND (SiteCode<>@SiteCode OR OrderQty<=0))
    THROW 53215,''Order line SiteCode or quantity is invalid.'',1;
   IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId=@PurchaseOrderId)
    THROW 53216,''Order has no lines.'',1;

   DECLARE @LineId bigint,@PartNo nvarchar(100),@Qty decimal(18,3),
    @ItemId5 char(5),@SupplierId5 char(5),@DocNo nvarchar(40),
    @LotNo nvarchar(120),@Qr nvarchar(500),@MaterialLotId bigint,@ComponentLotId bigint;
   EXEC MES.usp_FVN_AllocateSupplierId5 @SupplierCode=@SupplierCode,@SupplierId5=@SupplierId5 OUTPUT;
   DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT PurchaseOrderLineId,PartNo,OrderQty FROM MES.FVN_ComponentPurchaseOrderLine
    WITH(UPDLOCK,HOLDLOCK) WHERE PurchaseOrderId=@PurchaseOrderId ORDER BY LineNo;
   OPEN c; FETCH NEXT FROM c INTO @LineId,@PartNo,@Qty;
   WHILE @@FETCH_STATUS=0
   BEGIN
    SELECT @MaterialLotId=MaterialLotId FROM MES.FVN_ComponentLot
     WHERE PurchaseOrderLineId=@LineId;
    IF @MaterialLotId IS NULL
    BEGIN
     IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentLot WHERE PurchaseOrderLineId=@LineId)
     BEGIN
      EXEC MES.usp_FVN_AllocateItemId5 @SiteCode=@SiteCode,@PartNo=@PartNo,@ItemId5=@ItemId5 OUTPUT;
      SET @DocNo=CONVERT(varchar(20),@PurchaseOrderId)+N''-''+CONVERT(varchar(20),@LineId);
      SET @LotNo=CONVERT(char(6),@LotIssueDate,12)+@SupplierId5+@ItemId5+@DocNo;
      IF LEN(@LotNo)>120 THROW 53217,''Generated component LOT exceeds 120 characters.'',1;
      SET @Qr=N''FVN1|''+@LotNo;
      INSERT MES.FVN_ComponentLot(PurchaseOrderLineId,SiteCode,PartNo,SupplierCode,
       LotNo,LotIssueDate,IssuedQty,QrPayload)
      VALUES(@LineId,@SiteCode,@PartNo,@SupplierCode,@LotNo,@LotIssueDate,@Qty,@Qr);
      SET @ComponentLotId=SCOPE_IDENTITY();
      INSERT MES.FVN_MaterialLot(SiteCode,PartNo,LotNo,LotType,SupplierLotNo,SupplierCode,
       LotStatus,SourceSystem,SourceReference)
      VALUES(@SiteCode,@PartNo,@LotNo,''RAW'',@LotNo,@SupplierCode,''BLOCKED'',''MES'',
       N''COMPONENT_PO:''+CONVERT(nvarchar(30),@ComponentLotId));
      SET @MaterialLotId=SCOPE_IDENTITY();
      UPDATE MES.FVN_ComponentLot SET MaterialLotId=@MaterialLotId
       WHERE ComponentLotId=@ComponentLotId;
     END
     ELSE
      THROW 53218,''Component LOT exists but canonical MaterialLot link is missing; reconcile before release.'',1;
    END;
    SET @MaterialLotId=NULL;
    FETCH NEXT FROM c INTO @LineId,@PartNo,@Qty;
   END;
   CLOSE c; DEALLOCATE c;
   UPDATE MES.FVN_ComponentPurchaseOrder SET Status=''RELEASED'',
    ReleasedAtUtc=SYSUTCDATETIME(),ReleasedBy=@ReleasedBy,
    UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ReleasedBy
    WHERE PurchaseOrderId=@PurchaseOrderId;
   COMMIT TRANSACTION;
   SELECT l.ComponentLotId,l.MaterialLotId,l.LotNo,l.QrPayload,l.IssuedQty,
    p.LineNo,p.PartNo,p.PartDescription
   FROM MES.FVN_ComponentLot l JOIN MES.FVN_ComponentPurchaseOrderLine p
    ON p.PurchaseOrderLineId=l.PurchaseOrderLineId
   WHERE p.PurchaseOrderId=@PurchaseOrderId ORDER BY p.LineNo;
  END TRY
  BEGIN CATCH
   IF CURSOR_STATUS(''local'',''c'')>=0 CLOSE c;
   IF CURSOR_STATUS(''local'',''c'')>-3 DEALLOCATE c;
   IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
   THROW;
  END CATCH;
 END;');

 /* Receive one scan quantity against an already-issued QR. A QR may be received
    in multiple documents, but each request idempotency key is single-use. */
 EXEC(N'
 CREATE PROCEDURE MES.usp_FVN_ComponentReceipt_Post
  @SiteCode nvarchar(30),@ReceiptNo nvarchar(80),@QrPayload nvarchar(500),
  @Quantity decimal(19,6),@TargetSlotId int,@ReceivedBy nvarchar(100),
  @IdempotencyKey nvarchar(180),@ReceivedAtUtc datetime2(3)=NULL
 AS
 BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF NULLIF(LTRIM(RTRIM(@SiteCode)),N'''') IS NULL THROW 53220,''SiteCode is required.'',1;
  IF NULLIF(LTRIM(RTRIM(@ReceiptNo)),N'''') IS NULL THROW 53221,''ReceiptNo is required.'',1;
  IF NULLIF(LTRIM(RTRIM(@QrPayload)),N'''') IS NULL THROW 53222,''QR payload is required.'',1;
  IF @Quantity<=0 THROW 53223,''Receive quantity must be positive.'',1;
  IF @TargetSlotId IS NULL OR @TargetSlotId<=0 THROW 53224,''Target Slot is required.'',1;
  IF NULLIF(LTRIM(RTRIM(@ReceivedBy)),N'''') IS NULL THROW 53225,''ReceivedBy is required.'',1;
  IF NULLIF(LTRIM(RTRIM(@IdempotencyKey)),N'''') IS NULL THROW 53226,''IdempotencyKey is required.'',1;
  SET @ReceivedAtUtc=ISNULL(@ReceivedAtUtc,SYSUTCDATETIME());
  BEGIN TRY
   BEGIN TRANSACTION;
   DECLARE @ComponentLotId bigint,@MaterialLotId bigint,@PartNo nvarchar(100),
    @LotSite nvarchar(30),@IssuedQty decimal(18,3),@ReceivedQty decimal(18,3),
    @ReceiptId bigint,@ReceiptLineId bigint,@TxnId bigint,@SlotId int,@POId bigint;
   SELECT @ComponentLotId=ComponentLotId,@MaterialLotId=MaterialLotId,
    @PartNo=PartNo,@LotSite=SiteCode,@IssuedQty=IssuedQty,@ReceivedQty=ReceivedQty
   FROM MES.FVN_ComponentLot WITH(UPDLOCK,HOLDLOCK)
   WHERE QrPayload=@QrPayload AND Status IN(''ISSUED'',''PARTIALLY_RECEIVED'');
   IF @ComponentLotId IS NULL THROW 53227,''QR is not a valid active MES component LOT label.'',1;
   IF @LotSite<>@SiteCode THROW 53228,''QR SiteCode does not match receiving site.'',1;
   IF @MaterialLotId IS NULL THROW 53229,''Component LOT is not linked to canonical MES MaterialLot.'',1;
   IF @ReceivedQty+@Quantity>@IssuedQty
    THROW 53230,''Receive quantity exceeds the remaining issued quantity.'',1;
   IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine WHERE IdempotencyKey=@IdempotencyKey)
    THROW 53231,''This receiving request has already been posted.'',1;

   SELECT @POId=h.PurchaseOrderId
   FROM MES.FVN_ComponentPurchaseOrder h
   JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderId=h.PurchaseOrderId
   WHERE l.PurchaseOrderLineId=(SELECT PurchaseOrderLineId FROM MES.FVN_ComponentLot WHERE ComponentLotId=@ComponentLotId);

   IF NOT EXISTS(SELECT 1 FROM dbo.FVN_SlotMesLocation
    WHERE SiteCode=@SiteCode AND SlotId=@TargetSlotId AND IsPrimary=1 AND IsActive=1)
    THROW 53232,''Target Slot has no active primary MES location mapping for this Site.'',1;

   SELECT @ReceiptId=ComponentReceiptId FROM MES.FVN_ComponentReceipt WITH(UPDLOCK,HOLDLOCK)
    WHERE SiteCode=@SiteCode AND ReceiptNo=@ReceiptNo;
   IF @ReceiptId IS NULL
   BEGIN
    INSERT MES.FVN_ComponentReceipt(SiteCode,ReceiptNo,ReceivedAtUtc,ReceivedBy,Status)
    VALUES(@SiteCode,@ReceiptNo,@ReceivedAtUtc,@ReceivedBy,''POSTED'');
    SET @ReceiptId=SCOPE_IDENTITY();
   END;

   INSERT MES.FVN_ComponentReceiptLine(ComponentReceiptId,ComponentLotId,MaterialLotId,
    SiteCode,PartNo,ReceivedQty,TargetSlotId,IdempotencyKey,QrPayload,ReceivedAtUtc)
   VALUES(@ReceiptId,@ComponentLotId,@MaterialLotId,@SiteCode,@PartNo,@Quantity,
    @TargetSlotId,@IdempotencyKey,@QrPayload,@ReceivedAtUtc);
   SET @ReceiptLineId=SCOPE_IDENTITY();

   EXEC dbo.usp_FVN_PCTP_PostStockMovement
    @SiteCode=@SiteCode,@PartNo=@PartNo,@MaterialLotId=@MaterialLotId,
    @MovementType=''RECEIPT'',@Quantity=@Quantity,@ReferenceType=''COMPONENT_PO_RECEIPT'',
    @IdempotencyKey=@IdempotencyKey,@ActorCode=@ReceivedBy,
    @InventoryTransactionId=@TxnId OUTPUT,@ToSlotId=@TargetSlotId,
    @ToStockState=''AVAILABLE'',@ReferenceNo=@ReceiptNo,
    @ReferenceLineNo=@ReceiptLineId,@TransactionAtUtc=@ReceivedAtUtc;

   UPDATE MES.FVN_ComponentReceiptLine SET InventoryTransactionId=@TxnId
    WHERE ComponentReceiptLineId=@ReceiptLineId;
   UPDATE MES.FVN_ComponentLot SET ReceivedQty=ReceivedQty+@Quantity,
    Status=CASE WHEN ReceivedQty+@Quantity>=IssuedQty THEN ''RECEIVED'' ELSE ''PARTIALLY_RECEIVED'' END
    WHERE ComponentLotId=@ComponentLotId;
   UPDATE MES.FVN_MaterialLot SET LotStatus=''AVAILABLE''
    WHERE MaterialLotId=@MaterialLotId AND LotStatus IN(''BLOCKED'',''RECEIVED'');
   UPDATE l SET ReceivedQty=ReceivedQty+@Quantity
    FROM MES.FVN_ComponentPurchaseOrderLine l
    JOIN MES.FVN_ComponentLot c ON c.PurchaseOrderLineId=l.PurchaseOrderLineId
    WHERE c.ComponentLotId=@ComponentLotId;
   UPDATE h SET Status=CASE
      WHEN NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine x
        WHERE x.PurchaseOrderId=h.PurchaseOrderId AND x.ReceivedQty<x.OrderQty) THEN ''RECEIVED''
      WHEN EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine x
        WHERE x.PurchaseOrderId=h.PurchaseOrderId AND x.ReceivedQty>0) THEN ''PARTIALLY_RECEIVED''
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
 END;');

 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('032',N'Component LOT canonical material identity and MES stock receipt posting');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
GO

SELECT MigrationVersion,MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion='032';
GO
