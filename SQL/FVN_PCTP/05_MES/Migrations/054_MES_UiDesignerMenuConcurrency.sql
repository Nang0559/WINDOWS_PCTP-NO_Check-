/*
 Migration 054: PCTP UI Designer - menu save with optimistic concurrency.

 usp_FVN_UiScreenMenu_Save (053) had no concurrency token, so two designers could overwrite each other's
 menu placement. This replaces it with a version that:
   - requires @ExpectedConfigVersion and compares it to MES.FVN_DocumentDefinition.ConfigVersion under UPDLOCK/HOLDLOCK;
   - raises 55131 on mismatch (same code as usp_FVN_DocumentMetadata_Save, mapped to DocumentConfigConflictException);
   - bumps ConfigVersion when the menu row really changed;
   - still touches presentation columns only (MenuGroup, MenuText, SequenceNo, IsActive) and audits before/after.
 It cannot create modules, views, fields, commands, procedures or workflows. Apply after 053.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 55400, 'Migration 054 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 55401, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '053')
    THROW 55402, 'Migration 053 must be applied before 054.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentMenu', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_UiScreenQuarantine', N'U') IS NULL
   OR OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NULL
    THROW 55403, 'Menu / permission objects are missing. Apply migration 053 first.', 1;

IF OBJECT_ID(N'MES.usp_FVN_UiScreenMenu_Save', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_UiScreenMenu_Save;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreenMenu_Save
    @ModuleCode varchar(80),
    @MenuGroup varchar(20),
    @MenuText nvarchar(160),
    @SequenceNo int,
    @IsActive bit,
    @ExpectedConfigVersion int,
    @ActorUserId bigint,
    @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 55420, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF EXISTS (SELECT 1 FROM MES.FVN_UiScreenQuarantine WHERE ModuleCode = @ModuleCode)
        THROW 55422, 'Module khai báo từ SQL đã bị cách ly; phải đăng ký lại trong C# trước.', 1;
    IF @MenuGroup NOT IN ('NHAPKHO','KHO','GIAOHANG','BAOCAO','CONGCU')
       OR @MenuText IS NULL OR LEN(LTRIM(RTRIM(@MenuText))) = 0 OR LEN(LTRIM(RTRIM(@MenuText))) > 160
       OR CHARINDEX(CHAR(9), @MenuText) > 0 OR CHARINDEX(CHAR(10), @MenuText) > 0 OR CHARINDEX(CHAR(13), @MenuText) > 0
       OR @SequenceNo IS NULL OR @SequenceNo NOT BETWEEN 0 AND 10000 OR @IsActive IS NULL
       OR @ExpectedConfigVersion IS NULL
       OR NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL
        THROW 55423, 'Thông tin menu không hợp lệ.', 1;

    DECLARE @Before nvarchar(max), @After nvarchar(max), @Changed int = 0,
            @Version int, @NewVersion int, @Now datetime2(3) = SYSUTCDATETIME();
    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Version = ConfigVersion
        FROM MES.FVN_DocumentDefinition WITH (UPDLOCK, HOLDLOCK)
        WHERE ModuleCode = @ModuleCode AND IsActive = 1;
        IF @Version IS NULL
            THROW 55421, 'Module chưa được đăng ký trong C# Trusted Registry / metadata seed.', 1;
        IF @Version <> @ExpectedConfigVersion
            THROW 55131, 'Cấu hình giao diện đã bị người khác thay đổi (ConfigVersion). Hãy tải lại rồi sửa lại.', 1;

        SELECT @Before = (SELECT MenuGroup, MenuText, SequenceNo, IsActive
                          FROM MES.FVN_DocumentMenu WITH (UPDLOCK, HOLDLOCK)
                          WHERE ModuleCode = @ModuleCode FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

        SET @After = (SELECT @MenuGroup AS MenuGroup, LTRIM(RTRIM(@MenuText)) AS MenuText,
                             @SequenceNo AS SequenceNo, @IsActive AS IsActive
                      FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

        IF ISNULL(@Before, N'') <> ISNULL(@After, N'')
        BEGIN
            IF @Before IS NOT NULL
                UPDATE MES.FVN_DocumentMenu
                SET MenuGroup = @MenuGroup, MenuText = LTRIM(RTRIM(@MenuText)), SequenceNo = @SequenceNo,
                    IsActive = @IsActive, UpdatedAtUtc = @Now, UpdatedBy = @ActorCode
                WHERE ModuleCode = @ModuleCode;
            ELSE
                INSERT MES.FVN_DocumentMenu(ModuleCode, MenuGroup, MenuText, SequenceNo, IsActive, UpdatedBy)
                VALUES (@ModuleCode, @MenuGroup, LTRIM(RTRIM(@MenuText)), @SequenceNo, @IsActive, @ActorCode);

            SET @Changed = 1;
            INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, BeforeValue, AfterValue, ActorCode)
            VALUES (@ModuleCode, 'UI_MENU', @ModuleCode,
                    CASE WHEN @Before IS NULL THEN 'CREATE' ELSE 'UPDATE' END, @Before, @After, @ActorCode);

            SET @NewVersion = @Version + 1;
            UPDATE MES.FVN_DocumentDefinition
            SET ConfigVersion = @NewVersion, UpdatedAtUtc = @Now, UpdatedBy = @ActorCode
            WHERE ModuleCode = @ModuleCode;
        END
        ELSE
            SET @NewVersion = @Version;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @NewVersion AS ConfigVersion, @Changed AS ChangedCount;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '054')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('054', N'PCTP UI Designer: menu save with ConfigVersion concurrency');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO