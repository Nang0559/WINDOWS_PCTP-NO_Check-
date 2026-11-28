/*
 Migration 080: work center / department cross reference (IFS reference code <-> 4-character LOT department code) and manual
 maintenance of both cross references.

 IFS work center numbers run up to 5 characters (1100E, 19A) while dbo.FVN_ProductionDepartment.DepartmentCode is the 4 characters used
 in the LOT. MES.FVN_DepartmentCodeCrossRef defines (SiteCode, WorkCenterNo) <-> (SiteCode, DepartmentCode), both unique.
 Generation (MES.usp_FVN_DepartmentCodeCrossRef_Generate, @Preview = 1 writes nothing):
   - 1-4 letters/digits: padded with '0' in front (19A -> 019A);
   - 5 letters/digits: the 4 characters that keep the suffix, first free of c1c2c3c5, c1c2c4c5, c1c3c4c5, c2c3c4c5, c1c2c3c4 (1100E -> 110E);
   - anything else, or a taken code: type the code by hand.
 Maintenance when new records arrive from IFS (both cross references): MES.usp_FVN_LineCodeCrossRef_Save / _Delete and
 MES.usp_FVN_DepartmentCodeCrossRef_Save / _Delete add or edit a row by hand. A 3/4-character code already defined in PCTP
 (dbo.FVN_ProductionLine / dbo.FVN_ProductionDepartment) cannot be changed or deleted from here.
 MES.usp_FVN_SiteDepartmentLine_Link now consults the work center cross reference first and registers the row when it is missing.
 Existing links in dbo.FVN_ProductionDepartment are copied into the cross reference. Written with stub + ALTER instead of the one-statement create form
 so it runs on any SQL Server that has THROW (2012+). Apply after 079.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57770, 'Migration 080 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '079')
    THROW 57771, 'Migration 079 must be registered before 080.', 1;
GO

IF COL_LENGTH(N'MES.FVN_LineCodeCrossRef', N'UpdatedAtUtc') IS NULL
    EXEC(N'ALTER TABLE MES.FVN_LineCodeCrossRef ADD UpdatedAtUtc datetime2(3) NULL, UpdatedBy nvarchar(100) NULL;');

IF OBJECT_ID(N'MES.FVN_DepartmentCodeCrossRef', N'U') IS NULL
    CREATE TABLE MES.FVN_DepartmentCodeCrossRef
    (
        SiteCode nvarchar(30) NOT NULL,
        WorkCenterNo nvarchar(30) NOT NULL,
        DepartmentCode varchar(4) NOT NULL,
        Description nvarchar(250) NULL,
        IsLegacySeed bit NOT NULL CONSTRAINT DF_FVN_DepartmentCodeCrossRef_Seed DEFAULT(0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DepartmentCodeCrossRef_Created DEFAULT(SYSUTCDATETIME()),
        CreatedBy nvarchar(100) NULL,
        UpdatedAtUtc datetime2(3) NULL,
        UpdatedBy nvarchar(100) NULL,
        CONSTRAINT PK_FVN_DepartmentCodeCrossRef PRIMARY KEY (SiteCode, WorkCenterNo),
        CONSTRAINT UQ_FVN_DepartmentCodeCrossRef_Dept UNIQUE (SiteCode, DepartmentCode),
        CONSTRAINT CK_FVN_DepartmentCodeCrossRef_Dept CHECK (LEN(DepartmentCode) = 4)
    );
GO

/* Existing links become cross reference rows. */
INSERT MES.FVN_DepartmentCodeCrossRef(SiteCode, WorkCenterNo, DepartmentCode, Description, IsLegacySeed, CreatedBy)
SELECT d.SiteCode, d.WorkCenterNo, d.DepartmentCode, LEFT(d.DepartmentName, 250), 1, N'migration-080'
FROM dbo.FVN_ProductionDepartment AS d
WHERE d.WorkCenterNo IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM MES.FVN_DepartmentCodeCrossRef x WHERE x.SiteCode = d.SiteCode AND x.WorkCenterNo = d.WorkCenterNo)
  AND NOT EXISTS (SELECT 1 FROM MES.FVN_DepartmentCodeCrossRef x WHERE x.SiteCode = d.SiteCode AND x.DepartmentCode = d.DepartmentCode);
GO

