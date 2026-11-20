/*
 Migration 050: read views and approved presentation metadata for the COMPONENT_DEMAND document.
 Layer 1 (trusted registry) lives in C#: PCTP/Modules/MesPlanning/Documents/ComponentDemandDefinition.cs.
 This migration seeds layer 2 so that it matches the registry exactly; the Document Runtime
 fails closed if they ever diverge. Layer 3 (enforcement) remains in MES.usp_FVN_ComponentIssue_Post.
 The views are read-only projections; the application always filters them by authorized SiteCode.
 SQL Server 2016 compatible. Apply after 049.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 54001, 'Migration 050 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 54002, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '049')
    THROW 54003, 'Migration 049 must be applied before 050.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '039')
    THROW 54004, 'Migration 039 (document metadata tables) must be applied before 050.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentDefinition', N'U') IS NULL
    THROW 54005, 'Missing MES.FVN_DocumentDefinition. Re-run migration 039 to repair document metadata tables.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentField', N'U') IS NULL
    THROW 54006, 'Missing MES.FVN_DocumentField. Re-run migration 039 to repair document metadata tables.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentCommand', N'U') IS NULL
    THROW 54007, 'Missing MES.FVN_DocumentCommand. Re-run migration 039 to repair document metadata tables.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentLayout', N'U') IS NULL
    THROW 54008, 'Missing MES.FVN_DocumentLayout. Re-run migration 039 to repair document metadata tables.', 1;
IF OBJECT_ID(N'MES.FVN_DocumentConfigAudit', N'U') IS NULL
    THROW 54009, 'Missing MES.FVN_DocumentConfigAudit. Re-run migration 039 to repair document metadata tables.', 1;
IF EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '050')
    RETURN;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'MES.vw_FVN_ComponentDemandHeader', N'V') IS NOT NULL
        DROP VIEW MES.vw_FVN_ComponentDemandHeader;
    EXEC (N'CREATE VIEW MES.vw_FVN_ComponentDemandHeader AS
        SELECT h.ComponentDemandId, h.SiteCode, h.ProductionPlanLineId, h.ProductPartNo, h.PlannedQty,
               h.BomEngChgLevel, h.BomTypeDb, h.AlternativeNo, h.Status, h.CreatedAtUtc, h.CreatedBy
        FROM MES.FVN_ComponentDemandHeader h');

    IF OBJECT_ID(N'MES.vw_FVN_ComponentDemandLine', N'V') IS NOT NULL
        DROP VIEW MES.vw_FVN_ComponentDemandLine;
    EXEC (N'CREATE VIEW MES.vw_FVN_ComponentDemandLine AS
        SELECT l.ComponentDemandLineId, l.ComponentDemandId, l.SiteCode, l.ComponentPartNo,
               l.RequiredQty, l.PartAvailableQty, l.NetRequiredQty, l.IssuedQty, l.UomCode, l.OperationNo
        FROM MES.FVN_ComponentDemandLine l');

    -- A document legitimately exposes the same column (key, site) in header and detail views, so a
    -- field is unique per (module, region, field) rather than per (module, field).
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_FVN_DocumentField_Code'
               AND parent_object_id = OBJECT_ID(N'MES.FVN_DocumentField'))
        ALTER TABLE MES.FVN_DocumentField DROP CONSTRAINT UQ_FVN_DocumentField_Code;
    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_FVN_DocumentField_Region_Code'
                   AND parent_object_id = OBJECT_ID(N'MES.FVN_DocumentField'))
        ALTER TABLE MES.FVN_DocumentField
            ADD CONSTRAINT UQ_FVN_DocumentField_Region_Code UNIQUE (ModuleCode, RegionCode, FieldCode);

    IF NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentDefinition WHERE ModuleCode = 'COMPONENT_DEMAND')
        INSERT MES.FVN_DocumentDefinition
            (ModuleCode, HeaderView, DetailView, WorkflowCode, DisplayName, UpdatedBy)
        VALUES ('COMPONENT_DEMAND', 'MES.vw_FVN_ComponentDemandHeader', 'MES.vw_FVN_ComponentDemandLine',
                'NONE', N'Yêu cầu linh kiện', N'MIGRATION_050');

    DECLARE @Field TABLE
    (
        FieldCode varchar(100), RegionCode varchar(30), Caption nvarchar(160), SequenceNo int,
        Width int NULL, DisplayFormat nvarchar(80) NULL, EditorCode varchar(40), IsVisible bit
    );
    INSERT @Field VALUES
     ('ComponentDemandId','HEADER',N'Mã yêu cầu',10,110,'0','NUMBER',1),
     ('SiteCode','HEADER',N'Site',20,80,NULL,'TEXT',1),
     ('ProductionPlanLineId','HEADER',N'Dòng kế hoạch',30,120,'0','NUMBER',1),
     ('ProductPartNo','HEADER',N'Mã sản phẩm',40,160,NULL,'TEXT',1),
     ('PlannedQty','HEADER',N'SL kế hoạch',50,110,'#,##0.######','NUMBER',1),
     ('BomEngChgLevel','HEADER',N'Mức ECO BOM',60,100,NULL,'TEXT',1),
     ('BomTypeDb','HEADER',N'Loại BOM',70,100,NULL,'TEXT',1),
     ('AlternativeNo','HEADER',N'BOM thay thế',80,100,NULL,'TEXT',1),
     ('Status','HEADER',N'Trạng thái',90,100,NULL,'TEXT',1),
     ('CreatedAtUtc','HEADER',N'Tạo lúc (UTC)',100,150,'dd/MM/yyyy HH:mm:ss','DATETIME',1),
     ('CreatedBy','HEADER',N'Người tạo',110,120,NULL,'TEXT',1),
     ('ComponentDemandLineId','DETAIL',N'Mã dòng',10,100,'0','NUMBER',1),
     ('ComponentDemandId','DETAIL',N'Mã yêu cầu',20,100,'0','NUMBER',0),
     ('SiteCode','DETAIL',N'Site',30,80,NULL,'TEXT',0),
     ('ComponentPartNo','DETAIL',N'Mã linh kiện',40,170,NULL,'TEXT',1),
     ('RequiredQty','DETAIL',N'SL yêu cầu',50,110,'#,##0.######','NUMBER',1),
     ('PartAvailableQty','DETAIL',N'SL PART khả dụng',60,130,'#,##0.######','NUMBER',1),
     ('NetRequiredQty','DETAIL',N'SL cần xuất',70,110,'#,##0.######','NUMBER',1),
     ('IssuedQty','DETAIL',N'SL đã cấp',80,110,'#,##0.######','NUMBER',1),
     ('UomCode','DETAIL',N'ĐVT',90,70,NULL,'TEXT',1),
     ('OperationNo','DETAIL',N'Công đoạn',100,90,NULL,'TEXT',1);

    INSERT MES.FVN_DocumentField
        (ModuleCode, FieldCode, RegionCode, Caption, SequenceNo, Width, DisplayFormat, EditorCode, IsVisible, IsReadOnly)
    SELECT 'COMPONENT_DEMAND', f.FieldCode, f.RegionCode, f.Caption, f.SequenceNo, f.Width, f.DisplayFormat, f.EditorCode, f.IsVisible, 1
    FROM @Field f
    WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentField x
                      WHERE x.ModuleCode = 'COMPONENT_DEMAND' AND x.RegionCode = f.RegionCode AND x.FieldCode = f.FieldCode);

    IF NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentCommand WHERE ModuleCode = 'COMPONENT_DEMAND' AND CommandCode = 'ISSUE_COMPONENT')
        INSERT MES.FVN_DocumentCommand (ModuleCode, CommandCode, Caption, SequenceNo, IsVisible)
        VALUES ('COMPONENT_DEMAND', 'ISSUE_COMPONENT', N'Xuất linh kiện', 10, 1);

    IF NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentLayout WHERE ModuleCode = 'COMPONENT_DEMAND' AND LayoutCode = 'HEADER')
        INSERT MES.FVN_DocumentLayout (ModuleCode, LayoutCode, RegionCode, Caption, SequenceNo, IsVisible, IsReadOnly, UpdatedBy)
        VALUES ('COMPONENT_DEMAND', 'HEADER', 'HEADER', N'Thông tin yêu cầu linh kiện', 10, 1, 1, N'MIGRATION_050');
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_DocumentLayout WHERE ModuleCode = 'COMPONENT_DEMAND' AND LayoutCode = 'DETAIL')
        INSERT MES.FVN_DocumentLayout (ModuleCode, LayoutCode, RegionCode, Caption, SequenceNo, IsVisible, IsReadOnly, UpdatedBy)
        VALUES ('COMPONENT_DEMAND', 'DETAIL', 'DETAIL', N'Danh sách linh kiện', 20, 1, 1, N'MIGRATION_050');

    INSERT MES.FVN_DocumentConfigAudit (ModuleCode, ConfigObject, ConfigKey, ActionCode, AfterValue, ActorCode)
    VALUES ('COMPONENT_DEMAND', 'DEFINITION', N'COMPONENT_DEMAND', 'SEED',
            N'Initial approved metadata matching the trusted C# ComponentDemandDefinition', N'MIGRATION_050');

    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('050', N'COMPONENT_DEMAND document read views and approved presentation metadata');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
