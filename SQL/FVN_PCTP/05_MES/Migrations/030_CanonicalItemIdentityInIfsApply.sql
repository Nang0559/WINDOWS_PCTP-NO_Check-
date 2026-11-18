/*
 Migration 030: make MES.FVN_ItemIdentity the only allocator used by IFS apply.
 Migration 028 introduced a legacy allocator in dbo.FVN_PartIdentity.
 Migration 029 introduced the canonical MES identity tables and compatibility trigger.
 This migration removes the old allocation block from IFS.usp_ApplySyncBatch and
 delegates allocation to MES.usp_FVN_AllocateItemId5. dbo.FVN_PartIdentity remains
 only as a compatibility projection for old PCTP readers.
 SQL Server 2016 compatible; IFS remains read-only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 53000, 'Migration 030 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL
    THROW 53001, 'MES.FVN_SchemaMigration is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='029')
    THROW 53002, 'Migration 029 must be applied before migration 030.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='030')
    RETURN;
IF OBJECT_ID(N'IFS.usp_ApplySyncBatch',N'P') IS NULL
   OR OBJECT_ID(N'MES.FVN_ItemIdentity',N'U') IS NULL
   OR OBJECT_ID(N'MES.usp_FVN_AllocateItemId5',N'P') IS NULL
   OR OBJECT_ID(N'dbo.FVN_PartIdentity',N'U') IS NULL
    THROW 53003, 'Canonical identity or IFS apply objects are missing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Definition nvarchar(max),
            @Start int,
            @Finish int,
            @Replacement nvarchar(max);

    SET @Definition=OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch',N'P'));
    IF @Definition IS NULL
        THROW 53004, 'Cannot read IFS.usp_ApplySyncBatch definition.', 1;

    /* The exact block below was injected by migration 028. Replace it as a unit. */
    SET @Start=CHARINDEX(
        N'/* Allocate one stable five-character MES item identity for every synchronized IFS part. */',
        @Definition);

    IF @Start>0
    BEGIN
        SET @Finish=CHARINDEX(N'UPDATE IFS.SyncBatch',@Definition,@Start);
        IF @Finish=0
            THROW 53005, 'Cannot locate the end of migration 028 item allocator block.', 1;

        SET @Replacement=N'
        /* Canonical allocator: MES.FVN_ItemIdentity is the sole source of ItemId5.
           The legacy dbo.FVN_PartIdentity table is maintained by the compatibility
           procedure/trigger and is never used to allocate a new token here. */
        DECLARE @Fvn030Site nvarchar(30),
                @Fvn030Part nvarchar(100),
                @Fvn030ItemId5 char(5);

        DECLARE Fvn030ItemCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT ip.SiteCode,ip.PartNo
            FROM IFS.FVN_InventoryPart AS ip
            LEFT JOIN MES.FVN_ItemIdentity AS ii
              ON ii.SiteCode=ip.SiteCode AND ii.PartNo=ip.PartNo
            WHERE ii.ItemId5 IS NULL
            ORDER BY ip.SiteCode,ip.PartNo;

        OPEN Fvn030ItemCursor;
        FETCH NEXT FROM Fvn030ItemCursor INTO @Fvn030Site,@Fvn030Part;
        WHILE @@FETCH_STATUS=0
        BEGIN
            EXEC MES.usp_FVN_AllocateItemId5
                @SiteCode=@Fvn030Site,
                @PartNo=@Fvn030Part,
                @ItemId5=@Fvn030ItemId5 OUTPUT;
            FETCH NEXT FROM Fvn030ItemCursor INTO @Fvn030Site,@Fvn030Part;
        END;
        CLOSE Fvn030ItemCursor;
        DEALLOCATE Fvn030ItemCursor;

        ';
        SET @Definition=STUFF(@Definition,@Start,@Finish-@Start,@Replacement);
    END
    ELSE IF CHARINDEX(N'MES.usp_FVN_AllocateItemId5',@Definition)=0
        THROW 53006, 'IFS apply contains neither the legacy allocator marker nor the canonical allocator; manual review is required.', 1;

    /* OBJECT_DEFINITION reports CREATE PROCEDURE even for an altered procedure. */
    SET @Definition=REPLACE(@Definition,N'CREATE PROCEDURE',N'ALTER PROCEDURE');
    IF CHARINDEX(N'CREATE PROCEDURE',@Definition)>0
        THROW 53007, 'Cannot convert IFS apply definition to ALTER PROCEDURE.', 1;

    EXEC sys.sp_executesql @Definition;

    INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
    VALUES('030',N'Canonical MES item identity allocator in IFS apply');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Post-deployment checks: the IFS apply procedure must call the canonical allocator. */
SELECT MigrationVersion,MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion IN ('028','029','030')
ORDER BY MigrationVersion;

SELECT
    COUNT(*) AS ItemIdentityRows,
    COUNT(ItemId5) AS ItemTokensAssigned,
    COUNT(DISTINCT ItemId5) AS DistinctItemTokens
FROM MES.FVN_ItemIdentity;

SELECT
    COUNT(*) AS SupplierIdentityRows,
    COUNT(SupplierId5) AS SupplierTokensAssigned,
    COUNT(DISTINCT SupplierId5) AS DistinctSupplierTokens
FROM MES.FVN_SupplierIdentity;

SELECT
    CASE WHEN CHARINDEX(N'MES.usp_FVN_AllocateItemId5',
                        OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch',N'P')))>0
         THEN N'OK - canonical allocator is used'
         ELSE N'ERROR - canonical allocator not found'
    END AS IFSApplyIdentityCheck;
GO
