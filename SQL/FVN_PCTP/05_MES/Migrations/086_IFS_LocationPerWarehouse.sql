/*
 Migration 086: IFS Location is decided per MES Kho (warehouse); Rack / Slot exist ONLY in MES.

 Model (what IFS can and cannot express):
   IFS     : Site  ->  Warehouse  ->  Location (inventory location of that warehouse).        IFS has NO Rack and NO Slot.
   PCTP/MES: Site  ->  Kho (dbo.FVN_Warehouse)  ->  Rack (dbo.FVN_Rack)  ->  Slot (dbo.FVN_Slot)   (physical layout, MES only).
 Until now 083 asked for an IFS Location for EVERY Slot, which mixed the two layers (the screen "Map Slot kho <-> MES Location" treated an IFS
 Location as if it were a Slot). From 086 on:
   * dbo.FVN_Warehouse.IfsWarehouseCode (085) maps the Kho to its IFS warehouse and the NEW dbo.FVN_Warehouse.IfsLocationNo picks the ONE IFS
     Location that every Slot of the Kho posts to (must belong to the mapped IFS warehouse).
   * A Slot only gets its area (PART / WIP / ...) in MES. Its dbo.FVN_SlotMesLocation row is a DERIVED projection of the Kho's IfsLocationNo,
     kept so every existing consumer (receiving, stock lookup, Part -> WIP, posting adapters, migrations 019-043 / 082) keeps reading the same table.

 Adds / changes (nothing is deleted, no stock, Rack or Slot row is modified):
   - dbo.FVN_Warehouse.IfsLocationNo + FK to MES.FVN_IfsInventoryLocation (SiteCode, LocationNo).
   - Backfill: a Kho already mapped to an IFS warehouse whose active Slots all point to the same Location of that warehouse gets that Location.
     Kho whose Slots point to several Locations are left alone (they keep working, flagged SLOT_LEVEL_LEGACY) until a Location is chosen for the Kho.
   - MES.usp_FVN_Warehouse_SyncSlotLocations : projects the Kho Location onto the FVN_SlotMesLocation rows of its Slots (history kept IsActive = 0).
   - MES.usp_FVN_Warehouse_SetLocation       : choose / remove the IFS Location of a Kho, then project it.
   - MES.usp_FVN_Warehouse_MapIfs (ALTER)    : changing / removing the IFS warehouse clears a Location that no longer belongs to it, then projects
                                               (replaces the 085 guard 57860, which would now block on derived rows).
   - MES.usp_FVN_SlotMesLocation_Assign (ALTER): sets the AREA only. @LocationNo is optional and, if given, must equal the Kho Location (57877).
   - MES.usp_FVN_SlotMesLocation_Clear  (ALTER): removes the AREA only; the Location stays with the Kho.
   - dbo.TR_FVN_Slot_InheritWarehouseLocation  : a newly created Slot inherits the Kho Location.
   - Views vw_FVN_WarehouseIfsMapStatus / vw_FVN_SlotMesLocationStatus (trailing columns only) / vw_FVN_IfsMapHub (LOCATION stage = per Kho).
 READY logic of 083 is unchanged. Written with stub + ALTER like 080 / 083 / 085. Apply after 085 (sqlcmd -b -I).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57870, 'Migration 086 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '085')
    THROW 57871, 'Migration 085 must be registered before 086.', 1;
IF COL_LENGTH(N'dbo.FVN_Warehouse', N'IfsWarehouseCode') IS NULL
   OR OBJECT_ID(N'dbo.FVN_SlotMesLocation', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_IfsInventoryLocation', N'U') IS NULL
    THROW 57872, 'Warehouse IFS map (085) or slot / location tables are missing.', 1;
GO

/* ------------------------------------------------------------------ 1. column + key */
IF COL_LENGTH(N'dbo.FVN_Warehouse', N'IfsLocationNo') IS NULL
    ALTER TABLE dbo.FVN_Warehouse ADD IfsLocationNo nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_Warehouse_IfsLocation')
    ALTER TABLE dbo.FVN_Warehouse WITH CHECK ADD CONSTRAINT FK_FVN_Warehouse_IfsLocation
        FOREIGN KEY (SiteCode, IfsLocationNo) REFERENCES MES.FVN_IfsInventoryLocation (SiteCode, LocationNo);
