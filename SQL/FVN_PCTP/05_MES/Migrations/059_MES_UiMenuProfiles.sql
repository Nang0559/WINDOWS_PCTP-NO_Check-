/*
 Migration 059: UI Menu Designer (Bravo "Tùy chọn các mục trình đơn") - menu profiles.

 A menu profile is a tree of GROUP / ITEM nodes. An ITEM node stores only an ItemKey (the Name of a shell menu item
 that already exists in C#, or of a dynamic UI screen item). The shell resolves the key against the items it really
 has, silently drops unknown keys and empty groups, and never turns an item visible that RBAC hid. SQL therefore
 cannot add a navigation target, an event handler, a permission, or any SQL text to the menu: it only arranges and
 renames. A profile may be assigned to roles (like Bravo's per-department menu); a user with no matching role gets
 DEFAULT; an empty profile means the standard shell menu.

 Rights (ADMIN.DOCUMENT_DESIGNER): VIEW = open/list, EDIT = save a profile / create a profile.
 Runtime resolution (usp_FVN_UiMenuProfile_Runtime) needs only an active signed-in user.
 Change history is written to MES.FVN_UiScreenAudit with ScreenCode 'MENU:<ProfileCode>' (readable with 058).
 Apply after migration 058.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 59000, 'Migration 059 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 59001, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '058')
    THROW 59002, 'Migration 058 must be applied before 059.', 1;
IF OBJECT_ID(N'MES.FVN_UiScreenAudit', N'U') IS NULL OR OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NULL
    THROW 59003, 'UI layout objects are missing. Apply migration 056 first.', 1;
IF OBJECT_ID(N'SEC.Role', N'U') IS NULL OR OBJECT_ID(N'SEC.UserRole', N'U') IS NULL OR OBJECT_ID(N'SEC.UserAccount', N'U') IS NULL
    THROW 59004, 'Security tables are missing.', 1;
GO

/* ====================================================================== schema drift guard
 If a menu table with these names already exists but has other columns (an earlier draft, or a hand-made table),
 CREATE TABLE IF NOT EXISTS below would silently keep it and every procedure would fail with Msg 207 (invalid column).
 Quarantine instead: the three tables are renamed together, with their constraints and indexes, to *_Legacy_<UTC time>
 (nothing is deleted), and the tables are re-created with the structure this migration defines. Static sp_rename calls
 only; no dynamic SQL. Re-running 059 on a correct schema does nothing here.
*/
DECLARE @Expected TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL);
INSERT @Expected (TableName, ColumnName) VALUES
    (N'FVN_UiMenuProfile', N'ProfileCode'), (N'FVN_UiMenuProfile', N'DisplayName'), (N'FVN_UiMenuProfile', N'ConfigVersion'),
    (N'FVN_UiMenuProfile', N'IsActive'), (N'FVN_UiMenuProfile', N'UpdatedBy'), (N'FVN_UiMenuProfile', N'UpdatedAtUtc'),
    (N'FVN_UiMenuProfileRole', N'ProfileCode'), (N'FVN_UiMenuProfileRole', N'RoleCode'),
    (N'FVN_UiMenuNode', N'ProfileCode'), (N'FVN_UiMenuNode', N'NodeId'), (N'FVN_UiMenuNode', N'ParentNodeId'),
    (N'FVN_UiMenuNode', N'NodeType'), (N'FVN_UiMenuNode', N'ItemKey'), (N'FVN_UiMenuNode', N'Caption'),
    (N'FVN_UiMenuNode', N'SequenceNo');

IF EXISTS (SELECT 1 FROM @Expected e
           WHERE OBJECT_ID(N'MES.' + e.TableName, N'U') IS NOT NULL
             AND COL_LENGTH(N'MES.' + e.TableName, e.ColumnName) IS NULL)
