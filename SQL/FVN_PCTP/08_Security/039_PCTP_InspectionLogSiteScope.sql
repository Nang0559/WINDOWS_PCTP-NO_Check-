/*
 Migration 039: site isolation for packaging inspection history.
 Apply after 038. Existing legacy rows remain SiteCode NULL and are not exposed
 through site-scoped history queries until explicitly backfilled by the owner.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'FVN_PCTP' THROW 53900,'Migration 039 may run only in FVN_PCTP.',1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration',N'U') IS NULL THROW 53901,'MES migration registry is missing.',1;
IF NOT EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='038')
 THROW 53902,'Migration 038 must be applied before 039.',1;
IF EXISTS(SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='039') RETURN;
IF OBJECT_ID(N'dbo.FVN_InspectionLog',N'U') IS NULL THROW 53903,'FVN_InspectionLog is missing.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF COL_LENGTH(N'dbo.FVN_InspectionLog',N'SiteCode') IS NULL
  ALTER TABLE dbo.FVN_InspectionLog ADD SiteCode nvarchar(30) NULL;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_InspectionLog') AND name=N'IX_FVN_InspectionLog_SiteCode_InspectionCode')
  CREATE INDEX IX_FVN_InspectionLog_SiteCode_InspectionCode
   ON dbo.FVN_InspectionLog(SiteCode,InspectionCode,CheckedAt);
 INSERT MES.FVN_SchemaMigration(MigrationVersion,MigrationName)
 VALUES('039',N'Site isolation for packaging inspection history');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
