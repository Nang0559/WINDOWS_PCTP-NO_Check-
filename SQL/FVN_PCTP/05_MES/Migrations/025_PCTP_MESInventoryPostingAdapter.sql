/*
 Migration 025: PCTP-facing adapter to the canonical MES inventory posting procedure.
 Prerequisite: 024 is optional; schema prerequisite is 023.
 New PCTP code calls this adapter instead of updating STOCKTP/SlotLot quantities.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52500, 'Migration 025 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52501, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '023')
    THROW 52502, 'Migration 023 must be applied before migration 025.', 1;
IF OBJECT_ID(N'MES.usp_PostInventoryMovement', N'P') IS NULL
    THROW 52503, 'MES.usp_PostInventoryMovement from MES migration 004 is required.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '025')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC(N'
    CREATE PROCEDURE dbo.usp_FVN_PCTP_PostStockMovement
        @SiteCode nvarchar(30),
        @PartNo nvarchar(100),
        @MaterialLotId bigint,
        @MovementType varchar(20),
        @Quantity decimal(19,6),
        @ReferenceType varchar(30),
        @IdempotencyKey nvarchar(180),
        @ActorCode nvarchar(100),
        @InventoryTransactionId bigint OUTPUT,
        @FromSlotId int=NULL,
        @ToSlotId int=NULL,
        @FromStockState varchar(16)=NULL,
        @ToStockState varchar(16)=NULL,
        @ReferenceNo nvarchar(100)=NULL,
        @ReferenceLineNo int=NULL,
        @TransactionAtUtc datetime2(3)=NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @FromLocationNo nvarchar(100), @ToLocationNo nvarchar(100);
        DECLARE @UomCode nvarchar(40);

        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode=@SiteCode AND IsActive=1)
            THROW 52510, ''PCTP site is not registered or inactive.'', 1;

        SELECT @UomCode=UnitMeas
        FROM IFS.FVN_InventoryPart
        WHERE SiteCode=@SiteCode AND PartNo=@PartNo;

        IF @UomCode IS NULL
            THROW 52511, ''PartNo does not exist in IFS for the supplied SiteCode.'', 1;
        IF LEN(@UomCode)>30
            THROW 52512, ''IFS UnitMeas exceeds the MES posting UOM width.'', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM MES.FVN_MaterialLot
            WHERE SiteCode=@SiteCode AND PartNo=@PartNo AND MaterialLotId=@MaterialLotId
        )
            THROW 52513, ''MaterialLotId does not belong to the supplied (SiteCode, PartNo).' ', 1;

        IF @FromSlotId IS NOT NULL
        BEGIN
            SELECT @FromLocationNo=LocationNo
            FROM dbo.FVN_SlotMesLocation
            WHERE SiteCode=@SiteCode AND SlotId=@FromSlotId AND IsPrimary=1 AND IsActive=1;
            IF @FromLocationNo IS NULL
                THROW 52514, ''Source Slot has no active primary MES location mapping.'', 1;
        END;

        IF @ToSlotId IS NOT NULL
        BEGIN
            SELECT @ToLocationNo=LocationNo
            FROM dbo.FVN_SlotMesLocation
            WHERE SiteCode=@SiteCode AND SlotId=@ToSlotId AND IsPrimary=1 AND IsActive=1;
            IF @ToLocationNo IS NULL
                THROW 52515, ''Destination Slot has no active primary MES location mapping.'', 1;
        END;

        EXEC MES.usp_PostInventoryMovement
            @SiteCode=@SiteCode,
            @PartNo=@PartNo,
            @MaterialLotId=@MaterialLotId,
            @MovementType=@MovementType,
            @Quantity=@Quantity,
            @UomCode=@UomCode,
            @ReferenceType=@ReferenceType,
            @IdempotencyKey=@IdempotencyKey,
            @TransactionAtUtc=@TransactionAtUtc,
            @ActorCode=@ActorCode,
            @InventoryTransactionId=@InventoryTransactionId OUTPUT,
            @FromLocationNo=@FromLocationNo,
            @ToLocationNo=@ToLocationNo,
            @FromStockState=@FromStockState,
            @ToStockState=@ToStockState,
            @ReferenceNo=@ReferenceNo,
            @ReferenceLineNo=@ReferenceLineNo;
    END;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('025', N'PCTP adapter to canonical MES inventory posting');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* PCTP inventory commands must persist the returned InventoryTransactionId
   on their business document/audit row. Never update FVN_STOCKTP.SLCONLAI
   or FVN_SlotLot.Quantity as a stock movement. */
SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '025';
GO
