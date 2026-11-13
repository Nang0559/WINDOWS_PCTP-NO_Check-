/*
 Migration 007: cross-entity integrity, PO receipt consistency and material reservations.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='006') THROW 51000,'Run migration 006 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='007') RETURN;
BEGIN TRANSACTION;

/* BOM site/product consistency */
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'MES.BomRevision') AND name=N'UQ_MES_BomRevision_IdSite')
 ALTER TABLE MES.BomRevision ADD CONSTRAINT UQ_MES_BomRevision_IdSite UNIQUE(BomRevisionId,SiteCode);
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'MES.BomRevision') AND name=N'UQ_MES_BomRevision_IdSiteParent')
 ALTER TABLE MES.BomRevision ADD CONSTRAINT UQ_MES_BomRevision_IdSiteParent UNIQUE(BomRevisionId,SiteCode,ParentPartNo);
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'MES.BomComponent') AND name=N'UQ_MES_BomComponent_IdRevision')
 ALTER TABLE MES.BomComponent ADD CONSTRAINT UQ_MES_BomComponent_IdRevision UNIQUE(BomComponentId,BomRevisionId);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'MES.BomComponent') AND name=N'FK_MES_BomComponent_RevisionSite')
 ALTER TABLE MES.BomComponent ADD CONSTRAINT FK_MES_BomComponent_RevisionSite
 FOREIGN KEY(BomRevisionId,SiteCode) REFERENCES MES.BomRevision(BomRevisionId,SiteCode);
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'MES.ProductionOrder') AND name=N'UQ_MES_ProductionOrder_IdBom')
 ALTER TABLE MES.ProductionOrder ADD CONSTRAINT UQ_MES_ProductionOrder_IdBom UNIQUE(ProductionOrderId,BomRevisionId);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'MES.ProductionOrder') AND name=N'FK_MES_ProductionOrder_BomProduct')
 ALTER TABLE MES.ProductionOrder ADD CONSTRAINT FK_MES_ProductionOrder_BomProduct
 FOREIGN KEY(BomRevisionId,SiteCode,ProductPartNo) REFERENCES MES.BomRevision(BomRevisionId,SiteCode,ParentPartNo);

IF COL_LENGTH(N'MES.ProductionOrderMaterial',N'BomRevisionId') IS NULL
 ALTER TABLE MES.ProductionOrderMaterial ADD BomRevisionId bigint NULL;
UPDATE om SET BomRevisionId=o.BomRevisionId
 FROM MES.ProductionOrderMaterial om JOIN MES.ProductionOrder o ON o.ProductionOrderId=om.ProductionOrderId
 WHERE om.BomRevisionId IS NULL;
IF EXISTS(SELECT 1 FROM MES.ProductionOrderMaterial WHERE BomRevisionId IS NULL)
 THROW 51700,'ProductionOrderMaterial has no parent BOM revision; resolve rows before migration 007.',1;
ALTER TABLE MES.ProductionOrderMaterial ALTER COLUMN BomRevisionId bigint NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'MES.ProductionOrderMaterial') AND name=N'FK_MES_OrderMaterial_OrderBom')
 ALTER TABLE MES.ProductionOrderMaterial ADD CONSTRAINT FK_MES_OrderMaterial_OrderBom
 FOREIGN KEY(ProductionOrderId,BomRevisionId) REFERENCES MES.ProductionOrder(ProductionOrderId,BomRevisionId);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'MES.ProductionOrderMaterial') AND name=N'FK_MES_OrderMaterial_BomComponentRevision')
 ALTER TABLE MES.ProductionOrderMaterial ADD CONSTRAINT FK_MES_OrderMaterial_BomComponentRevision
 FOREIGN KEY(BomComponentId,BomRevisionId) REFERENCES MES.BomComponent(BomComponentId,BomRevisionId);

/* Ensure a receipt line's PO line belongs to the same PO as its receipt header. */
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'MES.PurchaseOrderLine') AND name=N'UQ_MES_POL_IdPO')
 ALTER TABLE MES.PurchaseOrderLine ADD CONSTRAINT UQ_MES_POL_IdPO UNIQUE(PurchaseOrderLineId,PurchaseOrderId);
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'MES.MaterialReceipt') AND name=N'UQ_MES_Receipt_IdPO')
 ALTER TABLE MES.MaterialReceipt ADD CONSTRAINT UQ_MES_Receipt_IdPO UNIQUE(MaterialReceiptId,PurchaseOrderId);
