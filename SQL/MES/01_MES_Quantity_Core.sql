/*
    PCTP MES - Quantity-only core schema
    Target: SQL Server
    Branch: feature/mes-product-genealogy-timeline

    IMPORTANT
    - Additive MES tables only. No duplicate Part, Warehouse, Location, Rack or Slot master.
    - PartNo + Site are external natural keys. Resolve them against existing PCTP masters
      in application code until the exact existing table/key constraints are verified.
    - No price, currency, cost, valuation or accounting columns.
    - IFS is read-only and authoritative for Part Catalog, Inventory Part and BOM source data.
    - This script is a schema proposal/baseline. Review names/types against the target DB
      and run 00_MES_SourceDiscovery_ReadOnly.sql before deployment.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'MES') IS NULL EXEC(N'CREATE SCHEMA MES');

IF OBJECT_ID(N'MES.IfsSyncBatch', N'U') IS NULL
BEGIN
    CREATE TABLE MES.IfsSyncBatch
    (
        SyncBatchId       bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_IfsSyncBatch PRIMARY KEY,
        SourceSystem      nvarchar(30) NOT NULL CONSTRAINT DF_MES_IfsSyncBatch_Source DEFAULT(N'IFS9'),
        EntityName        nvarchar(50) NOT NULL,
        SiteCode          nvarchar(30) NULL,
        StartedAt         datetime2(3) NOT NULL CONSTRAINT DF_MES_IfsSyncBatch_Started DEFAULT(SYSUTCDATETIME()),
        CompletedAt       datetime2(3) NULL,
        SyncMode          varchar(12) NOT NULL,
        Status            varchar(16) NOT NULL,
        ReadCount         int NOT NULL CONSTRAINT DF_MES_IfsSyncBatch_Read DEFAULT(0),
        InsertCount       int NOT NULL CONSTRAINT DF_MES_IfsSyncBatch_Insert DEFAULT(0),
        UpdateCount       int NOT NULL CONSTRAINT DF_MES_IfsSyncBatch_Update DEFAULT(0),
        ErrorCount        int NOT NULL CONSTRAINT DF_MES_IfsSyncBatch_Error DEFAULT(0),
        SourceWatermark   nvarchar(100) NULL,
        ErrorSummary      nvarchar(2000) NULL,
        CONSTRAINT CK_MES_IfsSyncBatch_Mode CHECK (SyncMode IN ('FULL','INCREMENTAL')),
        CONSTRAINT CK_MES_IfsSyncBatch_Status CHECK (Status IN ('RUNNING','SUCCEEDED','PARTIAL','FAILED'))
    );
END;

IF OBJECT_ID(N'MES.BomHeader', N'U') IS NULL
BEGIN
    CREATE TABLE MES.BomHeader
    (
        BomHeaderId       bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_BomHeader PRIMARY KEY,
        SiteCode          nvarchar(30) NOT NULL,
        ParentPartNo      nvarchar(60) NOT NULL,
        RevisionCode      nvarchar(30) NOT NULL,
        StructureType     nvarchar(30) NOT NULL,
        AlternateNo       nvarchar(30) NOT NULL CONSTRAINT DF_MES_BomHeader_Alt DEFAULT(N'*'),
        BomStatus         nvarchar(20) NOT NULL,
        EffectiveFrom     datetime2(0) NULL,
        EffectiveTo       datetime2(0) NULL,
        SourceModifiedAt  datetime2(3) NULL,
        SourceRowKey      nvarchar(200) NULL,
        SourceHash        varbinary(32) NULL,
        SyncBatchId       bigint NULL,
        IsCurrent         bit NOT NULL CONSTRAINT DF_MES_BomHeader_Current DEFAULT(1),
        CreatedAt         datetime2(3) NOT NULL CONSTRAINT DF_MES_BomHeader_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_MES_BomHeader_Sync FOREIGN KEY (SyncBatchId) REFERENCES MES.IfsSyncBatch(SyncBatchId),
        CONSTRAINT UQ_MES_BomHeader_Source UNIQUE (SiteCode, ParentPartNo, RevisionCode, StructureType, AlternateNo, SourceHash)
    );
    CREATE INDEX IX_MES_BomHeader_Resolve ON MES.BomHeader(SiteCode, ParentPartNo, StructureType, BomStatus, EffectiveFrom, EffectiveTo);
END;

IF OBJECT_ID(N'MES.BomLine', N'U') IS NULL
BEGIN
    CREATE TABLE MES.BomLine
    (
        BomLineId          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_BomLine PRIMARY KEY,
        BomHeaderId        bigint NOT NULL,
        SourceLineNo       nvarchar(40) NOT NULL,
        ComponentPartNo    nvarchar(60) NOT NULL,
        QtyPerAssembly     decimal(19,6) NOT NULL,
        ComponentUom       nvarchar(20) NOT NULL,
        OperationNo        nvarchar(30) NULL,
        LineType           nvarchar(20) NOT NULL CONSTRAINT DF_MES_BomLine_Type DEFAULT(N'COMPONENT'),
        IsConsumed         bit NOT NULL CONSTRAINT DF_MES_BomLine_Consumed DEFAULT(1),
        IsLotOrigin        bit NOT NULL CONSTRAINT DF_MES_BomLine_LotOrigin DEFAULT(0),
        IsPhantom          bit NOT NULL CONSTRAINT DF_MES_BomLine_Phantom DEFAULT(0),
        ScrapFactor        decimal(9,6) NULL,
        SourceModifiedAt   datetime2(3) NULL,
        CONSTRAINT FK_MES_BomLine_Header FOREIGN KEY (BomHeaderId) REFERENCES MES.BomHeader(BomHeaderId),
        CONSTRAINT UQ_MES_BomLine_Source UNIQUE (BomHeaderId, SourceLineNo),
        CONSTRAINT CK_MES_BomLine_Qty CHECK (QtyPerAssembly >= 0),
        CONSTRAINT CK_MES_BomLine_Type CHECK (LineType IN ('COMPONENT','BYPRODUCT','COPRODUCT','DISASSEMBLY')),
        CONSTRAINT CK_MES_BomLine_Scrap CHECK (ScrapFactor IS NULL OR (ScrapFactor >= 0 AND ScrapFactor <= 1))
    );
    CREATE INDEX IX_MES_BomLine_Component ON MES.BomLine(ComponentPartNo, BomHeaderId);
END;

IF OBJECT_ID(N'MES.PurchaseOrder', N'U') IS NULL
BEGIN
    CREATE TABLE MES.PurchaseOrder
    (
        PurchaseOrderId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_PurchaseOrder PRIMARY KEY,
        SourceSystem     nvarchar(20) NOT NULL CONSTRAINT DF_MES_PO_Source DEFAULT(N'UPLOAD'),
        SourceKey        nvarchar(100) NULL,
        SupplierCode     nvarchar(50) NULL,
        SupplierName     nvarchar(200) NULL,
        SiteCode         nvarchar(30) NOT NULL,
        PurchaseOrderNo  nvarchar(60) NOT NULL,
        OrderDate        date NULL,
        Status           nvarchar(20) NOT NULL CONSTRAINT DF_MES_PO_Status DEFAULT(N'OPEN'),
        ImportBatchKey   uniqueidentifier NULL,
        CreatedAt        datetime2(3) NOT NULL CONSTRAINT DF_MES_PO_Created DEFAULT(SYSUTCDATETIME()),
        CreatedBy        nvarchar(100) NULL,
        CONSTRAINT UQ_MES_PurchaseOrder UNIQUE (SiteCode, PurchaseOrderNo)
    );
END;

IF OBJECT_ID(N'MES.PurchaseOrderLine', N'U') IS NULL
BEGIN
    CREATE TABLE MES.PurchaseOrderLine
    (
        PurchaseOrderLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_PurchaseOrderLine PRIMARY KEY,
        PurchaseOrderId     bigint NOT NULL,
        SourceLineNo        nvarchar(40) NOT NULL,
        PartNo              nvarchar(60) NOT NULL,
        OrderedQty          decimal(19,6) NOT NULL,
        UomCode             nvarchar(20) NOT NULL,
        DueDate             date NULL,
        CreatedAt           datetime2(3) NOT NULL CONSTRAINT DF_MES_POL_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_MES_POL_Header FOREIGN KEY (PurchaseOrderId) REFERENCES MES.PurchaseOrder(PurchaseOrderId),
        CONSTRAINT UQ_MES_POL_Source UNIQUE (PurchaseOrderId, SourceLineNo),
        CONSTRAINT CK_MES_POL_Qty CHECK (OrderedQty > 0)
    );
END;

IF OBJECT_ID(N'MES.MaterialLot', N'U') IS NULL
BEGIN
    CREATE TABLE MES.MaterialLot
    (
        MaterialLotId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialLot PRIMARY KEY,
        PartNo             nvarchar(60) NOT NULL,
        SupplierLotNo      nvarchar(100) NOT NULL,
        InternalLotNo      nvarchar(100) NULL,
        SupplierCode       nvarchar(50) NULL,
        SiteCode           nvarchar(30) NOT NULL,
        ManufacturedDate   date NULL,
        ExpiryDate         date NULL,
        LotStatus          nvarchar(20) NOT NULL CONSTRAINT DF_MES_Lot_Status DEFAULT(N'RECEIVED'),
        CreatedAt          datetime2(3) NOT NULL CONSTRAINT DF_MES_Lot_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT UQ_MES_MaterialLot UNIQUE (SiteCode, PartNo, SupplierLotNo)
    );
END;

IF OBJECT_ID(N'MES.MaterialReceipt', N'U') IS NULL
BEGIN
    CREATE TABLE MES.MaterialReceipt
    (
        MaterialReceiptId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialReceipt PRIMARY KEY,
        ReceiptNo         nvarchar(60) NOT NULL,
        SiteCode          nvarchar(30) NOT NULL,
        PurchaseOrderId   bigint NULL,
        ReceivedAt        datetime2(3) NOT NULL,
        ReceivedBy        nvarchar(100) NOT NULL,
        Status            nvarchar(20) NOT NULL CONSTRAINT DF_MES_Receipt_Status DEFAULT(N'DRAFT'),
        ExternalRef       nvarchar(100) NULL,
        CreatedAt         datetime2(3) NOT NULL CONSTRAINT DF_MES_Receipt_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT UQ_MES_MaterialReceipt UNIQUE (SiteCode, ReceiptNo),
        CONSTRAINT FK_MES_Receipt_PO FOREIGN KEY (PurchaseOrderId) REFERENCES MES.PurchaseOrder(PurchaseOrderId)
    );
END;

IF OBJECT_ID(N'MES.MaterialReceiptLine', N'U') IS NULL
BEGIN
    CREATE TABLE MES.MaterialReceiptLine
    (
        MaterialReceiptLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialReceiptLine PRIMARY KEY,
        MaterialReceiptId     bigint NOT NULL,
        PurchaseOrderLineId   bigint NULL,
        MaterialLotId         bigint NOT NULL,
        ReceivedQty           decimal(19,6) NOT NULL,
        AcceptedQty           decimal(19,6) NOT NULL CONSTRAINT DF_MES_ReceiptLine_Accepted DEFAULT(0),
        QuarantineQty         decimal(19,6) NOT NULL CONSTRAINT DF_MES_ReceiptLine_Quarantine DEFAULT(0),
        UomCode               nvarchar(20) NOT NULL,
        TargetLocationCode    nvarchar(60) NULL,
        CONSTRAINT FK_MES_ReceiptLine_Header FOREIGN KEY (MaterialReceiptId) REFERENCES MES.MaterialReceipt(MaterialReceiptId),
        CONSTRAINT FK_MES_ReceiptLine_POL FOREIGN KEY (PurchaseOrderLineId) REFERENCES MES.PurchaseOrderLine(PurchaseOrderLineId),
        CONSTRAINT FK_MES_ReceiptLine_Lot FOREIGN KEY (MaterialLotId) REFERENCES MES.MaterialLot(MaterialLotId),
        CONSTRAINT CK_MES_ReceiptLine_Qty CHECK (ReceivedQty > 0 AND AcceptedQty >= 0 AND QuarantineQty >= 0 AND AcceptedQty + QuarantineQty <= ReceivedQty)
    );
END;

IF OBJECT_ID(N'MES.ProductionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE MES.ProductionPlan
    (
        ProductionPlanId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionPlan PRIMARY KEY,
        PlanNo           nvarchar(60) NOT NULL,
        SiteCode         nvarchar(30) NOT NULL,
        PlanDate         date NOT NULL,
        SourceFileName   nvarchar(260) NULL,
        ImportBatchKey   uniqueidentifier NULL,
        Status           nvarchar(20) NOT NULL CONSTRAINT DF_MES_Plan_Status DEFAULT(N'IMPORTED'),
        CreatedAt        datetime2(3) NOT NULL CONSTRAINT DF_MES_Plan_Created DEFAULT(SYSUTCDATETIME()),
        CreatedBy        nvarchar(100) NULL,
        CONSTRAINT UQ_MES_ProductionPlan UNIQUE (SiteCode, PlanNo)
    );
END;

IF OBJECT_ID(N'MES.ProductionPlanLine', N'U') IS NULL
BEGIN
    CREATE TABLE MES.ProductionPlanLine
    (
        ProductionPlanLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionPlanLine PRIMARY KEY,
        ProductionPlanId     bigint NOT NULL,
        SourceLineNo         nvarchar(40) NOT NULL,
        ProductPartNo        nvarchar(60) NOT NULL,
        PlannedQty           decimal(19,6) NOT NULL,
        UomCode              nvarchar(20) NOT NULL,
        PlannedStart         datetime2(0) NULL,
        PlannedEnd           datetime2(0) NULL,
        CONSTRAINT FK_MES_PlanLine_Header FOREIGN KEY (ProductionPlanId) REFERENCES MES.ProductionPlan(ProductionPlanId),
        CONSTRAINT UQ_MES_PlanLine_Source UNIQUE (ProductionPlanId, SourceLineNo),
        CONSTRAINT CK_MES_PlanLine_Qty CHECK (PlannedQty > 0)
    );
END;

IF OBJECT_ID(N'MES.ProductionOrder', N'U') IS NULL
BEGIN
    CREATE TABLE MES.ProductionOrder
    (
        ProductionOrderId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionOrder PRIMARY KEY,
        OrderNo           nvarchar(60) NOT NULL,
        SiteCode          nvarchar(30) NOT NULL,
        ProductPartNo     nvarchar(60) NOT NULL,
        PlannedQty        decimal(19,6) NOT NULL,
        CompletedQty      decimal(19,6) NOT NULL CONSTRAINT DF_MES_Order_Completed DEFAULT(0),
        UomCode           nvarchar(20) NOT NULL,
        BomHeaderId       bigint NOT NULL,
        ProductionPlanLineId bigint NULL,
        Status            nvarchar(20) NOT NULL CONSTRAINT DF_MES_Order_Status DEFAULT(N'CREATED'),
        ReleasedAt        datetime2(3) NULL,
        ReleasedBy        nvarchar(100) NULL,
        CreatedAt         datetime2(3) NOT NULL CONSTRAINT DF_MES_Order_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT UQ_MES_ProductionOrder UNIQUE (SiteCode, OrderNo),
        CONSTRAINT FK_MES_Order_Bom FOREIGN KEY (BomHeaderId) REFERENCES MES.BomHeader(BomHeaderId),
        CONSTRAINT FK_MES_Order_PlanLine FOREIGN KEY (ProductionPlanLineId) REFERENCES MES.ProductionPlanLine(ProductionPlanLineId),
        CONSTRAINT CK_MES_Order_Qty CHECK (PlannedQty > 0 AND CompletedQty >= 0)
    );
END;

IF OBJECT_ID(N'MES.ProductionOrderMaterial', N'U') IS NULL
BEGIN
    CREATE TABLE MES.ProductionOrderMaterial
    (
        ProductionOrderMaterialId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionOrderMaterial PRIMARY KEY,
        ProductionOrderId         bigint NOT NULL,
        BomLineId                 bigint NULL,
        ComponentPartNo           nvarchar(60) NOT NULL,
        RequiredQty               decimal(19,6) NOT NULL,
        UomCode                   nvarchar(20) NOT NULL,
        OperationNo               nvarchar(30) NULL,
        IsConsumed                bit NOT NULL CONSTRAINT DF_MES_OrderMat_Consumed DEFAULT(1),
        CONSTRAINT FK_MES_OrderMat_Order FOREIGN KEY (ProductionOrderId) REFERENCES MES.ProductionOrder(ProductionOrderId),
        CONSTRAINT FK_MES_OrderMat_BomLine FOREIGN KEY (BomLineId) REFERENCES MES.BomLine(BomLineId),
        CONSTRAINT CK_MES_OrderMat_Qty CHECK (RequiredQty >= 0)
    );
    CREATE INDEX IX_MES_OrderMat_Order ON MES.ProductionOrderMaterial(ProductionOrderId, ComponentPartNo);
END;

IF OBJECT_ID(N'MES.MaterialReservation', N'U') IS NULL
BEGIN
    CREATE TABLE MES.MaterialReservation
    (
        MaterialReservationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialReservation PRIMARY KEY,
        ProductionOrderMaterialId bigint NOT NULL,
        MaterialLotId         bigint NOT NULL,
        LocationCode          nvarchar(60) NOT NULL,
        ReservedQty           decimal(19,6) NOT NULL,
        ReleasedQty           decimal(19,6) NOT NULL CONSTRAINT DF_MES_Reservation_Released DEFAULT(0),
        Status                nvarchar(20) NOT NULL CONSTRAINT DF_MES_Reservation_Status DEFAULT(N'ACTIVE'),
        CreatedAt             datetime2(3) NOT NULL CONSTRAINT DF_MES_Reservation_Created DEFAULT(SYSUTCDATETIME()),
        CreatedBy             nvarchar(100) NOT NULL,
        CONSTRAINT FK_MES_Reservation_OrderMat FOREIGN KEY (ProductionOrderMaterialId) REFERENCES MES.ProductionOrderMaterial(ProductionOrderMaterialId),
        CONSTRAINT FK_MES_Reservation_Lot FOREIGN KEY (MaterialLotId) REFERENCES MES.MaterialLot(MaterialLotId),
        CONSTRAINT CK_MES_Reservation_Qty CHECK (ReservedQty > 0 AND ReleasedQty >= 0 AND ReleasedQty <= ReservedQty)
    );
END;

IF OBJECT_ID(N'MES.MaterialExecutionEvent', N'U') IS NULL
BEGIN
    CREATE TABLE MES.MaterialExecutionEvent
    (
        MaterialExecutionEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialExecutionEvent PRIMARY KEY,
        ProductionOrderMaterialId bigint NOT NULL,
        MaterialLotId             bigint NOT NULL,
        EventType                 varchar(12) NOT NULL,
        Quantity                  decimal(19,6) NOT NULL,
        UomCode                   nvarchar(20) NOT NULL,
        FromLocationCode          nvarchar(60) NULL,
        ToLocationCode            nvarchar(60) NULL,
        EventAt                   datetime2(3) NOT NULL,
        OperatorCode              nvarchar(100) NOT NULL,
        QrRawValue                nvarchar(1000) NULL,
        IdempotencyKey            nvarchar(150) NOT NULL,
        ReferenceDocument         nvarchar(100) NULL,
        CreatedAt                 datetime2(3) NOT NULL CONSTRAINT DF_MES_ExecEvent_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_MES_ExecEvent_OrderMat FOREIGN KEY (ProductionOrderMaterialId) REFERENCES MES.ProductionOrderMaterial(ProductionOrderMaterialId),
        CONSTRAINT FK_MES_ExecEvent_Lot FOREIGN KEY (MaterialLotId) REFERENCES MES.MaterialLot(MaterialLotId),
        CONSTRAINT UQ_MES_ExecEvent_Idempotency UNIQUE (IdempotencyKey),
        CONSTRAINT CK_MES_ExecEvent_Type CHECK (EventType IN ('ISSUE','CONSUME','RETURN','SCRAP')),
        CONSTRAINT CK_MES_ExecEvent_Qty CHECK (Quantity > 0)
    );
    CREATE INDEX IX_MES_ExecEvent_Lot ON MES.MaterialExecutionEvent(MaterialLotId, EventAt);
    CREATE INDEX IX_MES_ExecEvent_OrderMat ON MES.MaterialExecutionEvent(ProductionOrderMaterialId, EventType);
END;

IF OBJECT_ID(N'MES.InventoryTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE MES.InventoryTransaction
    (
        InventoryTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_InventoryTransaction PRIMARY KEY,
        SiteCode               nvarchar(30) NOT NULL,
        PartNo                 nvarchar(60) NOT NULL,
        MaterialLotId          bigint NOT NULL,
        TransactionType        varchar(16) NOT NULL,
        Quantity               decimal(19,6) NOT NULL,
        UomCode                nvarchar(20) NOT NULL,
        FromLocationCode       nvarchar(60) NULL,
        ToLocationCode         nvarchar(60) NULL,
        ReferenceType          nvarchar(30) NOT NULL,
        ReferenceId            bigint NOT NULL,
        IdempotencyKey         nvarchar(150) NOT NULL,
        TransactionAt          datetime2(3) NOT NULL,
        ActorCode              nvarchar(100) NOT NULL,
        CreatedAt              datetime2(3) NOT NULL CONSTRAINT DF_MES_InvTxn_Created DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_MES_InvTxn_Lot FOREIGN KEY (MaterialLotId) REFERENCES MES.MaterialLot(MaterialLotId),
        CONSTRAINT UQ_MES_InvTxn_Idempotency UNIQUE (IdempotencyKey),
        CONSTRAINT CK_MES_InvTxn_Type CHECK (TransactionType IN ('RECEIPT','MOVE','ISSUE','RETURN','SCRAP','ADJUST')),
        CONSTRAINT CK_MES_InvTxn_Qty CHECK (Quantity > 0)
    );
    CREATE INDEX IX_MES_InvTxn_Stock ON MES.InventoryTransaction(SiteCode, PartNo, MaterialLotId, TransactionAt);
END;

IF OBJECT_ID(N'MES.FinishedLotGenealogy', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FinishedLotGenealogy
    (
        FinishedLotGenealogyId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_FinishedLotGenealogy PRIMARY KEY,
        ProductionOrderId      bigint NOT NULL,
        FinishedPartNo         nvarchar(60) NOT NULL,
        FinishedLotNo          nvarchar(100) NOT NULL,
        ComponentPartNo        nvarchar(60) NOT NULL,
        ComponentLotId         bigint NOT NULL,
        ConsumedQty            decimal(19,6) NOT NULL,
        UomCode                nvarchar(20) NOT NULL,
        FirstConsumedAt        datetime2(3) NOT NULL,
        LastConsumedAt         datetime2(3) NOT NULL,
        CONSTRAINT FK_MES_Genealogy_Order FOREIGN KEY (ProductionOrderId) REFERENCES MES.ProductionOrder(ProductionOrderId),
        CONSTRAINT FK_MES_Genealogy_ComponentLot FOREIGN KEY (ComponentLotId) REFERENCES MES.MaterialLot(MaterialLotId),
        CONSTRAINT CK_MES_Genealogy_Qty CHECK (ConsumedQty > 0)
    );
    CREATE INDEX IX_MES_Genealogy_Finished ON MES.FinishedLotGenealogy(FinishedPartNo, FinishedLotNo);
    CREATE INDEX IX_MES_Genealogy_Component ON MES.FinishedLotGenealogy(ComponentPartNo, ComponentLotId);
END;

COMMIT TRANSACTION;
