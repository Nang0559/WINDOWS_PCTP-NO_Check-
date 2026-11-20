/*
 Migration 051: write path for layer-2 (approved presentation metadata) of the Document Runtime.

 Layer 1 (C# trusted registry) is NOT touched: views, procedures, commands, editors and workflow bindings
 still come from code. This migration only lets an authorized designer change PRESENTATION, and enforces
 that in SQL (layer 3):
   - RBAC is checked inside SQL from SEC role grants + user overrides (function ADMIN.DOCUMENT_DESIGNER:
     VIEW = open/read, EDIT = save presentation, APPROVE = provision a document from the code registry).
     No role is granted automatically; grant it in the security administration screen.
   - Optimistic concurrency on MES.FVN_DocumentDefinition.ConfigVersion (UPDLOCK/HOLDLOCK, one transaction).
   - Save is UPDATE-ONLY on rows that already exist: caption, sequence, width, display format, visibility.
     It cannot touch EditorCode, IsReadOnly, IsRequired, LookupCode, RequiredStatusCode, views, procedures
     or workflow, and cannot add a field/command/layout.
   - Field codes must be real columns of the document's registered views.
   - Provision creates the definition (or only the missing rows) for a document that C# registered. It
     accepts views only as MES.vw_FVN_* objects that exist as views, never a procedure name or SQL text,
     and always seeds fields read-only. The app still fails closed at startup if SQL metadata and the
     C# registry ever disagree (DocumentDefinitionValidator).
   - Every change is written to MES.FVN_DocumentConfigAudit with before/after JSON.
 SQL Server 2016 compatible (OPENJSON, FOR JSON). Apply after 050.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 55100, 'Migration 051 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 55101, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '050')
    THROW 55102, 'Migration 050 must be applied before 051.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentDefinition', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentField', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentCommand', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentLayout', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentConfigAudit', N'U') IS NULL
    THROW 55103, 'Document metadata tables are missing. Re-run migration 039.', 1;
IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NULL
    THROW 55104, 'SEC.FunctionCatalog is missing. Apply security migrations first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT SEC.FunctionCatalog(FunctionCode, ModuleCode, FunctionName, SortOrder)
    SELECT 'ADMIN.DOCUMENT_DESIGNER', 'ADMIN', N'Thiết kế chứng từ (metadata giao diện Document Runtime)', 930
    WHERE NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'ADMIN.DOCUMENT_DESIGNER');

    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '051')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('051', N'Document Designer: RBAC function and enforced metadata write procedures');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* ---------- SQL-side permission check (same semantics as SEC.usp_Auth effective permissions) ---------- */
IF OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NOT NULL
    DROP FUNCTION MES.fn_FVN_DocumentDesigner_HasAction;
GO
CREATE FUNCTION MES.fn_FVN_DocumentDesigner_HasAction(@UserId bigint, @ActionCode varchar(20))
RETURNS bit
AS
BEGIN
    IF @UserId IS NULL OR @ActionCode NOT IN ('VIEW', 'EDIT', 'APPROVE') RETURN 0;
    IF NOT EXISTS (SELECT 1 FROM SEC.UserAccount
                   WHERE UserId = @UserId AND IsActive = 1
                     AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= SYSUTCDATETIME())) RETURN 0;
    IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog
                   WHERE FunctionCode = 'ADMIN.DOCUMENT_DESIGNER' AND IsActive = 1) RETURN 0;
    IF EXISTS (SELECT 1 FROM SEC.UserFunctionOverride
               WHERE UserId = @UserId AND FunctionCode = 'ADMIN.DOCUMENT_DESIGNER'
                 AND ActionCode = @ActionCode AND Effect = 'DENY') RETURN 0;
    IF EXISTS (SELECT 1 FROM SEC.UserFunctionOverride
               WHERE UserId = @UserId AND FunctionCode = 'ADMIN.DOCUMENT_DESIGNER'
                 AND ActionCode = @ActionCode AND Effect = 'ALLOW') RETURN 1;
    IF EXISTS (SELECT 1
               FROM SEC.UserRole ur
               JOIN SEC.Role r ON r.RoleId = ur.RoleId AND r.IsActive = 1
               JOIN SEC.RoleFunctionPermission p ON p.RoleId = r.RoleId
               WHERE ur.UserId = @UserId AND p.FunctionCode = 'ADMIN.DOCUMENT_DESIGNER'
                 AND p.ActionCode = @ActionCode AND p.IsAllowed = 1) RETURN 1;
    RETURN 0;
