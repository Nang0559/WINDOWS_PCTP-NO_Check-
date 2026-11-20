/*
 Migration 053: PCTP UI Designer - enforce C# Trusted Registry as the only screen catalogue.

 Migration 052 introduced SQL-created screen declarations from arbitrary MES.vw_FVN_* views.
 That path violates the three-layer contract. This migration:
   - quarantines every module created through FVN_DocumentDeclared;
   - removes the SQL declaration APIs and declaration table;
   - makes menu configuration reference only MES.FVN_DocumentDefinition rows;
   - exposes list/save procedures for presentation-only menu configuration.
 It does not create modules, views, fields, commands, procedures or workflows.
 Apply after 052 (or after 051 if 052 has not been applied).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 55300, 'Migration 053 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 55301, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion IN ('051','052'))
    THROW 55302, 'Migration 051 or 052 must be applied before 053.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentDefinition', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentConfigAudit', N'U') IS NULL
   OR OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NULL
    THROW 55303, 'Document metadata / permission objects are missing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'MES.FVN_UiScreenQuarantine', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_UiScreenQuarantine
        (
            ModuleCode varchar(80) NOT NULL CONSTRAINT PK_FVN_UiScreenQuarantine PRIMARY KEY,
            QuarantinedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_UiScreenQuarantine_At DEFAULT(SYSUTCDATETIME()),
            Reason nvarchar(240) NOT NULL
        );
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentDeclared', N'U') IS NOT NULL
    BEGIN
        INSERT MES.FVN_UiScreenQuarantine(ModuleCode, Reason)
        SELECT d.ModuleCode, N'Created through the retired SQL declaration path in migration 052; requires C# registration.'
        FROM MES.FVN_DocumentDeclared d
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_UiScreenQuarantine q WHERE q.ModuleCode = d.ModuleCode);
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentMenu', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentMenu
        (
            ModuleCode varchar(80) NOT NULL CONSTRAINT PK_FVN_DocumentMenu PRIMARY KEY,
            MenuGroup varchar(20) NOT NULL,
            MenuText nvarchar(160) NOT NULL,
            SequenceNo int NOT NULL CONSTRAINT DF_FVN_DocumentMenu_Sequence DEFAULT(100),
            IsActive bit NOT NULL CONSTRAINT DF_FVN_DocumentMenu_Active DEFAULT(0),
            UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocumentMenu_Updated DEFAULT(SYSUTCDATETIME()),
            UpdatedBy nvarchar(100) NOT NULL,
            CONSTRAINT FK_FVN_DocumentMenu_Definition FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDefinition(ModuleCode),
            CONSTRAINT CK_FVN_DocumentMenu_Group CHECK(MenuGroup IN ('NHAPKHO','KHO','GIAOHANG','BAOCAO','CONGCU')),
            CONSTRAINT CK_FVN_DocumentMenu_Sequence CHECK(SequenceNo BETWEEN 0 AND 10000)
        );
    END
    ELSE
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'MES.FVN_DocumentMenu')
                   AND name = N'FK_FVN_DocumentMenu_Declared')
            ALTER TABLE MES.FVN_DocumentMenu DROP CONSTRAINT FK_FVN_DocumentMenu_Declared;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'MES.FVN_DocumentMenu')
                       AND name = N'FK_FVN_DocumentMenu_Definition')
            ALTER TABLE MES.FVN_DocumentMenu WITH CHECK
                ADD CONSTRAINT FK_FVN_DocumentMenu_Definition FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDefinition(ModuleCode);
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentDeclared', N'U') IS NOT NULL
        DROP TABLE MES.FVN_DocumentDeclared;

    IF OBJECT_ID(N'MES.usp_FVN_DocumentDefinition_Create', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_DocumentDefinition_Create;
    IF OBJECT_ID(N'MES.usp_FVN_DocumentDesigner_ListViews', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_DocumentDesigner_ListViews;
    IF OBJECT_ID(N'MES.usp_FVN_DocumentDesigner_ListViewColumns', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_DocumentDesigner_ListViewColumns;
    IF OBJECT_ID(N'MES.usp_FVN_DocumentDesigner_ListFunctions', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_DocumentDesigner_ListFunctions;
    IF OBJECT_ID(N'MES.fn_FVN_DocumentEditorForType', N'FN') IS NOT NULL DROP FUNCTION MES.fn_FVN_DocumentEditorForType;

    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '053')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('053', N'PCTP UI Designer: C# trusted screen registry only; SQL presentation menu metadata');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiScreenMenu_List', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreenMenu_List;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreenMenu_List
    @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 55310, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;

    SELECT d.ModuleCode, d.DisplayName, m.MenuGroup, m.MenuText, m.SequenceNo, m.IsActive,
           ISNULL(d.ConfigVersion, 1) AS ConfigVersion
    FROM MES.FVN_DocumentDefinition d
    LEFT JOIN MES.FVN_DocumentMenu m ON m.ModuleCode = d.ModuleCode
    LEFT JOIN MES.FVN_UiScreenQuarantine q ON q.ModuleCode = d.ModuleCode
    WHERE q.ModuleCode IS NULL
    ORDER BY ISNULL(m.MenuGroup, 'CONGCU'), ISNULL(m.SequenceNo, 100), d.ModuleCode;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiScreenMenu_Save', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreenMenu_Save;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreenMenu_Save
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
        THROW 55320, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentDefinition WHERE ModuleCode = @ModuleCode)
        THROW 55321, 'Module chưa được đăng ký trong C# Trusted Registry / metadata seed.', 1;
    IF EXISTS (SELECT 1 FROM MES.FVN_UiScreenQuarantine WHERE ModuleCode = @ModuleCode)
        THROW 55322, 'Module khai báo từ SQL đã bị cách ly; phải đăng ký lại trong C# trước.', 1;
    IF @MenuGroup NOT IN ('NHAPKHO','KHO','GIAOHANG','BAOCAO','CONGCU')
       OR @MenuText IS NULL OR LEN(LTRIM(RTRIM(@MenuText))) = 0 OR LEN(LTRIM(RTRIM(@MenuText))) > 160
       OR @SequenceNo NOT BETWEEN 0 AND 10000 OR @IsActive IS NULL
       OR NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL
        THROW 55323, 'Thông tin menu không hợp lệ.', 1;

    DECLARE @Before nvarchar(max), @After nvarchar(max), @Changed int = 0;
    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Before = (SELECT MenuGroup, MenuText, SequenceNo, IsActive
                          FROM MES.FVN_DocumentMenu WITH (UPDLOCK, HOLDLOCK)
                          WHERE ModuleCode = @ModuleCode FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

        IF EXISTS (SELECT 1 FROM MES.FVN_DocumentMenu WHERE ModuleCode = @ModuleCode)
            UPDATE MES.FVN_DocumentMenu
            SET MenuGroup=@MenuGroup, MenuText=LTRIM(RTRIM(@MenuText)), SequenceNo=@SequenceNo,
                IsActive=@IsActive, UpdatedAtUtc=SYSUTCDATETIME(), UpdatedBy=@ActorCode
            WHERE ModuleCode=@ModuleCode;
        ELSE
            INSERT MES.FVN_DocumentMenu(ModuleCode,MenuGroup,MenuText,SequenceNo,IsActive,UpdatedBy)
            VALUES(@ModuleCode,@MenuGroup,LTRIM(RTRIM(@MenuText)),@SequenceNo,@IsActive,@ActorCode);

        SET @After = (SELECT @MenuGroup AS MenuGroup, LTRIM(RTRIM(@MenuText)) AS MenuText,
                             @SequenceNo AS SequenceNo, @IsActive AS IsActive
                      FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        IF ISNULL(@Before,N'') <> ISNULL(@After,N'')
        BEGIN
            SET @Changed = 1;
            INSERT MES.FVN_DocumentConfigAudit(ModuleCode,ConfigObject,ConfigKey,ActionCode,BeforeValue,AfterValue,ActorCode)
            VALUES(@ModuleCode,'UI_MENU',@ModuleCode,
                   CASE WHEN @Before IS NULL THEN 'CREATE' ELSE 'UPDATE' END,
                   @Before,@After,@ActorCode);
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT ISNULL((SELECT ConfigVersion FROM MES.FVN_DocumentDefinition WHERE ModuleCode=@ModuleCode),1) AS ConfigVersion,
           @Changed AS ChangedCount;
END;
GO
