/*
 Migration 077: Work Center <-> Department consistency with the IFS production line, and a controlled way to manage dbo.FVN_PCTPSite.

 IFS is the source of truth for which production line a work center belongs to (IFS.FVN_WorkCenter.IfsProductionLine).
 MES.FVN_ProductionLotCodeMap maps that same IFS line to a LOT line + department. A department that is linked to a work
 center of IFS line B must therefore not be the target of a mapping for IFS line A. This migration:
   1. MES.vw_FVN_MasterDataConsistency: + MAP_DEPT_WORKCENTER_IFS_LINE_MISMATCH (ERROR) and DEPT_WORKCENTER_NO_IFS_LINE (INFO).
   2. MES.vw_FVN_LotCodeMapIntegrity: + WORKCENTER_IFS_LINE_MISMATCH.
   3. MES.usp_FVN_SiteDepartmentLine_Link: refuses a work center whose IFS line differs from an active mapping of the department (57700).
   4. Trigger MES.trg_FVN_ProductionLotCodeMap_WorkCenterLine: refuses an active mapping whose department already holds a work
      center of another IFS line (57701). Rows touched by INSERT / UPDATE only: legacy rows stay readable and are listed by the view.
   5. dbo.FVN_PCTPSite management: MES.FVN_PCTPSiteAudit, MES.usp_FVN_PCTPSite_Save (allow / enable / disable / rename),
      SEC function MES.SITE_SETUP. A site can be allowed only when IFS.FVN_Site has it (run the SITE sync first); the name comes from
      IFS and a PCTP name is kept only when IFS has none. Nothing is deleted.
 SQL Server 2016 SP1+. Apply after 076.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57720, 'Migration 077 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '076')
    THROW 57721, 'Migration 076 must be registered before 077.', 1;
IF OBJECT_ID(N'MES.vw_FVN_MasterDataConsistency', N'V') IS NULL OR OBJECT_ID(N'MES.usp_FVN_SiteDepartmentLine_Link', N'P') IS NULL
    THROW 57722, 'Migrations 068 and 076 objects are missing.', 1;
GO

CREATE OR ALTER VIEW MES.vw_FVN_MasterDataConsistency
AS
SELECT CONVERT(varchar(40), 'SITE_NOT_IN_IFS') AS IssueCode, CONVERT(varchar(10), 'WARN') AS Severity,
       p.SiteCode, CONVERT(nvarchar(200), p.SiteCode) AS ObjectKey,
       CONVERT(nvarchar(400), N'Site đang hoạt động trong dbo.FVN_PCTPSite nhưng không có trong IFS.FVN_Site (chưa đồng bộ SITE hoặc đã bị IFS bỏ).') AS Detail
FROM dbo.FVN_PCTPSite AS p
WHERE p.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM IFS.FVN_Site AS s WHERE s.SiteCode = p.SiteCode AND s.IsPresentInIfs = 1)
UNION ALL
SELECT 'SITE_NAME_MISMATCH', 'INFO', p.SiteCode, p.SiteCode,
       N'Tên site khác nhau: PCTP="' + p.SiteName + N'" / IFS="' + s.SiteName + N'". dbo.vFVN_Site hiển thị tên IFS.'
FROM dbo.FVN_PCTPSite AS p
JOIN IFS.FVN_Site AS s ON s.SiteCode = p.SiteCode
WHERE NULLIF(LTRIM(RTRIM(p.SiteName)), N'') IS NOT NULL
  AND NULLIF(LTRIM(RTRIM(s.SiteName)), N'') IS NOT NULL
  AND LTRIM(RTRIM(p.SiteName)) <> LTRIM(RTRIM(s.SiteName))
UNION ALL
SELECT 'LINE_SITE_NOT_ALLOW_LISTED', 'ERROR', l.SiteCode, l.SiteCode + N'/' + l.LineCode,
       N'dbo.FVN_ProductionLine thuộc Site chưa khai báo trong dbo.FVN_PCTPSite.'
FROM dbo.FVN_ProductionLine AS l
WHERE NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite AS p WHERE p.SiteCode = l.SiteCode)
UNION ALL
SELECT 'MAP_IFS_LINE_MISSING', 'ERROR', m.SiteCode, m.SiteCode + N'/' + m.PlanProductionLineCode,
       N'Mapping đang hoạt động nhưng chuyền không có trong IFS.FVN_ProductionLine của Site.'
FROM MES.FVN_ProductionLotCodeMap AS m
WHERE m.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine AS l WHERE l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode)
UNION ALL
SELECT 'MAP_IFS_LINE_INACTIVE', 'WARN', m.SiteCode, m.SiteCode + N'/' + m.PlanProductionLineCode,
       N'Mapping đang hoạt động nhưng chuyền IFS đã ngừng (IFS.FVN_ProductionLine.IsActive = 0).'
FROM MES.FVN_ProductionLotCodeMap AS m
JOIN IFS.FVN_ProductionLine AS l ON l.SiteCode = m.SiteCode AND l.ProductionLineCode = m.PlanProductionLineCode
WHERE m.IsActive = 1 AND l.IsActive = 0
UNION ALL
SELECT 'MAP_LOT_LINE_MISSING', 'ERROR', m.SiteCode, m.SiteCode + N'/' + m.PlanProductionLineCode,
       N'Mapping trỏ tới mã chuyền LOT ' + m.LotLineCode + N' chưa có trong dbo.FVN_ProductionLine.'
FROM MES.FVN_ProductionLotCodeMap AS m
WHERE m.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine AS l WHERE l.SiteCode = m.SiteCode AND l.LineCode = m.LotLineCode)
UNION ALL
SELECT 'MAP_DEPARTMENT_MISSING', 'ERROR', m.SiteCode, m.SiteCode + N'/' + m.PlanProductionLineCode,
       N'Mapping trỏ tới bộ phận ' + m.DepartmentCode + N' chưa có trong dbo.FVN_ProductionDepartment.'
FROM MES.FVN_ProductionLotCodeMap AS m
WHERE m.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment AS d WHERE d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode)
UNION ALL
SELECT 'MAP_DEPARTMENT_OTHER_LINE', 'ERROR', m.SiteCode, m.SiteCode + N'/' + m.PlanProductionLineCode,
       N'Bộ phận ' + m.DepartmentCode + N' thuộc chuyền LOT ' + d.LineCode + N', mapping lại ghi ' + m.LotLineCode + N'.'
FROM MES.FVN_ProductionLotCodeMap AS m
JOIN dbo.FVN_ProductionDepartment AS d ON d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode
WHERE m.IsActive = 1 AND d.LineCode <> m.LotLineCode
UNION ALL
SELECT 'MAP_DEPT_WORKCENTER_IFS_LINE_MISMATCH', 'ERROR', m.SiteCode, m.SiteCode + N'/' + m.PlanProductionLineCode,
       N'Mapping chuyền IFS ' + m.PlanProductionLineCode + N' trỏ bộ phận ' + m.DepartmentCode + N', nhưng bộ phận liên kết Work Center '
       + d.WorkCenterNo + N' thuộc chuyền IFS ' + w.IfsProductionLine + N'. Sửa mapping hoặc gỡ / liên kết lại Work Center.'
FROM MES.FVN_ProductionLotCodeMap AS m
JOIN dbo.FVN_ProductionDepartment AS d ON d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode
JOIN IFS.FVN_WorkCenter AS w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
WHERE m.IsActive = 1
  AND NULLIF(LTRIM(RTRIM(w.IfsProductionLine)), N'') IS NOT NULL
  AND LTRIM(RTRIM(w.IfsProductionLine)) <> m.PlanProductionLineCode
UNION ALL
SELECT 'DEPT_WORKCENTER_NO_IFS_LINE', 'INFO', d.SiteCode, d.SiteCode + N'/' + d.DepartmentCode,
       N'Work Center ' + d.WorkCenterNo + N' chưa có chuyền IFS (IFS.FVN_WorkCenter.IfsProductionLine rỗng) nên chưa kiểm tra được khớp với mapping.'
FROM dbo.FVN_ProductionDepartment AS d
JOIN IFS.FVN_WorkCenter AS w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
WHERE w.IsPresentInIfs = 1 AND NULLIF(LTRIM(RTRIM(w.IfsProductionLine)), N'') IS NULL
UNION ALL
SELECT 'DEPT_WORKCENTER_NOT_IN_IFS', 'WARN', d.SiteCode, d.SiteCode + N'/' + d.DepartmentCode,
       N'Bộ phận liên kết Work Center ' + d.WorkCenterNo + N' không còn trong IFS.FVN_WorkCenter.'
FROM dbo.FVN_ProductionDepartment AS d
WHERE d.WorkCenterNo IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM IFS.FVN_WorkCenter AS w
                  WHERE w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo AND w.IsPresentInIfs = 1)
UNION ALL
SELECT 'DEPT_WORKCENTER_LINE_MISMATCH', 'INFO', d.SiteCode, d.SiteCode + N'/' + d.DepartmentCode,
       N'Work Center ' + d.WorkCenterNo + N' thuộc chuyền IFS ' + w.IfsProductionLine + N', bộ phận đang thuộc chuyền LOT ' + d.LineCode + N'.'
FROM dbo.FVN_ProductionDepartment AS d
JOIN IFS.FVN_WorkCenter AS w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
WHERE LEN(w.IfsProductionLine) = 3 AND w.IfsProductionLine <> d.LineCode;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link
    @SiteCode nvarchar(30), @WorkCenterNo nvarchar(30),
    @DepartmentCode varchar(4), @LineCode varchar(3),
    @LineName nvarchar(120) = NULL, @DepartmentName nvarchar(120) = NULL,
    @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @WorkCenterNo = LTRIM(RTRIM(@WorkCenterNo));
    SET @DepartmentCode = LTRIM(RTRIM(@DepartmentCode)); SET @LineCode = LTRIM(RTRIM(@LineCode));
    IF NULLIF(@SiteCode, N'') IS NULL OR NULLIF(@WorkCenterNo, N'') IS NULL
        THROW 56720, 'SiteCode and WorkCenterNo are required.', 1;
    IF LEN(ISNULL(@DepartmentCode, '')) <> 4 THROW 56721, 'DepartmentCode must contain exactly four characters.', 1;
    IF LEN(ISNULL(@LineCode, '')) <> 3 THROW 56722, 'LineCode must contain exactly three characters.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 56723, 'ActorCode is required.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
            THROW 56724, 'Site is not in the active PCTP site allow-list.', 1;

        DECLARE @WcDescription nvarchar(250), @WcIfsLine nvarchar(30), @MismatchMsg nvarchar(2047);
        SELECT @WcDescription = Description, @WcIfsLine = NULLIF(LTRIM(RTRIM(IfsProductionLine)), N'') FROM IFS.FVN_WorkCenter WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND IsPresentInIfs = 1;
        IF @@ROWCOUNT = 0
            THROW 56725, 'Work center does not exist in the synchronized IFS master for this site.', 1;

        /* Unification (077): IFS is the source of truth for the work center's production line. A department used by an active
           LOT code map of another IFS line cannot take a work center that IFS places on a different line. */
        IF @WcIfsLine IS NOT NULL
           AND EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap
                       WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1 AND PlanProductionLineCode <> @WcIfsLine)
        BEGIN
            SELECT TOP (1) @MismatchMsg = N'Work Center ' + @WorkCenterNo + N' thuộc chuyền IFS ' + @WcIfsLine + N', nhưng bộ phận ' + @DepartmentCode +
                   N' đang được mapping cho chuyền IFS ' + PlanProductionLineCode + N'. Sửa mapping (Khai báo chuyền) hoặc chọn bộ phận khác.'
              FROM MES.FVN_ProductionLotCodeMap
             WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1 AND PlanProductionLineCode <> @WcIfsLine;
            THROW 57700, @MismatchMsg, 1;
        END;

        /* The work center may be linked to only one department. */
        DECLARE @OtherDept varchar(4);
        SELECT @OtherDept = DepartmentCode FROM dbo.FVN_ProductionDepartment
        WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND DepartmentCode <> @DepartmentCode;
        IF @OtherDept IS NOT NULL
            THROW 56726, 'This work center is already linked to another department; unlink it first.', 1;

        /* Line: reuse the existing one, or create it (name required). */
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WITH (UPDLOCK, HOLDLOCK)
                       WHERE SiteCode = @SiteCode AND LineCode = @LineCode)
        BEGIN
            IF NULLIF(LTRIM(RTRIM(@LineName)), N'') IS NULL
                THROW 56727, 'Line does not exist for this site; LineName is required to create it.', 1;
            INSERT dbo.FVN_ProductionLine(SiteCode, LineCode, LineName, IsActive)
            VALUES (@SiteCode, @LineCode, LTRIM(RTRIM(@LineName)), 1);
        END
        ELSE IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND LineCode = @LineCode AND IsActive = 1)
            THROW 56728, 'Line is inactive.', 1;

        DECLARE @ExistingLine varchar(3), @ExistingWc nvarchar(30), @Exists bit = 0;
        SELECT @ExistingLine = LineCode, @ExistingWc = WorkCenterNo, @Exists = 1
        FROM dbo.FVN_ProductionDepartment WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;

        DECLARE @Action varchar(20) = 'LINK';
        IF @Exists = 0
        BEGIN
            INSERT dbo.FVN_ProductionDepartment(SiteCode, DepartmentCode, DepartmentName, LineCode, IsActive,
                                                WorkCenterNo, LinkedAtUtc, LinkedBy)
            VALUES (@SiteCode, @DepartmentCode, COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentName)), N''), @WcDescription, @DepartmentCode),
                    @LineCode, 1, @WorkCenterNo, SYSUTCDATETIME(), @ActorCode);
        END
        ELSE
        BEGIN
            IF @ExistingWc IS NOT NULL AND @ExistingWc <> @WorkCenterNo
                THROW 56729, 'Department is already linked to a different work center; unlink it first.', 1;

            /* Moving a department to another line would silently change existing LOT code maps. */
            IF @ExistingLine <> @LineCode AND EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap
                                                     WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1)
                THROW 56730, 'Department is used by active LOT code maps; it cannot be moved to another line.', 1;

            SET @Action = CASE WHEN @ExistingWc IS NULL THEN 'LINK' ELSE 'RELINK' END;
            UPDATE dbo.FVN_ProductionDepartment
            SET LineCode = @LineCode, WorkCenterNo = @WorkCenterNo, IsActive = 1,
                DepartmentName = COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentName)), N''), DepartmentName),
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
END
GO

