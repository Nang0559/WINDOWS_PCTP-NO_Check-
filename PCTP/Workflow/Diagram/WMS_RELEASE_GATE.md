# WMS — AUTHORITATIVE RELEASE GATE / END-TO-END ACCEPTANCE CONTRACT

> **Canonical scope:** this file defines the meaning of G1–G6 only. It is **not** the execution ledger and must not contain status copied from another branch or old candidate SHA.
>
> **Current branch:** `feature/wms-live-data-test-harness`
> **Current documentation baseline:** `3556cfc3729b7c776bdc09a91b4e5786f93d8475` (2026-10-30)
> **Gate owner:** this document. `WMS_TEST_EXECUTION_STATUS.md` is the evidence/status ledger and MUST reference an exact candidate SHA + workflow run/artifact.

No PASS may be inferred from class existence, source-contract tests, SQL seed success, isolated build success, or a UI runner build alone.

## 1. Canonical gate matrix

| Gate | Single authoritative definition | Required evidence boundary |
|---|---|---|
| **G1** | Traceability/source ownership: requirement → design → code → SQL/procedure → transaction → TestCaseId → evidence; every gap has disposition | Exact candidate SHA, regenerated matrix, gap disposition/owner/approved exception, evidence links |
| **G2** | Unit/regression tests on the exact candidate SHA, including required production project compile items | Exact-SHA test result and production build evidence |
| **G3** | SQL-engine integration using implementation/repository code against an isolated SQL Server, with assertions and rollback | Code path executed, postconditions asserted, rollback verified; disposable CI DB only |
| **G4** | Production-like schema/procedure compatibility on an isolated rollback-only database | Representative schema/procedure compatibility evidence; no production write |
| **G5** | **Two-session concurrency** on shared business keys/quotas, including lock serialization, no-double-consumption and injected rollback | Two independent sessions, before/after assertions, lock/serialization evidence, rollback evidence |
| **G6** | **Full business E2E:** real UI/procedure → real business mutation → DB/ledger/history assertions → reconciliation → cleanup | Live journey evidence on approved DB57 target, exact SHA/run, assertions and verified cleanup |

### 1.1 Gate ownership rules

1. G1–G6 are **gates**, not workflow phase names.
2. Receiving, pickup, direct-A0 delivery, MP/SP, defect and replacement are **G6 scenarios**; they do not create new gates.
3. `G7`, `G7-A`, `G7-B` are retired legacy scenario names. New documents and artifacts MUST NOT create `G7-*` IDs. Use `G6-REC-*`, `G6-PICK-*`, `G6-A0-*` as defined by the crosswalk.
4. If an execution script uses a different numeric phase label, that label is navigation only and does not redefine G1–G6.
5. A gate can be `PASS` only when its required evidence boundary is satisfied on the same candidate SHA. Historical evidence from another SHA is never inherited.

## 2. Canonical TestCaseId families

| Gate | TestCaseId family | Scope |
|---|---|---|
| G1 | `G1-TRC-*` | traceability and gap disposition |
| G2 | `G2-UT-*`, `G2-BLD-*` | unit/regression and production compile |
| G3 | `G3-SQL-*` | isolated implementation/repository SQL integration |
| G4 | `G4-SCHEMA-*`, `G4-SP-*` | production-like schema/procedure compatibility |
| G5 | `G5-CONC-*` | two-session concurrency, idempotency, rollback |
| G6 | `G6-REC-*`, `G6-PICK-*`, `G6-A0-*`, `G6-DEL-*`, `G6-MP-*`, `G6-SP-*`, `G6-CFG-*`, `G6-FIFO-*`, `G6-DEF-*`, `G6-QC-*`, `G6-REP-*`, `G6-RECNC-*` | business E2E scenarios and final reconciliation |

The authoritative scenario-to-design mapping is `WMS_TEST_DOCUMENTATION_CROSSWALK.md`; the authoritative execution state is `WMS_TEST_EXECUTION_STATUS.md`; the authoritative acceptance boundary is `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md`.

## 3. Release blockers

The following are release blockers until exact-SHA evidence closes them:

- **G1:** unresolved traceability gaps without owner/disposition/approved exception.
- **G2:** missing exact-SHA production compile or regression evidence.
- **G4:** missing representative schema/procedure compatibility evidence where required by the candidate.
- **G5:** no real two-session evidence for shared quota/business-key concurrency and rollback.
- **G6:** missing real business journey, reconciliation, or cleanup evidence.
- **B0-01:** legacy customer-delivery stock writers remain until delivery parity/cutover is proven.
- Any violation of the DB14 read-only / DB57-only-write safety boundary.

