/*
 Migration 085: IFS warehouse <-> dbo.FVN_Warehouse mapping, warehouse-aware slot / location mapping and the IFS Map Center hub.

 Why: IFS locations are synchronized into MES.FVN_IfsInventoryLocation (084) and IFS warehouses into MES.FVN_IfsWarehouse, but the physical
 Kho / Rack / Slot (dbo.FVN_Warehouse / FVN_Rack / FVN_Slot) knew nothing about them: Site was only a column copied by hand and no Slot could be
 traced to an IFS warehouse. This follows the same pattern already used for Site, Work Center and Production Line:
   IFS mirror (IFS.* / MES.FVN_Ifs*)  --map-->  PCTP master (dbo.FVN_*)

 Adds:
   - dbo.FVN_Warehouse.IfsWarehouseCode (FK to MES.FVN_IfsWarehouse by Site; one active PCTP warehouse per IFS warehouse).
   - MES.vw_FVN_WarehouseIfsMapStatus : every PCTP warehouse and every IFS warehouse with MapStatus
                                        MAPPED / NO_IFS_MAP / IFS_ONLY / IFS_INACTIVE and rack / slot / location counts.
   - MES.usp_FVN_Warehouse_MapIfs / _CreateFromIfs : map an existing Kho, or create the Kho from an IFS warehouse (Preview supported on _CreateFromIfs).
   - MES.usp_FVN_SlotMesLocation_Assign : refuses (57851) a Location whose IFS warehouse differs from the one mapped to the slot's Kho.
     Slots of an unmapped Kho keep working (READY logic of 083 is unchanged); the view below flags them.
   - MES.vw_FVN_SlotMesLocationStatus : two extra trailing columns WarehouseIfsCode / WarehouseMapStatus.
   - MES.vw_FVN_IfsMapHub : one row per Site and stage (SITE, WORK_CENTER, PRODUCTION_LINE, WAREHOUSE, LOCATION, SLOT) with Total / Mapped / Pending.
   - SEC functions WAREHOUSE.WAREHOUSE_IFS_MAP (VIEW / EDIT by Site) and MES.IFS_MAP_CENTER (VIEW), granted like 083.
 Nothing is deleted, no stock or slot row is modified. Written with stub + ALTER like 080 / 083. Apply after 084 (sqlcmd -b -I).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57850, 'Migration 085 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '084')
    THROW 57852, 'Migration 084 must be registered before 085.', 1;
IF OBJECT_ID(N'dbo.FVN_Warehouse', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_IfsWarehouse', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_IfsInventoryLocation', N'U') IS NULL OR OBJECT_ID(N'IFS.FVN_Site', N'U') IS NULL
    THROW 57853, 'Warehouse / IFS tables are missing (migrations 014, 027, 068, 084).', 1;
GO

/* ------------------------------------------------------------------ 1. column + key */
IF COL_LENGTH(N'dbo.FVN_Warehouse', N'IfsWarehouseCode') IS NULL
    ALTER TABLE dbo.FVN_Warehouse ADD IfsWarehouseCode nvarchar(60) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_Warehouse_IfsWarehouse')
    ALTER TABLE dbo.FVN_Warehouse WITH CHECK ADD CONSTRAINT FK_FVN_Warehouse_IfsWarehouse
        FOREIGN KEY (SiteCode, IfsWarehouseCode) REFERENCES MES.FVN_IfsWarehouse (SiteCode, WarehouseCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FVN_Warehouse_IfsWarehouse' AND object_id = OBJECT_ID(N'dbo.FVN_Warehouse'))
    CREATE UNIQUE INDEX UX_FVN_Warehouse_IfsWarehouse ON dbo.FVN_Warehouse (SiteCode, IfsWarehouseCode)
        WHERE IfsWarehouseCode IS NOT NULL AND IsActive = 1;
GO

/* ------------------------------------------------------------------ 2. warehouse map status */
IF OBJECT_ID(N'MES.vw_FVN_WarehouseIfsMapStatus', N'V') IS NULL EXEC(N'CREATE VIEW MES.vw_FVN_WarehouseIfsMapStatus AS SELECT 1 AS X;');
GO
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
)
SELECT w.SiteCode, w.WarehouseId, w.Name AS WarehouseName, w.IsActive AS WarehouseIsActive,
       w.IfsWarehouseCode, iw.Description AS IfsDescription,
       ISNULL(rs.RackCount, 0) AS RackCount, ISNULL(rs.SlotCount, 0) AS SlotCount, ISNULL(l.LocationCount, 0) AS IfsLocationCount,
       CASE WHEN w.IfsWarehouseCode IS NULL THEN 'NO_IFS_MAP'
            WHEN iw.IsActive = 0 THEN 'IFS_INACTIVE'
            ELSE 'MAPPED' END AS MapStatus
