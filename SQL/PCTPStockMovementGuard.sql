/*
    PCTP stock movement idempotency guard.
    Deploy to the dedicated QC/test database first, then the approved application database.
    Do not run against production without backup/change approval.
    The guard row must be inserted in the same UnitOfWork transaction as stock,
    delivery and history writes; rollback removes the claim with the stock changes.
*/
IF OBJECT_ID(N'dbo.PCTPStockMovementGuard', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PCTPStockMovementGuard
    (
        GuardId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PCTPStockMovementGuard PRIMARY KEY,
        ReferenceType nvarchar(80) NULL,
        ReferenceId nvarchar(200) NULL,
        QrHash varbinary(32) NULL,
        TemHash varbinary(32) NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_PCTPStockMovementGuard_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_PCTPStockMovementGuard_ReferencePair CHECK
        (
            (ReferenceType IS NULL AND ReferenceId IS NULL)
            OR (ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL)
        )
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PCTPStockMovementGuard') AND name = N'UX_PCTPStockMovementGuard_Reference')
    CREATE UNIQUE INDEX UX_PCTPStockMovementGuard_Reference
        ON dbo.PCTPStockMovementGuard(ReferenceType, ReferenceId)
        WHERE ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PCTPStockMovementGuard') AND name = N'UX_PCTPStockMovementGuard_QrHash')
    CREATE UNIQUE INDEX UX_PCTPStockMovementGuard_QrHash
        ON dbo.PCTPStockMovementGuard(QrHash)
        WHERE QrHash IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PCTPStockMovementGuard') AND name = N'UX_PCTPStockMovementGuard_TemHash')
    CREATE UNIQUE INDEX UX_PCTPStockMovementGuard_TemHash
        ON dbo.PCTPStockMovementGuard(TemHash)
        WHERE TemHash IS NOT NULL;
GO
