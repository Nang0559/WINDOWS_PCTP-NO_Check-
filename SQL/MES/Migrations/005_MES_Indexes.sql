/*
 Migration 005: workload indexes. PK/unique indexes are already declared with their tables.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='004') THROW 51000,'Run migration 004 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='005') RETURN;
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.MaterialLot') AND name=N'IX_MES_MaterialLot_FIFO')
 CREATE NONCLUSTERED INDEX IX_MES_MaterialLot_FIFO
 ON MES.MaterialLot(SiteCode,PartNo,LotStatus,ExpiryDate,ManufacturedDate,MaterialLotId)
 INCLUDE(LotNo,LotType,SupplierLotNo);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.InventoryBalance') AND name=N'IX_MES_Balance_PartLocationState')
 CREATE NONCLUSTERED INDEX IX_MES_Balance_PartLocationState
 ON MES.InventoryBalance(SiteCode,PartNo,LocationNo,StockState)
 INCLUDE(MaterialLotId,OnHandQty,ReservedQty,UpdatedAtUtc);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.InventoryTransaction') AND name=N'IX_MES_InvTxn_LotTimeline')
 CREATE NONCLUSTERED INDEX IX_MES_InvTxn_LotTimeline
 ON MES.InventoryTransaction(SiteCode,PartNo,MaterialLotId,TransactionAtUtc,InventoryTransactionId)
 INCLUDE(MovementType,Quantity,UomCode,FromLocationNo,ToLocationNo,ReferenceType,ReferenceNo,ReferenceLineNo);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.InventoryTransaction') AND name=N'IX_MES_InvTxn_Reference')
 CREATE NONCLUSTERED INDEX IX_MES_InvTxn_Reference
 ON MES.InventoryTransaction(ReferenceType,ReferenceNo,ReferenceLineNo)
 INCLUDE(SiteCode,PartNo,MaterialLotId,MovementType,Quantity,TransactionAtUtc);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.ProductionOrder') AND name=N'IX_MES_ProductionOrder_Status')
 CREATE NONCLUSTERED INDEX IX_MES_ProductionOrder_Status
 ON MES.ProductionOrder(SiteCode,Status,ProductPartNo)
 INCLUDE(OrderNo,PlannedQty,CompletedQty,BomRevisionId);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.ProductionOrderMaterial') AND name=N'IX_MES_OrderMaterial_Order')
 CREATE NONCLUSTERED INDEX IX_MES_OrderMaterial_Order
 ON MES.ProductionOrderMaterial(ProductionOrderId,ComponentPartNo)
 INCLUDE(RequiredQty,UomCode,OperationNo,BomComponentId);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.MaterialExecutionEvent') AND name=N'IX_MES_ExecEvent_OrderTimeline')
 CREATE NONCLUSTERED INDEX IX_MES_ExecEvent_OrderTimeline
 ON MES.MaterialExecutionEvent(ProductionOrderMaterialId,EventAtUtc,MaterialExecutionEventId)
 INCLUDE(EventType,InventoryTransactionId,OperatorCode);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FinishedLotGenealogy') AND name=N'IX_MES_Genealogy_FinishedLot')
 CREATE NONCLUSTERED INDEX IX_MES_Genealogy_FinishedLot
 ON MES.FinishedLotGenealogy(FinishedLotId,MaterialExecutionEventId)
 INCLUDE(ComponentLotId,ProductionOrderId,ConsumedQty,UomCode);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.FinishedLotGenealogy') AND name=N'IX_MES_Genealogy_ComponentLot')
 CREATE NONCLUSTERED INDEX IX_MES_Genealogy_ComponentLot
 ON MES.FinishedLotGenealogy(ComponentLotId,MaterialExecutionEventId)
 INCLUDE(FinishedLotId,ProductionOrderId,ConsumedQty,UomCode);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.MaterialReceiptLine') AND name=N'IX_MES_ReceiptLine_Lot')
 CREATE NONCLUSTERED INDEX IX_MES_ReceiptLine_Lot
 ON MES.MaterialReceiptLine(SiteCode,PartNo,MaterialLotId)
 INCLUDE(MaterialReceiptId,LineNo,ReceivedQty,AcceptedQty,QuarantineQty);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.BomComponent') AND name=N'IX_MES_BomComponent_Component')
 CREATE NONCLUSTERED INDEX IX_MES_BomComponent_Component
 ON MES.BomComponent(SiteCode,ComponentPartNo,BomRevisionId)
 INCLUDE(LineNo,QtyPer,UomCode,OperationNo);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.IfsInventoryPart') AND name=N'IX_MES_IfsPart_Active')
 CREATE NONCLUSTERED INDEX IX_MES_IfsPart_Active
 ON MES.IfsInventoryPart(SiteCode,IsActive,PartNo)
 INCLUDE(Description,InventoryUom,PartType,PartStatus);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'MES.IfsInventoryLocation') AND name=N'IX_MES_IfsLocation_Warehouse')
 CREATE NONCLUSTERED INDEX IX_MES_IfsLocation_Warehouse
 ON MES.IfsInventoryLocation(SiteCode,WarehouseCode,IsActive,LocationNo)
 INCLUDE(LocationGroupCode,LocationType,Description);

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('005',N'MES workload indexes');
COMMIT TRANSACTION;
GO