BEGIN
    DECLARE @Suffix nvarchar(20) = N'_Legacy_' + FORMAT(SYSUTCDATETIME(), N'yyyyMMddHHmmss');
    DECLARE @Tbl sysname, @Obj sysname, @Old nvarchar(400), @New sysname;

    DECLARE tbl_cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT v.TableName FROM (VALUES (N'FVN_UiMenuNode', 1), (N'FVN_UiMenuProfileRole', 2), (N'FVN_UiMenuProfile', 3)) v(TableName, Ord)
        WHERE OBJECT_ID(N'MES.' + v.TableName, N'U') IS NOT NULL ORDER BY v.Ord;
    OPEN tbl_cur;
    FETCH NEXT FROM tbl_cur INTO @Tbl;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- stand-alone indexes (constraint-backed ones are renamed as objects below)
        DECLARE ix_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT i.name FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID(N'MES.' + @Tbl) AND i.name IS NOT NULL AND i.is_primary_key = 0 AND i.is_unique_constraint = 0;
        OPEN ix_cur;
        FETCH NEXT FROM ix_cur INTO @Obj;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @Old = N'MES.' + QUOTENAME(@Tbl) + N'.' + QUOTENAME(@Obj);
            SET @New = LEFT(@Obj, 100) + @Suffix;
            EXEC sys.sp_rename @Old, @New, N'INDEX';
            FETCH NEXT FROM ix_cur INTO @Obj;
        END;
        CLOSE ix_cur; DEALLOCATE ix_cur;

        -- PK / UNIQUE / FOREIGN KEY / CHECK / DEFAULT constraints owned by the table
        DECLARE con_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT o.name FROM sys.objects o
            WHERE o.parent_object_id = OBJECT_ID(N'MES.' + @Tbl) AND o.type IN ('PK', 'UQ', 'F', 'C', 'D');
        OPEN con_cur;
        FETCH NEXT FROM con_cur INTO @Obj;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @Old = N'MES.' + QUOTENAME(@Obj);
            SET @New = LEFT(@Obj, 100) + @Suffix;
            EXEC sys.sp_rename @Old, @New, N'OBJECT';
            FETCH NEXT FROM con_cur INTO @Obj;
        END;
        CLOSE con_cur; DEALLOCATE con_cur;

        SET @Old = N'MES.' + QUOTENAME(@Tbl);
        SET @New = LEFT(@Tbl, 100) + @Suffix;
        EXEC sys.sp_rename @Old, @New, N'OBJECT';
        FETCH NEXT FROM tbl_cur INTO @Tbl;
    END;
    CLOSE tbl_cur; DEALLOCATE tbl_cur;
    PRINT N'Migration 059: menu tables had a different structure; renamed to *' + @Suffix + N' and re-created.';
END;
GO

