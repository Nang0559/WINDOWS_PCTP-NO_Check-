/*
 Migration 063: pallet labels (tem palet) built from MaxCloseQty of the packing spec.

 1. MES.FVN_ComponentPallet / MES.FVN_ComponentPalletLabelHistory, MES.FVN_ComponentLot.ComponentPalletId.
    A pallet groups consecutive small-label LOTs of one PO line until adding the next LOT would exceed MaxCloseQty
    (spec of the PO supplier wins over the generic N'' row). Membership is stored, so changing the spec later never
    moves LOTs between pallets that were already printed. QR of a pallet = N'FVN2|' + PalletNo.
 2. MES.usp_FVN_ComponentPO_PalletBuild   idempotent: pallets only for LOTs that have none yet. A PartNo without
    MaxCloseQty stops the build and is listed in the message. CLOSED PO never gets new pallets.
 3. MES.usp_FVN_ComponentPO_PalletData / _PalletLabelLog   rows for the pallet sheet and INITIAL_PRINT / REPRINT history
    (REPRINT is allowed in every state, including CLOSED).
 4. MES.usp_FVN_ComponentPallet_Lots   LOTs of a scanned pallet QR; receiving then posts each LOT through the existing
    usp_FVN_ComponentReceipt_Post (one idempotent receipt per LOT).
 Apply after migration 062. SQL Server 2016 SP1+.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54700, 'Migration 063 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '062')
    THROW 54701, 'Migration 062 must be applied before 063.', 1;
GO

IF OBJECT_ID(N'MES.FVN_ComponentPallet', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ComponentPallet
    (
        ComponentPalletId   bigint IDENTITY(1,1) NOT NULL,
        PurchaseOrderLineId bigint        NOT NULL,
        SiteCode            nvarchar(30)  NOT NULL,
        PartNo              nvarchar(100) NOT NULL,
        PalletSeq           int           NOT NULL,
        PalletNo            nvarchar(120) NOT NULL,
        QrPayload           nvarchar(500) NOT NULL,
        TotalQty            decimal(18,3) NOT NULL,
        LotCount            int           NOT NULL,
        MaxCloseQtySnapshot decimal(18,3) NOT NULL,
        CreatedAtUtc        datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ComponentPallet_Created DEFAULT (SYSUTCDATETIME()),
        CreatedBy           nvarchar(100) NOT NULL,
        CONSTRAINT PK_MES_FVN_ComponentPallet PRIMARY KEY (ComponentPalletId),
        CONSTRAINT UQ_MES_FVN_ComponentPallet_Seq UNIQUE (PurchaseOrderLineId, PalletSeq),
        CONSTRAINT UQ_MES_FVN_ComponentPallet_Qr UNIQUE (QrPayload),
        CONSTRAINT FK_MES_FVN_ComponentPallet_Line FOREIGN KEY (PurchaseOrderLineId)
            REFERENCES MES.FVN_ComponentPurchaseOrderLine (PurchaseOrderLineId)
    );
END;
GO

IF COL_LENGTH(N'MES.FVN_ComponentLot', N'ComponentPalletId') IS NULL
    ALTER TABLE MES.FVN_ComponentLot ADD ComponentPalletId bigint NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MES_FVN_ComponentLot_Pallet')
    ALTER TABLE MES.FVN_ComponentLot ADD CONSTRAINT FK_MES_FVN_ComponentLot_Pallet
        FOREIGN KEY (ComponentPalletId) REFERENCES MES.FVN_ComponentPallet (ComponentPalletId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MES_FVN_ComponentLot_Pallet' AND object_id = OBJECT_ID(N'MES.FVN_ComponentLot'))
    CREATE INDEX IX_MES_FVN_ComponentLot_Pallet ON MES.FVN_ComponentLot (ComponentPalletId) WHERE ComponentPalletId IS NOT NULL;
GO

IF OBJECT_ID(N'MES.FVN_ComponentPalletLabelHistory', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_ComponentPalletLabelHistory
    (
        PalletLabelHistoryId bigint IDENTITY(1,1) NOT NULL,
        ComponentPalletId    bigint        NOT NULL,
        ActionCode           varchar(20)   NOT NULL,
        Reason               nvarchar(300) NULL,
        RequestedBy          nvarchar(100) NOT NULL,
        RequestedAtUtc       datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_ComponentPalletLabelHistory_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MES_FVN_ComponentPalletLabelHistory PRIMARY KEY (PalletLabelHistoryId),
        CONSTRAINT CK_MES_FVN_ComponentPalletLabelHistory_Action CHECK (ActionCode IN ('INITIAL_PRINT', 'REPRINT')),
        CONSTRAINT FK_MES_FVN_ComponentPalletLabelHistory_Pallet FOREIGN KEY (ComponentPalletId)
            REFERENCES MES.FVN_ComponentPallet (ComponentPalletId)
    );
    CREATE INDEX IX_MES_FVN_ComponentPalletLabelHistory_Pallet ON MES.FVN_ComponentPalletLabelHistory (ComponentPalletId, ActionCode);
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPO_PalletBuild
    @PurchaseOrderId bigint,
    @ActorCode       nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @PurchaseOrderId IS NULL OR @PurchaseOrderId <= 0 THROW 54710, 'PurchaseOrderId is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 54711, 'ActorCode is required.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status varchar(20), @SupplierCode nvarchar(60), @SiteCode nvarchar(30);
        SELECT @Status = Status, @SupplierCode = SupplierCode, @SiteCode = SiteCode
          FROM MES.FVN_ComponentPurchaseOrder WITH (UPDLOCK, HOLDLOCK) WHERE PurchaseOrderId = @PurchaseOrderId;
        IF @Status IS NULL THROW 54712, 'Component purchase order was not found.', 1;
        IF @Status NOT IN ('RELEASED', 'PARTIALLY_RECEIVED', 'RECEIVED', 'CLOSED')
            THROW 54713, 'Pallet labels exist only after the purchase order is released.', 1;

        CREATE TABLE #Lots (Ord int IDENTITY(1,1) PRIMARY KEY, ComponentLotId bigint NOT NULL, LineId bigint NOT NULL,
                            PartNo nvarchar(100) NOT NULL, Qty decimal(18,3) NOT NULL, MaxQty decimal(18,3) NULL);
        INSERT #Lots (ComponentLotId, LineId, PartNo, Qty, MaxQty)
        SELECT c.ComponentLotId, l.PurchaseOrderLineId, l.PartNo, c.IssuedQty,
               (SELECT TOP (1) s.MaxCloseQty FROM MES.FVN_ComponentPackSpec s
                 WHERE s.SiteCode = l.SiteCode AND s.PartNo = l.PartNo AND s.IsActive = 1 AND s.SupplierCode IN (@SupplierCode, N'')
                 ORDER BY CASE WHEN s.SupplierCode = N'' THEN 1 ELSE 0 END)
          FROM MES.FVN_ComponentLot c
          JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = c.PurchaseOrderLineId
         WHERE l.PurchaseOrderId = @PurchaseOrderId AND c.ComponentPalletId IS NULL AND c.Status <> 'CANCELLED'
         ORDER BY l.[LineNo], c.ComponentLotId;

        IF NOT EXISTS (SELECT 1 FROM #Lots) OR @Status = 'CLOSED'
        BEGIN
            COMMIT TRANSACTION;
            RETURN;
        END;

        DECLARE @Missing nvarchar(2000), @MissingMsg nvarchar(2047);
        SET @Missing = (SELECT N', ' + x.PartNo FROM (SELECT DISTINCT PartNo FROM #Lots WHERE MaxQty IS NULL) x
                         ORDER BY x.PartNo FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(2000)');
        IF @Missing IS NOT NULL
        BEGIN
            SET @MissingMsg = N'Chưa khai báo SL / palet (MaxCloseQty) trong quy cách đóng gói cho mã: ' + STUFF(@Missing, 1, 2, N'') + N'.';
            THROW 54714, @MissingMsg, 1;
        END;

        DECLARE @LotId bigint, @LineId bigint, @PartNo nvarchar(100), @Qty decimal(18,3), @Max decimal(18,3),
                @CurLine bigint = NULL, @PalletId bigint = NULL, @Sum decimal(18,3) = 0, @Cnt int = 0, @Seq int, @PalletNo nvarchar(120);
        DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT ComponentLotId, LineId, PartNo, Qty, MaxQty FROM #Lots ORDER BY Ord;
        OPEN c; FETCH NEXT FROM c INTO @LotId, @LineId, @PartNo, @Qty, @Max;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            IF @PalletId IS NULL OR @CurLine <> @LineId OR @Sum + @Qty > @Max
            BEGIN
                SELECT @Seq = ISNULL(MAX(PalletSeq), 0) + 1 FROM MES.FVN_ComponentPallet WHERE PurchaseOrderLineId = @LineId;
                SET @PalletNo = CONVERT(nvarchar(20), @PurchaseOrderId) + N'-' + CONVERT(nvarchar(20), @LineId) + N'-P' + CONVERT(nvarchar(10), @Seq);
                INSERT MES.FVN_ComponentPallet (PurchaseOrderLineId, SiteCode, PartNo, PalletSeq, PalletNo, QrPayload, TotalQty, LotCount, MaxCloseQtySnapshot, CreatedBy)
                VALUES (@LineId, @SiteCode, @PartNo, @Seq, @PalletNo, N'FVN2|' + @PalletNo, 0, 0, @Max, @ActorCode);
                SET @PalletId = SCOPE_IDENTITY(); SET @CurLine = @LineId; SET @Sum = 0; SET @Cnt = 0;
            END;
            SET @Sum += @Qty; SET @Cnt += 1;
            UPDATE MES.FVN_ComponentLot SET ComponentPalletId = @PalletId WHERE ComponentLotId = @LotId;
            UPDATE MES.FVN_ComponentPallet SET TotalQty = @Sum, LotCount = @Cnt WHERE ComponentPalletId = @PalletId;
            FETCH NEXT FROM c INTO @LotId, @LineId, @PartNo, @Qty, @Max;
        END;
        CLOSE c; DEALLOCATE c;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local', 'c') >= 0 CLOSE c;
        IF CURSOR_STATUS('local', 'c') > -3 DEALLOCATE c;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPO_PalletData
    @PurchaseOrderId bigint,
    @OnlyUnprinted   bit = 0,
    @ActorCode       nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC MES.usp_FVN_ComponentPO_PalletBuild @PurchaseOrderId = @PurchaseOrderId, @ActorCode = @ActorCode;
    ;WITH x AS
    (
        SELECT p.ComponentPalletId, p.PalletNo, p.QrPayload, p.PalletSeq, p.TotalQty, p.LotCount, p.PartNo,
               l.PartDescription, l.[LineNo], h.PurchaseOrderNo, h.SiteCode, h.SupplierCode, h.SupplierNameSnapshot AS SupplierName,
               COUNT(*) OVER (PARTITION BY p.PurchaseOrderLineId) AS PalletCount,
               (SELECT MIN(c.LotIssueDate) FROM MES.FVN_ComponentLot c WHERE c.ComponentPalletId = p.ComponentPalletId) AS LotIssueDate,
               CASE WHEN EXISTS (SELECT 1 FROM MES.FVN_ComponentPalletLabelHistory y
                                  WHERE y.ComponentPalletId = p.ComponentPalletId AND y.ActionCode = 'INITIAL_PRINT') THEN 1 ELSE 0 END AS IsPrinted
          FROM MES.FVN_ComponentPallet p
          JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = p.PurchaseOrderLineId
          JOIN MES.FVN_ComponentPurchaseOrder h ON h.PurchaseOrderId = l.PurchaseOrderId
         WHERE l.PurchaseOrderId = @PurchaseOrderId
    )
    SELECT * FROM x WHERE @OnlyUnprinted = 0 OR IsPrinted = 0 ORDER BY [LineNo], PalletSeq;
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPO_PalletLabelLog
    @PurchaseOrderId bigint,
    @ActionCode      varchar(20),
    @Reason          nvarchar(300) = NULL,
    @RequestedBy     nvarchar(100),
    @PalletIdsJson   nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @ActionCode NOT IN ('INITIAL_PRINT', 'REPRINT') THROW 54721, 'ActionCode must be INITIAL_PRINT or REPRINT.', 1;
    IF @ActionCode = 'REPRINT' AND NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL THROW 54722, 'Reprint reason is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@RequestedBy)), N'') IS NULL THROW 54723, 'RequestedBy is required.', 1;
    DECLARE @Ids TABLE (ComponentPalletId bigint PRIMARY KEY);
    INSERT @Ids SELECT DISTINCT CONVERT(bigint, [value]) FROM OPENJSON(@PalletIdsJson);
    IF NOT EXISTS (SELECT 1 FROM @Ids) THROW 54724, 'No pallet to log.', 1;
    IF EXISTS (SELECT 1 FROM @Ids i WHERE NOT EXISTS (
                   SELECT 1 FROM MES.FVN_ComponentPallet p
                     JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = p.PurchaseOrderLineId
                    WHERE p.ComponentPalletId = i.ComponentPalletId AND l.PurchaseOrderId = @PurchaseOrderId))
        THROW 54725, 'A pallet does not belong to this purchase order.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Lock int = (SELECT TOP (1) 1 FROM MES.FVN_ComponentPurchaseOrder WITH (UPDLOCK, HOLDLOCK) WHERE PurchaseOrderId = @PurchaseOrderId);
        INSERT MES.FVN_ComponentPalletLabelHistory (ComponentPalletId, ActionCode, Reason, RequestedBy)
        SELECT i.ComponentPalletId, @ActionCode, NULLIF(LTRIM(RTRIM(@Reason)), N''), @RequestedBy
          FROM @Ids i
         WHERE @ActionCode = 'REPRINT'
            OR NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentPalletLabelHistory y
                            WHERE y.ComponentPalletId = i.ComponentPalletId AND y.ActionCode = 'INITIAL_PRINT');
        DECLARE @Logged int = @@ROWCOUNT;
        COMMIT TRANSACTION;
        SELECT @Logged AS LoggedPallets;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPallet_Lots
    @SiteCode  nvarchar(30),
    @QrPayload nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentPallet WHERE QrPayload = @QrPayload AND SiteCode = @SiteCode)
        THROW 54730, 'QR is not a pallet label of this Site.', 1;
    SELECT c.ComponentLotId, c.LotNo, c.QrPayload AS LotQrPayload, c.IssuedQty, c.ReceivedQty, c.Status, p.PalletNo, h.PurchaseOrderNo
      FROM MES.FVN_ComponentPallet p
      JOIN MES.FVN_ComponentLot c ON c.ComponentPalletId = p.ComponentPalletId
      JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = p.PurchaseOrderLineId
      JOIN MES.FVN_ComponentPurchaseOrder h ON h.PurchaseOrderId = l.PurchaseOrderId
     WHERE p.QrPayload = @QrPayload AND p.SiteCode = @SiteCode AND c.Status <> 'CANCELLED'
     ORDER BY c.ComponentLotId;
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '063')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('063', N'Component pallet labels from MaxCloseQty, pallet receiving lookup');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '063';
GO
