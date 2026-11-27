/*
 Migration 067: synchronize IFSAPP.PRODUCTION_LINE (IFS Logical Unit ProductionLine, frmProdLineMgt) into FVN_PCTP.

 1. IFS.FVN_ProductionLine           read-only copy of the IFS production lines, key (SiteCode = IFS CONTRACT, ProductionLineCode).
                                     Typed columns come from the IFS payload; the whole IFS row is kept in SourcePayloadJson.
 2. IFS.FVN_SyncBatch.ProductionLineRows, CK_IFS_Stage_Entity accepts 'PRODUCTION_LINE', IFS.usp_ApplySyncBatch merges it
    (same one-way staging -> apply path as WAREHOUSE / SUPPLIER; selective sync of only this entity is supported).
 3. MES.vFVN_ProductionLineSetup     every IFS line of a Site with its LOT-code mapping state (also shows mappings whose line no longer exists in IFS).
 4. MES.usp_FVN_ProductionLotCodeMap_Save  declares / changes / deactivates the mapping of one IFS line to the PCTP LOT line code (3 chars)
                                     and department code (4 chars) used by the plan upload and LOT/QR generation.
 Site master stays dbo.FVN_PCTPSite (PCTP-owned list of sites the application works with); a synced line of a Site that is not in
 that list is kept but flagged IsKnownSite = 0 and cannot be mapped until the Site is declared.
 Apply after migration 066, in FVN_PCTP. SQL Server 2016 SP1+. Oracle remains read-only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 55700, 'Migration 067 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
   OR NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '066')
    THROW 55701, 'Migration 066 must be applied before 067.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '037')
    THROW 55702, 'Migration 037 (selective IFS sync) must be applied before 067.', 1;
IF OBJECT_ID(N'IFS.FVN_SyncBatch', N'U') IS NULL OR OBJECT_ID(N'IFS.FVN_SyncStageRow', N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P') IS NULL
    THROW 55703, 'IFS sync objects are missing. Apply migrations 012..037 first.', 1;
IF OBJECT_ID(N'MES.FVN_ProductionLotCodeMap', N'U') IS NULL OR OBJECT_ID(N'dbo.FVN_PCTPSite', N'U') IS NULL
   OR OBJECT_ID(N'dbo.FVN_ProductionLine', N'U') IS NULL OR OBJECT_ID(N'dbo.FVN_ProductionDepartment', N'U') IS NULL
    THROW 55704, 'MES.FVN_ProductionLotCodeMap / dbo.FVN_PCTPSite / dbo.FVN_ProductionLine / dbo.FVN_ProductionDepartment are missing (migrations 014, 028, 034).', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'IFS.FVN_ProductionLine', N'U') IS NULL
    BEGIN
        CREATE TABLE IFS.FVN_ProductionLine
        (
            SiteCode                 nvarchar(30)   NOT NULL,   /* IFS CONTRACT */
            ProductionLineCode       nvarchar(30)   NOT NULL,   /* IFS PRODUCTION_LINE */
            Description              nvarchar(800)  NULL,
            CalendarId               nvarchar(40)   NULL,
            TaktDailyDemand          decimal(18,4)  NULL,
            TaktTime                 decimal(18,4)  NULL,
            TaktTimeStartDate        date           NULL,
            TaktTimeEndDate          date           NULL,
            TaktTimeCalcDate         date           NULL,
            DailyOperatingMinutes    decimal(18,4)  NULL,
            MinSnapshotDate          date           NULL,
            MaxSnapshotDate          date           NULL,
            DefaultSchedulingHorizon nvarchar(40)   NULL,
            LineSchedLoadMethod      nvarchar(40)   NULL,
            IsActive                 bit            NOT NULL CONSTRAINT DF_IFS_FVN_ProductionLine_Active DEFAULT (1),
            SourceRowKey             nvarchar(200)  NULL,
            SourceObjVersion         nvarchar(56)   NULL,
            SourceHash               varbinary(32)  NULL,
            SourcePayloadJson        nvarchar(max)  NULL,
            LastSeenBatchId          bigint         NULL,
            SyncedAtUtc              datetime2(3)   NOT NULL CONSTRAINT DF_IFS_FVN_ProductionLine_Synced DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_IFS_FVN_ProductionLine PRIMARY KEY (SiteCode, ProductionLineCode)
        );
    END;

    IF COL_LENGTH(N'IFS.FVN_SyncBatch', N'ProductionLineRows') IS NULL
        ALTER TABLE IFS.FVN_SyncBatch ADD ProductionLineRows int NOT NULL
            CONSTRAINT DF_IFS_FVN_SyncBatch_ProductionLineRows DEFAULT (0);

    IF EXISTS (SELECT 1 FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(N'IFS.FVN_SyncStageRow') AND name = N'CK_IFS_Stage_Entity')
        ALTER TABLE IFS.FVN_SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;
    ALTER TABLE IFS.FVN_SyncStageRow WITH CHECK
        ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (EntityName IN ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE','BOM_COMPONENT','WAREHOUSE','SUPPLIER','PRODUCTION_LINE'));

    /* Extend the transactional apply procedure the same way migrations 027/028 did. PartNo of a PRODUCTION_LINE stage row
       carries the IFS PRODUCTION_LINE code, SiteCode carries CONTRACT. */
    DECLARE @Definition nvarchar(max), @At int, @Merge nvarchar(max);
    SET @Definition = OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch', N'P'));
    IF @Definition IS NULL
        THROW 55705, 'Cannot read IFS.usp_ApplySyncBatch.', 1;

    IF CHARINDEX(N'MERGE IFS.FVN_ProductionLine AS T', @Definition) = 0
    BEGIN
        SET @At = CHARINDEX(N'UPDATE IFS.SyncBatch', @Definition, CHARINDEX(N'MERGE MES.FVN_IfsWarehouse', @Definition));
        IF @At = 0
            THROW 55706, 'Cannot locate the final batch update in IFS.usp_ApplySyncBatch.', 1;

        SET @Merge = N'
        MERGE IFS.FVN_ProductionLine AS T
        USING
        (
            SELECT s.SiteCode,
                   s.PartNo AS ProductionLineCode,
                   JSON_VALUE(s.PayloadJson, ''$.DESCRIPTION'') AS Description,
                   JSON_VALUE(s.PayloadJson, ''$.CALENDAR_ID'') AS CalendarId,
                   TRY_CONVERT(decimal(18,4), TRY_CONVERT(float, JSON_VALUE(s.PayloadJson, ''$.TAKT_DAILY_DEMAND''))) AS TaktDailyDemand,
                   TRY_CONVERT(decimal(18,4), TRY_CONVERT(float, JSON_VALUE(s.PayloadJson, ''$.TAKT_TIME''))) AS TaktTime,
                   TRY_CONVERT(date, JSON_VALUE(s.PayloadJson, ''$.TAKT_TIME_START_DATE'')) AS TaktTimeStartDate,
                   TRY_CONVERT(date, JSON_VALUE(s.PayloadJson, ''$.TAKT_TIME_END_DATE'')) AS TaktTimeEndDate,
                   TRY_CONVERT(date, JSON_VALUE(s.PayloadJson, ''$.TAKT_TIME_CALC_DATE'')) AS TaktTimeCalcDate,
                   TRY_CONVERT(decimal(18,4), TRY_CONVERT(float, JSON_VALUE(s.PayloadJson, ''$.DAILY_OPERATING_MINUTES''))) AS DailyOperatingMinutes,
                   TRY_CONVERT(date, JSON_VALUE(s.PayloadJson, ''$.MIN_SNAPSHOT_DATE'')) AS MinSnapshotDate,
                   TRY_CONVERT(date, JSON_VALUE(s.PayloadJson, ''$.MAX_SNAPSHOT_DATE'')) AS MaxSnapshotDate,
                   JSON_VALUE(s.PayloadJson, ''$.DEFAULT_SCHEDULING_HORIZON'') AS DefaultSchedulingHorizon,
                   COALESCE(JSON_VALUE(s.PayloadJson, ''$.LINE_SCHED_LOAD_METHOD_DB''), JSON_VALUE(s.PayloadJson, ''$.LINE_SCHED_LOAD_METHOD'')) AS LineSchedLoadMethod,
                   s.SourceRowKey, s.SourceObjVersion, s.SourceHash, s.PayloadJson
            FROM IFS.SyncStageRow AS s
            WHERE s.SyncBatchId = @SyncBatchId AND s.EntityName = ''PRODUCTION_LINE''
        ) AS S
          ON T.SiteCode = S.SiteCode AND T.ProductionLineCode = S.ProductionLineCode
        WHEN MATCHED THEN UPDATE SET
            Description = S.Description, CalendarId = S.CalendarId,
            TaktDailyDemand = S.TaktDailyDemand, TaktTime = S.TaktTime,
            TaktTimeStartDate = S.TaktTimeStartDate, TaktTimeEndDate = S.TaktTimeEndDate, TaktTimeCalcDate = S.TaktTimeCalcDate,
            DailyOperatingMinutes = S.DailyOperatingMinutes,
            MinSnapshotDate = S.MinSnapshotDate, MaxSnapshotDate = S.MaxSnapshotDate,
            DefaultSchedulingHorizon = S.DefaultSchedulingHorizon, LineSchedLoadMethod = S.LineSchedLoadMethod,
            IsActive = 1,
            SourceRowKey = S.SourceRowKey, SourceObjVersion = S.SourceObjVersion, SourceHash = S.SourceHash, SourcePayloadJson = S.PayloadJson,
            LastSeenBatchId = @SyncBatchId, SyncedAtUtc = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
            (SiteCode, ProductionLineCode, Description, CalendarId, TaktDailyDemand, TaktTime, TaktTimeStartDate, TaktTimeEndDate,
             TaktTimeCalcDate, DailyOperatingMinutes, MinSnapshotDate, MaxSnapshotDate, DefaultSchedulingHorizon, LineSchedLoadMethod,
             IsActive, SourceRowKey, SourceObjVersion, SourceHash, SourcePayloadJson, LastSeenBatchId, SyncedAtUtc)
        VALUES
            (S.SiteCode, S.ProductionLineCode, S.Description, S.CalendarId, S.TaktDailyDemand, S.TaktTime, S.TaktTimeStartDate, S.TaktTimeEndDate,
             S.TaktTimeCalcDate, S.DailyOperatingMinutes, S.MinSnapshotDate, S.MaxSnapshotDate, S.DefaultSchedulingHorizon, S.LineSchedLoadMethod,
             1, S.SourceRowKey, S.SourceObjVersion, S.SourceHash, S.PayloadJson, @SyncBatchId, SYSUTCDATETIME())
        OUTPUT ''IFS_PRODUCTION_LINE'', $action INTO @Changes;

        ';
        SET @Definition = STUFF(@Definition, @At, 0, @Merge);
    END;

    SET @Definition = REPLACE(@Definition, N'CREATE PROCEDURE', N'ALTER PROCEDURE');
    IF CHARINDEX(N'CREATE PROCEDURE', @Definition) > 0
        THROW 55707, 'Cannot convert IFS.usp_ApplySyncBatch to ALTER PROCEDURE.', 1;
    EXEC sys.sp_executesql @Definition;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

IF OBJECT_ID(N'IFS.FVN_ProductionLine', N'U') IS NULL
    THROW 55708, 'IFS.FVN_ProductionLine was not created; fix the error above and re-run 067.', 1;
GO

CREATE OR ALTER VIEW MES.vFVN_ProductionLineSetup
AS
/* One row per IFS production line of a Site, with its LOT-code mapping. Mappings whose IFS line is gone are listed with ExistsInIfs = 0. */
SELECT l.SiteCode, l.ProductionLineCode, l.Description, l.CalendarId, l.TaktTime, l.TaktDailyDemand, l.DailyOperatingMinutes,
       CONVERT(bit, 1) AS ExistsInIfs,
       CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite s WHERE s.SiteCode = l.SiteCode AND s.IsActive = 1) THEN 1 ELSE 0 END) AS IsKnownSite,
       m.LotLineCode, m.DepartmentCode,
       CONVERT(bit, CASE WHEN m.PlanProductionLineCode IS NULL THEN 0 ELSE 1 END) AS IsMapped,
       CONVERT(bit, ISNULL(m.IsActive, 0)) AS MapIsActive,
       l.SyncedAtUtc
