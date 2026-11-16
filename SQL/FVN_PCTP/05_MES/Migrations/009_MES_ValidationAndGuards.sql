/*
 Migration 009: receipt/PO consistency guard and stock-ledger reconciliation.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='008') THROW 51000,'Run migration 008 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='009') RETURN;

IF OBJECT_ID(N'MES.trg_MaterialReceiptLine_ValidatePO',N'TR') IS NULL EXEC(N'CREATE TRIGGER MES.trg_MaterialReceiptLine_ValidatePO
ON MES.MaterialReceiptLine
AFTER INSERT,UPDATE
AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(
  SELECT 1 FROM inserted i
  JOIN MES.MaterialReceipt r ON r.MaterialReceiptId=i.MaterialReceiptId
  WHERE ISNULL(i.PurchaseOrderId,-1)<>ISNULL(r.PurchaseOrderId,-1) OR i.SiteCode<>r.SiteCode)
  THROW 51900,''Receipt line PurchaseOrderId must match its receipt header.'',1;
 IF EXISTS(
  SELECT 1 FROM inserted i
  JOIN MES.PurchaseOrderLine pol ON pol.PurchaseOrderLineId=i.PurchaseOrderLineId
  WHERE i.PurchaseOrderId IS NULL
     OR pol.PurchaseOrderId<>i.PurchaseOrderId
     OR pol.SiteCode<>i.SiteCode
     OR pol.PartNo<>i.PartNo)
  THROW 51901,''Receipt line PO line, SiteCode and PartNo must match.'',1;
END');


IF OBJECT_ID(N'MES.trg_MES_PurchaseOrderLine_ValidateSite',N'TR') IS NULL EXEC(N'CREATE TRIGGER MES.trg_MES_PurchaseOrderLine_ValidateSite
ON MES.PurchaseOrderLine
AFTER INSERT,UPDATE
AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN MES.PurchaseOrder p ON p.PurchaseOrderId=i.PurchaseOrderId WHERE p.SiteCode<>i.SiteCode)
  THROW 51902,''Purchase order line SiteCode must match purchase order header.'',1;
END');

IF OBJECT_ID(N'MES.usp_ValidateInventoryBalance',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_ValidateInventoryBalance
 @SiteCode nvarchar(30)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 ;WITH MovementDelta AS
 (
  SELECT SiteCode,PartNo,MaterialLotId,FromLocationNo AS LocationNo,
         FromStockState AS StockState,CONVERT(decimal(38,6),-Quantity) AS DeltaQty
  FROM MES.InventoryTransaction WHERE FromLocationNo IS NOT NULL
  UNION ALL
  SELECT SiteCode,PartNo,MaterialLotId,ToLocationNo,ToStockState,
         CONVERT(decimal(38,6),Quantity)
  FROM MES.InventoryTransaction WHERE ToLocationNo IS NOT NULL
 ),
 Ledger AS
 (
  SELECT SiteCode,PartNo,MaterialLotId,LocationNo,StockState,SUM(DeltaQty) AS LedgerQty
  FROM MovementDelta GROUP BY SiteCode,PartNo,MaterialLotId,LocationNo,StockState
 )
 SELECT COALESCE(b.SiteCode,l.SiteCode) AS SiteCode,
        COALESCE(b.PartNo,l.PartNo) AS PartNo,
        COALESCE(b.MaterialLotId,l.MaterialLotId) AS MaterialLotId,
        COALESCE(b.LocationNo,l.LocationNo) AS LocationNo,
        COALESCE(b.StockState,l.StockState) AS StockState,
        ISNULL(b.OnHandQty,0) AS BalanceQty,
        ISNULL(l.LedgerQty,0) AS LedgerQty,
        ISNULL(b.OnHandQty,0)-ISNULL(l.LedgerQty,0) AS DifferenceQty
 FROM MES.InventoryBalance b
 FULL OUTER JOIN Ledger l
  ON l.SiteCode=b.SiteCode AND l.PartNo=b.PartNo AND l.MaterialLotId=b.MaterialLotId
 AND l.LocationNo=b.LocationNo AND l.StockState=b.StockState
 WHERE (@SiteCode IS NULL OR COALESCE(b.SiteCode,l.SiteCode)=@SiteCode)
   AND ABS(ISNULL(b.OnHandQty,0)-ISNULL(l.LedgerQty,0))>0.000001
 ORDER BY COALESCE(b.SiteCode,l.SiteCode),COALESCE(b.PartNo,l.PartNo),
          COALESCE(b.MaterialLotId,l.MaterialLotId),COALESCE(b.LocationNo,l.LocationNo);
END');

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('009',N'Receipt guards and inventory ledger reconciliation');
GO
