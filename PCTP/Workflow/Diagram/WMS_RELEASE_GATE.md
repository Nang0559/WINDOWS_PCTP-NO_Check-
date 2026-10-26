# WMS — RELEASE GATE / END-TO-END ACCEPTANCE CONTRACT

> **Status: NOT READY / BLOCKED**  
> Branch: `feature/wms-end-to-end-consistency`  
> Current code HEAD: `e6dea18fdcbf6cc4d3041dc0f08894f3baa8aa79`  
> CI run for this HEAD: `40251750826` — queued/in progress at documentation time.

This is the authoritative release contract. No PASS may be inferred from class existence, source-contract tests, SQL-engine tests, or a successful build alone.

## 1. Gate matrix

| Gate | Meaning | Current |
|---|---|---|
| G1 | Source/architecture ownership | 🟡 |
| G2 | SQL behavior | 🟡 |
| G3 | Transaction/state atomicity | 🔴 |
| G4 | Representative real schema | 🔴 |
| G5 | Two-session concurrency | 🔴 |
| G6 | Full E2E reconciliation | 🔴 |

Release remains RED while any P0 blocker is open or transaction/concurrency evidence is missing.

## 2. P0 blockers

### B0-02 — Rework export quota

**Implementation: CLOSED. Verification: OPEN.**

Runtime sequence is:

```text
Begin transaction
 → idempotency(OperationId)
 → serialize current Rework ticket row
 → EnsureCanExport
 → validate SlotLot / LOT / STOCKTP
 → IStockMovementService.Export
 → workflow ledger + history
 → Commit
```

`XuLyHangLoiModuleFactory` supplies the Phase-4 guard lazily so the Initial QC / Rework dependency cycle is avoided. If the guard cannot be resolved, export fails closed.

Required proof:

- over-limit export rejected;
- exact-limit export succeeds;
- same OperationId mutates only once;
- two sessions cannot consume the same remaining quota.

### B0-03 — Rework delivery concurrency

**OPEN.**

The delivery path must acquire the ticket serialization boundary **before** reading exported/delivered quantities used for authorization. The final transaction must re-read quantities after the lock and before inserting delivery records.

Required sequence:

```text
Begin
 → lock ticket
 → read committed export/delivery totals
 → validate remaining
 → stock movement + delivery ledger
 → commit
```

A stale read followed by a transaction is not acceptable.

### B0-01 — Customer Delivery stock owner

**OPEN.**

`PhieuKhoRepository` still contains legacy paths that directly change `STOCKTP` through QR/FIFO stored procedures and then adjust SlotLot. This violates the single stock-owner boundary.

Required architecture:

```text
Customer Delivery
  → QR/PART/FIFO allocation (read/plan only)
  → IStockMovementService.Export
  → atomic STOCKTP + SlotLot mutation
  → delivery ledger/history
```

FIFO, QR, PART, multi-LOT, partial LOT and rollback behavior must remain equivalent after migration.

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

### VERIFIED
Exact code SHA + test identifier + schema identity + successful execution are recorded.

### IMPLEMENTED / NOT VERIFIED
Code exists, but the required runtime/business/concurrency evidence is not complete.

### BLOCKED
A release blocker remains.

### PLANNED
Not implemented.

## 6. Current CI evidence

The previous run `40204047693` tested merge commit `1b6cff5db7a48ade89b940afee6e4e81a41d9c8a`. Desktop build and SQL-engine integration passed, but unit/regression failed 1/269 because the source-contract extractor still expected an obsolete method boundary.

That test was corrected in commit `e6dea18fdcbf6cc4d3041dc0f08894f3baa8aa79`. CI run `40251750826` was automatically queued for this exact HEAD and must be allowed to complete before any test gate is marked PASS.

The previous green run `40103616750` is historical evidence only; it predates the current Rework export implementation and is not sufficient to verify this HEAD.

## 7. Closure sequence

1. Verify CI run `40251750826`.
2. Close B0-03 with transaction-before-read + serialization and two-session tests.
3. Refactor B0-01 so all stock mutation goes through `IStockMovementService.Export`.
4. Run SQL rollback/FIFO/QR/PART/multi-LOT tests.
5. Run representative DBA-schema integration (G4).
6. Run real two-session concurrency (G5).
7. Run full E2E reconciliation (G6).
8. Update this gate with exact evidence and only then permit release.
