# WMS — RELEASE GATE / END-TO-END ACCEPTANCE CONTRACT

> Status: **NOT READY / BLOCKED**
>
> Branch: `feature/wms-end-to-end-consistency`
>
> Current code/documentation HEAD: `94a145536591e4579e177b5debbcee5bc3689b79`
>
> This is the authoritative release contract for the WMS end-to-end wave. It does not infer PASS from the existence of a class, interface, migration, source-contract test, or successful SQL-engine test.

## 1. Current release decision

The branch remains Draft/Blocked. The Rework export guard has now been wired into the runtime export path and the composition root supplies it lazily to avoid the Phase-4 dependency cycle. This is an **implementation change**, not yet a production verification PASS.

| Gate | Meaning | Required evidence | Current |
|---|---|---|---|
| G1 | Source/architecture contract | Static scan proving forbidden stock writers are absent or explicitly allow-listed | 🟡 |
| G2 | SQL behavior | Isolated SQL tests for movement, constraints and rollback | 🟡 |
| G3 | Transaction/state | Real workflow tests proving state transition + stock + audit are atomic | 🔴 |
| G4 | Real schema integration | Business tests against DBA-provided representative schema | 🔴 |
| G5 | Concurrency | Two-session tests proving no over-export/over-delivery and no double mutation | 🔴 |
| G6 | End-to-end | NHAP → QC → SLOT/LOT → XUAT → GIAO → ledger/history reconciliation | 🔴 |

**Release rule:** any P0 blocker, unresolved stock-owner ambiguity, unverified transaction boundary, or missing concurrency evidence keeps the release gate RED.

## 2. P0 blockers / closure status

### B0-02 — Rework export guard

**Implementation: CLOSED. Verification: OPEN.**

`ReworkStockService.XuatKhoRework(...)` now performs, in order:

```text
Begin transaction
  → idempotency lookup by OperationId
  → lock current Rework ticket row in the same transaction
  → EnsureCanExport
  → validate SlotLot / LOT / quantity
  → IStockMovementService.Export
  → write workflow ledger + history
  → commit
```

The Phase-4 service is supplied lazily from `XuLyHangLoiModuleFactory` because Initial QC depends on the Rework trace service. If the guard cannot be resolved, export fails closed.

Required verification remains:

- over-limit export is rejected;
- exact-limit export succeeds;
- retry is idempotent;
- two concurrent sessions cannot both consume the same remaining quota.

### B0-03 — Rework delivery quantity must be read under transaction/lock

**Status: OPEN.**

The delivery path still requires a complete review so all reads of exported/delivered quantities used for authorization occur after the transaction and serialization boundary is acquired.

Required concurrency proof:

```text
Session A: acquire ticket lock → read remaining → write → commit
Session B: waits → rereads remaining → rejects when limit is exhausted
```

A transaction without serialization evidence is not sufficient.

### B0-01 — Customer delivery has one stock owner

**Status: OPEN.**

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

Before removing the legacy path, prove parity for FIFO, QR, PART, multi-LOT, partial LOT and rollback.

## 3. Mandatory business invariants

1. Rework export never exceeds Initial QC `SoLuongRework`.
2. Rework delivery never exceeds the amount approved by Initial QC.
3. A retry with the same business operation does not mutate stock twice.
4. A failed movement leaves STOCKTP, SlotLot, workflow ledger and history unchanged.
5. Active stock only can be exported; quarantine/NG rows cannot be treated as available stock.
6. STOCKTP and SlotLot remain consistent after every committed stock mutation.
7. FIFO/QR/PART/multi-LOT allocation remains behaviorally equivalent during Customer Delivery migration.
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

- `🟢 VERIFIED` — implementation + required evidence complete at the recorded SHA.
- `🟡 IMPLEMENTED / NOT VERIFIED` — code exists, but required evidence is missing.
- `🔴 BLOCKED` — known defect or missing prerequisite prevents verification.
- `⚪ PLANNED` — not implemented.

## 7. Single source of truth for SQL

Authoritative stored procedures and schema scripts belong under `SQL/`.

`PCTP/Workflow/Diagram` may contain design notes, acceptance criteria and evidence references, but must not become a second authoritative copy of executable SQL.

Legacy/no-extension SP snapshots should be removed or explicitly marked as immutable evidence snapshots with a recorded source SHA. They must not be mistaken for deployable SQL.

## 8. Final acceptance sequence

1. Verify B0-02 with unit/source and real DB tests.
2. Close B0-03 with a real two-session delivery test.
3. Refactor B0-01 to the single stock owner and prove parity.
4. Obtain DBA schema-only package.
5. Run G4 on the representative schema.
6. Run G5 concurrency tests.
7. Run G6 complete workflow reconciliation.
8. Re-run Windows solution build on the final SHA.
9. Regenerate all evidence with that SHA.
10. Freeze documentation and change status to `🟢 VERIFIED` only after all gates pass.
