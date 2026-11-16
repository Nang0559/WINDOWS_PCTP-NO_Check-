/*
    PCTP - audit history for manual LOT stock reconciliation.
    Apply this script to each configured database where the LOT adjustment feature will be used, before enabling the UI.
    This script creates metadata only; it does not alter STOCKTP or SlotLot data.
*/
IF OBJECT_ID(N'dbo.PCTPStockLotAdjustmentHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PCTPStockLotAdjustmentHistory
    (
        RequestId UNIQUEIDENTIFIER NOT NULL,
        ItemCode NVARCHAR(100) NOT NULL,
        LotNo NVARCHAR(100) NOT NULL,
        StockBefore INT NOT NULL,
        StockAfter INT NOT NULL,
        SlotBefore INT NOT NULL,
        SlotAfter INT NOT NULL,
        StockDelta INT NOT NULL,
        SlotDelta INT NOT NULL,
        DestinationSlotId INT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        Actor NVARCHAR(256) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL
            CONSTRAINT DF_PCTPStockLotAdjustmentHistory_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PCTPStockLotAdjustmentHistory PRIMARY KEY CLUSTERED (RequestId)
    );

    CREATE INDEX IX_PCTPStockLotAdjustmentHistory_LotCreatedAt
        ON dbo.PCTPStockLotAdjustmentHistory (ItemCode, LotNo, CreatedAt DESC);
END;
GO
