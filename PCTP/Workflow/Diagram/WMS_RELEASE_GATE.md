# WMS — AUTHORITATIVE RELEASE GATE / END-TO-END ACCEPTANCE CONTRACT

> **Status: NOT READY / BLOCKED**  
> Branch: `feature/wms-end-to-end-consistency`  
> Gate owner: this document. `WMS_OVERALL_STATUS.md` is the operational dashboard and MUST use the same G1–G6 definitions below.  
> Last source-review baseline: `fe73e91cf25737def51a85ab0642723e5711085f` (2026-10-26).  
> CI run `40254095397`: SUCCESS, but workflow payload reports `head_sha=20de3005dca50f2fbcefe6698ba434d1095fdae8`; PR metadata references head `fe73e91…`. Treat as **not verified on the current code SHA** until a run explicitly reports that SHA.

No PASS may be inferred from class existence, source-contract tests, SQL-engine tests, or a successful build alone.

## 1. Canonical gate matrix

| Gate | Single definition | Current |
|---|---|---|
| G1 | Traceability/source ownership: requirement → code → SQL → transaction → test → evidence; all gaps classified and closed or approved exceptions | 🔴 OPEN — regenerated matrix (`docs/TRACEABILITY_MATRIX.md`, `PCTP.QC.UI/G1-gaps.csv`): 88 code rows (66 GAP), 17 SQL scripts, 22 non-implemented TestMatrix rows, 105 gaps. Classified in `docs/G1_GAP_TRIAGE.md`; owners/exceptions not yet assigned. A COMPLETE row means "referenced by a test", not "behaviour verified" (see triage §2) |
| G2 | Unit/regression tests on exact candidate SHA, including production project compile items | 🟡 Last green baseline `a41d5df…`; current candidate `bb15a60…` CI CANCELLED (runs `40264579206`, `40264584779`); full WinForms build NOT VERIFIED |
| G3 | SQL engine integration using implementation/repository code, isolated SQL Server, data assertions and rollback | 🟡 PASS LIMITED historically; does not prove representative DBA schema or full workflow rollback |
| G4 | Representative production-like schema/procedure compatibility on isolated, rollback-only database | 🔴 BLOCKED — no current evidence |
| G5 | Two-session SQL concurrency for shared business keys/quotas, including lock serialization and injected rollback | 🔴 OPEN — no SQL two-session evidence |
| G6 | Full business E2E: real UI journey → DB/ledger/history assertions → cleanup, including delivery and reconciliation | 🔴 OPEN — UI runner build alone is not a business journey |

**Release rule:** keep PR #3 Draft and release RED while any P0 blocker or G4/G5/G6 is open.

### 1.1 Candidate `1300e22` — CI findings and fixes (unverified until rerun)

CI on `1300e22` FAILED (run `40268627593`). Root causes found by source review, fixes in the follow-up patch; **none is a PASS until a run reports the new SHA**:

| Job | Failure | Root cause | Fix |
|---|---|---|---|
| unit-regression-tests | `ReworkFinalQc_TakesTicketLockAndRereadsStateBeforeAnyWrite` (1 of 317) | The test searched `phieu = GetRequired(phieuXuLyId)` from method start and matched the pre-lock `var phieu = GetRequired(...)`, so "re-read after lock" could never hold | Search the re-read and `GetReworkPlan` only after the `AcquireReworkTicketLock(` call; assertions not relaxed |
| sql-engine-integration | `Could not find the lock resource prefix after a production sp_getapplock block` | `LoadProductionSql()` scanned every `sp_getapplock` block in the file; the `AcquireReworkTicketLock()` helper block has no literal resource prefix after it | Extract per method (`XuatKhoRework`, `GiaoHangRework`, `QCXacNhanCuoi` via helper + `TicketLockResource`); require one shared `QT_REWORK_TICKET:` resource, Exclusive, Transaction-owned, 15000 ms, `THROW 51000` |

R1–R5 have still not run on SQL Server. The SQL fixture remains protocol-level: it does not call `GiaoHangRework()`/`XuatKhoRework()` directly.

## 2. P0 blockers

### B0-02 — Rework export quota

**Implementation: CLOSED. Verification: OPEN.**

Runtime sequence in `ReworkStockService.XuatKhoRework`:

```text
Begin transaction
 → idempotency(OperationId)
 → lock current Rework ticket row
 → EnsureCanExport
 → validate SlotLot / LOT / STOCKTP
 → IStockMovementService.Export
 → workflow ledger + history
 → Commit
```

The Phase-4 guard is resolved lazily to avoid the Initial QC / Rework dependency cycle. If it cannot be resolved, export fails closed.

Required SQL evidence: over-limit rejected; exact-limit accepted; same OperationId mutates once; two sessions cannot consume the same remaining quota; failure after movement rolls back stock and ledger.

### B0-03 — Rework delivery concurrency

**Implementation: CLOSED. Verification: OPEN.**

`QTChungService.GiaoHangRework` begins/joins the UoW, obtains a transaction-owned exclusive application lock for the ticket, reloads the ticket, and only then reads export/delivery ledgers. Delivery ledger and status changes are committed in the same transaction. Error paths call `SafeRollback`.

The existing source-contract test proves source ordering only. It is not SQL concurrency evidence.