GO

/* ------------------------------------------------------------------ 2. backfill (only unambiguous Kho) */
UPDATE w
   SET w.IfsLocationNo = x.LocationNo
FROM dbo.FVN_Warehouse AS w
JOIN (SELECT r.SiteCode, r.WarehouseId, MIN(sm.LocationNo) AS LocationNo
      FROM dbo.FVN_Rack AS r
      JOIN dbo.FVN_Slot AS s ON s.SiteCode = r.SiteCode AND s.RackId = r.RackId AND s.IsActive = 1
      JOIN dbo.FVN_SlotMesLocation AS sm ON sm.SiteCode = s.SiteCode AND sm.SlotId = s.SlotId AND sm.IsPrimary = 1 AND sm.IsActive = 1
      WHERE r.IsActive = 1
      GROUP BY r.SiteCode, r.WarehouseId
      HAVING COUNT(DISTINCT sm.LocationNo) = 1) AS x ON x.SiteCode = w.SiteCode AND x.WarehouseId = w.WarehouseId
JOIN MES.FVN_IfsInventoryLocation AS loc ON loc.SiteCode = x.SiteCode AND loc.LocationNo = x.LocationNo AND loc.IsActive = 1
WHERE w.IsActive = 1 AND w.IfsLocationNo IS NULL AND w.IfsWarehouseCode IS NOT NULL AND loc.WarehouseCode = w.IfsWarehouseCode;
GO

/* ------------------------------------------------------------------ 3. projection: Kho Location -> Slot rows */
IF OBJECT_ID(N'MES.usp_FVN_Warehouse_SyncSlotLocations', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_Warehouse_SyncSlotLocations AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_Warehouse_SyncSlotLocations
    @SiteCode    nvarchar(30),
    @WarehouseId int = NULL,        /* NULL = every Kho of the Site THAT HAS a Location; a given Kho is projected even when it has none (= retire) */
    @ActorCode   nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    IF @SiteCode = N'' THROW 57873, N'SiteCode là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57874, N'ActorCode là bắt buộc.', 1;

    DECLARE @Slots TABLE (SlotId int PRIMARY KEY, LocationNo nvarchar(100) NULL);
    INSERT @Slots (SlotId, LocationNo)
    SELECT s.SlotId, w.IfsLocationNo
    FROM dbo.FVN_Slot AS s
    JOIN dbo.FVN_Rack AS r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId AND r.IsActive = 1
    JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId AND w.IsActive = 1
    WHERE s.SiteCode = @SiteCode AND s.IsActive = 1
      AND ((@WarehouseId IS NULL AND w.IfsLocationNo IS NOT NULL) OR w.WarehouseId = @WarehouseId);

    DECLARE @Own bit = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
    BEGIN TRY
        IF @Own = 1 BEGIN TRANSACTION;

        /* retire projection rows that differ from the Kho Location (or when the Kho has none); history is kept */
        UPDATE sm SET IsPrimary = 0, IsActive = 0
        FROM dbo.FVN_SlotMesLocation AS sm WITH (UPDLOCK, HOLDLOCK)
        JOIN @Slots AS t ON t.SlotId = sm.SlotId
        WHERE sm.SiteCode = @SiteCode AND (sm.IsPrimary = 1 OR sm.IsActive = 1)
          AND (t.LocationNo IS NULL OR sm.LocationNo <> t.LocationNo);

        /* re-activate a retired row of the same Location, then insert what is still missing */
        UPDATE sm SET IsPrimary = 1, IsActive = 1
        FROM dbo.FVN_SlotMesLocation AS sm
        JOIN @Slots AS t ON t.SlotId = sm.SlotId AND sm.LocationNo = t.LocationNo
        WHERE sm.SiteCode = @SiteCode AND (sm.IsPrimary = 0 OR sm.IsActive = 0);

        INSERT dbo.FVN_SlotMesLocation (SiteCode, SlotId, LocationNo, IsPrimary, IsActive, CreatedBy)
        SELECT @SiteCode, t.SlotId, t.LocationNo, 1, 1, @ActorCode
        FROM @Slots AS t
        WHERE t.LocationNo IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.FVN_SlotMesLocation AS x WHERE x.SiteCode = @SiteCode AND x.SlotId = t.SlotId AND x.LocationNo = t.LocationNo);

        IF @Own = 1 COMMIT TRANSACTION;
        SELECT COUNT(*) AS SlotsProjected FROM @Slots;
    END TRY
    BEGIN CATCH
        IF @Own = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ------------------------------------------------------------------ 4. choose the IFS Location of a Kho */