/* 079 view + the audit columns added above. */
ALTER VIEW MES.vw_FVN_LineCodeCrossRef
AS
SELECT x.SiteCode, x.LineCode5, x.LineCode3, x.FamilyCode, x.SeqNo, x.Description, x.IfsProductionLineCode,
       i.Description AS IfsProductionLineDescription, l.LineName AS LotLineName,
       CASE WHEN l.LineCode IS NULL THEN 'NOT_DEFINED' WHEN l.IsActive = 0 THEN 'INACTIVE' ELSE 'DEFINED' END AS LotLineStatus,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy, x.UpdatedAtUtc, x.UpdatedBy
FROM MES.FVN_LineCodeCrossRef x
LEFT JOIN dbo.FVN_ProductionLine l ON l.SiteCode = x.SiteCode AND l.LineCode = x.LineCode3
LEFT JOIN IFS.FVN_ProductionLine i ON i.SiteCode = x.SiteCode AND i.ProductionLineCode = x.IfsProductionLineCode;
GO

IF OBJECT_ID(N'MES.vw_FVN_DepartmentCodeCrossRef', N'V') IS NULL EXEC(N'CREATE VIEW MES.vw_FVN_DepartmentCodeCrossRef AS SELECT 1 AS X;');
GO
ALTER VIEW MES.vw_FVN_DepartmentCodeCrossRef
AS
SELECT x.SiteCode, x.WorkCenterNo, x.DepartmentCode, x.Description,
       w.Description AS IfsWorkCenterDescription, w.IsPresentInIfs,
       d.DepartmentName AS LotDepartmentName, d.LineCode AS LotLineCode,
       CASE WHEN d.DepartmentCode IS NULL THEN 'NOT_DEFINED'
            WHEN d.WorkCenterNo = x.WorkCenterNo THEN 'LINKED'
            ELSE 'DEFINED_OTHER_WORKCENTER' END AS LotDepartmentStatus,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy, x.UpdatedAtUtc, x.UpdatedBy
FROM MES.FVN_DepartmentCodeCrossRef x
LEFT JOIN IFS.FVN_WorkCenter w ON w.SiteCode = x.SiteCode AND w.WorkCenterNo = x.WorkCenterNo
LEFT JOIN dbo.FVN_ProductionDepartment d ON d.SiteCode = x.SiteCode AND d.DepartmentCode = x.DepartmentCode;
GO

