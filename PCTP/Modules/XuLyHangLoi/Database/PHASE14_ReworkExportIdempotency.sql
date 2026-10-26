/*
    PHASE 14 - Rework export idempotency
    Adds a stable command identifier to the export ledger.
    Safe for existing databases: nullable for legacy rows; new application writes
    must provide a non-empty OperationId. Unique filtered index prevents a command
    from creating two export ledger rows.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'dbo.FVN_TraHangQTChungXuat', N'OperationId') IS NULL
    ALTER TABLE dbo.FVN_TraHangQTChungXuat ADD OperationId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FVN_TraHangQTChungXuat')
      AND name = N'UX_FVN_TraHangQTChungXuat_OperationId'
)
    CREATE UNIQUE INDEX UX_FVN_TraHangQTChungXuat_OperationId
        ON dbo.FVN_TraHangQTChungXuat(OperationId)
        WHERE OperationId IS NOT NULL;
GO

COMMIT TRANSACTION;
GO
PRINT N'PHASE14_ReworkExportIdempotency completed.';
GO
