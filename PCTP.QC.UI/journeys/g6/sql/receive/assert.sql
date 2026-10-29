-- g6-receive: worklist postcondition (read-only). Matches the seed: 2 production docs of 10 pcs on one 20-char LOT.
SELECT
    (SELECT COUNT(*) FROM dbo.vNhapTP WHERE MA_SAN_PHAM LIKE @Prefix + N'%') AS WorklistRows,
    (SELECT ISNULL(SUM(SL_DA_SAN_XUAT), 0) FROM dbo.vNhapTP WHERE MA_SAN_PHAM LIKE @Prefix + N'%') AS Produced,
    (SELECT ISNULL(SUM(SL_DA_NHAP), 0) FROM dbo.vNhapTP WHERE MA_SAN_PHAM LIKE @Prefix + N'%') AS Received,
    (SELECT ISNULL(MAX(LEN(LOT_NO)), 0) FROM dbo.vNhapTP WHERE MA_SAN_PHAM LIKE @Prefix + N'%') AS LotLength,
    (SELECT COUNT(*) FROM dbo.Slot s JOIN dbo.Rack r ON r.RackId = s.RackId
      WHERE r.RackName LIKE @Prefix + N'%' AND s.IsOccupied = 0) AS FreeSlots;
