# PCTP QR LOT format — confirmed business contract

Status: business field layout confirmed by the operator on 2026-10-30. This document is the shared reference for QC fixtures and QR-format acceptance. It does not authorize production parser or warehouse-procedure changes.

## 1. QR envelopes

### Tem tổng — 6 colon-separated fields

```text
LOTNO_TEM_TONG:MA_HANG:NGAY_SX:SO_LUONG_TEM:SO_THU_TU_MA_PHIEU:MA_PHIEU
2610300027230077170007512000:2210A-K2C-V000:30/10/2026:2000:2:A010000000059140
```

| Field | Meaning | Example |
|---|---|---|
| 1 | LOTNO token | `2610300027230077170007512000` |
| 2 | Item code | `2210A-K2C-V000` |
| 3 | Manufacturing date, dd/MM/yyyy | `30/10/2026` |
| 4 | Label quantity | `2000` |
| 5 | Sequence number belonging to the `A…` slip code (`SoPhieuTong` in the legacy model) | `2` |
| 6 | Slip code (`MaPhieu`) | `A010000000059140` |

### Tem hộp — 4 colon-separated fields

```text
LOTNO_TEM_HOP:MA_HANG:NGAY_SX:SO_LUONG
```

The box label uses its own serial and quantity suffix in the LOTNO token. Warehouse receipt uses the total label; warehouse issue/delivery uses the box label. The quantity used for a box issue is the quantity field of that box QR, not the total-label quantity.

## 1.1 Independent identity domains — do not conflate them

The operator confirmed that field 5 and field 6 together identify the receiving slip/label reference:

- Field 6 (`MaPhieu`) is the `A…` slip code.
- Field 5 (`SoPhieuTong`) is the sequence number associated with that `A…` code.
- The receiving reference key is `MaPhieu:SoPhieuTong`, for example `A010000000059140:2`.
- This sequence is **not** the daily label sequence embedded in LOTNO positions 21–24 (the example value `0751`).
- LOTNO's daily sequence distinguishes printed LOT/label tokens; it must never replace either part of the slip reference key.
- Conversely, the slip reference key must never be used to derive or replace the 20-character stock LOT identity.

The legacy property name `SoPhieuTong` is retained for compatibility, but its business meaning in this six-field QR is specifically the sequence number associated with `MaPhieu`, not the daily LOTNO sequence and not a count of labels on the slip.

## 2. LOTNO token layout

The operator-confirmed total-label example is exactly 28 characters:

| 1-based positions | Length | Value | Meaning |
|---|---:|---|---|
| 1–6 | 6 | `261030` | Production date, YYMMDD |
| 7–11 | 5 | `00272` | Product code |
| 12 | 1 | `3` | Production shift |
| 13 | 1 | `0` | Gear |
| 14–16 | 3 | `077` | Production line |
| 17–20 | 4 | `1700` | Department |
| 21–24 | 4 | `0751` | Daily label sequence |
| 25–28 | 4 | `2000` | Quantity encoded in LOTNO |

Thus the canonical stock LOT identity is the first 20 characters:

```text
26103000272300771700
```

The final 8 characters (daily label sequence + embedded quantity) distinguish a printed label, not a different stock LOT. Never use the complete 28-character label token as a separate stock LOT identity when matching total and box labels.

## 3. Customer Gear validation

- HVN: Gear must be `0`.
- YMVN: Gear must be one of `1`–`5`, mapped as `1=A, 2=B, 3=C, 4=D, 5=E`.
- Do not infer a missing Gear or silently translate unsupported values.

## 4. Stock workflow invariants

1. Receiving/import uses the total label.
2. Issue/delivery uses the box label.
3. Total and box labels must resolve to the same 20-character stock LOT identity.
4. Item code and production date must also match the receiving LOT record.
5. Issue quantity comes from the box QR's explicit quantity field and must not exceed eligible stock in that LOT.
6. FIFO, when applicable, is evaluated only among eligible stock rows inside the already-resolved LOT. It cannot switch to another LOT.
7. Invalid length/characters, invalid date, invalid customer Gear, mismatched item/date/LOT identity, duplicate/replayed label, shortage, or failed transaction must not create partial stock/ledger mutations.

## 5. Operator-confirmed matching box-label fixture

The operator-confirmed real box QR is:

```text
2610300027230077170007520020:2210A-K2C-V000:30/10/2026:20
```

Its four fields are LOTNO token, item code, manufacturing date, and box quantity. The LOTNO token is exactly 28 characters. Its first 20 characters match the total-label token:

| Label | LOTNO token | First 20 characters (stock LOT identity) | Quantity |
|---|---|---|---:|
| Total | `2610300027230077170007512000` | `26103000272300771700` | 2000 |
| Box | `2610300027230077170007520020` | `26103000272300771700` | 20 |

Therefore, the supplied total and box labels are a valid matching fixture for the confirmed LOT identity. There is no outstanding QR-sample discrepancy. The box issue quantity is 20, taken from field 4 of the box QR.

## 6. Test layers and production boundary

- Contract tests validate the field positions, 20-character identity, the exact operator-confirmed total/box QR pair, customer Gear mapping, and malformed-token rejection.
- Production parser tests must execute the compiled production `QRCodeParser` and verify its parsed fields.
- A contract-oracle test is not proof that production receiving/export services normalize the LOT correctly.
- The current legacy `ParseTachLotQr` implementation documents a 27-character LOT and strips only the final four characters. That assumption does not match this 28-character / 20-character identity contract. Track it as a production integration blocker; do not change the production parser or central SQL in this QC-only change.
- Full acceptance additionally requires isolated SQL Server tests for receiving with total labels, issue with box labels, shared LOT reconciliation, shortage/rollback/replay/concurrency, and cleanup; then a real Windows UI journey. Do not mark G6 PASS from parser/unit tests alone.


### Confirmed production integration gap (not a QR-data blocker)

Source inspection of `QRCodeInfo`, `QRCodeParser.ParseQRCode`, `NhapTpReceivingService.NhapTpVaoSlot`, and `QRCodeWorkflowAdapter` found two concrete issues:

1. `QRCodeInfo.RawLotNoSL` defaults to an empty string. `ParseQRCode` currently sets `LotNo` and `RawLotNo`, but does not set `RawLotNoSL`. In the no-`matchedPhieu` receiving path, `RawLotNoSL ?? RawLotNo ?? LotNo` selects the non-null empty string; `StripCounterAndQty` returns it unchanged. The resulting LOT is empty and receiving fails with "Không xác định được LOT."
2. `QRCodeWorkflowAdapter.BuildReceivingRequest` and `BuildCustomerDeliveryRequest` copy `qr.LotNo` directly. The parser currently leaves that property as the full 28-character printed token, while the confirmed stock key is 20 characters. This can send the wrong LOT key into the central stock movement path.

The correct parser/adapter contract should preserve the printed token in `RawLotNoSL` (and raw trace fields), while exposing the normalized 20-character stock identity in `LotNo`. The receiving service should also use a non-empty fallback, not null-coalescing alone, for legacy QR objects. Do not infer that these paths are safe merely because the isolated parser and helper tests pass.

Three explicit acceptance tests were added with `Skip` so the current CI remains truthful while production changes are outside this QC-only authorization: parser raw/normalized fields, receiving adapter normalized LOT, and delivery adapter normalized LOT + box quantity. Enable these tests only after the production fix is approved and implemented.