IF OBJECT_ID(N'MES.usp_FVN_DepartmentCodeCrossRef_Generate', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_DepartmentCodeCrossRef_Generate AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_DepartmentCodeCrossRef_Generate
    @SiteCode nvarchar(30), @WorkCenterNo nvarchar(30), @DepartmentOverride varchar(4) = NULL,
    @Description nvarchar(250) = NULL, @ActorCode nvarchar(100) = NULL, @Preview bit = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @WorkCenterNo = LTRIM(RTRIM(@WorkCenterNo));
    SET @DepartmentOverride = NULLIF(UPPER(LTRIM(RTRIM(@DepartmentOverride))), '');
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'');
    IF NULLIF(@SiteCode, N'') IS NULL OR NULLIF(@WorkCenterNo, N'') IS NULL THROW 57780, 'SiteCode and WorkCenterNo are required.', 1;
    IF @DepartmentOverride IS NOT NULL AND (LEN(@DepartmentOverride) <> 4 OR @DepartmentOverride COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Z]%')
        THROW 57781, 'The department code must be exactly 4 letters/digits.', 1;
    IF @Preview = 0 AND NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57782, 'ActorCode is required to save.', 1;

    DECLARE @WcDescription nvarchar(250);
    SELECT @WcDescription = Description FROM IFS.FVN_WorkCenter WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo AND IsPresentInIfs = 1;
    IF @@ROWCOUNT = 0 THROW 57783, 'Work center does not exist in the synchronized IFS master for this Site.', 1;

    DECLARE @Code varchar(4) = NULL;
    SELECT @Code = DepartmentCode FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo;
    IF @Code IS NOT NULL
    BEGIN
        SELECT @SiteCode AS SiteCode, @WorkCenterNo AS WorkCenterNo, @Code AS DepartmentCode, CONVERT(bit, 1) AS IsExisting,
               CONVERT(bit, CASE WHEN @Preview = 1 THEN 1 ELSE 0 END) AS IsPreview;
        RETURN;
    END;

    DECLARE @n nvarchar(30) = UPPER(@WorkCenterNo);
    DECLARE @c TABLE (Ord int PRIMARY KEY, Code varchar(4) NOT NULL);
    IF @DepartmentOverride IS NOT NULL
        INSERT @c VALUES (1, @DepartmentOverride);
    ELSE IF LEN(@n) BETWEEN 1 AND 4 AND @n COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Z]%'
        INSERT @c VALUES (1, RIGHT('0000' + CONVERT(varchar(4), @n), 4));
    ELSE IF LEN(@n) = 5 AND @n COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Z]%'
        INSERT @c VALUES
            (1, SUBSTRING(@n, 1, 3) + SUBSTRING(@n, 5, 1)), (2, SUBSTRING(@n, 1, 2) + SUBSTRING(@n, 4, 2)),
            (3, SUBSTRING(@n, 1, 1) + SUBSTRING(@n, 3, 3)), (4, SUBSTRING(@n, 2, 4)), (5, SUBSTRING(@n, 1, 4));

    SELECT TOP (1) @Code = c.Code FROM @c AS c
    WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DepartmentCodeCrossRef AS x WHERE x.SiteCode = @SiteCode AND x.DepartmentCode = c.Code)
      AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment AS d
                      WHERE d.SiteCode = @SiteCode AND d.DepartmentCode = c.Code AND d.WorkCenterNo IS NOT NULL AND d.WorkCenterNo <> @WorkCenterNo)
    ORDER BY c.Ord;
    IF @Code IS NULL
        THROW 57784, 'No free 4-character department code can be generated for this work center; type one by hand.', 1;

    IF @Preview = 0
        INSERT MES.FVN_DepartmentCodeCrossRef(SiteCode, WorkCenterNo, DepartmentCode, Description, CreatedBy)
        VALUES (@SiteCode, @WorkCenterNo, @Code, LEFT(COALESCE(@Description, @WcDescription), 250), @ActorCode);
    SELECT @SiteCode AS SiteCode, @WorkCenterNo AS WorkCenterNo, @Code AS DepartmentCode, CONVERT(bit, 0) AS IsExisting,
           CONVERT(bit, CASE WHEN @Preview = 1 THEN 1 ELSE 0 END) AS IsPreview;
END
GO

IF OBJECT_ID(N'MES.usp_FVN_DepartmentCodeCrossRef_Save', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_DepartmentCodeCrossRef_Save AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_DepartmentCodeCrossRef_Save
    @SiteCode nvarchar(30), @WorkCenterNo nvarchar(30), @DepartmentCode varchar(4),
    @Description nvarchar(250) = NULL, @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @WorkCenterNo = LTRIM(RTRIM(@WorkCenterNo));
    SET @DepartmentCode = UPPER(LTRIM(RTRIM(@DepartmentCode))); SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'');
    IF NULLIF(@SiteCode, N'') IS NULL OR NULLIF(@WorkCenterNo, N'') IS NULL THROW 57790, 'SiteCode and WorkCenterNo are required.', 1;
    IF LEN(ISNULL(@DepartmentCode, '')) <> 4 OR @DepartmentCode COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Z]%'
        THROW 57791, 'The department code must be exactly 4 letters/digits.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57792, 'ActorCode is required.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_WorkCenter WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo)
            THROW 57793, 'Work center does not exist in the synchronized IFS master for this Site.', 1;
        IF EXISTS (SELECT 1 FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode AND WorkCenterNo <> @WorkCenterNo)
            THROW 57794, 'This department code already belongs to another work center in the cross reference.', 1;
        IF EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode
                   AND WorkCenterNo IS NOT NULL AND WorkCenterNo <> @WorkCenterNo)
            THROW 57795, 'This department code is already linked to another work center in PCTP.', 1;

        DECLARE @Old varchar(4), @Exists bit = 0;
        SELECT @Old = DepartmentCode, @Exists = 1 FROM MES.FVN_DepartmentCodeCrossRef WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo;
        IF @Exists = 1 AND @Old <> @DepartmentCode
           AND EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo)
            THROW 57796, 'This work center is already linked to a PCTP department; unlink it before changing its department code.', 1;

        IF @Exists = 0
            INSERT MES.FVN_DepartmentCodeCrossRef(SiteCode, WorkCenterNo, DepartmentCode, Description, CreatedBy)
            VALUES (@SiteCode, @WorkCenterNo, @DepartmentCode, @Description, @ActorCode);
        ELSE
            UPDATE MES.FVN_DepartmentCodeCrossRef
            SET DepartmentCode = @DepartmentCode, Description = @Description, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorCode
            WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo;
        COMMIT TRANSACTION;
        SELECT * FROM MES.vw_FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'MES.usp_FVN_DepartmentCodeCrossRef_Delete', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_DepartmentCodeCrossRef_Delete AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_DepartmentCodeCrossRef_Delete
    @SiteCode nvarchar(30), @WorkCenterNo nvarchar(30), @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @WorkCenterNo = LTRIM(RTRIM(@WorkCenterNo));
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57792, 'ActorCode is required.', 1;
    DECLARE @Dept varchar(4) = (SELECT DepartmentCode FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo);
    IF @Dept IS NULL THROW 57797, 'Cross reference row not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.FVN_ProductionDepartment WHERE SiteCode = @SiteCode AND DepartmentCode = @Dept)
        THROW 57798, 'This department code is already defined in PCTP (dbo.FVN_ProductionDepartment); it cannot be deleted here.', 1;
    DELETE MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo;
