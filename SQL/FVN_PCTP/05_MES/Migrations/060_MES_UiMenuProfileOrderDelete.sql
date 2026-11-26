/*
 Migration 060: UI Menu Designer - profile priority, delete profile, full screen catalogue.

 1. ProfileOrder: a user whose roles sit in several profiles gets the profile with the SMALLEST ProfileOrder
    (ties: ProfileCode). Replaces the implicit alphabetical rule of 059. Default 100.
 2. usp_FVN_UiMenuProfile_SetOrder: changes ProfileOrder (EDIT, versioned, audited).
 3. usp_FVN_UiMenuProfile_Delete: removes a profile with its nodes and role links (EDIT, versioned, audited; the audit row
    keeps a JSON copy so it can be rebuilt). DEFAULT cannot be deleted.
 4. usp_FVN_UiMenuProfile_ListScreens: every ACTIVE approved UI screen, regardless of the actor's VIEW rights on its data
    source, so the designer's left list no longer depends on who is designing. It only feeds the designer; the shell
    still creates a screen item only for users who may open that screen, and drops keys it does not have.
 Apply after migration 059.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 60000, 'Migration 060 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 60001, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '059')
    THROW 60002, 'Migration 059 must be applied before 060.', 1;
IF OBJECT_ID(N'MES.FVN_UiMenuProfile', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_UiScreen', N'U') IS NULL
    THROW 60003, 'Menu profile or UI screen tables are missing.', 1;
-- Msg 207 on DisplayName / NodeId / ParentNodeId means a menu table with another structure already exists. Stop here with
-- a clear instruction instead of creating procedures that cannot compile. Migration 059 (re-run) quarantines such tables.
IF COL_LENGTH(N'MES.FVN_UiMenuProfile', N'DisplayName') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuProfile', N'ConfigVersion') IS NULL
   OR OBJECT_ID(N'MES.FVN_UiMenuNode', N'U') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuNode', N'NodeId') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuNode', N'ParentNodeId') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuNode', N'SequenceNo') IS NULL
   OR OBJECT_ID(N'MES.FVN_UiMenuProfileRole', N'U') IS NULL
    THROW 60004, 'Menu tables do not match migration 059 (DisplayName/NodeId/ParentNodeId missing). Re-run 059_MES_UiMenuProfiles.sql (it renames the old tables to *_Legacy_<time> and re-creates them), then run 060 again.', 1;
GO
-- SSMS keeps running the batches after a failed batch; skip them all (restored by the last batch) so no 207 noise follows.
IF COL_LENGTH(N'MES.FVN_UiMenuProfile', N'DisplayName') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuProfile', N'ConfigVersion') IS NULL
   OR OBJECT_ID(N'MES.FVN_UiMenuNode', N'U') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuNode', N'NodeId') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuNode', N'ParentNodeId') IS NULL
   OR COL_LENGTH(N'MES.FVN_UiMenuNode', N'SequenceNo') IS NULL
   OR OBJECT_ID(N'MES.FVN_UiMenuProfileRole', N'U') IS NULL
    SET NOEXEC ON;
GO

IF COL_LENGTH(N'MES.FVN_UiMenuProfile', N'ProfileOrder') IS NULL
BEGIN
    ALTER TABLE MES.FVN_UiMenuProfile
        ADD ProfileOrder int NOT NULL CONSTRAINT DF_MES_FVN_UiMenuProfile_Order DEFAULT (100);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MES_FVN_UiMenuProfile_Order')
    ALTER TABLE MES.FVN_UiMenuProfile
        ADD CONSTRAINT CK_MES_FVN_UiMenuProfile_Order CHECK (ProfileOrder BETWEEN 1 AND 9999);
GO

/* ====================================================================== list / load: now return ProfileOrder */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_List', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_List;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_List @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 59010, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    SELECT p.ProfileCode, p.DisplayName, p.ConfigVersion, p.IsActive,
           (SELECT COUNT(*) FROM MES.FVN_UiMenuNode n WHERE n.ProfileCode = p.ProfileCode) AS NodeCount,
           (SELECT COUNT(*) FROM MES.FVN_UiMenuProfileRole r WHERE r.ProfileCode = p.ProfileCode) AS RoleCount,
           p.ProfileOrder
    FROM MES.FVN_UiMenuProfile p
    WHERE p.IsActive = 1
    ORDER BY CASE WHEN p.ProfileCode = 'DEFAULT' THEN 0 ELSE 1 END, p.ProfileOrder, p.ProfileCode;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_Load', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_Load;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Load @ActorUserId bigint, @ProfileCode varchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 59012, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_UiMenuProfile WHERE ProfileCode = @ProfileCode AND IsActive = 1)
        THROW 59013, 'Profile menu không tồn tại hoặc đã tắt.', 1;
    SELECT ProfileCode, DisplayName, ConfigVersion, ProfileOrder FROM MES.FVN_UiMenuProfile WHERE ProfileCode = @ProfileCode;
    SELECT NodeId, ParentNodeId, NodeType, ItemKey, Caption, SequenceNo
    FROM MES.FVN_UiMenuNode WHERE ProfileCode = @ProfileCode ORDER BY ParentNodeId, SequenceNo, NodeId;
    SELECT RoleCode FROM MES.FVN_UiMenuProfileRole WHERE ProfileCode = @ProfileCode ORDER BY RoleCode;
