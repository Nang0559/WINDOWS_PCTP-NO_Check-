/*
 Read-only smoke test for the FVN_PCTP schema foundation.
 Run after schema migrations 000..023 and 025.
 Optional migration 024 is not required.
 No source or target data is modified.
*/
SET NOCOUNT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52900, 'Run this smoke test only in FVN_PCTP.', 1;

DECLARE @Expected TABLE
(
    ObjectSchema sysname NOT NULL,
    ObjectName sysname NOT NULL,
    ObjectType char(2) NOT NULL
);

INSERT INTO @Expected (ObjectSchema,ObjectName,ObjectType)
VALUES
('dbo','FVN_PCTPSite','U'),
('dbo','FVN_Warehouse','U'),
('dbo','FVN_Rack','U'),
('dbo','FVN_Slot','U'),
('dbo','FVN_SlotMesLocation','U'),
('dbo','FVN_PartIdentity','U'),
('dbo','FVN_PartOperationalProfile','U'),
('dbo','FVN_PartCustomerRule','U'),
('dbo','FVN_Gear','U'),
('dbo','FVN_STOCKTP','U'),
('dbo','FVN_SlotLot','U'),
('dbo','FVN_StockHistory','U'),
('dbo','FVN_IFSPHIEUGIAOHANG','U'),
('dbo','FVN_IFSPHIEUGIAOHANG_SP','U'),
('dbo','FVN_LUUPHIEUGIAOHANG','U'),
('dbo','FVN_DeliveryCommandLedger','U'),
('dbo','FVN_DeliveryCommandQr','U'),
('dbo','FVN_DocQrCode','U'),
('dbo','FVN_DocQrCodeDb','U'),
('dbo','FVN_InternalTransfer','U'),
('dbo','FVN_InternalReturn','U'),
('dbo','FVN_PhieuTraHang','U'),
('dbo','FVN_PhieuTraHangCT','U'),
('dbo','FVN_PhieuXuLyBatThuong','U'),
('dbo','FVN_PhieuXuLyBatThuongAffectedLot','U'),
('dbo','FVN_PhieuXuLyBatThuongQCInitial','U'),
('dbo','FVN_PXLB_ReworkPlan','U'),
('dbo','FVN_PXLB_ReworkMovement','U'),
('dbo','FVN_PXLB_ReworkQC','U'),
('dbo','FVN_PXLB_Disposition','U'),
('dbo','FVN_PXLB_Compensation','U'),
('dbo','FVN_PXLB_CompensationMovement','U'),
('dbo','FVN_PXLB_WorkflowAudit','U'),
('dbo','FVN_TraHangQTChungXuat','U'),
('dbo','FVN_TraHangQTChungGiao','U'),
('dbo','FVN_TraHangQTChungQC','U'),
('dbo','FVN_TraHangQTChungNhapNG','U'),
('dbo','FVN_sys_WorkflowTransitions','U'),
('dbo','FVN_HangChoGiao','U'),
('dbo','FVN_HangChoGiaoPickDetail','U'),
('dbo','FVN_ItemFifoConfig','U'),
('dbo','FVN_ItemFifoConfig_History','U'),
('dbo','FVN_Purchase_Order_DB_HD','U'),
('dbo','FVN_Purchase_Order_DB_CT','U'),
('dbo','FVN_Purchase_Order_HVNDB','U'),
('dbo','FVN_InspectionLog','U'),
('dbo','vFVN_PCTP_Part','V'),
('dbo','vFVN_STOCKTP','V'),
('dbo','vFVN_PCTP_SlotInventory','V'),
('dbo','usp_FVN_Slot_SaveLots','P'),
('dbo','usp_FVN_ItemFifoConfig_Upsert','P'),
('dbo','usp_FVN_DocQrCode_Insert','P'),
('dbo','usp_FVN_PCTP_PostStockMovement','P');

SELECT e.ObjectSchema,e.ObjectName,e.ObjectType
FROM @Expected e
WHERE OBJECT_ID(QUOTENAME(e.ObjectSchema)+N'.'+QUOTENAME(e.ObjectName),e.ObjectType) IS NULL
ORDER BY e.ObjectSchema,e.ObjectName;

IF EXISTS
(
    SELECT 1
    FROM @Expected e
    WHERE OBJECT_ID(QUOTENAME(e.ObjectSchema)+N'.'+QUOTENAME(e.ObjectName),e.ObjectType) IS NULL
)
    THROW 52901, 'One or more required PCTP objects are missing.', 1;

IF TYPE_ID(N'dbo.FVN_SlotLotTableType') IS NULL
    THROW 52902, 'FVN_SlotLotTableType is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='023')
    THROW 52903, 'Schema migration 023 is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion='025')
    THROW 52904, 'MES posting adapter migration 025 is missing.', 1;

/* The key FKs must be composite; a PartNo-only FK is not accepted. */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys fk
    WHERE fk.name=N'FK_FVN_PartIdentity_IFSPart'
      AND (SELECT COUNT(*) FROM sys.foreign_key_columns fkc
           WHERE fkc.constraint_object_id=fk.object_id)=2
)
    THROW 52905, 'PartIdentity must reference IFS with the composite key.', 1;

/* No Bravo B20 master table may be recreated in the target. */
IF OBJECT_ID(N'dbo.B20Item',N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.B20Dept',N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.B20Gear',N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.B20ImageStore',N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.B20ItemQuyCach',N'U') IS NOT NULL
    THROW 52906, 'A forbidden Bravo B20 master table exists in FVN_PCTP.', 1;

SELECT
    N'PASS' AS SmokeTestStatus,
    N'FVN_PCTP schema objects and composite part key are present' AS Detail;

SELECT MigrationVersion,MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion BETWEEN '014' AND '023'
   OR MigrationVersion='025'
ORDER BY MigrationVersion;

SELECT
    (SELECT COUNT(*) FROM dbo.FVN_PCTPSite WHERE IsActive=1) AS ActivePCTPSites,
    (SELECT COUNT(*) FROM dbo.FVN_Warehouse WHERE IsActive=1) AS ActiveWarehouses,
    (SELECT COUNT(*) FROM dbo.FVN_SlotMesLocation WHERE IsActive=1 AND IsPrimary=1) AS ActivePrimarySlotMappings;
GO
