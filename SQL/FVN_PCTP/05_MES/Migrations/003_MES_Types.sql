/*
 Migration 003: strongly typed TVP contracts for bulk scanning/import.
 SQL Server 2016-compatible conditional CREATE TYPE.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='002') THROW 51000,'Run migration 002 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='003') RETURN;
BEGIN TRANSACTION;

IF TYPE_ID(N'MES.TVP_LotScan') IS NULL
 EXEC(N'CREATE TYPE MES.TVP_LotScan AS TABLE(
  ClientRowNo int NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  PartNo nvarchar(100) NOT NULL,
  LotNo nvarchar(120) NOT NULL,
  LocationNo nvarchar(100) NULL,
  QrRawValue nvarchar(1000) NULL,
  Quantity decimal(19,6) NULL,
  UomCode nvarchar(30) NULL,
  PRIMARY KEY(ClientRowNo))');

IF TYPE_ID(N'MES.TVP_InventoryMovement') IS NULL
 EXEC(N'CREATE TYPE MES.TVP_InventoryMovement AS TABLE(
  ClientRowNo int NOT NULL,
  SiteCode nvarchar(30) NOT NULL,
  PartNo nvarchar(100) NOT NULL,
  LotNo nvarchar(120) NOT NULL,
  MovementType varchar(20) NOT NULL,
  Quantity decimal(19,6) NOT NULL,
  UomCode nvarchar(30) NOT NULL,
  FromLocationNo nvarchar(100) NULL,
  ToLocationNo nvarchar(100) NULL,
  FromStockState varchar(16) NULL,
  ToStockState varchar(16) NULL,
  ReferenceType varchar(30) NOT NULL,
  ReferenceNo nvarchar(100) NULL,
  ReferenceLineNo int NULL,
  IdempotencyKey nvarchar(180) NOT NULL,
  TransactionAtUtc datetime2(3) NOT NULL,
  ActorCode nvarchar(100) NOT NULL,
  PRIMARY KEY(ClientRowNo),
  UNIQUE(IdempotencyKey))');

IF TYPE_ID(N'MES.TVP_BomComponent') IS NULL
 EXEC(N'CREATE TYPE MES.TVP_BomComponent AS TABLE(
  LineNo int NOT NULL,
  ParentLineNo int NULL,
  SiteCode nvarchar(30) NOT NULL,
  ComponentPartNo nvarchar(100) NOT NULL,
  QtyPer decimal(19,6) NOT NULL,
  UomCode nvarchar(30) NOT NULL,
  OperationNo nvarchar(40) NULL,
  LineType varchar(20) NOT NULL,
  IsPhantom bit NOT NULL,
  PRIMARY KEY(LineNo))');

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('003',N'MES table-valued parameter contracts');
COMMIT TRANSACTION;
GO
