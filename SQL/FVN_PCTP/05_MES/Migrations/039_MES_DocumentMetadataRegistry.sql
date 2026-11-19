/*
 Migration 039: metadata tables for the trusted Document Runtime.
 SQL metadata is presentation/configuration only. It does not store executable SQL,
 dynamic procedure names, arbitrary expressions or command implementations.
 C# DocumentRegistry remains the allow-list for module/view/workflow/command bindings.
 SQL Server 2016 compatible. Apply after 038.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 53900, 'Migration 039 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 53901, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '038')
    THROW 53902, 'Migration 038 must be applied before 039.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '039')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'MES.FVN_DocumentDefinition', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentDefinition
        (
            DocumentDefinitionId int IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_FVN_DocumentDefinition PRIMARY KEY,
            ModuleCode varchar(80) NOT NULL,
            HeaderView sysname NOT NULL,
            DetailView sysname NOT NULL,
            WorkflowCode varchar(80) NOT NULL,
            DisplayName nvarchar(160) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_FVN_DocumentDefinition_Active DEFAULT(1),
            ConfigVersion int NOT NULL CONSTRAINT DF_FVN_DocumentDefinition_Version DEFAULT(1),
            UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocumentDefinition_Updated DEFAULT(SYSUTCDATETIME()),
            UpdatedBy nvarchar(100) NOT NULL,
            RowVersion rowversion NOT NULL,
            CONSTRAINT UQ_FVN_DocumentDefinition_Module UNIQUE(ModuleCode)
        );
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentLayout', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentLayout
        (
            DocumentLayoutId bigint IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_FVN_DocumentLayout PRIMARY KEY,
            ModuleCode varchar(80) NOT NULL,
            LayoutCode varchar(80) NOT NULL,
            RegionCode varchar(30) NOT NULL,
            Caption nvarchar(160) NULL,
            SequenceNo int NOT NULL CONSTRAINT DF_FVN_DocumentLayout_Sequence DEFAULT(0),
            Width int NULL,
            Height int NULL,
            IsVisible bit NOT NULL CONSTRAINT DF_FVN_DocumentLayout_Visible DEFAULT(1),
            IsReadOnly bit NOT NULL CONSTRAINT DF_FVN_DocumentLayout_ReadOnly DEFAULT(0),
            LayoutJson nvarchar(max) NULL,
            UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FVN_DocumentLayout_Updated DEFAULT(SYSUTCDATETIME()),
            UpdatedBy nvarchar(100) NOT NULL,
            CONSTRAINT FK_FVN_DocumentLayout_Definition FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDefinition(ModuleCode),
            CONSTRAINT UQ_FVN_DocumentLayout_Code UNIQUE(ModuleCode, LayoutCode),
            CONSTRAINT CK_FVN_DocumentLayout_Json CHECK(LayoutJson IS NULL OR ISJSON(LayoutJson)=1)
        );
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentField', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentField
        (
            DocumentFieldId bigint IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_FVN_DocumentField PRIMARY KEY,
            ModuleCode varchar(80) NOT NULL,
            FieldCode varchar(100) NOT NULL,
            RegionCode varchar(30) NOT NULL,
            Caption nvarchar(160) NOT NULL,
            SequenceNo int NOT NULL CONSTRAINT DF_FVN_DocumentField_Sequence DEFAULT(0),
            Width int NULL,
            DisplayFormat nvarchar(80) NULL,
            EditorCode varchar(40) NOT NULL CONSTRAINT DF_FVN_DocumentField_Editor DEFAULT('TEXT'),
            IsVisible bit NOT NULL CONSTRAINT DF_FVN_DocumentField_Visible DEFAULT(1),
            IsReadOnly bit NOT NULL CONSTRAINT DF_FVN_DocumentField_ReadOnly DEFAULT(0),
            IsRequired bit NOT NULL CONSTRAINT DF_FVN_DocumentField_Required DEFAULT(0),
            LookupCode varchar(80) NULL,
            CONSTRAINT FK_FVN_DocumentField_Definition FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDefinition(ModuleCode),
            CONSTRAINT UQ_FVN_DocumentField_Code UNIQUE(ModuleCode, FieldCode),
            CONSTRAINT CK_FVN_DocumentField_Editor CHECK(EditorCode IN
                ('TEXT','NUMBER','DATE','DATETIME','CHECK','LOOKUP','MEMO','BARCODE','QR'))
        );
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentCommand', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentCommand
        (
            DocumentCommandId bigint IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_FVN_DocumentCommand PRIMARY KEY,
            ModuleCode varchar(80) NOT NULL,
            CommandCode varchar(80) NOT NULL,
            Caption nvarchar(120) NOT NULL,
            SequenceNo int NOT NULL CONSTRAINT DF_FVN_DocumentCommand_Sequence DEFAULT(0),
            IsVisible bit NOT NULL CONSTRAINT DF_FVN_DocumentCommand_Visible DEFAULT(1),
            RequiredStatusCode int NULL,
            CONSTRAINT FK_FVN_DocumentCommand_Definition FOREIGN KEY(ModuleCode)
                REFERENCES MES.FVN_DocumentDefinition(ModuleCode),
            CONSTRAINT UQ_FVN_DocumentCommand_Code UNIQUE(ModuleCode, CommandCode)
        );
    END;

    IF OBJECT_ID(N'MES.FVN_DocumentConfigAudit', N'U') IS NULL
    BEGIN
        CREATE TABLE MES.FVN_DocumentConfigAudit
        (
            ConfigAuditId bigint IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_FVN_DocumentConfigAudit PRIMARY KEY,
            ModuleCode varchar(80) NOT NULL,
            ConfigObject varchar(30) NOT NULL,
            ConfigKey nvarchar(160) NOT NULL,
            ActionCode varchar(20) NOT NULL,
            BeforeValue nvarchar(max) NULL,
            AfterValue nvarchar(max) NULL,
            ActorCode nvarchar(100) NOT NULL,
            OccurredAtUtc datetime2(3) NOT NULL
                CONSTRAINT DF_FVN_DocumentConfigAudit_Date DEFAULT(SYSUTCDATETIME())
        );
        CREATE INDEX IX_FVN_DocumentConfigAudit_ModuleDate
            ON MES.FVN_DocumentConfigAudit(ModuleCode, OccurredAtUtc);
    END;

    INSERT MES.FVN_SchemaMigration(MigrationVersion, MigrationName)
    VALUES ('039', N'Trusted document runtime presentation metadata');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