/* ====================================================================== tables */
IF OBJECT_ID(N'MES.FVN_UiMenuProfile', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiMenuProfile
    (
        ProfileCode   varchar(40)   NOT NULL,
        DisplayName   nvarchar(120) NOT NULL,
        ConfigVersion int           NOT NULL CONSTRAINT DF_MES_FVN_UiMenuProfile_Ver DEFAULT (1),
        IsActive      bit           NOT NULL CONSTRAINT DF_MES_FVN_UiMenuProfile_Active DEFAULT (1),
        UpdatedBy     nvarchar(100) NOT NULL,
        UpdatedAtUtc  datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_UiMenuProfile_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MES_FVN_UiMenuProfile PRIMARY KEY (ProfileCode),
        CONSTRAINT CK_MES_FVN_UiMenuProfile_Code CHECK (ProfileCode NOT LIKE '%[^A-Z0-9_]%')
    );
END;

IF OBJECT_ID(N'MES.FVN_UiMenuProfileRole', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiMenuProfileRole
    (
        ProfileCode varchar(40) NOT NULL,
        RoleCode    varchar(50) NOT NULL,
        CONSTRAINT PK_MES_FVN_UiMenuProfileRole PRIMARY KEY (ProfileCode, RoleCode),
        CONSTRAINT UQ_MES_FVN_UiMenuProfileRole_Role UNIQUE (RoleCode),   -- a role belongs to one profile: no ambiguity
        CONSTRAINT FK_MES_FVN_UiMenuProfileRole_Profile FOREIGN KEY (ProfileCode) REFERENCES MES.FVN_UiMenuProfile (ProfileCode)
    );
END;

IF OBJECT_ID(N'MES.FVN_UiMenuNode', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiMenuNode
    (
        ProfileCode  varchar(40)   NOT NULL,
        NodeId       int           NOT NULL,
        ParentNodeId int           NULL,
        NodeType     varchar(5)    NOT NULL,
        ItemKey      varchar(100)  NULL,      -- Name of an existing shell item; never a command, class or SQL
        Caption      nvarchar(160) NULL,      -- GROUP: required. ITEM: optional rename
        SequenceNo   int           NOT NULL,
        CONSTRAINT PK_MES_FVN_UiMenuNode PRIMARY KEY (ProfileCode, NodeId),
        CONSTRAINT FK_MES_FVN_UiMenuNode_Profile FOREIGN KEY (ProfileCode) REFERENCES MES.FVN_UiMenuProfile (ProfileCode),
        CONSTRAINT FK_MES_FVN_UiMenuNode_Parent FOREIGN KEY (ProfileCode, ParentNodeId) REFERENCES MES.FVN_UiMenuNode (ProfileCode, NodeId),
        CONSTRAINT CK_MES_FVN_UiMenuNode_Type CHECK (NodeType IN ('GROUP', 'ITEM')),
        CONSTRAINT CK_MES_FVN_UiMenuNode_Shape CHECK (
               (NodeType = 'GROUP' AND ItemKey IS NULL AND NULLIF(LTRIM(RTRIM(Caption)), N'') IS NOT NULL)
            OR (NodeType = 'ITEM'  AND ItemKey IS NOT NULL)),
        CONSTRAINT CK_MES_FVN_UiMenuNode_Key CHECK (ItemKey IS NULL OR ItemKey NOT LIKE '%[^A-Za-z0-9_]%')
    );
    CREATE UNIQUE INDEX UX_MES_FVN_UiMenuNode_Item ON MES.FVN_UiMenuNode (ProfileCode, ItemKey) WHERE ItemKey IS NOT NULL;
END;
GO

/* ====================================================================== list / roles / load (designer VIEW) */
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
           (SELECT COUNT(*) FROM MES.FVN_UiMenuProfileRole r WHERE r.ProfileCode = p.ProfileCode) AS RoleCount
    FROM MES.FVN_UiMenuProfile p
    WHERE p.IsActive = 1
    ORDER BY CASE WHEN p.ProfileCode = 'DEFAULT' THEN 0 ELSE 1 END, p.ProfileCode;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_ListRoles', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_ListRoles;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_ListRoles @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 59011, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    SELECT RoleCode, RoleName FROM SEC.Role WHERE IsActive = 1 ORDER BY RoleName, RoleCode;
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
    SELECT ProfileCode, DisplayName, ConfigVersion FROM MES.FVN_UiMenuProfile WHERE ProfileCode = @ProfileCode;
    SELECT NodeId, ParentNodeId, NodeType, ItemKey, Caption, SequenceNo
    FROM MES.FVN_UiMenuNode WHERE ProfileCode = @ProfileCode ORDER BY ParentNodeId, SequenceNo, NodeId;
    SELECT RoleCode FROM MES.FVN_UiMenuProfileRole WHERE ProfileCode = @ProfileCode ORDER BY RoleCode;
END;
GO

/* ====================================================================== create (designer EDIT) */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_Create', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_Create;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Create
    @ActorUserId bigint, @ActorCode nvarchar(100), @ProfileCode varchar(40), @DisplayName nvarchar(120)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 59020, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 59021, 'ActorCode is required.', 1;
    IF @ProfileCode IS NULL OR @ProfileCode = '' OR @ProfileCode LIKE '%[^A-Z0-9_]%'
        THROW 59022, 'ProfileCode must be upper-case letters, digits and underscore.', 1;
    IF NULLIF(LTRIM(RTRIM(@DisplayName)), N'') IS NULL THROW 59023, 'DisplayName is required.', 1;
    IF EXISTS (SELECT 1 FROM MES.FVN_UiMenuProfile WHERE ProfileCode = @ProfileCode) THROW 59024, 'Profile menu đã tồn tại.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        INSERT MES.FVN_UiMenuProfile (ProfileCode, DisplayName, UpdatedBy) VALUES (@ProfileCode, LTRIM(RTRIM(@DisplayName)), @ActorCode);
        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, VersionAfter)
        VALUES ('MENU:' + @ProfileCode, 'CREATE', @ActorUserId, @ActorCode, 1);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ====================================================================== save (designer EDIT): replace-all, versioned, audited */
IF OBJECT_ID(N'MES.usp_FVN_UiMenuProfile_Save', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiMenuProfile_Save;
GO
CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Save
    @ActorUserId bigint, @ActorCode nvarchar(100), @ProfileCode varchar(40), @ExpectedConfigVersion int,
    @NodesJson nvarchar(max), @RolesJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 59110, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 59111, 'ActorCode is required.', 1;
    IF ISJSON(@NodesJson) <> 1 OR ISJSON(@RolesJson) <> 1 THROW 59112, 'Payload must be valid JSON arrays.', 1;

    DECLARE @N TABLE (NodeId int NULL, ParentNodeId int NULL, NodeType varchar(20) NULL, ItemKey varchar(200) NULL,
                      Caption nvarchar(400) NULL, SequenceNo int NULL);
    INSERT @N
    SELECT NodeId, ParentNodeId, UPPER(LTRIM(RTRIM(NodeType))), NULLIF(LTRIM(RTRIM(ItemKey)), ''), NULLIF(LTRIM(RTRIM(Caption)), N''), SequenceNo
    FROM OPENJSON(@NodesJson) WITH (NodeId int '$.NodeId', ParentNodeId int '$.ParentNodeId', NodeType varchar(20) '$.NodeType',
                                    ItemKey varchar(200) '$.ItemKey', Caption nvarchar(400) '$.Caption', SequenceNo int '$.SequenceNo');
    DECLARE @R0 TABLE (RoleCode nvarchar(200) NOT NULL);
    INSERT @R0 SELECT DISTINCT LTRIM(RTRIM([value])) FROM OPENJSON(@RolesJson) WHERE [type] = 1;

    IF (SELECT COUNT(*) FROM @N) > 300 THROW 59120, 'Vượt giới hạn: tối đa 300 mục menu.', 1;
    IF EXISTS (SELECT 1 FROM @N WHERE NodeId IS NULL OR NodeId < 1 OR SequenceNo IS NULL)
       OR EXISTS (SELECT 1 FROM @N GROUP BY NodeId HAVING COUNT(*) > 1)
        THROW 59121, 'NodeId thiếu hoặc trùng trong payload.', 1;
    IF EXISTS (SELECT 1 FROM @N c WHERE c.ParentNodeId IS NOT NULL
                  AND (c.ParentNodeId = c.NodeId OR NOT EXISTS (SELECT 1 FROM @N p WHERE p.NodeId = c.ParentNodeId)))
        THROW 59122, 'Nút cha không tồn tại hoặc trỏ về chính nó.', 1;
    IF EXISTS (SELECT 1 FROM @N WHERE NodeType IS NULL OR NodeType NOT IN ('GROUP', 'ITEM')
                  OR (NodeType = 'GROUP' AND (ItemKey IS NOT NULL OR Caption IS NULL OR LEN(Caption) > 160))
                  OR (NodeType = 'ITEM' AND (ItemKey IS NULL OR LEN(ItemKey) > 100 OR ItemKey LIKE '%[^A-Za-z0-9_]%'
                                              OR (Caption IS NOT NULL AND LEN(Caption) > 160))))
        THROW 59123, 'Nhóm cần tiêu đề (<=160); mục cần ItemKey chỉ gồm chữ, số, gạch dưới (<=100).', 1;
    IF EXISTS (SELECT 1 FROM @N c JOIN @N p ON p.NodeId = c.ParentNodeId WHERE p.NodeType = 'ITEM')
        THROW 59124, 'Một mục menu không thể chứa mục con.', 1;
    IF EXISTS (SELECT 1 FROM @N WHERE ItemKey IS NOT NULL GROUP BY ItemKey HAVING COUNT(*) > 1)
        THROW 59125, 'Một mục menu chỉ được đặt một lần trong profile.', 1;

    /* depth <= 3 and no cycles: every node must be reachable from a root within 3 levels */
    DECLARE @T TABLE (NodeId int NOT NULL, Lvl int NOT NULL);
    ;WITH t AS (
        SELECT NodeId, 1 AS Lvl FROM @N WHERE ParentNodeId IS NULL
        UNION ALL
        SELECT c.NodeId, t.Lvl + 1 FROM @N c JOIN t ON c.ParentNodeId = t.NodeId WHERE t.Lvl < 4)
    INSERT @T SELECT NodeId, Lvl FROM t OPTION (MAXRECURSION 10);
    IF (SELECT COUNT(DISTINCT NodeId) FROM @T) <> (SELECT COUNT(*) FROM @N) OR EXISTS (SELECT 1 FROM @T WHERE Lvl > 3)
        THROW 59126, 'Cây menu vòng lặp hoặc sâu quá 3 cấp.', 1;

    IF EXISTS (SELECT 1 FROM @R0 x WHERE LEN(x.RoleCode) > 50
                  OR NOT EXISTS (SELECT 1 FROM SEC.Role r WHERE r.RoleCode = x.RoleCode AND r.IsActive = 1))
        THROW 59127, 'Vai trò không tồn tại hoặc đã tắt.', 1;
    IF @ProfileCode = 'DEFAULT' AND EXISTS (SELECT 1 FROM @R0)
        THROW 59128, 'Profile DEFAULT là menu dự phòng, không gán vai trò.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Version int;
        SELECT @Version = ConfigVersion FROM MES.FVN_UiMenuProfile WITH (UPDLOCK, HOLDLOCK)
        WHERE ProfileCode = @ProfileCode AND IsActive = 1;
        IF @Version IS NULL THROW 59130, 'Profile menu không tồn tại hoặc đã tắt.', 1;
        IF @Version <> @ExpectedConfigVersion
            THROW 59131, 'Menu đã bị người khác thay đổi (ConfigVersion). Hãy tải lại rồi sửa lại.', 1;
        IF EXISTS (SELECT 1 FROM MES.FVN_UiMenuProfileRole pr JOIN @R0 r ON r.RoleCode = pr.RoleCode WHERE pr.ProfileCode <> @ProfileCode)
            THROW 59129, 'Một vai trò chỉ thuộc một profile menu; vai trò này đã gán cho profile khác.', 1;

        DELETE FROM MES.FVN_UiMenuNode WHERE ProfileCode = @ProfileCode;
        DELETE FROM MES.FVN_UiMenuProfileRole WHERE ProfileCode = @ProfileCode;
        INSERT MES.FVN_UiMenuNode (ProfileCode, NodeId, ParentNodeId, NodeType, ItemKey, Caption, SequenceNo)
        SELECT @ProfileCode, NodeId, ParentNodeId, NodeType, ItemKey, Caption, SequenceNo FROM @N;
        INSERT MES.FVN_UiMenuProfileRole (ProfileCode, RoleCode)
        SELECT @ProfileCode, r.RoleCode FROM @R0 r;
        UPDATE MES.FVN_UiMenuProfile
           SET ConfigVersion = @Version + 1, UpdatedBy = @ActorCode, UpdatedAtUtc = SYSUTCDATETIME()
         WHERE ProfileCode = @ProfileCode;
        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, VersionBefore, VersionAfter, DetailJson)
        VALUES ('MENU:' + @ProfileCode, 'SAVE', @ActorUserId, @ActorCode, @Version, @Version + 1,
                (SELECT (SELECT COUNT(*) FROM @N) AS Nodes, (SELECT COUNT(*) FROM @R0) AS Roles
                 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
        COMMIT TRANSACTION;
        SELECT @Version + 1 AS ConfigVersion;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* ====================================================================== runtime (shell): only an active signed-in user */
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
         ORDER BY p.ProfileCode);
    IF @Profile IS NULL SET @Profile = 'DEFAULT';

    SELECT n.ProfileCode, n.NodeId, n.ParentNodeId, n.NodeType, n.ItemKey, n.Caption, n.SequenceNo
    FROM MES.FVN_UiMenuNode n
    JOIN MES.FVN_UiMenuProfile p ON p.ProfileCode = n.ProfileCode AND p.IsActive = 1
    WHERE n.ProfileCode = @Profile
    ORDER BY n.ParentNodeId, n.SequenceNo, n.NodeId;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_UiMenuProfile WHERE ProfileCode = 'DEFAULT')
        INSERT MES.FVN_UiMenuProfile (ProfileCode, DisplayName, UpdatedBy)
        VALUES ('DEFAULT', N'Menu mặc định (để trống = menu chuẩn của ứng dụng)', N'MIGRATION_059');
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '059')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('059', N'PCTP UI Menu Designer: menu profiles (tree of groups and existing shell items)');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
