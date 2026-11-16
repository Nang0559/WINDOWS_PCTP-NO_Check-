/*
PHASE15 - Persist exact SlotLot rows consumed by a pending-delivery pick.
Run once before deploying the application code that supports cancelling pending delivery.
*/
IF OBJECT_ID(N'dbo.FVN_HangChoGiaoPickDetail', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FVN_HangChoGiaoPickDetail
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FVN_HangChoGiaoPickDetail PRIMARY KEY,
        HangChoGiaoId INT NOT NULL,
        SlotIdNguon INT NOT NULL,
        ItemCode NVARCHAR(100) NOT NULL,
        LotNo NVARCHAR(500) NOT NULL,
        Quantity INT NOT NULL,
        TemCode NVARCHAR(500) NULL,
        QrData NVARCHAR(MAX) NULL,
        ImportDate DATETIME NULL,
        PhieuStatus TINYINT NOT NULL CONSTRAINT DF_FVN_HangChoGiaoPickDetail_PhieuStatus DEFAULT(0),
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_FVN_HangChoGiaoPickDetail_CreatedAt DEFAULT(GETDATE()),
        CONSTRAINT CK_FVN_HangChoGiaoPickDetail_Quantity CHECK (Quantity > 0)
    );

    CREATE INDEX IX_FVN_HangChoGiaoPickDetail_HangChoGiaoId
        ON dbo.FVN_HangChoGiaoPickDetail(HangChoGiaoId, Id);
END
GO
