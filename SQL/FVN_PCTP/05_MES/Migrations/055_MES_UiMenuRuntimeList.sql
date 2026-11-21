/*
 Migration 055: PCTP UI Designer - runtime menu list for the shell.

 The shell reads presentation menu placement for signed-in users. This procedure returns only menu presentation
 columns for active, non-quarantined definitions. The C# shell still filters by its Trusted Registry and the
 registered screen's VIEW permission. This procedure does not grant screen access.
 Apply after migration 054.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 55500, 'Migration 055 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 55501, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '054')
    THROW 55502, 'Migration 054 must be applied before 055.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentMenu', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_UiScreenQuarantine', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_DocumentDefinition', N'U') IS NULL
    THROW 55503, 'Menu objects are missing. Apply migration 053/054 first.', 1;
GO

IF OBJECT_ID(N'MES.usp_FVN_UiScreenMenu_Runtime', N'P') IS NOT NULL
    DROP PROCEDURE MES.usp_FVN_UiScreenMenu_Runtime;
GO
CREATE PROCEDURE MES.usp_FVN_UiScreenMenu_Runtime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.ModuleCode, m.MenuGroup, m.MenuText, m.SequenceNo, m.IsActive
    FROM MES.FVN_DocumentMenu AS m
    INNER JOIN MES.FVN_DocumentDefinition AS d
        ON d.ModuleCode = m.ModuleCode AND d.IsActive = 1
    LEFT JOIN MES.FVN_UiScreenQuarantine AS q ON q.ModuleCode = m.ModuleCode
    WHERE q.ModuleCode IS NULL
    ORDER BY CASE m.MenuGroup
        WHEN 'NHAPKHO' THEN 1 WHEN 'KHO' THEN 2 WHEN 'GIAOHANG' THEN 3
        WHEN 'BAOCAO' THEN 4 WHEN 'CONGCU' THEN 5 ELSE 99 END,
        m.SequenceNo, m.ModuleCode;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '055')
        INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
        VALUES ('055', N'PCTP UI Designer: runtime menu list for the shell');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO