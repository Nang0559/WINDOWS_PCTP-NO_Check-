/*
 Migration 010: least-privilege database roles.
 Create roles only; map actual database users/logins separately after identity approval.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='009') THROW 51000,'Run migration 009 first.',1;
IF EXISTS(SELECT 1 FROM MES.SchemaMigration WHERE MigrationVersion='010') RETURN;
BEGIN TRANSACTION;

IF DATABASE_PRINCIPAL_ID(N'MES_Reader') IS NULL CREATE ROLE MES_Reader AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'MES_Operator') IS NULL CREATE ROLE MES_Operator AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'MES_BOMApprover') IS NULL CREATE ROLE MES_BOMApprover AUTHORIZATION dbo;

GRANT SELECT ON OBJECT::MES.IfsInventoryPart TO MES_Reader;
GRANT SELECT ON OBJECT::MES.IfsWarehouse TO MES_Reader;
GRANT SELECT ON OBJECT::MES.IfsLocationGroup TO MES_Reader;
GRANT SELECT ON OBJECT::MES.IfsInventoryLocation TO MES_Reader;
GRANT SELECT ON OBJECT::MES.BomRevision TO MES_Reader;
GRANT SELECT ON OBJECT::MES.BomComponent TO MES_Reader;
GRANT SELECT ON OBJECT::MES.MaterialLot TO MES_Reader;
GRANT SELECT ON OBJECT::MES.InventoryBalance TO MES_Reader;
GRANT SELECT ON OBJECT::MES.InventoryTransaction TO MES_Reader;
GRANT SELECT ON OBJECT::MES.FinishedLotGenealogy TO MES_Reader;
GRANT SELECT ON OBJECT::MES.fn_AvailableStockByPart TO MES_Reader;
GRANT SELECT ON OBJECT::MES.fn_LotMovementTimeline TO MES_Reader;
GRANT EXECUTE ON OBJECT::MES.usp_PostInventoryMovement TO MES_Operator;
GRANT EXECUTE ON OBJECT::MES.usp_ReserveMaterial TO MES_Operator;
GRANT EXECUTE ON OBJECT::MES.usp_ConsumeMaterialReservation TO MES_Operator;
GRANT EXECUTE ON OBJECT::MES.usp_ReleaseMaterialReservation TO MES_Operator;
GRANT EXECUTE ON OBJECT::MES.usp_LinkConsumedLotToFinishedLot TO MES_Operator;
GRANT EXECUTE ON OBJECT::MES.usp_GetFifoCandidates TO MES_Reader;
GRANT EXECUTE ON OBJECT::MES.usp_GetForwardGenealogy TO MES_Reader;
GRANT EXECUTE ON OBJECT::MES.usp_GetBackwardGenealogy TO MES_Reader;
GRANT EXECUTE ON OBJECT::MES.usp_ValidateInventoryBalance TO MES_Reader;
GRANT EXECUTE ON OBJECT::MES.usp_ApproveBomRevision TO MES_BOMApprover;

INSERT MES.SchemaMigration(MigrationVersion,MigrationName) VALUES('010',N'Least privilege MES database roles');
COMMIT TRANSACTION;
GO