CREATE OR ALTER VIEW MES.vw_FVN_LotCodeMapIntegrity
AS
SELECT m.SiteCode, m.PlanProductionLineCode, m.LotLineCode, m.DepartmentCode,
       CASE WHEN l.LineCode IS NULL THEN 'LINE_NOT_DEFINED'
            WHEN d.DepartmentCode IS NULL THEN 'DEPARTMENT_NOT_DEFINED'
            WHEN d.LineCode <> m.LotLineCode THEN 'DEPARTMENT_BELONGS_TO_OTHER_LINE'
            WHEN w.WorkCenterNo IS NOT NULL AND NULLIF(LTRIM(RTRIM(w.IfsProductionLine)), N'') IS NOT NULL
                 AND LTRIM(RTRIM(w.IfsProductionLine)) <> m.PlanProductionLineCode THEN 'WORKCENTER_IFS_LINE_MISMATCH'
            WHEN l.IsActive = 0 OR d.IsActive = 0 THEN 'LINE_OR_DEPARTMENT_INACTIVE'
            ELSE 'OK' END AS IntegrityStatus
FROM MES.FVN_ProductionLotCodeMap m
LEFT JOIN dbo.FVN_ProductionLine l ON l.SiteCode = m.SiteCode AND l.LineCode = m.LotLineCode
LEFT JOIN dbo.FVN_ProductionDepartment d ON d.SiteCode = m.SiteCode AND d.DepartmentCode = m.DepartmentCode
LEFT JOIN IFS.FVN_WorkCenter w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
WHERE m.IsActive = 1;
GO

