/*
 Migration 057: UI Layout - catalogue of approved screens for the shell menu.

 Returns only screens that are active, whose main data source is still approved/active and for which the actor
 holds VIEW on the source's function code (and on every active child source). The list only DRIVES THE MENU:
 opening a screen still runs usp_FVN_UiScreen_Load (VIEW check) and the C# Site-scope check, so this procedure
 grants nothing. A screen exists only because a holder of APPROVE on ADMIN.DOCUMENT_DESIGNER created it from an
 approved data source (usp_FVN_UiScreen_Create); there is no code path from this list to arbitrary SQL or code.
 Apply after migration 056.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57000, 'Migration 057 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 57001, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '056')
    THROW 57002, 'Migration 056 must be applied before 057.', 1;
IF OBJECT_ID(N'MES.FVN_UiScreen', N'U') IS NULL OR OBJECT_ID(N'MES.fn_FVN_UiHasFunctionAction', N'FN') IS NULL
    THROW 57003, 'UI layout objects are missing. Apply migration 056 first.', 1;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiScreen_List', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_UiScreen_List;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreen_List @ActorUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SELECT sc.ScreenCode, sc.Title, sc.SourceCode, s.ViewFunctionCode
    FROM MES.FVN_UiScreen AS sc
    INNER JOIN MES.FVN_UiDataSource AS s ON s.SourceCode = sc.SourceCode AND s.IsActive = 1
    WHERE sc.IsActive = 1
      AND MES.fn_FVN_UiHasFunctionAction(@ActorUserId, s.ViewFunctionCode, 'VIEW') = 1
      AND NOT EXISTS (SELECT 1
                      FROM MES.FVN_UiScreenChild AS ch
                      INNER JOIN MES.FVN_UiDataSource AS cs ON cs.SourceCode = ch.SourceCode AND cs.IsActive = 1
                      WHERE ch.ScreenCode = sc.ScreenCode
                        AND MES.fn_FVN_UiHasFunctionAction(@ActorUserId, cs.ViewFunctionCode, 'VIEW') = 0)
    ORDER BY sc.Title, sc.ScreenCode;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '057')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('057', N'PCTP UI Layout: approved screen catalogue for the shell menu');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
