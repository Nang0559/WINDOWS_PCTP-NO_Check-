/*
 Migration 016: PCTP-owned IFS delivery-order snapshots and delivery history.
 Prerequisite: 015.
 SQL Server 2016.
 Snapshot tables are PCTP integration snapshots, not the authoritative IFS part master.
 Delivery records retain the proven MP/SP/customer/order/LOT fields.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 51600, 'Migration 016 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 51601, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '015')
    THROW 51602, 'Migration 015 must be applied before migration 016.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '016')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.FVN_IFSPHIEUGIAOHANG', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_IFSPHIEUGIAOHANG
        (
            SnapshotRowId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            STT nvarchar(500) NULL,
            FIND nvarchar(500) NULL,
            CUSTOMER_NO nvarchar(500) NULL,
            GIOGIAO nvarchar(500) NULL,
            SOLUONG decimal(18,4) NULL,
            DV nvarchar(500) NULL,
            ADDNM nvarchar(500) NULL,
            CUA nvarchar(500) NULL,
            MAHANG nvarchar(500) NULL,
            PartNo nvarchar(100) NULL,
            TENHANG nvarchar(500) NULL,
            CUSTOMER_PO_REL_NO nvarchar(500) NULL,
            TRUYEN nvarchar(500) NULL,
            ORDER_NO nvarchar(500) NULL,
            CATALOG_NO nvarchar(500) NULL,
            NGAYGIAO nvarchar(500) NULL,
            CUSTOMER_PO_NO nvarchar(500) NULL,
            HOP nvarchar(500) NULL,
            LOT nvarchar(500) NULL,
            DIA_CHI nvarchar(500) NULL,
            KGX nvarchar(500) NULL,
            STATUS nvarchar(500) NULL,
            STATUSDOC nvarchar(500) NULL,
            TTPHIEU nvarchar(500) NULL,
            GIOGIAOFCC nvarchar(500) NULL,
            SHIP_ADDR_NO nvarchar(500) NULL,
            NHAMAY nvarchar(500) NULL,
            NOTE nvarchar(500) NULL,
            PO_NO nvarchar(500) NULL,
            PO_ITEM nvarchar(500) NULL,
            SourceBatchId bigint NULL,
            SourceCapturedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_IFSPHIEU_Captured DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_IFSPHIEUGIAOHANG PRIMARY KEY (SnapshotRowId),
            CONSTRAINT FK_FVN_IFSPHIEUGIAOHANG_Site FOREIGN KEY (SiteCode) REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_IFSPHIEUGIAOHANG_Part FOREIGN KEY (SiteCode, PartNo) REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT CK_FVN_IFSPHIEU_Qty CHECK (SOLUONG IS NULL OR SOLUONG >= 0)
        );
        CREATE INDEX IX_FVN_IFSPHIEUGIAOHANG_Order
            ON dbo.FVN_IFSPHIEUGIAOHANG (SiteCode, ORDER_NO, PO_NO, PO_ITEM);
        CREATE INDEX IX_FVN_IFSPHIEUGIAOHANG_CustomerPart
            ON dbo.FVN_IFSPHIEUGIAOHANG (SiteCode, CUSTOMER_NO, MAHANG);
    END;

    IF OBJECT_ID(N'dbo.FVN_IFSPHIEUGIAOHANG_SP', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_IFSPHIEUGIAOHANG_SP
        (
            SnapshotRowId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            STT nvarchar(500) NULL,
            FIND nvarchar(500) NULL,
            CUSTOMER_NO nvarchar(500) NULL,
            GIOGIAO nvarchar(500) NULL,
            SOLUONG decimal(18,4) NULL,
            DV nvarchar(500) NULL,
            ADDNM nvarchar(500) NULL,
            CUA nvarchar(500) NULL,
            MAHANG nvarchar(500) NULL,
            PartNo nvarchar(100) NULL,
            TENHANG nvarchar(500) NULL,
            CUSTOMER_PO_REL_NO nvarchar(500) NULL,
            TRUYEN nvarchar(500) NULL,
            ORDER_NO nvarchar(500) NULL,
            CATALOG_NO nvarchar(500) NULL,
            NGAYGIAO nvarchar(500) NULL,
            CUSTOMER_PO_NO nvarchar(500) NULL,
            HOP nvarchar(500) NULL,
            LOT nvarchar(500) NULL,
            DIA_CHI nvarchar(500) NULL,
            KGX nvarchar(500) NULL,
            STATUS nvarchar(500) NULL,
            STATUSDOC nvarchar(500) NULL,
            TTPHIEU nvarchar(500) NULL,
            GIOGIAOFCC nvarchar(500) NULL,
            SHIP_ADDR_NO nvarchar(500) NULL,
            NHAMAY nvarchar(500) NULL,
            NOTE nvarchar(500) NULL,
            PO_NO nvarchar(500) NULL,
            PO_ITEM nvarchar(500) NULL,
            SourceBatchId bigint NULL,
            SourceCapturedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_IFSPHIEUSP_Captured DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_IFSPHIEUGIAOHANG_SP PRIMARY KEY (SnapshotRowId),
            CONSTRAINT FK_FVN_IFSPHIEUGIAOHANG_SP_Site FOREIGN KEY (SiteCode) REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_IFSPHIEUGIAOHANG_SP_Part FOREIGN KEY (SiteCode, PartNo) REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT CK_FVN_IFSPHIEU_SP_Qty CHECK (SOLUONG IS NULL OR SOLUONG >= 0)
        );
        CREATE INDEX IX_FVN_IFSPHIEUGIAOHANG_SP_Order
            ON dbo.FVN_IFSPHIEUGIAOHANG_SP (SiteCode, ORDER_NO, PO_NO, PO_ITEM);
        CREATE INDEX IX_FVN_IFSPHIEUGIAOHANG_SP_CustomerPart
            ON dbo.FVN_IFSPHIEUGIAOHANG_SP (SiteCode, CUSTOMER_NO, MAHANG);
    END;

    IF OBJECT_ID(N'dbo.FVN_LUUPHIEUGIAOHANG', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_LUUPHIEUGIAOHANG
        (
            DeliveryRowId bigint IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            STT smallint NULL,
            CUA nvarchar(100) NULL,
            TRUYEN nvarchar(100) NULL,
            MAHANG nvarchar(100) NULL,
            TENHANG nvarchar(120) NULL,
            LOT nvarchar(500) NULL,
            DV nvarchar(100) NULL,
            SOLUONG int NULL,
            NGAYGIAO smalldatetime NULL,
            GIOGIAO nvarchar(100) NULL,
            STATUS nvarchar(100) NULL,
            TTPHIEU nvarchar(100) NULL,
            NHAMAY nvarchar(120) NULL,
            GIOGIAOFCC nvarchar(100) NULL,
            GearYMVN nvarchar(100) NULL,
            HOP int NULL,
            STATUSDOC nchar(50) NULL,
            Note nvarchar(500) NULL,
            TGLUU rowversion NOT NULL,
            PO_NO nvarchar(400) NULL,
            PO_ITEM nvarchar(400) NULL,
            PartNo nvarchar(100) NULL,
            MaterialLotId bigint NULL,
            CustomerCode nvarchar(60) NULL,
            SourceDocumentNo nvarchar(100) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LUU_Created DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_FVN_LUUPHIEUGIAOHANG PRIMARY KEY (DeliveryRowId),
            CONSTRAINT FK_FVN_LUU_Site FOREIGN KEY (SiteCode) REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT UQ_FVN_LUU_SiteDelivery UNIQUE (SiteCode, DeliveryRowId),
            CONSTRAINT FK_FVN_LUU_IFSPart FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_LUU_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT CK_FVN_LUU_Qty CHECK (SOLUONG >= 0)
        );

        /* Source UQ_LUUPHIEU business columns retained, scoped by SiteCode. */
        CREATE UNIQUE INDEX UX_FVN_LUUPHIEUGIAOHANG_Business
            ON dbo.FVN_LUUPHIEUGIAOHANG
            (SiteCode, STT, MAHANG, NGAYGIAO, CUA, TRUYEN, SOLUONG, GIOGIAO, NHAMAY);

        CREATE INDEX IX_FVN_LUUPHIEUGIAOHANG_NgayNhaMayGio
            ON dbo.FVN_LUUPHIEUGIAOHANG (SiteCode, NGAYGIAO, NHAMAY, GIOGIAO);
        CREATE INDEX IX_FVN_LUUPHIEUGIAOHANG_GioNhaMay
            ON dbo.FVN_LUUPHIEUGIAOHANG (SiteCode, GIOGIAOFCC, NHAMAY);
        CREATE INDEX IX_FVN_LUUPHIEUGIAOHANG_PO
            ON dbo.FVN_LUUPHIEUGIAOHANG (SiteCode, PO_NO, PO_ITEM);
        CREATE INDEX IX_FVN_LUUPHIEUGIAOHANG_Lot
            ON dbo.FVN_LUUPHIEUGIAOHANG (SiteCode, PartNo, LOT);
    END;

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('016', N'PCTP IFS order snapshots and delivery history');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Integration contract:
   - IFS snapshots retain raw order/PO fields and are not part masters.
   - Delivery business rows remain separate from MES inventory movements.
   - Posting a confirmed delivery must create one MES transaction and persist
     its reference; do not deduct stock by editing FVN_STOCKTP or FVN_SlotLot. */
SELECT
    N'dbo.FVN_LUUPHIEUGIAOHANG' AS DeliveryDocument,
    N'MES.FVN_InventoryTransaction' AS StockMovementAuthority,
    N'(SiteCode, PartNo)' AS PartKey;
GO