IF OBJECT_ID(N'MES.usp_FVN_Warehouse_SetLocation', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_Warehouse_SetLocation AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_Warehouse_SetLocation
    @SiteCode    nvarchar(30),
    @WarehouseId int,
    @LocationNo  nvarchar(100) = NULL,   /* NULL = remove the Location of the Kho (its Slots stop being READY) */
    @ActorCode   nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @LocationNo = NULLIF(LTRIM(RTRIM(ISNULL(@LocationNo, N''))), N'');
    IF @SiteCode = N'' OR @WarehouseId IS NULL THROW 57873, N'SiteCode và WarehouseId là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57874, N'ActorCode là bắt buộc.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
        THROW 57875, N'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;

    DECLARE @IfsWh nvarchar(60);
    SELECT @IfsWh = IfsWarehouseCode FROM dbo.FVN_Warehouse WHERE SiteCode = @SiteCode AND WarehouseId = @WarehouseId AND IsActive = 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_Warehouse WHERE SiteCode = @SiteCode AND WarehouseId = @WarehouseId AND IsActive = 1)
        THROW 57876, N'Kho không tồn tại hoặc đang ngừng dùng trong Site.', 1;
    IF @LocationNo IS NOT NULL
    BEGIN
        IF @IfsWh IS NULL
            THROW 57878, N'Kho chưa map Kho IFS. Map Kho PCTP ↔ Kho IFS trước, rồi chọn Location của kho IFS đó.', 1;
        IF NOT EXISTS (SELECT 1 FROM MES.FVN_IfsInventoryLocation WHERE SiteCode = @SiteCode AND LocationNo = @LocationNo AND IsActive = 1)
            THROW 57879, N'Location chưa có hoặc đã ngừng trong IFS của Site. Đồng bộ INVENTORY_LOCATION từ IFS rồi chọn lại.', 1;
        IF NOT EXISTS (SELECT 1 FROM MES.FVN_IfsInventoryLocation WHERE SiteCode = @SiteCode AND LocationNo = @LocationNo AND WarehouseCode = @IfsWh)
            THROW 57880, N'Location thuộc kho IFS khác với Kho IFS đã map cho Kho này.', 1;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;
        UPDATE dbo.FVN_Warehouse
           SET IfsLocationNo = @LocationNo, ModifiedAtUtc = SYSUTCDATETIME(), ModifiedBy = @ActorCode
         WHERE SiteCode = @SiteCode AND WarehouseId = @WarehouseId;
        EXEC MES.usp_FVN_Warehouse_SyncSlotLocations @SiteCode = @SiteCode, @WarehouseId = @WarehouseId, @ActorCode = @ActorCode;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ------------------------------------------------------------------ 5. MapIfs: a changed IFS warehouse drops a Location that no longer fits */
