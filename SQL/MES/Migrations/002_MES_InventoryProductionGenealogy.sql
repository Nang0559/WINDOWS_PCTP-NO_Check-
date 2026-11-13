/*
 Migration 002: quantity ledger, balance projection, receipt, production and genealogy.
 All quantities are positive magnitudes; direction is defined by MovementType.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='001') THROW 51000,'Run migration 001 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='002') RETURN;
BEGIN TRANSACTION;

IF OBJECT_ID(N'MES.PurchaseOrder',N'U') IS NULL
CREATE TABLE MES.PurchaseOrder(
 PurchaseOrderId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_PurchaseOrder PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, PurchaseOrderNo nvarchar(80) NOT NULL,
 SupplierCode nvarchar(60) NULL, SupplierName nvarchar(250) NULL,
 OrderDate date NULL, Status varchar(20) NOT NULL CONSTRAINT DF_MES_PO_Status DEFAULT('OPEN'),
 SourceSystem varchar(20) NOT NULL CONSTRAINT DF_MES_PO_Source DEFAULT('UPLOAD'),
 SourceKey nvarchar(150) NULL, CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_PO_Created DEFAULT(SYSUTCDATETIME()),
 CreatedBy nvarchar(100) NULL,
 CONSTRAINT UQ_MES_PurchaseOrder UNIQUE(SiteCode,PurchaseOrderNo),
 CONSTRAINT CK_MES_PO_Status CHECK(Status IN('OPEN','PARTIAL','CLOSED','CANCELLED')));

IF OBJECT_ID(N'MES.PurchaseOrderLine',N'U') IS NULL
CREATE TABLE MES.PurchaseOrderLine(
 PurchaseOrderLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_PurchaseOrderLine PRIMARY KEY,
 PurchaseOrderId bigint NOT NULL, LineNo int NOT NULL,
 SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL,
 OrderedQty decimal(19,6) NOT NULL, UomCode nvarchar(30) NOT NULL,
 DueDate date NULL, CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_POL_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT FK_MES_POL_Header FOREIGN KEY(PurchaseOrderId) REFERENCES MES.PurchaseOrder(PurchaseOrderId),
 CONSTRAINT FK_MES_POL_Part FOREIGN KEY(SiteCode,PartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT UQ_MES_POL_Line UNIQUE(PurchaseOrderId,LineNo),
 CONSTRAINT CK_MES_POL_Qty CHECK(OrderedQty>0));

IF OBJECT_ID(N'MES.MaterialLot',N'U') IS NULL
CREATE TABLE MES.MaterialLot(
 MaterialLotId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialLot PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL, LotNo nvarchar(120) NOT NULL,
 LotType varchar(12) NOT NULL, SupplierLotNo nvarchar(120) NULL, SupplierCode nvarchar(60) NULL,
 ManufacturedDate date NULL, ExpiryDate date NULL,
 LotStatus varchar(20) NOT NULL CONSTRAINT DF_MES_Lot_Status DEFAULT('RECEIVED'),
 ParentLotId bigint NULL, SourceSystem varchar(20) NOT NULL CONSTRAINT DF_MES_Lot_Source DEFAULT('MES'),
 SourceReference nvarchar(200) NULL, CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Lot_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT UQ_MES_MaterialLot_Natural UNIQUE(SiteCode,PartNo,LotNo),
 CONSTRAINT UQ_MES_MaterialLot_Composite UNIQUE(SiteCode,PartNo,MaterialLotId),
 CONSTRAINT FK_MES_Lot_Part FOREIGN KEY(SiteCode,PartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT FK_MES_Lot_Parent FOREIGN KEY(ParentLotId) REFERENCES MES.MaterialLot(MaterialLotId),
 CONSTRAINT CK_MES_Lot_Type CHECK(LotType IN('RAW','WIP','FG','PACK')),
 CONSTRAINT CK_MES_Lot_Status CHECK(LotStatus IN('RECEIVED','AVAILABLE','QUARANTINE','BLOCKED','CONSUMED','CLOSED')),
 CONSTRAINT CK_MES_Lot_Dates CHECK(ExpiryDate IS NULL OR ManufacturedDate IS NULL OR ExpiryDate>=ManufacturedDate));

IF OBJECT_ID(N'MES.InventoryTransaction',N'U') IS NULL
CREATE TABLE MES.InventoryTransaction(
 InventoryTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_InventoryTransaction PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL, MaterialLotId bigint NOT NULL,
 MovementType varchar(20) NOT NULL, Quantity decimal(19,6) NOT NULL, UomCode nvarchar(30) NOT NULL,
 FromLocationNo nvarchar(100) NULL, ToLocationNo nvarchar(100) NULL,
 FromStockState varchar(16) NULL, ToStockState varchar(16) NULL,
 ReferenceType varchar(30) NOT NULL, ReferenceNo nvarchar(100) NULL, ReferenceLineNo int NULL,
 IdempotencyKey nvarchar(180) NOT NULL, TransactionAtUtc datetime2(3) NOT NULL,
 ActorCode nvarchar(100) NOT NULL, ReversalOfTransactionId bigint NULL,
 CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_InvTxn_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT UQ_MES_InvTxn_Idempotency UNIQUE(IdempotencyKey),
 CONSTRAINT FK_MES_InvTxn_Lot FOREIGN KEY(SiteCode,PartNo,MaterialLotId) REFERENCES MES.MaterialLot(SiteCode,PartNo,MaterialLotId),
 CONSTRAINT FK_MES_InvTxn_FromLoc FOREIGN KEY(SiteCode,FromLocationNo) REFERENCES MES.IfsInventoryLocation(SiteCode,LocationNo),
 CONSTRAINT FK_MES_InvTxn_ToLoc FOREIGN KEY(SiteCode,ToLocationNo) REFERENCES MES.IfsInventoryLocation(SiteCode,LocationNo),
 CONSTRAINT FK_MES_InvTxn_Reversal FOREIGN KEY(ReversalOfTransactionId) REFERENCES MES.InventoryTransaction(InventoryTransactionId),
 CONSTRAINT CK_MES_InvTxn_Type CHECK(MovementType IN('RECEIPT','MOVE','ISSUE','RETURN','SCRAP','ADJUST_IN','ADJUST_OUT','PROD_RECEIPT','QUARANTINE','RELEASE')),
 CONSTRAINT CK_MES_InvTxn_Qty CHECK(Quantity>0),
 CONSTRAINT CK_MES_InvTxn_Location CHECK(
  (MovementType IN('RECEIPT','RETURN','PROD_RECEIPT','ADJUST_IN') AND ToLocationNo IS NOT NULL) OR
  (MovementType IN('ISSUE','SCRAP','ADJUST_OUT') AND FromLocationNo IS NOT NULL) OR
  (MovementType IN('MOVE','QUARANTINE','RELEASE') AND FromLocationNo IS NOT NULL AND ToLocationNo IS NOT NULL)),
 CONSTRAINT CK_MES_InvTxn_States CHECK(
  (FromStockState IS NULL OR FromStockState IN('AVAILABLE','QUARANTINE','WIP','FG','BLOCKED')) AND
  (ToStockState IS NULL OR ToStockState IN('AVAILABLE','QUARANTINE','WIP','FG','BLOCKED'))));

IF OBJECT_ID(N'MES.InventoryBalance',N'U') IS NULL
CREATE TABLE MES.InventoryBalance(
 SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL, MaterialLotId bigint NOT NULL,
 LocationNo nvarchar(100) NOT NULL, StockState varchar(16) NOT NULL,
 OnHandQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Balance_OnHand DEFAULT(0),
 ReservedQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Balance_Reserved DEFAULT(0),
 UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Balance_Updated DEFAULT(SYSUTCDATETIME()),
 RowVer rowversion NOT NULL,
 CONSTRAINT PK_MES_InventoryBalance PRIMARY KEY CLUSTERED(SiteCode,PartNo,MaterialLotId,LocationNo,StockState),
 CONSTRAINT FK_MES_Balance_Lot FOREIGN KEY(SiteCode,PartNo,MaterialLotId) REFERENCES MES.MaterialLot(SiteCode,PartNo,MaterialLotId),
 CONSTRAINT FK_MES_Balance_Location FOREIGN KEY(SiteCode,LocationNo) REFERENCES MES.IfsInventoryLocation(SiteCode,LocationNo),
 CONSTRAINT CK_MES_Balance_State CHECK(StockState IN('AVAILABLE','QUARANTINE','WIP','FG','BLOCKED')),
 CONSTRAINT CK_MES_Balance_Qty CHECK(OnHandQty>=0 AND ReservedQty>=0 AND ReservedQty<=OnHandQty));

IF OBJECT_ID(N'MES.MaterialReceipt',N'U') IS NULL
CREATE TABLE MES.MaterialReceipt(
 MaterialReceiptId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialReceipt PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, ReceiptNo nvarchar(80) NOT NULL,
 PurchaseOrderId bigint NULL, ReceivedAtUtc datetime2(3) NOT NULL,
 ReceivedBy nvarchar(100) NOT NULL, Status varchar(16) NOT NULL CONSTRAINT DF_MES_Receipt_Status DEFAULT('DRAFT'),
 ExternalReference nvarchar(150) NULL, CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Receipt_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT UQ_MES_Receipt UNIQUE(SiteCode,ReceiptNo),
 CONSTRAINT FK_MES_Receipt_PO FOREIGN KEY(PurchaseOrderId) REFERENCES MES.PurchaseOrder(PurchaseOrderId),
 CONSTRAINT CK_MES_Receipt_Status CHECK(Status IN('DRAFT','POSTED','CANCELLED')));

IF OBJECT_ID(N'MES.MaterialReceiptLine',N'U') IS NULL
CREATE TABLE MES.MaterialReceiptLine(
 MaterialReceiptLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialReceiptLine PRIMARY KEY,
 MaterialReceiptId bigint NOT NULL, LineNo int NOT NULL, PurchaseOrderLineId bigint NULL,
 SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL, MaterialLotId bigint NOT NULL,
 ReceivedQty decimal(19,6) NOT NULL, AcceptedQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Receipt_Accepted DEFAULT(0),
 QuarantineQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Receipt_Quarantine DEFAULT(0),
 UomCode nvarchar(30) NOT NULL, TargetLocationNo nvarchar(100) NOT NULL,
 InventoryTransactionId bigint NULL,
 CONSTRAINT FK_MES_ReceiptLine_Header FOREIGN KEY(MaterialReceiptId) REFERENCES MES.MaterialReceipt(MaterialReceiptId),
 CONSTRAINT FK_MES_ReceiptLine_POL FOREIGN KEY(PurchaseOrderLineId) REFERENCES MES.PurchaseOrderLine(PurchaseOrderLineId),
 CONSTRAINT FK_MES_ReceiptLine_Lot FOREIGN KEY(SiteCode,PartNo,MaterialLotId) REFERENCES MES.MaterialLot(SiteCode,PartNo,MaterialLotId),
 CONSTRAINT FK_MES_ReceiptLine_Loc FOREIGN KEY(SiteCode,TargetLocationNo) REFERENCES MES.IfsInventoryLocation(SiteCode,LocationNo),
 CONSTRAINT FK_MES_ReceiptLine_Txn FOREIGN KEY(InventoryTransactionId) REFERENCES MES.InventoryTransaction(InventoryTransactionId),
 CONSTRAINT UQ_MES_ReceiptLine UNIQUE(MaterialReceiptId,LineNo),
 CONSTRAINT CK_MES_ReceiptLine_Qty CHECK(ReceivedQty>0 AND AcceptedQty>=0 AND QuarantineQty>=0 AND AcceptedQty+QuarantineQty<=ReceivedQty));

IF OBJECT_ID(N'MES.ProductionPlan',N'U') IS NULL
CREATE TABLE MES.ProductionPlan(
 ProductionPlanId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionPlan PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, PlanNo nvarchar(80) NOT NULL, PlanDate date NOT NULL,
 SourceFileName nvarchar(260) NULL, ImportBatchKey uniqueidentifier NULL,
 Status varchar(20) NOT NULL CONSTRAINT DF_MES_Plan_Status DEFAULT('IMPORTED'),
 CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Plan_Created DEFAULT(SYSUTCDATETIME()),
 CreatedBy nvarchar(100) NULL, CONSTRAINT UQ_MES_Plan UNIQUE(SiteCode,PlanNo),
 CONSTRAINT CK_MES_Plan_Status CHECK(Status IN('IMPORTED','VALIDATED','RELEASED','CLOSED','CANCELLED')));

IF OBJECT_ID(N'MES.ProductionPlanLine',N'U') IS NULL
CREATE TABLE MES.ProductionPlanLine(
 ProductionPlanLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionPlanLine PRIMARY KEY,
 ProductionPlanId bigint NOT NULL, LineNo int NOT NULL,
 SiteCode nvarchar(30) NOT NULL, ProductPartNo nvarchar(100) NOT NULL,
 PlannedQty decimal(19,6) NOT NULL, UomCode nvarchar(30) NOT NULL,
 PlannedStartUtc datetime2(0) NULL, PlannedEndUtc datetime2(0) NULL,
 CONSTRAINT FK_MES_PlanLine_Header FOREIGN KEY(ProductionPlanId) REFERENCES MES.ProductionPlan(ProductionPlanId),
 CONSTRAINT FK_MES_PlanLine_Part FOREIGN KEY(SiteCode,ProductPartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT UQ_MES_PlanLine UNIQUE(ProductionPlanId,LineNo),
 CONSTRAINT CK_MES_PlanLine_Qty CHECK(PlannedQty>0),
 CONSTRAINT CK_MES_PlanLine_Dates CHECK(PlannedEndUtc IS NULL OR PlannedStartUtc IS NULL OR PlannedEndUtc>=PlannedStartUtc));

IF OBJECT_ID(N'MES.ProductionOrder',N'U') IS NULL
CREATE TABLE MES.ProductionOrder(
 ProductionOrderId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionOrder PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, OrderNo nvarchar(80) NOT NULL,
 ProductPartNo nvarchar(100) NOT NULL, BomRevisionId bigint NOT NULL,
 ProductionPlanLineId bigint NULL, PlannedQty decimal(19,6) NOT NULL,
 CompletedQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Order_Completed DEFAULT(0),
 UomCode nvarchar(30) NOT NULL, Status varchar(20) NOT NULL CONSTRAINT DF_MES_Order_Status DEFAULT('CREATED'),
 ReleasedAtUtc datetime2(3) NULL, ReleasedBy nvarchar(100) NULL,
 CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Order_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT UQ_MES_ProductionOrder UNIQUE(SiteCode,OrderNo),
 CONSTRAINT FK_MES_Order_Product FOREIGN KEY(SiteCode,ProductPartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT FK_MES_Order_BOM FOREIGN KEY(BomRevisionId) REFERENCES MES.BomRevision(BomRevisionId),
 CONSTRAINT FK_MES_Order_PlanLine FOREIGN KEY(ProductionPlanLineId) REFERENCES MES.ProductionPlanLine(ProductionPlanLineId),
 CONSTRAINT CK_MES_Order_Qty CHECK(PlannedQty>0 AND CompletedQty>=0 AND CompletedQty<=PlannedQty),
 CONSTRAINT CK_MES_Order_Status CHECK(Status IN('CREATED','RELEASED','RUNNING','COMPLETED','CLOSED','CANCELLED')));

IF OBJECT_ID(N'MES.ProductionOrderMaterial',N'U') IS NULL
CREATE TABLE MES.ProductionOrderMaterial(
 ProductionOrderMaterialId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_ProductionOrderMaterial PRIMARY KEY,
 ProductionOrderId bigint NOT NULL, BomComponentId bigint NULL,
 SiteCode nvarchar(30) NOT NULL, ComponentPartNo nvarchar(100) NOT NULL,
 RequiredQty decimal(19,6) NOT NULL, UomCode nvarchar(30) NOT NULL,
 OperationNo nvarchar(40) NULL, IsBackflush bit NOT NULL CONSTRAINT DF_MES_OrderMat_Backflush DEFAULT(0),
 CONSTRAINT FK_MES_OrderMat_Order FOREIGN KEY(ProductionOrderId) REFERENCES MES.ProductionOrder(ProductionOrderId),
 CONSTRAINT FK_MES_OrderMat_BOMComp FOREIGN KEY(BomComponentId) REFERENCES MES.BomComponent(BomComponentId),
 CONSTRAINT FK_MES_OrderMat_Part FOREIGN KEY(SiteCode,ComponentPartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT CK_MES_OrderMat_Qty CHECK(RequiredQty>=0));

IF OBJECT_ID(N'MES.MaterialExecutionEvent',N'U') IS NULL
CREATE TABLE MES.MaterialExecutionEvent(
 MaterialExecutionEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialExecutionEvent PRIMARY KEY,
 ProductionOrderMaterialId bigint NOT NULL, InventoryTransactionId bigint NOT NULL,
 EventType varchar(12) NOT NULL, EventAtUtc datetime2(3) NOT NULL,
 OperatorCode nvarchar(100) NOT NULL, QrRawValue nvarchar(1000) NULL,
 IdempotencyKey nvarchar(180) NOT NULL,
 CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_ExecEvent_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT FK_MES_ExecEvent_OrderMat FOREIGN KEY(ProductionOrderMaterialId) REFERENCES MES.ProductionOrderMaterial(ProductionOrderMaterialId),
 CONSTRAINT FK_MES_ExecEvent_InvTxn FOREIGN KEY(InventoryTransactionId) REFERENCES MES.InventoryTransaction(InventoryTransactionId),
 CONSTRAINT UQ_MES_ExecEvent_Txn UNIQUE(InventoryTransactionId),
 CONSTRAINT UQ_MES_ExecEvent_Idempotency UNIQUE(IdempotencyKey),
 CONSTRAINT CK_MES_ExecEvent_Type CHECK(EventType IN('ISSUE','CONSUME','RETURN','SCRAP')));

IF OBJECT_ID(N'MES.FinishedLotGenealogy',N'U') IS NULL
CREATE TABLE MES.FinishedLotGenealogy(
 FinishedLotGenealogyId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_FinishedLotGenealogy PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, ProductionOrderId bigint NOT NULL,
 FinishedLotId bigint NOT NULL, ComponentLotId bigint NOT NULL,
 MaterialExecutionEventId bigint NOT NULL, ConsumedQty decimal(19,6) NOT NULL,
 UomCode nvarchar(30) NOT NULL, CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Genealogy_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT FK_MES_Genealogy_Order FOREIGN KEY(ProductionOrderId) REFERENCES MES.ProductionOrder(ProductionOrderId),
 CONSTRAINT FK_MES_Genealogy_FinishedLot FOREIGN KEY(FinishedLotId) REFERENCES MES.MaterialLot(MaterialLotId),
 CONSTRAINT FK_MES_Genealogy_ComponentLot FOREIGN KEY(ComponentLotId) REFERENCES MES.MaterialLot(MaterialLotId),
 CONSTRAINT FK_MES_Genealogy_Event FOREIGN KEY(MaterialExecutionEventId) REFERENCES MES.MaterialExecutionEvent(MaterialExecutionEventId),
 CONSTRAINT UQ_MES_Genealogy_Event UNIQUE(MaterialExecutionEventId,FinishedLotId,ComponentLotId),
 CONSTRAINT CK_MES_Genealogy_Qty CHECK(ConsumedQty>0));

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('002',N'Inventory, production and actual genealogy');
COMMIT TRANSACTION;
GO