FROM dbo.FVN_Warehouse AS w
LEFT JOIN MES.FVN_IfsWarehouse AS iw ON iw.SiteCode = w.SiteCode AND iw.WarehouseCode = w.IfsWarehouseCode
LEFT JOIN RackSlot AS rs ON rs.SiteCode = w.SiteCode AND rs.WarehouseId = w.WarehouseId
LEFT JOIN Loc AS l ON l.SiteCode = w.SiteCode AND l.WarehouseCode = w.IfsWarehouseCode
WHERE w.IsActive = 1
UNION ALL
SELECT iw.SiteCode, NULL, NULL, NULL, iw.WarehouseCode, iw.Description, 0, 0, ISNULL(l.LocationCount, 0), 'IFS_ONLY'
FROM MES.FVN_IfsWarehouse AS iw
LEFT JOIN Loc AS l ON l.SiteCode = iw.SiteCode AND l.WarehouseCode = iw.WarehouseCode
WHERE iw.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.FVN_Warehouse AS w WHERE w.SiteCode = iw.SiteCode AND w.IfsWarehouseCode = iw.WarehouseCode AND w.IsActive = 1);
GO

/* ------------------------------------------------------------------ 3. map / create procedures */
IF OBJECT_ID(N'MES.usp_FVN_Warehouse_MapIfs', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_Warehouse_MapIfs AS RETURN 0;');
GO
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
        /* Slots already mapped to a location of another IFS warehouse would contradict the new map. */
        IF EXISTS (SELECT 1
                   FROM dbo.FVN_Rack r
                   JOIN dbo.FVN_Slot s ON s.SiteCode = r.SiteCode AND s.RackId = r.RackId
                   JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode = s.SiteCode AND sm.SlotId = s.SlotId AND sm.IsActive = 1 AND sm.IsPrimary = 1
                   JOIN MES.FVN_IfsInventoryLocation loc ON loc.SiteCode = sm.SiteCode AND loc.LocationNo = sm.LocationNo
                   WHERE r.SiteCode = @SiteCode AND r.WarehouseId = @WarehouseId AND loc.WarehouseCode <> @IfsWarehouseCode)
            THROW 57860, N'Kho này đang có Slot map sang Location của kho IFS khác. Gỡ map các Slot đó (hoặc chọn đúng kho IFS) trước.', 1;
    END;

    UPDATE dbo.FVN_Warehouse
       SET IfsWarehouseCode = @IfsWarehouseCode, ModifiedAtUtc = SYSUTCDATETIME(), ModifiedBy = @ActorCode
     WHERE SiteCode = @SiteCode AND WarehouseId = @WarehouseId;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_Warehouse_CreateFromIfs', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_Warehouse_CreateFromIfs AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_Warehouse_CreateFromIfs
    @SiteCode         nvarchar(30),
    @IfsWarehouseCode nvarchar(60) = NULL,   /* NULL = every IFS warehouse of the Site that has no PCTP warehouse yet */
    @ActorCode        nvarchar(100),
    @Preview          bit = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @IfsWarehouseCode = NULLIF(LTRIM(RTRIM(ISNULL(@IfsWarehouseCode, N''))), N'');
    IF @SiteCode = N'' THROW 57854, N'SiteCode là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57855, N'ActorCode là bắt buộc.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
        THROW 57856, N'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;

    DECLARE @New TABLE (IfsWarehouseCode nvarchar(60) PRIMARY KEY, Name nvarchar(200) NOT NULL, NameClash bit NOT NULL);
    INSERT @New (IfsWarehouseCode, Name, NameClash)
    SELECT iw.WarehouseCode,
           LEFT(ISNULL(NULLIF(LTRIM(RTRIM(iw.Description)), N''), iw.WarehouseCode), 200),
           CASE WHEN EXISTS (SELECT 1 FROM dbo.FVN_Warehouse w WHERE w.SiteCode = iw.SiteCode AND w.IsActive = 1
                             AND w.Name = LEFT(ISNULL(NULLIF(LTRIM(RTRIM(iw.Description)), N''), iw.WarehouseCode), 200)) THEN 1 ELSE 0 END
    FROM MES.FVN_IfsWarehouse iw
    WHERE iw.SiteCode = @SiteCode AND iw.IsActive = 1
      AND (@IfsWarehouseCode IS NULL OR iw.WarehouseCode = @IfsWarehouseCode)
      AND NOT EXISTS (SELECT 1 FROM dbo.FVN_Warehouse w WHERE w.SiteCode = iw.SiteCode AND w.IfsWarehouseCode = iw.WarehouseCode AND w.IsActive = 1);

    IF @IfsWarehouseCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM @New)
        THROW 57861, N'Kho IFS không có, đã ngừng, hoặc đã có Kho PCTP map sẵn.', 1;
    /* A name that already exists in the Site is not guessed: the code is appended, so two warehouses never look identical on screen. */
    UPDATE @New SET Name = LEFT(Name, 150) + N' (' + IfsWarehouseCode + N')' WHERE NameClash = 1;

    IF @Preview = 1
    BEGIN
        SELECT @SiteCode AS SiteCode, IfsWarehouseCode, Name AS NewWarehouseName, NameClash FROM @New ORDER BY IfsWarehouseCode;
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;
        INSERT dbo.FVN_Warehouse (SiteCode, Name, IsActive, CreatedBy, IfsWarehouseCode)
        SELECT @SiteCode, Name, 1, @ActorCode, IfsWarehouseCode FROM @New;
        COMMIT TRANSACTION;
        SELECT @SiteCode AS SiteCode, IfsWarehouseCode, Name AS NewWarehouseName, NameClash FROM @New ORDER BY IfsWarehouseCode;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ------------------------------------------------------------------ 4. slot status view (trailing columns) + assign with warehouse check */
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
            ELSE 'OK' END AS WarehouseMapStatus