ALTER PROCEDURE MES.usp_FVN_Warehouse_MapIfs
    @SiteCode         nvarchar(30),
    @WarehouseId      int,
    @IfsWarehouseCode nvarchar(60) = NULL,   /* NULL = remove the map */
    @ActorCode        nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @IfsWarehouseCode = NULLIF(LTRIM(RTRIM(ISNULL(@IfsWarehouseCode, N''))), N'');
    IF @SiteCode = N'' OR @WarehouseId IS NULL THROW 57854, N'SiteCode và WarehouseId là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57855, N'ActorCode là bắt buộc.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
        THROW 57856, N'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_Warehouse WHERE SiteCode = @SiteCode AND WarehouseId = @WarehouseId AND IsActive = 1)
        THROW 57857, N'Kho không tồn tại hoặc đang ngừng dùng trong Site.', 1;
    IF @IfsWarehouseCode IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM MES.FVN_IfsWarehouse WHERE SiteCode = @SiteCode AND WarehouseCode = @IfsWarehouseCode AND IsActive = 1)
            THROW 57858, N'Kho IFS chưa có hoặc đã ngừng trong Site. Đồng bộ nhóm WAREHOUSE từ IFS rồi chọn lại.', 1;
        IF EXISTS (SELECT 1 FROM dbo.FVN_Warehouse WHERE SiteCode = @SiteCode AND IfsWarehouseCode = @IfsWarehouseCode AND IsActive = 1 AND WarehouseId <> @WarehouseId)
            THROW 57859, N'Kho IFS này đã được map cho một Kho PCTP khác của Site.', 1;
    END;

    DECLARE @PrevLocation nvarchar(100);
    SELECT @PrevLocation = IfsLocationNo FROM dbo.FVN_Warehouse WHERE SiteCode = @SiteCode AND WarehouseId = @WarehouseId;

    BEGIN TRY
        BEGIN TRANSACTION;
        /* The Kho Location is kept only while it still belongs to the (new) IFS warehouse; otherwise it is cleared and the Slots are re-projected. */
        UPDATE w
           SET w.IfsWarehouseCode = @IfsWarehouseCode,
               w.IfsLocationNo = CASE WHEN @IfsWarehouseCode IS NOT NULL AND w.IfsLocationNo IS NOT NULL
                                           AND EXISTS (SELECT 1 FROM MES.FVN_IfsInventoryLocation loc
                                                       WHERE loc.SiteCode = w.SiteCode AND loc.LocationNo = w.IfsLocationNo
                                                         AND loc.WarehouseCode = @IfsWarehouseCode AND loc.IsActive = 1)
                                      THEN w.IfsLocationNo ELSE NULL END,
               w.ModifiedAtUtc = SYSUTCDATETIME(), w.ModifiedBy = @ActorCode
        FROM dbo.FVN_Warehouse AS w
        WHERE w.SiteCode = @SiteCode AND w.WarehouseId = @WarehouseId;

        /* Only a Kho that already worked at Kho level (it had a Location before this call) is re-projected; legacy slot-level rows are left untouched. */
        IF @PrevLocation IS NOT NULL
            EXEC MES.usp_FVN_Warehouse_SyncSlotLocations @SiteCode = @SiteCode, @WarehouseId = @WarehouseId, @ActorCode = @ActorCode;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ------------------------------------------------------------------ 6. views */