FROM IFS.FVN_ProductionLine AS l
LEFT JOIN MES.FVN_ProductionLotCodeMap AS m ON m.SiteCode = l.SiteCode AND m.PlanProductionLineCode = l.ProductionLineCode
UNION ALL
SELECT m.SiteCode, m.PlanProductionLineCode, NULL, NULL, NULL, NULL, NULL,
       CONVERT(bit, 0),
       CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite s WHERE s.SiteCode = m.SiteCode AND s.IsActive = 1) THEN 1 ELSE 0 END),
       m.LotLineCode, m.DepartmentCode, CONVERT(bit, 1), CONVERT(bit, m.IsActive), NULL
FROM MES.FVN_ProductionLotCodeMap AS m
WHERE NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine AS l WHERE l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode);
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ProductionLotCodeMap_Save
    @SiteCode              nvarchar(30),
    @PlanProductionLineCode nvarchar(30),
    @LotLineCode           varchar(3),
    @DepartmentCode        varchar(4),
    @IsActive              bit = 1,
    @ActorCode             nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @PlanProductionLineCode = LTRIM(RTRIM(ISNULL(@PlanProductionLineCode, N'')));
    SET @LotLineCode = LTRIM(RTRIM(ISNULL(@LotLineCode, '')));
    SET @DepartmentCode = LTRIM(RTRIM(ISNULL(@DepartmentCode, '')));
    SET @IsActive = ISNULL(@IsActive, 1);
    IF @SiteCode = N'' OR @PlanProductionLineCode = N'' THROW 55710, 'SiteCode and PlanProductionLineCode are required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 55711, 'ActorCode is required.', 1;
    IF LEN(@LotLineCode) <> 3 THROW 55712, 'Mã chuyền LOT phải đúng 3 ký tự.', 1;
    IF LEN(@DepartmentCode) <> 4 THROW 55713, 'Mã bộ phận phải đúng 4 ký tự.', 1;

    DECLARE @IfsDescription nvarchar(800);
    SELECT @IfsDescription = Description FROM IFS.FVN_ProductionLine WHERE SiteCode = @SiteCode AND ProductionLineCode = @PlanProductionLineCode;

    IF @IsActive = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
            THROW 55714, 'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;
        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine WHERE SiteCode = @SiteCode AND ProductionLineCode = @PlanProductionLineCode)
            THROW 55715, 'Chuyền này chưa có trong IFS.FVN_ProductionLine của Site. Đồng bộ PRODUCTION_LINE từ IFS trước.', 1;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @IsActive = 1
        BEGIN
            /* LOT line master (3 chars): created from the IFS description when it does not exist yet. */
            IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WITH (UPDLOCK, HOLDLOCK) WHERE SiteCode = @SiteCode AND LineCode = @LotLineCode)
                INSERT dbo.FVN_ProductionLine (SiteCode, LineCode, LineName, IsActive)
                VALUES (@SiteCode, @LotLineCode, LEFT(ISNULL(NULLIF(@IfsDescription, N''), @PlanProductionLineCode), 120), 1);

            /* Department master (4 chars) belongs to exactly one LOT line (FK on SiteCode + LineCode). */
            DECLARE @DeptLine varchar(3);
            SELECT @DeptLine = LineCode FROM dbo.FVN_ProductionDepartment WITH (UPDLOCK, HOLDLOCK)
             WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;
            IF @DeptLine IS NULL
                INSERT dbo.FVN_ProductionDepartment (SiteCode, DepartmentCode, DepartmentName, LineCode, IsActive)
                VALUES (@SiteCode, @DepartmentCode, N'Bộ phận ' + @DepartmentCode, @LotLineCode, 1);
            ELSE IF @DeptLine <> @LotLineCode
            BEGIN
                DECLARE @Msg nvarchar(300) = N'Bộ phận ' + @DepartmentCode + N' đang thuộc chuyền LOT ' + @DeptLine + N', không thể gán cho chuyền LOT ' + @LotLineCode + N'.';
                THROW 55716, @Msg, 1;
            END;
        END;

        UPDATE MES.FVN_ProductionLotCodeMap WITH (UPDLOCK, HOLDLOCK)
           SET LotLineCode = @LotLineCode, DepartmentCode = @DepartmentCode, IsActive = @IsActive
         WHERE SiteCode = @SiteCode AND PlanProductionLineCode = @PlanProductionLineCode;
        IF @@ROWCOUNT = 0
            INSERT MES.FVN_ProductionLotCodeMap (SiteCode, PlanProductionLineCode, LotLineCode, DepartmentCode, IsActive, CreatedBy)
            VALUES (@SiteCode, @PlanProductionLineCode, @LotLineCode, @DepartmentCode, @IsActive, @ActorCode);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '067')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('067', N'IFS PRODUCTION_LINE sync, production line setup view and LOT-code map save');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '067';
GO
