/*
 Migration 056: PCTP UI Layout Designer (Bravo-style "Khai báo cơ sở dữ liệu" + "Khai báo layout", without XML).

 Two separate declarations, as in Bravo:
   1. DATA SOURCE (MES.FVN_UiDataSource): an APPROVED read-only view. The permission code that guards reading it
      belongs to this row and is set by a user holding APPROVE on ADMIN.DOCUMENT_DESIGNER - the layout author cannot pick it.
      The Site column is mandatory, so every runtime read is Site-scoped (fail-closed).
   2. LAYOUT (MES.FVN_UiScreen + Column / Child / Filter): structured rows, validated against sys.columns of the approved
      view on every save. There is NO SQL text, expression, procedure name, DLL or class name anywhere in a layout.
      Quick filters (Bravo "navigator") are column + operator + (closed token list | literal value) - never typed SQL.

 A layout screen is READ-ONLY. Commands (procedures) remain registered in C# (Trusted Registry), exactly as for 052/053.
 SQL Server 2016 compatible. Apply after 055. Run in FVN_PCTP.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 56000, 'Migration 056 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 56001, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '055')
    THROW 56002, 'Migration 055 must be applied before 056.', 1;
IF OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NULL
    THROW 56003, 'Migration 051 (designer RBAC function) is missing.', 1;
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NULL
    THROW 56004, 'SEC.FunctionCatalog is missing. Apply security migrations first.', 1;
IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'WAREHOUSE.COMPONENT_PO' AND IsActive = 1)
    THROW 56005, 'Function WAREHOUSE.COMPONENT_PO is missing (security migration 044).', 1;
IF OBJECT_ID(N'MES.FVN_ComponentPurchaseOrder', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_ComponentPurchaseOrderLine', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_ComponentLot', N'U') IS NULL
    THROW 56006, 'Component PO tables are missing (migration 031).', 1;
GO

/* ====================================================================== tables */
IF OBJECT_ID(N'MES.FVN_UiDataSource', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiDataSource
    (
        SourceCode       varchar(60)   NOT NULL,
        ViewName         nvarchar(128) NOT NULL,
        DisplayName      nvarchar(160) NOT NULL,
        ViewFunctionCode varchar(100)  NOT NULL,
        SiteColumn       sysname       NOT NULL,
        IsActive         bit           NOT NULL CONSTRAINT DF_MES_FVN_UiDataSource_Active DEFAULT (1),
        ApprovedBy       nvarchar(100) NOT NULL,
        ApprovedAtUtc    datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_UiDataSource_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MES_FVN_UiDataSource PRIMARY KEY (SourceCode),
        CONSTRAINT UQ_MES_FVN_UiDataSource_View UNIQUE (ViewName),
        CONSTRAINT CK_MES_FVN_UiDataSource_Code CHECK (SourceCode NOT LIKE '%[^A-Z0-9_]%'),
        CONSTRAINT CK_MES_FVN_UiDataSource_View CHECK (ViewName LIKE N'MES.vw[_]FVN[_]%' AND ViewName NOT LIKE N'%[^A-Za-z0-9_.]%')
    );
END;

IF OBJECT_ID(N'MES.FVN_UiScreen', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiScreen
    (
        ScreenCode        varchar(60)   NOT NULL,
        Title             nvarchar(160) NOT NULL,
        SourceCode        varchar(60)   NOT NULL,
        DefaultSortColumn sysname       NULL,
        DefaultSortDesc   bit           NOT NULL CONSTRAINT DF_MES_FVN_UiScreen_SortDesc DEFAULT (1),
        GroupColumn       sysname       NULL,
        PageSize          int           NOT NULL CONSTRAINT DF_MES_FVN_UiScreen_Page DEFAULT (200),
        ConfigVersion     int           NOT NULL CONSTRAINT DF_MES_FVN_UiScreen_Ver DEFAULT (1),
        IsActive          bit           NOT NULL CONSTRAINT DF_MES_FVN_UiScreen_Active DEFAULT (1),
        UpdatedBy         nvarchar(100) NOT NULL,
        UpdatedAtUtc      datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_UiScreen_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MES_FVN_UiScreen PRIMARY KEY (ScreenCode),
        CONSTRAINT FK_MES_FVN_UiScreen_Source FOREIGN KEY (SourceCode) REFERENCES MES.FVN_UiDataSource (SourceCode),
        CONSTRAINT CK_MES_FVN_UiScreen_Code CHECK (ScreenCode NOT LIKE '%[^A-Z0-9_]%'),
        CONSTRAINT CK_MES_FVN_UiScreen_Page CHECK (PageSize BETWEEN 20 AND 500)
    );
END;

IF OBJECT_ID(N'MES.FVN_UiScreenColumn', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiScreenColumn
    (
        ScreenCode    varchar(60)  NOT NULL,
        RegionCode    varchar(40)  NOT NULL,   -- MAIN or a ChildCode
        ColumnName    sysname      NOT NULL,
        Caption       nvarchar(120) NOT NULL,
        SequenceNo    int          NOT NULL,
        Width         int          NULL,
        Align         varchar(6)   NOT NULL CONSTRAINT DF_MES_FVN_UiScreenColumn_Align DEFAULT ('LEFT'),
        DisplayFormat varchar(24)  NULL,
        IsVisible     bit          NOT NULL CONSTRAINT DF_MES_FVN_UiScreenColumn_Vis DEFAULT (1),
        CONSTRAINT PK_MES_FVN_UiScreenColumn PRIMARY KEY (ScreenCode, RegionCode, ColumnName),
        CONSTRAINT FK_MES_FVN_UiScreenColumn_Screen FOREIGN KEY (ScreenCode) REFERENCES MES.FVN_UiScreen (ScreenCode),
        CONSTRAINT CK_MES_FVN_UiScreenColumn_Align CHECK (Align IN ('LEFT', 'RIGHT', 'CENTER')),
        CONSTRAINT CK_MES_FVN_UiScreenColumn_Width CHECK (Width IS NULL OR Width BETWEEN 20 AND 1000),
        CONSTRAINT CK_MES_FVN_UiScreenColumn_Format CHECK (DisplayFormat IS NULL
            OR DisplayFormat IN ('N0', 'N2', 'N3', 'dd/MM/yyyy', 'dd/MM/yyyy HH:mm'))
    );
END;

IF OBJECT_ID(N'MES.FVN_UiScreenChild', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiScreenChild
    (
        ScreenCode   varchar(60)   NOT NULL,
        ChildCode    varchar(40)   NOT NULL,
        TabCaption   nvarchar(120) NOT NULL,
        SourceCode   varchar(60)   NOT NULL,
        ParentColumn sysname       NOT NULL,   -- column of the MAIN view
        ChildColumn  sysname       NOT NULL,   -- column of the child view
        SortColumn   sysname       NULL,
        SortDesc     bit           NOT NULL CONSTRAINT DF_MES_FVN_UiScreenChild_Desc DEFAULT (0),
        SequenceNo   int           NOT NULL,
        CONSTRAINT PK_MES_FVN_UiScreenChild PRIMARY KEY (ScreenCode, ChildCode),
        CONSTRAINT FK_MES_FVN_UiScreenChild_Screen FOREIGN KEY (ScreenCode) REFERENCES MES.FVN_UiScreen (ScreenCode),
        CONSTRAINT FK_MES_FVN_UiScreenChild_Source FOREIGN KEY (SourceCode) REFERENCES MES.FVN_UiDataSource (SourceCode),
        CONSTRAINT CK_MES_FVN_UiScreenChild_Code CHECK (ChildCode NOT LIKE '%[^A-Z0-9_]%' AND ChildCode <> 'MAIN')
    );
END;

IF OBJECT_ID(N'MES.FVN_UiScreenFilter', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiScreenFilter
    (
        ScreenCode   varchar(60)   NOT NULL,
        FilterCode   varchar(40)   NOT NULL,
        Caption      nvarchar(120) NOT NULL,
        ColumnName   sysname       NOT NULL,
        OperatorCode varchar(10)   NOT NULL,
        TokenCode    varchar(20)   NULL,
        LiteralValue nvarchar(200) NULL,
        SequenceNo   int           NOT NULL,
        IsDefault    bit           NOT NULL CONSTRAINT DF_MES_FVN_UiScreenFilter_Def DEFAULT (0),
        CONSTRAINT PK_MES_FVN_UiScreenFilter PRIMARY KEY (ScreenCode, FilterCode),
        CONSTRAINT FK_MES_FVN_UiScreenFilter_Screen FOREIGN KEY (ScreenCode) REFERENCES MES.FVN_UiScreen (ScreenCode),
        CONSTRAINT CK_MES_FVN_UiScreenFilter_Code CHECK (FilterCode NOT LIKE '%[^A-Z0-9_]%'),
        CONSTRAINT CK_MES_FVN_UiScreenFilter_Op CHECK (OperatorCode IN ('EQ', 'NE', 'GE', 'LE', 'CONTAINS')),
        CONSTRAINT CK_MES_FVN_UiScreenFilter_Token CHECK (TokenCode IS NULL OR TokenCode IN ('TODAY', 'MONTH_START', 'YEAR_START', 'SITE')),
        CONSTRAINT CK_MES_FVN_UiScreenFilter_One CHECK ((TokenCode IS NULL AND LiteralValue IS NOT NULL)
                                                      OR (TokenCode IS NOT NULL AND LiteralValue IS NULL))
    );
END;

IF OBJECT_ID(N'MES.FVN_UiScreenAudit', N'U') IS NULL
BEGIN
    CREATE TABLE MES.FVN_UiScreenAudit
    (
        AuditId       bigint IDENTITY(1, 1) NOT NULL,
        ScreenCode    varchar(60)   NOT NULL,
        ActionCode    varchar(20)   NOT NULL,
        ActorUserId   bigint        NOT NULL,
        ActorCode     nvarchar(100) NOT NULL,
        VersionBefore int           NULL,
        VersionAfter  int           NULL,
        DetailJson    nvarchar(max) NULL,
        AtUtc         datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_UiScreenAudit_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MES_FVN_UiScreenAudit PRIMARY KEY (AuditId)
    );
END;
GO

/* ====================================================================== helper functions */
IF OBJECT_ID(N'MES.fn_FVN_UiHasFunctionAction', N'FN') IS NOT NULL DROP FUNCTION MES.fn_FVN_UiHasFunctionAction;
GO
CREATE FUNCTION MES.fn_FVN_UiHasFunctionAction(@UserId bigint, @FunctionCode varchar(100), @ActionCode varchar(20))
RETURNS bit
AS
BEGIN
    IF @UserId IS NULL OR @FunctionCode IS NULL OR @ActionCode NOT IN ('VIEW', 'EDIT', 'APPROVE') RETURN 0;
    IF NOT EXISTS (SELECT 1 FROM SEC.UserAccount
                   WHERE UserId = @UserId AND IsActive = 1
                     AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= SYSUTCDATETIME())) RETURN 0;
    IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = @FunctionCode AND IsActive = 1) RETURN 0;
    IF EXISTS (SELECT 1 FROM SEC.UserFunctionOverride
               WHERE UserId = @UserId AND FunctionCode = @FunctionCode AND ActionCode = @ActionCode AND Effect = 'DENY') RETURN 0;
    IF EXISTS (SELECT 1 FROM SEC.UserFunctionOverride
               WHERE UserId = @UserId AND FunctionCode = @FunctionCode AND ActionCode = @ActionCode AND Effect = 'ALLOW') RETURN 1;
    IF EXISTS (SELECT 1
               FROM SEC.UserRole ur
               JOIN SEC.Role r ON r.RoleId = ur.RoleId AND r.IsActive = 1
               JOIN SEC.RoleFunctionPermission p ON p.RoleId = r.RoleId
               WHERE ur.UserId = @UserId AND p.FunctionCode = @FunctionCode
                 AND p.ActionCode = @ActionCode AND p.IsAllowed = 1) RETURN 1;
    RETURN 0;
END;
GO

/* Column catalogue of an APPROVED view: only what sys.columns says, minus types a grid cannot show safely. */
IF OBJECT_ID(N'MES.fn_FVN_UiSourceColumns', N'IF') IS NOT NULL DROP FUNCTION MES.fn_FVN_UiSourceColumns;
GO
CREATE FUNCTION MES.fn_FVN_UiSourceColumns(@ViewName nvarchar(128))
RETURNS TABLE
AS
RETURN
(
    SELECT c.name AS ColumnName, t.name AS TypeName, c.column_id AS ColumnId, c.is_nullable AS IsNullable,
           CASE WHEN t.name IN ('tinyint', 'smallint', 'int', 'bigint', 'decimal', 'numeric', 'float', 'real', 'money', 'smallmoney') THEN 'NUM'
                WHEN t.name IN ('date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset') THEN 'DATE'
                WHEN t.name IN ('char', 'varchar', 'nchar', 'nvarchar') AND c.max_length <> -1 THEN 'TEXT'
                WHEN t.name = 'bit' THEN 'BIT'
                ELSE 'OTHER' END AS TypeGroup
    FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(@ViewName, N'V')
);
GO

/* ====================================================================== data source (Khai báo cơ sở dữ liệu) */
IF OBJECT_ID(N'MES.usp_FVN_UiDataSource_Candidates', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiDataSource_Candidates;
GO
CREATE PROCEDURE MES.usp_FVN_UiDataSource_Candidates @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'APPROVE') = 0
        THROW 56010, 'Không có quyền APPROVE trên ADMIN.DOCUMENT_DESIGNER.', 1;
    SELECT CONCAT(SCHEMA_NAME(v.schema_id), N'.', v.name) AS ViewName
    FROM sys.views v
    WHERE SCHEMA_NAME(v.schema_id) = N'MES' AND v.name LIKE N'vw[_]FVN[_]%'
      AND NOT EXISTS (SELECT 1 FROM MES.FVN_UiDataSource s WHERE s.ViewName = CONCAT(SCHEMA_NAME(v.schema_id), N'.', v.name))
    ORDER BY v.name;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiDataSource_Approve', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiDataSource_Approve;
GO
CREATE PROCEDURE MES.usp_FVN_UiDataSource_Approve
    @ActorUserId bigint, @ActorCode nvarchar(100),
    @SourceCode varchar(60), @ViewName nvarchar(128), @DisplayName nvarchar(160),
    @ViewFunctionCode varchar(100), @SiteColumn sysname, @IsActive bit = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'APPROVE') = 0
        THROW 56020, 'Không có quyền APPROVE trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 56021, 'ActorCode is required.', 1;
    IF @SourceCode IS NULL OR @SourceCode = '' OR @SourceCode LIKE '%[^A-Z0-9_]%'
        THROW 56022, 'SourceCode must be upper-case letters, digits and underscore.', 1;
    IF @ViewName IS NULL OR @ViewName NOT LIKE N'MES.vw[_]FVN[_]%' OR @ViewName LIKE N'%[^A-Za-z0-9_.]%'
        THROW 56023, 'Chỉ được duyệt view MES.vw_FVN_*.', 1;
    IF OBJECT_ID(@ViewName, N'V') IS NULL THROW 56024, 'View không tồn tại.', 1;
    IF NULLIF(LTRIM(RTRIM(@DisplayName)), N'') IS NULL THROW 56025, 'DisplayName is required.', 1;
    IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = @ViewFunctionCode AND IsActive = 1)
        THROW 56026, 'Mã quyền xem phải là một chức năng có thật trong SEC.FunctionCatalog.', 1;
    IF NOT EXISTS (SELECT 1 FROM MES.fn_FVN_UiSourceColumns(@ViewName) WHERE ColumnName = @SiteColumn AND TypeGroup = 'TEXT')
        THROW 56027, 'Cột Site phải là cột chữ có thật của view (bắt buộc để giới hạn dữ liệu theo Site).', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        IF EXISTS (SELECT 1 FROM MES.FVN_UiDataSource WHERE SourceCode = @SourceCode)
        BEGIN
            IF EXISTS (SELECT 1 FROM MES.FVN_UiDataSource WHERE SourceCode = @SourceCode AND ViewName <> @ViewName)
                THROW 56028, 'SourceCode đã gắn với view khác; không đổi view của nguồn đã duyệt.', 1;
            UPDATE MES.FVN_UiDataSource
               SET DisplayName = @DisplayName, ViewFunctionCode = @ViewFunctionCode, SiteColumn = @SiteColumn,
                   IsActive = @IsActive, ApprovedBy = @ActorCode, ApprovedAtUtc = SYSUTCDATETIME()
             WHERE SourceCode = @SourceCode;
        END
        ELSE
            INSERT MES.FVN_UiDataSource (SourceCode, ViewName, DisplayName, ViewFunctionCode, SiteColumn, IsActive, ApprovedBy)
            VALUES (@SourceCode, @ViewName, @DisplayName, @ViewFunctionCode, @SiteColumn, @IsActive, @ActorCode);
        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, DetailJson)
        VALUES (N'SOURCE:' + @SourceCode, 'APPROVE_SRC', @ActorUserId, @ActorCode,
                (SELECT @ViewName AS ViewName, @ViewFunctionCode AS ViewFunctionCode, @SiteColumn AS SiteColumn, @IsActive AS IsActive FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiDataSource_List', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiDataSource_List;
GO
CREATE PROCEDURE MES.usp_FVN_UiDataSource_List @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 56030, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    SELECT SourceCode, ViewName, DisplayName, ViewFunctionCode, SiteColumn, IsActive
    FROM MES.FVN_UiDataSource ORDER BY SourceCode;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiDataSource_Columns', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiDataSource_Columns;
GO
CREATE PROCEDURE MES.usp_FVN_UiDataSource_Columns @ActorUserId bigint, @SourceCode varchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 56031, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    DECLARE @ViewName nvarchar(128) = (SELECT ViewName FROM MES.FVN_UiDataSource WHERE SourceCode = @SourceCode AND IsActive = 1);
    IF @ViewName IS NULL THROW 56032, 'Nguồn dữ liệu chưa được duyệt hoặc đã tắt.', 1;
    SELECT ColumnName, TypeName, TypeGroup, IsNullable
    FROM MES.fn_FVN_UiSourceColumns(@ViewName) WHERE TypeGroup <> 'OTHER' ORDER BY ColumnId;
END;
GO

/* ====================================================================== screen */
IF OBJECT_ID(N'MES.usp_FVN_UiScreen_Create', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreen_Create;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreen_Create
    @ActorUserId bigint, @ActorCode nvarchar(100), @ScreenCode varchar(60), @Title nvarchar(160), @SourceCode varchar(60)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'APPROVE') = 0
        THROW 56040, 'Không có quyền APPROVE trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 56041, 'ActorCode is required.', 1;
    IF @ScreenCode IS NULL OR @ScreenCode = '' OR @ScreenCode LIKE '%[^A-Z0-9_]%' THROW 56042, 'ScreenCode must be upper-case letters, digits and underscore.', 1;
    IF NULLIF(LTRIM(RTRIM(@Title)), N'') IS NULL THROW 56043, 'Title is required.', 1;
    IF EXISTS (SELECT 1 FROM MES.FVN_UiScreen WHERE ScreenCode = @ScreenCode) THROW 56044, 'Màn hình đã tồn tại.', 1;
    DECLARE @ViewName nvarchar(128) = (SELECT ViewName FROM MES.FVN_UiDataSource WHERE SourceCode = @SourceCode AND IsActive = 1);
    IF @ViewName IS NULL THROW 56045, 'Nguồn dữ liệu chưa được duyệt hoặc đã tắt.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        INSERT MES.FVN_UiScreen (ScreenCode, Title, SourceCode, UpdatedBy) VALUES (@ScreenCode, @Title, @SourceCode, @ActorCode);
        INSERT MES.FVN_UiScreenColumn (ScreenCode, RegionCode, ColumnName, Caption, SequenceNo, Align, IsVisible)
        SELECT @ScreenCode, 'MAIN', ColumnName, ColumnName, ROW_NUMBER() OVER (ORDER BY ColumnId),
               CASE WHEN TypeGroup = 'NUM' THEN 'RIGHT' ELSE 'LEFT' END, 1
        FROM MES.fn_FVN_UiSourceColumns(@ViewName) WHERE TypeGroup <> 'OTHER';
        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, VersionAfter)
        VALUES (@ScreenCode, 'CREATE', @ActorUserId, @ActorCode, 1);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* Runtime + designer load. Allowed for: VIEW on the source's own function code, or VIEW on the designer function. */
IF OBJECT_ID(N'MES.usp_FVN_UiScreen_Load', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreen_Load;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreen_Load @ActorUserId bigint, @ScreenCode varchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Function varchar(100), @MainView nvarchar(128);
    SELECT @Function = s.ViewFunctionCode, @MainView = s.ViewName
    FROM MES.FVN_UiScreen sc JOIN MES.FVN_UiDataSource s ON s.SourceCode = sc.SourceCode AND s.IsActive = 1
    WHERE sc.ScreenCode = @ScreenCode AND sc.IsActive = 1;
    IF @MainView IS NULL THROW 56050, 'Màn hình không tồn tại, đã tắt hoặc nguồn dữ liệu không còn được duyệt.', 1;
    IF MES.fn_FVN_UiHasFunctionAction(@ActorUserId, @Function, 'VIEW') = 0
       AND MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 56051, 'Không có quyền xem màn hình này.', 1;

    SELECT sc.ScreenCode, sc.Title, sc.SourceCode, s.ViewName, s.ViewFunctionCode, s.SiteColumn,
           sc.DefaultSortColumn, sc.DefaultSortDesc, sc.GroupColumn, sc.PageSize, sc.ConfigVersion
    FROM MES.FVN_UiScreen sc JOIN MES.FVN_UiDataSource s ON s.SourceCode = sc.SourceCode
    WHERE sc.ScreenCode = @ScreenCode;

    SELECT RegionCode, ColumnName, Caption, SequenceNo, Width, Align, DisplayFormat, IsVisible
    FROM MES.FVN_UiScreenColumn WHERE ScreenCode = @ScreenCode ORDER BY RegionCode, SequenceNo;

    SELECT ch.ChildCode, ch.TabCaption, ch.SourceCode, s.ViewName, s.ViewFunctionCode, s.SiteColumn,
           ch.ParentColumn, ch.ChildColumn, ch.SortColumn, ch.SortDesc, ch.SequenceNo
    FROM MES.FVN_UiScreenChild ch JOIN MES.FVN_UiDataSource s ON s.SourceCode = ch.SourceCode AND s.IsActive = 1
    WHERE ch.ScreenCode = @ScreenCode ORDER BY ch.SequenceNo;

    SELECT FilterCode, Caption, ColumnName, OperatorCode, TokenCode, LiteralValue, SequenceNo, IsDefault
    FROM MES.FVN_UiScreenFilter WHERE ScreenCode = @ScreenCode ORDER BY SequenceNo;

    /* Catalogue of every region's view, so the runtime never needs designer rights to validate columns. */
    SELECT 'MAIN' AS RegionCode, k.ColumnName, k.TypeName, k.TypeGroup, k.IsNullable
    FROM MES.fn_FVN_UiSourceColumns(@MainView) k WHERE k.TypeGroup <> 'OTHER'
    UNION ALL
    SELECT ch.ChildCode, k.ColumnName, k.TypeName, k.TypeGroup, k.IsNullable
    FROM MES.FVN_UiScreenChild ch
    JOIN MES.FVN_UiDataSource s ON s.SourceCode = ch.SourceCode AND s.IsActive = 1
    CROSS APPLY MES.fn_FVN_UiSourceColumns(s.ViewName) k
    WHERE ch.ScreenCode = @ScreenCode AND k.TypeGroup <> 'OTHER';
END;
GO

/* Replace-all save of one screen layout. Every name is re-validated against sys.columns of the approved views. */
IF OBJECT_ID(N'MES.usp_FVN_UiScreen_Save', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreen_Save;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreen_Save
    @ActorUserId bigint, @ActorCode nvarchar(100), @ScreenCode varchar(60), @ExpectedConfigVersion int,
    @Title nvarchar(160), @DefaultSortColumn sysname = NULL, @DefaultSortDesc bit = 1, @GroupColumn sysname = NULL,
    @PageSize int = 200,
    @ColumnsJson nvarchar(max), @ChildrenJson nvarchar(max), @FiltersJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 56110, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 56111, 'ActorCode is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@Title)), N'') IS NULL THROW 56112, 'Title is required.', 1;
    IF @PageSize NOT BETWEEN 20 AND 500 THROW 56113, 'PageSize must be 20..500.', 1;
    IF ISJSON(@ColumnsJson) <> 1 OR ISJSON(@ChildrenJson) <> 1 OR ISJSON(@FiltersJson) <> 1
        THROW 56114, 'Payload must be valid JSON arrays.', 1;

    DECLARE @C TABLE (RegionCode varchar(40) NOT NULL, ColumnName sysname COLLATE DATABASE_DEFAULT NOT NULL, Caption nvarchar(120) NULL,
                      SequenceNo int NOT NULL, Width int NULL, Align varchar(6) NOT NULL, DisplayFormat varchar(24) NULL, IsVisible bit NOT NULL);
    DECLARE @K TABLE (ChildCode varchar(40) NOT NULL, TabCaption nvarchar(120) NULL, SourceCode varchar(60) NOT NULL,
                      ParentColumn sysname COLLATE DATABASE_DEFAULT NOT NULL, ChildColumn sysname COLLATE DATABASE_DEFAULT NOT NULL, SortColumn sysname COLLATE DATABASE_DEFAULT NULL, SortDesc bit NOT NULL, SequenceNo int NOT NULL);
    DECLARE @F TABLE (FilterCode varchar(40) NOT NULL, Caption nvarchar(120) NULL, ColumnName sysname COLLATE DATABASE_DEFAULT NOT NULL, OperatorCode varchar(10) NOT NULL,
                      TokenCode varchar(20) NULL, LiteralValue nvarchar(200) NULL, SequenceNo int NOT NULL, IsDefault bit NOT NULL);
    DECLARE @R TABLE (RegionCode varchar(40) NOT NULL PRIMARY KEY, ViewName nvarchar(128) NOT NULL);

    INSERT @C SELECT RegionCode, ColumnName, Caption, SequenceNo, Width, ISNULL(Align, 'LEFT'), NULLIF(DisplayFormat, ''), ISNULL(IsVisible, 1)
    FROM OPENJSON(@ColumnsJson) WITH (RegionCode varchar(40) '$.RegionCode', ColumnName sysname '$.ColumnName', Caption nvarchar(120) '$.Caption',
         SequenceNo int '$.SequenceNo', Width int '$.Width', Align varchar(6) '$.Align', DisplayFormat varchar(24) '$.DisplayFormat', IsVisible bit '$.IsVisible');
    INSERT @K SELECT ChildCode, TabCaption, SourceCode, ParentColumn, ChildColumn, NULLIF(SortColumn, ''), ISNULL(SortDesc, 0), SequenceNo
    FROM OPENJSON(@ChildrenJson) WITH (ChildCode varchar(40) '$.ChildCode', TabCaption nvarchar(120) '$.TabCaption', SourceCode varchar(60) '$.SourceCode',
         ParentColumn sysname '$.ParentColumn', ChildColumn sysname '$.ChildColumn', SortColumn sysname '$.SortColumn', SortDesc bit '$.SortDesc', SequenceNo int '$.SequenceNo');
    INSERT @F SELECT FilterCode, Caption, ColumnName, OperatorCode, NULLIF(TokenCode, ''), LiteralValue, SequenceNo, ISNULL(IsDefault, 0)
    FROM OPENJSON(@FiltersJson) WITH (FilterCode varchar(40) '$.FilterCode', Caption nvarchar(120) '$.Caption', ColumnName sysname '$.ColumnName',
         OperatorCode varchar(10) '$.OperatorCode', TokenCode varchar(20) '$.TokenCode', LiteralValue nvarchar(200) '$.LiteralValue',
         SequenceNo int '$.SequenceNo', IsDefault bit '$.IsDefault');

    IF (SELECT COUNT(*) FROM @K) > 8 OR (SELECT COUNT(*) FROM @F) > 12 OR EXISTS (SELECT 1 FROM @C GROUP BY RegionCode HAVING COUNT(*) > 80)
        THROW 56120, 'Vượt giới hạn: tối đa 8 tab con, 12 bộ lọc nhanh, 80 cột mỗi vùng.', 1;
    IF EXISTS (SELECT 1 FROM @C GROUP BY RegionCode, ColumnName HAVING COUNT(*) > 1)
       OR EXISTS (SELECT 1 FROM @K GROUP BY ChildCode HAVING COUNT(*) > 1)
       OR EXISTS (SELECT 1 FROM @F GROUP BY FilterCode HAVING COUNT(*) > 1)
        THROW 56121, 'Trùng cột / tab con / bộ lọc trong payload.', 1;
    IF EXISTS (SELECT 1 FROM @C WHERE NULLIF(LTRIM(RTRIM(Caption)), N'') IS NULL OR Align NOT IN ('LEFT', 'RIGHT', 'CENTER')
                  OR (Width IS NOT NULL AND Width NOT BETWEEN 20 AND 1000)
                  OR (DisplayFormat IS NOT NULL AND DisplayFormat NOT IN ('N0', 'N2', 'N3', 'dd/MM/yyyy', 'dd/MM/yyyy HH:mm')))
        THROW 56122, 'Giá trị trình bày của cột không hợp lệ (tiêu đề, độ rộng, căn lề hoặc định dạng).', 1;
    IF EXISTS (SELECT 1 FROM @K WHERE ChildCode LIKE '%[^A-Z0-9_]%' OR ChildCode = 'MAIN' OR NULLIF(LTRIM(RTRIM(TabCaption)), N'') IS NULL)
       OR EXISTS (SELECT 1 FROM @F WHERE FilterCode LIKE '%[^A-Z0-9_]%' OR NULLIF(LTRIM(RTRIM(Caption)), N'') IS NULL)
        THROW 56123, 'Mã hoặc tiêu đề của tab con / bộ lọc không hợp lệ.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Version int, @MainView nvarchar(128);
        SELECT @Version = sc.ConfigVersion, @MainView = s.ViewName
        FROM MES.FVN_UiScreen sc WITH (UPDLOCK, HOLDLOCK)
        JOIN MES.FVN_UiDataSource s ON s.SourceCode = sc.SourceCode AND s.IsActive = 1
        WHERE sc.ScreenCode = @ScreenCode AND sc.IsActive = 1;
        IF @Version IS NULL THROW 56130, 'Màn hình không tồn tại, đã tắt hoặc nguồn dữ liệu không còn được duyệt.', 1;
        IF @Version <> @ExpectedConfigVersion
            THROW 56131, 'Layout đã bị người khác thay đổi (ConfigVersion). Hãy tải lại rồi sửa lại.', 1;

        INSERT @R VALUES ('MAIN', @MainView);
        INSERT @R SELECT k.ChildCode, s.ViewName FROM @K k JOIN MES.FVN_UiDataSource s ON s.SourceCode = k.SourceCode AND s.IsActive = 1;
        IF (SELECT COUNT(*) FROM @R) <> 1 + (SELECT COUNT(*) FROM @K)
            THROW 56132, 'Tab con dùng nguồn dữ liệu chưa được duyệt hoặc đã tắt.', 1;
        IF EXISTS (SELECT 1 FROM @C c WHERE NOT EXISTS (SELECT 1 FROM @R r WHERE r.RegionCode = c.RegionCode))
            THROW 56133, 'Cột thuộc vùng không có trong layout.', 1;

        /* every column must be in the approved view's catalogue; normalise to the catalogue spelling */
        DECLARE @Bad int = 0;
        UPDATE c SET ColumnName = m.ColumnName
        FROM @C c JOIN @R r ON r.RegionCode = c.RegionCode
        CROSS APPLY (SELECT TOP (1) k.ColumnName FROM MES.fn_FVN_UiSourceColumns(r.ViewName) k
                     WHERE k.ColumnName = c.ColumnName AND k.TypeGroup <> 'OTHER') m;
        SELECT @Bad = COUNT(*) FROM @C c JOIN @R r ON r.RegionCode = c.RegionCode
        WHERE NOT EXISTS (SELECT 1 FROM MES.fn_FVN_UiSourceColumns(r.ViewName) k WHERE k.ColumnName = c.ColumnName AND k.TypeGroup <> 'OTHER');
        IF @Bad > 0 THROW 56134, 'Layout chứa cột không có trong view đã duyệt (hoặc kiểu cột không hỗ trợ).', 1;
        IF EXISTS (SELECT 1 FROM @R r WHERE NOT EXISTS (SELECT 1 FROM @C c WHERE c.RegionCode = r.RegionCode AND c.IsVisible = 1))
            THROW 56135, 'Mỗi vùng phải còn ít nhất một cột hiển thị.', 1;

        /* helper: column group inside a region (columns must be part of the layout, hidden allowed) */
        DECLARE @G TABLE (RegionCode varchar(40), ColumnName sysname COLLATE DATABASE_DEFAULT, TypeGroup varchar(5), PRIMARY KEY (RegionCode, ColumnName));
        INSERT @G SELECT c.RegionCode, c.ColumnName, k.TypeGroup
        FROM @C c JOIN @R r ON r.RegionCode = c.RegionCode
        CROSS APPLY MES.fn_FVN_UiSourceColumns(r.ViewName) k WHERE k.ColumnName = c.ColumnName;

        IF @DefaultSortColumn IS NOT NULL AND NOT EXISTS (SELECT 1 FROM @G WHERE RegionCode = 'MAIN' AND ColumnName = @DefaultSortColumn)
            THROW 56136, 'Cột sắp xếp mặc định phải là cột của layout.', 1;
        IF @GroupColumn IS NOT NULL AND NOT EXISTS (SELECT 1 FROM @G WHERE RegionCode = 'MAIN' AND ColumnName = @GroupColumn)
            THROW 56137, 'Cột nhóm phải là cột của layout.', 1;

        IF EXISTS (SELECT 1 FROM @K k
                   WHERE NOT EXISTS (SELECT 1 FROM @G p WHERE p.RegionCode = 'MAIN' AND p.ColumnName = k.ParentColumn)
                      OR NOT EXISTS (SELECT 1 FROM @G ch WHERE ch.RegionCode = k.ChildCode AND ch.ColumnName = k.ChildColumn)
                      OR (k.SortColumn IS NOT NULL AND NOT EXISTS (SELECT 1 FROM @G s WHERE s.RegionCode = k.ChildCode AND s.ColumnName = k.SortColumn)))
            THROW 56138, 'Cặp cột khóa cha-con (hoặc cột sắp xếp tab con) phải là cột có trong layout của từng bên.', 1;
        IF EXISTS (SELECT 1 FROM @K k JOIN @G p ON p.RegionCode = 'MAIN' AND p.ColumnName = k.ParentColumn
                   JOIN @G ch ON ch.RegionCode = k.ChildCode AND ch.ColumnName = k.ChildColumn WHERE p.TypeGroup <> ch.TypeGroup)
            THROW 56139, 'Cột khóa cha và con phải cùng nhóm kiểu dữ liệu.', 1;

        /* quick filters: operator must suit the column type, token must suit the column type, literal must convert */
        IF EXISTS (SELECT 1 FROM @F f WHERE NOT EXISTS (SELECT 1 FROM @G g WHERE g.RegionCode = 'MAIN' AND g.ColumnName = f.ColumnName))
            THROW 56140, 'Bộ lọc nhanh phải dùng cột của layout chính.', 1;
        IF EXISTS (SELECT 1 FROM @F f JOIN @G g ON g.RegionCode = 'MAIN' AND g.ColumnName = f.ColumnName
                   WHERE f.OperatorCode NOT IN ('EQ', 'NE', 'GE', 'LE', 'CONTAINS')
                      OR (f.OperatorCode = 'CONTAINS' AND g.TypeGroup <> 'TEXT')
                      OR (f.OperatorCode IN ('GE', 'LE') AND g.TypeGroup NOT IN ('NUM', 'DATE'))
                      OR (f.TokenCode IN ('TODAY', 'MONTH_START', 'YEAR_START') AND g.TypeGroup <> 'DATE')
                      OR (f.TokenCode = 'SITE' AND g.TypeGroup <> 'TEXT')
                      OR (f.TokenCode IS NULL AND f.LiteralValue IS NULL)
                      OR (f.TokenCode IS NOT NULL AND f.LiteralValue IS NOT NULL)
                      OR (f.TokenCode IS NOT NULL AND f.TokenCode NOT IN ('TODAY', 'MONTH_START', 'YEAR_START', 'SITE'))
                      OR (f.TokenCode IS NULL AND g.TypeGroup = 'DATE' AND TRY_CONVERT(date, f.LiteralValue, 23) IS NULL)
                      OR (f.TokenCode IS NULL AND g.TypeGroup = 'NUM' AND TRY_CONVERT(decimal(38, 10), f.LiteralValue) IS NULL)
                      OR (f.TokenCode IS NULL AND g.TypeGroup = 'BIT' AND f.LiteralValue NOT IN (N'0', N'1')))
            THROW 56141, 'Bộ lọc nhanh không hợp lệ (toán tử/token/giá trị không hợp với kiểu cột).', 1;
        IF (SELECT COUNT(*) FROM @F WHERE IsDefault = 1) > 1 THROW 56142, 'Chỉ được một bộ lọc mặc định.', 1;

        DELETE FROM MES.FVN_UiScreenFilter WHERE ScreenCode = @ScreenCode;
        DELETE FROM MES.FVN_UiScreenChild  WHERE ScreenCode = @ScreenCode;
        DELETE FROM MES.FVN_UiScreenColumn WHERE ScreenCode = @ScreenCode;

        UPDATE MES.FVN_UiScreen
           SET Title = @Title, DefaultSortColumn = @DefaultSortColumn, DefaultSortDesc = @DefaultSortDesc,
               GroupColumn = @GroupColumn, PageSize = @PageSize, ConfigVersion = @Version + 1,
               UpdatedBy = @ActorCode, UpdatedAtUtc = SYSUTCDATETIME()
         WHERE ScreenCode = @ScreenCode;
        INSERT MES.FVN_UiScreenColumn (ScreenCode, RegionCode, ColumnName, Caption, SequenceNo, Width, Align, DisplayFormat, IsVisible)
        SELECT @ScreenCode, RegionCode, ColumnName, LTRIM(RTRIM(Caption)), SequenceNo, Width, Align, DisplayFormat, IsVisible FROM @C;
        INSERT MES.FVN_UiScreenChild (ScreenCode, ChildCode, TabCaption, SourceCode, ParentColumn, ChildColumn, SortColumn, SortDesc, SequenceNo)
        SELECT @ScreenCode, ChildCode, LTRIM(RTRIM(TabCaption)), SourceCode, ParentColumn, ChildColumn, SortColumn, SortDesc, SequenceNo FROM @K;
        INSERT MES.FVN_UiScreenFilter (ScreenCode, FilterCode, Caption, ColumnName, OperatorCode, TokenCode, LiteralValue, SequenceNo, IsDefault)
        SELECT @ScreenCode, FilterCode, LTRIM(RTRIM(Caption)), ColumnName, OperatorCode, TokenCode, LiteralValue, SequenceNo, IsDefault FROM @F;

        INSERT MES.FVN_UiScreenAudit (ScreenCode, ActionCode, ActorUserId, ActorCode, VersionBefore, VersionAfter, DetailJson)
        VALUES (@ScreenCode, 'SAVE', @ActorUserId, @ActorCode, @Version, @Version + 1,
                (SELECT (SELECT COUNT(*) FROM @C) AS Columns, (SELECT COUNT(*) FROM @K) AS Children, (SELECT COUNT(*) FROM @F) AS Filters
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

/* ====================================================================== read-only views + seed: Phiếu đặt hàng linh kiện */
IF OBJECT_ID(N'MES.vw_FVN_UiComponentPo', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiComponentPo;
GO
CREATE VIEW MES.vw_FVN_UiComponentPo
AS
SELECT h.PurchaseOrderId, h.SiteCode, h.PurchaseOrderNo, h.OrderDate, h.SupplierCode, h.SupplierNameSnapshot,
       h.Remarks, h.Status, h.CreatedBy, h.ReleasedBy
FROM MES.FVN_ComponentPurchaseOrder h;
GO
IF OBJECT_ID(N'MES.vw_FVN_UiComponentPoLine', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiComponentPoLine;
GO
CREATE VIEW MES.vw_FVN_UiComponentPoLine
AS
SELECT l.PurchaseOrderId, l.SiteCode, l.[LineNo] AS [LineNo], l.PartNo, l.PartDescription, l.OrderQty, l.ReceivedQty,
       l.OrderQty - l.ReceivedQty AS DiffQty, l.UnitCode, l.NeedByDate
FROM MES.FVN_ComponentPurchaseOrderLine l;
GO
IF OBJECT_ID(N'MES.vw_FVN_UiComponentPoLot', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiComponentPoLot;
GO
CREATE VIEW MES.vw_FVN_UiComponentPoLot
AS
SELECT l.PurchaseOrderId, lot.SiteCode, lot.LotNo, lot.PartNo, lot.LotIssueDate, lot.IssuedQty, lot.ReceivedQty, lot.Status
FROM MES.FVN_ComponentLot lot
JOIN MES.FVN_ComponentPurchaseOrderLine l ON l.PurchaseOrderLineId = lot.PurchaseOrderLineId;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '056')
    BEGIN
        INSERT MES.FVN_UiDataSource (SourceCode, ViewName, DisplayName, ViewFunctionCode, SiteColumn, ApprovedBy)
        SELECT v.SourceCode, v.ViewName, v.DisplayName, 'WAREHOUSE.COMPONENT_PO', N'SiteCode', N'MIGRATION_056'
        FROM (VALUES ('COMPONENT_PO',      N'MES.vw_FVN_UiComponentPo',     N'Phiếu đặt hàng linh kiện'),
                     ('COMPONENT_PO_LINE', N'MES.vw_FVN_UiComponentPoLine', N'Chi tiết phụ tùng của PO'),
                     ('COMPONENT_PO_LOT',  N'MES.vw_FVN_UiComponentPoLot',  N'Tem / Lot của PO')) v(SourceCode, ViewName, DisplayName)
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_UiDataSource s WHERE s.SourceCode = v.SourceCode);

        IF NOT EXISTS (SELECT 1 FROM MES.FVN_UiScreen WHERE ScreenCode = 'COMPONENT_PO')
        BEGIN
            INSERT MES.FVN_UiScreen (ScreenCode, Title, SourceCode, DefaultSortColumn, DefaultSortDesc, GroupColumn, PageSize, UpdatedBy)
            VALUES ('COMPONENT_PO', N'Phiếu đặt hàng linh kiện', 'COMPONENT_PO', 'OrderDate', 1, 'Status', 200, N'MIGRATION_056');

            INSERT MES.FVN_UiScreenColumn (ScreenCode, RegionCode, ColumnName, Caption, SequenceNo, Width, Align, DisplayFormat, IsVisible)
            VALUES
            ('COMPONENT_PO', 'MAIN', 'PurchaseOrderNo',      N'Số PO',          1, 110, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'OrderDate',            N'Ngày lập phiếu', 2,  95, 'LEFT',  'dd/MM/yyyy', 1),
            ('COMPONENT_PO', 'MAIN', 'SupplierCode',         N'Mã nhà cung cấp',3, 110, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'SupplierNameSnapshot', N'Nhà cung cấp',   4, 240, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'Remarks',              N'Diễn giải',      5, 240, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'CreatedBy',            N'Người lập',      6, 110, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'Status',               N'Trạng thái',     7, 130, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'PurchaseOrderId',      N'Id',             8,  72, 'RIGHT', NULL, 1),
            ('COMPONENT_PO', 'MAIN', 'SiteCode',             N'Site',           9,  60, 'LEFT',  NULL, 0),
            ('COMPONENT_PO', 'MAIN', 'ReleasedBy',           N'Người phát hành',10,110, 'LEFT',  NULL, 0),
            ('COMPONENT_PO', 'LINES', 'PartNo',              N'Mã vật tư',      1, 140, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'LINES', 'PartDescription',     N'Tên phụ tùng',   2, 220, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'LINES', 'OrderQty',            N'SL đặt',         3,  80, 'RIGHT', 'N0', 1),
            ('COMPONENT_PO', 'LINES', 'UnitCode',            N'Đvt',            4,  50, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'LINES', 'NeedByDate',          N'Ngày yêu cầu',   5,  95, 'LEFT',  'dd/MM/yyyy', 1),
            ('COMPONENT_PO', 'LINES', 'ReceivedQty',         N'SL thực tế',     6,  80, 'RIGHT', 'N0', 1),
            ('COMPONENT_PO', 'LINES', 'DiffQty',             N'Chênh lệch',     7,  80, 'RIGHT', 'N0', 1),
            ('COMPONENT_PO', 'LINES', 'PurchaseOrderId',     N'PO Id',          8,  60, 'RIGHT', NULL, 0),
            ('COMPONENT_PO', 'LINES', 'LineNo',             N'Dòng',           0,  50, 'RIGHT', NULL, 1),
            ('COMPONENT_PO', 'LOTS',  'LotNo',               N'Số lot',         1, 200, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'LOTS',  'PartNo',              N'Mã vật tư',      2, 140, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'LOTS',  'LotIssueDate',        N'Ngày phát hành', 3,  95, 'LEFT',  'dd/MM/yyyy', 1),
            ('COMPONENT_PO', 'LOTS',  'IssuedQty',           N'SL theo tem',    4,  90, 'RIGHT', 'N0', 1),
            ('COMPONENT_PO', 'LOTS',  'ReceivedQty',         N'SL thực tế',     5,  90, 'RIGHT', 'N0', 1),
            ('COMPONENT_PO', 'LOTS',  'Status',              N'Trạng thái',     6, 130, 'LEFT',  NULL, 1),
            ('COMPONENT_PO', 'LOTS',  'PurchaseOrderId',     N'PO Id',          7,  60, 'RIGHT', NULL, 0);

            INSERT MES.FVN_UiScreenChild (ScreenCode, ChildCode, TabCaption, SourceCode, ParentColumn, ChildColumn, SortColumn, SortDesc, SequenceNo)
            VALUES ('COMPONENT_PO', 'LINES', N'Chi tiết phụ tùng', 'COMPONENT_PO_LINE', 'PurchaseOrderId', 'PurchaseOrderId', 'LineNo', 0, 1),
                   ('COMPONENT_PO', 'LOTS',  N'Danh sách tem',     'COMPONENT_PO_LOT',  'PurchaseOrderId', 'PurchaseOrderId', 'LotNo', 0, 2);

            INSERT MES.FVN_UiScreenFilter (ScreenCode, FilterCode, Caption, ColumnName, OperatorCode, TokenCode, LiteralValue, SequenceNo, IsDefault)
            VALUES ('COMPONENT_PO', 'OPEN',   N'Đang xử lý',          'Status',    'NE', NULL, N'CLOSED', 1, 1),
                   ('COMPONENT_PO', 'CLOSED', N'Dữ liệu đã kết thúc', 'Status',    'EQ', NULL, N'CLOSED', 2, 0),
                   ('COMPONENT_PO', 'TODAY',  N'Chứng từ trong ngày', 'OrderDate', 'GE', 'TODAY',       NULL, 3, 0),
                   ('COMPONENT_PO', 'MONTH',  N'Chứng từ trong tháng','OrderDate', 'GE', 'MONTH_START', NULL, 4, 0),
                   ('COMPONENT_PO', 'YEAR',   N'Chứng từ trong năm',  'OrderDate', 'GE', 'YEAR_START',  NULL, 5, 0);
        END;
        INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
        VALUES ('056', N'PCTP UI Layout Designer: approved data sources, structured layouts, seed COMPONENT_PO');
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO