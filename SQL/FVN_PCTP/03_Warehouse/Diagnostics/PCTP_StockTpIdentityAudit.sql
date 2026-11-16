/*
    PCTP STOCKTP identity and cross-ledger diagnostic.
    READ ONLY: no CREATE/ALTER/INSERT/UPDATE/DELETE/EXEC.
    Run only against an approved isolated business-schema copy or DB57 after approval.
    Never run against 192.168.200.14\BRAVO.

    Purpose:
    - LOT is the unique business key in STOCKTP (confirmed business rule).
    - A LOT may be distributed across multiple active SlotLot rows and multiple Slots.
    - Compare one STOCKTP row per LOT against the SUM of active SlotLot quantities for that LOT.
    - Detect one LOT mapped to inconsistent item codes, duplicate STOCKTP LOTs and Slot header mismatches.
*/

-- 1. STOCKTP must contain exactly one row per LOT.
SELECT
    LTRIM(RTRIM(LOT)) AS LOT,
    COUNT_BIG(*) AS RowCount,
    COUNT(DISTINCT LTRIM(RTRIM(ISNULL(PART, N'')))) AS DistinctPartCount,
    SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
    SUM(ISNULL(SLXUAT, 0)) AS TotalIssued,
    SUM(ISNULL(SLCONLAI, 0)) AS TotalAvailable
FROM dbo.STOCKTP
GROUP BY LTRIM(RTRIM(LOT))
HAVING COUNT_BIG(*) <> 1
ORDER BY LTRIM(RTRIM(LOT));

-- 2. Compare one STOCKTP row per LOT with the sum of all active SlotLot rows.
-- A LOT can appear in multiple Slots; SlotLot quantities must be aggregated by LOT,
-- not compared one-to-one and not grouped by SlotId.
;WITH StockByLot AS
(
    SELECT
        LTRIM(RTRIM(LOT)) AS LOT,
        MAX(LTRIM(RTRIM(ISNULL(PART, N'')))) AS PART,
        COUNT_BIG(*) AS StockRows,
        SUM(ISNULL(SLCONLAI, 0)) AS StockQty,
        SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
        SUM(ISNULL(SLXUAT, 0)) AS TotalIssued
    FROM dbo.STOCKTP
    GROUP BY LTRIM(RTRIM(LOT))
),
SlotByLot AS
(
    SELECT
        LTRIM(RTRIM(LotNo)) AS LOT,
        SUM(ISNULL(Quantity, 0)) AS ActiveSlotQty,
        COUNT(DISTINCT LTRIM(RTRIM(ISNULL(ItemCode, N'')))) AS DistinctItemCodes,
        COUNT(DISTINCT SlotId) AS SlotCount
    FROM dbo.SlotLot
    WHERE PhieuStatus = 0 AND ISNULL(Quantity, 0) > 0
    GROUP BY LTRIM(RTRIM(LotNo))
)
SELECT
    COALESCE(st.LOT, sl.LOT) AS LOT,
    st.PART,
    ISNULL(st.StockRows, 0) AS StockTpRows,
    ISNULL(st.StockQty, 0) AS StockTpQty,
    ISNULL(sl.ActiveSlotQty, 0) AS ActiveSlotLotQty,
    ISNULL(st.StockQty, 0) - ISNULL(sl.ActiveSlotQty, 0) AS Difference,
    ISNULL(sl.SlotCount, 0) AS ActiveSlotCount,
    ISNULL(sl.DistinctItemCodes, 0) AS DistinctItemCodes,
    ISNULL(st.TotalReceived, 0) AS TotalReceived,
    ISNULL(st.TotalIssued, 0) AS TotalIssued
FROM StockByLot st
FULL OUTER JOIN SlotByLot sl ON sl.LOT = st.LOT
WHERE ISNULL(st.StockRows, 0) <> 1
   OR ISNULL(st.StockQty, 0) <> ISNULL(sl.ActiveSlotQty, 0)
   OR ISNULL(sl.DistinctItemCodes, 0) > 1
ORDER BY COALESCE(st.LOT, sl.LOT);

-- 3. STOCKTP equation at the unique LOT grain.
SELECT
    LTRIM(RTRIM(LOT)) AS LOT,
    MAX(LTRIM(RTRIM(ISNULL(PART, N'')))) AS PART,
    SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
    SUM(ISNULL(SLXUAT, 0)) AS TotalIssued,
    SUM(ISNULL(SLCONLAI, 0)) AS TotalAvailable,
    SUM(ISNULL(SLNHAP, 0)) - SUM(ISNULL(SLXUAT, 0)) - SUM(ISNULL(SLCONLAI, 0)) AS Difference
FROM dbo.STOCKTP
GROUP BY LTRIM(RTRIM(LOT))
HAVING SUM(ISNULL(SLNHAP, 0)) - SUM(ISNULL(SLXUAT, 0)) <> SUM(ISNULL(SLCONLAI, 0))
ORDER BY LTRIM(RTRIM(LOT));

-- 5. Slot header must equal the sum of active SlotLot rows in that Slot.
SELECT
    s.SlotId,
    ISNULL(s.Quantity, 0) AS SlotHeaderQty,
    ISNULL(x.ActiveSlotQty, 0) AS ActiveSlotLotQty,
    ISNULL(s.Quantity, 0) - ISNULL(x.ActiveSlotQty, 0) AS Difference
FROM dbo.Slot s
LEFT JOIN
(
    SELECT SlotId, SUM(ISNULL(Quantity, 0)) AS ActiveSlotQty
    FROM dbo.SlotLot
    WHERE PhieuStatus = 0 AND ISNULL(Quantity, 0) > 0
    GROUP BY SlotId
) x ON x.SlotId = s.SlotId
WHERE ISNULL(s.Quantity, 0) <> ISNULL(x.ActiveSlotQty, 0)
ORDER BY s.SlotId;
