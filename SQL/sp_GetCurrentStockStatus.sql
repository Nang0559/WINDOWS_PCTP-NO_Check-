CREATE PROCEDURE [dbo].[sp_GetCurrentStockStatus]  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    SELECT  
        ISNULL(sl.ItemCode, stp.PART)                                       AS ItemCode,  
        stp.LOT                                                             AS LotNo,  
        sl.TemCode                                                          AS QrData,  
        ISNULL(sl.Quantity, stp.SLCONLAI)                                   AS Quantity,  
        CASE WHEN s.SlotId IS NOT NULL  
             THEN w.Name + '-' + r.RackName + '-Slot' + CAST(s.SlotNumber AS NVARCHAR(20))  
             ELSE NULL END                                                  AS LocationCode,  
        stp.SATUS                                                           AS Status  
    FROM STOCKTP stp  
    LEFT JOIN SlotLot sl  
        ON (CASE WHEN LEN(sl.LotNo) >= 20 THEN LEFT(sl.LotNo,20) ELSE LEFT(sl.LotNo,13) END)  
         = (CASE WHEN LEN(stp.LOT) >= 20 THEN LEFT(stp.LOT,20) ELSE LEFT(stp.LOT,13) END)  
       AND sl.PhieuStatus = 0  
    LEFT JOIN Slot s      ON s.SlotId = sl.SlotId  
    LEFT JOIN Rack r      ON r.RackId = s.RackId  
    LEFT JOIN Warehouse w ON w.WarehouseId = r.WarehouseId  
    WHERE ISNULL(stp.SLCONLAI, 0) > 0  
    ORDER BY stp.LOT, w.Name, r.RackName, s.SlotNumber;  
END
