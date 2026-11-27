/*
 Migration 066: màn hình layout (Bravo style) cho KẾ HOẠCH SẢN XUẤT và LỆNH SẢN XUẤT, cùng khuôn với COMPONENT_PO (056).

   PRODUCTION_PLAN   Kế hoạch sản xuất theo ca: nút Upload Excel, Kiểm tra, Chốt kế hoạch (FIRM), Hủy.
                     Quyền xem: MES.PLAN_UPLOAD.
   PRODUCTION_ORDER  Lệnh sản xuất: nút workflow lệnh + In tem thùng / In tem palet (+ in lại).
                     Quyền xem: MES.PRODUCTION_EXECUTION.

 Chỉ khai báo view đọc + nguồn dữ liệu đã duyệt + bố cục mặc định. Không thêm thủ tục ghi, không thêm quyền:
 mọi thao tác ghi do dịch vụ C# kiểm quyền / Site và thủ tục SQL (036, 065) kiểm lại trạng thái.
 Workflow plan / order dùng bảng chuẩn dbo.FVN_sys_WorkflowTransitions (MES_PRODUCTION_PLAN / MES_PRODUCTION_ORDER, migration 036).
 Apply after migrations 056, 057 and 065. SQL Server 2016 SP1+.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'FVN_PCTP'
    THROW 56600, 'Migration 066 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL
    THROW 56601, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '065')
    THROW 56602, 'Migration 065 must be applied before 066.', 1;
IF OBJECT_ID(N'MES.FVN_UiScreen', N'U') IS NULL OR OBJECT_ID(N'MES.FVN_UiDataSource', N'U') IS NULL
    THROW 56603, 'UI layout objects are missing. Apply migrations 056 and 057 first.', 1;
IF NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'MES.PLAN_UPLOAD' AND IsActive = 1)
   OR NOT EXISTS (SELECT 1 FROM SEC.FunctionCatalog WHERE FunctionCode = 'MES.PRODUCTION_EXECUTION' AND IsActive = 1)
    THROW 56604, 'Functions MES.PLAN_UPLOAD and MES.PRODUCTION_EXECUTION must exist in SEC.FunctionCatalog.', 1;
GO

/* ---------------------------------------------------------------- read views */
IF OBJECT_ID(N'MES.vw_FVN_UiProductionPlan', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiProductionPlan;
GO
CREATE VIEW MES.vw_FVN_UiProductionPlan
AS
SELECT p.ProductionPlanId, p.SiteCode, p.PlanNo, p.PlanDate, p.SourceFileName, p.Status,
       ISNULL(p.WorkflowStatusCode, CASE p.Status WHEN 'IMPORTED' THEN 0 WHEN 'VALIDATED' THEN 10 WHEN 'FIRM' THEN 20
                                                   WHEN 'CANCELLED' THEN 900 ELSE 0 END) AS WorkflowStatusCode,
       p.CreatedBy,
       (SELECT COUNT(*) FROM MES.FVN_ProductionPlanLine l WHERE l.ProductionPlanId = p.ProductionPlanId) AS LineCount,
       (SELECT SUM(l.PlannedQty) FROM MES.FVN_ProductionPlanLine l WHERE l.ProductionPlanId = p.ProductionPlanId) AS TotalQty
FROM MES.FVN_ProductionPlan p;
GO

IF OBJECT_ID(N'MES.vw_FVN_UiProductionPlanLine', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiProductionPlanLine;
GO
CREATE VIEW MES.vw_FVN_UiProductionPlanLine
AS
SELECT l.ProductionPlanId, l.SiteCode, l.[LineNo] AS [LineNo], l.ProductionDate, l.ShiftCode, l.ProductionLineCode,
       l.ProductPartNo, ip.Description AS ProductDescription, l.PlannedQty, l.UomCode, l.PlannedStartUtc, l.PlannedEndUtc
FROM MES.FVN_ProductionPlanLine l
LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode = l.SiteCode AND ip.PartNo = l.ProductPartNo;
GO

IF OBJECT_ID(N'MES.vw_FVN_UiProductionOrder', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiProductionOrder;
GO
CREATE VIEW MES.vw_FVN_UiProductionOrder
AS
SELECT o.ProductionOrderId, o.SiteCode, o.OrderNo, o.ProductPartNo, ip.Description AS ProductDescription,
       o.ProductionDate, o.ShiftCode, o.ProductionLineCode, o.DepartmentCode, o.PlannedQty, o.CompletedQty, o.UomCode,
       o.Status,
       ISNULL(o.WorkflowStatusCode, CASE o.Status WHEN 'CREATED' THEN 0 WHEN 'RELEASED' THEN 10 WHEN 'RUNNING' THEN 20
                                                  WHEN 'PAUSED' THEN 30 WHEN 'COMPLETED' THEN 40 WHEN 'CLOSED' THEN 50
                                                  WHEN 'CANCELLED' THEN 900 ELSE 0 END) AS WorkflowStatusCode,
       ISNULL((SELECT SUM(l.Qty) FROM MES.FVN_ProductionOrderLabel l
                WHERE l.ProductionOrderId = o.ProductionOrderId AND l.LabelKind = 'CARTON'), 0) AS CartonLabelQty,
       ISNULL((SELECT SUM(l.Qty) FROM MES.FVN_ProductionOrderLabel l
                WHERE l.ProductionOrderId = o.ProductionOrderId AND l.LabelKind = 'PALLET'), 0) AS PalletLabelQty,
       o.ReleasedBy
FROM MES.FVN_ProductionOrder o
LEFT JOIN IFS.FVN_InventoryPart ip ON ip.SiteCode = o.SiteCode AND ip.PartNo = o.ProductPartNo;
GO

IF OBJECT_ID(N'MES.vw_FVN_UiProductionOrderLabel', N'V') IS NOT NULL DROP VIEW MES.vw_FVN_UiProductionOrderLabel;
GO
CREATE VIEW MES.vw_FVN_UiProductionOrderLabel
AS
SELECT l.ProductionOrderId, l.SiteCode, l.LabelKind, l.Seq, l.LabelNo, l.Qty, l.PalletCode, l.CreatedAtUtc,
       (SELECT COUNT(*) FROM MES.FVN_ProductionOrderLabelHistory h WHERE h.ProductionOrderLabelId = l.ProductionOrderLabelId) AS PrintCount
FROM MES.FVN_ProductionOrderLabel l;
GO

/* ---------------------------------------------------------------- data sources + screens */
BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '066')
    BEGIN
        INSERT MES.FVN_UiDataSource (SourceCode, ViewName, DisplayName, ViewFunctionCode, SiteColumn, ApprovedBy)
        SELECT v.SourceCode, v.ViewName, v.DisplayName, v.FunctionCode, N'SiteCode', N'MIGRATION_066'
        FROM (VALUES ('PRODUCTION_PLAN',        N'MES.vw_FVN_UiProductionPlan',       N'Kế hoạch sản xuất',          'MES.PLAN_UPLOAD'),
                     ('PRODUCTION_PLAN_LINE',   N'MES.vw_FVN_UiProductionPlanLine',   N'Dòng kế hoạch sản xuất',     'MES.PLAN_UPLOAD'),
                     ('PRODUCTION_ORDER',       N'MES.vw_FVN_UiProductionOrder',      N'Lệnh sản xuất',              'MES.PRODUCTION_EXECUTION'),
                     ('PRODUCTION_ORDER_LABEL', N'MES.vw_FVN_UiProductionOrderLabel', N'Tem của lệnh sản xuất',      'MES.PRODUCTION_EXECUTION')
             ) v(SourceCode, ViewName, DisplayName, FunctionCode)
        WHERE NOT EXISTS (SELECT 1 FROM MES.FVN_UiDataSource s WHERE s.SourceCode = v.SourceCode);

        /* ---- PRODUCTION_PLAN ---- */
        IF NOT EXISTS (SELECT 1 FROM MES.FVN_UiScreen WHERE ScreenCode = 'PRODUCTION_PLAN')
        BEGIN
            INSERT MES.FVN_UiScreen (ScreenCode, Title, SourceCode, DefaultSortColumn, DefaultSortDesc, GroupColumn, PageSize, UpdatedBy)
            VALUES ('PRODUCTION_PLAN', N'Kế hoạch sản xuất', 'PRODUCTION_PLAN', 'PlanDate', 1, 'Status', 200, N'MIGRATION_066');

            INSERT MES.FVN_UiScreenColumn (ScreenCode, RegionCode, ColumnName, Caption, SequenceNo, Width, Align, DisplayFormat, IsVisible)
            VALUES
            ('PRODUCTION_PLAN', 'MAIN', 'PlanNo',            N'Số kế hoạch',    1, 140, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'MAIN', 'PlanDate',          N'Ngày kế hoạch',  2,  95, 'LEFT',  'dd/MM/yyyy', 1),
            ('PRODUCTION_PLAN', 'MAIN', 'LineCount',         N'Số dòng',        3,  70, 'RIGHT', 'N0', 1),
            ('PRODUCTION_PLAN', 'MAIN', 'TotalQty',          N'Tổng SL',        4,  90, 'RIGHT', 'N0', 1),
            ('PRODUCTION_PLAN', 'MAIN', 'SourceFileName',    N'File upload',    5, 220, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'MAIN', 'CreatedBy',         N'Người upload',   6, 110, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'MAIN', 'Status',            N'Trạng thái',     7, 110, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'MAIN', 'ProductionPlanId',  N'Id',             8,  72, 'RIGHT', NULL, 1),
            ('PRODUCTION_PLAN', 'MAIN', 'SiteCode',          N'Site',           9,  60, 'LEFT',  NULL, 0),
            ('PRODUCTION_PLAN', 'MAIN', 'WorkflowStatusCode',N'Mã trạng thái', 10,  60, 'RIGHT', NULL, 0),
            ('PRODUCTION_PLAN', 'LINES', 'LineNo',            N'Dòng',           0,  50, 'RIGHT', NULL, 1),
            ('PRODUCTION_PLAN', 'LINES', 'ProductionDate',    N'Ngày SX',        1,  90, 'LEFT',  'dd/MM/yyyy', 1),
            ('PRODUCTION_PLAN', 'LINES', 'ShiftCode',         N'Ca',             2,  50, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'LINES', 'ProductionLineCode',N'Chuyền',         3,  80, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'LINES', 'ProductPartNo',     N'Mã hàng',        4, 150, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'LINES', 'ProductDescription',N'Tên hàng',       5, 220, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'LINES', 'PlannedQty',        N'SL kế hoạch',    6,  90, 'RIGHT', 'N0', 1),
            ('PRODUCTION_PLAN', 'LINES', 'UomCode',           N'Đvt',            7,  50, 'LEFT',  NULL, 1),
            ('PRODUCTION_PLAN', 'LINES', 'ProductionPlanId',  N'Plan Id',        8,  60, 'RIGHT', NULL, 0);

            INSERT MES.FVN_UiScreenChild (ScreenCode, ChildCode, TabCaption, SourceCode, ParentColumn, ChildColumn, SortColumn, SortDesc, SequenceNo)
            VALUES ('PRODUCTION_PLAN', 'LINES', N'Dòng kế hoạch', 'PRODUCTION_PLAN_LINE', 'ProductionPlanId', 'ProductionPlanId', 'LineNo', 0, 1);

            INSERT MES.FVN_UiScreenFilter (ScreenCode, FilterCode, Caption, ColumnName, OperatorCode, TokenCode, LiteralValue, SequenceNo, IsDefault)
            VALUES ('PRODUCTION_PLAN', 'OPEN',      N'Đang xử lý',           'Status',   'NE', NULL, N'CANCELLED', 1, 1),
                   ('PRODUCTION_PLAN', 'CANCELLED', N'Đã hủy',               'Status',   'EQ', NULL, N'CANCELLED', 2, 0),
                   ('PRODUCTION_PLAN', 'TODAY',     N'Kế hoạch trong ngày',  'PlanDate', 'GE', 'TODAY',       NULL, 3, 0),
                   ('PRODUCTION_PLAN', 'MONTH',     N'Kế hoạch trong tháng', 'PlanDate', 'GE', 'MONTH_START', NULL, 4, 0),
                   ('PRODUCTION_PLAN', 'YEAR',      N'Kế hoạch trong năm',   'PlanDate', 'GE', 'YEAR_START',  NULL, 5, 0);
        END;

        /* ---- PRODUCTION_ORDER ---- */
        IF NOT EXISTS (SELECT 1 FROM MES.FVN_UiScreen WHERE ScreenCode = 'PRODUCTION_ORDER')
        BEGIN
            INSERT MES.FVN_UiScreen (ScreenCode, Title, SourceCode, DefaultSortColumn, DefaultSortDesc, GroupColumn, PageSize, UpdatedBy)
            VALUES ('PRODUCTION_ORDER', N'Lệnh sản xuất (in tem)', 'PRODUCTION_ORDER', 'ProductionDate', 1, 'Status', 200, N'MIGRATION_066');

            INSERT MES.FVN_UiScreenColumn (ScreenCode, RegionCode, ColumnName, Caption, SequenceNo, Width, Align, DisplayFormat, IsVisible)
            VALUES
            ('PRODUCTION_ORDER', 'MAIN', 'OrderNo',             N'Số lệnh',          1, 130, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'ProductionDate',      N'Ngày SX',          2,  90, 'LEFT',  'dd/MM/yyyy', 1),
            ('PRODUCTION_ORDER', 'MAIN', 'ShiftCode',           N'Ca',               3,  50, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'ProductionLineCode',  N'Chuyền',           4,  80, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'ProductPartNo',       N'Mã hàng',          5, 150, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'ProductDescription',  N'Tên hàng',         6, 220, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'PlannedQty',          N'SL lệnh',          7,  80, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'MAIN', 'CompletedQty',        N'SL hoàn thành',    8,  90, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'MAIN', 'CartonLabelQty',      N'SL đã tạo tem thùng', 9, 120, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'MAIN', 'PalletLabelQty',      N'SL đã tạo tem palet', 10, 120, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'MAIN', 'Status',              N'Trạng thái',      11, 110, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'ProductionOrderId',   N'Id',              12,  72, 'RIGHT', NULL, 1),
            ('PRODUCTION_ORDER', 'MAIN', 'SiteCode',            N'Site',            13,  60, 'LEFT',  NULL, 0),
            ('PRODUCTION_ORDER', 'MAIN', 'DepartmentCode',      N'Bộ phận',         14,  70, 'LEFT',  NULL, 0),
            ('PRODUCTION_ORDER', 'MAIN', 'WorkflowStatusCode',  N'Mã trạng thái',   15,  60, 'RIGHT', NULL, 0),
            ('PRODUCTION_ORDER', 'LABELS', 'LabelKind',         N'Loại tem',         1,  80, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'LABELS', 'Seq',               N'STT',              2,  50, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'LABELS', 'LabelNo',           N'Số tem',           3, 240, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'LABELS', 'Qty',               N'SL',               4,  70, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'LABELS', 'PalletCode',        N'Mã palet',         5, 150, 'LEFT',  NULL, 1),
            ('PRODUCTION_ORDER', 'LABELS', 'PrintCount',        N'Số lần in',        6,  70, 'RIGHT', 'N0', 1),
            ('PRODUCTION_ORDER', 'LABELS', 'ProductionOrderId', N'Order Id',         7,  60, 'RIGHT', NULL, 0);

            INSERT MES.FVN_UiScreenChild (ScreenCode, ChildCode, TabCaption, SourceCode, ParentColumn, ChildColumn, SortColumn, SortDesc, SequenceNo)
            VALUES ('PRODUCTION_ORDER', 'LABELS', N'Danh sách tem', 'PRODUCTION_ORDER_LABEL', 'ProductionOrderId', 'ProductionOrderId', 'Seq', 0, 1);

            INSERT MES.FVN_UiScreenFilter (ScreenCode, FilterCode, Caption, ColumnName, OperatorCode, TokenCode, LiteralValue, SequenceNo, IsDefault)
            VALUES ('PRODUCTION_ORDER', 'OPEN',   N'Đang xử lý',           'Status',         'NE', NULL, N'CLOSED', 1, 1),
                   ('PRODUCTION_ORDER', 'CLOSED', N'Lệnh đã đóng',         'Status',         'EQ', NULL, N'CLOSED', 2, 0),
                   ('PRODUCTION_ORDER', 'TODAY',  N'Lệnh trong ngày',      'ProductionDate', 'GE', 'TODAY',       NULL, 3, 0),
                   ('PRODUCTION_ORDER', 'MONTH',  N'Lệnh trong tháng',     'ProductionDate', 'GE', 'MONTH_START', NULL, 4, 0),
                   ('PRODUCTION_ORDER', 'YEAR',   N'Lệnh trong năm',       'ProductionDate', 'GE', 'YEAR_START',  NULL, 5, 0);
        END;

        INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
        VALUES ('066', N'PCTP UI Layout: PRODUCTION_PLAN and PRODUCTION_ORDER screens (upload, workflow, label print)');
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '066';
GO