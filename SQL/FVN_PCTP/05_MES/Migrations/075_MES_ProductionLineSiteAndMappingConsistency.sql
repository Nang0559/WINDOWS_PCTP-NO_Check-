/*
 Migration 075:
 - Make IFS site/line source explicit in the production-line setup view.
 - Require IFS Site + IFS Production Line + MES LOT mapping at upload SQL boundary.
 - Require IFS Site presence when activating a line mapping.
 Keys:
   IFS line = (SiteCode, ProductionLineCode)
   MES map  = (SiteCode, PlanProductionLineCode)
 dbo.FVN_ProductionLine remains the 3-character LOT-line master; it is NOT an IFS mirror.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57500, 'Migration 075 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_Site', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_ProductionLine', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_ProductionLotCodeMap', N'U') IS NULL
    THROW 57501, 'Required IFS site/line or MES mapping objects are missing. Apply migrations 067 and 068 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '067')
   OR NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '068')
    THROW 57502, 'Migrations 067 and 068 must be registered before 075.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '075')
    RETURN;

/* Setup view: every IFS line appears, even when no LOT mapping exists.
   A stale mapping is also shown so it can be reviewed/deactivated. */
DECLARE @ViewDefinition nvarchar(max) = N'CREATE VIEW MES.vFVN_ProductionLineSetup
AS
SELECT l.SiteCode,
       fs.SiteName,
       CONVERT(bit, ISNULL(fs.IsPresentInIfs, 0)) AS SiteExistsInIfs,
       CONVERT(bit, ISNULL(p.IsActive, 0)) AS SiteIsActive,
       l.ProductionLineCode,
       l.Description,
       l.CalendarId,
       l.TaktTime,
       l.TaktDailyDemand,
       l.DailyOperatingMinutes,
       CONVERT(bit, 1) AS ExistsInIfs,
       CONVERT(bit, CASE WHEN p.SiteCode IS NOT NULL AND p.IsActive = 1 THEN 1 ELSE 0 END) AS IsKnownSite,
       m.LotLineCode,
       m.DepartmentCode,
       CONVERT(bit, CASE WHEN m.PlanProductionLineCode IS NULL THEN 0 ELSE 1 END) AS IsMapped,
       CONVERT(bit, ISNULL(m.IsActive, 0)) AS MapIsActive,
       l.SyncedAtUtc
FROM IFS.FVN_ProductionLine AS l
LEFT JOIN IFS.FVN_Site AS fs ON fs.SiteCode = l.SiteCode
LEFT JOIN dbo.FVN_PCTPSite AS p ON p.SiteCode = l.SiteCode
LEFT JOIN MES.FVN_ProductionLotCodeMap AS m
  ON m.SiteCode = l.SiteCode AND m.PlanProductionLineCode = l.ProductionLineCode
UNION ALL
SELECT m.SiteCode,
       fs.SiteName,
       CONVERT(bit, ISNULL(fs.IsPresentInIfs, 0)),
       CONVERT(bit, ISNULL(p.IsActive, 0)),
       m.PlanProductionLineCode,
       NULL, NULL, NULL, NULL, NULL,
       CONVERT(bit, 0),
       CONVERT(bit, CASE WHEN p.SiteCode IS NOT NULL AND p.IsActive = 1 THEN 1 ELSE 0 END),
       m.LotLineCode,
       m.DepartmentCode,
       CONVERT(bit, 1),
       CONVERT(bit, m.IsActive),
       NULL
FROM MES.FVN_ProductionLotCodeMap AS m
LEFT JOIN IFS.FVN_Site AS fs ON fs.SiteCode = m.SiteCode
LEFT JOIN dbo.FVN_PCTPSite AS p ON p.SiteCode = m.SiteCode
WHERE NOT EXISTS
(
    SELECT 1 FROM IFS.FVN_ProductionLine AS l
    WHERE l.SiteCode = m.SiteCode
      AND l.ProductionLineCode = m.PlanProductionLineCode
);';
IF OBJECT_ID(N'MES.vFVN_ProductionLineSetup', N'V') IS NULL
    EXEC sys.sp_executesql @ViewDefinition;