END;
GO

/* ====================================================================== runtime: explicit priority instead of alphabetical */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_Runtime', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_Runtime;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Runtime @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF @ActorUserId IS NULL OR NOT EXISTS (SELECT 1 FROM SEC.UserAccount
                                           WHERE UserId = @ActorUserId AND IsActive = 1
                                             AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= SYSUTCDATETIME()))
    BEGIN
        SELECT CAST(NULL AS varchar(40)) AS ProfileCode, CAST(NULL AS int) AS NodeId, CAST(NULL AS int) AS ParentNodeId,
               CAST(NULL AS varchar(5)) AS NodeType, CAST(NULL AS varchar(100)) AS ItemKey, CAST(NULL AS nvarchar(160)) AS Caption,
               CAST(NULL AS int) AS SequenceNo WHERE 1 = 0;
        RETURN;
    END;

    DECLARE @Profile varchar(40) =
        (SELECT TOP (1) p.ProfileCode
         FROM MES.FVN_UiMenuProfile p
         JOIN MES.FVN_UiMenuProfileRole pr ON pr.ProfileCode = p.ProfileCode
         JOIN SEC.Role r ON r.RoleCode = pr.RoleCode AND r.IsActive = 1
         JOIN SEC.UserRole ur ON ur.RoleId = r.RoleId AND ur.UserId = @ActorUserId
         WHERE p.IsActive = 1
         ORDER BY p.ProfileOrder, p.ProfileCode);
    IF @Profile IS NULL SET @Profile = 'DEFAULT';

    SELECT n.ProfileCode, n.NodeId, n.ParentNodeId, n.NodeType, n.ItemKey, n.Caption, n.SequenceNo
    FROM MES.FVN_UiMenuNode n
    JOIN MES.FVN_UiMenuProfile p ON p.ProfileCode = n.ProfileCode AND p.IsActive = 1
    WHERE n.ProfileCode = @Profile
    ORDER BY n.ParentNodeId, n.SequenceNo, n.NodeId;
END;
GO

