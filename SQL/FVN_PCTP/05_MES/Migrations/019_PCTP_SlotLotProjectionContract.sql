/*
 Migration 019: site-aware SlotLot TVP and atomic slot projection replacement.
 Prerequisite: 018.
 The procedure does not create/deduct stock. It only replaces the slot's QR/LOT
 projection after proving that its aggregate quantities equal the MES balance
 at the mapped MES location. Stock changes must be posted to MES first.
 SQL Server 2016.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51900, 'Migration 019 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 51901, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '018')
    THROW 51902, 'Migration 018 must be applied before migration 019.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '019')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF TYPE_ID(N'dbo.FVN_SlotLotTableType') IS NULL
        EXEC(N'
        CREATE TYPE dbo.FVN_SlotLotTableType AS TABLE
        (
            ItemCode nvarchar(100) NOT NULL,
            LotNo nvarchar(200) NOT NULL,
            Quantity int NOT NULL,
            TemCode nvarchar(200) NULL,
            QrData nvarchar(max) NULL,
            ImportDate datetime NULL,
            NgaySX nvarchar(20) NULL,
            SoPhieuTong nvarchar(50) NULL,
            MaPhieu nvarchar(100) NULL
        );');

    EXEC(N'
    CREATE PROCEDURE dbo.usp_FVN_Slot_SaveLots
        @SiteCode nvarchar(30),
        @SlotId int,
        @Lots dbo.FVN_SlotLotTableType READONLY
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @LocationNo nvarchar(100), @LockResult int;
        DECLARE @LockResource nvarchar(255);

        IF NULLIF(LTRIM(RTRIM(@SiteCode)), N'''') IS NULL
            THROW 51910, ''SiteCode is required.'', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.FVN_Slot
            WHERE SiteCode=@SiteCode AND SlotId=@SlotId AND IsActive=1
        )
            THROW 51911, ''Slot does not exist or is inactive for this SiteCode.'', 1;

        SELECT @LocationNo=LocationNo
        FROM dbo.FVN_SlotMesLocation
        WHERE SiteCode=@SiteCode AND SlotId=@SlotId AND IsPrimary=1 AND IsActive=1;

        IF @LocationNo IS NULL
            THROW 51912, ''Slot has no active primary MES location mapping.'', 1;

        IF EXISTS (SELECT 1 FROM @Lots WHERE Quantity < 0)
            THROW 51913, ''SlotLot quantity cannot be negative.'', 1;

        IF EXISTS
        (
            SELECT 1 FROM @Lots l
            WHERE NOT EXISTS
            (
                SELECT 1 FROM IFS.FVN_InventoryPart p
                WHERE p.SiteCode=@SiteCode AND p.PartNo=l.ItemCode
            )
        )
            THROW 51914, ''A SlotLot item does not exist in IFS for the supplied SiteCode.'', 1;

        IF EXISTS
        (
            SELECT 1 FROM @Lots l
            WHERE NOT EXISTS
            (
                SELECT 1 FROM MES.FVN_MaterialLot ml
                WHERE ml.SiteCode=@SiteCode AND ml.PartNo=l.ItemCode AND ml.LotNo=l.LotNo
            )
        )
            THROW 51915, ''A SlotLot LOT has no canonical MES.FVN_MaterialLot identity.'', 1;

        SET @LockResource = N''PCTP_SLOT_SAVE:'' + @SiteCode + N'':'' + CONVERT(nvarchar(20),@SlotId);

        BEGIN TRY
            BEGIN TRANSACTION;

            EXEC @LockResult=sys.sp_getapplock
                @Resource=@LockResource,
                @LockMode=''Exclusive'',
                @LockOwner=''Transaction'',
                @LockTimeout=10000;

            IF @LockResult < 0
                THROW 51916, ''Could not acquire the slot save lock.'', 1;

            /* Compare the entire new slot projection with canonical MES on-hand.
               Compare aggregate by (SiteCode, PartNo, MaterialLotId), not QR rows. */
            DECLARE @NewProjection TABLE
            (
                PartNo nvarchar(100) NOT NULL,
                MaterialLotId bigint NOT NULL,
                Qty decimal(19,6) NOT NULL,
                PRIMARY KEY (PartNo, MaterialLotId)
            );
            DECLARE @MesProjection TABLE
            (
                PartNo nvarchar(100) NOT NULL,
                MaterialLotId bigint NOT NULL,
                Qty decimal(19,6) NOT NULL,
                PRIMARY KEY (PartNo, MaterialLotId)
            );

            INSERT INTO @NewProjection (PartNo, MaterialLotId, Qty)
            SELECT l.ItemCode, ml.MaterialLotId, SUM(CONVERT(decimal(19,6),l.Quantity))
            FROM @Lots l
            INNER JOIN MES.FVN_MaterialLot ml
                ON ml.SiteCode=@SiteCode AND ml.PartNo=l.ItemCode AND ml.LotNo=l.LotNo
            GROUP BY l.ItemCode, ml.MaterialLotId;

            INSERT INTO @MesProjection (PartNo, MaterialLotId, Qty)
            SELECT b.PartNo, b.MaterialLotId, SUM(b.OnHandQty)
            FROM MES.FVN_InventoryBalance b WITH (UPDLOCK,HOLDLOCK)
            WHERE b.SiteCode=@SiteCode AND b.LocationNo=@LocationNo
            GROUP BY b.PartNo, b.MaterialLotId;

            IF EXISTS
            (
                SELECT 1
                FROM @NewProjection n
                FULL OUTER JOIN @MesProjection m
                    ON m.PartNo=n.PartNo AND m.MaterialLotId=n.MaterialLotId
                WHERE ISNULL(n.Qty,0) <> ISNULL(m.Qty,0)
            )
                THROW 51917, ''SlotLot projection does not match MES.FVN_InventoryBalance. Post stock movement first.'', 1;

            DELETE FROM dbo.FVN_SlotLot
            WHERE SiteCode=@SiteCode AND SlotId=@SlotId;

            INSERT INTO dbo.FVN_SlotLot
            (
                SiteCode, SlotId, ItemCode, LotNo, Quantity, TemCode, QrData,
                ImportDate, CreatedDate, NgaySX, SoPhieuTong, MaPhieu, MaterialLotId
            )
            SELECT
                @SiteCode, @SlotId, l.ItemCode, l.LotNo, l.Quantity, l.TemCode, l.QrData,
                l.ImportDate, GETDATE(), l.NgaySX, l.SoPhieuTong, l.MaPhieu, ml.MaterialLotId
            FROM @Lots l
            INNER JOIN MES.FVN_MaterialLot ml
                ON ml.SiteCode=@SiteCode AND ml.PartNo=l.ItemCode AND ml.LotNo=l.LotNo;

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('019', N'Site-aware SlotLot projection and MES balance reconciliation');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Type and procedure are the only supported write path for replacing a slot's
   QR/LOT projection. Do not call the legacy dbo.usp_Slot_SaveLots. */
SELECT
    TYPE_NAME(tt.user_type_id) AS TableTypeName,
    OBJECT_SCHEMA_NAME(p.object_id) AS ProcedureSchema,
    p.name AS ProcedureName
FROM sys.table_types tt
CROSS JOIN sys.procedures p
WHERE tt.name = N'FVN_SlotLotTableType'
  AND p.name = N'usp_FVN_Slot_SaveLots';
GO