ALTER VIEW MES.vw_FVN_WarehouseIfsMapStatus
AS
WITH RackSlot AS
(
    SELECT r.SiteCode, r.WarehouseId, COUNT(DISTINCT r.RackId) AS RackCount, COUNT(s.SlotId) AS SlotCount
    FROM dbo.FVN_Rack AS r
    LEFT JOIN dbo.FVN_Slot AS s ON s.SiteCode = r.SiteCode AND s.RackId = r.RackId AND s.IsActive = 1
    WHERE r.IsActive = 1
    GROUP BY r.SiteCode, r.WarehouseId
),
Loc AS
(
    SELECT SiteCode, WarehouseCode, COUNT(*) AS LocationCount
    FROM MES.FVN_IfsInventoryLocation WHERE IsActive = 1
    GROUP BY SiteCode, WarehouseCode
),
SlotLoc AS
(
    SELECT r.SiteCode, r.WarehouseId, COUNT(DISTINCT sm.LocationNo) AS SlotLocationCount
    FROM dbo.FVN_Rack AS r
    JOIN dbo.FVN_Slot AS s ON s.SiteCode = r.SiteCode AND s.RackId = r.RackId AND s.IsActive = 1
    JOIN dbo.FVN_SlotMesLocation AS sm ON sm.SiteCode = s.SiteCode AND sm.SlotId = s.SlotId AND sm.IsPrimary = 1 AND sm.IsActive = 1
    WHERE r.IsActive = 1
    GROUP BY r.SiteCode, r.WarehouseId
)
SELECT w.SiteCode, w.WarehouseId, w.Name AS WarehouseName, w.IsActive AS WarehouseIsActive,
       w.IfsWarehouseCode, iw.Description AS IfsDescription,
       ISNULL(rs.RackCount, 0) AS RackCount, ISNULL(rs.SlotCount, 0) AS SlotCount, ISNULL(l.LocationCount, 0) AS IfsLocationCount,
       CASE WHEN w.IfsWarehouseCode IS NULL THEN 'NO_IFS_MAP'
            WHEN iw.IsActive = 0 THEN 'IFS_INACTIVE'
            ELSE 'MAPPED' END AS MapStatus,
       w.IfsLocationNo, wl.Description AS IfsLocationDescription,
       CASE WHEN w.IfsWarehouseCode IS NULL THEN 'NO_IFS_MAP'
            WHEN w.IfsLocationNo IS NULL THEN CASE WHEN ISNULL(sl.SlotLocationCount, 0) > 0 THEN 'SLOT_LEVEL_LEGACY' ELSE 'NO_LOCATION' END
            WHEN wl.LocationNo IS NULL OR wl.IsActive = 0 THEN 'LOCATION_INACTIVE'
            WHEN wl.WarehouseCode <> w.IfsWarehouseCode THEN 'LOCATION_MISMATCH'
            ELSE 'OK' END AS LocationMapStatus,
       ISNULL(sl.SlotLocationCount, 0) AS SlotLocationCount
FROM dbo.FVN_Warehouse AS w
LEFT JOIN MES.FVN_IfsWarehouse AS iw ON iw.SiteCode = w.SiteCode AND iw.WarehouseCode = w.IfsWarehouseCode
LEFT JOIN MES.FVN_IfsInventoryLocation AS wl ON wl.SiteCode = w.SiteCode AND wl.LocationNo = w.IfsLocationNo
LEFT JOIN RackSlot AS rs ON rs.SiteCode = w.SiteCode AND rs.WarehouseId = w.WarehouseId
LEFT JOIN SlotLoc AS sl ON sl.SiteCode = w.SiteCode AND sl.WarehouseId = w.WarehouseId
LEFT JOIN Loc AS l ON l.SiteCode = w.SiteCode AND l.WarehouseCode = w.IfsWarehouseCode
WHERE w.IsActive = 1
UNION ALL
SELECT iw.SiteCode, NULL, NULL, NULL, iw.WarehouseCode, iw.Description, 0, 0, ISNULL(l.LocationCount, 0), 'IFS_ONLY', NULL, NULL, NULL, 0
FROM MES.FVN_IfsWarehouse AS iw
LEFT JOIN Loc AS l ON l.SiteCode = iw.SiteCode AND l.WarehouseCode = iw.WarehouseCode
WHERE iw.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.FVN_Warehouse AS w WHERE w.SiteCode = iw.SiteCode AND w.IfsWarehouseCode = iw.WarehouseCode AND w.IsActive = 1);
GO

