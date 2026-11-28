/*
 Migration 076: unify Site / Production Line / Department master data across the IFS, dbo and MES schemas.

 The same business words live in three schemas on purpose; what was missing were the links and one place to see drift:
   Site        IFS.FVN_Site              = IFS mirror (identity, name, presence in IFS)      -> source of truth for the NAME
               dbo.FVN_PCTPSite          = PCTP allow-list (activation only, ~40 FKs)        -> source of truth for ACTIVATION
   Line        IFS.FVN_ProductionLine    = IFS mirror (long IFS code)                        -> source of truth for the IFS LINE
               dbo.FVN_ProductionLine    = LOT line master (3-char LotLineCode)
               MES.FVN_ProductionLotCodeMap = bridge (Site + IFS line -> LOT line + department)
   Department  dbo.FVN_ProductionDepartment = PCTP department master (4-char), linked to IFS.FVN_WorkCenter by WorkCenterNo
 Nothing is dropped or merged: the mirrors are re-synchronised from IFS and the masters are referenced by ~40 tables.

 This migration only:
   1. fills an empty dbo.FVN_PCTPSite.SiteName from IFS.FVN_Site (never overwrites a name that is set);
   2. adds dbo.vFVN_Site: one row per site with the effective name, activation and IFS presence;
   3. adds the missing link MES.FVN_ProductionLotCodeMap(Site, PlanProductionLineCode) -> IFS.FVN_ProductionLine
      (WITH NOCHECK: legacy rows stay readable, new or re-keyed rows must name a synchronised IFS line);
   4. adds MES.vw_FVN_MasterDataConsistency: every remaining disagreement between the three schemas, one row each;
   5. adds MES.usp_FVN_MasterData_Reconcile: preview by default; @Apply = 1 only creates missing LOT line / department
      rows implied by active mappings and fills empty site names; stale mappings are deactivated only with
      @DeactivateStaleMappings = 1. It never deletes.
 SQL Server 2016+. Apply after 067, 068 and 075.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57600, 'Migration 076 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_Site', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_ProductionLine', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_WorkCenter', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FVN_PCTPSite', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FVN_ProductionLine', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FVN_ProductionDepartment', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_ProductionLotCodeMap', N'U') IS NULL
    THROW 57601, 'Required site / line / department objects are missing. Apply migrations 067, 068 and 075 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '067')
   OR NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '068')
   OR NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '075')
    THROW 57602, 'Migrations 067, 068 and 075 must be registered before 076.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '076')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* 1. One site name: IFS is the source; PCTP only keeps a name when IFS has none. */
    UPDATE p
       SET SiteName = LEFT(LTRIM(RTRIM(fs.SiteName)), 120)
      FROM dbo.FVN_PCTPSite AS p
      JOIN IFS.FVN_Site AS fs ON fs.SiteCode = p.SiteCode AND fs.IsPresentInIfs = 1
     WHERE NULLIF(LTRIM(RTRIM(p.SiteName)), N'') IS NULL
       AND NULLIF(LTRIM(RTRIM(fs.SiteName)), N'') IS NOT NULL;

    /* 3. Bridge -> IFS line link (new / re-keyed rows only; mirrors are soft-deactivated, never deleted). */
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_ProductionLotCodeMap_IfsLine')
        EXEC(N'ALTER TABLE MES.FVN_ProductionLotCodeMap WITH NOCHECK
               ADD CONSTRAINT FK_FVN_ProductionLotCodeMap_IfsLine
               FOREIGN KEY (SiteCode, PlanProductionLineCode)
               REFERENCES IFS.FVN_ProductionLine (SiteCode, ProductionLineCode);');

    /* 2. dbo.vFVN_Site */
    IF OBJECT_ID(N'dbo.vFVN_Site', N'V') IS NOT NULL DROP VIEW dbo.vFVN_Site;
    EXEC(N'
    CREATE VIEW dbo.vFVN_Site
    AS
    SELECT k.SiteCode,
           COALESCE(NULLIF(LTRIM(RTRIM(fs.SiteName)), N''''), NULLIF(LTRIM(RTRIM(ps.SiteName)), N'''')) AS SiteName,
           fs.CompanyCode,
           CONVERT(bit, CASE WHEN ps.SiteCode IS NULL THEN 0 ELSE 1 END) AS IsAllowListed,
           CONVERT(bit, ISNULL(ps.IsActive, 0)) AS IsActive,
           CONVERT(bit, ISNULL(fs.IsPresentInIfs, 0)) AS IsPresentInIfs,
           CASE WHEN ps.SiteCode IS NULL THEN ''NOT_ALLOW_LISTED''
                WHEN ps.IsActive = 0 THEN ''DISABLED''
                WHEN ISNULL(fs.IsPresentInIfs, 0) = 0 THEN ''NOT_IN_IFS''
                ELSE ''OK'' END AS SiteStatus,
           fs.SyncedAtUtc AS IfsSyncedAtUtc
    FROM (SELECT SiteCode FROM dbo.FVN_PCTPSite UNION SELECT SiteCode FROM IFS.FVN_Site) AS k
    LEFT JOIN dbo.FVN_PCTPSite AS ps ON ps.SiteCode = k.SiteCode
    LEFT JOIN IFS.FVN_Site AS fs ON fs.SiteCode = k.SiteCode;');

    /* 4. Consistency view: one row per disagreement. */
    IF OBJECT_ID(N'MES.vw_FVN_MasterDataConsistency', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_MasterDataConsistency;
    EXEC(N'
    CREATE VIEW MES.vw_FVN_MasterDataConsistency
    AS
    SELECT CONVERT(varchar(40), ''SITE_NOT_IN_IFS'') AS IssueCode, CONVERT(varchar(10), ''WARN'') AS Severity,
           p.SiteCode, CONVERT(nvarchar(200), p.SiteCode) AS ObjectKey,
           CONVERT(nvarchar(400), N''Site đang hoạt động trong dbo.FVN_PCTPSite nhưng không có trong IFS.FVN_Site (chưa đồng bộ SITE hoặc đã bị IFS bỏ).'') AS Detail
    FROM dbo.FVN_PCTPSite AS p
    WHERE p.IsActive = 1
      AND NOT EXISTS (SELECT 1 FROM IFS.FVN_Site AS s WHERE s.SiteCode = p.SiteCode AND s.IsPresentInIfs = 1)
    UNION ALL
    SELECT ''SITE_NAME_MISMATCH'', ''INFO'', p.SiteCode, p.SiteCode,
           N''Tên site khác nhau: PCTP="'' + p.SiteName + N''" / IFS="'' + s.SiteName + N''". dbo.vFVN_Site hiển thị tên IFS.''
    FROM dbo.FVN_PCTPSite AS p
    JOIN IFS.FVN_Site AS s ON s.SiteCode = p.SiteCode
    WHERE NULLIF(LTRIM(RTRIM(p.SiteName)), N'''') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(s.SiteName)), N'''') IS NOT NULL
      AND LTRIM(RTRIM(p.SiteName)) <> LTRIM(RTRIM(s.SiteName))
    UNION ALL
    SELECT ''LINE_SITE_NOT_ALLOW_LISTED'', ''ERROR'', l.SiteCode, l.SiteCode + N''/'' + l.LineCode,
           N''dbo.FVN_ProductionLine thuộc Site chưa khai báo trong dbo.FVN_PCTPSite.''
    FROM dbo.FVN_ProductionLine AS l
    WHERE NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite AS p WHERE p.SiteCode = l.SiteCode)
    UNION ALL
    SELECT ''MAP_IFS_LINE_MISSING'', ''ERROR'', m.SiteCode, m.SiteCode + N''/'' + m.PlanProductionLineCode,
           N''Mapping đang hoạt động nhưng chuyền không có trong IFS.FVN_ProductionLine của Site.''
    FROM MES.FVN_ProductionLotCodeMap AS m
    WHERE m.IsActive = 1
      AND NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine AS l WHERE l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode)
    UNION ALL
    SELECT ''MAP_IFS_LINE_INACTIVE'', ''WARN'', m.SiteCode, m.SiteCode + N''/'' + m.PlanProductionLineCode,
           N''Mapping đang hoạt động nhưng chuyền IFS đã ngừng (IFS.FVN_ProductionLine.IsActive = 0).''
    FROM MES.FVN_ProductionLotCodeMap AS m
    JOIN IFS.FVN_ProductionLine AS l ON l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode
    WHERE m.IsActive = 1 AND l.IsActive = 0
    UNION ALL
    SELECT ''MAP_LOT_LINE_MISSING'', ''ERROR'', m.SiteCode, m.SiteCode + N''/'' + m.PlanProductionLineCode,
           N''Mapping trỏ tới mã chuyền LOT '' + m.LotLineCode + N'' chưa có trong dbo.FVN_ProductionLine.''
    FROM MES.FVN_ProductionLotCodeMap AS m
    WHERE m.IsActive = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine AS l WHERE l.SiteCode = m.SiteCode AND l.LineCode = m.LotLineCode)
    UNION ALL
    SELECT ''MAP_DEPARTMENT_MISSING'', ''ERROR'', m.SiteCode, m.SiteCode + N''/'' + m.PlanProductionLineCode,
           N''Mapping trỏ tới bộ phận '' + m.DepartmentCode + N'' chưa có trong dbo.FVN_ProductionDepartment.''
    FROM MES.FVN_ProductionLotCodeMap AS m
    WHERE m.IsActive = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment AS d WHERE d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode)
    UNION ALL
    SELECT ''MAP_DEPARTMENT_OTHER_LINE'', ''ERROR'', m.SiteCode, m.SiteCode + N''/'' + m.PlanProductionLineCode,
           N''Bộ phận '' + m.DepartmentCode + N'' thuộc chuyền LOT '' + d.LineCode + N'', mapping lại ghi '' + m.LotLineCode + N''.''
    FROM MES.FVN_ProductionLotCodeMap AS m
    JOIN dbo.FVN_ProductionDepartment AS d ON d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode
    WHERE m.IsActive = 1 AND d.LineCode <> m.LotLineCode
    UNION ALL
    SELECT ''DEPT_WORKCENTER_NOT_IN_IFS'', ''WARN'', d.SiteCode, d.SiteCode + N''/'' + d.DepartmentCode,
           N''Bộ phận liên kết Work Center '' + d.WorkCenterNo + N'' không còn trong IFS.FVN_WorkCenter.''
    FROM dbo.FVN_ProductionDepartment AS d
    WHERE d.WorkCenterNo IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM IFS.FVN_WorkCenter AS w
                      WHERE w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo AND w.IsPresentInIfs = 1)
    UNION ALL
    SELECT ''DEPT_WORKCENTER_LINE_MISMATCH'', ''INFO'', d.SiteCode, d.SiteCode + N''/'' + d.DepartmentCode,
           N''Work Center '' + d.WorkCenterNo + N'' thuộc chuyền IFS '' + w.IfsProductionLine + N'', bộ phận đang thuộc chuyền LOT '' + d.LineCode + N''.''
    FROM dbo.FVN_ProductionDepartment AS d
    JOIN IFS.FVN_WorkCenter AS w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
    WHERE LEN(w.IfsProductionLine) = 3 AND w.IfsProductionLine <> d.LineCode;');

    /* 5. Reconcile: preview unless @Apply = 1; additive repairs only. */
    IF OBJECT_ID(N'MES.usp_FVN_MasterData_Reconcile', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_MasterData_Reconcile;
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_MasterData_Reconcile
        @Apply bit = 0,
        @DeactivateStaleMappings bit = 0,
        @ActorCode nvarchar(100) = NULL
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        SET @Apply = ISNULL(@Apply, 0);
        SET @DeactivateStaleMappings = ISNULL(@DeactivateStaleMappings, 0);
        IF (@Apply = 1 OR @DeactivateStaleMappings = 1) AND NULLIF(LTRIM(RTRIM(@ActorCode)), N'''') IS NULL
            THROW 57610, ''ActorCode is required when @Apply or @DeactivateStaleMappings is 1.'', 1;

        DECLARE @SiteNames int = 0, @Lines int = 0, @Depts int = 0, @Stale int = 0;

        IF @Apply = 1 OR @DeactivateStaleMappings = 1
        BEGIN
            BEGIN TRY
                BEGIN TRANSACTION;

                IF @Apply = 1
                BEGIN
                    UPDATE p
                       SET SiteName = LEFT(LTRIM(RTRIM(fs.SiteName)), 120)
                      FROM dbo.FVN_PCTPSite AS p
                      JOIN IFS.FVN_Site AS fs ON fs.SiteCode = p.SiteCode AND fs.IsPresentInIfs = 1
                     WHERE NULLIF(LTRIM(RTRIM(p.SiteName)), N'''') IS NULL
                       AND NULLIF(LTRIM(RTRIM(fs.SiteName)), N'''') IS NOT NULL;
                    SET @SiteNames = @@ROWCOUNT;

                    /* LOT line master rows implied by active mappings. */
                    INSERT dbo.FVN_ProductionLine (SiteCode, LineCode, LineName, IsActive)
                    SELECT m.SiteCode, m.LotLineCode,
                           LEFT(ISNULL(NULLIF(MAX(l.Description), N''''), MAX(m.PlanProductionLineCode)), 120), 1
                      FROM MES.FVN_ProductionLotCodeMap AS m
                      LEFT JOIN IFS.FVN_ProductionLine AS l
                        ON l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode
                     WHERE m.IsActive = 1
                       AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine AS x
                                       WHERE x.SiteCode = m.SiteCode AND x.LineCode = m.LotLineCode)
                     GROUP BY m.SiteCode, m.LotLineCode;
                    SET @Lines = @@ROWCOUNT;

                    /* Departments implied by active mappings, only when the code is unambiguous (one LOT line). */
                    INSERT dbo.FVN_ProductionDepartment (SiteCode, DepartmentCode, DepartmentName, LineCode, IsActive)
                    SELECT m.SiteCode, m.DepartmentCode, N''Bộ phận '' + m.DepartmentCode, MIN(m.LotLineCode), 1
                      FROM MES.FVN_ProductionLotCodeMap AS m
                     WHERE m.IsActive = 1
                       AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment AS d
                                       WHERE d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode)
                     GROUP BY m.SiteCode, m.DepartmentCode
                    HAVING COUNT(DISTINCT m.LotLineCode) = 1;
                    SET @Depts = @@ROWCOUNT;
                END;

                IF @DeactivateStaleMappings = 1
                BEGIN
                    UPDATE m
                       SET IsActive = 0
                      FROM MES.FVN_ProductionLotCodeMap AS m
                     WHERE m.IsActive = 1
                       AND NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine AS l
                                       WHERE l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode
                                         AND l.IsActive = 1);
                    SET @Stale = @@ROWCOUNT;
                END;

                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH
        END;

        SELECT @Apply AS Applied, @DeactivateStaleMappings AS StaleDeactivationRequested,
               @SiteNames AS SiteNamesFilled, @Lines AS LotLinesCreated, @Depts AS DepartmentsCreated,
               @Stale AS StaleMappingsDeactivated;
        SELECT IssueCode, Severity, SiteCode, ObjectKey, Detail
          FROM MES.vw_FVN_MasterDataConsistency
         ORDER BY CASE Severity WHEN ''ERROR'' THEN 0 WHEN ''WARN'' THEN 1 ELSE 2 END, IssueCode, SiteCode, ObjectKey;
    END');

    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('076', N'MES master data unification: site / line / department views, bridge FK and reconcile');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '076';
GO
