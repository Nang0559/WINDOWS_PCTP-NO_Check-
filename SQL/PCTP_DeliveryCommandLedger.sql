/*
    B0-01 / P7 / P8 prerequisite: durable ledger for the customer delivery stock command.
    DEPLOYMENT SCRIPT - NOT executed automatically by QC. Idempotent: safe to re-run.
    Run only on an approved isolated copy first; never against production during tests.

    - DeliveryCommandLedger : one row per committed BusinessKey (P7 replay returns prior outcome).
    - DeliveryCommandQr     : one row per delivered QR; UNIQUE QR rejects replay under another key (P8).
    Both tables are written in the same transaction as STOCKTP/SlotLot (single UoW).
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.DeliveryCommandLedger', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryCommandLedger
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DeliveryCommandLedger PRIMARY KEY,
        BusinessKey       NVARCHAR(200) NOT NULL,
        CustomerCode      NVARCHAR(50)  NOT NULL,
        DeliveryReference NVARCHAR(100) NOT NULL,
        PerformedBy       NVARCHAR(100) NOT NULL,
        OccurredAt        DATETIME2(0)  NOT NULL,
        TotalQuantity     INT           NOT NULL,
        PendingSttCsv     NVARCHAR(1000) NOT NULL,
        CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_DeliveryCommandLedger_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT CK_DeliveryCommandLedger_Qty CHECK (TotalQuantity > 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DeliveryCommandLedger') AND name = N'UX_DeliveryCommandLedger_BusinessKey')
    CREATE UNIQUE INDEX UX_DeliveryCommandLedger_BusinessKey ON dbo.DeliveryCommandLedger(BusinessKey);

IF OBJECT_ID(N'dbo.DeliveryCommandQr', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryCommandQr
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DeliveryCommandQr PRIMARY KEY,
        LedgerId    INT           NOT NULL CONSTRAINT FK_DeliveryCommandQr_Ledger REFERENCES dbo.DeliveryCommandLedger(Id),
        PartCode    NVARCHAR(100) NOT NULL,
        QrKey       NVARCHAR(450) NOT NULL,
        CreatedAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_DeliveryCommandQr_CreatedAt DEFAULT (SYSDATETIME())
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DeliveryCommandQr') AND name = N'UX_DeliveryCommandQr_QrKey')
    CREATE UNIQUE INDEX UX_DeliveryCommandQr_QrKey ON dbo.DeliveryCommandQr(QrKey);

COMMIT TRANSACTION;