END;
GO

/* ---------- Save presentation (update-only, optimistic concurrency, audited) ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentMetadata_Save', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentMetadata_Save;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentMetadata_Save
    @ModuleCode varchar(80),
    @ActorUserId bigint,
    @ActorCode nvarchar(100),
    @ExpectedConfigVersion int,
    @FieldsJson nvarchar(max) = NULL,    -- [{RegionCode,FieldCode,Caption,SequenceNo,Width,DisplayFormat,IsVisible}]
    @CommandsJson nvarchar(max) = NULL,  -- [{CommandCode,Caption,SequenceNo,IsVisible}]
    @LayoutsJson nvarchar(max) = NULL    -- [{LayoutCode,Caption,SequenceNo,IsVisible}]
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'EDIT') = 0
        THROW 55110, 'Không có quyền EDIT trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 55111, 'ActorCode is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ModuleCode)), '') IS NULL THROW 55112, 'ModuleCode is required.', 1;
    IF (@FieldsJson IS NOT NULL AND ISJSON(@FieldsJson) <> 1)
       OR (@CommandsJson IS NOT NULL AND ISJSON(@CommandsJson) <> 1)
       OR (@LayoutsJson IS NOT NULL AND ISJSON(@LayoutsJson) <> 1)
        THROW 55113, 'Payload must be valid JSON.', 1;

    DECLARE @F TABLE (RegionCode varchar(30) NOT NULL, FieldCode varchar(100) NOT NULL, Caption nvarchar(160) NULL,
        SequenceNo int NULL, Width int NULL, DisplayFormat nvarchar(80) NULL, IsVisible bit NULL);
    DECLARE @C TABLE (CommandCode varchar(80) NOT NULL, Caption nvarchar(120) NULL, SequenceNo int NULL, IsVisible bit NULL);
    DECLARE @L TABLE (LayoutCode varchar(80) NOT NULL, Caption nvarchar(160) NULL, SequenceNo int NULL, IsVisible bit NULL);

    IF @FieldsJson IS NOT NULL
        INSERT @F
        SELECT UPPER(RegionCode), FieldCode, Caption, SequenceNo, Width, DisplayFormat, IsVisible
        FROM OPENJSON(@FieldsJson) WITH (
            RegionCode varchar(30) '$.RegionCode', FieldCode varchar(100) '$.FieldCode',
            Caption nvarchar(160) '$.Caption', SequenceNo int '$.SequenceNo', Width int '$.Width',
            DisplayFormat nvarchar(80) '$.DisplayFormat', IsVisible bit '$.IsVisible');
    IF @CommandsJson IS NOT NULL
        INSERT @C
        SELECT CommandCode, Caption, SequenceNo, IsVisible
        FROM OPENJSON(@CommandsJson) WITH (
            CommandCode varchar(80) '$.CommandCode', Caption nvarchar(120) '$.Caption',
            SequenceNo int '$.SequenceNo', IsVisible bit '$.IsVisible');
    IF @LayoutsJson IS NOT NULL
        INSERT @L
        SELECT UPPER(LayoutCode), Caption, SequenceNo, IsVisible
        FROM OPENJSON(@LayoutsJson) WITH (
            LayoutCode varchar(80) '$.LayoutCode', Caption nvarchar(160) '$.Caption',
            SequenceNo int '$.SequenceNo', IsVisible bit '$.IsVisible');

    -- Pure payload validation (no data needed).
    IF EXISTS (SELECT 1 FROM @F WHERE RegionCode NOT IN ('HEADER', 'DETAIL')) THROW 55120, 'Unknown field region.', 1;
    IF EXISTS (SELECT 1 FROM @F GROUP BY RegionCode, FieldCode HAVING COUNT(*) > 1) THROW 55121, 'Duplicate field in payload.', 1;
    IF EXISTS (SELECT 1 FROM @C GROUP BY CommandCode HAVING COUNT(*) > 1) THROW 55122, 'Duplicate command in payload.', 1;
    IF EXISTS (SELECT 1 FROM @L GROUP BY LayoutCode HAVING COUNT(*) > 1) THROW 55123, 'Duplicate layout in payload.', 1;
    IF EXISTS (SELECT 1 FROM @L WHERE LayoutCode NOT IN ('HEADER', 'DETAIL')) THROW 55124, 'Unknown layout.', 1;
    IF EXISTS (SELECT 1 FROM @F WHERE Caption IS NULL OR LEN(LTRIM(RTRIM(Caption))) = 0
                                   OR CHARINDEX(CHAR(9), Caption) > 0 OR CHARINDEX(CHAR(10), Caption) > 0 OR CHARINDEX(CHAR(13), Caption) > 0
                                   OR SequenceNo IS NULL OR SequenceNo NOT BETWEEN 0 AND 10000
                                   OR IsVisible IS NULL
                                   OR (Width IS NOT NULL AND Width NOT BETWEEN 20 AND 2000)
                                   OR (DisplayFormat IS NOT NULL AND (LEN(DisplayFormat) > 40
                                        OR DisplayFormat COLLATE Latin1_General_BIN LIKE '%[^a-zA-Z0-9 #,.:/%-]%')))
        THROW 55125, 'Invalid field presentation value (caption, sequence, width, display format or visibility).', 1;
    IF EXISTS (SELECT 1 FROM @C WHERE Caption IS NULL OR LEN(LTRIM(RTRIM(Caption))) = 0
                                   OR CHARINDEX(CHAR(9), Caption) > 0 OR CHARINDEX(CHAR(10), Caption) > 0 OR CHARINDEX(CHAR(13), Caption) > 0
                                   OR SequenceNo IS NULL OR SequenceNo NOT BETWEEN 0 AND 10000 OR IsVisible IS NULL)
        THROW 55126, 'Invalid command presentation value.', 1;
    IF EXISTS (SELECT 1 FROM @L WHERE (Caption IS NOT NULL AND (LEN(Caption) > 160
                                   OR CHARINDEX(CHAR(9), Caption) > 0 OR CHARINDEX(CHAR(10), Caption) > 0 OR CHARINDEX(CHAR(13), Caption) > 0))
                                   OR SequenceNo IS NULL OR SequenceNo NOT BETWEEN 0 AND 10000 OR IsVisible IS NULL)
        THROW 55127, 'Invalid layout presentation value.', 1;

    DECLARE @ChangedCount int = 0, @NewVersion int, @Now datetime2(3) = SYSUTCDATETIME();

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Version int, @HeaderView sysname, @DetailView sysname;
        SELECT @Version = ConfigVersion, @HeaderView = HeaderView, @DetailView = DetailView
        FROM MES.FVN_DocumentDefinition WITH (UPDLOCK, HOLDLOCK)
        WHERE ModuleCode = @ModuleCode AND IsActive = 1;
        IF @Version IS NULL THROW 55130, 'Document definition was not found (or is inactive).', 1;
        IF @Version <> @ExpectedConfigVersion
            THROW 55131, 'Cấu hình chứng từ đã bị người khác thay đổi (ConfigVersion). Hãy tải lại rồi sửa lại.', 1;
        IF ISNULL(OBJECTPROPERTY(OBJECT_ID(@HeaderView), 'IsView'), 0) <> 1 OR ISNULL(OBJECTPROPERTY(OBJECT_ID(@DetailView), 'IsView'), 0) <> 1
            THROW 55132, 'Registered header/detail view does not exist as a view.', 1;

        -- Update-only: every payload row must already exist, and the field must be a real column of its view.
        IF EXISTS (SELECT 1 FROM @F f WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentField t
                   WHERE t.ModuleCode = @ModuleCode AND t.RegionCode = f.RegionCode AND t.FieldCode = f.FieldCode))
            THROW 55133, 'Save cannot add a field. Use Provision for registered fields without metadata.', 1;
        IF EXISTS (SELECT 1 FROM @F f WHERE NOT EXISTS (SELECT 1 FROM sys.columns c
                   WHERE c.object_id = OBJECT_ID(CASE f.RegionCode WHEN 'HEADER' THEN @HeaderView ELSE @DetailView END)
                     AND c.name = f.FieldCode))
            THROW 55134, 'Field is not a column of the registered view.', 1;
        IF EXISTS (SELECT 1 FROM @C x WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentCommand t
                   WHERE t.ModuleCode = @ModuleCode AND t.CommandCode = x.CommandCode))
            THROW 55135, 'Save cannot add a command.', 1;
        IF EXISTS (SELECT 1 FROM @L x WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentLayout t
                   WHERE t.ModuleCode = @ModuleCode AND t.LayoutCode = x.LayoutCode))
            THROW 55136, 'Save cannot add a layout.', 1;

        DECLARE @FC TABLE (RegionCode varchar(30), FieldCode varchar(100), OldCaption nvarchar(160), OldSeq int, OldWidth int,
            OldFormat nvarchar(80), OldVisible bit, NewCaption nvarchar(160), NewSeq int, NewWidth int, NewFormat nvarchar(80), NewVisible bit);
        UPDATE t SET Caption = LTRIM(RTRIM(f.Caption)), SequenceNo = f.SequenceNo, Width = f.Width,
                     DisplayFormat = NULLIF(f.DisplayFormat, N''), IsVisible = f.IsVisible
        OUTPUT inserted.RegionCode, inserted.FieldCode, deleted.Caption, deleted.SequenceNo, deleted.Width,
               deleted.DisplayFormat, deleted.IsVisible, inserted.Caption, inserted.SequenceNo, inserted.Width,
               inserted.DisplayFormat, inserted.IsVisible INTO @FC
        FROM MES.FVN_DocumentField t
        JOIN @F f ON t.ModuleCode = @ModuleCode AND t.RegionCode = f.RegionCode AND t.FieldCode = f.FieldCode
        WHERE t.Caption <> LTRIM(RTRIM(f.Caption)) OR t.SequenceNo <> f.SequenceNo
           OR ISNULL(t.Width, -1) <> ISNULL(f.Width, -1)
           OR ISNULL(t.DisplayFormat, N'') <> ISNULL(f.DisplayFormat, N'')
           OR t.IsVisible <> f.IsVisible;
        SET @ChangedCount += @@ROWCOUNT;
        INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, BeforeValue, AfterValue, ActorCode)
        SELECT @ModuleCode, 'FIELD', c.RegionCode + N':' + c.FieldCode, 'UPDATE',
               (SELECT c.OldCaption AS Caption, c.OldSeq AS SequenceNo, c.OldWidth AS Width, c.OldFormat AS DisplayFormat, c.OldVisible AS IsVisible FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
               (SELECT c.NewCaption AS Caption, c.NewSeq AS SequenceNo, c.NewWidth AS Width, c.NewFormat AS DisplayFormat, c.NewVisible AS IsVisible FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
               @ActorCode
        FROM @FC c;

        DECLARE @CC TABLE (CommandCode varchar(80), OldCaption nvarchar(120), OldSeq int, OldVisible bit,
            NewCaption nvarchar(120), NewSeq int, NewVisible bit);
        UPDATE t SET Caption = LTRIM(RTRIM(x.Caption)), SequenceNo = x.SequenceNo, IsVisible = x.IsVisible
        OUTPUT inserted.CommandCode, deleted.Caption, deleted.SequenceNo, deleted.IsVisible,
               inserted.Caption, inserted.SequenceNo, inserted.IsVisible INTO @CC
        FROM MES.FVN_DocumentCommand t
        JOIN @C x ON t.ModuleCode = @ModuleCode AND t.CommandCode = x.CommandCode
        WHERE t.Caption <> LTRIM(RTRIM(x.Caption)) OR t.SequenceNo <> x.SequenceNo OR t.IsVisible <> x.IsVisible;
        SET @ChangedCount += @@ROWCOUNT;
        INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, BeforeValue, AfterValue, ActorCode)
        SELECT @ModuleCode, 'COMMAND', c.CommandCode, 'UPDATE',
               (SELECT c.OldCaption AS Caption, c.OldSeq AS SequenceNo, c.OldVisible AS IsVisible FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
               (SELECT c.NewCaption AS Caption, c.NewSeq AS SequenceNo, c.NewVisible AS IsVisible FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
               @ActorCode
        FROM @CC c;

        DECLARE @LC TABLE (LayoutCode varchar(80), OldCaption nvarchar(160), OldSeq int, OldVisible bit,
            NewCaption nvarchar(160), NewSeq int, NewVisible bit);
        UPDATE t SET Caption = NULLIF(LTRIM(RTRIM(x.Caption)), N''), SequenceNo = x.SequenceNo, IsVisible = x.IsVisible,
                     UpdatedAtUtc = @Now, UpdatedBy = @ActorCode
        OUTPUT inserted.LayoutCode, deleted.Caption, deleted.SequenceNo, deleted.IsVisible,
               inserted.Caption, inserted.SequenceNo, inserted.IsVisible INTO @LC
        FROM MES.FVN_DocumentLayout t
        JOIN @L x ON t.ModuleCode = @ModuleCode AND t.LayoutCode = x.LayoutCode
        WHERE ISNULL(t.Caption, N'') <> ISNULL(LTRIM(RTRIM(x.Caption)), N'') OR t.SequenceNo <> x.SequenceNo OR t.IsVisible <> x.IsVisible;
        SET @ChangedCount += @@ROWCOUNT;
        INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, BeforeValue, AfterValue, ActorCode)
        SELECT @ModuleCode, 'LAYOUT', c.LayoutCode, 'UPDATE',
               (SELECT c.OldCaption AS Caption, c.OldSeq AS SequenceNo, c.OldVisible AS IsVisible FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
               (SELECT c.NewCaption AS Caption, c.NewSeq AS SequenceNo, c.NewVisible AS IsVisible FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
               @ActorCode
        FROM @LC c;

        -- A region must keep at least one visible field, otherwise the document renders as an empty grid.
        IF EXISTS (SELECT 1 FROM MES.FVN_DocumentField WHERE ModuleCode = @ModuleCode GROUP BY RegionCode HAVING SUM(CAST(IsVisible AS int)) = 0)
            THROW 55140, 'Mỗi vùng (HEADER/DETAIL) phải còn ít nhất một cột hiển thị.', 1;

        SET @NewVersion = @Version;
        IF @ChangedCount > 0
        BEGIN
            SET @NewVersion = @Version + 1;
            UPDATE MES.FVN_DocumentDefinition
            SET ConfigVersion = @NewVersion, UpdatedAtUtc = @Now, UpdatedBy = @ActorCode
            WHERE ModuleCode = @ModuleCode;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @NewVersion AS ConfigVersion, @ChangedCount AS ChangedCount;
END;
GO

/* ---------- Provision: create the definition, or only the missing rows, for a document registered in C# ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentMetadata_Provision', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentMetadata_Provision;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentMetadata_Provision
    @ModuleCode varchar(80),
    @HeaderView sysname,
    @DetailView sysname,
    @WorkflowCode varchar(80),
    @DisplayName nvarchar(160),
    @ActorUserId bigint,
    @ActorCode nvarchar(100),
    @FieldsJson nvarchar(max),    -- [{RegionCode,FieldCode,EditorCode,Caption,SequenceNo,Width,DisplayFormat,IsVisible}]
    @CommandsJson nvarchar(max)   -- [{CommandCode,Caption,SequenceNo,IsVisible}]
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
       OR MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'APPROVE') = 0
        THROW 55150, 'Không có quyền APPROVE trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 55151, 'ActorCode is required.', 1;
    IF @ModuleCode IS NULL OR @ModuleCode COLLATE Latin1_General_BIN LIKE '%[^A-Z0-9_]%' OR LEN(@ModuleCode) = 0
        THROW 55152, 'ModuleCode must be upper-case letters, digits and underscore.', 1;
    IF @HeaderView NOT LIKE 'MES.vw[_]FVN[_]%' OR @DetailView NOT LIKE 'MES.vw[_]FVN[_]%'
       OR @HeaderView COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_.]%' OR @DetailView COLLATE Latin1_General_BIN LIKE '%[^A-Za-z0-9_.]%'
        THROW 55153, 'Document views must be MES.vw_FVN_* objects.', 1;
    IF ISNULL(OBJECTPROPERTY(OBJECT_ID(@HeaderView), 'IsView'), 0) <> 1 OR ISNULL(OBJECTPROPERTY(OBJECT_ID(@DetailView), 'IsView'), 0) <> 1
        THROW 55154, 'Header/detail view does not exist as a view.', 1;
    IF NULLIF(LTRIM(RTRIM(@DisplayName)), N'') IS NULL THROW 55155, 'DisplayName is required.', 1;
    IF @WorkflowCode IS NULL OR @WorkflowCode COLLATE Latin1_General_BIN LIKE '%[^A-Z0-9_]%' OR LEN(@WorkflowCode) = 0
        THROW 55156, 'WorkflowCode must be upper-case letters, digits and underscore (NONE when not workflow-driven).', 1;
    IF @FieldsJson IS NULL OR ISJSON(@FieldsJson) <> 1 OR @CommandsJson IS NULL OR ISJSON(@CommandsJson) <> 1
        THROW 55157, 'Payload must be valid JSON.', 1;

    DECLARE @F TABLE (RegionCode varchar(30) NOT NULL, FieldCode varchar(100) NOT NULL, EditorCode varchar(40) NOT NULL,
        Caption nvarchar(160) NOT NULL, SequenceNo int NOT NULL, Width int NULL, DisplayFormat nvarchar(80) NULL, IsVisible bit NOT NULL);
    DECLARE @C TABLE (CommandCode varchar(80) NOT NULL, Caption nvarchar(120) NOT NULL, SequenceNo int NOT NULL, IsVisible bit NOT NULL);
    INSERT @F
    SELECT UPPER(RegionCode), FieldCode, UPPER(EditorCode), Caption, SequenceNo, Width, DisplayFormat, IsVisible
    FROM OPENJSON(@FieldsJson) WITH (
        RegionCode varchar(30) '$.RegionCode', FieldCode varchar(100) '$.FieldCode', EditorCode varchar(40) '$.EditorCode',
        Caption nvarchar(160) '$.Caption', SequenceNo int '$.SequenceNo', Width int '$.Width',
        DisplayFormat nvarchar(80) '$.DisplayFormat', IsVisible bit '$.IsVisible');
    INSERT @C
    SELECT CommandCode, Caption, SequenceNo, IsVisible
    FROM OPENJSON(@CommandsJson) WITH (
        CommandCode varchar(80) '$.CommandCode', Caption nvarchar(120) '$.Caption',
        SequenceNo int '$.SequenceNo', IsVisible bit '$.IsVisible');

    IF NOT EXISTS (SELECT 1 FROM @F) OR NOT EXISTS (SELECT 1 FROM @C) THROW 55158, 'At least one field and one command are required.', 1;
    IF EXISTS (SELECT 1 FROM @F WHERE RegionCode NOT IN ('HEADER', 'DETAIL')
                OR EditorCode NOT IN ('TEXT','NUMBER','DATE','DATETIME','CHECK','LOOKUP','MEMO','BARCODE','QR')
                OR LEN(LTRIM(RTRIM(Caption))) = 0 OR SequenceNo NOT BETWEEN 0 AND 10000
                OR (Width IS NOT NULL AND Width NOT BETWEEN 20 AND 2000)
                OR (DisplayFormat IS NOT NULL AND (LEN(DisplayFormat) > 40
                     OR DisplayFormat COLLATE Latin1_General_BIN LIKE '%[^a-zA-Z0-9 #,.:/%-]%')))
        THROW 55159, 'Invalid provisioned field.', 1;
    IF EXISTS (SELECT 1 FROM @F GROUP BY RegionCode, FieldCode HAVING COUNT(*) > 1)
       OR EXISTS (SELECT 1 FROM @C GROUP BY CommandCode HAVING COUNT(*) > 1)
        THROW 55160, 'Duplicate item in provision payload.', 1;
    IF EXISTS (SELECT 1 FROM @C WHERE LEN(LTRIM(RTRIM(Caption))) = 0 OR SequenceNo NOT BETWEEN 0 AND 10000)
        THROW 55161, 'Invalid provisioned command.', 1;
    IF EXISTS (SELECT 1 FROM @F f WHERE NOT EXISTS (SELECT 1 FROM sys.columns c
               WHERE c.object_id = OBJECT_ID(CASE f.RegionCode WHEN 'HEADER' THEN @HeaderView ELSE @DetailView END)
                 AND c.name = f.FieldCode))
        THROW 55162, 'Field is not a column of the registered view.', 1;

    DECLARE @Now datetime2(3) = SYSUTCDATETIME(), @Added int = 0, @Version int, @Created bit = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ExistingHeader sysname, @ExistingDetail sysname, @ExistingWorkflow varchar(80);
        SELECT @Version = ConfigVersion, @ExistingHeader = HeaderView, @ExistingDetail = DetailView, @ExistingWorkflow = WorkflowCode
        FROM MES.FVN_DocumentDefinition WITH (UPDLOCK, HOLDLOCK) WHERE ModuleCode = @ModuleCode;

        IF @Version IS NULL
        BEGIN
            INSERT MES.FVN_DocumentDefinition(ModuleCode, HeaderView, DetailView, WorkflowCode, DisplayName, UpdatedBy)
            VALUES (@ModuleCode, @HeaderView, @DetailView, @WorkflowCode, LTRIM(RTRIM(@DisplayName)), @ActorCode);
            SET @Version = 1; SET @Created = 1;
            INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, AfterValue, ActorCode)
            VALUES (@ModuleCode, 'DEFINITION', @ModuleCode, 'PROVISION',
                    (SELECT @HeaderView AS HeaderView, @DetailView AS DetailView, @WorkflowCode AS WorkflowCode FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                    @ActorCode);
        END
        ELSE IF @ExistingHeader <> @HeaderView OR @ExistingDetail <> @DetailView OR @ExistingWorkflow <> @WorkflowCode
            THROW 55163, 'Existing definition has a different view/workflow binding than the code registry; fix it by migration, not by the designer.', 1;

        INSERT MES.FVN_DocumentField(ModuleCode, FieldCode, RegionCode, Caption, SequenceNo, Width, DisplayFormat, EditorCode, IsVisible, IsReadOnly)
        SELECT @ModuleCode, f.FieldCode, f.RegionCode, LTRIM(RTRIM(f.Caption)), f.SequenceNo, f.Width, NULLIF(f.DisplayFormat, N''), f.EditorCode, f.IsVisible, 1
        FROM @F f
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentField t WHERE t.ModuleCode = @ModuleCode AND t.RegionCode = f.RegionCode AND t.FieldCode = f.FieldCode);
        SET @Added += @@ROWCOUNT;

        INSERT MES.FVN_DocumentCommand(ModuleCode, CommandCode, Caption, SequenceNo, IsVisible)
        SELECT @ModuleCode, x.CommandCode, LTRIM(RTRIM(x.Caption)), x.SequenceNo, x.IsVisible
        FROM @C x
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentCommand t WHERE t.ModuleCode = @ModuleCode AND t.CommandCode = x.CommandCode);
        SET @Added += @@ROWCOUNT;

        INSERT MES.FVN_DocumentLayout(ModuleCode, LayoutCode, RegionCode, Caption, SequenceNo, IsVisible, IsReadOnly, UpdatedBy)
        SELECT @ModuleCode, v.LayoutCode, v.LayoutCode, LTRIM(RTRIM(@DisplayName)), v.SequenceNo, 1, 1, @ActorCode
        FROM (VALUES ('HEADER', 10), ('DETAIL', 20)) v(LayoutCode, SequenceNo)
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentLayout t WHERE t.ModuleCode = @ModuleCode AND t.LayoutCode = v.LayoutCode);
        SET @Added += @@ROWCOUNT;

        IF @Added > 0 AND @Created = 0
        BEGIN
            SET @Version = @Version + 1;
            UPDATE MES.FVN_DocumentDefinition SET ConfigVersion = @Version, UpdatedAtUtc = @Now, UpdatedBy = @ActorCode WHERE ModuleCode = @ModuleCode;
            INSERT MES.FVN_DocumentConfigAudit(ModuleCode, ConfigObject, ConfigKey, ActionCode, AfterValue, ActorCode)
            VALUES (@ModuleCode, 'DEFINITION', @ModuleCode, 'COMPLETE',
                    (SELECT @Added AS AddedRows FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), @ActorCode);
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT @Version AS ConfigVersion, @Added AS ChangedCount;
END;
GO

/* ---------- Audit history ---------- */
IF OBJECT_ID(N'MES.usp_FVN_DocumentMetadata_ListAudit', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_DocumentMetadata_ListAudit;
GO
CREATE PROCEDURE MES.usp_FVN_DocumentMetadata_ListAudit
    @ModuleCode varchar(80),
    @ActorUserId bigint,
    @Top int = 200
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 55170, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF @Top IS NULL OR @Top < 1 OR @Top > 1000 SET @Top = 200;
    SELECT TOP (@Top) OccurredAtUtc, ConfigObject, ConfigKey, ActionCode, ActorCode, BeforeValue, AfterValue
    FROM MES.FVN_DocumentConfigAudit
    WHERE ModuleCode = @ModuleCode
    ORDER BY ConfigAuditId DESC;
END;
GO
