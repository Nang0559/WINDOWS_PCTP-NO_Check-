/*
 Migration 028: authoritative IFS supplier master and canonical component LOT identity.
 Prerequisites: migrations 000..027.
 SQL Server 2016 compatible. IFS remains read-only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52800, 'Migration 028 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52801, 'MES.FVN_SchemaMigration is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='027')
    THROW 52802, 'Migration 027 must be applied before migration 028.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='028')
    RETURN;
IF OBJECT_ID(N'IFS.FVN_SyncBatch',N'U') IS NULL
   OR OBJECT_ID(N'IFS.FVN_SyncStageRow',N'U') IS NULL
   OR OBJECT_ID(N'IFS.usp_ApplySyncBatch',N'P') IS NULL
    THROW 52803, 'IFS synchronization objects are missing.', 1;
IF OBJECT_ID(N'IFS.FVN_Supplier',N'U') IS NULL
BEGIN
    CREATE TABLE IFS.FVN_Supplier
    (
        SupplierCode nvarchar(60) NOT NULL,
        SupplierName nvarchar(250) NOT NULL,
        SourceRowKey nvarchar(200) NULL,
        SourceObjVersion nvarchar(56) NULL,
        SourceHash varbinary(32) NULL,
        SourcePayloadJson nvarchar(max) NULL,
        LastSeenBatchId bigint NULL,
        SyncedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_IFS_FVN_Supplier_Synced DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_IFS_FVN_Supplier PRIMARY KEY(SupplierCode)
    );
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'IFS.FVN_SyncBatch',N'SupplierRows') IS NULL
        ALTER TABLE IFS.FVN_SyncBatch ADD SupplierRows int NOT NULL
            CONSTRAINT DF_IFS_FVN_SyncBatch_SupplierRows DEFAULT(0);

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'IFS.FVN_SyncStageRow')
          AND name=N'CK_IFS_Stage_Entity'
    )
        ALTER TABLE IFS.FVN_SyncStageRow DROP CONSTRAINT CK_IFS_Stage_Entity;

    ALTER TABLE IFS.FVN_SyncStageRow WITH CHECK
        ADD CONSTRAINT CK_IFS_Stage_Entity CHECK
        (EntityName IN
        ('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE','BOM_COMPONENT','WAREHOUSE','SUPPLIER'));

    DECLARE @Definition nvarchar(max), @At int, @Merge nvarchar(max);
    SET @Definition=OBJECT_DEFINITION(OBJECT_ID(N'IFS.usp_ApplySyncBatch',N'P'));
    IF @Definition IS NULL
        THROW 52804, 'Cannot read IFS.usp_ApplySyncBatch.', 1;

    IF CHARINDEX(N'MERGE IFS.FVN_Supplier AS T',@Definition)=0
    BEGIN
        SET @At=CHARINDEX(N'UPDATE IFS.SyncBatch',
            @Definition,CHARINDEX(N'MERGE MES.FVN_IfsWarehouse',@Definition));
        IF @At=0
            THROW 52805, 'Cannot locate final batch update in IFS.usp_ApplySyncBatch.', 1;

        SET @Merge=N'
        MERGE IFS.FVN_Supplier AS T
        USING
        (
            SELECT s.PartNo AS SupplierCode,
                   JSON_VALUE(s.PayloadJson,''$.NAME'') AS SupplierName,
                   s.SourceRowKey,s.SourceObjVersion,s.SourceHash,s.PayloadJson
            FROM IFS.SyncStageRow AS s
            WHERE s.SyncBatchId=@SyncBatchId AND s.EntityName=''SUPPLIER''
        ) AS S
          ON T.SupplierCode=S.SupplierCode
        WHEN MATCHED THEN UPDATE SET
            SupplierName=COALESCE(NULLIF(S.SupplierName,N''''),T.SupplierName),
            SourceRowKey=S.SourceRowKey,SourceObjVersion=S.SourceObjVersion,
            SourceHash=S.SourceHash,SourcePayloadJson=S.PayloadJson,
            LastSeenBatchId=@SyncBatchId,SyncedAtUtc=SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
            (SupplierCode,SupplierName,SourceRowKey,SourceObjVersion,SourceHash,
             SourcePayloadJson,LastSeenBatchId,SyncedAtUtc)
        VALUES
            (S.SupplierCode,COALESCE(NULLIF(S.SupplierName,N''''),S.SupplierCode),
             S.SourceRowKey,S.SourceObjVersion,S.SourceHash,S.PayloadJson,
             @SyncBatchId,SYSUTCDATETIME())
        OUTPUT ''IFS_SUPPLIER'',$action INTO @Changes;

        /* Allocate one stable five-character MES item identity for every synchronized IFS part. */
        DECLARE @ItemCodeLock int, @ItemCodeBase int, @PendingItemCount int;
        EXEC @ItemCodeLock=sys.sp_getapplock
            @Resource=N''FVN_PartIdentity_ItemId5_Allocator'',
            @LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=15000;
        IF @ItemCodeLock<0
            THROW 52807, ''Cannot lock the five-character item-code allocator during IFS apply.'', 1;

        SELECT @ItemCodeBase=ISNULL(MAX(TRY_CONVERT(int,ItemId5)),0)
        FROM dbo.FVN_PartIdentity WITH(UPDLOCK,HOLDLOCK)
        WHERE ItemId5 IS NOT NULL;

        DECLARE @PendingItemCodes TABLE
        (
            RowNo int NOT NULL PRIMARY KEY,
            SiteCode nvarchar(30) NOT NULL,
            PartNo nvarchar(100) NOT NULL
        );

        INSERT @PendingItemCodes(RowNo,SiteCode,PartNo)
        SELECT ROW_NUMBER() OVER(ORDER BY ip.SiteCode,ip.PartNo),ip.SiteCode,ip.PartNo
        FROM IFS.FVN_InventoryPart ip
        LEFT JOIN dbo.FVN_PartIdentity pi
          ON pi.SiteCode=ip.SiteCode AND pi.PartNo=ip.PartNo
        WHERE pi.ItemId5 IS NULL;

        SELECT @PendingItemCount=COUNT(*) FROM @PendingItemCodes;
        IF @ItemCodeBase+@PendingItemCount>99999
            THROW 52808, ''Five-character item-code space (00001..99999) is exhausted.'', 1;

        UPDATE pi
           SET ItemId5=RIGHT(''00000''+CONVERT(varchar(5),@ItemCodeBase+p.RowNo),5)
        FROM dbo.FVN_PartIdentity pi
        INNER JOIN @PendingItemCodes p
          ON p.SiteCode=pi.SiteCode AND p.PartNo=pi.PartNo;

        INSERT dbo.FVN_PartIdentity(SiteCode,PartNo,ItemId5)
        SELECT p.SiteCode,p.PartNo,
               RIGHT(''00000''+CONVERT(varchar(5),@ItemCodeBase+p.RowNo),5)
        FROM @PendingItemCodes p
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.FVN_PartIdentity pi
            WHERE pi.SiteCode=p.SiteCode AND pi.PartNo=p.PartNo
        );

        ';
        SET @Definition=STUFF(@Definition,@At,0,@Merge);
    END;

    SET @Definition=REPLACE(@Definition,N'CREATE PROCEDURE',N'ALTER PROCEDURE');
    IF CHARINDEX(N'CREATE PROCEDURE',@Definition)>0
        THROW 52806, 'Cannot convert IFS.usp_ApplySyncBatch to ALTER PROCEDURE.', 1;
    EXEC sys.sp_executesql @Definition;

    /* A five-character canonical item identity replaces the ambiguous Bravo name Code0/Id_Item.
       Existing QR identity values are preserved; new values are allocated once and never recycled. */
    IF COL_LENGTH(N'dbo.FVN_PartIdentity',N'ItemId5') IS NULL
        ALTER TABLE dbo.FVN_PartIdentity ADD ItemId5 char(5) NULL;

    UPDATE dbo.FVN_PartIdentity
       SET ItemId5=LegacyQrItemCode
     WHERE ItemId5 IS NULL AND LegacyQrItemCode IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.FVN_PartIdentity')
                     AND name=N'UX_FVN_PartIdentity_ItemId5')
        CREATE UNIQUE INDEX UX_FVN_PartIdentity_ItemId5
            ON dbo.FVN_PartIdentity(ItemId5) WHERE ItemId5 IS NOT NULL;

    EXEC(N'
    CREATE PROCEDURE dbo.usp_FVN_PartIdentity_AllocateItemId5
        @SiteCode nvarchar(30),
        @PartNo nvarchar(100),
        @ItemId5 char(5) OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        SET @ItemId5=NULL;

        IF NOT EXISTS (SELECT 1 FROM IFS.FVN_InventoryPart
                       WHERE SiteCode=@SiteCode AND PartNo=@PartNo)
            THROW 52810, ''Part does not exist in the synchronized IFS inventory-part master.'', 1;

        BEGIN TRY
            BEGIN TRANSACTION;
            DECLARE @LockResult int, @Next int;
            EXEC @LockResult=sys.sp_getapplock
                @Resource=N''FVN_PartIdentity_ItemId5_Allocator'',
                @LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=15000;
            IF @LockResult<0 THROW 52811, ''Cannot lock the five-character item-code allocator.'', 1;

            SELECT @ItemId5=ItemId5
            FROM dbo.FVN_PartIdentity WITH(UPDLOCK,HOLDLOCK)
            WHERE SiteCode=@SiteCode AND PartNo=@PartNo;

            IF @ItemId5 IS NULL
            BEGIN
                SELECT @Next=ISNULL(MAX(TRY_CONVERT(int,ItemId5)),0)+1
                FROM dbo.FVN_PartIdentity WITH(UPDLOCK,HOLDLOCK)
                WHERE ItemId5 IS NOT NULL;
                IF @Next>99999 THROW 52812, ''Five-character item-code space (00001..99999) is exhausted.'', 1;
                SET @ItemId5=RIGHT(''00000''+CONVERT(varchar(5),@Next),5);

                IF EXISTS(SELECT 1 FROM dbo.FVN_PartIdentity
                          WHERE SiteCode=@SiteCode AND PartNo=@PartNo)
                    UPDATE dbo.FVN_PartIdentity SET ItemId5=@ItemId5
                    WHERE SiteCode=@SiteCode AND PartNo=@PartNo;
                ELSE
                    INSERT dbo.FVN_PartIdentity(SiteCode,PartNo,ItemId5)
                    VALUES(@SiteCode,@PartNo,@ItemId5);
            END;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END;
    ');

    /* Production line/department codes are PCTP operational masters, not IFS supplier/part data.
       Their lengths are part of the 20-character finished LOT contract. */
    IF OBJECT_ID(N'dbo.FVN_ProductionLine',N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_ProductionLine
        (
            SiteCode nvarchar(30) NOT NULL,
            LineCode varchar(3) NOT NULL,
            LineName nvarchar(120) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_ProductionLine_Active DEFAULT(1),
            CONSTRAINT PK_FVN_ProductionLine PRIMARY KEY(SiteCode,LineCode),
            CONSTRAINT CK_FVN_ProductionLine_Code CHECK(LEN(LineCode)=3)
        );
    END;

    IF OBJECT_ID(N'dbo.FVN_ProductionDepartment',N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_ProductionDepartment
        (
            SiteCode nvarchar(30) NOT NULL,
            DepartmentCode varchar(4) NOT NULL,
            DepartmentName nvarchar(120) NOT NULL,
            LineCode varchar(3) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_ProductionDepartment_Active DEFAULT(1),
            CONSTRAINT PK_FVN_ProductionDepartment PRIMARY KEY(SiteCode,DepartmentCode),
            CONSTRAINT FK_FVN_ProductionDepartment_Line FOREIGN KEY(SiteCode,LineCode)
                REFERENCES dbo.FVN_ProductionLine(SiteCode,LineCode),
            CONSTRAINT CK_FVN_ProductionDepartment_Code CHECK(LEN(DepartmentCode)=4)
        );
    END;

    /* Keep legacy LOT rows readable. The untrusted FK enforces every new/changed
       SupplierCode immediately while allowing pre-existing Bravo codes to be reconciled. */
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_FVN_MaterialLot_IFSSupplier')
        ALTER TABLE MES.FVN_MaterialLot WITH NOCHECK
            ADD CONSTRAINT FK_FVN_MaterialLot_IFSSupplier
            FOREIGN KEY(SupplierCode) REFERENCES IFS.FVN_Supplier(SupplierCode);

    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_BuildComponentLotNo
        @RequestDate date,
        @SiteCode nvarchar(30),
        @PartNo nvarchar(100),
        @SupplierCode nvarchar(60),
        @DocNo nvarchar(40),
        @LotNo nvarchar(120) OUTPUT,
        @ItemId5 char(5) OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET @LotNo=NULL;
        SET @ItemId5=NULL;

        IF @RequestDate IS NULL THROW 52820, ''RequestDate is required.'', 1;
        IF NULLIF(LTRIM(RTRIM(@SupplierCode)),N'''') IS NULL
            THROW 52821, ''SupplierCode is required for a component LOT.'', 1;
        IF NULLIF(LTRIM(RTRIM(@DocNo)),N'''') IS NULL
            THROW 52822, ''Receipt document number is required for a component LOT.'', 1;
        IF NOT EXISTS(SELECT 1 FROM IFS.FVN_Supplier WHERE SupplierCode=@SupplierCode)
            THROW 52823, ''SupplierCode does not exist in the synchronized IFS supplier master.'', 1;

        EXEC dbo.usp_FVN_PartIdentity_AllocateItemId5
            @SiteCode=@SiteCode,@PartNo=@PartNo,@ItemId5=@ItemId5 OUTPUT;

        SET @LotNo=CONVERT(char(6),@RequestDate,12)+
                   LTRIM(RTRIM(@SupplierCode))+
                   @ItemId5+
                   LTRIM(RTRIM(@DocNo));
        IF LEN(@LotNo)>120
            THROW 52824, ''Generated component LOT exceeds the 120-character storage limit.'', 1;
    END;
    ');

    EXEC(N'
    CREATE PROCEDURE MES.usp_FVN_BuildProductionLotNo
        @RequestDate date,
        @SiteCode nvarchar(30),
        @PartNo nvarchar(100),
        @ShiftCode varchar(2),
        @LineCode varchar(3),
        @DepartmentCode varchar(4),
        @LotNo char(20) OUTPUT,
        @ItemId5 char(5) OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET @LotNo=NULL;
        SET @ItemId5=NULL;

        IF @RequestDate IS NULL THROW 52830, ''Production date is required.'', 1;
        IF LEN(LTRIM(RTRIM(ISNULL(@ShiftCode,''''))))<>2
            THROW 52831, ''ShiftCode must contain exactly two characters (shift + gear)'', 1;
        IF LEN(LTRIM(RTRIM(ISNULL(@LineCode,''''))))<>3
            THROW 52832, ''LineCode must contain exactly three characters.'', 1;
        IF LEN(LTRIM(RTRIM(ISNULL(@DepartmentCode,''''))))<>4
            THROW 52833, ''DepartmentCode must contain exactly four characters.'', 1;

        IF NOT EXISTS(SELECT 1 FROM dbo.FVN_ProductionLine
                      WHERE SiteCode=@SiteCode AND LineCode=@LineCode AND IsActive=1)
            THROW 52834, ''Production line is not configured or inactive for this site.'', 1;
        IF NOT EXISTS(SELECT 1 FROM dbo.FVN_ProductionDepartment
                      WHERE SiteCode=@SiteCode AND DepartmentCode=@DepartmentCode
                        AND LineCode=@LineCode AND IsActive=1)
            THROW 52835, ''Department is not configured or inactive for this site.'', 1;

        EXEC dbo.usp_FVN_PartIdentity_AllocateItemId5
            @SiteCode=@SiteCode,@PartNo=@PartNo,@ItemId5=@ItemId5 OUTPUT;

        SET @LotNo=CONVERT(char(6),@RequestDate,12)+
                   @ItemId5+
                   @ShiftCode+
                   @LineCode+
                   @DepartmentCode;
        IF LEN(@LotNo)<>20
            THROW 52836, ''Generated production LOT is not exactly 20 characters.'', 1;
    END;
    ');

    INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
    VALUES('028',N'IFS supplier master and canonical component LOT identity');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion,MigrationName
FROM MES.FVN_SchemaMigration WHERE MigrationVersion='028';
SELECT TOP (20) SupplierCode,SupplierName,LastSeenBatchId,SyncedAtUtc
FROM IFS.FVN_Supplier ORDER BY SupplierCode;
GO
