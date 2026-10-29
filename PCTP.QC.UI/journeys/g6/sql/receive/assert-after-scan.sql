-- g6-receive: read-only postconditions after two tem-tong scans through FormEnterItemSV.
-- The UI/service must create STOCKTP, SlotLot and StockHistory; the seed never inserts these result rows.
SELECT
    (SELECT COUNT(*) FROM dbo.STOCKTP
      WHERE PART = @Prefix + N'_FG') AS StockRows,
    (SELECT ISNULL(SUM(CAST(SLNHAP AS bigint)), 0) FROM dbo.STOCKTP
      WHERE PART = @Prefix + N'_FG') AS StockReceived,
    (SELECT ISNULL(SUM(CAST(SLCONLAI AS bigint)), 0) FROM dbo.STOCKTP
      WHERE PART = @Prefix + N'_FG') AS StockRemaining,
    (SELECT ISNULL(SUM(CAST(SLXUAT AS bigint)), 0) FROM dbo.STOCKTP
      WHERE PART = @Prefix + N'_FG') AS StockIssued,
    (SELECT COUNT(*) FROM dbo.STOCKTP
      WHERE PART = @Prefix + N'_FG'
        AND (SLNHAP - SLXUAT <> SLCONLAI OR SLCONLAI < 0 OR SLXUAT > SLNHAP)) AS StockViolations,
    (SELECT COUNT(*) FROM dbo.SlotLot
      WHERE ItemCode = @Prefix + N'_FG') AS SlotLotRows,
    (SELECT ISNULL(SUM(CAST(Quantity AS bigint)), 0) FROM dbo.SlotLot
      WHERE ItemCode = @Prefix + N'_FG') AS SlotLotQty,
    (SELECT COUNT(*) FROM dbo.StockHistory
      WHERE ItemCode = @Prefix + N'_FG') AS HistoryRows,
    (SELECT ISNULL(SUM(CAST(Quantity AS bigint)), 0) FROM dbo.StockHistory
      WHERE ItemCode = @Prefix + N'_FG') AS HistoryQty,
    (SELECT COUNT(*) FROM dbo.SlotLot l
      WHERE l.ItemCode = @Prefix + N'_FG'
        AND NOT EXISTS (SELECT 1 FROM dbo.STOCKTP s WHERE s.PART = l.ItemCode AND s.LOT = l.LotNo)) AS SlotLotWithoutStockViolations;
