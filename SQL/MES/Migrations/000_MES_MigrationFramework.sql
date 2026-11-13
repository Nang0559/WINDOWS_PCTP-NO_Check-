/*
 FVN_PCTP MES migration framework. Run in target DB context; no hardcoded USE.
 SQL Server 2016 compatible.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF SCHEMA_ID(N'MES') IS NULL EXEC(N'CREATE SCHEMA MES AUTHORIZATION dbo');
IF OBJECT_ID(N'MES.SchemaMigration',N'U') IS NULL
BEGIN
 CREATE TABLE MES.SchemaMigration(
  MigrationVersion varchar(20) NOT NULL,
  MigrationName nvarchar(200) NOT NULL,
  ScriptSha256 char(64) NULL,
  AppliedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MES_SchemaMigration_AppliedAt DEFAULT(SYSUTCDATETIME()),
  AppliedBy sysname NOT NULL CONSTRAINT DF_MES_SchemaMigration_AppliedBy DEFAULT(ORIGINAL_LOGIN()),
  HostName nvarchar(128) NULL CONSTRAINT DF_MES_SchemaMigration_Host DEFAULT(HOST_NAME()),
  CONSTRAINT PK_MES_SchemaMigration PRIMARY KEY(MigrationVersion));
END;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='000')
 INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('000',N'MES migration framework');
GO
