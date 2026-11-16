/*
 Migration 006: inline table-valued read functions.
 Functions are read-only query surfaces; all stock writes go through procedures.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='005') THROW 51000,'Run migration 005 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='006') RETURN;

IF OBJECT_ID(N'MES.fn_AvailableStockByPart',N'IF') IS NULL EXEC(N'CREATE FUNCTION MES.fn_AvailableStockByPart
(
 @SiteCode nvarchar(30),
 @PartNo nvarchar(100),
 @LocationNo nvarchar(100)
)
RETURNS TABLE
AS
RETURN
(
 SELECT b.SiteCode,b.PartNo,b.MaterialLotId,l.LotNo,b.LocationNo,
        b.StockState,b.OnHandQty,b.ReservedQty,
        (b.OnHandQty-b.ReservedQty) AS AvailableQty,
        l.ManufacturedDate,l.ExpiryDate,p.InventoryUom
 FROM MES.InventoryBalance AS b
 INNER JOIN MES.MaterialLot AS l
  ON l.SiteCode=b.SiteCode AND l.PartNo=b.PartNo AND l.MaterialLotId=b.MaterialLotId
 INNER JOIN MES.IfsInventoryPart AS p
  ON p.SiteCode=b.SiteCode AND p.PartNo=b.PartNo
 WHERE b.SiteCode=@SiteCode AND b.PartNo=@PartNo
  AND (@LocationNo IS NULL OR b.LocationNo=@LocationNo)
  AND b.StockState=''AVAILABLE'' AND b.OnHandQty>b.ReservedQty
  AND l.LotStatus IN(''RECEIVED'',''AVAILABLE'')
)');

IF OBJECT_ID(N'MES.fn_LotMovementTimeline',N'IF') IS NULL EXEC(N'CREATE FUNCTION MES.fn_LotMovementTimeline
(
 @SiteCode nvarchar(30),
 @PartNo nvarchar(100),
 @MaterialLotId bigint
)
RETURNS TABLE
AS
RETURN
(
 SELECT t.InventoryTransactionId,t.MovementType,t.Quantity,t.UomCode,
        t.FromLocationNo,t.ToLocationNo,t.FromStockState,t.ToStockState,
        t.ReferenceType,t.ReferenceNo,t.ReferenceLineNo,
        t.TransactionAtUtc,t.ActorCode,t.IdempotencyKey
 FROM MES.InventoryTransaction AS t
 WHERE t.SiteCode=@SiteCode AND t.PartNo=@PartNo AND t.MaterialLotId=@MaterialLotId
)');

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('006',N'MES inline table-valued read functions');
GO
