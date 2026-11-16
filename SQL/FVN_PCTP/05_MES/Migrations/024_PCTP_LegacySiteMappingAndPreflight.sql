/*
 Migration 024: explicit source-to-target site/part/LOT mapping and read-only preflight.
 Prerequisite: 023.
 Source database B7R2_FCC is read-only. This migration does not copy or update source rows.
 Run dbo.usp_FVN_PCTP_LegacyPreflight after inserting the approved mapping rows.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52400, 'Migration 024 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52401, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '023')
    THROW 52402, 'Migration 023 must be applied before migration 024.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '024')
    RETURN;
IF DB_ID(N'B7R2_FCC') IS NULL
    THROW 52403, 'Source database B7R2_FCC is not visible on this SQL Server instance.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.FVN_LegacyWarehouseSiteMap', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_LegacyWarehouseSiteMap
        (
            LegacyWarehouseId int NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            TargetWarehouseId int NOT NULL,
            MappedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LegacyWarehouseSiteMap_Mapped DEFAULT (SYSUTCDATETIME()),
            MappedBy nvarchar(100) NOT NULL,
            MappingNote nvarchar(500) NULL,
            CONSTRAINT PK_FVN_LegacyWarehouseSiteMap PRIMARY KEY (LegacyWarehouseId),
            CONSTRAINT UQ_FVN_LegacyWarehouseSiteMap_Target UNIQUE (SiteCode, TargetWarehouseId),
            CONSTRAINT FK_FVN_LegacyWarehouseSiteMap_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_LegacyWarehouseSiteMap_Warehouse FOREIGN KEY (SiteCode, TargetWarehouseId)
                REFERENCES dbo.FVN_Warehouse (SiteCode, WarehouseId)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_LegacyPartSiteMap', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_LegacyPartSiteMap
        (
            SourceObject varchar(40) NOT NULL,
            LegacyPartNo nvarchar(100) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            MappedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LegacyPartSiteMap_Mapped DEFAULT (SYSUTCDATETIME()),
            MappedBy nvarchar(100) NOT NULL,
            MappingNote nvarchar(500) NULL,
            CONSTRAINT PK_FVN_LegacyPartSiteMap PRIMARY KEY (SourceObject, LegacyPartNo, SiteCode),
            CONSTRAINT FK_FVN_LegacyPartSiteMap_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT CK_FVN_LegacyPartSiteMap_Source CHECK
                (SourceObject IN ('SlotLot','StockHistory','STOCKTP','Delivery','ReturnDetail','Abnormal'))
        );
        CREATE INDEX IX_FVN_LegacyPartSiteMap_Target
            ON dbo.FVN_LegacyPartSiteMap (SiteCode, PartNo);
    END;

    IF OBJECT_ID(N'dbo.FVN_LegacyLotSiteMap', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_LegacyLotSiteMap
        (
            SourceObject varchar(40) NOT NULL,
            LegacyLotNo nvarchar(400) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL,
            MaterialLotId bigint NOT NULL,
            MappedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LegacyLotSiteMap_Mapped DEFAULT (SYSUTCDATETIME()),
            MappedBy nvarchar(100) NOT NULL,
            MappingNote nvarchar(500) NULL,
            CONSTRAINT PK_FVN_LegacyLotSiteMap PRIMARY KEY (SourceObject, LegacyLotNo, SiteCode, PartNo),
            CONSTRAINT FK_FVN_LegacyLotSiteMap_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT CK_FVN_LegacyLotSiteMap_Source CHECK
                (SourceObject IN ('SlotLot','StockHistory','STOCKTP','Delivery','ReturnDetail','Abnormal','InspectionLog'))
        );
        CREATE INDEX IX_FVN_LegacyLotSiteMap_Target
            ON dbo.FVN_LegacyLotSiteMap (SiteCode, PartNo, MaterialLotId);
    END;

    IF OBJECT_ID(N'dbo.FVN_LegacyFactorySiteMap', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_LegacyFactorySiteMap
        (
            LegacyFactoryKey nvarchar(100) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            MappedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LegacyFactorySiteMap_Mapped DEFAULT (SYSUTCDATETIME()),
            MappedBy nvarchar(100) NOT NULL,
            MappingNote nvarchar(500) NULL,
            CONSTRAINT PK_FVN_LegacyFactorySiteMap PRIMARY KEY (LegacyFactoryKey),
            CONSTRAINT FK_FVN_LegacyFactorySiteMap_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_LegacyDocumentSiteMap', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_LegacyDocumentSiteMap
        (
            SourceObject varchar(40) NOT NULL,
            LegacyDocumentNo nvarchar(200) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            MappedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LegacyDocumentSiteMap_Mapped DEFAULT (SYSUTCDATETIME()),
            MappedBy nvarchar(100) NOT NULL,
            MappingNote nvarchar(500) NULL,
            CONSTRAINT PK_FVN_LegacyDocumentSiteMap PRIMARY KEY (SourceObject, LegacyDocumentNo),
            CONSTRAINT FK_FVN_LegacyDocumentSiteMap_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT CK_FVN_LegacyDocumentSiteMap_Source CHECK
                (SourceObject IN ('IFSPHIEUGIAOHANG','IFSPHIEUGIAOHANG_SP','LUUPHIEUGIAOHANG',
                                  'FVN_PhieuTraHang','FVN_PhieuXuLyBatThuong','FVN_Purchase_Order_DB_HD'))
        );
    END;

    EXEC(N'
    CREATE PROCEDURE dbo.usp_FVN_PCTP_LegacyPreflight
    AS
    BEGIN
        SET NOCOUNT ON;
        IF DB_ID(N''B7R2_FCC'') IS NULL
            THROW 52410, ''Source database B7R2_FCC is not visible.'', 1;

        /* Result 1: source row counts (metadata only; no source writes). */
        SELECT N''Warehouse'' AS SourceObject, COUNT_BIG(*) AS SourceRows FROM B7R2_FCC.dbo.Warehouse
        UNION ALL SELECT N''Rack'', COUNT_BIG(*) FROM B7R2_FCC.dbo.Rack
        UNION ALL SELECT N''Slot'', COUNT_BIG(*) FROM B7R2_FCC.dbo.Slot
        UNION ALL SELECT N''SlotLot'', COUNT_BIG(*) FROM B7R2_FCC.dbo.SlotLot
        UNION ALL SELECT N''STOCKTP'', COUNT_BIG(*) FROM B7R2_FCC.dbo.STOCKTP
        UNION ALL SELECT N''StockHistory'', COUNT_BIG(*) FROM B7R2_FCC.dbo.StockHistory
        UNION ALL SELECT N''IFSPHIEUGIAOHANG'', COUNT_BIG(*) FROM B7R2_FCC.dbo.IFSPHIEUGIAOHANG
        UNION ALL SELECT N''IFSPHIEUGIAOHANG_SP'', COUNT_BIG(*) FROM B7R2_FCC.dbo.IFSPHIEUGIAOHANG_SP
        UNION ALL SELECT N''LUUPHIEUGIAOHANG'', COUNT_BIG(*) FROM B7R2_FCC.dbo.LUUPHIEUGIAOHANG;

        /* Result 2: warehouses without an approved SiteCode/target mapping. */
        SELECT w.WarehouseId, w.Name
        FROM B7R2_FCC.dbo.Warehouse w
        LEFT JOIN dbo.FVN_LegacyWarehouseSiteMap m
            ON m.LegacyWarehouseId=w.WarehouseId
        WHERE m.LegacyWarehouseId IS NULL
        ORDER BY w.WarehouseId;

        /* Result 3: Rack/Slot hierarchy rows that cannot resolve through mapped warehouses. */
        SELECT r.RackId, r.WarehouseId
        FROM B7R2_FCC.dbo.Rack r
        LEFT JOIN dbo.FVN_LegacyWarehouseSiteMap m
            ON m.LegacyWarehouseId=r.WarehouseId
        WHERE m.LegacyWarehouseId IS NULL
        ORDER BY r.RackId;

        SELECT s.SlotId, s.RackId
        FROM B7R2_FCC.dbo.Slot s
        LEFT JOIN B7R2_FCC.dbo.Rack r ON r.RackId=s.RackId
        LEFT JOIN dbo.FVN_LegacyWarehouseSiteMap m ON m.LegacyWarehouseId=r.WarehouseId
        WHERE r.RackId IS NULL OR m.LegacyWarehouseId IS NULL
        ORDER BY s.SlotId;

        /* Result 4: SlotLot rows missing IFS part or MES LOT mapping. */
        SELECT sl.SlotLotId, sl.SlotId, sl.ItemCode, sl.LotNo,
               m.SiteCode, pm.PartNo, lm.MaterialLotId
        FROM B7R2_FCC.dbo.SlotLot sl
        LEFT JOIN B7R2_FCC.dbo.Slot s ON s.SlotId=sl.SlotId
        LEFT JOIN B7R2_FCC.dbo.Rack r ON r.RackId=s.RackId
        LEFT JOIN dbo.FVN_LegacyWarehouseSiteMap m ON m.LegacyWarehouseId=r.WarehouseId
        LEFT JOIN dbo.FVN_LegacyPartSiteMap pm
            ON pm.SourceObject=''SlotLot'' AND pm.LegacyPartNo=sl.ItemCode AND pm.SiteCode=m.SiteCode
        LEFT JOIN dbo.FVN_LegacyLotSiteMap lm
            ON lm.SourceObject=''SlotLot'' AND lm.LegacyLotNo=sl.LotNo
           AND lm.SiteCode=m.SiteCode AND lm.PartNo=pm.PartNo
        WHERE m.SiteCode IS NULL OR pm.PartNo IS NULL OR lm.MaterialLotId IS NULL
        ORDER BY sl.SlotLotId;

        /* Result 5: STOCKTP has no warehouse key; every LOT needs an explicit site/part map. */
        SELECT st.LOT, st.PART, lm.SiteCode, lm.PartNo, lm.MaterialLotId
        FROM B7R2_FCC.dbo.STOCKTP st
        LEFT JOIN dbo.FVN_LegacyLotSiteMap lm
            ON lm.SourceObject=''STOCKTP'' AND lm.LegacyLotNo=st.LOT
        WHERE lm.MaterialLotId IS NULL OR (st.PART IS NOT NULL AND lm.PartNo IS NULL)
        ORDER BY st.LOT;

        /* Result 6: StockHistory without a slot-derived or explicit LOT map. */
        SELECT h.Id, h.SlotId, h.ItemCode, h.LotNo
        FROM B7R2_FCC.dbo.StockHistory h
        LEFT JOIN B7R2_FCC.dbo.Slot s ON s.SlotId=h.SlotId
        LEFT JOIN B7R2_FCC.dbo.Rack r ON r.RackId=s.RackId
        LEFT JOIN dbo.FVN_LegacyWarehouseSiteMap wm ON wm.LegacyWarehouseId=r.WarehouseId
        LEFT JOIN dbo.FVN_LegacyLotSiteMap lm
            ON lm.SourceObject=''StockHistory'' AND lm.LegacyLotNo=h.LotNo
        WHERE (wm.SiteCode IS NULL AND lm.SiteCode IS NULL)
        ORDER BY h.Id;

        /* Result 7: delivery/return records still need document/factory site mapping. */
        SELECT N''LUUPHIEUGIAOHANG'' AS SourceObject, d.STT, d.MAHANG, d.LOT, d.NHAMAY
        FROM B7R2_FCC.dbo.LUUPHIEUGIAOHANG d
        LEFT JOIN dbo.FVN_LegacyFactorySiteMap fm ON fm.LegacyFactoryKey=LTRIM(RTRIM(d.NHAMAY))
        WHERE fm.SiteCode IS NULL
        ORDER BY d.NGAYGIAO, d.STT;

        SELECT N''FVN_PhieuTraHang'' AS SourceObject, p.Id, p.SoPhieu
        FROM B7R2_FCC.dbo.FVN_PhieuTraHang p
        LEFT JOIN dbo.FVN_LegacyDocumentSiteMap dm
            ON dm.SourceObject=''FVN_PhieuTraHang'' AND dm.LegacyDocumentNo=p.SoPhieu
        WHERE dm.SiteCode IS NULL
        ORDER BY p.Id;
    END;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('024', N'PCTP legacy site mapping and read-only migration preflight');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Execute only after migrations 000–023 and after B7R2_FCC is visible:
   EXEC dbo.usp_FVN_PCTP_LegacyPreflight;
   The procedure is read-only against B7R2_FCC and returns unresolved mapping rows. */
SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '024';
GO