## 4. Mandatory stock and transaction invariants

1. `STOCKTP` and active `SlotLot` quantities reconcile atomically.
2. Approved workflows do not write stock outside the authoritative stock-movement owner.
3. Rework export never exceeds approved Rework quantity.
4. Rework delivery never exceeds approved/exported quantity.
5. The same business operation cannot mutate stock twice.
6. Failure rolls back stock, ledger, state and history together.
7. Quarantine/NG stock is never treated as active export stock.
8. Every committed movement has one authoritative audit/history record.
9. Customer delivery preserves QR/PART/FIFO, multi-LOT and partial-LOT semantics.
10. Cleanup is limited to run-owned identities and is verified after execution.

## 5. Evidence status vocabulary

| Status | Meaning |
|---|---|
| `PASS` | Required evidence exists for the exact candidate SHA and all mandatory assertions pass |
| `FAIL` | The required execution occurred and one or more assertions failed |
| `INCOMPLETE` | The scenario exists but required steps/evidence are missing |
| `BLOCKED` | Execution cannot proceed because a prerequisite or safety condition is unresolved |
| `NOT VERIFIED` | Source/implementation exists, but runtime evidence for the required boundary is absent |
| `NOT AVAILABLE IN SOURCE DATA` | The source shape does not exist in DB14; use an explicitly marked DB57 synthetic-input case if policy permits |
| `PLANNED` | Not implemented |

`IMPLEMENTED` is an implementation state, not a gate PASS state.

## 6. Safety boundary for live acceptance

The release gate follows the Master Spec without exception:

- DB14 (`192.168.200.14\\BRAVO / B7R2_FCC`) is **read-only**.
- DB57 (`192.168.200.57 / B7R2_FCC`) is the only approved shared test-write target.
- SQL may extract source data, create prerequisite input/configuration on DB57, observe/assert and perform run-scoped cleanup.
- SQL MUST NOT fake scan/receive/delivery/QC/defect/replacement/history outcomes.
- Live acceptance must use the real UI/procedure/service path.
- No credentials or secrets may be written to source, logs or artifacts.

## 7. Required closure sequence

1. Regenerate G1 traceability for the **current candidate SHA** and classify every gap.
2. Run G2 on the same candidate SHA, including the required production compile.
3. Run G3 isolated SQL implementation integration and retain assertions/rollback evidence.
4. Run G4 representative schema/procedure compatibility where applicable.
5. Run G5 two-session concurrency for shared quotas/business keys, including rollback injection.
6. Complete B0-01 delivery ownership/parity before claiming customer-delivery E2E closure.
7. Execute G6 real UI/procedure journeys: receive → pickup/direct-A0 → delivery → QC/defect → replacement → reconciliation → cleanup, according to the TestCase catalog and source-data availability.
8. Update `WMS_TEST_EXECUTION_STATUS.md` with exact SHA, run ID, artifact and evidence links. Do not change this canonical definition merely to reflect a new status.

## 8. Document governance

The WMS documentation chain is:

```text
Master Spec (safety / acceptance boundary)
        ↓
WMS_RELEASE_GATE.md (canonical G1–G6 definitions)
        ↓
WMS_TEST_DOCUMENTATION_CROSSWALK.md (design → TestCaseId → assertion/evidence)
        ↓
WMS_TEST_CASE_CATALOG.md (scenario inventory)
        ↓
WMS_TEST_EXECUTION_STATUS.md (exact-SHA execution/evidence ledger)
        ↓
CI / DB57 runtime artifacts (actual evidence)
```

Supporting design documents remain authoritative for expected business behavior, including workflow, data ownership, stock state machine, transaction rules, delivery/QR/FIFO, QC/defect and replacement documents. They are not replaced by the test documents.

When documents conflict, apply this order:

1. Master Spec safety boundary.
2. Workflow/functional design and data/transaction contracts for expected behavior.
3. Current code/schema/procedure inspection.
4. Crosswalk/TestCaseId mapping.
5. Runtime evidence for PASS/FAIL.

## 9. What this document intentionally does not contain

This file MUST NOT contain:

- a copied status snapshot from another branch;
- a historical candidate SHA presented as the current candidate;
- gap counts without an exact artifact/SHA;
- claims that a manifest/worklist/build is G6 PASS;
- a seventh gate.

Dynamic status belongs in `WMS_TEST_EXECUTION_STATUS.md` and `docs/QC_GATE_STATUS.md`.
