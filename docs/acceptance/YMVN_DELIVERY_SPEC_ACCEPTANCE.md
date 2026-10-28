# YMVN delivery specification (B0-01, part 2)

Scenario: `PCTP.QC.SqlIntegration/YmvnDeliverySpecScenario.cs` (cases Y1–Y15)  
Source contract: `PCTP.QC.Tests/YmvnDeliverySpecContractTests.cs` (13 tests, job `unit-regression-tests`)  
Run: the scenario runs automatically inside `dotnet run --project PCTP.QC.SqlIntegration -- --engine-only` (job `sql-engine-integration`, isolated SQL Server).  
Fixtures: uniquely named `##PCTP_QC_YMVN_<guid>_*` tempdb tables; no `dbo` business table is touched.

## Purpose

B0-01 replaces the legacy writer `PhieuKhoRepository.CapNhapKhoYMVN` → `TruStockTpFifo` (direct `STOCKTP` update) with a delivery command. This suite **freezes the current behaviour first** (characterization) so that the replacement can be proven to keep parity, as required by `WMS_LEGACY_FILE_CLASSIFICATION.md` §4. It does not change any production code.

## What it proves and what it does not

| Proven (protocol level) | Not proven |
|---|---|
| The three `STOCKTP` statements (lock-read, FIFO select, guarded `UPDATE`) are **extracted at run time** from `PhieuKhoRepository.cs`, bound to a tempdb fixture and executed on SQL Server; drift in them breaks the scenario. | That `CapNhapKhoYMVN` itself runs these steps (it needs the WinForms/DevExpress graph). Step order is mirrored in the scenario and kept honest by the source-contract tests only. |
| The LOT match predicate is the real `LotCodeHelper.BuildLotMatchSql`. | `BulkStockAdjustService`/`StockMovementService` behaviour: the SlotLot pick is a SQL mirror (virtual slot A0 first, then FIFO, throw when short). |
| `STOCKTP` == active `SlotLot` per LOT key after each case; rollback restores stock, SlotLot, TMP, ledgers and scan rows together. | `HoanTatSauKhiTruKho` (pending-delivery close + `EXPORT_CONFIRMED_HVN` history), `DanhDauDaGiao` order-table marking, QR/PART allocation rules of `Usp_Qrcode_Take_LotYMVN2405`, production schema/triggers (G4), UI journeys (G6). |

Negative control **Y12** removes the locks and the quantity guard and **must** oversell (`SLCONLAI = -10`). If it does not, the scenario fails because the race would be undetectable.

## Scenario matrix

| ID | Spec requirement | Setup | Expected (current behaviour) |
|---|---|---|---|
| Y1 | FIFO, tie-break, partial last row | three legacy short LOTs, 10 each, two with the same receipt date, deliver 25 | order `NGAYNHAP ASC, LOT ASC`; rows 0/0/5; STOCKTP == SlotLot |
| Y2 | exact 20-char key | two 20-char LOTs sharing the 13-char prefix | only the requested LOT moves; over-request reports `SLTK` of its own key |
| Y3 | legacy fallback (**F-Y2**) | short literal + 20-char LOT with the same 13-char prefix | the literal consumes the legacy row, then spills into the 20-char row |
| Y4 | multi-LOT | `LOT1-10, LOT2-5` | both deducted, one ledger row, TMP `OK`, DG rows consumed, both reconcile |
| Y5 | partial delivery | 100 in stock, two deliveries of 30 | 40 left; `SLNHAP - SLXUAT = SLCONLAI`; reconciled |
| Y6 | shortage on LOT 2 | LOT 1 ok, LOT 2 short by 2 | `SLC=5 SLTK=3 SLT=2`; LOT 1, SlotLot, TMP, ledger all rolled back |
| Y7 | malformed input | `""`, `"   "`, `","`, `abc`, `abc-`, `-5`, `LOT-0`, `LOT-x`, `LOT--0` | rejected before any write |
| Y8 | SlotLot short | STOCKTP 10, SlotLot 6 | fails closed (`manageTransaction:false` semantics), STOCKTP restored |
| Y9 | failure before commit | failure injected after every write | stock, SlotLot, TMP, ledgers, DG rows unchanged |
| Y10 | retry same STT (**F-Y3**) | ample stock / scarce stock | ample: TMP guard fails and rolls back, deducted once; scarce: reported as shortage with `SLTK=0` |
| Y11 | two sessions, same remaining 10 | production lock-read in both | A committed, B shortage, stock 0, reconciled |
| Y12 | negative control | no locks, no guard, barrier after the FIFO select | both commit, `SLCONLAI = -10` (oversell) |
| Y13 | defense in depth | no locks, guard kept | one commit, one `STOCK_CHANGED`, stock 0 |
| Y14 | same STT from two sessions | ample stock | A committed, B fails on the TMP guard, deducted once, one ledger row |
| Y15 | scan rows (**F-Y1**) | two STT of one part, DG rows on both | both DG rows archived and deleted; non-DG row stays |

## Coverage against WMS_LEGACY_FILE_CLASSIFICATION §4

| Required characterization | Status |
|---|---|
| 1. FIFO by LOT, QR/PART rules, multi-LOT/Slot, partial | FIFO/multi-LOT/partial/Slot: Y1–Y5. **QR/PART allocation: not covered** (lives in `Usp_Qrcode_Take_LotYMVN2405` / DOCQR; needs its own scenario). |
| 2. STOCKTP = Σ SlotLot; document + history + ledger commit/rollback together | STOCKTP/SlotLot/ledger/TMP: Y4–Y9. **Pending-delivery close and history rows: not mirrored.** |
| 3. Retry does not deduct twice; mid-way failure rolls back everything | Y9, Y10, Y14 |
| 4. Movement service becomes mandatory | source-contract test `B0_01_StillOpen_...` documents the open state; flip it with the gate document |

## Findings for the owner (not changed by this patch)

1. **F-Y1 scan rows by part, not STT.** The archive `INSERT … SELECT` and the `DELETE` use `MAHANGFCC = @maHang AND KETQUA = 'DG'`, so delivering one STT also consumes the DG rows of other STT of the same part. Confirm whether "complete the whole part" is intended.
2. **F-Y2 short literal spills over.** A literal shorter than 20 characters falls back to the 13-char legacy key and can consume a 20-char lot that only shares the prefix. The delivery command must keep or explicitly replace this rule.
3. **F-Y3 no "already delivered" signal.** Replay protection is only the TMP `STATUS = 'NG'` guard evaluated **after** stock is deducted (rolled back by the exception). With scarce stock a replay is reported as a shortage. The delivery command should check the business key before touching stock and return one explicit "already processed" result (release-gate scenario "same operation retried → one mutation").
4. **F-Y4 per-STT transactions.** `PhieuYmvnService` calls `CapNhapKhoYMVN` once per row, each with its own transaction: a business error is collected and the loop continues; an exception stops the loop with earlier STT already committed. Decide whether a batch must be atomic.

## Status rule

Do **not** change B0-01 from OPEN. Record this suite as "YMVN characterization, protocol-level, CI run `<id>` on SHA `<sha>`" in `WMS_RELEASE_GATE.md` only after the run is green. Closing B0-01 additionally needs the delivery command, parity tests against it, removal of the legacy writers, and a run of the real repository methods on a Windows/.NET Framework harness (G4/G6).