ALTER VIEW MES.vw_FVN_SlotMesLocationStatus
AS
SELECT s.SiteCode, s.SlotId, w.Name AS WarehouseName, r.RackName, s.SlotNumber,
       ar.AreaCode,
       sm.LocationNo, loc.WarehouseCode AS LocationWarehouseCode, loc.LocationGroupCode, loc.Description AS LocationDescription,
       CASE WHEN ar.AreaCode IS NULL THEN 'NO_AREA'
            WHEN sm.LocationNo IS NULL THEN 'NO_LOCATION'
            WHEN loc.LocationNo IS NULL OR loc.IsActive = 0 THEN 'LOCATION_INACTIVE'
            ELSE 'READY' END AS MapStatus,
       w.IfsWarehouseCode AS WarehouseIfsCode,
       CASE WHEN w.IfsWarehouseCode IS NULL THEN 'NO_IFS_MAP'
            WHEN loc.LocationNo IS NOT NULL AND loc.WarehouseCode <> w.IfsWarehouseCode THEN 'WAREHOUSE_MISMATCH'
            WHEN w.IfsLocationNo IS NULL THEN 'NO_WH_LOCATION'
            WHEN sm.LocationNo IS NULL OR sm.LocationNo <> w.IfsLocationNo THEN 'OUT_OF_SYNC'
            ELSE 'OK' END AS WarehouseMapStatus,
       w.IfsLocationNo AS WarehouseLocationNo
FROM dbo.FVN_Slot AS s
JOIN dbo.FVN_Rack AS r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId AND r.IsActive = 1
JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId AND w.IsActive = 1
LEFT JOIN dbo.FVN_InventoryAreaSlot AS ar ON ar.SiteCode = s.SiteCode AND ar.SlotId = s.SlotId AND ar.IsActive = 1
LEFT JOIN dbo.FVN_SlotMesLocation AS sm ON sm.SiteCode = s.SiteCode AND sm.SlotId = s.SlotId AND sm.IsPrimary = 1 AND sm.IsActive = 1
LEFT JOIN MES.FVN_IfsInventoryLocation AS loc ON loc.SiteCode = sm.SiteCode AND loc.LocationNo = sm.LocationNo
WHERE s.IsActive = 1;
GO

/* ------------------------------------------------------------------ 7. Slot: area only; Location comes from the Kho */
ALTER PROCEDURE MES.usp_FVN_SlotMesLocation_Assign
    @SiteCode   nvarchar(30),
    @SlotId     int,
    @AreaCode   varchar(8),
    @LocationNo nvarchar(100) = NULL,   /* kept for old callers; if given it must equal the Location of the Slot's Kho */
    @ActorCode  nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @AreaCode = UPPER(LTRIM(RTRIM(ISNULL(@AreaCode, ''))));
    SET @LocationNo = NULLIF(LTRIM(RTRIM(ISNULL(@LocationNo, N''))), N'');
    IF @SiteCode = N'' OR @SlotId IS NULL THROW 57833, N'SiteCode và SlotId là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57834, N'ActorCode là bắt buộc.', 1;
    IF @AreaCode NOT IN ('PART', 'WIP') THROW 57835, N'Vùng kho phải là PART hoặc WIP.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
        THROW 57837, N'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;
    IF NOT EXISTS (SELECT 1 FROM MES.vw_FVN_SlotMesLocationStatus WHERE SiteCode = @SiteCode AND SlotId = @SlotId)
        THROW 57838, N'Slot không tồn tại, đang ngừng dùng hoặc thuộc Rack / Kho đang ngừng dùng.', 1;

    DECLARE @WarehouseId int, @WhLoc nvarchar(100);
    SELECT @WarehouseId = w.WarehouseId, @WhLoc = w.IfsLocationNo
    FROM dbo.FVN_Slot AS s
    JOIN dbo.FVN_Rack AS r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId
    JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId
    WHERE s.SiteCode = @SiteCode AND s.SlotId = @SlotId;
    IF @LocationNo IS NOT NULL AND (@WhLoc IS NULL OR @LocationNo <> @WhLoc)
        THROW 57877, N'Location IFS do Kho quyết định, không gán riêng cho Slot. Chọn Location cho Kho ở màn Map Kho PCTP ↔ Kho IFS.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        /* One slot belongs to exactly one area. */
        UPDATE dbo.FVN_InventoryAreaSlot WITH (UPDLOCK, HOLDLOCK)
           SET AreaCode = @AreaCode, IsActive = 1
         WHERE SiteCode = @SiteCode AND SlotId = @SlotId;
        IF @@ROWCOUNT = 0
            INSERT dbo.FVN_InventoryAreaSlot (SiteCode, SlotId, AreaCode, IsActive, CreatedBy)
            VALUES (@SiteCode, @SlotId, @AreaCode, 1, @ActorCode);

        /* Make sure the derived Location row of this Slot matches the Kho (covers Slots created before the Kho Location was chosen). */
        IF @WhLoc IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.FVN_SlotMesLocation WHERE SiteCode = @SiteCode AND SlotId = @SlotId AND LocationNo = @WhLoc AND IsPrimary = 1 AND IsActive = 1)
            EXEC MES.usp_FVN_Warehouse_SyncSlotLocations @SiteCode = @SiteCode, @WarehouseId = @WarehouseId, @ActorCode = @ActorCode;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

