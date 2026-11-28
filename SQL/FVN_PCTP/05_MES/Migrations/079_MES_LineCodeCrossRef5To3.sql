/*
 Migration 079: 5-character line code (reference standard) <-> 3-character LOT line code (what the LOT really uses).

 The LOT keeps the 3-character LineCode (dbo.FVN_ProductionLine.LineCode, part of the 20-character LOT). This migration adds the
 definition that links it to the factory's 5-character line code, for cross viewing and for generating both together:
   MES.FVN_LineCodeSitePrefix     first character of the 5-character code per Site (VN2W -> V, VN4W -> 4).
   MES.FVN_LineCodeCrossRef       (SiteCode, LineCode5) <-> (SiteCode, LineCode3), both unique; optional IFS production line.
   MES.vw_FVN_LineCodeCrossRef    cross view with the dbo.FVN_ProductionLine definition status.
   MES.usp_FVN_LineCodeCrossRef_Generate   generates both codes (@Preview = 1 writes nothing).
 5-character standard: prefix(1) + family(2-3 letters) + sequence, or prefix(1) + 4 characters for a single machine.
   Numbered family: family 2 letters -> 2-digit sequence (SK01), 3 letters -> 1 digit (NFP1); next free number of the family.
 3-character code: numbered -> 2 family letters + base-36 sequence (SK01 -> SK1, SK10 -> SKA; a 3-letter family tries its last 2
   letters first: NFP1 -> FP1); single -> first free of body chars 123, 124, 134, 234. A code can also be typed by hand.
 When an IFS line is attached, MES.usp_FVN_LotLineCode_Resolve (078) returns the cross-referenced 3-character code first, so the
 work center link uses the same code. Existing codes are loaded by 079b_MES_LineCodeCrossRefSeed.sql. Apply after 078.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 57745, 'Migration 079 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '078')
    THROW 57746, 'Migration 078 must be registered before 079.', 1;
GO

SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID(N'MES.FVN_LineCodeSitePrefix', N'U') IS NULL
    CREATE TABLE MES.FVN_LineCodeSitePrefix
    (
        SiteCode nvarchar(30) NOT NULL CONSTRAINT PK_FVN_LineCodeSitePrefix PRIMARY KEY,
        Prefix char(1) NOT NULL CONSTRAINT CK_FVN_LineCodeSitePrefix CHECK (Prefix COLLATE Latin1_General_100_BIN2 LIKE '[A-Z0-9]')
    );
IF NOT EXISTS (SELECT 1 FROM MES.FVN_LineCodeSitePrefix WHERE SiteCode = N'VN2W') INSERT MES.FVN_LineCodeSitePrefix VALUES (N'VN2W', 'V');
IF NOT EXISTS (SELECT 1 FROM MES.FVN_LineCodeSitePrefix WHERE SiteCode = N'VN4W') INSERT MES.FVN_LineCodeSitePrefix VALUES (N'VN4W', '4');

IF OBJECT_ID(N'MES.FVN_LineCodeCrossRef', N'U') IS NULL
    CREATE TABLE MES.FVN_LineCodeCrossRef
    (
        SiteCode nvarchar(30) NOT NULL,
        LineCode5 char(5) NOT NULL,
        LineCode3 varchar(3) NOT NULL,
        FamilyCode varchar(3) NULL,
        SeqNo int NULL,
        Description nvarchar(200) NULL,
        IfsProductionLineCode nvarchar(30) NULL,
        IsLegacySeed bit NOT NULL CONSTRAINT DF_FVN_LineCodeCrossRef_Seed DEFAULT(0),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_LineCodeCrossRef_Created DEFAULT(SYSUTCDATETIME()),
        CreatedBy nvarchar(100) NULL,
        CONSTRAINT PK_FVN_LineCodeCrossRef PRIMARY KEY (SiteCode, LineCode5),
        CONSTRAINT UQ_FVN_LineCodeCrossRef_3 UNIQUE (SiteCode, LineCode3),
        CONSTRAINT CK_FVN_LineCodeCrossRef_5 CHECK (LEN(LineCode5) = 5),
        CONSTRAINT CK_FVN_LineCodeCrossRef_3 CHECK (LEN(LineCode3) = 3)
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FVN_LineCodeCrossRef_IfsLine' AND object_id = OBJECT_ID(N'MES.FVN_LineCodeCrossRef'))
    EXEC(N'CREATE UNIQUE INDEX UX_FVN_LineCodeCrossRef_IfsLine ON MES.FVN_LineCodeCrossRef(SiteCode, IfsProductionLineCode) WHERE IfsProductionLineCode IS NOT NULL;');
GO

CREATE OR ALTER VIEW MES.vw_FVN_LineCodeCrossRef
AS
SELECT x.SiteCode, x.LineCode5, x.LineCode3, x.FamilyCode, x.SeqNo, x.Description, x.IfsProductionLineCode,
       i.Description AS IfsProductionLineDescription, l.LineName AS LotLineName,
       CASE WHEN l.LineCode IS NULL THEN 'NOT_DEFINED' WHEN l.IsActive = 0 THEN 'INACTIVE' ELSE 'DEFINED' END AS LotLineStatus,
       x.IsLegacySeed, x.CreatedAtUtc, x.CreatedBy
FROM MES.FVN_LineCodeCrossRef x
LEFT JOIN dbo.FVN_ProductionLine l ON l.SiteCode = x.SiteCode AND l.LineCode = x.LineCode3
LEFT JOIN IFS.FVN_ProductionLine i ON i.SiteCode = x.SiteCode AND i.ProductionLineCode = x.IfsProductionLineCode;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_LineCodeCrossRef_Generate
    @SiteCode nvarchar(30), @FamilyCode varchar(3) = NULL, @Body varchar(4) = NULL,
    @Description nvarchar(200) = NULL, @IfsProductionLine nvarchar(30) = NULL, @LineCode3Override varchar(3) = NULL,
    @ActorCode nvarchar(100) = NULL, @Preview bit = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode));
    SET @FamilyCode = NULLIF(UPPER(LTRIM(RTRIM(@FamilyCode))), ''); SET @Body = NULLIF(UPPER(LTRIM(RTRIM(@Body))), '');
    SET @LineCode3Override = NULLIF(UPPER(LTRIM(RTRIM(@LineCode3Override))), '');
    SET @IfsProductionLine = NULLIF(LTRIM(RTRIM(@IfsProductionLine)), N'');
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'');
    IF NULLIF(@SiteCode, N'') IS NULL THROW 57750, 'SiteCode is required.', 1;
    IF (@FamilyCode IS NULL AND @Body IS NULL) OR (@FamilyCode IS NOT NULL AND @Body IS NOT NULL)
        THROW 57751, 'Give either FamilyCode (numbered machines) or Body (single machine), not both.', 1;
    IF @FamilyCode IS NOT NULL AND (LEN(@FamilyCode) NOT BETWEEN 2 AND 3 OR @FamilyCode COLLATE Latin1_General_100_BIN2 LIKE '%[^A-Z0-9]%')
        THROW 57752, 'FamilyCode must be 2 or 3 letters/digits.', 1;
    IF @Body IS NOT NULL AND (LEN(@Body) <> 4 OR @Body COLLATE Latin1_General_100_BIN2 LIKE '%[^A-Z0-9]%')
        THROW 57753, 'Body must be exactly 4 letters/digits.', 1;
    IF @LineCode3Override IS NOT NULL AND (LEN(@LineCode3Override) <> 3 OR @LineCode3Override COLLATE Latin1_General_100_BIN2 LIKE '%[^A-Z0-9]%')
        THROW 57754, 'The 3-character code must be exactly 3 letters/digits.', 1;
    IF @Preview = 0 AND NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 57755, 'ActorCode is required to save.', 1;

    DECLARE @Prefix char(1) = (SELECT Prefix FROM MES.FVN_LineCodeSitePrefix WHERE SiteCode = @SiteCode);
    IF @Prefix IS NULL THROW 57756, 'The Site has no 5-character prefix (MES.FVN_LineCodeSitePrefix).', 1;

    IF @IfsProductionLine IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_ProductionLine WHERE SiteCode = @SiteCode AND ProductionLineCode = @IfsProductionLine AND IsActive = 1)
            THROW 57757, 'IFS production line does not exist or is inactive for this Site.', 1;
        IF EXISTS (SELECT 1 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine)
            THROW 57758, 'This IFS production line already has a 5/3-character definition.', 1;
    END;

    DECLARE @Code5 char(5), @Seq int = NULL, @Width int, @D char(1);
    IF @FamilyCode IS NOT NULL
    BEGIN
        SET @Width = 4 - LEN(@FamilyCode);
        SELECT @Seq = ISNULL(MAX(SeqNo), 0) + 1 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND FamilyCode = @FamilyCode;
        IF @Seq > CASE WHEN @Width = 1 THEN 9 ELSE 99 END OR @Seq > 35
            THROW 57759, 'The family is full for this code length (5-character sequence or 3-character base-36 limit).', 1;
        SET @D = SUBSTRING('0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ', @Seq + 1, 1);
        SET @Code5 = @Prefix + @FamilyCode + RIGHT(REPLICATE('0', @Width) + CONVERT(varchar(2), @Seq), @Width);
    END
    ELSE SET @Code5 = @Prefix + @Body;

    IF EXISTS (SELECT 1 FROM MES.FVN_LineCodeCrossRef WHERE SiteCode = @SiteCode AND LineCode5 = @Code5)
        THROW 57760, 'This 5-character code already exists.', 1;

    /* 3-character code: forced by an existing LOT line of the IFS line, else typed, else the first free candidate. */
    DECLARE @Code3 varchar(3) = NULL, @Tied varchar(3) = NULL;
    IF @IfsProductionLine IS NOT NULL
        SELECT TOP (1) @Tied = LineCode FROM dbo.FVN_ProductionLine WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine;
    IF @Tied IS NOT NULL AND @LineCode3Override IS NOT NULL AND @LineCode3Override <> @Tied
        THROW 57761, 'The IFS line is already tied to another 3-character LOT line; leave the 3-character code empty.', 1;
    IF @Tied IS NOT NULL
        SET @Code3 = @Tied;
    ELSE
    BEGIN
        DECLARE @c TABLE (Ord int PRIMARY KEY, Code varchar(3) NOT NULL);
        IF @LineCode3Override IS NOT NULL INSERT @c VALUES (1, @LineCode3Override);
        ELSE IF @FamilyCode IS NOT NULL AND LEN(@FamilyCode) = 2 INSERT @c VALUES (1, @FamilyCode + @D);
        ELSE IF @FamilyCode IS NOT NULL INSERT @c VALUES (1, RIGHT(@FamilyCode, 2) + @D), (2, LEFT(@FamilyCode, 2) + @D);
        ELSE INSERT @c VALUES (1, SUBSTRING(@Body, 1, 3)), (2, SUBSTRING(@Body, 1, 2) + SUBSTRING(@Body, 4, 1)),
                              (3, SUBSTRING(@Body, 1, 1) + SUBSTRING(@Body, 3, 2)), (4, SUBSTRING(@Body, 2, 3));

        SELECT TOP (1) @Code3 = c.Code FROM @c AS c
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_LineCodeCrossRef AS x WHERE x.SiteCode = @SiteCode AND x.LineCode3 = c.Code)
          AND NOT EXISTS (SELECT 1 FROM dbo.FVN_ProductionLine AS l
                          WHERE l.SiteCode = @SiteCode AND l.LineCode = c.Code AND l.IfsProductionLineCode IS NOT NULL
                            AND l.IfsProductionLineCode <> ISNULL(@IfsProductionLine, N''))
        ORDER BY c.Ord;
        IF @Code3 IS NULL
            THROW 57762, 'No free 3-character code for this 5-character code; type one by hand.', 1;
    END;

    IF @Preview = 0
    BEGIN
        INSERT MES.FVN_LineCodeCrossRef(SiteCode, LineCode5, LineCode3, FamilyCode, SeqNo, Description, IfsProductionLineCode, CreatedBy)
        VALUES (@SiteCode, @Code5, @Code3, @FamilyCode, @Seq, @Description, @IfsProductionLine, @ActorCode);
    END;
    SELECT @SiteCode AS SiteCode, @Code5 AS LineCode5, @Code3 AS LineCode3, @FamilyCode AS FamilyCode, @Seq AS SeqNo,
           CONVERT(bit, CASE WHEN @Preview = 1 THEN 1 ELSE 0 END) AS IsPreview;
END
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_LotLineCode_Resolve
    @SiteCode nvarchar(30), @IfsProductionLine nvarchar(30), @LotLineCode varchar(3) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @SiteCode = LTRIM(RTRIM(@SiteCode)); SET @IfsProductionLine = LTRIM(RTRIM(@IfsProductionLine));
    SET @LotLineCode = NULL;
    IF NULLIF(@SiteCode, N'') IS NULL OR NULLIF(@IfsProductionLine, N'') IS NULL RETURN;

    /* 079: the 5<->3 character cross reference is the first authority. */
    SELECT TOP (1) @LotLineCode = LineCode3 FROM MES.FVN_LineCodeCrossRef
    WHERE SiteCode = @SiteCode AND IfsProductionLineCode = @IfsProductionLine;
    IF @LotLineCode IS NOT NULL RETURN;

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

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '079')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('079', N'Line code cross reference: 5-character standard <-> 3-character LOT line code');
GO