Required SQL evidence: two sessions deliver against the same remaining quantity; combined committed delivery never exceeds approved/exported quantity; injected failure after first ledger insert leaves no partial ledger, status or stock mutation.

### B0-01 — Customer Delivery stock owner

**OPEN — legacy writer paths remain.**

`PhieuKhoRepository` still calls:
- `Usp_Qrcode_Update_Stock2405`;
- `Usp_Qrcode_Update_Stock_SP`;
- `TruStockTpFifo` (direct `STOCKTP` update).

The repository constructor still allows `IStockMovementService = null`. Source review of `53af05d` found a single production construction path: `GiaoHangKhachModuleFactory.Build` passes a `StockMovementService` to `PhieuRepository`, which passes it to its one `PhieuKhoRepository`. `DeliveryLegacyWriterInventoryTests` pins that wiring and the exact set of legacy STOCKTP writers so neither can change silently. This does not close B0-01: STOCKTP is still written by the two stored procedures and by `TruStockTpFifo`; parity P1–P13 are not started (see `WMS_B0_01_DELIVERY_PARITY.md`).

Do not simply replace the stored procedures with one `Export` call. First implement a delivery command that accepts validated QR/PART/FIFO allocation, resolves exact LOT/SlotLot quantities, and atomically applies `STOCKTP`, `SlotLot`, delivery ledger/history and pending-delivery state under the shared UoW. Preserve multi-LOT, partial-LOT, retry/idempotency and rollback behavior. Remove legacy writers only after parity tests pass.

## 3. Mandatory invariants

1. `STOCKTP` and active `SlotLot` quantities reconcile atomically.
2. No approved workflow writes stock outside the KhoCore movement owner.
3. Rework export ≤ Initial QC approved Rework quantity.
4. Rework delivery ≤ approved Rework quantity and ≤ exported quantity.
5. Same business operation cannot mutate stock twice.
6. Failed operation rolls back stock, ledger, state and history together.
7. Quarantine/NG stock is never treated as active export stock.
8. Every committed movement has one authoritative audit record.

## 4. Required acceptance scenarios

| Scenario | Required result |
|---|---|
| Rework approved 10, export 11 | Reject, no mutation |
| Two sessions export 8 against approved 10 | Total committed ≤ 10 |
| Two sessions deliver 8 against approved 10 | Total committed ≤ 10 |
| Same OperationId retried | One mutation |
| Same QR delivered twice | One mutation |
| Customer delivery spans 2+ LOT | FIFO and stock reconciliation preserved |
| Movement/ledger failure | Full rollback |

## 5. Evidence rules

- **VERIFIED:** exact candidate code SHA + test identifier + schema identity + successful execution are recorded.
- **IMPLEMENTED / NOT VERIFIED:** code exists, but required runtime/business/concurrency evidence is incomplete.
- **BLOCKED:** a release blocker remains.
- **PLANNED:** not implemented.

## 6. Canonical closure sequence

1. Reconcile G1 traceability and classify all source/project compile gaps.
2. Build the full WinForms solution on Windows at the exact candidate SHA; run G2.
3. Add and execute SQL two-session + rollback tests for B0-02/B0-03 (G5).
4. Design and implement the B0-01 delivery command; require movement service in the production composition root.
5. Prove QR/PART/FIFO/multi-LOT/partial-LOT parity, idempotency and rollback.
6. Run representative isolated DBA-schema tests (G4).
7. Run full UI-to-database business journeys and reconciliation (G6).
8. Update both gate documents from this canonical matrix only after evidence is attached.


## 7. Current CI evidence snapshot (2026-10-26)

| Run | Event | SHA | Result | Scope |
|---|---|---|---|---|
| [40262798818](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40262798818) | push | `a41d5df18ee7cb3920915d47548e1db7899eb2f1` | SUCCESS | PCTP QC Test Platform workflow (configured jobs) |
| [40262804379](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40262804379) | pull_request | `a41d5df18ee7cb3920915d47548e1db7899eb2f1` | SUCCESS | PCTP QC Test Platform workflow (configured jobs) |

Older CI snapshots are historical only and must not be combined with these exact-SHA runs as if they validated the same candidate. The workflow does not build the full legacy WinForms `PCTP.csproj`, does not execute SQL two-session tests, and does not perform UI-to-database G6 journeys.

## 8. SQL files under `Workflow/Diagram`

Do not delete SQL files solely because they are stored under a documentation directory. Inventory each file by purpose: (a) authoritative deploy/migration script, (b) reproducible test fixture/seed, (c) diagnostic/query aid, or (d) obsolete snapshot. Only category (d) may be removed, and only after verifying there are no workflow, DBA, CI, or operator references. Move deployable scripts to a dedicated database/migrations location in a separate change with updated references; preserve content and history.


## 9. Latest candidate status

Candidate code SHA `bb15a604a292a75c61455d25094940ee80aa5671` contains the delivery command contract, shared Rework ticket lock and associated contract/source tests. Runs `40264579206` (push) and `40264584779` (PR) were cancelled before checkout while successive commits were pushed. No test result can be inferred from those runs. The last verified green baseline remains `a41d5df18ee7cb3920915d47548e1db7899eb2f1`.