ALTER PROCEDURE MES.usp_FVN_SlotMesLocation_Clear
    @SiteCode  nvarchar(30),
    @SlotId    int,
    @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    IF @SiteCode = N'' OR @SlotId IS NULL THROW 57833, N'SiteCode và SlotId là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57834, N'ActorCode là bắt buộc.', 1;

    /* Only the AREA is removed; the Location belongs to the Kho and stays. */
    UPDATE dbo.FVN_InventoryAreaSlot SET IsActive = 0
     WHERE SiteCode = @SiteCode AND SlotId = @SlotId AND IsActive = 1;
END;
GO

/* ------------------------------------------------------------------ 8. new Slot inherits the Kho Location */
IF OBJECT_ID(N'dbo.TR_FVN_Slot_InheritWarehouseLocation', N'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.TR_FVN_Slot_InheritWarehouseLocation ON dbo.FVN_Slot AFTER INSERT AS RETURN;');
GO
ALTER TRIGGER dbo.TR_FVN_Slot_InheritWarehouseLocation
ON dbo.FVN_Slot
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.FVN_SlotMesLocation (SiteCode, SlotId, LocationNo, IsPrimary, IsActive, CreatedBy)
    SELECT i.SiteCode, i.SlotId, w.IfsLocationNo, 1, 1, N'TRIGGER:WAREHOUSE_LOCATION'
    FROM inserted AS i
    JOIN dbo.FVN_Rack AS r ON r.SiteCode = i.SiteCode AND r.RackId = i.RackId
    JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId AND w.IsActive = 1 AND w.IfsLocationNo IS NOT NULL
    WHERE i.IsActive = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.FVN_SlotMesLocation AS x WHERE x.SiteCode = i.SiteCode AND x.SlotId = i.SlotId AND x.LocationNo = w.IfsLocationNo);
END;
GO

