/*
 Migration 081: one strict chain for the master data behind LOT codes - every code is picked from a list, never typed.

   IFS work center ──(screen 1: Work Center IFS <-> mã bộ phận 4 ký tự)──> 4-character department code        MES.FVN_DepartmentCodeCrossRef
   IFS production line ──(screen 2: Mã chuyền 5 ký tự <-> 3 ký tự)──────> 3-character LOT line code           MES.FVN_LineCodeCrossRef
   work center + department + LOT line ──(screen 3: Liên kết Work Center)──> dbo.FVN_ProductionLine / dbo.FVN_ProductionDepartment
   IFS line ──(screen 4: Khai báo chuyền sản xuất)──> MES.FVN_ProductionLotCodeMap, choosing only the LOT line and department of screen 3

 Changes:
   - MES.vw_FVN_WorkCenterCodeStatus: EVERY work center synchronized from IFS with its department code (CodeStatus NO_CODE / HAS_CODE), so screen 1
     lists the IFS work centers instead of an empty cross reference table.
   - MES.vw_FVN_IfsLineCodeStatus: EVERY active IFS production line with its 5 / 3 character codes (same columns as MES.vw_FVN_LineCodeCrossRef plus CodeStatus).
   - MES.usp_FVN_SiteDepartmentLine_Link: department code only from the work center cross reference (57822), LOT line code only from the line cross
     reference or the LOT line already tied to the IFS line (57823 / 57824); nothing is generated or registered silently any more.
   - MES.usp_FVN_ProductionLotCodeMap_Save: no longer creates dbo.FVN_ProductionLine / dbo.FVN_ProductionDepartment rows. The LOT line must already
     belong to the IFS line (57820) and the department must be linked to a work center on that LOT line (57821).
 Written with stub + ALTER like 080 (not the one-statement create form). Apply after 080.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57810, 'Migration 081 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '080')
    THROW 57811, 'Migration 080 must be registered before 081.', 1;
IF OBJECT_ID(N'MES.FVN_DepartmentCodeCrossRef', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_LineCodeCrossRef', N'U') IS NULL
    THROW 57812, 'The line / department cross reference tables are missing (migrations 079 / 080).', 1;
GO

SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'MES.vw_FVN_WorkCenterCodeStatus', N'V') IS NULL EXEC(N'CREATE VIEW MES.vw_FVN_WorkCenterCodeStatus AS SELECT 1 AS X;');
GO
ALTER VIEW MES.vw_FVN_WorkCenterCodeStatus
AS
SELECT w.SiteCode, w.WorkCenterNo, w.Description AS IfsWorkCenterDescription, w.IsPresentInIfs,
       NULLIF(LTRIM(RTRIM(w.IfsProductionLine)), N'') AS IfsProductionLine,
       x.DepartmentCode, x.Description,
       CASE WHEN x.DepartmentCode IS NULL THEN 'NO_CODE' ELSE 'HAS_CODE' END AS CodeStatus,
       d.DepartmentName AS LotDepartmentName, d.LineCode AS LotLineCode,
       CASE WHEN x.DepartmentCode IS NULL THEN 'NO_CODE'
            WHEN d.DepartmentCode IS NULL THEN 'NOT_DEFINED'
            WHEN d.WorkCenterNo = x.WorkCenterNo THEN 'LINKED'
            ELSE 'DEFINED_OTHER_WORKCENTER' END AS LotDepartmentStatus,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy, x.UpdatedAtUtc, x.UpdatedBy
FROM IFS.FVN_WorkCenter AS w
LEFT JOIN MES.FVN_DepartmentCodeCrossRef AS x ON x.SiteCode = w.SiteCode AND x.WorkCenterNo = w.WorkCenterNo
LEFT JOIN dbo.FVN_ProductionDepartment AS d ON d.SiteCode = x.SiteCode AND d.DepartmentCode = x.DepartmentCode
WHERE w.IsPresentInIfs = 1
UNION ALL
SELECT x.SiteCode, x.WorkCenterNo, NULL, CONVERT(bit, 0), NULL,
       x.DepartmentCode, x.Description, 'HAS_CODE',
       d.DepartmentName, d.LineCode,
       CASE WHEN d.DepartmentCode IS NULL THEN 'NOT_DEFINED'
            WHEN d.WorkCenterNo = x.WorkCenterNo THEN 'LINKED'
            ELSE 'DEFINED_OTHER_WORKCENTER' END,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy, x.UpdatedAtUtc, x.UpdatedBy
FROM MES.FVN_DepartmentCodeCrossRef AS x
LEFT JOIN dbo.FVN_ProductionDepartment AS d ON d.SiteCode = x.SiteCode AND d.DepartmentCode = x.DepartmentCode
WHERE NOT EXISTS (SELECT 1 FROM IFS.FVN_WorkCenter AS w
                  WHERE w.SiteCode = x.SiteCode AND w.WorkCenterNo = x.WorkCenterNo AND w.IsPresentInIfs = 1);
GO

SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'MES.vw_FVN_IfsLineCodeStatus', N'V') IS NULL EXEC(N'CREATE VIEW MES.vw_FVN_IfsLineCodeStatus AS SELECT 1 AS X;');
GO
ALTER VIEW MES.vw_FVN_IfsLineCodeStatus
AS
SELECT l.SiteCode, x.LineCode5, x.LineCode3, x.FamilyCode, x.SeqNo, x.Description,
       l.ProductionLineCode AS IfsProductionLineCode, l.Description AS IfsProductionLineDescription,
       ll.LineName AS LotLineName,
       CASE WHEN x.LineCode3 IS NULL THEN 'NO_CODE'
            WHEN ll.LineCode IS NULL THEN 'NOT_DEFINED' WHEN ll.IsActive = 0 THEN 'INACTIVE' ELSE 'DEFINED' END AS LotLineStatus,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy, x.UpdatedAtUtc, x.UpdatedBy,
       CASE WHEN x.LineCode3 IS NULL THEN 'NO_CODE' ELSE 'HAS_CODE' END AS CodeStatus
FROM IFS.FVN_ProductionLine AS l
LEFT JOIN MES.FVN_LineCodeCrossRef AS x ON x.SiteCode = l.SiteCode AND x.IfsProductionLineCode = l.ProductionLineCode
LEFT JOIN dbo.FVN_ProductionLine AS ll ON ll.SiteCode = x.SiteCode AND ll.LineCode = x.LineCode3
WHERE l.IsActive = 1
UNION ALL
SELECT x.SiteCode, x.LineCode5, x.LineCode3, x.FamilyCode, x.SeqNo, x.Description,
       x.IfsProductionLineCode, i.Description,
       ll.LineName,
       CASE WHEN ll.LineCode IS NULL THEN 'NOT_DEFINED' WHEN ll.IsActive = 0 THEN 'INACTIVE' ELSE 'DEFINED' END,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy, x.UpdatedAtUtc, x.UpdatedBy,
       'HAS_CODE'
FROM MES.FVN_LineCodeCrossRef AS x
LEFT JOIN IFS.FVN_ProductionLine AS i ON i.SiteCode = x.SiteCode AND i.ProductionLineCode = x.IfsProductionLineCode
LEFT JOIN dbo.FVN_ProductionLine AS ll ON ll.SiteCode = x.SiteCode AND ll.LineCode = x.LineCode3
WHERE NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine AS a
                  WHERE a.SiteCode = x.SiteCode AND a.ProductionLineCode = x.IfsProductionLineCode AND a.IsActive = 1);
GO

IF OBJECT_ID(N'MES.usp_FVN_ProductionLotCodeMap_Save', N'P') IS NULL
    THROW 57813, 'MES.usp_FVN_ProductionLotCodeMap_Save is missing (migration 067).', 1;
GO
ALTER PROCEDURE MES.usp_FVN_ProductionLotCodeMap_Save
    @SiteCode               nvarchar(30),
    @PlanProductionLineCode nvarchar(30),
    @LotLineCode            varchar(3),
    @DepartmentCode         varchar(4),
    @IsActive               bit = 1,
    @ActorCode              nvarchar(100)
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
    IF LEN(@LotLineCode) <> 3 THROW 55712, N'Mã chuyền LOT phải đúng 3 ký tự.', 1;
    IF LEN(@DepartmentCode) <> 4 THROW 55713, N'Mã bộ phận phải đúng 4 ký tự.', 1;

    IF @IsActive = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode = @SiteCode AND IsActive = 1)
            THROW 55714, N'Site chưa được khai báo (dbo.FVN_PCTPSite) hoặc đang ngừng dùng.', 1;
        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_Site WHERE SiteCode = @SiteCode AND IsPresentInIfs = 1)
            THROW 57511, 'Site is not present in IFS.FVN_Site; synchronize SITE before activating this mapping.', 1;
        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine WHERE SiteCode = @SiteCode AND ProductionLineCode = @PlanProductionLineCode)
            THROW 55715, N'Chuyền này chưa có trong IFS.FVN_ProductionLine của Site. Đồng bộ PRODUCTION_LINE từ IFS trước.', 1;

        /* 081: the LOT line and the department are master data defined beforehand; this procedure only chooses among them. */
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine
                       WHERE SiteCode = @SiteCode AND LineCode = @LotLineCode AND IsActive = 1
                         AND IfsProductionLineCode = @PlanProductionLineCode)
            THROW 57820, N'Mã chuyền LOT chưa được định nghĩa cho chuyền IFS này. Làm theo thứ tự: màn "Mã chuyền 5 ký tự ↔ 3 ký tự (LOT)" rồi "Liên kết Work Center", sau đó chọn lại.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment
                       WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND LineCode = @LotLineCode
                         AND IsActive = 1 AND WorkCenterNo IS NOT NULL)
            THROW 57821, N'Bộ phận này chưa liên kết Work Center thuộc chuyền LOT đã chọn. Mở màn "Liên kết Work Center" để liên kết trước, sau đó chọn lại.', 1;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

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