/* ====================================================================== set order (designer EDIT) */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_SetOrder', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_SetOrder;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_SetOrder
    @ActorUserId bigint, @ActorCode nvarchar(100), @ProfileCode varchar(40), @ExpectedConfigVersion int, @ProfileOrder int
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 60110, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 60111, 'ActorCode is required.', 1;
    IF @ProfileOrder IS NULL OR @ProfileOrder < 1 OR @ProfileOrder > 9999
        THROW 60112, 'Thứ tự ưu tiên phải từ 1 đến 9999.', 1;
    IF @ProfileCode = 'DEFAULT' THROW 60113, 'Profile DEFAULT là menu dự phòng, không có thứ tự ưu tiên.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Version int, @OldOrder int;
        SELECT @Version = ConfigVersion, @OldOrder = ProfileOrder FROM MES.FVN_UiMenuProfile WITH (UPDLOCK, HOLDLOCK)
        WHERE ProfileCode = @ProfileCode AND IsActive = 1;
        IF @Version IS NULL THROW 59130, 'Profile menu không tồn tại hoặc đã tắt.', 1;
        IF @Version <> @ExpectedConfigVersion
            THROW 59131, 'Menu đã bị người khác thay đổi (ConfigVersion). Hãy tải lại rồi sửa lại.', 1;
        UPDATE MES.FVN_UiMenuProfile
           SET ProfileOrder = @ProfileOrder, ConfigVersion = @Version + 1, UpdatedBy = @ActorCode, UpdatedAtUtc = SYSUTCDATETIME()
         WHERE ProfileCode = @ProfileCode;
        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, VersionBefore, VersionAfter, DetailJson)
        VALUES ('MENU:' + @ProfileCode, 'SETORDER', @ActorUserId, @ActorCode, @Version, @Version + 1,
                (SELECT @OldOrder AS OldOrder, @ProfileOrder AS NewOrder FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
        COMMIT TRANSACTION;
        SELECT @Version + 1 AS ConfigVersion;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ====================================================================== delete profile (designer EDIT) */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_Delete', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_Delete;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Delete
    @ActorUserId bigint, @ActorCode nvarchar(100), @ProfileCode varchar(40), @ExpectedConfigVersion int
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 60120, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 60121, 'ActorCode is required.', 1;
    IF @ProfileCode = 'DEFAULT' THROW 60122, 'Không thể xóa profile DEFAULT (dùng "Về menu chuẩn" để làm rỗng).', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Version int;
        SELECT @Version = ConfigVersion FROM MES.FVN_UiMenuProfile WITH (UPDLOCK, HOLDLOCK)
        WHERE ProfileCode = @ProfileCode AND IsActive = 1;
        IF @Version IS NULL THROW 59130, 'Profile menu không tồn tại hoặc đã tắt.', 1;
        IF @Version <> @ExpectedConfigVersion
            THROW 59131, 'Menu đã bị người khác thay đổi (ConfigVersion). Hãy tải lại rồi xóa lại.', 1;

        -- keep a rebuildable copy in the audit trail before the rows disappear
        DECLARE @Copy nvarchar(max) =
            (SELECT (SELECT NodeId, ParentNodeId, NodeType, ItemKey, Caption, SequenceNo
                     FROM MES.FVN_UiMenuNode WHERE ProfileCode = @ProfileCode ORDER BY NodeId FOR JSON PATH) AS Nodes,
                    (SELECT RoleCode FROM MES.FVN_UiMenuProfileRole WHERE ProfileCode = @ProfileCode ORDER BY RoleCode FOR JSON PATH) AS Roles,
                    (SELECT DisplayName, ProfileOrder FROM MES.FVN_UiMenuProfile WHERE ProfileCode = @ProfileCode
                     FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS Profile
             FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

        DELETE FROM MES.FVN_UiMenuNode WHERE ProfileCode = @ProfileCode;
        DELETE FROM MES.FVN_UiMenuProfileRole WHERE ProfileCode = @ProfileCode;
        DELETE FROM MES.FVN_UiMenuProfile WHERE ProfileCode = @ProfileCode;
        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, VersionBefore, VersionAfter, DetailJson)
        VALUES ('MENU:' + @ProfileCode, 'DELETE', @ActorUserId, @ActorCode, @Version, NULL, @Copy);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ====================================================================== screen catalogue for the designer (VIEW) */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_ListScreens', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_ListScreens;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_ListScreens @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 60130, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    -- Same activity rules as usp_FVN_UiScreen_List (057) but WITHOUT the per-user VIEW filter: this list only offers
    -- keys to the designer. Who actually sees the item is still decided by the shell, per signed-in user.
    SELECT sc.ScreenCode, sc.Title
    FROM MES.FVN_UiScreen AS sc
    INNER JOIN MES.FVN_UiDataSource AS s ON s.SourceCode = sc.SourceCode AND s.IsActive = 1
    WHERE sc.IsActive = 1
    ORDER BY sc.Title, sc.ScreenCode;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '060')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('060', N'PCTP UI Menu Designer: profile priority, delete profile, full screen catalogue');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SET NOEXEC OFF;
GO
