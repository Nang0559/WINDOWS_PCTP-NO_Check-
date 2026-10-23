/* ============================================================================
   PHASE14 — SlotLot inventory invariant audit
   ---------------------------------------------------------------------------
   Canonical inventory rule:
       PhieuStatus = 0  => current physical stock
       PhieuStatus = 1  => Split/history only
       PhieuStatus = 2  => ExportedOut/history only
       PhieuStatus = 3  => NG/quarantine only

   Therefore:
       Slot.Quantity = SUM(SlotLot.Quantity WHERE PhieuStatus = 0)
       STOCKTP.SLCONLAI = SUM(SlotLot.Quantity WHERE PhieuStatus = 0)

   This script is READ-ONLY. It deliberately does not mutate production data.
   Run it against the isolated QC/test database before changing fixtures.
   ============================================================================ */

/* 1. First diagnostic requested by QC review. */
SELECT
    PhieuStatus,
    COUNT(*) AS RowCount,
    SUM(ISNULL(Quantity, 0)) AS Quantity
FROM SlotLot
GROUP BY PhieuStatus
ORDER BY PhieuStatus;

/* 2. Historical/quarantine rows must not contribute to current physical stock. */
SELECT
    PhieuStatus,
    COUNT(*) AS PositiveQuantityRows,
    SUM(ISNULL(Quantity, 0)) AS PositiveQuantity
FROM SlotLot
WHERE PhieuStatus IN (1, 2, 3)
  AND ISNULL(Quantity, 0) > 0
GROUP BY PhieuStatus
ORDER BY PhieuStatus;

/* 3. Slot header invariant: stored Slot.Quantity must equal active SlotLot rows. */
SELECT
    s.SlotId,
    ISNULL(s.Quantity, 0) AS StoredSlotQuantity,
    ISNULL(x.ActiveLotQuantity, 0) AS ExpectedSlotQuantity,
    ISNULL(s.Quantity, 0) - ISNULL(x.ActiveLotQuantity, 0) AS Difference
FROM Slot s
LEFT JOIN (
    SELECT SlotId, SUM(ISNULL(Quantity, 0)) AS ActiveLotQuantity
    FROM SlotLot
    WHERE PhieuStatus = 0
    GROUP BY SlotId
) x ON x.SlotId = s.SlotId
WHERE ISNULL(s.Quantity, 0) <> ISNULL(x.ActiveLotQuantity, 0)
ORDER BY s.SlotId;

/* 4. Per-physical-LOT STOCKTP vs active SlotLot invariant. */
SELECT
    st.LOT,
    ISNULL(st.SLCONLAI, 0) AS StockTpQuantity,
    ISNULL(sl.ActiveQuantity, 0) AS ActiveSlotLotQuantity,
    ISNULL(st.SLCONLAI, 0) - ISNULL(sl.ActiveQuantity, 0) AS Difference
FROM STOCKTP st
LEFT JOIN (
    SELECT RTRIM(LotNo) AS LotNo, SUM(ISNULL(Quantity, 0)) AS ActiveQuantity
    FROM SlotLot
    WHERE PhieuStatus = 0
    GROUP BY RTRIM(LotNo)
) sl ON sl.LotNo = RTRIM(st.LOT)
WHERE ISNULL(st.SLCONLAI, 0) <> ISNULL(sl.ActiveQuantity, 0)
ORDER BY st.LOT;

/* 5. Explicitly expose status 1/2 rows with positive Quantity so fixtures can
      be corrected instead of silently treating historical quantity as stock. */
SELECT
    SlotLotId,
    SlotId,
    LotNo,
    ItemCode,
    Quantity,
    PhieuStatus,
    CreatedDate,
    ImportDate
FROM SlotLot
WHERE PhieuStatus IN (1, 2)
  AND ISNULL(Quantity, 0) > 0
ORDER BY SlotId, LotNo, SlotLotId;
