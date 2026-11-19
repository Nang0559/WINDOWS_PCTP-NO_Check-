/*
 Migration 043: component warehouse (PART) workflow.
   1. Component purchase-order (yeu cau mua hang) upload + lifecycle on the shared workflow
      table dbo.FVN_sys_WorkflowTransitions (ProcessCode MES_COMPONENT_PO), with an event log.
   2. Component LOT transfer between PART slots posted through the canonical MES inventory
      posting adapter (dbo.usp_FVN_PCTP_PostStockMovement, MOVE) with an audit document.

 Status codes are stable API values (same convention as MES migration 036):
   0 DRAFT -> 10 VALIDATED -> 20 RELEASED -> 30 PARTIALLY_RECEIVED -> 40 RECEIVED -> 50 CLOSED
   900 CANCELLED (from 0 / 10 / 20 only, and only while nothing has been received).
 30 and 40 are receipt-driven: MES.usp_FVN_ComponentReceipt_Post (migration 032) sets them when
 a QR is received; they cannot be set manually. A trigger records those transitions in the event log.

 Does not post stock by itself except through the transfer procedure, and never updates
 STOCKTP / SlotLot quantities directly.
 SQL Server 2016 compatible. Apply after 036 (and 032/033/025 which it depends on).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54300, 'Migration 043 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54301, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '036')
    THROW 54302, 'Migration 036 must be applied before 043.', 1;
IF OBJECT_ID(N'dbo.FVN_sys_WorkflowTransitions', N'U') IS NULL
    THROW 54303, 'Shared workflow transition table is required.', 1;
IF OBJECT_ID(N'MES.FVN_ComponentPurchaseOrder', N'U') IS NULL
   OR OBJECT_ID(N'MES.usp_FVN_ComponentPO_Release', N'P') IS NULL
   OR OBJECT_ID(N'MES.usp_FVN_ComponentReceipt_Post', N'P') IS NULL
    THROW 54304, 'Component purchase order / receipt migrations 031 and 032 are required.', 1;
IF OBJECT_ID(N'dbo.usp_FVN_PCTP_PostStockMovement', N'P') IS NULL
    THROW 54305, 'dbo.usp_FVN_PCTP_PostStockMovement from migration 025 is required.', 1;
IF OBJECT_ID(N'dbo.FVN_InventoryAreaSlot', N'U') IS NULL
    THROW 54306, 'dbo.FVN_InventoryAreaSlot from migration 033 is required.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '043')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ---------- Event log + transfer document ---------- */
    IF OBJECT_ID(N'MES.FVN_ComponentPoWorkflowEvent', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_ComponentPoWorkflowEvent
        (
            WorkflowEventId bigint IDENTITY(1,1) NOT NULL,
            PurchaseOrderId bigint NOT NULL,
            ProcessCode varchar(40) NOT NULL,
            FromStatus int NULL,
            ToStatus int NOT NULL,
            ActionName nvarchar(80) NOT NULL,
            Reason nvarchar(500) NULL,
            ActorCode nvarchar(100) NOT NULL,
            OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ComponentPoWorkflowEvent_Date DEFAULT(SYSUTCDATETIME()),
            CorrelationId uniqueidentifier NULL,
            CONSTRAINT PK_FVN_ComponentPoWorkflowEvent PRIMARY KEY(WorkflowEventId),
            CONSTRAINT FK_FVN_ComponentPoWorkflowEvent_PO FOREIGN KEY(PurchaseOrderId)
                REFERENCES MES.FVN_ComponentPurchaseOrder(PurchaseOrderId)
        );
        CREATE INDEX IX_FVN_ComponentPoWorkflowEvent_PO
            ON MES.FVN_ComponentPoWorkflowEvent(PurchaseOrderId, OccurredAtUtc, WorkflowEventId);
    END;

    IF OBJECT_ID(N'MES.FVN_ComponentLotTransfer', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_ComponentLotTransfer
        (
            ComponentLotTransferId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            TransferNo nvarchar(80) NOT NULL,
            MaterialLotId bigint NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            FromSlotId int NOT NULL,
            ToSlotId int NOT NULL,
            Quantity decimal(19,6) NOT NULL,
            Reason nvarchar(300) NULL,
            InventoryTransactionId bigint NULL,
            IdempotencyKey nvarchar(170) NOT NULL,
            TransferredBy nvarchar(100) NOT NULL,
            TransferredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_ComponentLotTransfer_Date DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_ComponentLotTransfer PRIMARY KEY(ComponentLotTransferId),
            CONSTRAINT UQ_FVN_ComponentLotTransfer_Key UNIQUE(IdempotencyKey),
            CONSTRAINT FK_FVN_ComponentLotTransfer_Lot FOREIGN KEY(MaterialLotId)
                REFERENCES MES.FVN_MaterialLot(MaterialLotId),
            CONSTRAINT CK_FVN_ComponentLotTransfer_Qty CHECK(Quantity>0),
            CONSTRAINT CK_FVN_ComponentLotTransfer_Slots CHECK(FromSlotId<>ToSlotId)
        );
        CREATE INDEX IX_FVN_ComponentLotTransfer_SiteLot
            ON MES.FVN_ComponentLotTransfer(SiteCode, MaterialLotId, TransferredAtUtc);
    END;

    IF TYPE_ID(N'MES.FVN_ComponentPoImportLine') IS NULL
        CREATE TYPE MES.FVN_ComponentPoImportLine AS TABLE
        (
            [LineNo] int NOT NULL PRIMARY KEY,
            PartNo nvarchar(100) NOT NULL,
            PartDescription nvarchar(250) NULL,
            OrderQty decimal(18,3) NOT NULL,
            UnitCode nvarchar(20) NULL,
            NeedByDate date NULL
        );

    /* ---------- Shared workflow transitions ---------- */
    DECLARE @Seed TABLE(ProcessCode varchar(40), FromStatus int, ToStatus int, ActionName nvarchar(80), Description nvarchar(250));
    INSERT @Seed VALUES
    ('MES_COMPONENT_PO',   0,  10, 'VALIDATE',        N'Kiem tra dong PO, ma linh kiem IFS va nha cung cap'),
    ('MES_COMPONENT_PO',  10,  20, 'RELEASE',         N'Phat hanh PO va sinh LOT/QR linh kien'),
    ('MES_COMPONENT_PO',  20,  30, 'RECEIVE_PARTIAL', N'He thong ghi nhan khi quet QR nhan mot phan'),
    ('MES_COMPONENT_PO',  20,  40, 'RECEIVE_FULL',    N'He thong ghi nhan khi quet QR nhan du'),
    ('MES_COMPONENT_PO',  30,  40, 'RECEIVE_FULL',    N'He thong ghi nhan khi quet QR nhan du'),
    ('MES_COMPONENT_PO',  40,  50, 'CLOSE',           N'Dong PO da nhan du'),
    ('MES_COMPONENT_PO',  30,  50, 'SHORT_CLOSE',     N'Dong PO khi khong nhan them (bat buoc ly do)'),
    ('MES_COMPONENT_PO',   0, 900, 'CANCEL',          N'Huy PO nhap'),
    ('MES_COMPONENT_PO',  10, 900, 'CANCEL',          N'Huy PO da kiem tra'),
    ('MES_COMPONENT_PO',  20, 900, 'CANCEL',          N'Huy PO da phat hanh khi chua nhan hang');
    INSERT dbo.FVN_sys_WorkflowTransitions(ProcessCode, FromStatus, ToStatus, ActionName, Description, IsActive)
    SELECT s.ProcessCode, s.FromStatus, s.ToStatus, s.ActionName, s.Description, 1
    FROM @Seed s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions t
                      WHERE t.ProcessCode=s.ProcessCode AND t.FromStatus=s.FromStatus
                        AND t.ToStatus=s.ToStatus AND t.IsActive=1);

    /* ---------- Status code mapping (single definition) ---------- */
    EXEC(N'
    CREATE FUNCTION MES.fn_FVN_ComponentPoStatusCode(@Status varchar(20))
    RETURNS int
    AS
    BEGIN
        RETURN CASE @Status
            WHEN ''DRAFT'' THEN 0 WHEN ''VALIDATED'' THEN 10 WHEN ''RELEASED'' THEN 20
            WHEN ''PARTIALLY_RECEIVED'' THEN 30 WHEN ''RECEIVED'' THEN 40
            WHEN ''CLOSED'' THEN 50 WHEN ''CANCELLED'' THEN 900 ELSE NULL END;
    END');

    /* ---------- Receipt-driven transitions are logged by trigger ---------- */
    EXEC(N'
    CREATE TRIGGER MES.tr_FVN_ComponentPO_ReceiptWorkflow
    ON MES.FVN_ComponentPurchaseOrder
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        IF NOT UPDATE(Status) RETURN;
        INSERT MES.FVN_ComponentPoWorkflowEvent(PurchaseOrderId,ProcessCode,FromStatus,ToStatus,ActionName,ActorCode)
        SELECT i.PurchaseOrderId,''MES_COMPONENT_PO'',
               MES.fn_FVN_ComponentPoStatusCode(d.Status),MES.fn_FVN_ComponentPoStatusCode(i.Status),
               CASE WHEN i.Status=''RECEIVED'' THEN N''RECEIVE_FULL'' ELSE N''RECEIVE_PARTIAL'' END,
               ISNULL(NULLIF(i.UpdatedBy,N''''),SUSER_SNAME())
        FROM inserted i
        JOIN deleted d ON d.PurchaseOrderId=i.PurchaseOrderId
        WHERE i.Status<>d.Status AND i.Status IN(''PARTIALLY_RECEIVED'',''RECEIVED'');
    END');

    /* ---------- Upload one PO (header + lines). Re-upload is allowed only while DRAFT. ---------- */
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_ComponentPO_Import
        @SiteCode nvarchar(30),
        @PurchaseOrderNo nvarchar(60),
        @SupplierCode nvarchar(60),
        @OrderDate date,
        @ActorCode nvarchar(100),
        @Lines MES.FVN_ComponentPoImportLine READONLY,
        @Remarks nvarchar(500)=NULL,
        @CorrelationId uniqueidentifier=NULL
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        SET @SiteCode=UPPER(LTRIM(RTRIM(@SiteCode)));
        SET @PurchaseOrderNo=LTRIM(RTRIM(@PurchaseOrderNo));
        SET @SupplierCode=LTRIM(RTRIM(@SupplierCode));
        IF NULLIF(@SiteCode,N'''') IS NULL THROW 54310,''SiteCode is required.'',1;
        IF NULLIF(@PurchaseOrderNo,N'''') IS NULL THROW 54311,''PurchaseOrderNo is required.'',1;
        IF NULLIF(@SupplierCode,N'''') IS NULL THROW 54312,''SupplierCode is required.'',1;
        IF @OrderDate IS NULL THROW 54313,''OrderDate is required.'',1;
        IF NULLIF(LTRIM(RTRIM(@ActorCode)),N'''') IS NULL THROW 54314,''ActorCode is required.'',1;
        IF NOT EXISTS(SELECT 1 FROM @Lines) THROW 54315,''PO upload has no lines.'',1;
        IF EXISTS(SELECT 1 FROM @Lines WHERE OrderQty<=0) THROW 54316,''Order quantity must be greater than 0.'',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode=@SiteCode AND IsActive=1)
            THROW 54317,''Site is not registered or inactive.'',1;
        IF EXISTS(SELECT 1 FROM @Lines l WHERE NOT EXISTS(
                SELECT 1 FROM IFS.FVN_InventoryPart p WHERE p.SiteCode=@SiteCode AND p.PartNo=l.PartNo))
            THROW 54318,''A PartNo in the upload does not exist in the IFS part master for this Site.'',1;

        /* Creates the MES supplier identity from the synchronized IFS supplier master when missing. */
        DECLARE @SupplierId5 char(5);
        EXEC MES.usp_FVN_AllocateSupplierId5 @SupplierCode=@SupplierCode,@SupplierId5=@SupplierId5 OUTPUT;

        DECLARE @OwnTran bit=CASE WHEN @@TRANCOUNT=0 THEN 1 ELSE 0 END;
        BEGIN TRY
            IF @OwnTran=1 BEGIN TRANSACTION;
            DECLARE @PoId bigint,@Status varchar(20),@Action nvarchar(80),@FromStatus int;
            SELECT @PoId=PurchaseOrderId,@Status=Status
            FROM MES.FVN_ComponentPurchaseOrder WITH(UPDLOCK,HOLDLOCK)
            WHERE SiteCode=@SiteCode AND PurchaseOrderNo=@PurchaseOrderNo;

            IF @PoId IS NULL
            BEGIN
                INSERT MES.FVN_ComponentPurchaseOrder(SiteCode,PurchaseOrderNo,SupplierCode,SupplierNameSnapshot,
                    OrderDate,Status,Remarks,CreatedBy)
                SELECT @SiteCode,@PurchaseOrderNo,@SupplierCode,s.SupplierName,@OrderDate,''DRAFT'',@Remarks,@ActorCode
                FROM MES.FVN_SupplierIdentity s WHERE s.SupplierCode=@SupplierCode;
                SET @PoId=SCOPE_IDENTITY();
                SET @Action=N''UPLOAD''; SET @FromStatus=NULL;
            END
            ELSE
            BEGIN
                IF @Status<>''DRAFT''
                    THROW 54319,''PO already exists and is no longer DRAFT; it cannot be replaced by an upload.'',1;
                DELETE FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId=@PoId;
                UPDATE h SET SupplierCode=@SupplierCode,SupplierNameSnapshot=s.SupplierName,OrderDate=@OrderDate,
                       Remarks=@Remarks,UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ActorCode
                FROM MES.FVN_ComponentPurchaseOrder h
                JOIN MES.FVN_SupplierIdentity s ON s.SupplierCode=@SupplierCode
                WHERE h.PurchaseOrderId=@PoId;
                SET @Action=N''REUPLOAD''; SET @FromStatus=0;
            END;

            INSERT MES.FVN_ComponentPurchaseOrderLine(PurchaseOrderId,[LineNo],SiteCode,PartNo,PartDescription,
                OrderQty,UnitCode,NeedByDate)
            SELECT @PoId,l.[LineNo],@SiteCode,l.PartNo,l.PartDescription,l.OrderQty,
                   LEFT(ISNULL(l.UnitCode,p.UnitMeas),20),l.NeedByDate
            FROM @Lines l
            JOIN IFS.FVN_InventoryPart p ON p.SiteCode=@SiteCode AND p.PartNo=l.PartNo;

            INSERT MES.FVN_ComponentPoWorkflowEvent(PurchaseOrderId,ProcessCode,FromStatus,ToStatus,ActionName,
                ActorCode,CorrelationId)
            VALUES(@PoId,''MES_COMPONENT_PO'',@FromStatus,0,@Action,@ActorCode,@CorrelationId);

            IF @OwnTran=1 COMMIT TRANSACTION;
            SELECT @PoId AS PurchaseOrderId,@PurchaseOrderNo AS PurchaseOrderNo,
                   (SELECT COUNT(*) FROM @Lines) AS LineCount,@Action AS ActionName;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 AND @OwnTran=1 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    /* ---------- Manual workflow transition (validate / release / close / cancel) ---------- */
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_ComponentPO_Transition
        @PurchaseOrderId bigint,
        @FromStatus int,
        @ToStatus int,
        @ActorCode nvarchar(100),
        @Reason nvarchar(500)=NULL,
        @CorrelationId uniqueidentifier=NULL
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        IF @PurchaseOrderId IS NULL OR @PurchaseOrderId<=0 THROW 54330,''PurchaseOrderId is required.'',1;
        IF NULLIF(LTRIM(RTRIM(@ActorCode)),N'''') IS NULL THROW 54331,''ActorCode is required.'',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions
            WHERE ProcessCode=''MES_COMPONENT_PO'' AND FromStatus=@FromStatus AND ToStatus=@ToStatus AND IsActive=1)
            THROW 54332,''Transition is not configured in the shared workflow table.'',1;
        IF @ToStatus IN(30,40)
            THROW 54333,''Receipt-driven statuses are posted by component receipt and cannot be set manually.'',1;
        IF @ToStatus=900 AND NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL
            THROW 54334,''Cancel reason is required.'',1;
        IF @ToStatus=50 AND @FromStatus=30 AND NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL
            THROW 54335,''Short-close reason is required.'',1;

        DECLARE @Status varchar(20),@Action nvarchar(80),@Lots int=0,@Note nvarchar(500);
        SET @Reason=NULLIF(LTRIM(RTRIM(@Reason)),N'''');
        BEGIN TRY
            BEGIN TRANSACTION;
            SELECT @Status=Status FROM MES.FVN_ComponentPurchaseOrder WITH(UPDLOCK,HOLDLOCK)
            WHERE PurchaseOrderId=@PurchaseOrderId;
            IF @Status IS NULL THROW 54336,''Component purchase order was not found.'',1;
            IF MES.fn_FVN_ComponentPoStatusCode(@Status)<>@FromStatus
                THROW 54337,''Order state has changed; reload and retry.'',1;
            SELECT TOP (1) @Action=ActionName FROM dbo.FVN_sys_WorkflowTransitions
            WHERE ProcessCode=''MES_COMPONENT_PO'' AND FromStatus=@FromStatus AND ToStatus=@ToStatus AND IsActive=1
            ORDER BY Id;

            IF @ToStatus=10
            BEGIN
                IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId=@PurchaseOrderId)
                    THROW 54338,''Cannot validate an order without lines.'',1;
                IF EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine l
                          WHERE l.PurchaseOrderId=@PurchaseOrderId AND NOT EXISTS(
                            SELECT 1 FROM IFS.FVN_InventoryPart p WHERE p.SiteCode=l.SiteCode AND p.PartNo=l.PartNo))
                    THROW 54339,''A PartNo of the order no longer exists in the IFS part master.'',1;
                IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrder h
                          JOIN MES.FVN_SupplierIdentity s ON s.SupplierCode=h.SupplierCode
                          WHERE h.PurchaseOrderId=@PurchaseOrderId)
                    THROW 54340,''Supplier identity of the order does not exist.'',1;
                UPDATE MES.FVN_ComponentPurchaseOrder SET Status=''VALIDATED'',
                       UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ActorCode
                WHERE PurchaseOrderId=@PurchaseOrderId;
            END
            ELSE IF @ToStatus=20
            BEGIN
                /* Existing release procedure issues the LOT/QR labels and sets Status=RELEASED. */
                EXEC MES.usp_FVN_ComponentPO_Release @PurchaseOrderId=@PurchaseOrderId,@ReleasedBy=@ActorCode;
                SELECT @Lots=COUNT(*) FROM MES.FVN_ComponentLot c
                JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId=c.PurchaseOrderLineId
                WHERE l.PurchaseOrderId=@PurchaseOrderId;
            END
            ELSE IF @ToStatus=50
            BEGIN
                /* Lots never received are cancelled; partially received lots are closed at the received quantity. */
                UPDATE c SET Status=''CANCELLED''
                FROM MES.FVN_ComponentLot c
                JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId=c.PurchaseOrderLineId
                WHERE l.PurchaseOrderId=@PurchaseOrderId AND c.ReceivedQty=0 AND c.Status=''ISSUED'';
                UPDATE c SET Status=''RECEIVED''
                FROM MES.FVN_ComponentLot c
                JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId=c.PurchaseOrderLineId
                WHERE l.PurchaseOrderId=@PurchaseOrderId AND c.Status=''PARTIALLY_RECEIVED'';
                SET @Note=CASE WHEN @Reason IS NULL THEN NULL ELSE N''[CLOSE] ''+@Reason END;
                UPDATE MES.FVN_ComponentPurchaseOrder SET Status=''CLOSED'',
                       Remarks=LEFT(CONCAT(Remarks+N'' | '',@Note),500),
                       UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ActorCode
                WHERE PurchaseOrderId=@PurchaseOrderId;
            END
            ELSE IF @ToStatus=900
            BEGIN
                IF EXISTS(SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine
                          WHERE PurchaseOrderId=@PurchaseOrderId AND ReceivedQty>0)
                    THROW 54341,''An order that already received quantity cannot be cancelled; use short close.'',1;
                UPDATE c SET Status=''CANCELLED''
                FROM MES.FVN_ComponentLot c
                JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId=c.PurchaseOrderLineId
                WHERE l.PurchaseOrderId=@PurchaseOrderId AND c.ReceivedQty=0;
                UPDATE MES.FVN_ComponentPurchaseOrder SET Status=''CANCELLED'',
                       CancelledAtUtc=SYSUTCDATETIME(),CancelledBy=@ActorCode,
                       Remarks=LEFT(CONCAT(Remarks+N'' | '',N''[CANCEL] ''+@Reason),500),
                       UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@ActorCode
                WHERE PurchaseOrderId=@PurchaseOrderId;
            END
            ELSE THROW 54342,''Unsupported component PO target status.'',1;

            INSERT MES.FVN_ComponentPoWorkflowEvent(PurchaseOrderId,ProcessCode,FromStatus,ToStatus,ActionName,
                Reason,ActorCode,CorrelationId)
            VALUES(@PurchaseOrderId,''MES_COMPONENT_PO'',@FromStatus,@ToStatus,@Action,@Reason,@ActorCode,@CorrelationId);
            COMMIT TRANSACTION;

            SELECT @PurchaseOrderId AS PurchaseOrderId,@FromStatus AS FromStatus,@ToStatus AS ToStatus,
                   ''MES_COMPONENT_PO'' AS ProcessCode,@Lots AS LotsIssued;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    /* ---------- Transfer one component LOT between PART slots ---------- */
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_ComponentLot_Transfer
        @SiteCode nvarchar(30),
        @TransferNo nvarchar(80),
        @MaterialLotId bigint,
        @FromSlotId int,
        @ToSlotId int,
        @Quantity decimal(19,6),
        @ActorCode nvarchar(100),
        @IdempotencyKey nvarchar(170),
        @Reason nvarchar(300)=NULL
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        SET @SiteCode=UPPER(LTRIM(RTRIM(@SiteCode)));
        IF NULLIF(@SiteCode,N'''') IS NULL THROW 54350,''SiteCode is required.'',1;
        IF NULLIF(LTRIM(RTRIM(@TransferNo)),N'''') IS NULL THROW 54351,''TransferNo is required.'',1;
        IF @MaterialLotId IS NULL OR @MaterialLotId<=0 THROW 54352,''MaterialLotId is required.'',1;
        IF @FromSlotId IS NULL OR @ToSlotId IS NULL OR @FromSlotId<=0 OR @ToSlotId<=0
            THROW 54353,''Source and destination Slot are required.'',1;
        IF @FromSlotId=@ToSlotId THROW 54354,''Source and destination Slot must differ.'',1;
        IF @Quantity IS NULL OR @Quantity<=0 THROW 54355,''Transfer quantity must be greater than 0.'',1;
        IF NULLIF(LTRIM(RTRIM(@ActorCode)),N'''') IS NULL THROW 54356,''ActorCode is required.'',1;
        IF NULLIF(LTRIM(RTRIM(@IdempotencyKey)),N'''') IS NULL THROW 54357,''IdempotencyKey is required.'',1;

        /* Replay of an already-posted request returns the original document. */
        IF EXISTS(SELECT 1 FROM MES.FVN_ComponentLotTransfer WHERE IdempotencyKey=@IdempotencyKey)
        BEGIN
            SELECT ComponentLotTransferId,InventoryTransactionId,TransferNo,Quantity
            FROM MES.FVN_ComponentLotTransfer WHERE IdempotencyKey=@IdempotencyKey;
            RETURN;
        END;

        BEGIN TRY
            BEGIN TRANSACTION;
            DECLARE @PartNo nvarchar(100),@LotSite nvarchar(30),@LotStatus varchar(20);
            SELECT @PartNo=PartNo,@LotSite=SiteCode,@LotStatus=LotStatus
            FROM MES.FVN_MaterialLot WITH(UPDLOCK,HOLDLOCK) WHERE MaterialLotId=@MaterialLotId;
            IF @PartNo IS NULL THROW 54358,''Material LOT was not found.'',1;
            IF @LotSite<>@SiteCode THROW 54359,''LOT does not belong to the supplied Site.'',1;
            IF @LotStatus<>''AVAILABLE'' THROW 54361,''Only an AVAILABLE LOT can be transferred.'',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.FVN_InventoryAreaSlot
                          WHERE SiteCode=@SiteCode AND SlotId=@FromSlotId AND AreaCode=''PART'' AND IsActive=1)
                THROW 54362,''Source Slot is not an active PART slot of this Site.'',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.FVN_InventoryAreaSlot
                          WHERE SiteCode=@SiteCode AND SlotId=@ToSlotId AND AreaCode=''PART'' AND IsActive=1)
                THROW 54363,''Destination Slot is not an active PART slot of this Site.'',1;

            DECLARE @FromLoc nvarchar(100),@ToLoc nvarchar(100);
            SELECT @FromLoc=LocationNo FROM dbo.FVN_SlotMesLocation
            WHERE SiteCode=@SiteCode AND SlotId=@FromSlotId AND IsPrimary=1 AND IsActive=1;
            SELECT @ToLoc=LocationNo FROM dbo.FVN_SlotMesLocation
            WHERE SiteCode=@SiteCode AND SlotId=@ToSlotId AND IsPrimary=1 AND IsActive=1;
            IF @FromLoc IS NULL OR @ToLoc IS NULL
                THROW 54364,''Source or destination Slot has no active primary MES location mapping.'',1;
            IF @FromLoc=@ToLoc THROW 54365,''Both Slots map to the same MES location.'',1;

            DECLARE @Avail decimal(19,6);
            SELECT @Avail=SUM(OnHandQty-ReservedQty) FROM MES.FVN_InventoryBalance
            WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@MaterialLotId
              AND LocationNo=@FromLoc AND StockState=''AVAILABLE'';
            IF ISNULL(@Avail,0)<@Quantity
                THROW 54366,''Available quantity at the source Slot is lower than the transfer quantity.'',1;

            DECLARE @TxnId bigint,@PostKey nvarchar(180)=N''CLT:''+@IdempotencyKey;
            EXEC dbo.usp_FVN_PCTP_PostStockMovement
                @SiteCode=@SiteCode,@PartNo=@PartNo,@MaterialLotId=@MaterialLotId,
                @MovementType=''MOVE'',@Quantity=@Quantity,@ReferenceType=''COMPONENT_LOT_MOVE'',
                @IdempotencyKey=@PostKey,@ActorCode=@ActorCode,@InventoryTransactionId=@TxnId OUTPUT,
                @FromSlotId=@FromSlotId,@ToSlotId=@ToSlotId,
                @FromStockState=''AVAILABLE'',@ToStockState=''AVAILABLE'',@ReferenceNo=@TransferNo;

            INSERT MES.FVN_ComponentLotTransfer(SiteCode,TransferNo,MaterialLotId,PartNo,FromSlotId,ToSlotId,
                Quantity,Reason,InventoryTransactionId,IdempotencyKey,TransferredBy)
            VALUES(@SiteCode,LTRIM(RTRIM(@TransferNo)),@MaterialLotId,@PartNo,@FromSlotId,@ToSlotId,
                @Quantity,NULLIF(LTRIM(RTRIM(@Reason)),N''''),@TxnId,@IdempotencyKey,@ActorCode);
            DECLARE @DocId bigint=SCOPE_IDENTITY();
            COMMIT TRANSACTION;

            SELECT @DocId AS ComponentLotTransferId,@TxnId AS InventoryTransactionId,
                   LTRIM(RTRIM(@TransferNo)) AS TransferNo,@Quantity AS Quantity;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES('043', N'Component PO upload/workflow on shared transitions and component LOT transfer between PART slots');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT ProcessCode, FromStatus, ToStatus, ActionName
FROM dbo.FVN_sys_WorkflowTransitions
WHERE ProcessCode = 'MES_COMPONENT_PO' AND IsActive = 1
ORDER BY FromStatus, ToStatus;
GO