/* ------------------------------------------------------------------ 9. hub: LOCATION stage is per Kho, not per IFS location */
ALTER VIEW MES.vw_FVN_IfsMapHub
AS
SELECT x.SiteCode, x.StageCode, x.StageOrder, x.TotalCount, x.MappedCount, x.TotalCount - x.MappedCount AS PendingCount, x.LastSyncUtc
FROM
(
    SELECT s.SiteCode, 'SITE' AS StageCode, 1 AS StageOrder, 1 AS TotalCount,
           CASE WHEN EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite p WHERE p.SiteCode = s.SiteCode AND p.IsActive = 1) THEN 1 ELSE 0 END AS MappedCount,
           s.SyncedAtUtc AS LastSyncUtc
    FROM IFS.FVN_Site s WHERE s.IsPresentInIfs = 1
    UNION ALL
    SELECT c.SiteCode, 'WORK_CENTER', 2, COUNT(*), SUM(CASE WHEN c.LotDepartmentStatus = 'LINKED' THEN 1 ELSE 0 END), NULL
    FROM MES.vw_FVN_WorkCenterCodeStatus c WHERE c.IsPresentInIfs = 1 GROUP BY c.SiteCode
    UNION ALL
    SELECT l.SiteCode, 'PRODUCTION_LINE', 3, COUNT(*), SUM(CASE WHEN l.CodeStatus = 'HAS_CODE' THEN 1 ELSE 0 END), NULL
    FROM MES.vw_FVN_IfsLineCodeStatus l GROUP BY l.SiteCode
    UNION ALL
    SELECT w.SiteCode, 'WAREHOUSE', 4, COUNT(*), SUM(CASE WHEN w.MapStatus = 'MAPPED' THEN 1 ELSE 0 END), NULL
    FROM (SELECT iw.SiteCode, CASE WHEN EXISTS (SELECT 1 FROM dbo.FVN_Warehouse f WHERE f.SiteCode = iw.SiteCode AND f.IfsWarehouseCode = iw.WarehouseCode AND f.IsActive = 1)
                                   THEN 'MAPPED' ELSE 'IFS_ONLY' END AS MapStatus
          FROM MES.FVN_IfsWarehouse iw WHERE iw.IsActive = 1) w
    GROUP BY w.SiteCode
    UNION ALL
    /* One IFS Location per Kho that is mapped to an IFS warehouse (IFS has no Rack / Slot, so Locations are not counted per Slot). */
    SELECT f.SiteCode, 'LOCATION', 5, COUNT(*), SUM(f.LocationOk), MAX(f.LastSyncUtc)
    FROM (SELECT w.SiteCode,
                 CASE WHEN w.IfsLocationNo IS NOT NULL AND EXISTS (SELECT 1 FROM MES.FVN_IfsInventoryLocation loc
                                                                   WHERE loc.SiteCode = w.SiteCode AND loc.LocationNo = w.IfsLocationNo AND loc.IsActive = 1
                                                                     AND loc.WarehouseCode = w.IfsWarehouseCode) THEN 1 ELSE 0 END AS LocationOk,
                 (SELECT MAX(loc2.SyncedAtUtc) FROM MES.FVN_IfsInventoryLocation loc2 WHERE loc2.SiteCode = w.SiteCode) AS LastSyncUtc
          FROM dbo.FVN_Warehouse w WHERE w.IsActive = 1 AND w.IfsWarehouseCode IS NOT NULL) f
    GROUP BY f.SiteCode
    UNION ALL
    SELECT v.SiteCode, 'SLOT', 6, COUNT(*), SUM(CASE WHEN v.MapStatus = 'READY' THEN 1 ELSE 0 END), NULL
    FROM MES.vw_FVN_SlotMesLocationStatus v GROUP BY v.SiteCode
) x;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '086')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('086', N'IFS Location chosen per Kho (warehouse); Slot keeps only its area, Location is a derived projection of the Kho');
GO
SELECT SiteCode, LocationMapStatus, COUNT(*) AS Warehouses FROM MES.vw_FVN_WarehouseIfsMapStatus WHERE WarehouseId IS NOT NULL GROUP BY SiteCode, LocationMapStatus ORDER BY SiteCode, LocationMapStatus;
SELECT SiteCode, WarehouseMapStatus, COUNT(*) AS Slots FROM MES.vw_FVN_SlotMesLocationStatus GROUP BY SiteCode, WarehouseMapStatus ORDER BY SiteCode, WarehouseMapStatus;
GO
