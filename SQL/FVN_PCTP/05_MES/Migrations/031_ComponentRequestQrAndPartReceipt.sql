/*
 Migration 031: component requisition upload and PART warehouse receipt.
 Prerequisites: MES migrations 000..030, including 028/029/030.
 SQL Server 2016 compatible. IFS is read-only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 53100, 'Migration 031 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 53101, 'MES.FVN_SchemaMigration is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='030')
    THROW 53102, 'Migration 030 must be applied before migration 031.', 1;
IF OBJECT_ID(N'MES.usp_FVN_AllocateItemId5',N'P') IS NULL
   OR OBJECT_ID(N'MES.usp_FVN_AllocateSupplierId5',N'P') IS NULL
   OR OBJECT_ID(N'MES.usp_FVN_BuildComponentLotNo',N'P') IS NULL
   OR OBJECT_ID(N'MES.usp_PostInventoryMovement',N'P') IS NULL
    THROW 53103, 'Canonical item/supplier identity and MES posting procedures are required.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='031')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'MES.FVN_ComponentRequest',N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_ComponentRequest
        (
            ComponentRequestId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            RequestNo nvarchar(40) NOT NULL,
            RequestDate date NOT NULL,
            Status varchar(16) NOT NULL CONSTRAINT DF_MES_FVN_ComponentRequest_Status DEFAULT('OPEN'),
            SourceFileName nvarchar(260) NULL,
            SourceBatchKey uniqueidentifier NOT NULL,
            CreatedBy nvarchar(100) NOT NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentRequest_Created DEFAULT(SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(3) NULL,
            CONSTRAINT PK_MES_FVN_ComponentRequest PRIMARY KEY(ComponentRequestId),
            CONSTRAINT UQ_MES_FVN_ComponentRequest_No UNIQUE(SiteCode,RequestNo),
            CONSTRAINT UQ_MES_FVN_ComponentRequest_Batch UNIQUE(SiteCode,SourceBatchKey),
            CONSTRAINT CK_MES_FVN_ComponentRequest_Status CHECK(Status IN('OPEN','PARTIAL','RECEIVED','CANCELLED')),
            CONSTRAINT FK_MES_FVN_ComponentRequest_Site FOREIGN KEY(SiteCode) REFERENCES dbo.FVN_PCTPSite(SiteCode)
        );
    END;

    IF OBJECT_ID(N'MES.FVN_ComponentRequestLine',N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_ComponentRequestLine
        (
            ComponentRequestLineId bigint IDENTITY(1,1) NOT NULL,
            ComponentRequestId bigint NOT NULL,
            LineNo int NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            ItemId5 char(5) NOT NULL,
            RequestQty decimal(19,6) NOT NULL,
            ReceivedQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_FVN_ComponentRequestLine_Received DEFAULT(0),
            UomCode nvarchar(30) NOT NULL,
            Remark nvarchar(500) NULL,
            QrToken uniqueidentifier NOT NULL,
            QrPayload nvarchar(240) NOT NULL,
            Status varchar(16) NOT NULL CONSTRAINT DF_MES_FVN_ComponentRequestLine_Status DEFAULT('OPEN'),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentRequestLine_Created DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT PK_MES_FVN_ComponentRequestLine PRIMARY KEY(ComponentRequestLineId),
            CONSTRAINT UQ_MES_FVN_ComponentRequestLine_No UNIQUE(ComponentRequestId,LineNo),
            CONSTRAINT UQ_MES_FVN_ComponentRequestLine_QrToken UNIQUE(QrToken),
            CONSTRAINT UQ_MES_FVN_ComponentRequestLine_QrPayload UNIQUE(QrPayload),
            CONSTRAINT FK_MES_FVN_ComponentRequestLine_Header FOREIGN KEY(ComponentRequestId) REFERENCES MES.FVN_ComponentRequest(ComponentRequestId),
            CONSTRAINT FK_MES_FVN_ComponentRequestLine_Part FOREIGN KEY(SiteCode,PartNo) REFERENCES IFS.FVN_InventoryPart(SiteCode,PartNo),
            CONSTRAINT CK_MES_FVN_ComponentRequestLine_Qty CHECK(RequestQty>0 AND ReceivedQty>=0 AND ReceivedQty<=RequestQty),
            CONSTRAINT CK_MES_FVN_ComponentRequestLine_Status CHECK(Status IN('OPEN','PARTIAL','RECEIVED','CANCELLED'))
        );
    END;

    IF OBJECT_ID(N'MES.FVN_ComponentReceipt',N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_ComponentReceipt
        (
            ComponentReceiptId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            ReceiptNo nvarchar(80) NOT NULL,
            SupplierCode nvarchar(60) NOT NULL,
            ReceivedAtUtc datetime2(3) NOT NULL,
            ReceivedBy nvarchar(100) NOT NULL,
            Status varchar(16) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceipt_Status DEFAULT('POSTED'),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceipt_Created DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT PK_MES_FVN_ComponentReceipt PRIMARY KEY(ComponentReceiptId),
            CONSTRAINT UQ_MES_FVN_ComponentReceipt_No UNIQUE(SiteCode,ReceiptNo),
            CONSTRAINT FK_MES_FVN_ComponentReceipt_Site FOREIGN KEY(SiteCode) REFERENCES dbo.FVN_PCTPSite(SiteCode),
            CONSTRAINT FK_MES_FVN_ComponentReceipt_Supplier FOREIGN KEY(SupplierCode) REFERENCES IFS.FVN_Supplier(SupplierCode),
            CONSTRAINT CK_MES_FVN_ComponentReceipt_Status CHECK(Status IN('POSTED','CANCELLED'))
        );
    END;

    IF OBJECT_ID(N'MES.FVN_ComponentReceiptLine',N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_ComponentReceiptLine
        (
            ComponentReceiptLineId bigint IDENTITY(1,1) NOT NULL,
            ComponentReceiptId bigint NOT NULL,
            LineNo int NOT NULL,
            ComponentRequestLineId bigint NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NOT NULL,
            LotNo nvarchar(120) NOT NULL,
            SupplierLotNo nvarchar(120) NULL,
            ReceivedQty decimal(19,6) NOT NULL,
            UomCode nvarchar(30) NOT NULL,
            TargetLocationNo nvarchar(100) NOT NULL,
            InventoryTransactionId bigint NOT NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_FVN_ComponentReceiptLine_Created DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT PK_MES_FVN_ComponentReceiptLine PRIMARY KEY(ComponentReceiptLineId),
            CONSTRAINT UQ_MES_FVN_ComponentReceiptLine_No UNIQUE(ComponentReceiptId,LineNo),
            CONSTRAINT UQ_MES_FVN_ComponentReceiptLine_RequestLot UNIQUE(ComponentRequestLineId,LotNo),
            CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Header FOREIGN KEY(ComponentReceiptId) REFERENCES MES.FVN_ComponentReceipt(ComponentReceiptId),
            CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Request FOREIGN KEY(ComponentRequestLineId) REFERENCES MES.FVN_ComponentRequestLine(ComponentRequestLineId),
            CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Lot FOREIGN KEY(SiteCode,PartNo,MaterialLotId) REFERENCES MES.FVN_MaterialLot(SiteCode,PartNo,MaterialLotId),
            CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Txn FOREIGN KEY(InventoryTransactionId) REFERENCES MES.FVN_InventoryTransaction(InventoryTransactionId),
            CONSTRAINT FK_MES_FVN_ComponentReceiptLine_Location FOREIGN KEY(SiteCode,TargetLocationNo) REFERENCES MES.FVN_IfsInventoryLocation(SiteCode,LocationNo),
            CONSTRAINT CK_MES_FVN_ComponentReceiptLine_Qty CHECK(ReceivedQty>0)
        );
    END;

    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_ComponentRequest_Create
        @SiteCode nvarchar(30),
        @RequestDate date,
        @SourceFileName nvarchar(260)=NULL,
        @SourceBatchKey uniqueidentifier,
        @CreatedBy nvarchar(100),
        @LinesJson nvarchar(max)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF NULLIF(LTRIM(RTRIM(@SiteCode)),N'''') IS NULL
            THROW 53110, ''SiteCode is required; no default site is permitted.'', 1;
        IF NOT EXISTS(SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode=@SiteCode AND IsActive=1)
            THROW 53111, ''SiteCode is not registered or is inactive.'', 1;
        IF @RequestDate IS NULL THROW 53112, ''RequestDate is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@CreatedBy)),N'''') IS NULL
            THROW 53113, ''CreatedBy is required.'', 1;
        IF @SourceBatchKey IS NULL THROW 53114, ''SourceBatchKey is required for upload idempotency.'', 1;
        IF ISJSON(@LinesJson)<>1 THROW 53115, ''LinesJson must be a valid JSON array.'', 1;

        DECLARE @Rows TABLE
        (
            LineNo int NOT NULL PRIMARY KEY,
            PartNo nvarchar(100) NOT NULL,
            RequestQty decimal(19,6) NOT NULL,
            Remark nvarchar(500) NULL
        );

        INSERT @Rows(LineNo,PartNo,RequestQty,Remark)
        SELECT j.LineNo,LTRIM(RTRIM(j.PartNo)),j.RequestQty,j.Remark
        FROM OPENJSON(@LinesJson)
        WITH
        (
            LineNo int ''$.LineNo'',
            PartNo nvarchar(100) ''$.PartNo'',
            RequestQty decimal(19,6) ''$.RequestQty'',
            Remark nvarchar(500) ''$.Remark''
        ) j;

        IF NOT EXISTS(SELECT 1 FROM @Rows)
            THROW 53116, ''Upload contains no valid detail rows.'', 1;
        IF EXISTS(SELECT 1 FROM @Rows WHERE LineNo<=0 OR NULLIF(PartNo,N'''') IS NULL OR RequestQty<=0)
            THROW 53117, ''Each row requires positive LineNo, PartNo and RequestQty.'', 1;
        IF EXISTS
        (
            SELECT 1 FROM @Rows r
            LEFT JOIN IFS.FVN_InventoryPart p
              ON p.SiteCode=@SiteCode AND p.PartNo=r.PartNo AND p.IsActive=1
            WHERE p.PartNo IS NULL
        )
            THROW 53118, ''One or more PartNo values do not exist as active IFS inventory parts for this SiteCode.'', 1;

        BEGIN TRY
            BEGIN TRANSACTION;
            IF EXISTS(SELECT 1 FROM MES.FVN_ComponentRequest WITH(UPDLOCK,HOLDLOCK)
                      WHERE SiteCode=@SiteCode AND SourceBatchKey=@SourceBatchKey)
                THROW 53119, ''This upload batch was already imported.'', 1;

            DECLARE @RequestId bigint,@RequestNo nvarchar(40);
            INSERT MES.FVN_ComponentRequest
                (SiteCode,RequestNo,RequestDate,Status,SourceFileName,SourceBatchKey,CreatedBy)
            VALUES
                (@SiteCode,N''PENDING'',@RequestDate,''OPEN'',@SourceFileName,@SourceBatchKey,@CreatedBy);
            SET @RequestId=SCOPE_IDENTITY();
            SET @RequestNo=N''CR''+CONVERT(char(6),@RequestDate,12)+RIGHT(N''00000000''+CONVERT(nvarchar(20),@RequestId),8);
            UPDATE MES.FVN_ComponentRequest SET RequestNo=@RequestNo WHERE ComponentRequestId=@RequestId;

            DECLARE @LineNo int,@PartNo nvarchar(100),@Qty decimal(19,6),@Remark nvarchar(500),
                    @ItemId5 char(5),@Uom nvarchar(30),@Token uniqueidentifier,@Qr nvarchar(240);
            DECLARE c CURSOR LOCAL FAST_FORWARD FOR
                SELECT LineNo,PartNo,RequestQty,Remark FROM @Rows ORDER BY LineNo;
            OPEN c;
            FETCH NEXT FROM c INTO @LineNo,@PartNo,@Qty,@Remark;
            WHILE @@FETCH_STATUS=0
            BEGIN
                SET @ItemId5=NULL; SET @Uom=NULL;
                SELECT @Uom=UnitMeas FROM IFS.FVN_InventoryPart
                WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND IsActive=1;
                IF NULLIF(LTRIM(RTRIM(@Uom)),N'''') IS NULL
                    THROW 53120, ''IFS inventory part has no valid inventory UOM.'', 1;
                EXEC MES.usp_FVN_AllocateItemId5
                    @SiteCode=@SiteCode,@PartNo=@PartNo,@ItemId5=@ItemId5 OUTPUT;
                SET @Token=NEWID();
                SET @Qr=N''FVNCR|''+REPLACE(CONVERT(nvarchar(36),@Token),N''-'',N'''')+N''|''+@RequestNo+N''|''+CONVERT(nvarchar(12),@LineNo);
                INSERT MES.FVN_ComponentRequestLine
                    (ComponentRequestId,LineNo,SiteCode,PartNo,ItemId5,RequestQty,UomCode,Remark,QrToken,QrPayload)
                VALUES
                    (@RequestId,@LineNo,@SiteCode,@PartNo,@ItemId5,@Qty,@Uom,@Remark,@Token,@Qr);
                FETCH NEXT FROM c INTO @LineNo,@PartNo,@Qty,@Remark;
            END;
            CLOSE c; DEALLOCATE c;

            COMMIT TRANSACTION;
            SELECT h.ComponentRequestId,h.RequestNo,h.SiteCode,h.RequestDate,h.Status,
                   l.ComponentRequestLineId,l.LineNo,l.PartNo,l.ItemId5,l.RequestQty,l.ReceivedQty,
                   l.UomCode,l.QrPayload,l.Status AS LineStatus
            FROM MES.FVN_ComponentRequest h
            JOIN MES.FVN_ComponentRequestLine l ON l.ComponentRequestId=h.ComponentRequestId
            WHERE h.ComponentRequestId=@RequestId
            ORDER BY l.LineNo;
        END TRY
        BEGIN CATCH
            IF CURSOR_STATUS(''local'',''c'')>=0 CLOSE c;
            IF CURSOR_STATUS(''local'',''c'')>-3 DEALLOCATE c;
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;');

    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_ComponentReceipt_PostByQr
        @SiteCode nvarchar(30),
        @QrPayload nvarchar(240),
        @ReceiptNo nvarchar(80),
        @ReceiptLineNo int,
        @SupplierCode nvarchar(60),
        @DocNo nvarchar(40),
        @SupplierLotNo nvarchar(120)=NULL,
        @ReceivedQty decimal(19,6),
        @TargetLocationNo nvarchar(100),
        @ManufacturedDate date=NULL,
        @ExpiryDate date=NULL,
        @ReceivedAtUtc datetime2(3)=NULL,
        @ReceivedBy nvarchar(100)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        SET @ReceivedAtUtc=ISNULL(@ReceivedAtUtc,SYSUTCDATETIME());

        IF NULLIF(LTRIM(RTRIM(@SiteCode)),N'''') IS NULL
            THROW 53130, ''SiteCode is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@QrPayload)),N'''') IS NULL
            THROW 53131, ''QR payload is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@ReceiptNo)),N'''') IS NULL OR @ReceiptLineNo<=0
            THROW 53132, ''ReceiptNo and positive ReceiptLineNo are required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@SupplierCode)),N'''') IS NULL
            THROW 53133, ''SupplierCode is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@DocNo)),N'''') IS NULL
            THROW 53134, ''DocNo is required to build the component LOT.'', 1;
        IF @ReceivedQty<=0 THROW 53135, ''ReceivedQty must be greater than zero.'', 1;
        IF NULLIF(LTRIM(RTRIM(@TargetLocationNo)),N'''') IS NULL
            THROW 53136, ''TargetLocationNo is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@ReceivedBy)),N'''') IS NULL
            THROW 53137, ''ReceivedBy is required.'', 1;

        BEGIN TRY
            BEGIN TRANSACTION;

            DECLARE @RequestLineId bigint,@PartNo nvarchar(100),@RequestQty decimal(19,6),
                    @AlreadyReceived decimal(19,6),@Uom nvarchar(30),@ItemId5 char(5),
                    @RequestNo nvarchar(40),@RequestId bigint,@ReceiptId bigint,
                    @LotNo nvarchar(120),@MaterialLotId bigint,@TxnId bigint,
                    @LotRequestDate date,@IdempotencyKey nvarchar(180);

            SELECT @RequestLineId=l.ComponentRequestLineId,@PartNo=l.PartNo,
                   @RequestQty=l.RequestQty,@AlreadyReceived=l.ReceivedQty,@Uom=l.UomCode,
                   @ItemId5=l.ItemId5,@RequestNo=h.RequestNo,@RequestId=h.ComponentRequestId
            FROM MES.FVN_ComponentRequestLine l WITH(UPDLOCK,HOLDLOCK)
            JOIN MES.FVN_ComponentRequest h ON h.ComponentRequestId=l.ComponentRequestId
            WHERE h.SiteCode=@SiteCode AND l.QrPayload=@QrPayload
              AND h.Status<>'CANCELLED' AND l.Status<>'CANCELLED';

            IF @RequestLineId IS NULL
                THROW 53138, ''QR does not identify an active component request line for this SiteCode.'', 1;

            IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceiptLine x WITH(UPDLOCK,HOLDLOCK)
                      JOIN MES.FVN_ComponentReceipt r ON r.ComponentReceiptId=x.ComponentReceiptId
                      WHERE r.SiteCode=@SiteCode AND r.ReceiptNo=@ReceiptNo AND x.LineNo=@ReceiptLineNo)
                THROW 53139, ''This receipt line was already posted.'', 1;

            IF @AlreadyReceived+@ReceivedQty>@RequestQty
                THROW 53140, ''Received quantity exceeds the remaining component request quantity.'', 1;
            IF NOT EXISTS(SELECT 1 FROM IFS.FVN_Supplier WHERE SupplierCode=@SupplierCode)
                THROW 53141, ''SupplierCode is not present in the synchronized IFS supplier master.'', 1;
            IF NOT EXISTS(SELECT 1 FROM MES.FVN_IfsInventoryLocation
                          WHERE SiteCode=@SiteCode AND LocationNo=@TargetLocationNo AND IsActive=1)
                THROW 53142, ''Target location is not active for this SiteCode.'', 1;
            IF @ExpiryDate IS NOT NULL AND @ManufacturedDate IS NOT NULL AND @ExpiryDate<@ManufacturedDate
                THROW 53143, ''ExpiryDate cannot be earlier than ManufacturedDate.'', 1;

            IF EXISTS(SELECT 1 FROM MES.FVN_ComponentReceipt WITH(UPDLOCK,HOLDLOCK)
                      WHERE SiteCode=@SiteCode AND ReceiptNo=@ReceiptNo AND SupplierCode<>@SupplierCode)
                THROW 53144, ''ReceiptNo is already associated with a different supplier.'', 1;

            IF NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentReceipt
                          WHERE SiteCode=@SiteCode AND ReceiptNo=@ReceiptNo)
            BEGIN
                INSERT MES.FVN_ComponentReceipt(SiteCode,ReceiptNo,SupplierCode,ReceivedAtUtc,ReceivedBy,Status)
                VALUES(@SiteCode,@ReceiptNo,@SupplierCode,@ReceivedAtUtc,@ReceivedBy,''POSTED'');
                SET @ReceiptId=SCOPE_IDENTITY();
            END
            ELSE
                SELECT @ReceiptId=ComponentReceiptId FROM MES.FVN_ComponentReceipt
                WHERE SiteCode=@SiteCode AND ReceiptNo=@ReceiptNo;

            SET @LotRequestDate=CONVERT(date,@ReceivedAtUtc);
            SET @IdempotencyKey=N''COMPONENT_RECEIPT:''+@SiteCode+N'':''+@ReceiptNo+N'':''+CONVERT(nvarchar(12),@ReceiptLineNo);
            EXEC MES.usp_FVN_BuildComponentLotNo
                @RequestDate=@LotRequestDate,
                @SiteCode=@SiteCode,@PartNo=@PartNo,@SupplierCode=@SupplierCode,
                @DocNo=@DocNo,@LotNo=@LotNo OUTPUT,@ItemId5=@ItemId5 OUTPUT;

            INSERT MES.FVN_MaterialLot
                (SiteCode,PartNo,LotNo,LotType,SupplierLotNo,SupplierCode,ManufacturedDate,ExpiryDate,
                 LotStatus,SourceSystem,SourceReference)
            VALUES
                (@SiteCode,@PartNo,@LotNo,''RAW'',@SupplierLotNo,@SupplierCode,@ManufacturedDate,@ExpiryDate,
                 ''RECEIVED'',''PCTP'',''COMPONENT_REQUEST:''+@RequestNo);
            SET @MaterialLotId=SCOPE_IDENTITY();

            SET @TxnId=NULL;
            EXEC MES.usp_PostInventoryMovement
                @SiteCode=@SiteCode,@PartNo=@PartNo,@MaterialLotId=@MaterialLotId,
                @MovementType=''RECEIPT'',@Quantity=@ReceivedQty,@UomCode=@Uom,
                @ReferenceType=''COMPONENT_RECEIPT'',
                @IdempotencyKey=@IdempotencyKey,
                @TransactionAtUtc=@ReceivedAtUtc,@ActorCode=@ReceivedBy,
                @InventoryTransactionId=@TxnId OUTPUT,
                @ToLocationNo=@TargetLocationNo,@ToStockState=''AVAILABLE'',
                @ReferenceNo=@ReceiptNo,@ReferenceLineNo=@ReceiptLineNo;

            INSERT MES.FVN_ComponentReceiptLine
                (ComponentReceiptId,LineNo,ComponentRequestLineId,SiteCode,PartNo,MaterialLotId,LotNo,
                 SupplierLotNo,ReceivedQty,UomCode,TargetLocationNo,InventoryTransactionId)
            VALUES
                (@ReceiptId,@ReceiptLineNo,@RequestLineId,@SiteCode,@PartNo,@MaterialLotId,@LotNo,
                 @SupplierLotNo,@ReceivedQty,@Uom,@TargetLocationNo,@TxnId);

            UPDATE MES.FVN_ComponentRequestLine
               SET ReceivedQty=ReceivedQty+@ReceivedQty,
                   Status=CASE WHEN ReceivedQty+@ReceivedQty>=RequestQty THEN ''RECEIVED'' ELSE ''PARTIAL'' END
             WHERE ComponentRequestLineId=@RequestLineId;

            UPDATE h
               SET Status=CASE
                   WHEN NOT EXISTS(SELECT 1 FROM MES.FVN_ComponentRequestLine l
                                   WHERE l.ComponentRequestId=h.ComponentRequestId AND l.Status<>'RECEIVED')
                     THEN ''RECEIVED''
                   WHEN EXISTS(SELECT 1 FROM MES.FVN_ComponentRequestLine l
                               WHERE l.ComponentRequestId=h.ComponentRequestId AND l.ReceivedQty>0)
                     THEN ''PARTIAL'' ELSE ''OPEN'' END,
                   UpdatedAtUtc=SYSUTCDATETIME()
            FROM MES.FVN_ComponentRequest h WHERE h.ComponentRequestId=@RequestId;

            COMMIT TRANSACTION;
            SELECT @ReceiptId AS ComponentReceiptId,@ReceiptNo AS ReceiptNo,
                   @ReceiptLineNo AS ReceiptLineNo,@PartNo AS PartNo,@ItemId5 AS ItemId5,
                   @LotNo AS ComponentLotNo,@ReceivedQty AS ReceivedQty,@Uom AS UomCode,
                   @TargetLocationNo AS TargetLocationNo,@MaterialLotId AS MaterialLotId,
                   @TxnId AS InventoryTransactionId;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;');

    INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
    VALUES('031',N'Component requisition QR upload and PART receipt posting');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion,MigrationName
FROM MES.FVN_SchemaMigration WHERE MigrationVersion='031';
GO
