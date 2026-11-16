/*
    PCTP - Internal stock transfer destination ledger
    Apply to the configured PCTP QC database (DB57), never to DB14.
    Additive migration; does not alter legacy delivery or Slot tables.
*/
IF OBJECT_ID(N'dbo.PCTP_InternalLocation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PCTP_InternalLocation
    (
        LocationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PCTP_InternalLocation PRIMARY KEY,
        LocationName nvarchar(150) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_PCTP_InternalLocation_IsActive DEFAULT(1),
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_PCTP_InternalLocation_CreatedAt DEFAULT(SYSDATETIME()),
        CONSTRAINT UQ_PCTP_InternalLocation_Name UNIQUE(LocationName)
    );
END;
GO
IF OBJECT_ID(N'dbo.PCTP_InternalTransfer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PCTP_InternalTransfer
    (
        TransferId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PCTP_InternalTransfer PRIMARY KEY,
        RequestKey uniqueidentifier NOT NULL,
        TransferNo nvarchar(40) NULL,
        LocationId int NOT NULL,
        LotNo nvarchar(200) NOT NULL,
        ItemCode nvarchar(100) NOT NULL,
        Quantity int NOT NULL,
        SourceSlotId int NOT NULL,
        Reason nvarchar(500) NOT NULL,
        PerformedBy nvarchar(100) NOT NULL,
        CreatedAt datetime2(0) NOT NULL,
        CONSTRAINT CK_PCTP_InternalTransfer_Quantity CHECK (Quantity > 0),
        CONSTRAINT FK_PCTP_InternalTransfer_Location FOREIGN KEY(LocationId) REFERENCES dbo.PCTP_InternalLocation(LocationId)
    );
    CREATE UNIQUE INDEX UX_PCTP_InternalTransfer_TransferNo ON dbo.PCTP_InternalTransfer(TransferNo) WHERE TransferNo IS NOT NULL;
    CREATE UNIQUE INDEX UX_PCTP_InternalTransfer_RequestKey ON dbo.PCTP_InternalTransfer(RequestKey);
    CREATE INDEX IX_PCTP_InternalTransfer_Lot_Date ON dbo.PCTP_InternalTransfer(LotNo, CreatedAt DESC);
END;
GO
IF COL_LENGTH(N'dbo.PCTP_InternalTransfer', N'RequestKey') IS NULL
BEGIN
    ALTER TABLE dbo.PCTP_InternalTransfer ADD RequestKey uniqueidentifier NULL;
    UPDATE dbo.PCTP_InternalTransfer SET RequestKey = NEWID() WHERE RequestKey IS NULL;
    ALTER TABLE dbo.PCTP_InternalTransfer ALTER COLUMN RequestKey uniqueidentifier NOT NULL;
    CREATE UNIQUE INDEX UX_PCTP_InternalTransfer_RequestKey ON dbo.PCTP_InternalTransfer(RequestKey);
END;
GO
IF OBJECT_ID(N'dbo.PCTP_InternalStock', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PCTP_InternalStock
    (
        InternalStockId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PCTP_InternalStock PRIMARY KEY,
        LocationId int NOT NULL,
        ItemCode nvarchar(100) NOT NULL,
        LotNo nvarchar(200) NOT NULL,
        Quantity int NOT NULL,
        UpdatedAt datetime2(0) NOT NULL,
        CONSTRAINT CK_PCTP_InternalStock_Quantity CHECK (Quantity >= 0),
        CONSTRAINT FK_PCTP_InternalStock_Location FOREIGN KEY(LocationId) REFERENCES dbo.PCTP_InternalLocation(LocationId),
        CONSTRAINT UQ_PCTP_InternalStock_Location_Item_Lot UNIQUE(LocationId, ItemCode, LotNo)
    );
    CREATE INDEX IX_PCTP_InternalStock_Lot ON dbo.PCTP_InternalStock(LotNo, LocationId);
END;
GO

/*
    PCTP - Internal stock transfer + recall ledger.
    Apply only to the approved PCTP QC database (DB57); never apply to DB14.
*/
IF OBJECT_ID(N'dbo.PCTP_InternalReturn', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PCTP_InternalReturn
    (
        ReturnId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PCTP_InternalReturn PRIMARY KEY,
        RequestKey uniqueidentifier NOT NULL,
        ReturnNo nvarchar(40) NULL,
        TransferId int NOT NULL,
        TargetSlotId int NOT NULL,
        Quantity int NOT NULL,
        Reason nvarchar(500) NOT NULL,
        PerformedBy nvarchar(100) NOT NULL,
        CreatedAt datetime2(0) NOT NULL,
        CONSTRAINT CK_PCTP_InternalReturn_Quantity CHECK (Quantity > 0),
        CONSTRAINT FK_PCTP_InternalReturn_Transfer FOREIGN KEY(TransferId) REFERENCES dbo.PCTP_InternalTransfer(TransferId)
    );
    CREATE UNIQUE INDEX UX_PCTP_InternalReturn_RequestKey ON dbo.PCTP_InternalReturn(RequestKey);
    CREATE UNIQUE INDEX UX_PCTP_InternalReturn_ReturnNo ON dbo.PCTP_InternalReturn(ReturnNo) WHERE ReturnNo IS NOT NULL;
    CREATE INDEX IX_PCTP_InternalReturn_Transfer_Date ON dbo.PCTP_InternalReturn(TransferId, CreatedAt);
END;
GO
