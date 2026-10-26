/*
    Enforce the confirmed STOCKTP business key: one row per LOT.
    DEPLOYMENT SCRIPT - NOT executed automatically by QC.
    Run only after reviewing PCTP_StockTpIdentityAudit.sql output on an approved
    isolated business-schema copy / DB57. Never run against production during tests.

    Rack/Slot distribution is many-to-many at the location-detail level:
    one STOCKTP LOT row -> many active SlotLot rows across multiple Slots.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS
(
    SELECT 1
    FROM dbo.STOCKTP
    WHERE LOT IS NULL OR LTRIM(RTRIM(LOT)) = N''
)
    THROW 51001, 'STOCKTP contains NULL/blank LOT. Resolve data before enforcing unique LOT.', 1;

IF EXISTS
(
    SELECT LTRIM(RTRIM(LOT))
    FROM dbo.STOCKTP
    GROUP BY LTRIM(RTRIM(LOT))
    HAVING COUNT_BIG(*) > 1
)
    THROW 51002, 'STOCKTP contains duplicate normalized LOT values. Reconcile them before enforcing unique LOT.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOCKTP')
      AND name = N'UX_STOCKTP_LOT'
)
BEGIN
    CREATE UNIQUE INDEX UX_STOCKTP_LOT
        ON dbo.STOCKTP(LOT);
END;

COMMIT TRANSACTION;
