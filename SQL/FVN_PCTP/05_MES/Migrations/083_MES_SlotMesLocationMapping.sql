/*
 Migration 083: UI-backed mapping of warehouse slots to MES locations (IFS inventory locations) and inventory areas (PART / WIP).

 Receiving components (Kiểm kê hàng về), stock lookup and Part -> WIP issue only see a slot when ALL of these exist:
   dbo.FVN_Slot (active, active rack and warehouse)  +  dbo.FVN_InventoryAreaSlot (AreaCode PART or WIP)
   +  dbo.FVN_SlotMesLocation (primary, active)  ->  MES.FVN_IfsInventoryLocation (synchronized from IFS, active).
 Until now nothing in the application wrote the last two tables, so a Site such as VN2W showed
 "chưa có Slot PART hoạt động được map sang MES Location" with no screen to fix it.

 Adds:
   - MES.vw_FVN_SlotMesLocationStatus : every active slot with its area, primary MES location and MapStatus
                                        (READY / NO_AREA / NO_LOCATION / LOCATION_INACTIVE).
   - MES.usp_FVN_SlotMesLocation_Assign : sets the area and the single primary location of one slot (the location must exist and be active in IFS).
   - MES.usp_FVN_SlotMesLocation_Clear  : deactivates the area and primary location of one slot (history rows are kept).
   - Function WAREHOUSE.SLOT_MES_LOCATION (VIEW / EDIT, by Site), granted to SYSTEM_ADMIN and carried forward from MES.SITE_DEPARTMENT_LINE.
 Written with stub + ALTER like 080 / 081. Apply after 082.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57830, 'Migration 083 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '082')
    THROW 57831, 'Migration 082 must be registered before 083.', 1;
IF OBJECT_ID(N'dbo.FVN_SlotMesLocation', N'U') IS NULL OR OBJECT_ID(N'dbo.FVN_InventoryAreaSlot', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_IfsInventoryLocation', N'U') IS NULL
    THROW 57832, 'Slot / area / IFS location tables are missing (migrations 014 / 033).', 1;
GO

SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'MES.vw_FVN_SlotMesLocationStatus', N'V') IS NULL EXEC(N'CREATE VIEW MES.vw_FVN_SlotMesLocationStatus AS SELECT 1 AS X;');
GO
ALTER VIEW MES.vw_FVN_SlotMesLocationStatus
AS
SELECT s.SiteCode, s.SlotId, w.Name AS WarehouseName, r.RackName, s.SlotNumber,
       ar.AreaCode,
       sm.LocationNo, loc.WarehouseCode AS LocationWarehouseCode, loc.LocationGroupCode, loc.Description AS LocationDescription,
       CASE WHEN ar.AreaCode IS NULL THEN 'NO_AREA'
            WHEN sm.LocationNo IS NULL THEN 'NO_LOCATION'
            WHEN loc.LocationNo IS NULL OR loc.IsActive = 0 THEN 'LOCATION_INACTIVE'
            ELSE 'READY' END AS MapStatus
FROM dbo.FVN_Slot AS s
JOIN dbo.FVN_Rack AS r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId AND r.IsActive = 1
JOIN dbo.FVN_Warehouse AS w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId AND w.IsActive = 1
LEFT JOIN dbo.FVN_InventoryAreaSlot AS ar ON ar.SiteCode = s.SiteCode AND ar.SlotId = s.SlotId AND ar.IsActive = 1
LEFT JOIN dbo.FVN_SlotMesLocation AS sm ON sm.SiteCode = s.SiteCode AND sm.SlotId = s.SlotId AND sm.IsPrimary = 1 AND sm.IsActive = 1
LEFT JOIN MES.FVN_IfsInventoryLocation AS loc ON loc.SiteCode = sm.SiteCode AND loc.LocationNo = sm.LocationNo
WHERE s.IsActive = 1;
GO

IF OBJECT_ID(N'MES.usp_FVN_SlotMesLocation_Assign', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_SlotMesLocation_Assign AS RETURN 0;');
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

IF OBJECT_ID(N'MES.usp_FVN_SlotMesLocation_Clear', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_SlotMesLocation_Clear AS RETURN 0;');
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

    BEGIN TRY
        BEGIN TRANSACTION;
        UPDATE dbo.FVN_SlotMesLocation SET IsPrimary = 0, IsActive = 0
         WHERE SiteCode = @SiteCode AND SlotId = @SlotId AND (IsPrimary = 1 OR IsActive = 1);
        UPDATE dbo.FVN_InventoryAreaSlot SET IsActive = 0
         WHERE SiteCode = @SiteCode AND SlotId = @SlotId AND IsActive = 1;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ----------------------------------------------------------------------- function permission */
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NOT NULL AND OBJECT_ID(N'SEC.RoleFunctionPermission', N'U') IS NOT NULL
BEGIN
BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'WAREHOUSE.SLOT_MES_LOCATION')
        INSERT SEC.FunctionCatalog (FunctionCode, ModuleCode, FunctionName, ParentFunctionCode, SortOrder, IsActive)
        VALUES ('WAREHOUSE.SLOT_MES_LOCATION', 'WAREHOUSE', N'Map Slot kho ↔ MES Location (IFS) và vùng PART / WIP', 'WAREHOUSE.COMPONENT_STOCK', 66, 1);

    INSERT SEC.RoleFunctionPermission (RoleId, FunctionCode, ActionCode, IsAllowed)
    SELECT r.RoleId, 'WAREHOUSE.SLOT_MES_LOCATION', a.ActionCode, 1
    FROM SEC.Role r CROSS JOIN SEC.ActionCatalog a
    WHERE r.RoleCode = 'SYSTEM_ADMIN'
      AND NOT EXISTS (SELECT 1 FROM SEC.RoleFunctionPermission p
                      WHERE p.RoleId = r.RoleId AND p.FunctionCode = 'WAREHOUSE.SLOT_MES_LOCATION' AND p.ActionCode = a.ActionCode);

    /* Whoever already maintains the Site / line / department master data keeps working with the same Site scope. */
    INSERT SEC.RoleFunctionPermission (RoleId, FunctionCode, ActionCode, IsAllowed)
    SELECT p.RoleId, 'WAREHOUSE.SLOT_MES_LOCATION', p.ActionCode, p.IsAllowed
    FROM SEC.RoleFunctionPermission p
    WHERE p.FunctionCode = 'MES.SITE_DEPARTMENT_LINE' AND p.ActionCode IN ('VIEW', 'EDIT')
      AND NOT EXISTS (SELECT 1 FROM SEC.RoleFunctionPermission x
                      WHERE x.RoleId = p.RoleId AND x.FunctionCode = 'WAREHOUSE.SLOT_MES_LOCATION' AND x.ActionCode = p.ActionCode);

    INSERT SEC.RoleDataScope (RoleId, FunctionCode, ScopeType, ScopeValue, SiteCode, IsAllowed)
    SELECT s.RoleId, 'WAREHOUSE.SLOT_MES_LOCATION', s.ScopeType, s.ScopeValue, s.SiteCode, s.IsAllowed
    FROM SEC.RoleDataScope s
    WHERE s.FunctionCode = 'MES.SITE_DEPARTMENT_LINE'
      AND NOT EXISTS (SELECT 1 FROM SEC.RoleDataScope x
                      WHERE x.RoleId = s.RoleId AND x.FunctionCode = 'WAREHOUSE.SLOT_MES_LOCATION'
                        AND x.ScopeType = s.ScopeType AND x.ScopeValue = s.ScopeValue AND ISNULL(x.SiteCode, N'') = ISNULL(s.SiteCode, N''));

    INSERT SEC.UserDataScope (UserId, FunctionCode, ScopeType, ScopeValue, SiteCode, IsAllowed)
    SELECT s.UserId, 'WAREHOUSE.SLOT_MES_LOCATION', s.ScopeType, s.ScopeValue, s.SiteCode, s.IsAllowed
    FROM SEC.UserDataScope s
    WHERE s.FunctionCode = 'MES.SITE_DEPARTMENT_LINE'
      AND NOT EXISTS (SELECT 1 FROM SEC.UserDataScope x
                      WHERE x.UserId = s.UserId AND x.FunctionCode = 'WAREHOUSE.SLOT_MES_LOCATION'
                        AND x.ScopeType = s.ScopeType AND x.ScopeValue = s.ScopeValue AND ISNULL(x.SiteCode, N'') = ISNULL(s.SiteCode, N''));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '083')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('083', N'Slot to MES location and PART / WIP area mapping with UI procedures and function permission');
GO
SELECT SiteCode, MapStatus, COUNT(*) AS Slots FROM MES.vw_FVN_SlotMesLocationStatus GROUP BY SiteCode, MapStatus ORDER BY SiteCode, MapStatus;
GO