END
GO

IF OBJECT_ID(N'MES.usp_FVN_LineCodeCrossRef_Save', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_LineCodeCrossRef_Save AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_LineCodeCrossRef_Save
    @SiteCode nvarchar(30), @LineCode5 varchar(5), @LineCode3 varchar(3),
    @Description nvarchar(200) = NULL, @IfsProductionLine nvarchar(30) = NULL,
    @FamilyCode varchar(3) = NULL, @SeqNo int = NULL,
    @OriginalLineCode5 varchar(5) = NULL, @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode));
    SET @LineCode5 = UPPER(LTRIM(RTRIM(@LineCode5))); SET @LineCode3 = UPPER(LTRIM(RTRIM(@LineCode3)));
    SET @OriginalLineCode5 = NULLIF(UPPER(LTRIM(RTRIM(@OriginalLineCode5))), '');
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N''); SET @IfsProductionLine = NULLIF(LTRIM(RTRIM(@IfsProductionLine)), N'');
    SET @FamilyCode = NULLIF(UPPER(LTRIM(RTRIM(@FamilyCode))), '');
    IF NULLIF(@SiteCode, N'') IS NULL THROW 57800, 'SiteCode is required.', 1;
    IF LEN(ISNULL(@LineCode5, '')) <> 5 OR @LineCode5 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Z]%'
        THROW 57801, 'The 5-character code must be exactly 5 letters/digits.', 1;
    IF LEN(ISNULL(@LineCode3, '')) <> 3 OR @LineCode3 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Z]%'
        THROW 57802, 'The 3-character code must be exactly 3 letters/digits.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57803, 'ActorCode is required.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Key5 varchar(5) = ISNULL(@OriginalLineCode5, @LineCode5), @Old3 varchar(3), @Exists bit = 0;
        SELECT @Old3 = LineCode3, @Exists = 1 FROM MES.FVN_LineCodeCrossRef WITH (UPDLOCK, HOLDLOCK)
        WHERE SiteCode = @SiteCode AND LineCode5 = @Key5;
        IF @OriginalLineCode5 IS NOT NULL AND @Exists = 0 THROW 57804, 'The row being edited no longer exists.', 1;

        IF (@Exists = 0 OR @Key5 <> @LineCode5)
           AND EXISTS (SELECT 1 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND LineCode5 = @LineCode5)
            THROW 57805, 'This 5-character code already exists.', 1;
        IF EXISTS (SELECT 1 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND LineCode3 = @LineCode3 AND LineCode5 <> @Key5)
            THROW 57806, 'This 3-character code already belongs to another 5-character code.', 1;

        IF @IfsProductionLine IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine WHERE SiteCode = @SiteCode AND ProductionLineCode = @IfsProductionLine)
                THROW 57807, 'IFS production line does not exist for this Site.', 1;
            IF EXISTS (SELECT 1 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine AND LineCode5 <> @Key5)
                THROW 57808, 'This IFS production line already has a 5/3-character definition.', 1;
            IF EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine AND LineCode <> @LineCode3)
                THROW 57809, 'This IFS production line is already tied to another 3-character LOT line in PCTP.', 1;
        END;
        IF EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND LineCode = @LineCode3
                   AND IfsProductionLineCode IS NOT NULL AND IfsProductionLineCode <> ISNULL(@IfsProductionLine, N''))
            THROW 57810, 'This 3-character code is already used by a PCTP line tied to a different IFS line.', 1;
        IF @Exists = 1 AND @Old3 <> @LineCode3 AND EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND LineCode = @Old3)
            THROW 57811, 'The current 3-character code is already defined in PCTP (dbo.FVN_ProductionLine); it cannot be changed here.', 1;

        IF @Exists = 0
            INSERT MES.FVN_LineCodeCrossRef(SiteCode, LineCode5, LineCode3, FamilyCode, SeqNo, Description, IfsProductionLineCode, CreatedBy)
            VALUES (@SiteCode, @LineCode5, @LineCode3, @FamilyCode, @SeqNo, @Description, @IfsProductionLine, @ActorCode);
        ELSE
            UPDATE MES.FVN_LineCodeCrossRef
            SET LineCode5 = @LineCode5, LineCode3 = @LineCode3, FamilyCode = @FamilyCode, SeqNo = @SeqNo, Description = @Description,
                IfsProductionLineCode = @IfsProductionLine, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorCode
            WHERE SiteCode = @SiteCode AND LineCode5 = @Key5;
        COMMIT TRANSACTION;
        SELECT * FROM MES.vw_FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND LineCode5 = @LineCode5;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF OBJECT_ID(N'MES.usp_FVN_LineCodeCrossRef_Delete', N'P') IS NULL EXEC(N'CREATE PROCEDURE MES.usp_FVN_LineCodeCrossRef_Delete AS RETURN 0;');