FROM dbo.FVN_Slot AS s
JOIN dbo.FVN_Rack AS r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId AND r.IsActive = 1
JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId AND w.IsActive = 1
LEFT JOIN dbo.FVN_InventoryAreaSlot AS ar ON ar.SiteCode = s.SiteCode AND ar.SlotId = s.SlotId AND ar.IsActive = 1
LEFT JOIN dbo.FVN_SlotMesLocation AS sm ON sm.SiteCode = s.SiteCode AND sm.SlotId = s.SlotId AND sm.IsPrimary = 1 AND sm.IsActive = 1
LEFT JOIN MES.FVN_IfsInventoryLocation AS loc ON loc.SiteCode = sm.SiteCode AND loc.LocationNo = sm.LocationNo
WHERE s.IsActive = 1;
GO

ALTER PROCEDURE MES.usp_FVN_SlotMesLocation_Assign
    @SiteCode   nvarchar(30),
    @SlotId     int,
    @AreaCode   varchar(8),
    @LocationNo nvarchar(100),
    @ActorCode  nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @AreaCode = UPPER(LTRIM(RTRIM(ISNULL(@AreaCode, ''))));
    SET @LocationNo = LTRIM(RTRIM(ISNULL(@LocationNo, N'')));
    IF @SiteCode = N'' OR @SlotId IS NULL THROW 57833, N'SiteCode và SlotId là bắt buộc.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57834, N'ActorCode là bắt buộc.', 1;
    IF @AreaCode NOT IN ('PART', 'WIP') THROW 57835, N'Vùng kho phải là PART hoặc WIP.', 1;
    IF @LocationNo = N'' THROW 57836, N'Chọn MES Location (IFS) từ danh sách.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
        THROW 57837, N'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;
    IF NOT EXISTS (SELECT 1 FROM MES.vw_FVN_SlotMesLocationStatus WHERE SiteCode = @SiteCode AND SlotId = @SlotId)
        THROW 57838, N'Slot không tồn tại, đang ngừng dùng hoặc thuộc Rack / Kho đang ngừng dùng.', 1;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_IfsInventoryLocation WHERE SiteCode = @SiteCode AND LocationNo = @LocationNo AND IsActive = 1)
        THROW 57839, N'Location chưa có hoặc đã ngừng trong IFS của Site. Đồng bộ dữ liệu INVENTORY_LOCATION từ IFS rồi chọn lại.', 1;

    /* Warehouse map (085): when the slot's FVN_Warehouse is mapped to an IFS warehouse, the location must belong to that IFS warehouse. */
    DECLARE @WhIfs nvarchar(60), @LocWh nvarchar(60);
    SELECT @WhIfs = w.IfsWarehouseCode
    FROM dbo.FVN_Slot AS s
    JOIN dbo.FVN_Rack AS r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId
    JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId
    WHERE s.SiteCode = @SiteCode AND s.SlotId = @SlotId;
    SELECT @LocWh = WarehouseCode FROM MES.FVN_IfsInventoryLocation WHERE SiteCode = @SiteCode AND LocationNo = @LocationNo;
    IF @WhIfs IS NOT NULL AND @LocWh <> @WhIfs
        THROW 57851, N'Location thuộc kho IFS khác với kho IFS đã map cho Kho của Slot này. Chọn Location đúng kho hoặc đổi map Kho ↔ IFS.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        /* One slot belongs to exactly one area. */
        UPDATE dbo.FVN_InventoryAreaSlot WITH (UPDLOCK, HOLDLOCK)
           SET AreaCode = @AreaCode, IsActive = 1
         WHERE SiteCode = @SiteCode AND SlotId = @SlotId;
        IF @@ROWCOUNT = 0
            INSERT dbo.FVN_InventoryAreaSlot (SiteCode, SlotId, AreaCode, IsActive, CreatedBy)
            VALUES (@SiteCode, @SlotId, @AreaCode, 1, @ActorCode);

        /* One primary active location per slot (unique index UX_FVN_SlotMesLocation_Primary): retire the old one first, keep it as history. */
        UPDATE dbo.FVN_SlotMesLocation WITH (UPDLOCK, HOLDLOCK)
           SET IsPrimary = 0, IsActive = 0
         WHERE SiteCode = @SiteCode AND SlotId = @SlotId AND LocationNo <> @LocationNo AND (IsPrimary = 1 OR IsActive = 1);

        UPDATE dbo.FVN_SlotMesLocation
           SET IsPrimary = 1, IsActive = 1
         WHERE SiteCode = @SiteCode AND SlotId = @SlotId AND LocationNo = @LocationNo;
        IF @@ROWCOUNT = 0
            INSERT dbo.FVN_SlotMesLocation (SiteCode, SlotId, LocationNo, IsPrimary, IsActive, CreatedBy)
            VALUES (@SiteCode, @SlotId, @LocationNo, 1, 1, @ActorCode);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ------------------------------------------------------------------ 5. IFS Map Center hub */
