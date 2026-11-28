/*
 Migration 078: Work Center link takes its codes from IFS instead of typing them.

 Department code (4 chars) = IFS WorkCenterNo left-padded with '0' (19A -> 019A). A WorkCenterNo longer than 4 chars or with
 characters outside 0-9 / A-Z (for example 1100E) cannot be converted: the caller must supply the department code.
 LOT line code (3 chars) is generated from the IFS production line code and remembered in dbo.FVN_ProductionLine.IfsProductionLineCode:
   1. the LOT line already linked to that IFS line (dbo.FVN_ProductionLine.IfsProductionLineCode, then MES.FVN_ProductionLotCodeMap);
   2. otherwise the first 3 letters/digits of the IFS code; on a clash inside the Site the last 1-2 characters become a number
      (CAST_1250T -> CAS, CAST_DC135 -> CA1, CAST_DC350 -> CA2).
 Names are pushed from IFS: department name = work center description, line name = IFS production line description.
 Changes: dbo.FVN_ProductionLine.IfsProductionLineCode (+ filtered unique index, backfilled from active maps),
   MES.fn_FVN_DepartmentCodeFromWorkCenter, MES.usp_FVN_LotLineCode_Resolve (read-only, also used for the UI preview),
   MES.vw_FVN_WorkCenterLinkCandidates (suggestions), MES.usp_FVN_SiteDepartmentLine_Link (codes/names optional, @IfsProductionLine added).
 Old callers that pass DepartmentCode / LineCode / LineName keep working. SQL Server 2016 SP1+. Apply after 077.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57740, 'Migration 078 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '077')
    THROW 57741, 'Migration 077 must be registered before 078.', 1;
GO

SET QUOTED_IDENTIFIER ON;
IF COL_LENGTH(N'dbo.FVN_ProductionLine', N'IfsProductionLineCode') IS NULL
    EXEC(N'ALTER TABLE dbo.FVN_ProductionLine ADD IfsProductionLineCode nvarchar(30) NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FVN_ProductionLine_IfsLine' AND object_id = OBJECT_ID(N'dbo.FVN_ProductionLine'))
    EXEC(N'CREATE UNIQUE INDEX UX_FVN_ProductionLine_IfsLine ON dbo.FVN_ProductionLine(SiteCode, IfsProductionLineCode) WHERE IfsProductionLineCode IS NOT NULL;');
/* Backfill: a LOT line used by active maps of exactly one IFS line is that IFS line's LOT line. */
EXEC(N'
UPDATE l SET IfsProductionLineCode = x.PlanProductionLineCode
FROM dbo.FVN_ProductionLine AS l
JOIN (SELECT SiteCode, LotLineCode, MIN(PlanProductionLineCode) AS PlanProductionLineCode
      FROM MES.FVN_ProductionLotCodeMap WHERE IsActive = 1
      GROUP BY SiteCode, LotLineCode HAVING COUNT(DISTINCT PlanProductionLineCode) = 1) AS x
  ON x.SiteCode = l.SiteCode AND x.LotLineCode = l.LineCode
WHERE l.IfsProductionLineCode IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine AS o
                  WHERE o.SiteCode = l.SiteCode AND o.IfsProductionLineCode = x.PlanProductionLineCode);');
GO

alter  FUNCTION MES.fn_FVN_DepartmentCodeFromWorkCenter(@WorkCenterNo nvarchar(30))
RETURNS varchar(4)
AS
BEGIN
    DECLARE @n nvarchar(30) = UPPER(LTRIM(RTRIM(@WorkCenterNo)));
    IF @n IS NULL OR LEN(@n) = 0 OR LEN(@n) > 4 OR @n COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9A-Z]%'
        RETURN NULL;
    RETURN RIGHT('0000' + CONVERT(varchar(4), @n), 4);
END
GO

