# PCTP QR LOT format — confirmed business contract

Status: business field layout confirmed by the operator on 2026-10-30. This document is the shared reference for QC fixtures and QR-format acceptance. It does not authorize production parser or warehouse-procedure changes.

## 1. QR envelopes

### Tem tổng — 6 colon-separated fields

```text
LOTNO_TEM_TONG:MA_HANG:NGAY_SX:SO_LUONG_TEM:SO_TEM_CUA_PHIEU:MA_TEM
2610300027230077170007512000:2210A-K2C-V000:30/10/2026:2000:2:A010000000059140
```

| Field | Meaning | Example |
|---|---|---|
| 1 | LOTNO token | `2610300027230077170007512000` |
| 2 | Item code | `2210A-K2C-V000` |
| 3 | Manufacturing date, dd/MM/yyyy | `30/10/2026` |
| 4 | Label quantity | `2000` |
| 5 | Number of labels on the slip | `2` |
| 6 | Label identifier | `A010000000059140` |

### Tem hộp — 4 colon-separated fields

```text
LOTNO_TEM_HOP:MA_HANG:NGAY_SX:SO_LUONG
```

The box label uses its own serial and quantity suffix in the LOTNO token. Warehouse receipt uses the total label; warehouse issue/delivery uses the box label. The quantity used for a box issue is the quantity field of that box QR, not the total-label quantity.

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

## 5. Important unresolved fixture discrepancy

An earlier box-label example supplied in the conversation was:

```text
2610300027230077520020:2210A-K2C-V000:30/10/2026:20
```

Its LOTNO token is 22 characters, not 28, and its first 20 characters do not equal the confirmed total-label stock LOT identity. It therefore cannot be treated as a valid matching box-label fixture under this contract. Do not silently pad, repair, or reinterpret it. Obtain the corrected real box QR before enabling a box-label E2E journey.

## 6. Test layers and production boundary

- Contract tests validate the field positions, 20-character identity, total/box envelope shape, customer Gear mapping, and the known invalid sample.
- Production parser tests must execute the compiled production `QRCodeParser` and verify its parsed fields.
- A contract-oracle test is not proof that production receiving/export services normalize the LOT correctly.
- The current legacy `ParseTachLotQr` implementation documents a 27-character LOT and strips only the final four characters. That assumption does not match this 28-character / 20-character identity contract. Track it as a production integration blocker; do not change the production parser or central SQL in this QC-only change.
- Full acceptance additionally requires isolated SQL Server tests for receiving with total labels, issue with box labels, shared LOT reconciliation, shortage/rollback/replay/concurrency, and cleanup; then a real Windows UI journey. Do not mark G6 PASS from parser/unit tests alone.
