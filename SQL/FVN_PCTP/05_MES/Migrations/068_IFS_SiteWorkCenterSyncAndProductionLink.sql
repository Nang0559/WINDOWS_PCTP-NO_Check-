/*
 Migration 068: IFS Site + Work Center synchronization and the Site-Department-Line link definition.

 1. IFS mirrors (read-only copies of IFSAPP views, never written back to IFS)
      IFSAPP.SITE         -> IFS.FVN_Site
      IFSAPP.WORK_CENTER  -> IFS.FVN_WorkCenter   (IFS "Work Center" == PCTP production department)
 2. Selective-sync contract: entities SITE / WORK_CENTER, batch counters, and
    IFS.usp_ApplySiteWorkCenterBatch (separate apply; IFS.usp_ApplySyncBatch is NOT modified).
 3. Link definition (Site -> Line -> Department -> IFS Work Center)
      dbo.FVN_ProductionDepartment.WorkCenterNo  (nullable link to the IFS work center)
      UNIQUE(SiteCode, DepartmentCode, LineCode) + FK from MES.FVN_ProductionLotCodeMap so a LOT
      map row can never pair a department with a line it does not belong to
      MES.usp_FVN_SiteDepartmentLine_Link / _Unlink (validated, audited)
      MES.vw_FVN_SiteDepartmentLine, MES.vw_FVN_WorkCenterLinkCandidates, MES.vw_FVN_LotCodeMapIntegrity

 Policy: sync NEVER creates lines/departments or links by itself. LineCode (3) and DepartmentCode (4)
 are part of the 20-character production LOT identity and must be chosen explicitly through the Link
 procedure. IFS Production Line / Department No are exposed only as suggestions.
 Sites are not auto-added to dbo.FVN_PCTPSite (explicit allow-list); only SiteName is refreshed.

 SQL Server 2016 compatible. Apply after 067 (requires 028, 037; 067 adds the PRODUCTION_LINE sync entity this script keeps).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 56700, 'Migration 068 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 56701, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '037')
    THROW 56702, 'Migration 037 (selective IFS sync) must be applied before 068.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '067')
    THROW 56703, 'Migration 067 (PRODUCTION_LINE sync) must be applied before 068.', 1;
IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL OR OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_ProductionLine', N'U') IS NULL
    THROW 56704, 'IFS synchronization objects are missing.', 1;
IF OBJECT_ID(N'dbo.FVN_PCTPSite', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FVN_ProductionLine', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FVN_ProductionDepartment', N'U') IS NULL
   OR OBJECT_ID(N'MES.FVN_ProductionLotCodeMap', N'U') IS NULL
    THROW 56705, 'Site / production line / department / LOT code map tables are required.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '068')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ---------------------------------------------------------------- 1. IFS mirrors */
    IF OBJECT_ID(N'IFS.FVN_Site', N'U') IS NULL
        CREATE TABLE IFS.FVN_Site
        (
            SiteCode nvarchar(30) NOT NULL,
            SiteName nvarchar(250) NULL,
            CompanyCode nvarchar(40) NULL,
            IsPresentInIfs bit NOT NULL CONSTRAINT DF_IFS_FVN_Site_Present DEFAULT(1),
            SourceRowKey nvarchar(200) NULL,
            SourceObjVersion nvarchar(56) NULL,
            SourceHash varbinary(32) NULL,
            SourcePayloadJson nvarchar(max) NULL,
            LastSeenBatchId bigint NULL,
            SyncedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_IFS_FVN_Site_Synced DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT PK_IFS_FVN_Site PRIMARY KEY(SiteCode)
        );

    IF OBJECT_ID(N'IFS.FVN_WorkCenter', N'U') IS NULL
        CREATE TABLE IFS.FVN_WorkCenter
        (
            SiteCode nvarchar(30) NOT NULL,
            WorkCenterNo nvarchar(30) NOT NULL,
            Description nvarchar(250) NULL,
            WorkCenterCode nvarchar(60) NULL,          /* Internal / External work center */
            IfsProductionLine nvarchar(30) NULL,
            IfsProductionLineDescription nvarchar(250) NULL,
            IfsDepartmentNo nvarchar(30) NULL,
            IfsDepartmentDescription nvarchar(250) NULL,
            CostCenterId nvarchar(30) NULL,
            CalendarId nvarchar(30) NULL,
            QueueTime decimal(18,6) NULL,
            Utilization decimal(18,6) NULL,
            AverageCapacity decimal(18,6) NULL,
            IsPresentInIfs bit NOT NULL CONSTRAINT DF_IFS_FVN_WorkCenter_Present DEFAULT(1),
            SourceRowKey nvarchar(200) NULL,
            SourceObjVersion nvarchar(56) NULL,
            SourceHash varbinary(32) NULL,
            SourcePayloadJson nvarchar(max) NULL,
            LastSeenBatchId bigint NULL,
            SyncedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_IFS_FVN_WorkCenter_Synced DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT PK_IFS_FVN_WorkCenter PRIMARY KEY(SiteCode, WorkCenterNo)
        );

    /* -------------------------------------------------- 2. selective-sync contract */
    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'SiteRows') IS NULL
        EXEC(N'ALTER TABLE IFS.FVN_SyncBatch ADD SiteRows int NOT NULL CONSTRAINT DF_IFS_FVN_SyncBatch_SiteRows DEFAULT(0);');
    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'WorkCenterRows') IS NULL
        EXEC(N'ALTER TABLE IFS.FVN_SyncBatch ADD WorkCenterRows int NOT NULL CONSTRAINT DF_IFS_FVN_SyncBatch_WorkCenterRows DEFAULT(0);');

    IF EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncStageRow') AND name = N'CK_IFS_Stage_Entity')
        ALTER TABLE IFS.FVN_SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;
    ALTER TABLE IFS.FVN_SyncStageRow WITH CHECK ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (EntityName IN ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE','BOM_COMPONENT',
                        'WAREHOUSE','SUPPLIER','PRODUCTION_LINE','SITE','WORK_CENTER'));

    IF OBJECT_ID(N'IFS.usp_ApplySiteWorkCenterBatch', N'P') IS NOT NULL
        DROP PROCEDURE IFS.usp_ApplySiteWorkCenterBatch;
    EXEC(N'
    CREATE PROCEDURE IFS.usp_ApplySiteWorkCenterBatch
        @SyncBatchId bigint
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        BEGIN TRY
            BEGIN TRANSACTION;

            DECLARE @Lock int;
            EXEC @Lock = sys.sp_getapplock @Resource = ''IFS_SITE_WORKCENTER_SYNC'', @LockMode = ''Exclusive'',
                @LockOwner = ''Transaction'', @LockTimeout = 0;
            IF @Lock < 0 THROW 56710, ''Another Site/Work Center synchronization is already applying.'', 1;

            IF NOT EXISTS (SELECT 1 FROM IFS.FVN_SyncBatch WITH (UPDLOCK, HOLDLOCK)
                           WHERE SyncBatchId = @SyncBatchId AND Status IN (''STAGED'', ''APPLIED''))
                THROW 56711, ''Sync batch must be STAGED (or already APPLIED) before Site/Work Center apply.'', 1;

            IF EXISTS (SELECT 1 FROM IFS.FVN_SyncStageRow
                       WHERE SyncBatchId = @SyncBatchId AND EntityName IN (''SITE'', ''WORK_CENTER'')
                       GROUP BY EntityName, SiteCode, PartNo HAVING COUNT(*) > 1)
                THROW 56712, ''Duplicate Site/Work Center business key found in staging.'', 1;

            /* A work center row must belong to a site that is in IFS (this batch or an earlier one). */
            IF EXISTS (SELECT 1 FROM IFS.FVN_SyncStageRow w
                       WHERE w.SyncBatchId = @SyncBatchId AND w.EntityName = ''WORK_CENTER''
                         AND NOT EXISTS (SELECT 1 FROM IFS.FVN_SyncStageRow s
                                         WHERE s.SyncBatchId = @SyncBatchId AND s.EntityName = ''SITE'' AND s.PartNo = w.SiteCode)
                         AND NOT EXISTS (SELECT 1 FROM IFS.FVN_Site fs WHERE fs.SiteCode = w.SiteCode))
                THROW 56713, ''Work Center row references a Site that has not been synchronized from IFS.'', 1;

            DECLARE @Seen TABLE (SiteCode nvarchar(30) NOT NULL PRIMARY KEY);

            /* -------- SITE */
            MERGE IFS.FVN_Site AS t
            USING (SELECT s.PartNo AS SiteCode,
                          JSON_VALUE(s.PayloadJson, ''$.DESCRIPTION'') AS SiteName,
                          JSON_VALUE(s.PayloadJson, ''$.COMPANY'') AS CompanyCode,
                          s.SourceRowKey, s.SourceObjVersion, s.SourceHash, s.PayloadJson
                   FROM IFS.FVN_SyncStageRow s
                   WHERE s.SyncBatchId = @SyncBatchId AND s.EntityName = ''SITE'') AS src
               ON t.SiteCode = src.SiteCode
            WHEN MATCHED THEN UPDATE SET
                SiteName = src.SiteName, CompanyCode = src.CompanyCode, IsPresentInIfs = 1,
                SourceRowKey = src.SourceRowKey, SourceObjVersion = src.SourceObjVersion,
                SourceHash = src.SourceHash, SourcePayloadJson = src.PayloadJson,
                LastSeenBatchId = @SyncBatchId, SyncedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN INSERT
                (SiteCode, SiteName, CompanyCode, IsPresentInIfs, SourceRowKey, SourceObjVersion, SourceHash,
                 SourcePayloadJson, LastSeenBatchId)
                VALUES (src.SiteCode, src.SiteName, src.CompanyCode, 1, src.SourceRowKey, src.SourceObjVersion,
                        src.SourceHash, src.PayloadJson, @SyncBatchId);

            /* A full SITE read lets us mark vanished sites; rows are never removed (history / FKs). */
            IF EXISTS (SELECT 1 FROM IFS.FVN_SyncStageRow WHERE SyncBatchId = @SyncBatchId AND EntityName = ''SITE'')
                UPDATE IFS.FVN_Site SET IsPresentInIfs = 0
                WHERE ISNULL(LastSeenBatchId, 0) <> @SyncBatchId AND IsPresentInIfs = 1;

            /* Refresh the display name of allow-listed sites only; the allow-list itself is never extended here. */
            UPDATE p SET SiteName = fs.SiteName
            FROM dbo.FVN_PCTPSite p
            JOIN IFS.FVN_Site fs ON fs.SiteCode = p.SiteCode
            WHERE fs.LastSeenBatchId = @SyncBatchId AND fs.SiteName IS NOT NULL
              AND ISNULL(p.SiteName, N'''') <> fs.SiteName;

            /* -------- WORK_CENTER */
            INSERT @Seen(SiteCode)
            SELECT DISTINCT s.SiteCode FROM IFS.FVN_SyncStageRow s
            WHERE s.SyncBatchId = @SyncBatchId AND s.EntityName = ''WORK_CENTER'';

            MERGE IFS.FVN_WorkCenter AS t
            USING (SELECT s.SiteCode, s.PartNo AS WorkCenterNo,
                          JSON_VALUE(s.PayloadJson, ''$.DESCRIPTION'') AS Description,
                          COALESCE(JSON_VALUE(s.PayloadJson, ''$.WORK_CENTER_CODE''), JSON_VALUE(s.PayloadJson, ''$.WORK_CENTER_CODE_DB'')) AS WorkCenterCode,
                          JSON_VALUE(s.PayloadJson, ''$.PRODUCTION_LINE'') AS IfsProductionLine,
                          COALESCE(JSON_VALUE(s.PayloadJson, ''$.PRODUCTION_LINE_DESCRIPTION''), JSON_VALUE(s.PayloadJson, ''$.PRODUCTION_LINE_DESC'')) AS IfsProductionLineDescription,
                          JSON_VALUE(s.PayloadJson, ''$.DEPARTMENT_NO'') AS IfsDepartmentNo,
                          COALESCE(JSON_VALUE(s.PayloadJson, ''$.DEPARTMENT_DESCRIPTION''), JSON_VALUE(s.PayloadJson, ''$.DEPARTMENT_DESC'')) AS IfsDepartmentDescription,
                          COALESCE(JSON_VALUE(s.PayloadJson, ''$.COST_CENTER_ID''), JSON_VALUE(s.PayloadJson, ''$.COST_CENTER'')) AS CostCenterId,
                          JSON_VALUE(s.PayloadJson, ''$.CALENDAR_ID'') AS CalendarId,
                          TRY_CONVERT(decimal(18,6), JSON_VALUE(s.PayloadJson, ''$.QUEUE_TIME'')) AS QueueTime,
                          TRY_CONVERT(decimal(18,6), JSON_VALUE(s.PayloadJson, ''$.UTILIZATION'')) AS Utilization,
                          TRY_CONVERT(decimal(18,6), JSON_VALUE(s.PayloadJson, ''$.AVERAGE_CAPACITY'')) AS AverageCapacity,
                          s.SourceRowKey, s.SourceObjVersion, s.SourceHash, s.PayloadJson
                   FROM IFS.FVN_SyncStageRow s
                   WHERE s.SyncBatchId = @SyncBatchId AND s.EntityName = ''WORK_CENTER'') AS src
               ON t.SiteCode = src.SiteCode AND t.WorkCenterNo = src.WorkCenterNo
            WHEN MATCHED THEN UPDATE SET
                Description = src.Description, WorkCenterCode = src.WorkCenterCode,
                IfsProductionLine = src.IfsProductionLine, IfsProductionLineDescription = src.IfsProductionLineDescription,
                IfsDepartmentNo = src.IfsDepartmentNo, IfsDepartmentDescription = src.IfsDepartmentDescription,
                CostCenterId = src.CostCenterId, CalendarId = src.CalendarId, QueueTime = src.QueueTime,
                Utilization = src.Utilization, AverageCapacity = src.AverageCapacity, IsPresentInIfs = 1,
                SourceRowKey = src.SourceRowKey, SourceObjVersion = src.SourceObjVersion, SourceHash = src.SourceHash,
                SourcePayloadJson = src.PayloadJson, LastSeenBatchId = @SyncBatchId, SyncedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN INSERT
                (SiteCode, WorkCenterNo, Description, WorkCenterCode, IfsProductionLine, IfsProductionLineDescription,
                 IfsDepartmentNo, IfsDepartmentDescription, CostCenterId, CalendarId, QueueTime, Utilization,
                 AverageCapacity, IsPresentInIfs, SourceRowKey, SourceObjVersion, SourceHash, SourcePayloadJson, LastSeenBatchId)
                VALUES (src.SiteCode, src.WorkCenterNo, src.Description, src.WorkCenterCode, src.IfsProductionLine,
                        src.IfsProductionLineDescription, src.IfsDepartmentNo, src.IfsDepartmentDescription,
                        src.CostCenterId, src.CalendarId, src.QueueTime, src.Utilization, src.AverageCapacity, 1,
                        src.SourceRowKey, src.SourceObjVersion, src.SourceHash, src.PayloadJson, @SyncBatchId);

            /* Work centers that disappeared from IFS for the sites read in this batch: flagged only, never removed. */
            UPDATE w SET IsPresentInIfs = 0
            FROM IFS.FVN_WorkCenter w
            JOIN @Seen z ON z.SiteCode = w.SiteCode
            WHERE ISNULL(w.LastSeenBatchId, 0) <> @SyncBatchId AND w.IsPresentInIfs = 1;

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    /* ------------------------------------------- 3. Site-Department-Line link definition */
    IF COL_LENGTH(N'dbo.FVN_ProductionDepartment', N'WorkCenterNo') IS NULL
        EXEC(N'ALTER TABLE dbo.FVN_ProductionDepartment ADD WorkCenterNo nvarchar(30) NULL;');
    IF COL_LENGTH(N'dbo.FVN_ProductionDepartment', N'LinkedAtUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.FVN_ProductionDepartment ADD LinkedAtUtc datetime2(3) NULL;');
    IF COL_LENGTH(N'dbo.FVN_ProductionDepartment', N'LinkedBy') IS NULL
        EXEC(N'ALTER TABLE dbo.FVN_ProductionDepartment ADD LinkedBy nvarchar(100) NULL;');

    /* One IFS work center belongs to at most one department of the site. */
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_ProductionDepartment')
                   AND name = N'UX_FVN_ProductionDepartment_WorkCenter')
        EXEC(N'CREATE UNIQUE INDEX UX_FVN_ProductionDepartment_WorkCenter
               ON dbo.FVN_ProductionDepartment(SiteCode, WorkCenterNo) WHERE WorkCenterNo IS NOT NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_ProductionDepartment_WorkCenter')
        EXEC(N'ALTER TABLE dbo.FVN_ProductionDepartment WITH CHECK
               ADD CONSTRAINT FK_FVN_ProductionDepartment_WorkCenter
               FOREIGN KEY(SiteCode, WorkCenterNo) REFERENCES IFS.FVN_WorkCenter(SiteCode, WorkCenterNo);');

    /* Lines and departments must belong to an allow-listed site (new/changed rows only: legacy rows stay readable). */
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_ProductionLine_Site')
        EXEC(N'ALTER TABLE dbo.FVN_ProductionLine WITH NOCHECK
               ADD CONSTRAINT FK_FVN_ProductionLine_Site FOREIGN KEY(SiteCode) REFERENCES dbo.FVN_PCTPSite(SiteCode);');

    /* Site + Department + Line is a unique triple so the LOT code map can reference it. */
    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_FVN_ProductionDepartment_SiteDeptLine')
        EXEC(N'ALTER TABLE dbo.FVN_ProductionDepartment
               ADD CONSTRAINT UQ_FVN_ProductionDepartment_SiteDeptLine UNIQUE(SiteCode, DepartmentCode, LineCode);');

    /* A LOT code map row may only pair a department with the line it actually belongs to. */
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FVN_ProductionLotCodeMap_SiteDeptLine')
        EXEC(N'ALTER TABLE MES.FVN_ProductionLotCodeMap WITH NOCHECK
               ADD CONSTRAINT FK_FVN_ProductionLotCodeMap_SiteDeptLine
               FOREIGN KEY(SiteCode, DepartmentCode, LotLineCode)
               REFERENCES dbo.FVN_ProductionDepartment(SiteCode, DepartmentCode, LineCode);');

    IF OBJECT_ID(N'MES.FVN_SiteDepartmentLineAudit', N'U') IS NULL
        CREATE TABLE MES.FVN_SiteDepartmentLineAudit
        (
            AuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_SiteDepartmentLineAudit PRIMARY KEY,
            ActionCode varchar(20) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            DepartmentCode varchar(4) NOT NULL,
            LineCode varchar(3) NULL,
            WorkCenterNo nvarchar(30) NULL,
            PreviousWorkCenterNo nvarchar(30) NULL,
            ActorCode nvarchar(100) NOT NULL,
            OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_SiteDepartmentLineAudit_Date DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT CK_FVN_SiteDepartmentLineAudit_Action CHECK(ActionCode IN ('LINK','RELINK','UNLINK'))
        );

    /* ----------------------------------------------------------------- views */
    IF OBJECT_ID(N'MES.vw_FVN_SiteDepartmentLine', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_SiteDepartmentLine;
    EXEC(N'
    CREATE VIEW MES.vw_FVN_SiteDepartmentLine
    AS
    SELECT d.SiteCode, COALESCE(fs.SiteName, ps.SiteName) AS SiteName, ps.IsActive AS SiteIsActive,
           l.LineCode, l.LineName, l.IsActive AS LineIsActive,
           d.DepartmentCode, d.DepartmentName, d.IsActive AS DepartmentIsActive,
           d.WorkCenterNo, w.Description AS WorkCenterDescription, w.IsPresentInIfs AS WorkCenterPresentInIfs,
           w.IfsProductionLine, w.IfsDepartmentNo,
           CASE WHEN d.WorkCenterNo IS NULL THEN ''NOT_LINKED''
                WHEN w.IsPresentInIfs = 0 THEN ''LINKED_MISSING_IN_IFS''
                ELSE ''LINKED'' END AS LinkStatus,
           d.LinkedAtUtc, d.LinkedBy
    FROM dbo.FVN_ProductionDepartment d
    JOIN dbo.FVN_ProductionLine l ON l.SiteCode = d.SiteCode AND l.LineCode = d.LineCode
    LEFT JOIN dbo.FVN_PCTPSite ps ON ps.SiteCode = d.SiteCode
    LEFT JOIN IFS.FVN_Site fs ON fs.SiteCode = d.SiteCode
    LEFT JOIN IFS.FVN_WorkCenter w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo;');

    IF OBJECT_ID(N'MES.vw_FVN_WorkCenterLinkCandidates', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_WorkCenterLinkCandidates;
    EXEC(N'
    CREATE VIEW MES.vw_FVN_WorkCenterLinkCandidates
    AS
    SELECT w.SiteCode, w.WorkCenterNo, w.Description, w.WorkCenterCode, w.IsPresentInIfs,
           w.IfsProductionLine, w.IfsProductionLineDescription, w.IfsDepartmentNo, w.IfsDepartmentDescription,
           CASE WHEN LEN(w.IfsProductionLine) = 3 THEN w.IfsProductionLine END AS SuggestedLineCode,
           CASE WHEN LEN(w.IfsDepartmentNo) = 4 THEN w.IfsDepartmentNo END AS SuggestedDepartmentCode,
           d.DepartmentCode AS LinkedDepartmentCode, d.LineCode AS LinkedLineCode,
           CASE WHEN d.DepartmentCode IS NULL THEN ''UNLINKED'' ELSE ''LINKED'' END AS LinkStatus,
           CASE WHEN ps.SiteCode IS NULL THEN 0 ELSE 1 END AS SiteIsAllowListed
    FROM IFS.FVN_WorkCenter w
    LEFT JOIN dbo.FVN_ProductionDepartment d ON d.SiteCode = w.SiteCode AND d.WorkCenterNo = w.WorkCenterNo
    LEFT JOIN dbo.FVN_PCTPSite ps ON ps.SiteCode = w.SiteCode AND ps.IsActive = 1;');

    IF OBJECT_ID(N'MES.vw_FVN_LotCodeMapIntegrity', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_LotCodeMapIntegrity;
    EXEC(N'
    CREATE VIEW MES.vw_FVN_LotCodeMapIntegrity
    AS
    SELECT m.SiteCode, m.PlanProductionLineCode, m.LotLineCode, m.DepartmentCode,
           CASE WHEN l.LineCode IS NULL THEN ''LINE_NOT_DEFINED''
                WHEN d.DepartmentCode IS NULL THEN ''DEPARTMENT_NOT_DEFINED''
                WHEN d.LineCode <> m.LotLineCode THEN ''DEPARTMENT_BELONGS_TO_OTHER_LINE''
                WHEN l.IsActive = 0 OR d.IsActive = 0 THEN ''LINE_OR_DEPARTMENT_INACTIVE''
                ELSE ''OK'' END AS IntegrityStatus
    FROM MES.FVN_ProductionLotCodeMap m
    LEFT JOIN dbo.FVN_ProductionLine l ON l.SiteCode = m.SiteCode AND l.LineCode = m.LotLineCode
    LEFT JOIN dbo.FVN_ProductionDepartment d ON d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode
    WHERE m.IsActive = 1;');

    /* ---------------------------------------------------------------- procedures */
    IF OBJECT_ID(N'MES.usp_FVN_SiteDepartmentLine_Link', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link;
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link
        @SiteCode nvarchar(30), @WorkCenterNo nvarchar(30),
        @DepartmentCode varchar(4), @LineCode varchar(3),
        @LineName nvarchar(120) = NULL, @DepartmentName nvarchar(120) = NULL,
        @ActorCode nvarchar(100)
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @WorkCenterNo = LTRIM(RTRIM(@WorkCenterNo));
        SET @DepartmentCode = LTRIM(RTRIM(@DepartmentCode)); SET @LineCode = LTRIM(RTRIM(@LineCode));
        IF NULLIF(@SiteCode, N'''') IS NULL OR NULLIF(@WorkCenterNo, N'''') IS NULL
            THROW 56720, ''SiteCode and WorkCenterNo are required.'', 1;
        IF LEN(ISNULL(@DepartmentCode, '''')) <> 4 THROW 56721, ''DepartmentCode must contain exactly four characters.'', 1;
        IF LEN(ISNULL(@LineCode, '''')) <> 3 THROW 56722, ''LineCode must contain exactly three characters.'', 1;
        IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'''') IS NULL THROW 56723, ''ActorCode is required.'', 1;

        BEGIN TRY
            BEGIN TRANSACTION;

            IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
                THROW 56724, ''Site is not in the active PCTP site allow-list.'', 1;

            DECLARE @WcDescription nvarchar(250);
            SELECT @WcDescription = Description FROM IFS.FVN_WorkCenter WITH (UPDLOCK, HOLDLOCK)
            WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND IsPresentInIfs = 1;
            IF @@ROWCOUNT = 0
                THROW 56725, ''Work center does not exist in the synchronized IFS master for this site.'', 1;

            /* The work center may be linked to only one department. */
            DECLARE @OtherDept varchar(4);
            SELECT @OtherDept = DepartmentCode FROM dbo.FVN_ProductionDepartment
            WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND DepartmentCode <> @DepartmentCode;
            IF @OtherDept IS NOT NULL
                THROW 56726, ''This work center is already linked to another department; unlink it first.'', 1;

            /* Line: reuse the existing one, or create it (name required). */
            IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WITH (UPDLOCK, HOLDLOCK)
                           WHERE SiteCode = @SiteCode AND LineCode = @LineCode)
            BEGIN
                IF NULLIF(LTRIM(RTRIM(@LineName)), N'''') IS NULL
                    THROW 56727, ''Line does not exist for this site; LineName is required to create it.'', 1;
                INSERT dbo.FVN_ProductionLine(SiteCode, LineCode, LineName, IsActive)
                VALUES (@SiteCode, @LineCode, LTRIM(RTRIM(@LineName)), 1);
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND LineCode = @LineCode AND IsActive = 1)
                THROW 56728, ''Line is inactive.'', 1;

            DECLARE @ExistingLine varchar(3), @ExistingWc nvarchar(30), @Exists bit = 0;
            SELECT @ExistingLine = LineCode, @ExistingWc = WorkCenterNo, @Exists = 1
            FROM dbo.FVN_ProductionDepartment WITH (UPDLOCK, HOLDLOCK)
            WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;

            DECLARE @Action varchar(20) = ''LINK'';
            IF @Exists = 0
            BEGIN
                INSERT dbo.FVN_ProductionDepartment(SiteCode, DepartmentCode, DepartmentName, LineCode, IsActive,
                                                    WorkCenterNo, LinkedAtUtc, LinkedBy)
                VALUES (@SiteCode, @DepartmentCode, COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentName)), N''''), @WcDescription, @DepartmentCode),
                        @LineCode, 1, @WorkCenterNo, SYSUTCDATETIME(), @ActorCode);
            END
            ELSE
            BEGIN
                IF @ExistingWc IS NOT NULL AND @ExistingWc <> @WorkCenterNo
                    THROW 56729, ''Department is already linked to a different work center; unlink it first.'', 1;

                /* Moving a department to another line would silently change existing LOT code maps. */
                IF @ExistingLine <> @LineCode AND EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap
                                                         WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1)
                    THROW 56730, ''Department is used by active LOT code maps; it cannot be moved to another line.'', 1;

                SET @Action = CASE WHEN @ExistingWc IS NULL THEN ''LINK'' ELSE ''RELINK'' END;
                UPDATE dbo.FVN_ProductionDepartment
                SET LineCode = @LineCode, WorkCenterNo = @WorkCenterNo, IsActive = 1,
                    DepartmentName = COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentName)), N''''), DepartmentName),
                    LinkedAtUtc = SYSUTCDATETIME(), LinkedBy = @ActorCode
                WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;
            END;

            INSERT MES.FVN_SiteDepartmentLineAudit(ActionCode, SiteCode, DepartmentCode, LineCode, WorkCenterNo, PreviousWorkCenterNo, ActorCode)
            VALUES (@Action, @SiteCode, @DepartmentCode, @LineCode, @WorkCenterNo, @ExistingWc, @ActorCode);

            COMMIT TRANSACTION;
            SELECT * FROM MES.vw_FVN_SiteDepartmentLine WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    IF OBJECT_ID(N'MES.usp_FVN_SiteDepartmentLine_Unlink', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_SiteDepartmentLine_Unlink;
    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_SiteDepartmentLine_Unlink
        @SiteCode nvarchar(30), @DepartmentCode varchar(4), @ActorCode nvarchar(100)
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'''') IS NULL THROW 56723, ''ActorCode is required.'', 1;
        BEGIN TRY
            BEGIN TRANSACTION;
            DECLARE @Wc nvarchar(30), @Line varchar(3);
            SELECT @Wc = WorkCenterNo, @Line = LineCode FROM dbo.FVN_ProductionDepartment WITH (UPDLOCK, HOLDLOCK)
            WHERE SiteCode = LTRIM(RTRIM(@SiteCode)) AND DepartmentCode = LTRIM(RTRIM(@DepartmentCode));
            IF @@ROWCOUNT = 0 THROW 56731, ''Department not found for this site.'', 1;
            IF @Wc IS NULL THROW 56732, ''Department is not linked to an IFS work center.'', 1;

            /* Only the IFS link is removed; the department/line (and LOT identity) stay intact. */
            UPDATE dbo.FVN_ProductionDepartment SET WorkCenterNo = NULL, LinkedAtUtc = NULL, LinkedBy = NULL
            WHERE SiteCode = LTRIM(RTRIM(@SiteCode)) AND DepartmentCode = LTRIM(RTRIM(@DepartmentCode));

            INSERT MES.FVN_SiteDepartmentLineAudit(ActionCode, SiteCode, DepartmentCode, LineCode, WorkCenterNo, PreviousWorkCenterNo, ActorCode)
            VALUES (''UNLINK'', LTRIM(RTRIM(@SiteCode)), LTRIM(RTRIM(@DepartmentCode)), @Line, NULL, @Wc, @ActorCode);
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END');

    /* ------------------------------------------------------------- SEC function */
    IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NOT NULL
        INSERT SEC.FunctionCatalog(FunctionCode, ModuleCode, FunctionName, SortOrder)
        SELECT 'MES.SITE_DEPARTMENT_LINE', 'MES', N'Liên kết Site - Line - Department (Work Center IFS)', 86
        WHERE NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'MES.SITE_DEPARTMENT_LINE');

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('068', N'IFS Site/Work Center sync and Site-Department-Line link');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