ELSE
BEGIN
    SET @ViewDefinition = REPLACE(@ViewDefinition, N'CREATE VIEW', N'ALTER VIEW');
    EXEC sys.sp_executesql @ViewDefinition;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ProcPos int, @HeaderPrefix nvarchar(200);

    /* Harden MES mapping activation: IFS Site must also be present, not only the line row. */
    DECLARE @MapDefinition nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'MES.usp_FVN_ProductionLotCodeMap_Save', N'P'));
    IF @MapDefinition IS NULL
        THROW 57503, 'Cannot read MES.usp_FVN_ProductionLotCodeMap_Save.', 1;

    DECLARE @SiteAnchor nvarchar(500) =
        N'IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)';
    DECLARE @SitePos int = CHARINDEX(@SiteAnchor, @MapDefinition);
    IF @SitePos = 0
        THROW 57504, 'Cannot locate the PCTP site validation in the mapping save procedure.', 1;
    IF CHARINDEX(@SiteAnchor, @MapDefinition, @SitePos + LEN(@SiteAnchor)) > 0
        THROW 57505, 'Mapping save procedure contains duplicate site validation anchors; inspect it manually.', 1;

    IF CHARINDEX(N'IFS.FVN_Site WHERE SiteCode = @SiteCode AND IsPresentInIfs = 1', @MapDefinition) = 0
    BEGIN
        SET @MapDefinition = STUFF(@MapDefinition, @SitePos, 0,
            N'IF NOT EXISTS (SELECT 1 FROM IFS.FVN_Site WHERE SiteCode = @SiteCode AND IsPresentInIfs = 1)' + CHAR(13) + CHAR(10) +
            N'            THROW 57511, ''Site is not present in IFS.FVN_Site; synchronize SITE before activating this mapping.'', 1;' + CHAR(13) + CHAR(10) + N'        ');
    END;

    /* Header normalisation that does not depend on how the object was first created: OBJECT_DEFINITION returns the
       text of the last CREATE / ALTER / CREATE OR ALTER, in whatever casing and spacing it was submitted. Keep the
       text from the PROC keyword on and prefix CREATE OR ALTER, which can neither fail with Msg 2714 (object exists)
       nor with Msg 208 (object missing). */
    SET @ProcPos = PATINDEX(N'%PROC%', @MapDefinition COLLATE Latin1_General_100_CI_AS);
    SET @HeaderPrefix = CASE WHEN @ProcPos > 1 THEN UPPER(REPLACE(REPLACE(REPLACE(REPLACE(LEFT(@MapDefinition, @ProcPos - 1), CHAR(13), N''), CHAR(10), N''), CHAR(9), N''), N' ', N'')) END;
    IF @HeaderPrefix IS NULL OR @HeaderPrefix NOT IN (N'CREATE', N'ALTER', N'CREATEORALTER')
        THROW 57506, 'MES.usp_FVN_ProductionLotCodeMap_Save: cannot recognise the procedure header (expected CREATE / ALTER / CREATE OR ALTER PROCEDURE).', 1;
    SET @MapDefinition = STUFF(@MapDefinition, 1, @ProcPos - 1, N'CREATE OR ALTER ');
    EXEC sys.sp_executesql @MapDefinition;

    /* SQL is the final gate: import must use the exact site-scoped IFS line and active MES map. */
    DECLARE @ImportDefinition nvarchar(max) =
        OBJECT_DEFINITION(OBJECT_ID(N'MES.usp_FVN_ProductionPlan_ImportRow', N'P'));
    IF @ImportDefinition IS NULL
        THROW 57507, 'Cannot read MES.usp_FVN_ProductionPlan_ImportRow.', 1;

    DECLARE @ImportAnchor nvarchar(500) =
        N'IF NOT EXISTS(SELECT 1 FROM MES.FVN_ProductionShift WHERE SiteCode=@SiteCode AND ShiftCode=@ShiftCode AND IsActive=1)';
    DECLARE @ImportPos int = CHARINDEX(@ImportAnchor, @ImportDefinition);
    IF @ImportPos = 0
        THROW 57508, 'Cannot locate the production-shift validation in the plan import procedure.', 1;
    IF CHARINDEX(@ImportAnchor, @ImportDefinition, @ImportPos + LEN(@ImportAnchor)) > 0
        THROW 57509, 'Plan import procedure contains duplicate validation anchors; inspect it manually.', 1;

    IF CHARINDEX(N'MES.FVN_ProductionLotCodeMap', @ImportDefinition) = 0
    BEGIN
        SET @ImportDefinition = STUFF(@ImportDefinition, @ImportPos, 0,
            N'IF NOT EXISTS (SELECT 1 FROM IFS.FVN_Site WHERE SiteCode=@SiteCode AND IsPresentInIfs=1)' + CHAR(13) + CHAR(10) +
            N'   THROW 57520, ''Site is not present in IFS.FVN_Site; synchronize SITE before uploading a production plan.'', 1;' + CHAR(13) + CHAR(10) +
            N'  IF NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine WHERE SiteCode=@SiteCode AND ProductionLineCode=@ProductionLineCode AND IsActive=1)' + CHAR(13) + CHAR(10) +
            N'   THROW 57521, ''IFS production line is missing/inactive for this Site. Check (SiteCode, ProductionLineCode).' + CHAR(39) + N', 1;' + CHAR(13) + CHAR(10) +
            N'  IF NOT EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap WHERE SiteCode=@SiteCode AND PlanProductionLineCode=@ProductionLineCode AND IsActive=1 AND LEN(LTRIM(RTRIM(LotLineCode)))=3 AND LEN(LTRIM(RTRIM(DepartmentCode)))=4)' + CHAR(13) + CHAR(10) +
            N'   THROW 57522, ''Production line has no active LOT mapping for this Site. Check MES.FVN_ProductionLotCodeMap (SiteCode + PlanProductionLineCode).'', 1;' + CHAR(13) + CHAR(10));
    END;

    /* Header normalisation that does not depend on how the object was first created: OBJECT_DEFINITION returns the
       text of the last CREATE / ALTER / CREATE OR ALTER, in whatever casing and spacing it was submitted. Keep the
       text from the PROC keyword on and prefix CREATE OR ALTER, which can neither fail with Msg 2714 (object exists)
       nor with Msg 208 (object missing). */
    SET @ProcPos = PATINDEX(N'%PROC%', @ImportDefinition COLLATE Latin1_General_100_CI_AS);
    SET @HeaderPrefix = CASE WHEN @ProcPos > 1 THEN UPPER(REPLACE(REPLACE(REPLACE(REPLACE(LEFT(@ImportDefinition, @ProcPos - 1), CHAR(13), N''), CHAR(10), N''), CHAR(9), N''), N' ', N'')) END;
    IF @HeaderPrefix IS NULL OR @HeaderPrefix NOT IN (N'CREATE', N'ALTER', N'CREATEORALTER')
        THROW 57510, 'MES.usp_FVN_ProductionPlan_ImportRow: cannot recognise the procedure header (expected CREATE / ALTER / CREATE OR ALTER PROCEDURE).', 1;
    SET @ImportDefinition = STUFF(@ImportDefinition, 1, @ProcPos - 1, N'CREATE OR ALTER ');
    EXEC sys.sp_executesql @ImportDefinition;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES('075', N'MES production line IFS site and mapping consistency');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '075';
GO
