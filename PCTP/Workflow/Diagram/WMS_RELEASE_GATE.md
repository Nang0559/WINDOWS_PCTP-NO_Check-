# WMS — RELEASE GATE / END-TO-END ACCEPTANCE CONTRACT

> Status: **NOT READY / BLOCKED**
>
> Branch: `feature/wms-end-to-end-consistency`
>
> This document is the release contract for the WMS end-to-end wave. It deliberately does **not** infer PASS from the existence of a class, interface, migration, source-contract test, or successful SQL-engine test.

## 1. Release decision

The branch may be promoted to `Ready` only when all gates G1–G6 are evidenced against the same code revision and the same database/schema revision used by the business test.

| Gate | Meaning | Required evidence | Current |
|---|---|---|---|
| G1 | Source/architecture contract | Static scan proving forbidden stock writers are absent or explicitly allow-listed | 🟡 |
| G2 | SQL behavior | Isolated SQL tests for movement, constraints and rollback | 🟢/🟡 |
| G3 | Transaction/state | Real workflow tests proving state transition + stock + audit are atomic | 🔴 |
| G4 | Real schema integration | Business tests against DBA-provided representative schema | 🔴 |
| G5 | Concurrency | Two-session tests proving no over-export/over-delivery and no double mutation | 🔴 |
| G6 | End-to-end | NHAP → QC → SLOT/LOT → XUAT → GIAO → ledger/history reconciliation | 🔴 |

**Release rule:** any P0 blocker, any unresolved stock-owner ambiguity, any unverified transaction boundary, or any missing concurrency evidence keeps the release gate RED.

## 2. P0 blockers that must be closed

### B0-02 — Rework export guard is not on the runtime path

`IReworkPhase4Service.EnsureCanExport(...)` exists, but the reviewed `ReworkStockService.XuatKhoRework(...)` path does not invoke it before stock mutation.

Required runtime order:

```text
XuatKhoRework
  → idempotency lookup
  → transaction / business lock
  → EnsureCanExport
  → validate SlotLot / LOT / quantity
  → IStockMovementService.Export
  → write workflow ledger + history
  → commit
```

The guard must fail closed and must use the same transaction/concurrency boundary as the stock mutation.

### B0-03 — Rework delivery quantity must be read under the transaction/lock

Any read of `GetXuat` / delivered quantity used to decide whether another delivery is permitted must happen after the transaction and business lock are acquired.

The acceptance test must prove:

```text
Session A: read remaining → reserve/write → commit
Session B: waits → rereads remaining → rejects when limit is exhausted
```

A transaction without a serialization mechanism is not sufficient evidence for this gate.

### B0-01 — Customer delivery has one stock owner

`Giao Hàng Khách` must not directly mutate `STOCKTP` through legacy procedures/repositories and then mutate `SlotLot` separately.

Target architecture:

```text
Giao Hàng Khách
      ↓
IStockMovementService.Export
      ↓
STOCKTP + SlotLot + movement/idempotency
      ↓
workflow document + history
```

Before removing the legacy path, parity must be demonstrated for FIFO, QR, PART, multi-LOT, partial LOT and rollback.

## 3. Mandatory business invariants

1. Rework export never exceeds Initial QC `SoLuongRework`.
2. Rework delivery never exceeds the amount approved by Initial QC.
3. A retry with the same business operation does not mutate stock twice.
4. A failed movement leaves STOCKTP, SlotLot, workflow ledger and history unchanged.
5. Active stock only can be exported; quarantine/NG rows cannot be treated as available stock.
6. STOCKTP and SlotLot remain consistent after every committed stock mutation.
7. FIFO/QR/PART/multi-LOT allocation remains behaviorally equivalent during the Customer Delivery migration.
8. Every committed movement has exactly one authoritative audit/history record.

## 4. Mandatory concurrency matrix

| Scenario | Session A | Session B | Expected |
|---|---|---|---|
| Rework limit 10 | export 8 | export 8 | exactly one succeeds; committed total = 8 |
| Rework limit 10 | export 10 | retry same operation | one business mutation; retry is idempotent |
| Delivery limit 10 | deliver 8 | deliver 8 | exactly one succeeds; committed total <= 10 |
| Customer delivery same LOT | export 6 | export 6 | no negative/over-allocated stock |
| Same QR | export same QR | export same QR | one mutation only |

## 5. Evidence identity

Every evidence report must record:

```text
Repository: Nang0559/WINDOWS_PCTP-NO_Check-
Branch: feature/wms-end-to-end-consistency
Code SHA: <exact tested commit>
Schema SHA/version: <exact database package>
Test run: <CI/local run identifier>
Generated: <UTC timestamp>
```

Evidence from an older SHA must never be carried forward as proof for a newer SHA without rerunning the affected gate.

## 6. Documentation state vocabulary

Do not use ambiguous `DONE` for release claims.

Use only:

- `🟢 VERIFIED` — implementation + required evidence are complete at the recorded SHA.
- `🟡 IMPLEMENTED / NOT VERIFIED` — code exists, but required evidence is missing.
- `🔴 BLOCKED` — a known defect or missing prerequisite prevents verification.
- `⚪ PLANNED` — not implemented.

A roadmap item may be `IMPLEMENTED / NOT VERIFIED` even when its coding tasks are complete.

## 7. Single source of truth for SQL

Authoritative stored procedures and schema scripts belong under `SQL/`.

`PCTP/Workflow/Diagram` may contain design notes, acceptance criteria and evidence references, but must not become a second authoritative copy of executable SQL.

Legacy/no-extension SP snapshots should be removed or explicitly marked as immutable evidence snapshots with a recorded source SHA. They must not be mistaken for deployable SQL.

## 8. Final acceptance sequence

1. Close B0-02.
2. Close B0-03 with a real two-session test.
3. Refactor B0-01 to the single stock owner and prove parity.
4. Obtain DBA schema-only package.
5. Run G4 on the representative schema.
6. Run G5 concurrency tests.
7. Run G6 complete workflow reconciliation.
8. Re-run Windows solution build on the final SHA.
9. Regenerate all evidence with that SHA.
10. Freeze documentation and change status to `🟢 VERIFIED` only after all gates pass.