alter  PROCEDURE MES.usp_FVN_LotLineCode_Resolve
    @SiteCode nvarchar(30), @IfsProductionLine nvarchar(30), @LotLineCode varchar(3) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @IfsProductionLine = LTRIM(RTRIM(@IfsProductionLine));
    SET @LotLineCode = NULL;
    IF NULLIF(@SiteCode, N'') IS NULL OR NULLIF(@IfsProductionLine, N'') IS NULL RETURN;

    SELECT TOP (1) @LotLineCode = LineCode FROM dbo.FVN_ProductionLine
    WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine;
    IF @LotLineCode IS NOT NULL RETURN;

    SELECT TOP (1) @LotLineCode = LotLineCode FROM MES.FVN_ProductionLotCodeMap
    WHERE SiteCode = @SiteCode AND PlanProductionLineCode = @IfsProductionLine
    ORDER BY IsActive DESC, CreatedAtUtc DESC;
    IF @LotLineCode IS NOT NULL RETURN;

    DECLARE @Description nvarchar(800) =
        (SELECT Description FROM IFS.FVN_ProductionLine WHERE SiteCode = @SiteCode AND ProductionLineCode = @IfsProductionLine);

    DECLARE @src nvarchar(30) = UPPER(@IfsProductionLine), @clean varchar(3) = '', @i int = 1, @c nchar(1);
    WHILE @i <= LEN(@src) AND LEN(@clean) < 3
    BEGIN
        SET @c = SUBSTRING(@src, @i, 1);
        IF @c COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z0-9]' SET @clean = @clean + CONVERT(varchar(1), @c);
        SET @i += 1;
    END;
    DECLARE @base varchar(3) = LEFT(@clean + '000', 3);

    DECLARE @n int = 0, @sfx varchar(2), @cand varchar(3);
    WHILE @n <= 99
    BEGIN
        SET @sfx = CASE WHEN @n = 0 THEN '' ELSE CONVERT(varchar(2), @n) END;
        SET @cand = LEFT(@base, 3 - LEN(@sfx)) + @sfx;
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine AS l
                       WHERE l.SiteCode = @SiteCode AND l.LineCode = @cand
                         AND (l.IfsProductionLineCode IS NOT NULL OR ISNULL(l.LineName, N'') <> ISNULL(@Description, N'')))
           AND NOT EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap AS m
                           WHERE m.SiteCode = @SiteCode AND m.LotLineCode = @cand AND m.PlanProductionLineCode <> @IfsProductionLine)
        BEGIN
            SET @LotLineCode = @cand;
            RETURN;
        END;
        SET @n += 1;
    END;
    THROW 57742, 'Cannot generate a free 3-character LOT line code for this IFS production line.', 1;
END
GO

alter  VIEW MES.vw_FVN_WorkCenterLinkCandidates
AS
SELECT w.SiteCode, w.WorkCenterNo, w.Description, w.WorkCenterCode, w.IsPresentInIfs,
       w.IfsProductionLine, w.IfsProductionLineDescription, w.IfsDepartmentNo, w.IfsDepartmentDescription,
       COALESCE(ll.LineCode, CASE WHEN LEN(w.IfsProductionLine) = 3 THEN w.IfsProductionLine END) AS SuggestedLineCode,
       COALESCE(MES.fn_FVN_DepartmentCodeFromWorkCenter(w.WorkCenterNo),
                CASE WHEN LEN(w.IfsDepartmentNo) = 4 THEN w.IfsDepartmentNo END) AS SuggestedDepartmentCode,
       d.DepartmentCode AS LinkedDepartmentCode, d.LineCode AS LinkedLineCode,
       CASE WHEN d.DepartmentCode IS NULL THEN 'UNLINKED' ELSE 'LINKED' END AS LinkStatus,
       CASE WHEN ps.SiteCode IS NULL THEN 0 ELSE 1 END AS SiteIsAllowListed
FROM IFS.FVN_WorkCenter w
LEFT JOIN dbo.FVN_ProductionDepartment d ON d.SiteCode = w.SiteCode AND d.WorkCenterNo = w.WorkCenterNo
LEFT JOIN dbo.FVN_PCTPSite ps ON ps.SiteCode = w.SiteCode AND ps.IsActive = 1
LEFT JOIN dbo.FVN_ProductionLine ll ON ll.SiteCode = w.SiteCode AND ll.IfsProductionLineCode = w.IfsProductionLine;
GO

