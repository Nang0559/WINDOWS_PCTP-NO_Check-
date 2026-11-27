/*
 Migration 064: load the packing spec from Bravo B20ItemQuyCach.

 MES.FVN_ComponentPackSpec (migration 061) is the MES counterpart of Bravo dbo.B20ItemQuyCach:

     B20ItemQuyCach.CustomerCode  ->  SupplierCode   (NULL / empty = every supplier)
     B20ItemQuyCach.ItemCode      ->  PartNo
     B20ItemQuyCach.Quantity      ->  PackQty        (quantity of one carton / small label; "MinCloseQty" on the screen)
     B20ItemQuyCach.MaxCloseQty   ->  MaxCloseQty    (quantity of one pallet)
     B20ItemQuyCach.IsActive      ->  IsActive
     (Id, ParentId, IsGroup, BuiltinOrder, CreatedBy/At, ModifiedBy/At, timestamp are Bravo tree / audit columns and are not copied;
      group rows IsGroup = 1 are skipped.)

 MES.usp_FVN_ComponentPackSpec_ImportBravo reads the Bravo table of the SAME SQL Server instance (database name is a
 parameter, default B7R2_FCC) and upserts the rows for one Site. @DryRun = 1 (default) changes nothing and only reports.
 Rows that cannot be loaded are listed with the reason instead of failing the whole load:
     PACK_QTY_INVALID   Quantity empty / <= 0
     MAX_LT_PACK        MaxCloseQty smaller than Quantity (a pallet cannot be smaller than one carton)
     PART_NOT_IN_IFS    active row whose ItemCode is not in IFS.FVN_InventoryPart of the Site
 Several Bravo rows for the same CustomerCode + ItemCode: the active, most recently modified one wins.
 Result sets: 1) one summary row, 2) the skipped rows.
 Apply after migration 063. SQL Server 2016 SP1+ (CREATE OR ALTER).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54800, 'Migration 064 may run only in FVN_PCTP.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '063')
    THROW 54801, 'Migration 063 must be applied before 064.', 1;
IF OBJECT_ID(N'MES.FVN_ComponentPackSpec', N'U') IS NULL
    THROW 54802, 'MES.FVN_ComponentPackSpec is missing: apply migration 061 first.', 1;
GO

CREATE OR ALTER PROCEDURE MES.usp_FVN_ComponentPackSpec_ImportBravo
    @SiteCode      nvarchar(30),
    @ActorCode     nvarchar(100),
    @BravoDatabase sysname = N'B7R2_FCC',
    @DryRun        bit = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    SET @SiteCode = UPPER(LTRIM(RTRIM(ISNULL(@SiteCode, N''))));
    IF @SiteCode = N'' THROW 54810, 'SiteCode is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ActorCode)), N'') IS NULL THROW 54811, 'ActorCode is required.', 1;
    IF @BravoDatabase IS NULL OR NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @BravoDatabase)
        THROW 54812, 'Bravo database was not found on this SQL Server instance.', 1;
    SET @DryRun = ISNULL(@DryRun, 1);

    CREATE TABLE #Src
    (
        SupplierCode nvarchar(60)  NOT NULL,
        PartNo       nvarchar(100) NOT NULL,
        PackQty      decimal(18,3) NULL,
        MaxCloseQty  decimal(18,3) NULL,
        IsActive     bit           NOT NULL,
        Rn           int           NOT NULL
    );

    DECLARE @sql nvarchar(max) = N'
    ;WITH q AS
    (
        SELECT LTRIM(RTRIM(CONVERT(nvarchar(60),  ISNULL(CustomerCode, N'''')))) AS SupplierCode,
               LTRIM(RTRIM(CONVERT(nvarchar(100), ItemCode)))                    AS PartNo,
               TRY_CONVERT(decimal(18,3), Quantity)                              AS PackQty,
               TRY_CONVERT(decimal(18,3), NULLIF(MaxCloseQty, 0))                AS MaxCloseQty,
               CONVERT(bit, ISNULL(IsActive, 1))                                 AS IsActive,
               ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(CONVERT(nvarchar(60), ISNULL(CustomerCode, N'''')))),
                                               LTRIM(RTRIM(CONVERT(nvarchar(100), ItemCode)))
                                  ORDER BY CONVERT(int, ISNULL(IsActive, 1)) DESC,
                                           ISNULL(ModifiedAt, CreatedAt) DESC, Id DESC) AS Rn
          FROM ' + QUOTENAME(@BravoDatabase) + N'.dbo.B20ItemQuyCach
         WHERE ISNULL(IsGroup, 0) = 0 AND ItemCode IS NOT NULL AND LTRIM(RTRIM(CONVERT(nvarchar(100), ItemCode))) <> N''''
    )
    INSERT #Src (SupplierCode, PartNo, PackQty, MaxCloseQty, IsActive, Rn)
    SELECT SupplierCode, PartNo, PackQty, MaxCloseQty, IsActive, Rn FROM q WHERE Rn = 1;';
    EXEC sys.sp_executesql @sql;

    CREATE TABLE #Skipped
    (
        SupplierCode nvarchar(60), PartNo nvarchar(100), PackQty decimal(18,3), MaxCloseQty decimal(18,3), Reason varchar(30)
    );
    CREATE TABLE #Valid
    (
        SupplierCode nvarchar(60) NOT NULL, PartNo nvarchar(100) NOT NULL,
        PackQty decimal(18,3) NOT NULL, MaxCloseQty decimal(18,3) NULL, IsActive bit NOT NULL,
        PRIMARY KEY (SupplierCode, PartNo)
    );

    INSERT #Skipped SELECT SupplierCode, PartNo, PackQty, MaxCloseQty, 'PACK_QTY_INVALID'
      FROM #Src WHERE PackQty IS NULL OR PackQty <= 0;
    INSERT #Skipped SELECT SupplierCode, PartNo, PackQty, MaxCloseQty, 'MAX_LT_PACK'
      FROM #Src WHERE PackQty > 0 AND MaxCloseQty IS NOT NULL AND MaxCloseQty < PackQty;
    INSERT #Skipped SELECT s.SupplierCode, s.PartNo, s.PackQty, s.MaxCloseQty, 'PART_NOT_IN_IFS'
      FROM #Src s
     WHERE s.PackQty > 0 AND (s.MaxCloseQty IS NULL OR s.MaxCloseQty >= s.PackQty) AND s.IsActive = 1
       AND NOT EXISTS (SELECT 1 FROM IFS.FVN_InventoryPart p WHERE p.SiteCode = @SiteCode AND p.PartNo = s.PartNo);

    INSERT #Valid (SupplierCode, PartNo, PackQty, MaxCloseQty, IsActive)
    SELECT s.SupplierCode, s.PartNo, s.PackQty, s.MaxCloseQty, s.IsActive
      FROM #Src s
     WHERE s.PackQty > 0 AND (s.MaxCloseQty IS NULL OR s.MaxCloseQty >= s.PackQty)
       AND NOT EXISTS (SELECT 1 FROM #Skipped k WHERE k.SupplierCode = s.SupplierCode AND k.PartNo = s.PartNo);

    DECLARE @Total int = (SELECT COUNT(*) FROM #Src),
            @WillInsert int, @WillUpdate int, @Unchanged int, @Skipped int = (SELECT COUNT(*) FROM #Skipped);
    SELECT @WillInsert = COUNT(*) FROM #Valid v
     WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentPackSpec t WHERE t.SiteCode = @SiteCode AND t.SupplierCode = v.SupplierCode AND t.PartNo = v.PartNo);
    SELECT @WillUpdate = COUNT(*) FROM #Valid v
      JOIN MES.FVN_ComponentPackSpec t ON t.SiteCode = @SiteCode AND t.SupplierCode = v.SupplierCode AND t.PartNo = v.PartNo
     WHERE t.PackQty <> v.PackQty OR ISNULL(t.MaxCloseQty, -1) <> ISNULL(v.MaxCloseQty, -1) OR t.IsActive <> v.IsActive;
    SET @Unchanged = (SELECT COUNT(*) FROM #Valid) - @WillInsert - @WillUpdate;

    IF @DryRun = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            UPDATE t
               SET PackQty = v.PackQty, MaxCloseQty = v.MaxCloseQty, IsActive = v.IsActive,
                   UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @ActorCode
              FROM MES.FVN_ComponentPackSpec t WITH (UPDLOCK, HOLDLOCK)
              JOIN #Valid v ON t.SiteCode = @SiteCode AND t.SupplierCode = v.SupplierCode AND t.PartNo = v.PartNo
             WHERE t.PackQty <> v.PackQty OR ISNULL(t.MaxCloseQty, -1) <> ISNULL(v.MaxCloseQty, -1) OR t.IsActive <> v.IsActive;
            INSERT MES.FVN_ComponentPackSpec (SiteCode, SupplierCode, PartNo, PackQty, MaxCloseQty, IsActive, CreatedBy)
            SELECT @SiteCode, v.SupplierCode, v.PartNo, v.PackQty, v.MaxCloseQty, v.IsActive, @ActorCode
              FROM #Valid v
             WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_ComponentPackSpec t WITH (UPDLOCK, HOLDLOCK)
                                WHERE t.SiteCode = @SiteCode AND t.SupplierCode = v.SupplierCode AND t.PartNo = v.PartNo);
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END;

    SELECT @Total AS TotalRows, @WillInsert AS InsertRows, @WillUpdate AS UpdateRows, @Unchanged AS UnchangedRows,
           @Skipped AS SkippedRows, @DryRun AS DryRun;
    SELECT SupplierCode, PartNo, PackQty, MaxCloseQty, Reason FROM #Skipped ORDER BY Reason, PartNo, SupplierCode;
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '064')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('064', N'Pack spec import from Bravo B20ItemQuyCach');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '064';
GO
