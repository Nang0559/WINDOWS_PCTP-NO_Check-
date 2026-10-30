CREATE OR ALTER PROCEDURE [dbo].[sp_GetCurrentStockStatus]
AS
BEGIN
    SET NOCOUNT ON;

    /*
      Match physical LOT identity consistently with delivery confirmation:
      full LOT (>13 chars) matches exactly; a 13-character root is accepted
      only when it maps to one physical LOT. Multiple slot allocations for the
      same physical LOT remain separate location rows.

      Every LOT with SLCONLAI > 0 is always reported: the rows of one LOT add up
      to STOCKTP.SLCONLAI. Quantity that is not covered by an active SlotLot row
      (legacy / imported stock, or a partial put-away) is reported as one extra
      row with LocationCode = N'Chua gan slot' so the report never silently
      drops location information.

      QrData is the full QR of the SlotLot row (falls back to the TemCode when
      the QR text was not stored).
    */
    ;WITH StockLotKeys AS
    (
        SELECT
            LEFT(RTRIM(LOT), 13) AS LotKey,
            COUNT(DISTINCT RTRIM(LOT)) AS PhysicalLotCount
        FROM dbo.STOCKTP
        GROUP BY LEFT(RTRIM(LOT), 13)
    ),
    Slotted AS
    (
        SELECT
            stp.LOT AS StockLot,
            sl.ItemCode AS SlotItemCode,
            sl.Quantity AS SlotQuantity,
            ISNULL(NULLIF(sl.QrData, N''), sl.TemCode) AS SlotQr,
            CASE WHEN s.SlotId IS NOT NULL
                 THEN w.Name + N'-' + r.RackName + N'-Slot' + CAST(s.SlotNumber AS NVARCHAR(20))
                 ELSE NULL END AS SlotLocation,
            r.RackName AS RackName,
            s.SlotNumber AS SlotNumber,
            w.Name AS WarehouseName
        FROM dbo.STOCKTP AS stp
        LEFT JOIN StockLotKeys AS lk
            ON lk.LotKey = LEFT(RTRIM(stp.LOT), 13)
        INNER JOIN dbo.SlotLot AS sl
            ON sl.PhieuStatus = 0
           AND
           (
                (LEN(RTRIM(stp.LOT)) > 13 AND RTRIM(sl.LotNo) = RTRIM(stp.LOT))
                OR
                (LEN(RTRIM(stp.LOT)) <= 13
                 AND lk.PhysicalLotCount = 1
                 AND LEFT(RTRIM(sl.LotNo), 13) = LEFT(RTRIM(stp.LOT), 13))
           )
        LEFT JOIN dbo.Slot AS s ON s.SlotId = sl.SlotId
        LEFT JOIN dbo.Rack AS r ON r.RackId = s.RackId
        LEFT JOIN dbo.Warehouse AS w ON w.WarehouseId = r.WarehouseId
        WHERE ISNULL(stp.SLCONLAI, 0) > 0
    ),
    SlottedTotal AS
    (
        SELECT StockLot, SUM(SlotQuantity) AS SlotQty
        FROM Slotted
        GROUP BY StockLot
    )
    SELECT
        u.ItemCode,
        u.LotNo,
        u.QrData,
        u.Quantity,
        u.LocationCode,
        u.Status
    FROM
    (
        SELECT
            ISNULL(sd.SlotItemCode, stp.PART) AS ItemCode,
            stp.LOT AS LotNo,
            sd.SlotQr AS QrData,
            sd.SlotQuantity AS Quantity,
            sd.SlotLocation AS LocationCode,
            CASE WHEN stp.SATUS = 1 THEN N'Da ket thuc' ELSE N'Dang mo' END AS Status,
            sd.WarehouseName AS SortWarehouse,
            sd.RackName AS SortRack,
            sd.SlotNumber AS SortSlot
        FROM dbo.STOCKTP AS stp
        INNER JOIN Slotted AS sd ON sd.StockLot = stp.LOT
        WHERE ISNULL(stp.SLCONLAI, 0) > 0

        UNION ALL

        SELECT
            stp.PART AS ItemCode,
            stp.LOT AS LotNo,
            CAST(NULL AS NVARCHAR(MAX)) AS QrData,
            stp.SLCONLAI - ISNULL(st.SlotQty, 0) AS Quantity,
            N'Chua gan slot' AS LocationCode,
            CASE WHEN stp.SATUS = 1 THEN N'Da ket thuc' ELSE N'Dang mo' END AS Status,
            N'~' AS SortWarehouse,
            N'~' AS SortRack,
            2147483647 AS SortSlot
        FROM dbo.STOCKTP AS stp
        LEFT JOIN SlottedTotal AS st ON st.StockLot = stp.LOT
        WHERE ISNULL(stp.SLCONLAI, 0) > 0
          AND stp.SLCONLAI - ISNULL(st.SlotQty, 0) > 0
    ) AS u
    ORDER BY u.LotNo, u.SortWarehouse, u.SortRack, u.SortSlot;
END;
