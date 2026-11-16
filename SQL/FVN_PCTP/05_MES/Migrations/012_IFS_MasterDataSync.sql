/*
 012_IFS_MasterDataSync.sql
 One-way IFSAPP View -> FVN_PCTP. SQL Server 2016 compatible.
 Prerequisites: migrations 000 and 001 have been deployed.
 Does not connect to Oracle and does not write to IFS.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'MES.SchemaMigration',N'U') IS NULL
    THROW 51000,'Run MES migration 000 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='012') RETURN;
IF OBJECT_ID(N'MES.IfsInventoryPart',N'U') IS NULL
    THROW 51001,'Run MES migration 001 first.',1;

BEGIN TRANSACTION;
IF SCHEMA_ID(N'IFS') IS NULL EXEC(N'CREATE SCHEMA IFS AUTHORIZATION dbo;');

IF OBJECT_ID(N'IFS.SyncBatch',N'U') IS NULL
CREATE TABLE IFS.SyncBatch(
    SyncBatchId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IFS_SyncBatch PRIMARY KEY,
    SyncMode varchar(12) NOT NULL,
    Status varchar(16) NOT NULL,
    StartedAtUtc datetime2(3) NOT NULL,
    CompletedAtUtc datetime2(3) NULL,
    InitiatedBy nvarchar(128) NULL,
    PartCatalogRows int NOT NULL CONSTRAINT DF_IFS_Batch_PC DEFAULT(0),
    InventoryPartRows int NOT NULL CONSTRAINT DF_IFS_Batch_IP DEFAULT(0),
    BomHeaderRows int NOT NULL CONSTRAINT DF_IFS_Batch_BH DEFAULT(0),
    BomAlternativeRows int NOT NULL CONSTRAINT DF_IFS_Batch_BA DEFAULT(0),
    BomComponentRows int NOT NULL CONSTRAINT DF_IFS_Batch_BC DEFAULT(0),
    InsertCount int NOT NULL CONSTRAINT DF_IFS_Batch_Insert DEFAULT(0),
    UpdateCount int NOT NULL CONSTRAINT DF_IFS_Batch_Update DEFAULT(0),
    ErrorSummary nvarchar(2000) NULL,
    CONSTRAINT CK_IFS_Batch_Mode CHECK(SyncMode IN('FULL','INCREMENTAL')),
    CONSTRAINT CK_IFS_Batch_Status CHECK(Status IN('RUNNING','STAGED','APPLIED','FAILED'))
);

IF OBJECT_ID(N'IFS.SyncError',N'U') IS NULL
CREATE TABLE IFS.SyncError(
    SyncErrorId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IFS_SyncError PRIMARY KEY,
    SyncBatchId bigint NOT NULL,
    EntityName varchar(30) NULL,
    SourceBusinessKey nvarchar(1000) NULL,
    ErrorCode varchar(40) NOT NULL,
    ErrorMessage nvarchar(2000) NOT NULL,
    ErrorDetails nvarchar(max) NULL,
    LoggedAtUtc datetime2(3) NOT NULL,
    CONSTRAINT FK_IFS_SyncError_Batch FOREIGN KEY(SyncBatchId) REFERENCES IFS.SyncBatch(SyncBatchId)
);

IF OBJECT_ID(N'IFS.SyncStageRow',N'U') IS NULL
CREATE TABLE IFS.SyncStageRow(
    SyncStageRowId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IFS_SyncStageRow PRIMARY KEY,
    SyncBatchId bigint NOT NULL,
    EntityName varchar(30) NOT NULL,
    SiteCode nvarchar(30) NULL,
    PartNo nvarchar(100) NOT NULL,
    EngChgLevel nvarchar(24) NULL,
    BomTypeDb nvarchar(80) NULL,
    AlternativeNo nvarchar(80) NULL,
    LineItemNo decimal(18,0) NULL,
    SourceRowKey nvarchar(200) NULL,
    SourceObjVersion nvarchar(30) NULL,
    PayloadJson nvarchar(max) NOT NULL,
    SourceHash varbinary(32) NOT NULL,
    StagedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_IFS_Stage_At DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT FK_IFS_Stage_Batch FOREIGN KEY(SyncBatchId) REFERENCES IFS.SyncBatch(SyncBatchId),
    CONSTRAINT CK_IFS_Stage_Entity CHECK(EntityName IN('PART_CATALOG','INVENTORY_PART','BOM_HEADER','BOM_ALTERNATIVE','BOM_COMPONENT')),
    CONSTRAINT CK_IFS_Stage_Json CHECK(ISJSON(PayloadJson)=1)
);
CREATE INDEX IX_IFS_Stage_BatchEntity ON IFS.SyncStageRow(SyncBatchId,EntityName);

IF OBJECT_ID(N'IFS.PartCatalog',N'U') IS NULL
CREATE TABLE IFS.PartCatalog(
    PartNo nvarchar(100) NOT NULL CONSTRAINT PK_IFS_PartCatalog PRIMARY KEY,
    Description nvarchar(800) NOT NULL,
    InfoText nvarchar(4000) NULL,
    UnitCode nvarchar(120) NOT NULL,
    PartMainGroup nvarchar(80) NULL,
    LotTrackingCodeDb nvarchar(120) NULL,
    SerialRuleDb nvarchar(80) NULL,
    SerialTrackingCodeDb nvarchar(120) NULL,
    EngSerialTrackingCodeDb nvarchar(80) NULL,
    ConfigurableDb nvarchar(80) NULL,
    ConditionCodeUsageDb nvarchar(80) NULL,
    SubLotRuleDb nvarchar(80) NULL,
    LotQuantityRuleDb nvarchar(80) NULL,
    MultilevelTrackingDb nvarchar(80) NULL,
    ComponentLotRuleDb nvarchar(80) NULL,
    WeightNet decimal(19,6) NULL,
    WeightUom nvarchar(120) NULL,
    VolumeNet decimal(19,6) NULL,
    VolumeUom nvarchar(120) NULL,
    SourceRowKey nvarchar(200) NULL,
    SourceObjVersion nvarchar(30) NULL,
    SourceHash varbinary(32) NOT NULL,
    SourcePayloadJson nvarchar(max) NOT NULL,
    LastSeenBatchId bigint NOT NULL,
    SyncedAtUtc datetime2(3) NOT NULL,
    CONSTRAINT FK_IFS_PartCatalog_Batch FOREIGN KEY(LastSeenBatchId) REFERENCES IFS.SyncBatch(SyncBatchId)
);

IF OBJECT_ID(N'IFS.InventoryPart',N'U') IS NULL
CREATE TABLE IFS.InventoryPart(
    SiteCode nvarchar(30) NOT NULL,
    PartNo nvarchar(100) NOT NULL,
    Description nvarchar(800) NOT NULL,
    UnitMeas nvarchar(40) NOT NULL,
    PartStatus nvarchar(4) NOT NULL,
    AccountingGroup nvarchar(20) NULL,
    AssetClass nvarchar(8) NULL,
    PartProductCode nvarchar(20) NULL,
    PartProductFamily nvarchar(20) NULL,
    PlannerBuyer nvarchar(80) NULL,
    SupplyCodeDb nvarchar(12) NULL,
    TypeCodeDb nvarchar(8) NULL,
    StockManagementDb nvarchar(200) NULL,
    AvailabilityActivityStatusDb nvarchar(36) NULL,
    LifecycleStageDb nvarchar(80) NULL,
    InventoryValuationMethodDb nvarchar(200) NULL,
    NegativeOnHandDb nvarchar(200) NULL,
    CreateDate datetime2(0) NULL,
    LastActivityDate datetime2(0) NULL,
    DeclineDate datetime2(0) NULL,
    ExpiredDate datetime2(0) NULL,
    SourceRowKey nvarchar(200) NULL,
    SourceObjVersion nvarchar(30) NULL,
    SourceHash varbinary(32) NOT NULL,
    SourcePayloadJson nvarchar(max) NOT NULL,
    LastSeenBatchId bigint NOT NULL,
    SyncedAtUtc datetime2(3) NOT NULL,
    CONSTRAINT PK_IFS_InventoryPart PRIMARY KEY(SiteCode,PartNo),
    CONSTRAINT FK_IFS_InventoryPart_Batch FOREIGN KEY(LastSeenBatchId) REFERENCES IFS.SyncBatch(SyncBatchId)
);

IF OBJECT_ID(N'IFS.BomHeader',N'U') IS NULL
CREATE TABLE IFS.BomHeader(
    SiteCode nvarchar(20) NOT NULL,
    PartNo nvarchar(100) NOT NULL,
    EngChgLevel nvarchar(24) NOT NULL,
    BomTypeDb nvarchar(80) NOT NULL,
    EffectiveInDate datetime2(0) NOT NULL,
    EffectiveOutDate datetime2(0) NULL,
    EngRevision nvarchar(24) NULL,
    EngChgOrder nvarchar(40) NULL,
    SourceRowKey nvarchar(200) NULL,
    SourceObjVersion nvarchar(30) NULL,
    SourceHash varbinary(32) NOT NULL,
    SourcePayloadJson nvarchar(max) NOT NULL,
    LastSeenBatchId bigint NOT NULL,
    SyncedAtUtc datetime2(3) NOT NULL,
    CONSTRAINT PK_IFS_BomHeader PRIMARY KEY(SiteCode,PartNo,EngChgLevel,BomTypeDb),
    CONSTRAINT FK_IFS_BomHeader_Batch FOREIGN KEY(LastSeenBatchId) REFERENCES IFS.SyncBatch(SyncBatchId)
);

IF OBJECT_ID(N'IFS.BomAlternative',N'U') IS NULL
CREATE TABLE IFS.BomAlternative(
    SiteCode nvarchar(20) NOT NULL,
    PartNo nvarchar(100) NOT NULL,
    EngChgLevel nvarchar(24) NOT NULL,
    BomTypeDb nvarchar(80) NOT NULL,
    AlternativeNo nvarchar(80) NOT NULL,
    AlternativeDescription nvarchar(200) NULL,
    SourceRowKey nvarchar(200) NULL,
    SourceObjVersion nvarchar(30) NULL,
    SourceHash varbinary(32) NOT NULL,
    SourcePayloadJson nvarchar(max) NOT NULL,
    LastSeenBatchId bigint NOT NULL,
    SyncedAtUtc datetime2(3) NOT NULL,
    CONSTRAINT PK_IFS_BomAlternative PRIMARY KEY(SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo),
    CONSTRAINT FK_IFS_BomAlternative_Header FOREIGN KEY(SiteCode,PartNo,EngChgLevel,BomTypeDb)
        REFERENCES IFS.BomHeader(SiteCode,PartNo,EngChgLevel,BomTypeDb),
    CONSTRAINT FK_IFS_BomAlternative_Batch FOREIGN KEY(LastSeenBatchId) REFERENCES IFS.SyncBatch(SyncBatchId)
);

IF OBJECT_ID(N'IFS.BomComponent',N'U') IS NULL
CREATE TABLE IFS.BomComponent(
    SiteCode nvarchar(20) NOT NULL,
    PartNo nvarchar(100) NOT NULL,
    EngChgLevel nvarchar(24) NOT NULL,
    BomTypeDb nvarchar(80) NOT NULL,
    AlternativeNo nvarchar(80) NOT NULL,
    LineItemNo decimal(18,0) NOT NULL,
    LineSequence decimal(18,0) NOT NULL,
    ComponentPartNo nvarchar(100) NOT NULL,
    ComponentContract nvarchar(20) NOT NULL,
    QtyPerAssembly decimal(19,6) NOT NULL,
    ComponentScrap decimal(19,6) NULL,
    ShrinkageFactor decimal(19,6) NULL,
    OperationNo decimal(18,0) NULL,
    LeadtimeOffset decimal(19,6) NULL,
    IssueTypeDb nvarchar(100) NULL,
    ConsumptionItem nvarchar(80) NULL,
    PhantomConsume nvarchar(80) NULL,
    ChargedItem nvarchar(80) NULL,
    EffectiveInDate datetime2(0) NOT NULL,
    EffectiveOutDate datetime2(0) NULL,
    DrawPosNo nvarchar(40) NULL,
    QtyKg decimal(19,6) NULL,
    SourceRowKey nvarchar(200) NULL,
    SourceObjVersion nvarchar(30) NULL,
    SourceHash varbinary(32) NOT NULL,
    SourcePayloadJson nvarchar(max) NOT NULL,
    LastSeenBatchId bigint NOT NULL,
    SyncedAtUtc datetime2(3) NOT NULL,
    CONSTRAINT PK_IFS_BomComponent PRIMARY KEY(SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo,LineItemNo),
    CONSTRAINT FK_IFS_BomComponent_Alternative FOREIGN KEY(SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo)
        REFERENCES IFS.BomAlternative(SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo),
    CONSTRAINT FK_IFS_BomComponent_Batch FOREIGN KEY(LastSeenBatchId) REFERENCES IFS.SyncBatch(SyncBatchId)
);
CREATE INDEX IX_IFS_InventoryPart_PartNo ON IFS.InventoryPart(PartNo,SiteCode);
CREATE INDEX IX_IFS_BomHeader_PartNo ON IFS.BomHeader(PartNo,SiteCode);
CREATE INDEX IX_IFS_BomComponent_Component ON IFS.BomComponent(ComponentContract,ComponentPartNo);

-- Widen the legacy MES projection to the source lengths before copying IFS values.
ALTER TABLE MES.IfsInventoryPart ALTER COLUMN Description nvarchar(800) NULL;
ALTER TABLE MES.IfsInventoryPart ALTER COLUMN InventoryUom nvarchar(120) NULL;
ALTER TABLE MES.IfsInventoryPart ALTER COLUMN LotTrackingCode nvarchar(120) NULL;
ALTER TABLE MES.IfsInventoryPart ALTER COLUMN SerialTrackingCode nvarchar(120) NULL;

EXEC(N'
CREATE PROCEDURE IFS.usp_ApplySyncBatch
    @SyncBatchId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Changes table(EntityName varchar(30), ActionName nvarchar(10));

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @LockResult int;
        EXEC @LockResult=sys.sp_getapplock
            @Resource=''IFS_MASTER_BOM_FULL_SYNC'',
            @LockMode=''Exclusive'',
            @LockOwner=''Transaction'',
            @LockTimeout=0;
        IF @LockResult<0 THROW 51205,''Another IFS synchronization is already applying.'',1;

        IF NOT EXISTS(SELECT 1 FROM IFS.SyncBatch WITH(UPDLOCK,HOLDLOCK)
                      WHERE SyncBatchId=@SyncBatchId AND Status=''STAGED'')
            THROW 51200,''Sync batch must be STAGED before apply.'',1;

        IF EXISTS(
            SELECT 1 FROM IFS.SyncStageRow
            WHERE SyncBatchId=@SyncBatchId
            GROUP BY EntityName,SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo,LineItemNo
            HAVING COUNT(*)>1)
            THROW 51201,''Duplicate source business key found in staging.'',1;

        IF NOT EXISTS(SELECT 1 FROM IFS.SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''PART_CATALOG'')
           OR NOT EXISTS(SELECT 1 FROM IFS.SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''INVENTORY_PART'')
           OR NOT EXISTS(SELECT 1 FROM IFS.SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''BOM_HEADER'')
           OR NOT EXISTS(SELECT 1 FROM IFS.SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''BOM_ALTERNATIVE'')
           OR NOT EXISTS(SELECT 1 FROM IFS.SyncStageRow WHERE SyncBatchId=@SyncBatchId AND EntityName=''BOM_COMPONENT'')
            THROW 51202,''At least one required IFS view returned no rows; batch rejected.'',1;

        IF EXISTS(
            SELECT 1 FROM IFS.SyncStageRow a
            WHERE a.SyncBatchId=@SyncBatchId AND a.EntityName=''BOM_ALTERNATIVE''
              AND NOT EXISTS(
                SELECT 1 FROM IFS.SyncStageRow h
                WHERE h.SyncBatchId=@SyncBatchId AND h.EntityName=''BOM_HEADER''
                  AND h.SiteCode=a.SiteCode AND h.PartNo=a.PartNo
                  AND h.EngChgLevel=a.EngChgLevel AND h.BomTypeDb=a.BomTypeDb))
            THROW 51203,''BOM Alternative without matching Header in the same batch.'',1;

        IF EXISTS(
            SELECT 1 FROM IFS.SyncStageRow c
            WHERE c.SyncBatchId=@SyncBatchId AND c.EntityName=''BOM_COMPONENT''
              AND NOT EXISTS(
                SELECT 1 FROM IFS.SyncStageRow a
                WHERE a.SyncBatchId=@SyncBatchId AND a.EntityName=''BOM_ALTERNATIVE''
                  AND a.SiteCode=c.SiteCode AND a.PartNo=c.PartNo
                  AND a.EngChgLevel=c.EngChgLevel AND a.BomTypeDb=c.BomTypeDb
                  AND a.AlternativeNo=c.AlternativeNo))
            THROW 51204,''BOM Component without matching Alternative in the same batch.'',1;

        MERGE IFS.PartCatalog AS T
        USING (
            SELECT s.PartNo, s.SourceRowKey, s.SourceObjVersion, s.SourceHash, s.PayloadJson,
              JSON_VALUE(s.PayloadJson,''$.DESCRIPTION'') AS Description,
              JSON_VALUE(s.PayloadJson,''$.INFO_TEXT'') AS InfoText,
              JSON_VALUE(s.PayloadJson,''$.UNIT_CODE'') AS UnitCode,
              JSON_VALUE(s.PayloadJson,''$.PART_MAIN_GROUP'') AS PartMainGroup,
              JSON_VALUE(s.PayloadJson,''$.LOT_TRACKING_CODE_DB'') AS LotTrackingCodeDb,
              JSON_VALUE(s.PayloadJson,''$.SERIAL_RULE_DB'') AS SerialRuleDb,
              JSON_VALUE(s.PayloadJson,''$.SERIAL_TRACKING_CODE_DB'') AS SerialTrackingCodeDb,
              JSON_VALUE(s.PayloadJson,''$.ENG_SERIAL_TRACKING_CODE_DB'') AS EngSerialTrackingCodeDb,
              JSON_VALUE(s.PayloadJson,''$.CONFIGURABLE_DB'') AS ConfigurableDb,
              JSON_VALUE(s.PayloadJson,''$.CONDITION_CODE_USAGE_DB'') AS ConditionCodeUsageDb,
              JSON_VALUE(s.PayloadJson,''$.SUB_LOT_RULE_DB'') AS SubLotRuleDb,
              JSON_VALUE(s.PayloadJson,''$.LOT_QUANTITY_RULE_DB'') AS LotQuantityRuleDb,
              JSON_VALUE(s.PayloadJson,''$.MULTILEVEL_TRACKING_DB'') AS MultilevelTrackingDb,
              JSON_VALUE(s.PayloadJson,''$.COMPONENT_LOT_RULE_DB'') AS ComponentLotRuleDb,
              TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.WEIGHT_NET'')) AS WeightNet,
              JSON_VALUE(s.PayloadJson,''$.UOM_FOR_WEIGHT_NET'') AS WeightUom,
              TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.VOLUME_NET'')) AS VolumeNet,
              JSON_VALUE(s.PayloadJson,''$.UOM_FOR_VOLUME_NET'') AS VolumeUom
            FROM IFS.SyncStageRow s
            WHERE s.SyncBatchId=@SyncBatchId AND s.EntityName=''PART_CATALOG''
        ) S ON T.PartNo=S.PartNo
        WHEN MATCHED THEN UPDATE SET
          Description=S.Description,InfoText=S.InfoText,UnitCode=S.UnitCode,PartMainGroup=S.PartMainGroup,
          LotTrackingCodeDb=S.LotTrackingCodeDb,SerialRuleDb=S.SerialRuleDb,
          SerialTrackingCodeDb=S.SerialTrackingCodeDb,EngSerialTrackingCodeDb=S.EngSerialTrackingCodeDb,
          ConfigurableDb=S.ConfigurableDb,ConditionCodeUsageDb=S.ConditionCodeUsageDb,
          SubLotRuleDb=S.SubLotRuleDb,LotQuantityRuleDb=S.LotQuantityRuleDb,
          MultilevelTrackingDb=S.MultilevelTrackingDb,ComponentLotRuleDb=S.ComponentLotRuleDb,
          WeightNet=S.WeightNet,WeightUom=S.WeightUom,VolumeNet=S.VolumeNet,VolumeUom=S.VolumeUom,
          SourceRowKey=S.SourceRowKey,SourceObjVersion=S.SourceObjVersion,SourceHash=S.SourceHash,
          SourcePayloadJson=S.PayloadJson,LastSeenBatchId=@SyncBatchId,SyncedAtUtc=SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
          (PartNo,Description,InfoText,UnitCode,PartMainGroup,LotTrackingCodeDb,SerialRuleDb,
           SerialTrackingCodeDb,EngSerialTrackingCodeDb,ConfigurableDb,ConditionCodeUsageDb,
           SubLotRuleDb,LotQuantityRuleDb,MultilevelTrackingDb,ComponentLotRuleDb,WeightNet,WeightUom,
           VolumeNet,VolumeUom,SourceRowKey,SourceObjVersion,SourceHash,SourcePayloadJson,LastSeenBatchId,SyncedAtUtc)
        VALUES
          (S.PartNo,S.Description,S.InfoText,S.UnitCode,S.PartMainGroup,S.LotTrackingCodeDb,S.SerialRuleDb,
           S.SerialTrackingCodeDb,S.EngSerialTrackingCodeDb,S.ConfigurableDb,S.ConditionCodeUsageDb,
           S.SubLotRuleDb,S.LotQuantityRuleDb,S.MultilevelTrackingDb,S.ComponentLotRuleDb,S.WeightNet,S.WeightUom,
           S.VolumeNet,S.VolumeUom,S.SourceRowKey,S.SourceObjVersion,S.SourceHash,S.PayloadJson,@SyncBatchId,SYSUTCDATETIME())
        OUTPUT ''PART_CATALOG'',$action INTO @Changes;

        MERGE IFS.InventoryPart AS T
        USING (
          SELECT s.SiteCode,s.PartNo,s.SourceRowKey,s.SourceObjVersion,s.SourceHash,s.PayloadJson,
            JSON_VALUE(s.PayloadJson,''$.DESCRIPTION'') Description,
            JSON_VALUE(s.PayloadJson,''$.UNIT_MEAS'') UnitMeas,
            JSON_VALUE(s.PayloadJson,''$.PART_STATUS'') PartStatus,
            JSON_VALUE(s.PayloadJson,''$.ACCOUNTING_GROUP'') AccountingGroup,
            JSON_VALUE(s.PayloadJson,''$.ASSET_CLASS'') AssetClass,
            JSON_VALUE(s.PayloadJson,''$.PART_PRODUCT_CODE'') PartProductCode,
            JSON_VALUE(s.PayloadJson,''$.PART_PRODUCT_FAMILY'') PartProductFamily,
            JSON_VALUE(s.PayloadJson,''$.PLANNER_BUYER'') PlannerBuyer,
            JSON_VALUE(s.PayloadJson,''$.SUPPLY_CODE_DB'') SupplyCodeDb,
            JSON_VALUE(s.PayloadJson,''$.TYPE_CODE_DB'') TypeCodeDb,
            JSON_VALUE(s.PayloadJson,''$.STOCK_MANAGEMENT_DB'') StockManagementDb,
            JSON_VALUE(s.PayloadJson,''$.AVAIL_ACTIVITY_STATUS_DB'') AvailabilityActivityStatusDb,
            JSON_VALUE(s.PayloadJson,''$.LIFECYCLE_STAGE_DB'') LifecycleStageDb,
            JSON_VALUE(s.PayloadJson,''$.INVENTORY_VALUATION_METHOD_DB'') InventoryValuationMethodDb,
            JSON_VALUE(s.PayloadJson,''$.NEGATIVE_ON_HAND_DB'') NegativeOnHandDb,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.CREATE_DATE''),127) CreateDate,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.LAST_ACTIVITY_DATE''),127) LastActivityDate,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.DECLINE_DATE''),127) DeclineDate,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.EXPIRED_DATE''),127) ExpiredDate
          FROM IFS.SyncStageRow s
          WHERE s.SyncBatchId=@SyncBatchId AND s.EntityName=''INVENTORY_PART''
        ) S ON T.SiteCode=S.SiteCode AND T.PartNo=S.PartNo
        WHEN MATCHED THEN UPDATE SET
          Description=S.Description,UnitMeas=S.UnitMeas,PartStatus=S.PartStatus,
          AccountingGroup=S.AccountingGroup,AssetClass=S.AssetClass,PartProductCode=S.PartProductCode,
          PartProductFamily=S.PartProductFamily,PlannerBuyer=S.PlannerBuyer,SupplyCodeDb=S.SupplyCodeDb,
          TypeCodeDb=S.TypeCodeDb,StockManagementDb=S.StockManagementDb,
          AvailabilityActivityStatusDb=S.AvailabilityActivityStatusDb,LifecycleStageDb=S.LifecycleStageDb,
          InventoryValuationMethodDb=S.InventoryValuationMethodDb,NegativeOnHandDb=S.NegativeOnHandDb,
          CreateDate=S.CreateDate,LastActivityDate=S.LastActivityDate,DeclineDate=S.DeclineDate,ExpiredDate=S.ExpiredDate,
          SourceRowKey=S.SourceRowKey,SourceObjVersion=S.SourceObjVersion,SourceHash=S.SourceHash,
          SourcePayloadJson=S.PayloadJson,LastSeenBatchId=@SyncBatchId,SyncedAtUtc=SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
          (SiteCode,PartNo,Description,UnitMeas,PartStatus,AccountingGroup,AssetClass,PartProductCode,
           PartProductFamily,PlannerBuyer,SupplyCodeDb,TypeCodeDb,StockManagementDb,AvailabilityActivityStatusDb,
           LifecycleStageDb,InventoryValuationMethodDb,NegativeOnHandDb,CreateDate,LastActivityDate,DeclineDate,
           ExpiredDate,SourceRowKey,SourceObjVersion,SourceHash,SourcePayloadJson,LastSeenBatchId,SyncedAtUtc)
        VALUES
          (S.SiteCode,S.PartNo,S.Description,S.UnitMeas,S.PartStatus,S.AccountingGroup,S.AssetClass,S.PartProductCode,
           S.PartProductFamily,S.PlannerBuyer,S.SupplyCodeDb,S.TypeCodeDb,S.StockManagementDb,S.AvailabilityActivityStatusDb,
           S.LifecycleStageDb,S.InventoryValuationMethodDb,S.NegativeOnHandDb,S.CreateDate,S.LastActivityDate,S.DeclineDate,
           S.ExpiredDate,S.SourceRowKey,S.SourceObjVersion,S.SourceHash,S.PayloadJson,@SyncBatchId,SYSUTCDATETIME())
        OUTPUT ''INVENTORY_PART'',$action INTO @Changes;

        MERGE IFS.BomHeader AS T
        USING (
          SELECT s.SiteCode,s.PartNo,s.EngChgLevel,s.BomTypeDb,s.SourceRowKey,s.SourceObjVersion,s.SourceHash,s.PayloadJson,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.EFF_PHASE_IN_DATE''),127) EffectiveInDate,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.EFF_PHASE_OUT_DATE''),127) EffectiveOutDate,
            JSON_VALUE(s.PayloadJson,''$.ENG_REVISION'') EngRevision,
            JSON_VALUE(s.PayloadJson,''$.ENG_CHG_ORDER'') EngChgOrder
          FROM IFS.SyncStageRow s WHERE s.SyncBatchId=@SyncBatchId AND s.EntityName=''BOM_HEADER''
        ) S ON T.SiteCode=S.SiteCode AND T.PartNo=S.PartNo AND T.EngChgLevel=S.EngChgLevel AND T.BomTypeDb=S.BomTypeDb
        WHEN MATCHED THEN UPDATE SET EffectiveInDate=S.EffectiveInDate,EffectiveOutDate=S.EffectiveOutDate,
          EngRevision=S.EngRevision,EngChgOrder=S.EngChgOrder,SourceRowKey=S.SourceRowKey,
          SourceObjVersion=S.SourceObjVersion,SourceHash=S.SourceHash,SourcePayloadJson=S.PayloadJson,
          LastSeenBatchId=@SyncBatchId,SyncedAtUtc=SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
          (SiteCode,PartNo,EngChgLevel,BomTypeDb,EffectiveInDate,EffectiveOutDate,EngRevision,EngChgOrder,
           SourceRowKey,SourceObjVersion,SourceHash,SourcePayloadJson,LastSeenBatchId,SyncedAtUtc)
        VALUES
          (S.SiteCode,S.PartNo,S.EngChgLevel,S.BomTypeDb,S.EffectiveInDate,S.EffectiveOutDate,S.EngRevision,S.EngChgOrder,
           S.SourceRowKey,S.SourceObjVersion,S.SourceHash,S.PayloadJson,@SyncBatchId,SYSUTCDATETIME())
        OUTPUT ''BOM_HEADER'',$action INTO @Changes;

        MERGE IFS.BomAlternative AS T
        USING (
          SELECT s.SiteCode,s.PartNo,s.EngChgLevel,s.BomTypeDb,s.AlternativeNo,s.SourceRowKey,s.SourceObjVersion,s.SourceHash,s.PayloadJson,
            JSON_VALUE(s.PayloadJson,''$.ALTERNATIVE_DESCRIPTION'') AlternativeDescription
          FROM IFS.SyncStageRow s WHERE s.SyncBatchId=@SyncBatchId AND s.EntityName=''BOM_ALTERNATIVE''
        ) S ON T.SiteCode=S.SiteCode AND T.PartNo=S.PartNo AND T.EngChgLevel=S.EngChgLevel
           AND T.BomTypeDb=S.BomTypeDb AND T.AlternativeNo=S.AlternativeNo
        WHEN MATCHED THEN UPDATE SET AlternativeDescription=S.AlternativeDescription,SourceRowKey=S.SourceRowKey,
          SourceObjVersion=S.SourceObjVersion,SourceHash=S.SourceHash,SourcePayloadJson=S.PayloadJson,
          LastSeenBatchId=@SyncBatchId,SyncedAtUtc=SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
          (SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo,AlternativeDescription,SourceRowKey,
           SourceObjVersion,SourceHash,SourcePayloadJson,LastSeenBatchId,SyncedAtUtc)
        VALUES
          (S.SiteCode,S.PartNo,S.EngChgLevel,S.BomTypeDb,S.AlternativeNo,S.AlternativeDescription,S.SourceRowKey,
           S.SourceObjVersion,S.SourceHash,S.PayloadJson,@SyncBatchId,SYSUTCDATETIME())
        OUTPUT ''BOM_ALTERNATIVE'',$action INTO @Changes;

        MERGE IFS.BomComponent AS T
        USING (
          SELECT s.SiteCode,s.PartNo,s.EngChgLevel,s.BomTypeDb,s.AlternativeNo,s.LineItemNo,
            s.SourceRowKey,s.SourceObjVersion,s.SourceHash,s.PayloadJson,
            TRY_CONVERT(decimal(18,0),JSON_VALUE(s.PayloadJson,''$.LINE_SEQUENCE'')) LineSequence,
            JSON_VALUE(s.PayloadJson,''$.COMPONENT_PART'') ComponentPartNo,
            JSON_VALUE(s.PayloadJson,''$.COMPONENT_CONTRACT'') ComponentContract,
            TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.QTY_PER_ASSEMBLY'')) QtyPerAssembly,
            TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.COMPONENT_SCRAP'')) ComponentScrap,
            TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.SHRINKAGE_FACTOR'')) ShrinkageFactor,
            TRY_CONVERT(decimal(18,0),JSON_VALUE(s.PayloadJson,''$.OPERATION_NO'')) OperationNo,
            TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.LEADTIME_OFFSET'')) LeadtimeOffset,
            JSON_VALUE(s.PayloadJson,''$.ISSUE_TYPE_DB'') IssueTypeDb,
            JSON_VALUE(s.PayloadJson,''$.CONSUMPTION_ITEM'') ConsumptionItem,
            JSON_VALUE(s.PayloadJson,''$.PHANTOM_CONSUME'') PhantomConsume,
            JSON_VALUE(s.PayloadJson,''$.CHARGED_ITEM'') ChargedItem,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.EFF_PHASE_IN_DATE''),127) EffectiveInDate,
            TRY_CONVERT(datetime2(0),JSON_VALUE(s.PayloadJson,''$.EFF_PHASE_OUT_DATE''),127) EffectiveOutDate,
            JSON_VALUE(s.PayloadJson,''$.DRAW_POS_NO'') DrawPosNo,
            TRY_CONVERT(decimal(19,6),JSON_VALUE(s.PayloadJson,''$.QTY_KG'')) QtyKg
          FROM IFS.SyncStageRow s WHERE s.SyncBatchId=@SyncBatchId AND s.EntityName=''BOM_COMPONENT''
        ) S ON T.SiteCode=S.SiteCode AND T.PartNo=S.PartNo AND T.EngChgLevel=S.EngChgLevel
           AND T.BomTypeDb=S.BomTypeDb AND T.AlternativeNo=S.AlternativeNo AND T.LineItemNo=S.LineItemNo
        WHEN MATCHED THEN UPDATE SET LineSequence=S.LineSequence,ComponentPartNo=S.ComponentPartNo,
          ComponentContract=S.ComponentContract,QtyPerAssembly=S.QtyPerAssembly,ComponentScrap=S.ComponentScrap,
          ShrinkageFactor=S.ShrinkageFactor,OperationNo=S.OperationNo,LeadtimeOffset=S.LeadtimeOffset,
          IssueTypeDb=S.IssueTypeDb,ConsumptionItem=S.ConsumptionItem,PhantomConsume=S.PhantomConsume,
          ChargedItem=S.ChargedItem,EffectiveInDate=S.EffectiveInDate,EffectiveOutDate=S.EffectiveOutDate,
          DrawPosNo=S.DrawPosNo,QtyKg=S.QtyKg,SourceRowKey=S.SourceRowKey,SourceObjVersion=S.SourceObjVersion,
          SourceHash=S.SourceHash,SourcePayloadJson=S.PayloadJson,LastSeenBatchId=@SyncBatchId,SyncedAtUtc=SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT
          (SiteCode,PartNo,EngChgLevel,BomTypeDb,AlternativeNo,LineItemNo,LineSequence,ComponentPartNo,
           ComponentContract,QtyPerAssembly,ComponentScrap,ShrinkageFactor,OperationNo,LeadtimeOffset,IssueTypeDb,
           ConsumptionItem,PhantomConsume,ChargedItem,EffectiveInDate,EffectiveOutDate,DrawPosNo,QtyKg,
           SourceRowKey,SourceObjVersion,SourceHash,SourcePayloadJson,LastSeenBatchId,SyncedAtUtc)
        VALUES
          (S.SiteCode,S.PartNo,S.EngChgLevel,S.BomTypeDb,S.AlternativeNo,S.LineItemNo,S.LineSequence,S.ComponentPartNo,
           S.ComponentContract,S.QtyPerAssembly,S.ComponentScrap,S.ShrinkageFactor,S.OperationNo,S.LeadtimeOffset,S.IssueTypeDb,
           S.ConsumptionItem,S.PhantomConsume,S.ChargedItem,S.EffectiveInDate,S.EffectiveOutDate,S.DrawPosNo,S.QtyKg,
           S.SourceRowKey,S.SourceObjVersion,S.SourceHash,S.PayloadJson,@SyncBatchId,SYSUTCDATETIME())
        OUTPUT ''BOM_COMPONENT'',$action INTO @Changes;

        MERGE MES.IfsInventoryPart AS T
        USING (
          SELECT ip.*,pc.LotTrackingCodeDb,pc.SerialTrackingCodeDb
          FROM IFS.InventoryPart ip
          LEFT JOIN IFS.PartCatalog pc ON pc.PartNo=ip.PartNo
        ) AS S
          ON T.SiteCode=S.SiteCode AND T.PartNo=S.PartNo
        WHEN MATCHED THEN UPDATE SET
          Description=S.Description,InventoryUom=S.UnitMeas,PartType=S.TypeCodeDb,PartStatus=S.PartStatus,
          LotTrackingCode=S.LotTrackingCodeDb,SerialTrackingCode=S.SerialTrackingCodeDb,
          SourceModifiedAt=S.LastActivityDate,SourceRowKey=S.SourceRowKey,SourceHash=S.SourceHash,
          SyncedAtUtc=SYSUTCDATETIME(),IsActive=1
        WHEN NOT MATCHED THEN INSERT
          (SiteCode,PartNo,Description,InventoryUom,PartType,PartStatus,LotTrackingCode,SerialTrackingCode,
           IsActive,SourceModifiedAt,SourceRowKey,SourceHash,SyncedAtUtc)
        VALUES
          (S.SiteCode,S.PartNo,S.Description,S.UnitMeas,S.TypeCodeDb,S.PartStatus,S.LotTrackingCodeDb,
           S.SerialTrackingCodeDb,1,S.LastActivityDate,S.SourceRowKey,S.SourceHash,SYSUTCDATETIME())
        OUTPUT ''MES_IFS_INVENTORY_PART'',$action INTO @Changes;

        UPDATE IFS.SyncBatch
        SET Status=''APPLIED'',CompletedAtUtc=SYSUTCDATETIME(),
            InsertCount=(SELECT COUNT(*) FROM @Changes WHERE ActionName=''INSERT''),
            UpdateCount=(SELECT COUNT(*) FROM @Changes WHERE ActionName=''UPDATE'')
        WHERE SyncBatchId=@SyncBatchId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        UPDATE IFS.SyncBatch SET Status=''FAILED'',CompletedAtUtc=SYSUTCDATETIME(),
            ErrorSummary=LEFT(ERROR_MESSAGE(),2000) WHERE SyncBatchId=@SyncBatchId;
        THROW;
    END CATCH
END
');

INSERT MES.SchemaMigration(MigrationVersion,MigrationName)
VALUES('012',N'IFS one-way master data and BOM synchronization');
COMMIT TRANSACTION;
GO
