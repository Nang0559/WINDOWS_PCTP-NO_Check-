/*
 Migration 022: customer-specific LOT normalization and site-aware QR insert contract.
 Prerequisite: 021.
 The special customer trimming behavior is data-driven; this migration does not
 hard-code a customer or silently trim all LOT values.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52200, 'Migration 022 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52201, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '021')
    THROW 52202, 'Migration 021 must be applied before migration 022.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '022')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC(N'
    CREATE FUNCTION dbo.ufn_FVN_NormalizeCustomerLot
    (
        @SiteCode nvarchar(30),
        @PartNo nvarchar(100),
        @CustomerCode nvarchar(60),
        @LotNo nvarchar(400)
    )
    RETURNS nvarchar(400)
    AS
    BEGIN
        IF @LotNo IS NULL OR @LotNo=N'''' RETURN @LotNo;

        DECLARE @Rule varchar(30), @TrimLength tinyint;
        SELECT TOP (1)
            @Rule=LotNormalizationRule,
            @TrimLength=LotTrimRightLength
        FROM dbo.FVN_PartCustomerRule
        WHERE SiteCode=@SiteCode
          AND PartNo=@PartNo
          AND CustomerCode=@CustomerCode
          AND IsActive=1
          AND (EffectiveFrom IS NULL OR EffectiveFrom<=SYSUTCDATETIME())
          AND (EffectiveTo IS NULL OR EffectiveTo>SYSUTCDATETIME())
        ORDER BY CASE WHEN EffectiveFrom IS NULL THEN 0 ELSE 1 END DESC,
                 EffectiveFrom DESC,
                 PartCustomerRuleId DESC;

        IF @Rule=''TRIM_RIGHT'' AND @TrimLength>0 AND LEN(@LotNo)>@TrimLength
            RETURN LEFT(@LotNo,LEN(@LotNo)-@TrimLength);

        RETURN @LotNo;
    END;
    ');

    EXEC(N'
    CREATE PROCEDURE dbo.usp_FVN_DocQrCode_Insert
        @SiteCode nvarchar(30),
        @LOTFCC nvarchar(1000)=NULL,
        @MAHANGFCC nvarchar(60)=NULL,
        @SLTEMFCC smallint=NULL,
        @LOTHVN nvarchar(500)=NULL,
        @MAHANGHVN nvarchar(60)=NULL,
        @SLTEMHVN smallint=NULL,
        @STATUS smallint=NULL,
        @MAFCC nvarchar(50)=NULL,
        @STT smallint=NULL,
        @KETQUA nvarchar(30)=NULL,
        @CUA nvarchar(10)=NULL,
        @TRUYEN nvarchar(50)=NULL,
        @GIO nvarchar(50)=NULL,
        @STTBAN int=NULL,
        @SUALOTHVN nchar(30)=NULL,
        @FindTem nvarchar(50)=NULL,
        @SOPHIEU nvarchar(50)=NULL,
        @PartNoFcc nvarchar(100)=NULL,
        @PartNoHvn nvarchar(100)=NULL,
        @QrRowId bigint OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF NOT EXISTS (SELECT 1 FROM dbo.FVN_PCTPSite WHERE SiteCode=@SiteCode AND IsActive=1)
            THROW 52210, ''PCTP site is not registered or inactive.'', 1;

        IF @PartNoFcc IS NOT NULL AND NOT EXISTS
        (
            SELECT 1 FROM IFS.FVN_InventoryPart
            WHERE SiteCode=@SiteCode AND PartNo=@PartNoFcc
        )
            THROW 52211, ''FCC PartNo does not exist in IFS for this SiteCode.'', 1;

        IF @PartNoHvn IS NOT NULL AND NOT EXISTS
        (
            SELECT 1 FROM IFS.FVN_InventoryPart
            WHERE SiteCode=@SiteCode AND PartNo=@PartNoHvn
        )
            THROW 52212, ''HVN PartNo does not exist in IFS for this SiteCode.'', 1;

        IF NULLIF(LTRIM(RTRIM(@MAHANGFCC)),N'''') IS NOT NULL AND @PartNoFcc IS NULL
            THROW 52213, ''PartNoFcc is required when MAHANGFCC is supplied.'', 1;

        IF NULLIF(LTRIM(RTRIM(@MAHANGHVN)),N'''') IS NOT NULL AND @PartNoHvn IS NULL
            THROW 52214, ''PartNoHvn is required when MAHANGHVN is supplied.'', 1;

        INSERT INTO dbo.FVN_DocQrCode
        (
            SiteCode, LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
            [STATUS], MAFCC, STT, KETQUA, CUA, TRUYEN, GIO, STTBAN, SUALOTHVN,
            FindTem, SOPHIEU, PartNoFcc, PartNoHvn
        )
        VALUES
        (
            @SiteCode, @LOTFCC, @MAHANGFCC, @SLTEMFCC, @LOTHVN, @MAHANGHVN, @SLTEMHVN,
            @STATUS, @MAFCC, @STT, @KETQUA, @CUA, @TRUYEN, @GIO, @STTBAN, @SUALOTHVN,
            @FindTem, @SOPHIEU, @PartNoFcc, @PartNoHvn
        );

        SET @QrRowId=SCOPE_IDENTITY();
    END;
    ');

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('022', N'PCTP customer LOT normalization and site-aware QR insert');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Configure legacy customer/part rules explicitly after source-to-IFS mapping.
   Example rule for the confirmed trim-six case:
   INSERT dbo.FVN_PartCustomerRule
       (SiteCode,PartNo,CustomerCode,LotNormalizationRule,LotTrimRightLength,IsActive)
   VALUES (@SiteCode,@PartNo,N'0100002','TRIM_RIGHT',6,1);
   Never seed this rule for every part without validating the source behavior. */
SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '022';
GO