IF COL_LENGTH(N'MES.MaterialReceiptLine',N'PurchaseOrderId') IS NULL
 ALTER TABLE MES.MaterialReceiptLine ADD PurchaseOrderId bigint NULL;
UPDATE rl SET PurchaseOrderId=r.PurchaseOrderId
 FROM MES.MaterialReceiptLine rl JOIN MES.MaterialReceipt r ON r.MaterialReceiptId=rl.MaterialReceiptId
 WHERE rl.PurchaseOrderId IS NULL;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'MES.MaterialReceiptLine') AND name=N'FK_MES_ReceiptLine_HeaderPO')
 ALTER TABLE MES.MaterialReceiptLine ADD CONSTRAINT FK_MES_ReceiptLine_HeaderPO
 FOREIGN KEY(MaterialReceiptId,PurchaseOrderId) REFERENCES MES.MaterialReceipt(MaterialReceiptId,PurchaseOrderId);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'MES.MaterialReceiptLine') AND name=N'FK_MES_ReceiptLine_POLPO')
 ALTER TABLE MES.MaterialReceiptLine ADD CONSTRAINT FK_MES_ReceiptLine_POLPO
 FOREIGN KEY(PurchaseOrderLineId,PurchaseOrderId) REFERENCES MES.PurchaseOrderLine(PurchaseOrderLineId,PurchaseOrderId);

/* Reservations are holds against the balance; consumption writes the stock ledger exactly once. */
IF OBJECT_ID(N'MES.MaterialReservation',N'U') IS NULL
CREATE TABLE MES.MaterialReservation(
 MaterialReservationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_MaterialReservation PRIMARY KEY,
 ProductionOrderMaterialId bigint NOT NULL, SiteCode nvarchar(30) NOT NULL,
 PartNo nvarchar(100) NOT NULL, MaterialLotId bigint NOT NULL, LocationNo nvarchar(100) NOT NULL,
 ReservedQty decimal(19,6) NOT NULL, ConsumedQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Reservation_Consumed DEFAULT(0),
 ReleasedQty decimal(19,6) NOT NULL CONSTRAINT DF_MES_Reservation_Released DEFAULT(0),
 Status varchar(16) NOT NULL CONSTRAINT DF_MES_Reservation_Status DEFAULT('ACTIVE'),
 IdempotencyKey nvarchar(180) NOT NULL, CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_Reservation_Created DEFAULT(SYSUTCDATETIME()),
 CreatedBy nvarchar(100) NOT NULL,
 CONSTRAINT UQ_MES_Reservation_Idempotency UNIQUE(IdempotencyKey),
 CONSTRAINT FK_MES_Reservation_OrderMaterial FOREIGN KEY(ProductionOrderMaterialId) REFERENCES MES.ProductionOrderMaterial(ProductionOrderMaterialId),
 CONSTRAINT FK_MES_Reservation_Lot FOREIGN KEY(SiteCode,PartNo,MaterialLotId) REFERENCES MES.MaterialLot(SiteCode,PartNo,MaterialLotId),
 CONSTRAINT FK_MES_Reservation_Balance FOREIGN KEY(SiteCode,PartNo,MaterialLotId,LocationNo,StatusBalanceKey) REFERENCES MES.InventoryBalance(SiteCode,PartNo,MaterialLotId,LocationNo,StockState),
 CONSTRAINT CK_MES_Reservation_Qty CHECK(ReservedQty>0 AND ConsumedQty>=0 AND ReleasedQty>=0 AND ConsumedQty+ReleasedQty<=ReservedQty),
 CONSTRAINT CK_MES_Reservation_Status CHECK(Status IN('ACTIVE','PARTIAL','CONSUMED','RELEASED','CANCELLED')),
 StatusBalanceKey AS (CONVERT(varchar(16),'AVAILABLE')) PERSISTED
);

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('007',N'Cross-entity integrity and material reservations');
COMMIT TRANSACTION;
GO
