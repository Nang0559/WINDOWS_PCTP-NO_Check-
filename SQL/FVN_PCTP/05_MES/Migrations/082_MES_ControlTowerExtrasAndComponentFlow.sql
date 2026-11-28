/*
 Migration 082: read-only procedures behind the two Control Tower screens.

   MES.usp_FVN_ProductionFlow_Extra
       NG quantity and planned end date per production plan line. MES.usp_FVN_ProductionFlow_Dashboard (047/049) is NOT
       changed; the application merges these two columns into the rows that the scoped dashboard already returned,
       so no row outside the department scope can appear.

   MES.usp_FVN_ComponentFlow_Grid
       One row per component PO line / component LOT with the stage reached:
       PO -> LABEL -> RECEIPT -> REQUEST -> ISSUE -> STOCK.

   MES.usp_FVN_ComponentFlow_Summary
       One row with the demand / issue / stock figures that cannot be derived from the grid
       (open production demands, short components, posted issues, PART + WIP on hand).

 Nothing is written. QC / packaging / other component requests are not modelled in the schema, so only production
 demand (MES.FVN_ComponentDemandHeader) is counted as a request.
 Written without CREATE OR ALTER (same convention as 049 / 080 / 081).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF DB_NAME() <> N'FVN_PCTP' THROW 55200, 'Migration 082 may run only in FVN_PCTP.', 1;
IF OBJECT_ID(N'MES.FVN_SchemaMigration', N'U') IS NULL THROW 55201, 'MES migration registry is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '049')
    THROW 55202, 'Migration 049 must be applied before 082.', 1;
IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '063')
    THROW 55203, 'Migration 063 must be applied before 082.', 1;
GO

IF OBJECT_ID(N'MES.usp_FVN_ProductionFlow_Extra', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_ProductionFlow_Extra;
GO
CREATE PROCEDURE MES.usp_FVN_ProductionFlow_Extra
    @SiteCode nvarchar(30),
    @FromDate date = NULL,
    @ToDate   date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(LTRIM(RTRIM(@SiteCode)), N'') IS NULL THROW 55210, 'SiteCode is required.', 1;

    SELECT l.ProductionPlanLineId,
           ISNULL(lot.NgQty, 0) AS NgQty,
           CONVERT(date, l.PlannedEndUtc) AS PlannedEndDate
    FROM MES.FVN_ProductionPlanLine l
    JOIN MES.FVN_ProductionPlan p ON p.ProductionPlanId = l.ProductionPlanId
    OUTER APPLY
    (
        SELECT SUM(ISNULL(pl.FinalNgQty, pl.NgQty)) AS NgQty
        FROM MES.FVN_ProductionOrder o
        JOIN MES.FVN_ProductionLot pl ON pl.ProductionOrderId = o.ProductionOrderId
        WHERE o.ProductionPlanLineId = l.ProductionPlanLineId
    ) lot
    WHERE p.SiteCode = @SiteCode
      AND (@FromDate IS NULL OR ISNULL(l.ProductionDate, p.PlanDate) >= @FromDate)
      AND (@ToDate   IS NULL OR ISNULL(l.ProductionDate, p.PlanDate) <= @ToDate);
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_ComponentFlow_Grid', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_ComponentFlow_Grid;
GO
CREATE PROCEDURE MES.usp_FVN_ComponentFlow_Grid
    @SiteCode nvarchar(30),
    @FromDate date = NULL,
    @ToDate   date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(LTRIM(RTRIM(@SiteCode)), N'') IS NULL THROW 55220, 'SiteCode is required.', 1;

    ;WITH Stock AS
    (
        -- Tồn theo MaterialLot và khu (PART / WIP); cùng cách nối với 033 / ComponentStockRepository.
        SELECT b.SiteCode, b.MaterialLotId,
               SUM(CASE WHEN ar.AreaCode = 'PART' THEN b.OnHandQty ELSE 0 END) AS PartOnHandQty,
               SUM(CASE WHEN ar.AreaCode = 'WIP'  THEN b.OnHandQty ELSE 0 END) AS WipOnHandQty
        FROM MES.FVN_InventoryBalance b
        JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode = b.SiteCode AND sm.LocationNo = b.LocationNo
         AND sm.IsPrimary = 1 AND sm.IsActive = 1
        JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode = sm.SiteCode AND ar.SlotId = sm.SlotId AND ar.IsActive = 1
        WHERE b.SiteCode = @SiteCode AND b.StockState = 'AVAILABLE'
        GROUP BY b.SiteCode, b.MaterialLotId
    ),
    Issued AS
    (
        SELECT il.MaterialLotId, SUM(il.Quantity) AS IssuedToWipQty, MAX(i.IssuedAtUtc) AS LastIssuedAtUtc
        FROM MES.FVN_ComponentIssueLine il
        JOIN MES.FVN_ComponentIssue i ON i.ComponentIssueId = il.ComponentIssueId AND i.Status = 'POSTED'
        WHERE i.SiteCode = @SiteCode
        GROUP BY il.MaterialLotId
    ),
    OpenDemand AS
    (
        -- Nhu cầu sản xuất còn thiếu cấp theo PartNo.
        SELECT h.SiteCode, dl.ComponentPartNo AS PartNo,
               SUM(CASE WHEN dl.NetRequiredQty - dl.IssuedQty > 0 THEN dl.NetRequiredQty - dl.IssuedQty ELSE 0 END) AS OutstandingQty
        FROM MES.FVN_ComponentDemandHeader h
        JOIN MES.FVN_ComponentDemandLine dl ON dl.ComponentDemandId = h.ComponentDemandId
        WHERE h.SiteCode = @SiteCode AND h.Status IN ('OPEN', 'PARTIAL')
        GROUP BY h.SiteCode, dl.ComponentPartNo
    ),
    Base AS
    (
        SELECT po.PurchaseOrderId, po.PurchaseOrderNo, po.SiteCode, po.Status AS PoStatus, po.OrderDate,
               pol.PurchaseOrderLineId, pol.[LineNo], pol.PartNo, pol.PartDescription, pol.UnitCode,
               pol.OrderQty, pol.ReceivedQty AS PoLineReceivedQty,
               cl.ComponentLotId, cl.LotNo, cl.Status AS LotStatus,
               cl.IssuedQty AS LotQty, cl.ReceivedQty AS LotReceivedQty, cl.MaterialLotId,
               CONVERT(bit, CASE WHEN cl.ComponentLotId IS NOT NULL AND EXISTS
                   (SELECT 1 FROM MES.FVN_ComponentLotLabelHistory y
                    WHERE y.ComponentLotId = cl.ComponentLotId AND y.ActionCode = 'INITIAL_PRINT') THEN 1 ELSE 0 END) AS IsPrinted,
               ISNULL(s.PartOnHandQty, 0) AS PartOnHandQty,
               ISNULL(s.WipOnHandQty, 0)  AS WipOnHandQty,
               ISNULL(iss.IssuedToWipQty, 0) AS IssuedToWipQty,
               ISNULL(od.OutstandingQty, 0)  AS DemandOutstandingQty,
               rc.LastReceivedAtUtc, iss.LastIssuedAtUtc
        FROM MES.FVN_ComponentPurchaseOrder po
        JOIN MES.FVN_ComponentPurchaseOrderLine pol ON pol.PurchaseOrderId = po.PurchaseOrderId
        LEFT JOIN MES.FVN_ComponentLot cl ON cl.PurchaseOrderLineId = pol.PurchaseOrderLineId AND cl.Status <> 'CANCELLED'
        LEFT JOIN Stock s ON s.SiteCode = po.SiteCode AND s.MaterialLotId = cl.MaterialLotId
        LEFT JOIN Issued iss ON iss.MaterialLotId = cl.MaterialLotId
        LEFT JOIN OpenDemand od ON od.SiteCode = po.SiteCode AND od.PartNo = pol.PartNo
        OUTER APPLY
        (
            SELECT MAX(r.ReceivedAtUtc) AS LastReceivedAtUtc
            FROM MES.FVN_ComponentReceiptLine r WHERE r.ComponentLotId = cl.ComponentLotId
        ) rc
        WHERE po.SiteCode = @SiteCode
          AND po.Status <> 'CANCELLED'
          AND (@FromDate IS NULL OR po.OrderDate >= @FromDate)
          AND (@ToDate   IS NULL OR po.OrderDate <= @ToDate)
    ),
    Staged AS
    (
        SELECT b.*,
               CASE
                   WHEN b.ComponentLotId IS NULL THEN N'PO'
                   WHEN b.IssuedToWipQty > 0 AND b.PartOnHandQty <= 0 THEN N'STOCK'
                   WHEN b.IssuedToWipQty > 0 THEN N'ISSUE'
                   WHEN b.LotReceivedQty > 0 AND b.DemandOutstandingQty > 0 THEN N'REQUEST'
                   WHEN b.LotReceivedQty > 0 THEN N'RECEIPT'
                   ELSE N'LABEL'
               END AS FlowStage
        FROM Base b
    )
    SELECT st.PurchaseOrderId, st.PurchaseOrderNo, st.PurchaseOrderLineId, st.ComponentLotId, st.MaterialLotId,
           CASE WHEN st.LotNo IS NULL THEN st.PurchaseOrderNo + N' / ' + CONVERT(nvarchar(10), st.[LineNo])
                ELSE st.PurchaseOrderNo + N' / ' + st.LotNo END AS PoLotKey,
           st.LotNo, st.PartNo, st.PartDescription, st.SiteCode,
           st.PoStatus, st.LotStatus, st.IsPrinted,
           CASE WHEN st.ComponentLotId IS NULL THEN st.OrderQty ELSE st.LotQty END AS Qty,
           ISNULL(st.UnitCode, N'pcs') AS UnitCode,
           CASE WHEN st.ComponentLotId IS NULL THEN st.PoLineReceivedQty ELSE st.LotReceivedQty END AS ReceivedQty,
           st.PartOnHandQty, st.WipOnHandQty, st.IssuedToWipQty, st.DemandOutstandingQty,
           CONVERT(date, COALESCE(st.LastIssuedAtUtc, st.LastReceivedAtUtc, CONVERT(datetime2(3), st.OrderDate))) AS TransactionDate,
           st.FlowStage,
           CASE st.FlowStage
               WHEN N'PO'      THEN N'Nhà cung cấp → PO'
               WHEN N'LABEL'   THEN N'Tem thùng / pallet'
               WHEN N'RECEIPT' THEN N'Nhập → Kho PART'
               WHEN N'REQUEST' THEN N'Kho PART → Yêu cầu SX'
               WHEN N'ISSUE'   THEN N'Kho PART → WIP'
               ELSE N'WIP / Sản xuất' END AS LocationText,
           CASE
               WHEN st.ComponentLotId IS NULL THEN N'Đang chuẩn bị'
               WHEN st.IssuedToWipQty > 0 AND st.PartOnHandQty <= 0 THEN N'Đã xuất đủ'
               WHEN st.IssuedToWipQty > 0 THEN N'Đã xuất một phần'
               WHEN st.LotReceivedQty >= st.LotQty THEN N'Đã nhập kho'
               WHEN st.LotReceivedQty > 0 THEN N'Nhập một phần'
               WHEN st.IsPrinted = 1 THEN N'Đã in tem, chờ nhập'
               ELSE N'Chờ in tem' END AS MaterialStatus,
           CASE
               WHEN st.LotStatus = 'BLOCKED' THEN N'LOT đang bị khoá'
               WHEN st.ComponentLotId IS NOT NULL AND st.LotReceivedQty > 0 AND st.LotReceivedQty < st.LotQty
                    THEN N'Nhận thiếu so với tem'
               WHEN st.ComponentLotId IS NOT NULL AND st.LotReceivedQty = 0 AND st.IsPrinted = 0
                    AND st.PoStatus IN ('RELEASED', 'PARTIALLY_RECEIVED') THEN N'Chờ in tem'
               WHEN st.LotReceivedQty > 0 AND st.IssuedToWipQty = 0 AND st.DemandOutstandingQty > 0
                    AND st.PartOnHandQty < st.DemandOutstandingQty THEN N'Thiếu linh kiện cho yêu cầu SX'
               WHEN st.WipOnHandQty > 0 THEN N'Tồn WIP chưa tiêu hao'
               ELSE NULL END AS Warning,
           CASE
               WHEN st.PoStatus IN ('DRAFT', 'VALIDATED') THEN N'Chuẩn bị'
               WHEN st.PoStatus = 'CLOSED' THEN N'Hoàn thành'
               WHEN st.LotStatus = 'BLOCKED' THEN N'Tạm dừng'
               WHEN st.ComponentLotId IS NOT NULL AND st.LotReceivedQty > 0 AND st.IssuedToWipQty > 0 AND st.PartOnHandQty <= 0
                    AND st.WipOnHandQty <= 0 THEN N'Hoàn thành'
               WHEN st.IssuedToWipQty > 0 THEN N'Đang chạy'
               WHEN st.ComponentLotId IS NOT NULL AND st.LotReceivedQty > 0 THEN N'Đang chạy'
               ELSE N'Chờ xử lý' END AS StatusText
    FROM Staged st
    ORDER BY st.OrderDate DESC, st.PurchaseOrderNo, st.[LineNo], st.LotNo;
END;
GO

IF OBJECT_ID(N'MES.usp_FVN_ComponentFlow_Summary', N'P') IS NOT NULL DROP PROCEDURE MES.usp_FVN_ComponentFlow_Summary;
GO
CREATE PROCEDURE MES.usp_FVN_ComponentFlow_Summary
    @SiteCode nvarchar(30),
    @FromDate date = NULL,
    @ToDate   date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(LTRIM(RTRIM(@SiteCode)), N'') IS NULL THROW 55230, 'SiteCode is required.', 1;

    DECLARE @OpenDemands int = 0, @ShortLines int = 0, @DemandsReady int = 0;
    DECLARE @Issues int = 0, @PartQty decimal(19,6) = 0, @WipQty decimal(19,6) = 0;

    ;WITH D AS
    (
        SELECT h.ComponentDemandId,
               SUM(CASE WHEN dl.NetRequiredQty - dl.IssuedQty > 0 THEN 1 ELSE 0 END) AS OpenLines,
               SUM(CASE WHEN dl.NetRequiredQty - dl.IssuedQty > 0
                         AND dl.PartAvailableQty < dl.NetRequiredQty - dl.IssuedQty THEN 1 ELSE 0 END) AS ShortLines
        FROM MES.FVN_ComponentDemandHeader h
        JOIN MES.FVN_ComponentDemandLine dl ON dl.ComponentDemandId = h.ComponentDemandId
        JOIN MES.FVN_ProductionPlanLine pl ON pl.ProductionPlanLineId = h.ProductionPlanLineId
        JOIN MES.FVN_ProductionPlan pp ON pp.ProductionPlanId = pl.ProductionPlanId
        WHERE h.SiteCode = @SiteCode AND h.Status IN ('OPEN', 'PARTIAL')
          AND (@FromDate IS NULL OR ISNULL(pl.ProductionDate, pp.PlanDate) >= @FromDate)
          AND (@ToDate   IS NULL OR ISNULL(pl.ProductionDate, pp.PlanDate) <= @ToDate)
        GROUP BY h.ComponentDemandId
    )
    SELECT @OpenDemands = ISNULL(SUM(CASE WHEN OpenLines > 0 THEN 1 ELSE 0 END), 0),
           @ShortLines  = ISNULL(SUM(CASE WHEN ShortLines > 0 THEN 1 ELSE 0 END), 0),
           @DemandsReady = ISNULL(SUM(CASE WHEN OpenLines > 0 AND ShortLines = 0 THEN 1 ELSE 0 END), 0)
    FROM D;

    SELECT @Issues = COUNT_BIG(*)
    FROM MES.FVN_ComponentIssue i
    WHERE i.SiteCode = @SiteCode AND i.Status = 'POSTED'
      AND (@FromDate IS NULL OR CONVERT(date, i.IssuedAtUtc) >= @FromDate)
      AND (@ToDate   IS NULL OR CONVERT(date, i.IssuedAtUtc) <= @ToDate);

    SELECT @PartQty = ISNULL(SUM(CASE WHEN ar.AreaCode = 'PART' THEN b.OnHandQty ELSE 0 END), 0),
           @WipQty  = ISNULL(SUM(CASE WHEN ar.AreaCode = 'WIP'  THEN b.OnHandQty ELSE 0 END), 0)
    FROM MES.FVN_InventoryBalance b
    JOIN dbo.FVN_SlotMesLocation sm ON sm.SiteCode = b.SiteCode AND sm.LocationNo = b.LocationNo
     AND sm.IsPrimary = 1 AND sm.IsActive = 1
    JOIN dbo.FVN_InventoryAreaSlot ar ON ar.SiteCode = sm.SiteCode AND ar.SlotId = sm.SlotId AND ar.IsActive = 1
    WHERE b.SiteCode = @SiteCode AND b.StockState = 'AVAILABLE';

    SELECT @OpenDemands AS OpenDemandCount,
           @ShortLines  AS ShortDemandCount,
           @DemandsReady AS ReadyDemandCount,
           @Issues      AS PostedIssueCount,
           @PartQty     AS PartOnHandQty,
           @WipQty      AS WipOnHandQty;
END;
GO

IF NOT EXISTS (SELECT 1 FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '082')
    INSERT MES.FVN_SchemaMigration (MigrationVersion, MigrationName)
    VALUES ('082', N'MES Control Tower extras (NG qty, end date) and component flow grid / summary');
GO
SELECT MigrationVersion, MigrationName FROM MES.FVN_SchemaMigration WHERE MigrationVersion = '082';
GO