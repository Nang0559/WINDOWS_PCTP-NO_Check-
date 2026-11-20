/*
 Migration 052: Document Designer - create a NEW read-only document and put it in the menu.

 Until 051 a document could only exist if it was registered in C# (layer 1). 052 adds a second, narrower
 way to get a document: an administrator with APPROVE on ADMIN.DOCUMENT_DESIGNER declares one over two
 EXISTING views named MES.vw_FVN_*. What this migration deliberately does NOT allow:
   - no command / stored procedure / workflow (a declared document has zero commands, WorkflowCode = NONE);
   - no editable field (every field is IsReadOnly = 1);
   - no free SQL: view names are validated against sys.views, columns come from sys.columns of the real view
     (never from the caller), the permission function must already exist in SEC.FunctionCatalog.
 Reads still go through the C# runtime, which re-checks VIEW + SITE data scope on the declared function code
 and validates the declaration against the real schema at startup (fail-closed).

 New objects:
   MES.FVN_DocumentDeclared                       one row per SQL-declared document (function, key, site column)
   MES.FVN_DocumentMenu                           menu placement of a declared document
   MES.fn_FVN_DocumentEditorForType               SQL type -> editor code (same mapping as the C# validator)
   MES.usp_FVN_DocumentDesigner_ListViews         VIEW    candidate views
   MES.usp_FVN_DocumentDesigner_ListViewColumns   VIEW    exposable columns of one view
   MES.usp_FVN_DocumentDesigner_ListFunctions     VIEW    permission codes that can guard a document
   MES.usp_FVN_DocumentDefinition_Create          APPROVE create document + fields + layouts + menu (one transaction)
   MES.usp_FVN_DocumentMenu_Save                  EDIT    caption / group / order / active of a menu entry

 SQL Server 2016 compatible. Apply after 051.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 55200, 'Migration 052 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 55201, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '051')
    THROW 55202, 'Migration 051 must be applied before 052.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentDefinition', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_DocumentField', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentLayout', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_DocumentConfigAudit', N'U') IS NULL
    THROW 55203, 'Document metadata tables are missing. Re-run migration 039.', 1;
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NULL OR OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NULL
    THROW 55204, 'SEC.FunctionCatalog or migration 051 permission function is missing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'MES.FVN_DocumentDeclared', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentDeclared
        (
            ModuleCode varchar(80) NOT NULL CONSTRAINT PK_FVN_DocumentDeclared PRIMARY KEY,
            ViewFunctionCode varchar(100) NOT NULL,
            KeyField sysname NOT NULL,
            SiteField sysname NOT NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocumentDeclared_Created DEFAULT(SYSUTCDATETIME()),
            CreatedBy nvarchar(100) NOT NULL,
            CONSTRAINT FK_FVN_DocumentDeclared_Definition FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDefinition(ModuleCode)
        );
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentMenu', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentMenu
        (
            ModuleCode varchar(80) NOT NULL CONSTRAINT PK_FVN_DocumentMenu PRIMARY KEY,
            MenuGroup varchar(20) NOT NULL,
            MenuText nvarchar(160) NOT NULL,
            SequenceNo int NOT NULL CONSTRAINT DF_FVN_DocumentMenu_Sequence DEFAULT(100),
            IsActive bit NOT NULL CONSTRAINT DF_FVN_DocumentMenu_Active DEFAULT(1),
            UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocumentMenu_Updated DEFAULT(SYSUTCDATETIME()),
            UpdatedBy nvarchar(100) NOT NULL,
            CONSTRAINT FK_FVN_DocumentMenu_Declared FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDeclared(ModuleCode),
            CONSTRAINT CK_FVN_DocumentMenu_Group CHECK(MenuGroup IN ('NHAPKHO', 'KHO', 'GIAOHANG', 'BAOCAO', 'CONGCU')),
            CONSTRAINT CK_FVN_DocumentMenu_Sequence CHECK(SequenceNo BETWEEN 0 AND 10000)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '052')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('052', N'Document Designer: create read-only documents over approved views and place them in the menu');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ---------- SQL type -> editor code. Must stay identical to DocumentDefinitionValidator.IsCompatible. ---------- */