CREATE OR ALTER TRIGGER MES.trg_FVN_ProductionLotCodeMap_WorkCenterLine
ON MES.FVN_ProductionLotCodeMap
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Msg nvarchar(2047);
    SELECT TOP (1) @Msg = N'Bộ phận ' + i.DepartmentCode + N' đã liên kết Work Center ' + w.WorkCenterNo + N' thuộc chuyền IFS ' + LTRIM(RTRIM(w.IfsProductionLine))
                          + N', không thể mapping cho chuyền IFS ' + i.PlanProductionLineCode + N'. Gỡ liên kết Work Center hoặc chọn bộ phận khác.'
      FROM inserted i
      JOIN dbo.FVN_ProductionDepartment d ON d.SiteCode = i.SiteCode AND d.DepartmentCode = i.DepartmentCode
      JOIN IFS.FVN_WorkCenter w ON w.SiteCode = d.SiteCode AND w.WorkCenterNo = d.WorkCenterNo
     WHERE i.IsActive = 1
       AND NULLIF(LTRIM(RTRIM(w.IfsProductionLine)), N'') IS NOT NULL
       AND LTRIM(RTRIM(w.IfsProductionLine)) <> i.PlanProductionLineCode;
    IF @Msg IS NOT NULL THROW 57701, @Msg, 1;
