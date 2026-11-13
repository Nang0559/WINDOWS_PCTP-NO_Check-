/*
 MES deployment smoke test - read only.
 Run after migration 010. No data is inserted or modified.
*/
SET NOCOUNT ON;
SELECT DB_NAME() AS TargetDatabase,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(30)) AS SqlServerVersion,
       CAST(SERVERPROPERTY('Edition') AS nvarchar(100)) AS SqlServerEdition;

IF OBJECT_ID(N'MES.SchemaMigration',N'U') IS NULL
 THROW 52000,'MES migration framework is missing.',1;

SELECT MigrationVersion,MigrationName,AppliedAtUtc,AppliedBy
FROM MES.SchemaMigration ORDER BY MigrationVersion;

DECLARE @Expected TABLE(ObjectName sysname NOT NULL,ObjectType char(2) NOT NULL);
INSERT @Expected(ObjectName,ObjectType) VALUES
('IfsSyncBatch','U'),('IfsInventoryPart','U'),('IfsWarehouse','U'),('IfsLocationGroup','U'),
('IfsInventoryLocation','U'),('IfsBomBucketStage','U'),('BomRevision','U'),('BomComponent','U'),
('PurchaseOrder','U'),('PurchaseOrderLine','U'),('MaterialLot','U'),('InventoryTransaction','U'),
('InventoryBalance','U'),('MaterialReceipt','U'),('MaterialReceiptLine','U'),
('ProductionPlan','U'),('ProductionPlanLine','U'),('ProductionOrder','U'),
('ProductionOrderMaterial','U'),('MaterialExecutionEvent','U'),('FinishedLotGenealogy','U'),
('MaterialReservation','U'),('usp_PostInventoryMovement','P'),('usp_GetFifoCandidates','P'),
('usp_GetForwardGenealogy','P'),('usp_GetBackwardGenealogy','P'),('usp_ReserveMaterial','P'),
('usp_ConsumeMaterialReservation','P'),('usp_ReleaseMaterialReservation','P'),
('usp_LinkConsumedLotToFinishedLot','P'),('usp_ValidateInventoryBalance','P'),
('usp_ApproveBomRevision','P'),('fn_AvailableStockByPart','IF'),('fn_LotMovementTimeline','IF');
SELECT e.ObjectName,e.ObjectType
FROM @Expected e
WHERE OBJECT_ID(N'MES.'+e.ObjectName,e.ObjectType) IS NULL
ORDER BY e.ObjectType,e.ObjectName;

SELECT fk.name AS ForeignKeyName,
       OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ParentSchema,
       OBJECT_NAME(fk.parent_object_id) AS ParentTable,
       fk.is_disabled,fk.is_not_trusted
FROM sys.foreign_keys fk
WHERE fk.is_disabled=1 OR fk.is_not_trusted=1
ORDER BY ParentSchema,ParentTable,ForeignKeyName;

SELECT s.name AS SchemaName,o.name AS ObjectName,i.name AS IndexName,i.type_desc,i.is_disabled
FROM sys.indexes i
JOIN sys.objects o ON o.object_id=i.object_id
JOIN sys.schemas s ON s.schema_id=o.schema_id
WHERE s.name='MES' AND i.index_id>0 AND i.is_disabled=1
ORDER BY o.name,i.name;

-- Reconciliation: expected output is zero rows after all test movements.
EXEC MES.usp_ValidateInventoryBalance @SiteCode=NULL;
