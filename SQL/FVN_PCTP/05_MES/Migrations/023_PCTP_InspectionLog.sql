/*
 Migration 023: PCTP inspection/tem reconciliation history.
 Based on PCTP/Modules/XuLyHangLoi/Database/06_CREATE_InspectionLog.sql.
 Physical target is FVN_InspectionLog; item identity is site-scoped.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 52300, 'Migration 023 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 52301, 'Run MES migrations 000..013 first.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '022')
    THROW 52302, 'Migration 022 must be applied before migration 023.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '023')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.FVN_InspectionLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FVN_InspectionLog
        (
            LogId int IDENTITY(1,1) NOT NULL,
            SiteCode nvarchar(30) NOT NULL,
            InspectionCode nvarchar(100) NOT NULL,
            ItemCode nvarchar(100) NULL,
            PartNo nvarchar(100) NULL,
            TemCodeTong nvarchar(200) NULL,
            LotNoTong nvarchar(200) NULL,
            MaterialLotId bigint NULL,
            NSXTong nvarchar(50) NULL,
            SoLuongTong int NULL,
            MaPhieu nvarchar(100) NULL,
            BoxTemCode nvarchar(200) NULL,
            BoxLotNo nvarchar(200) NULL,
            BoxNSX nvarchar(50) NULL,
            IsMatch bit NULL CONSTRAINT DF_FVN_InspectionLog_IsMatch DEFAULT (1),
            MismatchFields nvarchar(500) NULL,
            FinalResult nvarchar(20) NULL,
            CheckedAt datetime NULL CONSTRAINT DF_FVN_InspectionLog_CheckedAt DEFAULT (GETDATE()),
            CheckedBy nvarchar(100) NULL,
            CONSTRAINT PK_FVN_InspectionLog PRIMARY KEY (LogId),
            CONSTRAINT FK_FVN_InspectionLog_Site FOREIGN KEY (SiteCode)
                REFERENCES dbo.FVN_PCTPSite (SiteCode),
            CONSTRAINT FK_FVN_InspectionLog_Part FOREIGN KEY (SiteCode, PartNo)
                REFERENCES IFS.FVN_InventoryPart (SiteCode, PartNo),
            CONSTRAINT FK_FVN_InspectionLog_MESLot FOREIGN KEY (SiteCode, PartNo, MaterialLotId)
                REFERENCES MES.FVN_MaterialLot (SiteCode, PartNo, MaterialLotId),
            CONSTRAINT CK_FVN_InspectionLog_Qty CHECK (SoLuongTong IS NULL OR SoLuongTong >= 0)
        );
        CREATE INDEX IX_FVN_InspectionLog_InspectionCode
            ON dbo.FVN_InspectionLog (SiteCode, InspectionCode, LogId);
        CREATE INDEX IX_FVN_InspectionLog_ItemCode_CheckedAt
            ON dbo.FVN_InspectionLog (SiteCode, PartNo, CheckedAt DESC);
    END;

    INSERT INTO MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('023', N'PCTP inspection and QR/LOT reconciliation history');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT MigrationVersion, MigrationName
FROM MES.FVN_SchemaMigration
WHERE MigrationVersion = '023';
GO