GO
ALTER PROCEDURE MES.usp_FVN_LineCodeCrossRef_Delete
    @SiteCode nvarchar(30), @LineCode5 varchar(5), @ActorCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @LineCode5 = UPPER(LTRIM(RTRIM(@LineCode5)));
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57803, 'ActorCode is required.', 1;
    DECLARE @C3 varchar(3) = (SELECT LineCode3 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND LineCode5 = @LineCode5);
    IF @C3 IS NULL THROW 57812, 'Cross reference row not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND LineCode = @C3)
        THROW 57813, 'This 3-character code is already defined in PCTP (dbo.FVN_ProductionLine); it cannot be deleted here.', 1;
    DELETE MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND LineCode5 = @LineCode5;
END
GO

IF OBJECT_ID(N'MES.usp_FVN_SiteDepartmentLine_Link', N'P') IS NULL
    THROW 57772, 'MES.usp_FVN_SiteDepartmentLine_Link is missing (migrations 068 / 077 / 078).', 1;
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

    /* Department code: given, otherwise the cross reference (080), otherwise IFS WorkCenterNo padded to 4 characters. */
    IF @DepartmentCode IS NULL
        SELECT @DepartmentCode = DepartmentCode FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo;
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

        /* 080: the work center / department cross reference must agree with the department used; register it when missing. */
        DECLARE @XrefDept varchar(4) = (SELECT DepartmentCode FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND WorkCenterNo = @WorkCenterNo);
        IF @XrefDept IS NOT NULL AND @XrefDept <> @DepartmentCode
            THROW 57785, 'The work center cross reference defines a different department code; edit the cross reference or use that code.', 1;
        IF @XrefDept IS NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM MES.FVN_DepartmentCodeCrossRef WHERE SiteCode = @SiteCode AND DepartmentCode = @DepartmentCode)
                THROW 57786, 'This department code already belongs to another work center in the cross reference.', 1;
            INSERT MES.FVN_DepartmentCodeCrossRef(SiteCode, WorkCenterNo, DepartmentCode, Description, CreatedBy)
            VALUES (@SiteCode, @WorkCenterNo, @DepartmentCode, LEFT(@WcDescription, 250), @ActorCode);
        END;

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

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '080')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('080', N'Work center / department cross reference; manual maintenance of the line and department cross references');
GO
