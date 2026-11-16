/*
 Migration 021: site-scoped FIFO policy writer and MES-backed PCTP slot inventory view.
 Prerequisite: 020.
 The FIFO upsert preserves the legacy rule: no history row when the value is unchanged.
 Slot quantities are read from MES.FVN_InventoryBalance only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52100, 'Migration 021 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52101, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '020')
    THROW 52102, 'Migration 020 must be applied before migration 021.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '021')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC(N'
    CREATE PROCEDURE dbo.usp_FVN_ItemFifoConfig_Upsert
        @SiteCode nvarchar(30),
        @PartNo nvarchar(100),
        @EnforceFifo bit,
        @UpdatedBy nvarchar(50)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF NULLIF(LTRIM(RTRIM(@SiteCode)),N'''') IS NULL
            THROW 52110, ''SiteCode is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@PartNo)),N'''') IS NULL
            THROW 52111, ''PartNo is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@UpdatedBy)),N'''') IS NULL
            THROW 52112, ''UpdatedBy is required.'', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode=@SiteCode AND IsActive=1)
            THROW 52113, ''PCTP site is not registered or inactive.'', 1;
        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_InventoryPart WHERE SiteCode=@SiteCode AND PartNo=@PartNo)
            THROW 52114, ''PartNo does not exist in IFS for the supplied SiteCode.'', 1;

        DECLARE @PreviousValue bit;
        BEGIN TRY
            BEGIN TRANSACTION;

            SELECT @PreviousValue=EnforceFifo
            FROM dbo.FVN_ItemFifoConfig WITH (UPDLOCK,HOLDLOCK)
            WHERE SiteCode=@SiteCode AND PartNo=@PartNo;

            IF @PreviousValue IS NOT NULL AND @PreviousValue=@EnforceFifo
            BEGIN
                COMMIT TRANSACTION;
                RETURN;
            END;

            IF @PreviousValue IS NULL
            BEGIN
                INSERT INTO dbo.FVN_ItemFifoConfig
                    (SiteCode,PartNo,EnforceFifo,UpdatedAt,UpdatedBy)
                VALUES
                    (@SiteCode,@PartNo,@EnforceFifo,GETDATE(),@UpdatedBy);
            END
            ELSE
            BEGIN
                UPDATE dbo.FVN_ItemFifoConfig
                SET EnforceFifo=@EnforceFifo, UpdatedAt=GETDATE(), UpdatedBy=@UpdatedBy
                WHERE SiteCode=@SiteCode AND PartNo=@PartNo;
            END;

            INSERT INTO dbo.FVN_ItemFifoConfig_History
                (SiteCode,PartNo,EnforceFifoCu,EnforceFifoMoi,ThoiGian,NguoiThucHien)
            VALUES
                (@SiteCode,@PartNo,@PreviousValue,@EnforceFifo,GETDATE(),@UpdatedBy);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;
    ');

    EXEC(N'
    CREATE VIEW dbo.vFVN_PCTP_SlotInventory
    AS
    SELECT
        w.SiteCode,
        w.WarehouseId,
        w.Name AS WarehouseName,
        r.RackId,
        r.RackName,
        s.SlotId,
        s.SlotNumber,
        lm.LocationNo,
        b.PartNo,
        ip.Description AS PartDescription,
        ip.UnitMeas,
        ml.MaterialLotId,
        ml.LotNo,
        ml.ManufacturedDate,
        ml.ExpiryDate,
        b.StockState,
        b.OnHandQty,
        b.ReservedQty,
        b.OnHandQty-b.ReservedQty AS AvailableQty,
        s.IsOccupied,
        s.IsActive AS SlotIsActive
    FROM dbo.FVN_Slot s
    INNER JOIN dbo.FVN_Rack r
        ON r.SiteCode=s.SiteCode AND r.RackId=s.RackId
    INNER JOIN dbo.FVN_Warehouse w
        ON w.SiteCode=r.SiteCode AND w.WarehouseId=r.WarehouseId
    INNER JOIN dbo.FVN_SlotMesLocation lm
        ON lm.SiteCode=s.SiteCode AND lm.SlotId=s.SlotId
       AND lm.IsPrimary=1 AND lm.IsActive=1
    INNER JOIN MES.FVN_InventoryBalance b
        ON b.SiteCode=lm.SiteCode AND b.LocationNo=lm.LocationNo
    INNER JOIN MES.FVN_MaterialLot ml
        ON ml.SiteCode=b.SiteCode AND ml.PartNo=b.PartNo
       AND ml.MaterialLotId=b.MaterialLotId
    INNER JOIN IFS.FVN_InventoryPart ip
        ON ip.SiteCode=b.SiteCode AND ip.PartNo=b.PartNo;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('021', N'PCTP FIFO policy writer and MES-backed slot inventory view');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '021';
GO
