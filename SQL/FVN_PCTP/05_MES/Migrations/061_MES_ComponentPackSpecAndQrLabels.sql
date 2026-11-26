/*
 Migration 061: component packing specification (quy cách đóng gói) and QR labels per pack.

 1. MES.FVN_ComponentPackSpec          replaces B20ItemQuyCach for component PO:
        SupplierCode (B20: CustomerCode), PartNo (B20: ItemCode), PackQty (B20: Quantity), MaxCloseQty (B20: MaxCloseQty).
        SupplierCode = N'' means "every supplier"; a row for the exact supplier wins over the N'' row.
 2. MES.usp_FVN_ComponentPackSpec_Upsert  one row (create / update / deactivate), used by the screen and the Excel upload.
 3. MES.usp_FVN_ComponentPO_Release       replaces the 032 version: one QR LOT per pack. A line of 7,200 with PackQty 2,400
        issues 3 LOT (-1, -2, -3); the last LOT holds the remainder. A PartNo without an active spec stops the release and
        is listed in the message (nothing is issued). Lines that already have LOT are left untouched (idempotent as before).
 4. MES.usp_FVN_ComponentPO_LabelData / _LabelLog  rows for the A4 label sheet and the INITIAL_PRINT / REPRINT history.
 RBAC stays in the application (WAREHOUSE.COMPONENT_PO). Apply after migration 060. SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54500, 'Migration 061 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '060')
    THROW 54501, 'Migration 060 must be applied before 061.', 1;
IF OBJECT_ID(N'MES.FVN_ComponentLot', N'U') IS NULL OR COL_LENGTH(N'MES.FVN_ComponentLot', N'MaterialLotId') IS NULL
    THROW 54502, 'Migration 032 (component LOT with MaterialLotId) must be applied before 061.', 1;
GO

IF OBJECT_ID(N'MES.FVN_ComponentPackSpec', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ComponentPackSpec
    (
        PackSpecId   bigint IDENTITY(1,1) NOT NULL,
        SiteCode     nvarchar(30)  NOT NULL,
        SupplierCode nvarchar(60)  NOT NULL CONSTRAINT DF_MES_FVN_ComponentPackSpec_Supplier DEFAULT (N''),
        PartNo       nvarchar(100) NOT NULL,
        PackQty      decimal(18,3) NOT NULL,
        MaxCloseQty  decimal(18,3) NULL,
        IsActive     bit           NOT NULL CONSTRAINT DF_MES_FVN_ComponentPackSpec_Active DEFAULT (1),
        CreatedAtUtc datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ComponentPackSpec_Created DEFAULT (SYSUTCDATETIME()),
        CreatedBy    nvarchar(100) NOT NULL,
        UpdatedAtUtc datetime2(3)  NULL,
        UpdatedBy    nvarchar(100) NULL,
        CONSTRAINT PK_MES_FVN_ComponentPackSpec PRIMARY KEY (PackSpecId),
        CONSTRAINT UQ_MES_FVN_ComponentPackSpec UNIQUE (SiteCode, SupplierCode, PartNo),
        CONSTRAINT CK_MES_FVN_ComponentPackSpec_Qty CHECK (PackQty > 0 AND (MaxCloseQty IS NULL OR MaxCloseQty > 0))
    );
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPackSpec_Upsert
    @SiteCode     nvarchar(30),
    @SupplierCode nvarchar(60) = N'',
    @PartNo       nvarchar(100),
    @PackQty      decimal(18,3),
    @MaxCloseQty  decimal(18,3) = NULL,
    @IsActive     bit = 1,
    @ActorCode    nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode     = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @SupplierCode = LTRIM(RTRIM(ISNULL(@SupplierCode, N'')));
    SET @PartNo       = LTRIM(RTRIM(ISNULL(@PartNo, N'')));
    SET @IsActive     = ISNULL(@IsActive, 1);
    IF @SiteCode = N'' OR @PartNo = N'' THROW 54510, 'SiteCode and PartNo are required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 54511, 'ActorCode is required.', 1;
    IF @PackQty IS NULL OR @PackQty <= 0 THROW 54512, 'PackQty must be greater than 0.', 1;
    IF @MaxCloseQty IS NOT NULL AND @MaxCloseQty <= 0 THROW 54513, 'MaxCloseQty must be greater than 0.', 1;
    IF @IsActive = 1 AND NOT EXISTS (SELECT 1 FROM IFS.FVN_InventoryPart WHERE SiteCode = @SiteCode AND PartNo = @PartNo)
        THROW 54514, 'PartNo does not exist in the IFS part master of the Site.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        UPDATE MES.FVN_ComponentPackSpec WITH (UPDLOCK, HOLDLOCK)
           SET PackQty = @PackQty, MaxCloseQty = @MaxCloseQty, IsActive = @IsActive,
               UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorCode
         WHERE SiteCode = @SiteCode AND SupplierCode = @SupplierCode AND PartNo = @PartNo;
        IF @@ROWCOUNT = 0
            INSERT MES.FVN_ComponentPackSpec (SiteCode, SupplierCode, PartNo, PackQty, MaxCloseQty, IsActive, CreatedBy)
            VALUES (@SiteCode, @SupplierCode, @PartNo, @PackQty, @MaxCloseQty, @IsActive, @ActorCode);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPO_Release
    @PurchaseOrderId bigint, @ReleasedBy nvarchar(100), @LotIssueDate date = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @PurchaseOrderId IS NULL OR @PurchaseOrderId <= 0 THROW 53210, 'PurchaseOrderId is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ReleasedBy)), N'') IS NULL THROW 53211, 'ReleasedBy is required.', 1;
    SET @LotIssueDate = ISNULL(@LotIssueDate, CONVERT(date, GETDATE()));
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status varchar(20), @SupplierCode nvarchar(60), @SiteCode nvarchar(30);
        SELECT @Status = Status, @SupplierCode = SupplierCode, @SiteCode = SiteCode
          FROM MES.FVN_ComponentPurchaseOrder WITH (UPDLOCK, HOLDLOCK)
         WHERE PurchaseOrderId = @PurchaseOrderId;
        IF @Status IS NULL THROW 53212, 'Component purchase order was not found.', 1;
        IF @Status IN ('CANCELLED', 'CLOSED') THROW 53213, 'Closed/cancelled order cannot be released.', 1;
        IF @Status IN ('RELEASED', 'PARTIALLY_RECEIVED', 'RECEIVED')
        BEGIN
            COMMIT TRANSACTION;
            SELECT l.ComponentLotId, l.MaterialLotId, l.LotNo, l.QrPayload, l.IssuedQty, p.[LineNo], p.PartNo, p.PartDescription
              FROM MES.FVN_ComponentLot l JOIN MES.FVN_ComponentPurchaseOrderLine p ON p.PurchaseOrderLineId = l.PurchaseOrderLineId
             WHERE p.PurchaseOrderId = @PurchaseOrderId ORDER BY p.[LineNo], l.ComponentLotId;
            RETURN;
        END;
        IF @Status <> 'VALIDATED' THROW 53214, 'Only a VALIDATED MES order can be released.', 1;
        IF EXISTS (SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId = @PurchaseOrderId AND (SiteCode <> @SiteCode OR OrderQty <= 0))
            THROW 53215, 'Order line SiteCode or quantity is invalid.', 1;
        IF NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentPurchaseOrderLine WHERE PurchaseOrderId = @PurchaseOrderId)
            THROW 53216, 'Order has no lines.', 1;

        /* Every part that still needs LOT must have an active packing spec (supplier-specific or generic). */
        DECLARE @Missing nvarchar(2000), @MissingMsg nvarchar(2047);
        SET @Missing = (SELECT N', ' + x.PartNo
                          FROM (SELECT DISTINCT l.PartNo
                                  FROM MES.FVN_ComponentPurchaseOrderLine l
                                 WHERE l.PurchaseOrderId = @PurchaseOrderId
                                   AND NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentLot c WHERE c.PurchaseOrderLineId = l.PurchaseOrderLineId)
                                   AND NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentPackSpec s
                                                    WHERE s.SiteCode = l.SiteCode AND s.PartNo = l.PartNo AND s.IsActive = 1
                                                      AND s.SupplierCode IN (@SupplierCode, N''))) x
                         ORDER BY x.PartNo
                           FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(2000)');
        IF @Missing IS NOT NULL
        BEGIN
            SET @MissingMsg = N'Chưa có quy cách đóng gói cho mã: ' + STUFF(@Missing, 1, 2, N'') + N'. Khai báo quy cách rồi phát hành lại.';
            THROW 54360, @MissingMsg, 1;
        END;

        DECLARE @LineId bigint, @PartNo nvarchar(100), @Qty decimal(18,3), @ItemId5 char(5), @SupplierId5 char(5), @DocNo nvarchar(40),
                @BaseLot nvarchar(120), @LotNo nvarchar(120), @Qr nvarchar(500), @MaterialLotId bigint, @ComponentLotId bigint,
                @Pack decimal(18,3), @Count int, @K int, @LotQty decimal(18,3);
        EXEC MES.usp_FVN_AllocateSupplierId5 @SupplierCode = @SupplierCode, @SupplierId5 = @SupplierId5 OUTPUT;
        DECLARE c CURSOR LOCAL FAST_FORWARD FOR
            SELECT PurchaseOrderLineId, PartNo, OrderQty FROM MES.FVN_ComponentPurchaseOrderLine WITH (UPDLOCK, HOLDLOCK)
             WHERE PurchaseOrderId = @PurchaseOrderId ORDER BY [LineNo];
        OPEN c; FETCH NEXT FROM c INTO @LineId, @PartNo, @Qty;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            IF EXISTS (SELECT 1 FROM MES.FVN_ComponentLot WHERE PurchaseOrderLineId = @LineId)
            BEGIN
                IF EXISTS (SELECT 1 FROM MES.FVN_ComponentLot WHERE PurchaseOrderLineId = @LineId AND MaterialLotId IS NULL)
                    THROW 53218, 'Component LOT exists but canonical MaterialLot link is missing; reconcile before release.', 1;
            END
            ELSE
            BEGIN
                SELECT TOP (1) @Pack = s.PackQty
                  FROM MES.FVN_ComponentPackSpec s
                 WHERE s.SiteCode = @SiteCode AND s.PartNo = @PartNo AND s.IsActive = 1 AND s.SupplierCode IN (@SupplierCode, N'')
                 ORDER BY CASE WHEN s.SupplierCode = N'' THEN 1 ELSE 0 END;
                SET @Count = CONVERT(int, CEILING(@Qty / @Pack));
                IF @Count > 5000 THROW 54361, N'Quy cách đóng gói quá nhỏ so với số lượng đặt: vượt 5000 tem cho một dòng.', 1;

                EXEC MES.usp_FVN_AllocateItemId5 @SiteCode = @SiteCode, @PartNo = @PartNo, @ItemId5 = @ItemId5 OUTPUT;
                SET @DocNo = CONVERT(varchar(20), @PurchaseOrderId) + N'-' + CONVERT(varchar(20), @LineId);
                SET @BaseLot = CONVERT(char(6), @LotIssueDate, 12) + @SupplierId5 + @ItemId5 + @DocNo;
                SET @K = 1;
                WHILE @K <= @Count
                BEGIN
                    SET @LotQty = CASE WHEN @K < @Count THEN @Pack ELSE @Qty - @Pack * (@Count - 1) END;
                    SET @LotNo = @BaseLot + N'-' + CONVERT(nvarchar(10), @K);
                    IF LEN(@LotNo) > 120 THROW 53217, 'Generated component LOT exceeds 120 characters.', 1;
                    SET @Qr = N'FVN1|' + @LotNo;
                    INSERT MES.FVN_ComponentLot (PurchaseOrderLineId, SiteCode, PartNo, SupplierCode, LotNo, LotIssueDate, IssuedQty, QrPayload)
                    VALUES (@LineId, @SiteCode, @PartNo, @SupplierCode, @LotNo, @LotIssueDate, @LotQty, @Qr);
                    SET @ComponentLotId = SCOPE_IDENTITY();
                    INSERT MES.FVN_MaterialLot (SiteCode, PartNo, LotNo, LotType, SupplierLotNo, SupplierCode, LotStatus, SourceSystem, SourceReference)
                    VALUES (@SiteCode, @PartNo, @LotNo, 'RAW', @LotNo, @SupplierCode, 'BLOCKED', 'MES', N'COMPONENT_PO:' + CONVERT(nvarchar(30), @ComponentLotId));
                    SET @MaterialLotId = SCOPE_IDENTITY();
                    UPDATE MES.FVN_ComponentLot SET MaterialLotId = @MaterialLotId WHERE ComponentLotId = @ComponentLotId;
                    SET @K += 1;
                END;
            END;
            FETCH NEXT FROM c INTO @LineId, @PartNo, @Qty;
        END;
        CLOSE c; DEALLOCATE c;
        UPDATE MES.FVN_ComponentPurchaseOrder
           SET Status = 'RELEASED', ReleasedAtUtc = SYSUTCDATETIME(), ReleasedBy = @ReleasedBy,
               UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ReleasedBy
         WHERE PurchaseOrderId = @PurchaseOrderId;
        COMMIT TRANSACTION;
        SELECT l.ComponentLotId, l.MaterialLotId, l.LotNo, l.QrPayload, l.IssuedQty, p.[LineNo], p.PartNo, p.PartDescription
          FROM MES.FVN_ComponentLot l JOIN MES.FVN_ComponentPurchaseOrderLine p ON p.PurchaseOrderLineId = l.PurchaseOrderLineId
         WHERE p.PurchaseOrderId = @PurchaseOrderId ORDER BY p.[LineNo], l.ComponentLotId;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'c') >= 0 CLOSE c;
        IF CURSOR_STATUS('local', 'c') > -3 DEALLOCATE c;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPO_LabelData
    @PurchaseOrderId bigint,
    @OnlyUnprinted   bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @PurchaseOrderId IS NULL OR @PurchaseOrderId <= 0 THROW 54520, 'PurchaseOrderId is required.', 1;
    /* Stt is the pack number inside the PO line, counted over every LOT, so it does not shift when only some are printed. */
    ;WITH x AS
    (
        SELECT c.ComponentLotId, c.LotNo, c.QrPayload, c.PartNo, l.PartDescription, c.IssuedQty, c.LotIssueDate, l.[LineNo],
               ROW_NUMBER() OVER (PARTITION BY l.PurchaseOrderLineId ORDER BY c.ComponentLotId) AS Seq,
               h.PurchaseOrderNo, h.SiteCode, h.SupplierCode, h.SupplierNameSnapshot AS SupplierName,
               CASE WHEN EXISTS (SELECT 1 FROM MES.FVN_ComponentLotLabelHistory y
                                  WHERE y.ComponentLotId = c.ComponentLotId AND y.ActionCode = 'INITIAL_PRINT') THEN 1 ELSE 0 END AS IsPrinted
          FROM MES.FVN_ComponentLot c
          JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = c.PurchaseOrderLineId
          JOIN MES.FVN_ComponentPurchaseOrder h ON h.PurchaseOrderId = l.PurchaseOrderId
         WHERE l.PurchaseOrderId = @PurchaseOrderId AND c.Status <> 'CANCELLED'
    )
    SELECT * FROM x WHERE @OnlyUnprinted = 0 OR IsPrinted = 0 ORDER BY [LineNo], ComponentLotId;
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
    INSERT MES.FVN_ComponentLotLabelHistory (ComponentLotId, ActionCode, CopyCount, Reason, RequestedBy)
    SELECT ComponentLotId, @ActionCode, 1, NULLIF(LTRIM(RTRIM(@Reason)), N''), @RequestedBy FROM @Ids;
    SELECT COUNT(*) AS LoggedLots FROM @Ids;
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '061')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('061', N'Component packing spec, LOT per pack on release, QR label data/log');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '061';
GO