END;
GO

IF OBJECT_ID(N'MES.FVN_PCTPSiteAudit', N'U') IS NULL
    CREATE TABLE MES.FVN_PCTPSiteAudit
    (
        AuditId          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_FVN_PCTPSiteAudit PRIMARY KEY,
        SiteCode         nvarchar(30)  NOT NULL,
        ActionCode       varchar(10)   NOT NULL,
        PreviousIsActive bit           NULL,
        IsActive         bit           NOT NULL,
        PreviousName     nvarchar(120) NULL,
        SiteName         nvarchar(120) NULL,
        ActorCode        nvarchar(100) NOT NULL,
        OccurredAtUtc    datetime2(3)  NOT NULL CONSTRAINT DF_MES_FVN_PCTPSiteAudit_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_MES_FVN_PCTPSiteAudit_Action CHECK (ActionCode IN ('ALLOW', 'ENABLE', 'DISABLE', 'RENAME'))
    );
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_PCTPSite_Save
    @SiteCode  nvarchar(30),
    @SiteName  nvarchar(120) = NULL,
    @IsActive  bit,
    @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    SET @SiteName = NULLIF(LTRIM(RTRIM(ISNULL(@SiteName, N''))), N'');
    IF @SiteCode = N'' THROW 57710, 'SiteCode is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57711, 'ActorCode is required.', 1;
    IF @IsActive IS NULL THROW 57712, 'IsActive is required.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @IfsName nvarchar(120), @IfsPresent bit = 0, @Msg nvarchar(2047);
        SELECT @IfsName = NULLIF(LTRIM(RTRIM(SiteName)), N''), @IfsPresent = IsPresentInIfs FROM IFS.FVN_Site WHERE SiteCode = @SiteCode;
        DECLARE @ExistingName nvarchar(120), @ExistingActive bit, @Exists bit = 0;
        SELECT @ExistingName = SiteName, @ExistingActive = IsActive, @Exists = 1
          FROM dbo.FVN_PCTPSite WITH (UPDLOCK, HOLDLOCK) WHERE SiteCode = @SiteCode;

        /* Only turning a site ON (new or previously off) needs IFS; renaming / keeping an active site does not. */
        IF @IsActive = 1 AND ISNULL(@IfsPresent, 0) = 0 AND (@Exists = 0 OR @ExistingActive = 0)
        BEGIN
            SET @Msg = N'Site ' + @SiteCode + N' chưa có trong IFS.FVN_Site (hoặc đã bị IFS bỏ). Đồng bộ dữ liệu IFS nhóm SITE trước khi cho phép.';
            THROW 57713, @Msg, 1;
        END;

        DECLARE @NewName nvarchar(120) = COALESCE(@IfsName, @SiteName, @ExistingName);
        IF @Exists = 0
        BEGIN
            INSERT dbo.FVN_PCTPSite (SiteCode, SiteName, IsActive, CreatedBy) VALUES (@SiteCode, @NewName, @IsActive, @ActorCode);
            INSERT MES.FVN_PCTPSiteAudit (SiteCode, ActionCode, PreviousIsActive, IsActive, PreviousName, SiteName, ActorCode)
            VALUES (@SiteCode, 'ALLOW', NULL, @IsActive, NULL, @NewName, @ActorCode);
        END
        ELSE
        BEGIN
            IF ISNULL(@ExistingName, N'') <> ISNULL(@NewName, N'') OR @ExistingActive <> @IsActive
            BEGIN
                UPDATE dbo.FVN_PCTPSite SET SiteName = @NewName, IsActive = @IsActive WHERE SiteCode = @SiteCode;
                INSERT MES.FVN_PCTPSiteAudit (SiteCode, ActionCode, PreviousIsActive, IsActive, PreviousName, SiteName, ActorCode)
                VALUES (@SiteCode,
                        CASE WHEN @ExistingActive <> @IsActive THEN CASE WHEN @IsActive = 1 THEN 'ENABLE' ELSE 'DISABLE' END ELSE 'RENAME' END,
                        @ExistingActive, @IsActive, @ExistingName, @NewName, @ActorCode);
            END;
        END;
        COMMIT TRANSACTION;
        SELECT * FROM dbo.vFVN_Site WHERE SiteCode = @SiteCode;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID(N'SEC.FunctionCatalog', N'U') IS NOT NULL
    INSERT SEC.FunctionCatalog (FunctionCode, ModuleCode, FunctionName, SortOrder)
    SELECT 'MES.SITE_SETUP', 'MES', N'Khai báo Site PCTP (dbo.FVN_PCTPSite)', 85
    WHERE NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'MES.SITE_SETUP');
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '077')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('077', N'Work Center - Department - IFS line consistency; PCTP site management');
GO
SELECT IssueCode, Severity, SiteCode, ObjectKey, Detail FROM MES.vw_FVN_MasterDataConsistency ORDER BY Severity, IssueCode, SiteCode;
GO