alter  PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link
    @SiteCode nvarchar(30), @WorkCenterNo nvarchar(30),
    @DepartmentCode varchar(4) = NULL, @LineCode varchar(3) = NULL,
    @LineName nvarchar(120) = NULL, @DepartmentName nvarchar(120) = NULL,
    @ActorCode nvarchar(100), @IfsProductionLine nvarchar(30) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @WorkCenterNo = LTRIM(RTRIM(@WorkCenterNo));
    SET @DepartmentCode = NULLIF(LTRIM(RTRIM(@DepartmentCode)), ''); SET @LineCode = NULLIF(LTRIM(RTRIM(@LineCode)), '');
    SET @IfsProductionLine = NULLIF(LTRIM(RTRIM(@IfsProductionLine)), N'');
    IF NULLIF(@SiteCode, N'') IS NULL OR NULLIF(@WorkCenterNo, N'') IS NULL
        THROW 56720, 'SiteCode and WorkCenterNo are required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 56723, 'ActorCode is required.', 1;

    /* Department code: given, otherwise IFS WorkCenterNo padded to 4 characters. */
    IF @DepartmentCode IS NULL SET @DepartmentCode = MES.fn_FVN_DepartmentCodeFromWorkCenter(@WorkCenterNo);
    IF @DepartmentCode IS NULL
        THROW 57703, 'WorkCenterNo cannot be converted to a 4-character department code (longer than 4 or not A-Z/0-9); supply DepartmentCode.', 1;
    IF LEN(@DepartmentCode) <> 4 THROW 56721, 'DepartmentCode must contain exactly four characters.', 1;
    IF @LineCode IS NOT NULL AND LEN(@LineCode) <> 3 THROW 56722, 'LineCode must contain exactly three characters.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
            THROW 56724, 'Site is not in the active PCTP site allow-list.', 1;

        DECLARE @WcDescription nvarchar(250), @WcIfsLine nvarchar(30), @MismatchMsg nvarchar(2047);
        SELECT @WcDescription = Description, @WcIfsLine = NULLIF(LTRIM(RTRIM(IfsProductionLine)), N'') FROM IFS.FVN_WorkCenter WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND IsPresentInIfs = 1;
        IF @@ROWCOUNT = 0
            THROW 56725, 'Work center does not exist in the synchronized IFS master for this site.', 1;

        /* IFS production line: IFS decides when the work center has one; otherwise the caller chooses it. */
        IF @WcIfsLine IS NOT NULL AND @IfsProductionLine IS NOT NULL AND @WcIfsLine <> @IfsProductionLine
            THROW 57702, 'Work center belongs to a different IFS production line; IFS is the source of truth.', 1;
        SET @IfsProductionLine = COALESCE(@WcIfsLine, @IfsProductionLine);

        DECLARE @IfsLineDescription nvarchar(800);
        IF @IfsProductionLine IS NOT NULL
        BEGIN
            SELECT @IfsLineDescription = Description FROM IFS.FVN_ProductionLine
            WHERE SiteCode = @SiteCode AND ProductionLineCode = @IfsProductionLine AND IsActive = 1;
            IF @@ROWCOUNT = 0
                THROW 57705, 'IFS production line does not exist or is inactive for this site.', 1;
        END;

        /* LOT line code: given, else the line already tied to this IFS line, else generated. */
        IF @LineCode IS NULL AND @IfsProductionLine IS NOT NULL
            EXEC MES.usp_FVN_LotLineCode_Resolve @SiteCode, @IfsProductionLine, @LineCode OUTPUT;
        IF @LineCode IS NULL
            SELECT @LineCode = LineCode FROM dbo.FVN_ProductionDepartment WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;
        IF @LineCode IS NULL
            THROW 57704, 'Choose the IFS production line (the work center has none in IFS) or supply LineCode.', 1;

        IF @IfsProductionLine IS NOT NULL
           AND EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap
                       WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1 AND PlanProductionLineCode <> @IfsProductionLine)
        BEGIN
            SELECT TOP (1) @MismatchMsg = N'Work Center ' + @WorkCenterNo + N' thuộc chuyền IFS ' + @IfsProductionLine + N', nhưng bộ phận ' + @DepartmentCode +
                   N' đang được mapping cho chuyền IFS ' + PlanProductionLineCode + N'. Sửa mapping (Khai báo chuyền) hoặc chọn bộ phận khác.'
              FROM MES.FVN_ProductionLotCodeMap
             WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1 AND PlanProductionLineCode <> @IfsProductionLine;
            THROW 57700, @MismatchMsg, 1;
        END;

        DECLARE @OtherDept varchar(4);
        SELECT @OtherDept = DepartmentCode FROM dbo.FVN_ProductionDepartment
        WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND DepartmentCode <> @DepartmentCode;
        IF @OtherDept IS NOT NULL
            THROW 56726, 'This work center is already linked to another department; unlink it first.', 1;

        /* Line: reuse it (tie it to the IFS line), or create it with the IFS description as its name. */
        DECLARE @ExistingLineIfs nvarchar(30), @LineExists bit = 0;
        SELECT @ExistingLineIfs = IfsProductionLineCode, @LineExists = 1 FROM dbo.FVN_ProductionLine WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND LineCode = @LineCode;
        IF @LineExists = 0
        BEGIN
            INSERT dbo.FVN_ProductionLine(SiteCode, LineCode, LineName, IsActive, IfsProductionLineCode)
            VALUES (@SiteCode, @LineCode,
                    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(@LineName)), N''), @IfsLineDescription, @IfsProductionLine, @LineCode), 120),
                    1, @IfsProductionLine);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND LineCode = @LineCode AND IsActive = 1)
                THROW 56728, 'Line is inactive.', 1;
            IF @IfsProductionLine IS NOT NULL AND @ExistingLineIfs IS NOT NULL AND @ExistingLineIfs <> @IfsProductionLine
                THROW 57706, 'This LOT line code already belongs to a different IFS production line.', 1;
            IF @IfsProductionLine IS NOT NULL AND @ExistingLineIfs IS NULL
                UPDATE dbo.FVN_ProductionLine SET IfsProductionLineCode = @IfsProductionLine WHERE SiteCode = @SiteCode AND LineCode = @LineCode;
        END;

        DECLARE @ExistingLine varchar(3), @ExistingWc nvarchar(30), @Exists bit = 0;
        SELECT @ExistingLine = LineCode, @ExistingWc = WorkCenterNo, @Exists = 1
        FROM dbo.FVN_ProductionDepartment WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode;

        DECLARE @Action varchar(20) = 'LINK';
        IF @Exists = 0
        BEGIN
            INSERT dbo.FVN_ProductionDepartment(SiteCode, DepartmentCode, DepartmentName, LineCode, IsActive,
                                                WorkCenterNo, LinkedAtUtc, LinkedBy)
            VALUES (@SiteCode, @DepartmentCode,
                    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentName)), N''), @WcDescription, @DepartmentCode), 120),
                    @LineCode, 1, @WorkCenterNo, SYSUTCDATETIME(), @ActorCode);
        END
        ELSE
        BEGIN
            IF @ExistingWc IS NOT NULL AND @ExistingWc <> @WorkCenterNo
                THROW 56729, 'Department is already linked to a different work center; unlink it first.', 1;
            IF @ExistingLine <> @LineCode AND EXISTS (SELECT 1 FROM MES.FVN_ProductionLotCodeMap
                                                     WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND IsActive = 1)
                THROW 56730, 'Department is used by active LOT code maps; it cannot be moved to another line.', 1;

            SET @Action = CASE WHEN @ExistingWc IS NULL THEN 'LINK' ELSE 'RELINK' END;
            UPDATE dbo.FVN_ProductionDepartment
            SET LineCode = @LineCode, WorkCenterNo = @WorkCenterNo, IsActive = 1,
                DepartmentName = LEFT(COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentName)), N''), @WcDescription, DepartmentName), 120),
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

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '078')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('078', N'Work Center link: department / LOT line codes derived from IFS');
GO
SELECT SiteCode, WorkCenterNo, SuggestedDepartmentCode, IfsProductionLine, SuggestedLineCode, LinkStatus
FROM MES.vw_FVN_WorkCenterLinkCandidates ORDER BY SiteCode, WorkCenterNo;
GO