IF OBJECT_ID(N'MES.usp_FVN_SiteDepartmentLine_Link', N'P') IS NULL
    THROW 57814, 'MES.usp_FVN_SiteDepartmentLine_Link is missing (migrations 068 / 077 / 078 / 080).', 1;
GO
ALTER PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link
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

    /* 081: the department code comes only from the work center cross reference (screen "Work Center IFS <-> mã bộ phận 4 ký tự (LOT)"). */
    DECLARE @XrefDept varchar(4) = (SELECT DepartmentCode FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo);
    IF @XrefDept IS NULL
        THROW 57822, N'Work Center chưa có mã bộ phận 4 ký tự. Khai báo ở màn "Work Center IFS ↔ mã bộ phận 4 ký tự (LOT)" trước khi liên kết.', 1;
    IF @DepartmentCode IS NOT NULL AND @DepartmentCode <> @XrefDept
        THROW 57785, N'Mã bộ phận khác mã đã khai báo cho Work Center này ở màn "Work Center IFS ↔ mã bộ phận 4 ký tự (LOT)".', 1;
    SET @DepartmentCode = @XrefDept;
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

        /* 081: the LOT line code comes only from the 5 <-> 3 cross reference (or the LOT line already tied to the IFS line); it is never generated here. */
        IF @IfsProductionLine IS NOT NULL
        BEGIN
            DECLARE @XrefLine varchar(3) = (SELECT LineCode3 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine);
            IF @XrefLine IS NULL
                SET @XrefLine = (SELECT LineCode FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine);
            IF @XrefLine IS NULL
                THROW 57823, N'Chuyền IFS chưa có mã chuyền LOT 3 ký tự. Khai báo ở màn "Mã chuyền 5 ký tự ↔ 3 ký tự (LOT)" trước khi liên kết.', 1;
            IF @LineCode IS NOT NULL AND @LineCode <> @XrefLine
                THROW 57824, N'Mã chuyền LOT khác mã đã khai báo cho chuyền IFS này ở màn "Mã chuyền 5 ký tự ↔ 3 ký tự (LOT)".', 1;
            SET @LineCode = @XrefLine;
        END;
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

        /* 081: the cross reference is defined beforehand (see the top of this procedure); it is no longer registered here. */

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
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '081')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('081', N'Strict master data chain: list every IFS work center / line, LOT codes only from cross references, map save and Link stop creating master rows');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '081';
GO
