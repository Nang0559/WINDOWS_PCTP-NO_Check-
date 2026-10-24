/*
    PCTP STOCKTP identity and cross-ledger diagnostic.
    READ ONLY: no CREATE/ALTER/INSERT/UPDATE/DELETE/EXEC.
    Run only against an approved isolated business-schema copy or DB57 after approval.
    Never run against 192.168.200.14\BRAVO.

    Purpose:
    - Establish whether LOT is unique in STOCKTP or whether duplicate LOT / LOT+PART rows exist.
    - Compare STOCKTP aggregate quantities with active SlotLot quantities.
    - Detect STOCKTP equation and Slot header mismatches before changing repository semantics.
    No assumption is made here that LOT alone is the business key.
*/

-- 1. Duplicate LOT across all PART values.
SELECT
    LTRIM(RTRIM(LOT)) AS LOT,
    COUNT_BIG(*) AS RowCount,
    COUNT(DISTINCT LTRIM(RTRIM(ISNULL(PART, N'')))) AS DistinctPartCount,
    SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
    SUM(ISNULL(SLXUAT, 0)) AS TotalIssued,
    SUM(ISNULL(SLCONLAI, 0)) AS TotalAvailable
FROM dbo.STOCKTP
GROUP BY LTRIM(RTRIM(LOT))
HAVING COUNT_BIG(*) > 1
ORDER BY LTRIM(RTRIM(LOT));

-- 2. Duplicate rows for the same logical LOT + PART key.
SELECT
    LTRIM(RTRIM(LOT)) AS LOT,
    LTRIM(RTRIM(ISNULL(PART, N''))) AS PART,
    COUNT_BIG(*) AS RowCount,
    SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
    SUM(ISNULL(SLXUAT, 0)) AS TotalIssued,
    SUM(ISNULL(SLCONLAI, 0)) AS TotalAvailable
FROM dbo.STOCKTP
GROUP BY LTRIM(RTRIM(LOT)), LTRIM(RTRIM(ISNULL(PART, N'')))
HAVING COUNT_BIG(*) > 1
ORDER BY LTRIM(RTRIM(LOT)), LTRIM(RTRIM(ISNULL(PART, N'')));

-- 3. Compare aggregate STOCKTP with active physical/virtual SlotLot by LOT + PART.
;WITH StockByKey AS
(
    SELECT
        LTRIM(RTRIM(LOT)) AS LOT,
        LTRIM(RTRIM(ISNULL(PART, N''))) AS PART,
        SUM(ISNULL(SLCONLAI, 0)) AS StockQty,
        SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
        SUM(ISNULL(SLXUAT, 0)) AS TotalIssued
    FROM dbo.STOCKTP
    GROUP BY LTRIM(RTRIM(LOT)), LTRIM(RTRIM(ISNULL(PART, N'')))
),
SlotByKey AS
(
    SELECT
        LTRIM(RTRIM(LotNo)) AS LOT,
        LTRIM(RTRIM(ISNULL(ItemCode, N''))) AS PART,
        SUM(ISNULL(Quantity, 0)) AS ActiveSlotQty
    FROM dbo.SlotLot
    WHERE PhieuStatus = 0 AND ISNULL(Quantity, 0) > 0
    GROUP BY LTRIM(RTRIM(LotNo)), LTRIM(RTRIM(ISNULL(ItemCode, N'')))
)
SELECT
    COALESCE(st.LOT, sl.LOT) AS LOT,
    COALESCE(st.PART, sl.PART) AS PART,
    ISNULL(st.StockQty, 0) AS StockTpQty,
    ISNULL(sl.ActiveSlotQty, 0) AS ActiveSlotLotQty,
    ISNULL(st.StockQty, 0) - ISNULL(sl.ActiveSlotQty, 0) AS Difference,
    ISNULL(st.TotalReceived, 0) AS TotalReceived,
    ISNULL(st.TotalIssued, 0) AS TotalIssued
FROM StockByKey st
FULL OUTER JOIN SlotByKey sl
    ON sl.LOT = st.LOT AND sl.PART = st.PART
WHERE ISNULL(st.StockQty, 0) <> ISNULL(sl.ActiveSlotQty, 0)
ORDER BY COALESCE(st.LOT, sl.LOT), COALESCE(st.PART, sl.PART);

-- 4. Check cumulative STOCKTP equation at the same LOT + PART grain.
SELECT
    LTRIM(RTRIM(LOT)) AS LOT,
    LTRIM(RTRIM(ISNULL(PART, N''))) AS PART,
    SUM(ISNULL(SLNHAP, 0)) AS TotalReceived,
    SUM(ISNULL(SLXUAT, 0)) AS TotalIssued,
    SUM(ISNULL(SLCONLAI, 0)) AS TotalAvailable,
    SUM(ISNULL(SLNHAP, 0)) - SUM(ISNULL(SLXUAT, 0)) - SUM(ISNULL(SLCONLAI, 0)) AS Difference
FROM dbo.STOCKTP
GROUP BY LTRIM(RTRIM(LOT)), LTRIM(RTRIM(ISNULL(PART, N'')))
HAVING SUM(ISNULL(SLNHAP, 0)) - SUM(ISNULL(SLXUAT, 0)) <> SUM(ISNULL(SLCONLAI, 0))
ORDER BY LTRIM(RTRIM(LOT)), LTRIM(RTRIM(ISNULL(PART, N'')));

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
