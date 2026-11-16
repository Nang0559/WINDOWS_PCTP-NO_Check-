/*
 Migration 001: IFS9 synchronized read models and controlled BOM.
 SQL Server 2016. IFS9 is read-only. Cost bucket source is staged only.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'MES.SchemaMigration',N'U') IS NULL THROW 51000,'Run migration 000 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='001') RETURN;
BEGIN TRANSACTION;

IF OBJECT_ID(N'MES.IfsSyncBatch',N'U') IS NULL
CREATE TABLE MES.IfsSyncBatch(
 SyncBatchId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_IfsSyncBatch PRIMARY KEY,
 SourceSystem varchar(20) NOT NULL CONSTRAINT DF_MES_IfsSync_Source DEFAULT('IFS9'),
 EntityName sysname NOT NULL, SiteCode nvarchar(30) NULL,
 SyncMode varchar(12) NOT NULL, Status varchar(16) NOT NULL,
 StartedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_IfsSync_Started DEFAULT(SYSUTCDATETIME()),
 CompletedAtUtc datetime2(3) NULL,
 ReadCount int NOT NULL CONSTRAINT DF_MES_IfsSync_Read DEFAULT(0),
 InsertCount int NOT NULL CONSTRAINT DF_MES_IfsSync_Insert DEFAULT(0),
 UpdateCount int NOT NULL CONSTRAINT DF_MES_IfsSync_Update DEFAULT(0),
 ErrorCount int NOT NULL CONSTRAINT DF_MES_IfsSync_Error DEFAULT(0),
 SourceWatermark nvarchar(200) NULL, ErrorSummary nvarchar(2000) NULL,
 CONSTRAINT CK_MES_IfsSync_Mode CHECK(SyncMode IN('FULL','INCREMENTAL')),
 CONSTRAINT CK_MES_IfsSync_Status CHECK(Status IN('RUNNING','SUCCEEDED','PARTIAL','FAILED')),
 CONSTRAINT CK_MES_IfsSync_Counts CHECK(ReadCount>=0 AND InsertCount>=0 AND UpdateCount>=0 AND ErrorCount>=0));

IF OBJECT_ID(N'MES.IfsInventoryPart',N'U') IS NULL
CREATE TABLE MES.IfsInventoryPart(
 SiteCode nvarchar(30) NOT NULL, PartNo nvarchar(100) NOT NULL,
 Description nvarchar(500) NULL, InventoryUom nvarchar(30) NULL,
 PartType nvarchar(50) NULL, PartStatus nvarchar(30) NULL,
 LotTrackingCode nvarchar(30) NULL, SerialTrackingCode nvarchar(30) NULL,
 IsActive bit NOT NULL CONSTRAINT DF_MES_IfsPart_Active DEFAULT(1),
 SourceModifiedAt datetime2(3) NULL, SourceRowKey nvarchar(250) NULL,
 SourceHash varbinary(32) NULL, SyncBatchId bigint NULL,
 SyncedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_IfsPart_Synced DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT PK_MES_IfsInventoryPart PRIMARY KEY(SiteCode,PartNo),
 CONSTRAINT FK_MES_IfsPart_Sync FOREIGN KEY(SyncBatchId) REFERENCES MES.IfsSyncBatch(SyncBatchId));

IF OBJECT_ID(N'MES.IfsWarehouse',N'U') IS NULL
CREATE TABLE MES.IfsWarehouse(
 SiteCode nvarchar(30) NOT NULL, WarehouseCode nvarchar(60) NOT NULL,
 Description nvarchar(250) NULL, IsActive bit NOT NULL CONSTRAINT DF_MES_IfsWh_Active DEFAULT(1),
 SourceRowKey nvarchar(250) NULL, SourceHash varbinary(32) NULL, SyncBatchId bigint NULL,
 CONSTRAINT PK_MES_IfsWarehouse PRIMARY KEY(SiteCode,WarehouseCode),
 CONSTRAINT FK_MES_IfsWh_Sync FOREIGN KEY(SyncBatchId) REFERENCES MES.IfsSyncBatch(SyncBatchId));

IF OBJECT_ID(N'MES.IfsLocationGroup',N'U') IS NULL
CREATE TABLE MES.IfsLocationGroup(
 SiteCode nvarchar(30) NOT NULL, LocationGroupCode nvarchar(60) NOT NULL,
 Description nvarchar(250) NULL, LocationType nvarchar(30) NULL,
 IsActive bit NOT NULL CONSTRAINT DF_MES_IfsLocGrp_Active DEFAULT(1),
 SourceRowKey nvarchar(250) NULL, SourceHash varbinary(32) NULL, SyncBatchId bigint NULL,
 CONSTRAINT PK_MES_IfsLocationGroup PRIMARY KEY(SiteCode,LocationGroupCode),
 CONSTRAINT FK_MES_IfsLocGrp_Sync FOREIGN KEY(SyncBatchId) REFERENCES MES.IfsSyncBatch(SyncBatchId));

IF OBJECT_ID(N'MES.IfsInventoryLocation',N'U') IS NULL
CREATE TABLE MES.IfsInventoryLocation(
 SiteCode nvarchar(30) NOT NULL, LocationNo nvarchar(100) NOT NULL,
 WarehouseCode nvarchar(60) NOT NULL, LocationGroupCode nvarchar(60) NULL,
 LocationType nvarchar(30) NULL, Description nvarchar(250) NULL,
 IsActive bit NOT NULL CONSTRAINT DF_MES_IfsLoc_Active DEFAULT(1),
 SourceRowKey nvarchar(250) NULL, SourceHash varbinary(32) NULL, SyncBatchId bigint NULL,
 SyncedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_IfsLoc_Synced DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT PK_MES_IfsInventoryLocation PRIMARY KEY(SiteCode,LocationNo),
 CONSTRAINT FK_MES_IfsLoc_Wh FOREIGN KEY(SiteCode,WarehouseCode) REFERENCES MES.IfsWarehouse(SiteCode,WarehouseCode),
 CONSTRAINT FK_MES_IfsLoc_Group FOREIGN KEY(SiteCode,LocationGroupCode) REFERENCES MES.IfsLocationGroup(SiteCode,LocationGroupCode),
 CONSTRAINT FK_MES_IfsLoc_Sync FOREIGN KEY(SyncBatchId) REFERENCES MES.IfsSyncBatch(SyncBatchId));

IF OBJECT_ID(N'MES.IfsBomBucketStage',N'U') IS NULL
CREATE TABLE MES.IfsBomBucketStage(
 IfsBomBucketStageId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_IfsBomBucketStage PRIMARY KEY,
 SyncBatchId bigint NOT NULL, SiteCode nvarchar(30) NOT NULL,
 CostSet int NOT NULL, PartNo nvarchar(100) NOT NULL,
 AlternativeNo nvarchar(80) NOT NULL, RoutingAlternativeNo nvarchar(80) NOT NULL,
 BucketLevel int NOT NULL, BucketSeq int NOT NULL, ParentBucketSeq int NULL,
 TopLevelPartNo nvarchar(100) NULL, ComponentPartNo nvarchar(100) NULL,
 BucketType nvarchar(50) NULL, Quantity decimal(19,6) NULL, UomCode nvarchar(30) NULL,
 SourceRowKey nvarchar(300) NOT NULL, SourceHash varbinary(32) NULL,
 VerificationStatus varchar(32) NOT NULL CONSTRAINT DF_MES_IfsBomStage_Status DEFAULT('PENDING_ENGINEERING_VERIFICATION'),
 StagedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_IfsBomStage_At DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT FK_MES_IfsBomStage_Batch FOREIGN KEY(SyncBatchId) REFERENCES MES.IfsSyncBatch(SyncBatchId),
 CONSTRAINT CK_MES_IfsBomStage_Status CHECK(VerificationStatus IN('PENDING_ENGINEERING_VERIFICATION','REJECTED','VERIFIED_SOURCE')),
 CONSTRAINT UQ_MES_IfsBomStage_Source UNIQUE(SyncBatchId,SiteCode,CostSet,PartNo,AlternativeNo,RoutingAlternativeNo,BucketSeq));

IF OBJECT_ID(N'MES.BomRevision',N'U') IS NULL
CREATE TABLE MES.BomRevision(
 BomRevisionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_BomRevision PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, ParentPartNo nvarchar(100) NOT NULL,
 RevisionCode nvarchar(60) NOT NULL, AlternateNo nvarchar(80) NOT NULL CONSTRAINT DF_MES_BomRev_Alt DEFAULT(N'*'),
 StructureType varchar(24) NOT NULL, BomStatus varchar(20) NOT NULL CONSTRAINT DF_MES_BomRev_Status DEFAULT('DRAFT'),
 EffectiveFrom datetime2(0) NULL, EffectiveTo datetime2(0) NULL,
 SourceKind varchar(20) NOT NULL CONSTRAINT DF_MES_BomRev_Source DEFAULT('MANUAL'),
 SourceReference nvarchar(250) NULL, ApprovedAtUtc datetime2(3) NULL, ApprovedBy nvarchar(100) NULL,
 CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_BomRev_Created DEFAULT(SYSUTCDATETIME()),
 CONSTRAINT UQ_MES_BomRevision_Business UNIQUE(SiteCode,ParentPartNo,RevisionCode,AlternateNo,StructureType),
 CONSTRAINT FK_MES_BomRevision_ParentPart FOREIGN KEY(SiteCode,ParentPartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT CK_MES_BomRevision_Type CHECK(StructureType IN('PRODUCTION','ENGINEERING','SERVICE')),
 CONSTRAINT CK_MES_BomRevision_Status CHECK(BomStatus IN('DRAFT','PENDING','APPROVED','RETIRED','REJECTED')),
 CONSTRAINT CK_MES_BomRevision_Dates CHECK(EffectiveTo IS NULL OR EffectiveFrom IS NULL OR EffectiveTo>EffectiveFrom),
 CONSTRAINT CK_MES_BomRevision_Source CHECK(SourceKind IN('MANUAL','IFS_VERIFIED','MIGRATED')));

IF OBJECT_ID(N'MES.BomComponent',N'U') IS NULL
CREATE TABLE MES.BomComponent(
 BomComponentId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MES_BomComponent PRIMARY KEY,
 SiteCode nvarchar(30) NOT NULL, BomRevisionId bigint NOT NULL,
 LineNo int NOT NULL, ParentLineNo int NULL, ComponentPartNo nvarchar(100) NOT NULL,
 QtyPer decimal(19,6) NOT NULL, UomCode nvarchar(30) NOT NULL,
 OperationNo nvarchar(40) NULL, LineType varchar(20) NOT NULL CONSTRAINT DF_MES_BomComp_Type DEFAULT('COMPONENT'),
 IsPhantom bit NOT NULL CONSTRAINT DF_MES_BomComp_Phantom DEFAULT(0),
 ScrapFactor decimal(9,6) NULL, SourceReference nvarchar(250) NULL,
 CONSTRAINT FK_MES_BomComp_Revision FOREIGN KEY(BomRevisionId) REFERENCES MES.BomRevision(BomRevisionId),
 CONSTRAINT UQ_MES_BomComp_Line UNIQUE(BomRevisionId,LineNo),
 CONSTRAINT FK_MES_BomComp_Part FOREIGN KEY(SiteCode,ComponentPartNo) REFERENCES MES.IfsInventoryPart(SiteCode,PartNo),
 CONSTRAINT CK_MES_BomComp_Qty CHECK(QtyPer>0),
 CONSTRAINT CK_MES_BomComp_Scrap CHECK(ScrapFactor IS NULL OR ScrapFactor>=0),
 CONSTRAINT CK_MES_BomComp_Type CHECK(LineType IN('COMPONENT','BYPRODUCT','COPRODUCT','PHANTOM')));

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('001',N'IFS9 mirrors and controlled BOM');
COMMIT TRANSACTION;
GO