IF OBJECT_ID(N'MES.vw_FVN_IfsMapHub', N'V') IS NULL EXEC(N'CREATE VIEW MES.vw_FVN_IfsMapHub AS SELECT 1 AS X;');
GO
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
    SELECT loc.SiteCode, 'LOCATION', 5, COUNT(*), SUM(CASE WHEN m.LocationNo IS NOT NULL THEN 1 ELSE 0 END), MAX(loc.SyncedAtUtc)
    FROM MES.FVN_IfsInventoryLocation loc
    LEFT JOIN (SELECT DISTINCT SiteCode, LocationNo FROM dbo.FVN_SlotMesLocation WHERE IsPrimary = 1 AND IsActive = 1) m
           ON m.SiteCode = loc.SiteCode AND m.LocationNo = loc.LocationNo
    WHERE loc.IsActive = 1 GROUP BY loc.SiteCode
    UNION ALL
    SELECT v.SiteCode, 'SLOT', 6, COUNT(*), SUM(CASE WHEN v.MapStatus = 'READY' THEN 1 ELSE 0 END), NULL
    FROM MES.vw_FVN_SlotMesLocationStatus v GROUP BY v.SiteCode
) x;
GO

/* ------------------------------------------------------------------ 6. function permissions */
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NOT NULL AND OBJECT_ID(N'SEC.RoleFunctionPermission', N'U') IS NOT NULL
BEGIN
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @F TABLE (FunctionCode varchar(100) PRIMARY KEY, FunctionName nvarchar(200), SortOrder int, SourceFunction varchar(100));
    INSERT @F VALUES
        ('WAREHOUSE.WAREHOUSE_IFS_MAP', N'Map Kho PCTP ↔ Kho IFS', 67, 'MES.SITE_DEPARTMENT_LINE'),
        ('MES.IFS_MAP_CENTER',          N'Trung tâm đồng bộ & Map IFS', 86, 'MES.SITE_DEPARTMENT_LINE');

    INSERT SEC.FunctionCatalog (FunctionCode, ModuleCode, FunctionName, ParentFunctionCode, SortOrder, IsActive)
    SELECT f.FunctionCode, CASE WHEN f.FunctionCode LIKE 'WAREHOUSE.%' THEN 'WAREHOUSE' ELSE 'MES' END, f.FunctionName, NULL, f.SortOrder, 1
    FROM @F f WHERE NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog c WHERE c.FunctionCode = f.FunctionCode);

    INSERT SEC.RoleFunctionPermission (RoleId, FunctionCode, ActionCode, IsAllowed)
    SELECT r.RoleId, f.FunctionCode, a.ActionCode, 1
    FROM SEC.Role r CROSS JOIN SEC.ActionCatalog a CROSS JOIN @F f
    WHERE r.RoleCode = 'SYSTEM_ADMIN'
      AND NOT EXISTS (SELECT 1 FROM SEC.RoleFunctionPermission p WHERE p.RoleId = r.RoleId AND p.FunctionCode = f.FunctionCode AND p.ActionCode = a.ActionCode);

    INSERT SEC.RoleFunctionPermission (RoleId, FunctionCode, ActionCode, IsAllowed)
    SELECT p.RoleId, f.FunctionCode, p.ActionCode, p.IsAllowed
    FROM @F f JOIN SEC.RoleFunctionPermission p ON p.FunctionCode = f.SourceFunction AND p.ActionCode IN ('VIEW', 'EDIT')
    WHERE NOT EXISTS (SELECT 1 FROM SEC.RoleFunctionPermission x WHERE x.RoleId = p.RoleId AND x.FunctionCode = f.FunctionCode AND x.ActionCode = p.ActionCode);

    INSERT SEC.RoleDataScope (RoleId, FunctionCode, ScopeType, ScopeValue, SiteCode, IsAllowed)
    SELECT s.RoleId, f.FunctionCode, s.ScopeType, s.ScopeValue, s.SiteCode, s.IsAllowed
    FROM @F f JOIN SEC.RoleDataScope s ON s.FunctionCode = f.SourceFunction
    WHERE NOT EXISTS (SELECT 1 FROM SEC.RoleDataScope x WHERE x.RoleId = s.RoleId AND x.FunctionCode = f.FunctionCode
                      AND x.ScopeType = s.ScopeType AND x.ScopeValue = s.ScopeValue AND ISNULL(x.SiteCode, N'') = ISNULL(s.SiteCode, N''));

    INSERT SEC.UserDataScope (UserId, FunctionCode, ScopeType, ScopeValue, SiteCode, IsAllowed)
    SELECT s.UserId, f.FunctionCode, s.ScopeType, s.ScopeValue, s.SiteCode, s.IsAllowed
    FROM @F f JOIN SEC.UserDataScope s ON s.FunctionCode = f.SourceFunction
    WHERE NOT EXISTS (SELECT 1 FROM SEC.UserDataScope x WHERE x.UserId = s.UserId AND x.FunctionCode = f.FunctionCode
                      AND x.ScopeType = s.ScopeType AND x.ScopeValue = s.ScopeValue AND ISNULL(x.SiteCode, N'') = ISNULL(s.SiteCode, N''));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '085')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('085', N'IFS warehouse to FVN_Warehouse map, warehouse-aware slot location map, IFS Map Center hub view');
GO
SELECT SiteCode, MapStatus, COUNT(*) AS Warehouses FROM MES.vw_FVN_WarehouseIfsMapStatus GROUP BY SiteCode, MapStatus ORDER BY SiteCode, MapStatus;
SELECT SiteCode, StageCode, TotalCount, MappedCount, PendingCount FROM MES.vw_FVN_IfsMapHub ORDER BY SiteCode, StageOrder;
GO