IF OBJECT_ID(N'MES.fn_FVN_DocumentEditorForType', N'FN') IS NOT NULL
    DROP FUNCTION MES.fn_FVN_DocumentEditorForType;
GO
CREATE FUNCTION MES.fn_FVN_DocumentEditorForType(@TypeName sysname)
RETURNS varchar(40)
AS
BEGIN
    RETURN CASE
        WHEN @TypeName IN (N'tinyint', N'smallint', N'int', N'bigint', N'decimal', N'numeric', N'float', N'real', N'money', N'smallmoney') THEN 'NUMBER'
        WHEN @TypeName = N'date' THEN 'DATE'
        WHEN @TypeName IN (N'datetime', N'datetime2', N'smalldatetime', N'datetimeoffset') THEN 'DATETIME'
        WHEN @TypeName = N'bit' THEN 'CHECK'
        WHEN @TypeName IN (N'char', N'varchar', N'nchar', N'nvarchar', N'text', N'ntext') THEN 'TEXT'
        ELSE NULL END;
END;
GO

/* ---------- Candidate views (VIEW) ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentDesigner_ListViews', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentDesigner_ListViews;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentDesigner_ListViews
    @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 55210, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;

    SELECT N'MES.' + v.name AS ViewName,
           (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = v.object_id) AS ColumnCount
    FROM sys.views v
    JOIN sys.schemas s ON s.schema_id = v.schema_id
    WHERE s.name = N'MES' AND v.name LIKE N'vw[_]FVN[_]%'
    ORDER BY v.name;
END;
GO

/* ---------- Columns a view can expose (VIEW) ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentDesigner_ListViewColumns', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentDesigner_ListViewColumns;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentDesigner_ListViewColumns
    @ActorUserId bigint,
    @ViewName sysname
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 55211, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF @ViewName IS NULL OR @ViewName NOT LIKE 'MES.vw[_]FVN[_]%'
       OR @ViewName COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_.]%'
        THROW 55212, 'Document views must be MES.vw_FVN_* objects.', 1;
    IF ISNULL(OBJECTPROPERTY(OBJECT_ID(@ViewName), 'IsView'), 0) <> 1
        THROW 55213, 'View does not exist.', 1;

    SELECT c.name AS ColumnName, t.name AS TypeName, MES.fn_FVN_DocumentEditorForType(t.name) AS EditorCode
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(@ViewName)
      AND MES.fn_FVN_DocumentEditorForType(t.name) IS NOT NULL
      AND c.name COLLATE Latin1_General_BIN NOT LIKE '%[^A-Za-z0-9_]%'
      AND c.name NOT LIKE '[0-9]%'
    ORDER BY c.column_id;
END;
GO

/* ---------- Permission codes that can guard a document (VIEW) ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentDesigner_ListFunctions', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentDesigner_ListFunctions;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentDesigner_ListFunctions
    @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 55214, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;

    SELECT FunctionCode, FunctionName
    FROM SEC.FunctionCatalog
    WHERE IsActive = 1
    ORDER BY ModuleCode, SortOrder, FunctionCode;
END;
GO

/* ---------- Create a read-only document + its menu entry (APPROVE) ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentDefinition_Create', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentDefinition_Create;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentDefinition_Create
    @ModuleCode varchar(80),
    @DisplayName nvarchar(160),
    @HeaderView sysname,
    @DetailView sysname,
    @ViewFunctionCode varchar(100),
    @KeyField sysname,
    @SiteField sysname,
    @MenuGroup varchar(20),
    @MenuText nvarchar(160),
    @ActorUserId bigint,
    @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'APPROVE') = 0
        THROW 55220, 'Không có quyền APPROVE trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 55221, 'ActorCode is required.', 1;
    IF @ModuleCode IS NULL OR LEN(@ModuleCode) = 0 OR LEN(@ModuleCode) > 80
       OR @ModuleCode COLLATE Latin1_General_BIN LIKE '%[^A-Z0-9_]%'
        THROW 55222, 'ModuleCode must be 1-80 upper-case letters, digits and underscore.', 1;
    IF @HeaderView IS NULL OR @DetailView IS NULL
       OR @HeaderView NOT LIKE 'MES.vw[_]FVN[_]%' OR @DetailView NOT LIKE 'MES.vw[_]FVN[_]%'
       OR @HeaderView COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_.]%' OR @DetailView COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_.]%'
        THROW 55223, 'Document views must be MES.vw_FVN_* objects.', 1;
    IF ISNULL(OBJECTPROPERTY(OBJECT_ID(@HeaderView), 'IsView'), 0) <> 1 OR ISNULL(OBJECTPROPERTY(OBJECT_ID(@DetailView), 'IsView'), 0) <> 1
        THROW 55224, 'Header/detail view does not exist as a view.', 1;
    IF NULLIF(LTRIM(RTRIM(@DisplayName)), N'') IS NULL OR LEN(LTRIM(RTRIM(@DisplayName))) > 160
       OR CHARINDEX(CHAR(9), @DisplayName) > 0 OR CHARINDEX(CHAR(10), @DisplayName) > 0 OR CHARINDEX(CHAR(13), @DisplayName) > 0
       OR NULLIF(LTRIM(RTRIM(@MenuText)), N'') IS NULL OR LEN(LTRIM(RTRIM(@MenuText))) > 160
       OR CHARINDEX(CHAR(9), @MenuText) > 0 OR CHARINDEX(CHAR(10), @MenuText) > 0 OR CHARINDEX(CHAR(13), @MenuText) > 0
        THROW 55225, 'DisplayName và MenuText là bắt buộc, tối đa 160 ký tự, không xuống dòng.', 1;
    IF @MenuGroup IS NULL OR @MenuGroup NOT IN ('NHAPKHO', 'KHO', 'GIAOHANG', 'BAOCAO', 'CONGCU')
        THROW 55226, 'MenuGroup phải là NHAPKHO, KHO, GIAOHANG, BAOCAO hoặc CONGCU.', 1;
    IF @ViewFunctionCode IS NULL OR @ViewFunctionCode COLLATE Latin1_General_BIN LIKE '%[^A-Z0-9_.]%'
       OR NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = @ViewFunctionCode AND IsActive = 1)
        THROW 55227, 'ViewFunctionCode phải là mã chức năng đang hoạt động trong SEC.FunctionCatalog.', 1;
    IF @KeyField IS NULL OR @SiteField IS NULL
       OR @KeyField COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_]%' OR @SiteField COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_]%'
       OR @KeyField LIKE '[0-9]%' OR @SiteField LIKE '[0-9]%'
        THROW 55228, 'KeyField / SiteField must be plain column names.', 1;

    -- Exposable columns come from the real views, never from the caller.
    DECLARE @F TABLE (RegionCode varchar(30) NOT NULL, FieldCode varchar(100) NOT NULL, EditorCode varchar(40) NOT NULL, SequenceNo int NOT NULL);
    INSERT @F(RegionCode, FieldCode, EditorCode, SequenceNo)
    SELECT 'HEADER', c.name, MES.fn_FVN_DocumentEditorForType(t.name), ROW_NUMBER() OVER (ORDER BY c.column_id) * 10
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(@HeaderView)
      AND MES.fn_FVN_DocumentEditorForType(t.name) IS NOT NULL
      AND c.name COLLATE Latin1_General_BIN NOT LIKE '%[^A-Za-z0-9_]%' AND c.name NOT LIKE '[0-9]%' AND LEN(c.name) <= 100;
    INSERT @F(RegionCode, FieldCode, EditorCode, SequenceNo)
    SELECT 'DETAIL', c.name, MES.fn_FVN_DocumentEditorForType(t.name), ROW_NUMBER() OVER (ORDER BY c.column_id) * 10
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(@DetailView)
      AND MES.fn_FVN_DocumentEditorForType(t.name) IS NOT NULL
      AND c.name COLLATE Latin1_General_BIN NOT LIKE '%[^A-Za-z0-9_]%' AND c.name NOT LIKE '[0-9]%' AND LEN(c.name) <= 100;

    IF NOT EXISTS (SELECT 1 FROM @F WHERE RegionCode = 'HEADER') OR NOT EXISTS (SELECT 1 FROM @F WHERE RegionCode = 'DETAIL')
        THROW 55229, 'Mỗi view phải có ít nhất một cột hiển thị được.', 1;
    IF (SELECT COUNT(*) FROM @F WHERE RegionCode = 'HEADER') > 200 OR (SELECT COUNT(*) FROM @F WHERE RegionCode = 'DETAIL') > 200
        THROW 55230, 'Mỗi view tối đa 200 cột.', 1;
    IF NOT EXISTS (SELECT 1 FROM @F WHERE RegionCode = 'HEADER' AND FieldCode = @KeyField)
       OR NOT EXISTS (SELECT 1 FROM @F WHERE RegionCode = 'DETAIL' AND FieldCode = @KeyField)
       OR NOT EXISTS (SELECT 1 FROM @F WHERE RegionCode = 'HEADER' AND FieldCode = @SiteField)
       OR NOT EXISTS (SELECT 1 FROM @F WHERE RegionCode = 'DETAIL' AND FieldCode = @SiteField)
        THROW 55231, 'KeyField và SiteField phải là cột có trong CẢ header view và detail view.', 1;

    DECLARE @Now datetime2(3) = SYSUTCDATETIME(), @FieldCount int;
    SELECT @FieldCount = COUNT(*) FROM @F;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM MES.FVN_DocumentDefinition WITH (UPDLOCK, HOLDLOCK) WHERE ModuleCode = @ModuleCode)
            THROW 55232, 'Mã chứng từ này đã tồn tại.', 1;

        INSERT MES.FVN_DocumentDefinition(ModuleCode, HeaderView, DetailView, WorkflowCode, DisplayName, UpdatedBy)
        VALUES (@ModuleCode, @HeaderView, @DetailView, 'NONE', LTRIM(RTRIM(@DisplayName)), @ActorCode);

        INSERT MES.FVN_DocumentField(ModuleCode, FieldCode, RegionCode, Caption, SequenceNo, EditorCode, IsVisible, IsReadOnly)
        SELECT @ModuleCode, f.FieldCode, f.RegionCode, f.FieldCode, f.SequenceNo, f.EditorCode, 1, 1
        FROM @F f;

        INSERT MES.FVN_DocumentLayout(ModuleCode, LayoutCode, RegionCode, Caption, SequenceNo, IsVisible, IsReadOnly, UpdatedBy)
        SELECT @ModuleCode, v.LayoutCode, v.LayoutCode, LTRIM(RTRIM(@DisplayName)), v.SequenceNo, 1, 1, @ActorCode
        FROM (VALUES ('HEADER', 10), ('DETAIL', 20)) v(LayoutCode, SequenceNo);

        INSERT MES.FVN_DocumentDeclared(ModuleCode, ViewFunctionCode, KeyField, SiteField, CreatedBy)
        VALUES (@ModuleCode, @ViewFunctionCode, @KeyField, @SiteField, @ActorCode);

        INSERT MES.FVN_DocumentMenu(ModuleCode, MenuGroup, MenuText, SequenceNo, IsActive, UpdatedBy)
        VALUES (@ModuleCode, @MenuGroup, LTRIM(RTRIM(@MenuText)), 100, 1, @ActorCode);

        INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, AfterValue, ActorCode)
        VALUES (@ModuleCode, 'DEFINITION', @ModuleCode, 'CREATE',
                (SELECT @HeaderView AS HeaderView, @DetailView AS DetailView, @ViewFunctionCode AS ViewFunctionCode,
                        @KeyField AS KeyField, @SiteField AS SiteField, @FieldCount AS FieldCount
                 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                @ActorCode);
        INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, AfterValue, ActorCode)
        VALUES (@ModuleCode, 'MENU', @ModuleCode, 'CREATE',
                (SELECT @MenuGroup AS MenuGroup, LTRIM(RTRIM(@MenuText)) AS MenuText, CAST(100 AS int) AS SequenceNo, CAST(1 AS bit) AS IsActive
                 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                @ActorCode);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT CAST(1 AS int) AS ConfigVersion, @FieldCount AS ChangedCount;
END;
GO

/* ---------- Menu entry of a declared document (EDIT): text, group, order, active ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentMenu_Save', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentMenu_Save;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentMenu_Save
    @ModuleCode varchar(80),
    @MenuGroup varchar(20),
    @MenuText nvarchar(160),
    @SequenceNo int,
    @IsActive bit,
    @ActorUserId bigint,
    @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 55240, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 55241, 'ActorCode is required.', 1;
    IF @MenuGroup IS NULL OR @MenuGroup NOT IN ('NHAPKHO', 'KHO', 'GIAOHANG', 'BAOCAO', 'CONGCU')
        THROW 55242, 'MenuGroup phải là NHAPKHO, KHO, GIAOHANG, BAOCAO hoặc CONGCU.', 1;
    IF NULLIF(LTRIM(RTRIM(@MenuText)), N'') IS NULL OR LEN(LTRIM(RTRIM(@MenuText))) > 160
       OR CHARINDEX(CHAR(9), @MenuText) > 0 OR CHARINDEX(CHAR(10), @MenuText) > 0 OR CHARINDEX(CHAR(13), @MenuText) > 0
        THROW 55243, 'MenuText là bắt buộc, tối đa 160 ký tự, không xuống dòng.', 1;
    IF @SequenceNo IS NULL OR @SequenceNo NOT BETWEEN 0 AND 10000 OR @IsActive IS NULL
        THROW 55244, 'Invalid menu order or active flag.', 1;

    DECLARE @Changed int = 0, @Version int;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @OldGroup varchar(20), @OldText nvarchar(160), @OldSeq int, @OldActive bit;
        SELECT @OldGroup = MenuGroup, @OldText = MenuText, @OldSeq = SequenceNo, @OldActive = IsActive
        FROM MES.FVN_DocumentMenu WITH (UPDLOCK, HOLDLOCK) WHERE ModuleCode = @ModuleCode;
        IF @OldGroup IS NULL THROW 55245, 'Chứng từ này không có mục menu (chỉ chứng từ khai báo bằng Designer mới có).', 1;

        IF @OldGroup <> @MenuGroup OR @OldText <> LTRIM(RTRIM(@MenuText)) OR @OldSeq <> @SequenceNo OR @OldActive <> @IsActive
        BEGIN
            UPDATE MES.FVN_DocumentMenu
            SET MenuGroup = @MenuGroup, MenuText = LTRIM(RTRIM(@MenuText)), SequenceNo = @SequenceNo,
                IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorCode
            WHERE ModuleCode = @ModuleCode;
            SET @Changed = 1;
            INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, BeforeValue, AfterValue, ActorCode)
            VALUES (@ModuleCode, 'MENU', @ModuleCode, 'UPDATE',
                    (SELECT @OldGroup AS MenuGroup, @OldText AS MenuText, @OldSeq AS SequenceNo, @OldActive AS IsActive
                     FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                    (SELECT @MenuGroup AS MenuGroup, LTRIM(RTRIM(@MenuText)) AS MenuText, @SequenceNo AS SequenceNo, @IsActive AS IsActive
                     FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                    @ActorCode);
        END;

        SELECT @Version = ConfigVersion FROM MES.FVN_DocumentDefinition WHERE ModuleCode = @ModuleCode;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT ISNULL(@Version, 1) AS ConfigVersion, @Changed AS ChangedCount;
END;
GO