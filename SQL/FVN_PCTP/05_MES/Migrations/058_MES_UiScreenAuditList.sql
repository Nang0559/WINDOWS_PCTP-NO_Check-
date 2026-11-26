/*
 Migration 058: UI Layout - read the change history of a screen (audit written by 056).

 Read-only. Requires VIEW on ADMIN.DOCUMENT_DESIGNER (same as opening the designer). Returns the most recent rows of
 MES.FVN_UiScreenAudit for one screen code (layout saves, screen creation) or for the data-source declarations
 (screen code SOURCE:<SourceCode>). It grants nothing and changes nothing; "restore a version" stays a manual
 re-save because the audit keeps counts and versions, not a full snapshot.
 Apply after migration 057.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 58000, 'Migration 058 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 58001, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '057')
    THROW 58002, 'Migration 057 must be applied before 058.', 1;
IF OBJECT_ID(N'MES.FVN_UiScreenAudit', N'U') IS NULL OR OBJECT_ID(N'MES.fn_FVN_DocumentDesigner_HasAction', N'FN') IS NULL
    THROW 58003, 'UI layout objects are missing. Apply migration 056 first.', 1;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiScreen_ListAudit', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreen_ListAudit;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreen_ListAudit @ActorUserId bigint, @ScreenCode varchar(80), @Top int = 100
AS
BEGIN
    SET NOCOUNT ON;
    IF MES.fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0
        THROW 58010, 'Không có quyền VIEW trên ADMIN.DOCUMENT_DESIGNER.', 1;
    IF @ScreenCode IS NULL OR @ScreenCode = '' THROW 58011, 'ScreenCode is required.', 1;
    IF @Top IS NULL OR @Top < 1 OR @Top > 500 SET @Top = 100;
    SELECT TOP (@Top) AuditId, AtUtc, ActionCode, ActorCode, VersionBefore, VersionAfter, DetailJson
    FROM MES.FVN_UiScreenAudit
    WHERE ScreenCode = @ScreenCode
    ORDER BY AuditId DESC;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '058')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('058', N'PCTP UI Layout: screen change history');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
