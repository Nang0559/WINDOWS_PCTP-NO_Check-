CREATE PROCEDURE [dbo].[sp_GetCurrentStockStatus]
AS
BEGIN
    SET NOCOUNT ON;

    /*
      Match physical LOT identity consistently with delivery confirmation:
      full LOT (>13 chars) matches exactly; a 13-character root is accepted
      only when it maps to one physical LOT. Multiple slot allocations for the
      same physical LOT remain separate location rows.
    */
    ;WITH StockLotKeys AS
    (
        SELECT
            LEFT(RTRIM(LOT), 13) AS LotKey,
            COUNT(DISTINCT RTRIM(LOT)) AS PhysicalLotCount
        FROM dbo.STOCKTP
        GROUP BY LEFT(RTRIM(LOT), 13)
    )
    SELECT
        ISNULL(sl.ItemCode, stp.PART) AS ItemCode,
        stp.LOT AS LotNo,
        sl.TemCode AS QrData,
        ISNULL(sl.Quantity, stp.SLCONLAI) AS Quantity,
        CASE WHEN s.SlotId IS NOT NULL
             THEN w.Name + N'-' + r.RackName + N'-Slot' + CAST(s.SlotNumber AS NVARCHAR(20))
             ELSE NULL END AS LocationCode,
        stp.SATUS AS Status
    FROM dbo.STOCKTP AS stp
    LEFT JOIN StockLotKeys AS lk
        ON lk.LotKey = LEFT(RTRIM(stp.LOT), 13)
    LEFT JOIN dbo.SlotLot AS sl
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
    ORDER BY stp.LOT, w.Name, r.RackName, s.SlotNumber;
END;
