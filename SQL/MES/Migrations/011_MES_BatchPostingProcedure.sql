/*
 Migration 011: atomic TVP batch posting.
 The batch is all-or-nothing and capped at 500 rows to bound lock duration.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='010') THROW 51000,'Run migration 010 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='011') RETURN;
BEGIN TRANSACTION;
IF OBJECT_ID(N'MES.usp_PostInventoryMovementBatch',N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_PostInventoryMovementBatch
 @Rows MES.TVP_InventoryMovement READONLY
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM @Rows) THROW 52100,''Movement batch is empty.'',1;
 IF (SELECT COUNT_BIG(*) FROM @Rows)>500 THROW 52101,''A single atomic batch is limited to 500 rows.'',1;
 DECLARE @Posted TABLE(ClientRowNo int NOT NULL PRIMARY KEY,InventoryTransactionId bigint NOT NULL);
 DECLARE @ClientRowNo int,@SiteCode nvarchar(30),@PartNo nvarchar(100),@LotNo nvarchar(120),
  @MaterialLotId bigint,@MovementType varchar(20),@Quantity decimal(19,6),@UomCode nvarchar(30),
  @FromLocationNo nvarchar(100),@ToLocationNo nvarchar(100),@FromStockState varchar(16),
  @ToStockState varchar(16),@ReferenceType varchar(30),@ReferenceNo nvarchar(100),
  @ReferenceLineNo int,@IdempotencyKey nvarchar(180),@TransactionAtUtc datetime2(3),
  @ActorCode nvarchar(100),@InventoryTransactionId bigint;
 DECLARE c CURSOR LOCAL FAST_FORWARD FOR
  SELECT ClientRowNo,SiteCode,PartNo,LotNo,MovementType,Quantity,UomCode,
   FromLocationNo,ToLocationNo,FromStockState,ToStockState,ReferenceType,ReferenceNo,
   ReferenceLineNo,IdempotencyKey,TransactionAtUtc,ActorCode
  FROM @Rows ORDER BY ClientRowNo;
 BEGIN TRY
  BEGIN TRANSACTION;
  OPEN c;
  FETCH NEXT FROM c INTO @ClientRowNo,@SiteCode,@PartNo,@LotNo,@MovementType,@Quantity,@UomCode,
   @FromLocationNo,@ToLocationNo,@FromStockState,@ToStockState,@ReferenceType,@ReferenceNo,
   @ReferenceLineNo,@IdempotencyKey,@TransactionAtUtc,@ActorCode;
  WHILE @@FETCH_STATUS=0
  BEGIN
   SET @MaterialLotId=NULL;
   SELECT @MaterialLotId=MaterialLotId FROM MES.MaterialLot
    WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND LotNo=@LotNo;
   IF @MaterialLotId IS NULL THROW 52102,''Batch contains a LotNo not found in MES.MaterialLot.'',1;
   SET @InventoryTransactionId=NULL;
   EXEC MES.usp_PostInventoryMovement
    @SiteCode=@SiteCode,@PartNo=@PartNo,@MaterialLotId=@MaterialLotId,
    @MovementType=@MovementType,@Quantity=@Quantity,@UomCode=@UomCode,
    @ReferenceType=@ReferenceType,@IdempotencyKey=@IdempotencyKey,
    @TransactionAtUtc=@TransactionAtUtc,@ActorCode=@ActorCode,
    @InventoryTransactionId=@InventoryTransactionId OUTPUT,
    @FromLocationNo=@FromLocationNo,@ToLocationNo=@ToLocationNo,
    @FromStockState=@FromStockState,@ToStockState=@ToStockState,
    @ReferenceNo=@ReferenceNo,@ReferenceLineNo=@ReferenceLineNo;
   INSERT @Posted(ClientRowNo,InventoryTransactionId) VALUES(@ClientRowNo,@InventoryTransactionId);
   FETCH NEXT FROM c INTO @ClientRowNo,@SiteCode,@PartNo,@LotNo,@MovementType,@Quantity,@UomCode,
    @FromLocationNo,@ToLocationNo,@FromStockState,@ToStockState,@ReferenceType,@ReferenceNo,
    @ReferenceLineNo,@IdempotencyKey,@TransactionAtUtc,@ActorCode;
  END;
  CLOSE c; DEALLOCATE c;
  COMMIT;
  SELECT ClientRowNo,InventoryTransactionId FROM @Posted ORDER BY ClientRowNo;
 END TRY
 BEGIN CATCH
  IF CURSOR_STATUS(''local'',''c'')>=0 CLOSE c;
  IF CURSOR_STATUS(''local'',''c'')>-3 DEALLOCATE c;
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END');
INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('011',N'Atomic TVP inventory movement batch posting');
COMMIT TRANSACTION;
GO
