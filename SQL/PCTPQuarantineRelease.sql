/*
 PCTP quarantine release audit contract.
 Safe deployment: metadata-only IF NOT EXISTS creation; no business rows are changed.
 ReleaseService writes this table inside the same UnitOfWork as SlotLot/stock changes.
*/
IF OBJECT_ID(N'dbo.FVN_PCTPQuarantineReleaseAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_PCTPQuarantineReleaseAudit
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_PCTPQuarantineReleaseAudit PRIMARY KEY,
        SlotLotId int NOT NULL,
        SlotId int NOT NULL,
        LotNo nvarchar(500) NOT NULL,
        ItemCode nvarchar(100) NOT NULL,
        Quantity int NOT NULL CONSTRAINT CK_FVN_PCTPQuarantineReleaseAudit_Quantity CHECK (Quantity > 0),
        PerformedBy nvarchar(200) NOT NULL,
        ReferenceId nvarchar(200) NOT NULL,
        Reason nvarchar(1000) NOT NULL,
        ReleasedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_PCTPQuarantineReleaseAudit_ReleasedAtUtc DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_FVN_PCTPQuarantineReleaseAudit_SlotLotId
        ON dbo.FVN_PCTPQuarantineReleaseAudit(SlotLotId, ReleasedAtUtc DESC);
END;
GO
