# WMS Master Spec Amendment 001 — G1–G6 / Scenario Naming / TestCaseId

**Applies to:** `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md` on `feature/wms-live-data-test-harness`.

**Purpose:** remove ambiguity between canonical release gates and legacy scenario labels without weakening any safety or acceptance requirement.

## 1. Canonical gate rule

The only gate definitions are G1–G6 from `PCTP/Workflow/Diagram/WMS_RELEASE_GATE.md`:

- G1 — traceability/source ownership.
- G2 — unit/regression + exact candidate build.
- G3 — isolated SQL implementation integration.
- G4 — production-like schema/procedure compatibility on isolated DB.
- G5 — two-session concurrency, shared business key/quota, serialization and rollback injection.
- G6 — full business E2E: real UI/procedure → DB/ledger/history assertions → cleanup, including delivery/reconciliation.

There is **no G7 release gate**.

## 2. Retirement of legacy G7 scenario labels

The existing Master Spec sections named `G7`, `G7-A`, and `G7-B` are scenario labels, not release gates. For all new test documentation and execution reports they are replaced by:

| Old label | New scenario ID | Gate |
|---|---|---|
| G7 / Nhập kho | `G6-REC-001` | G6 |
| G7-A / Pickup → staging/A0 → delivery | `G6-PICK-001` | G6 |
| G7-B / Direct A0 → delivery | `G6-A0-001` | G6 |

No new document may introduce another `G7-*` identifier.

## 3. TestCaseId is mandatory

The normative executable mapping is `PCTP/Workflow/Diagram/WMS_TEST_CASE_CATALOG.md`.

Every scenario referenced by the Master Spec must have a stable TestCaseId, exact candidate SHA, expected result, actual result, assertions, cleanup result and evidence run ID before it can be marked PASS.

## 4. Canonical dashboard mapping

`docs/QC_GATE_STATUS.md` is a dashboard only. It must not reinterpret execution-script phase labels as gate definitions.

If an old script reports “G5 receiving”, that is an execution phase label. It must be reported as `Gate=G6`, `TestCaseId=G6-REC-*` in acceptance evidence.

## 5. Traceability number rule

`docs/TRACEABILITY_MATRIX.md`, `PCTP.QC.UI/G1-gaps.csv` and CI-generated traceability artifacts are candidate-SHA snapshots. Their counts must never be mixed.

For the latest verified CI evidence at the time this amendment was created:

- Run: `40910630446`
- Candidate SHA: `b1a807fbdcef599c734b8a8e77d7f12dbfd5a4c3`
- G1 artifact: `pctp-g1-traceability-evidence`
- Code rows: 145
- Code rows with gaps: 99
- SQL scripts: 68
- Total gaps: 229
- G1 status: **FAIL / OPEN**

This does not make the checked-in static matrix numbers at another SHA incorrect; it means they are not comparable snapshots.

## 6. G6 current implementation boundary

The receive and one/multi-LOT delivery manifests now contain real UI QR input/Enter actions and post-scan assertions. This is source implementation evidence only.

Still required before G6 PASS:

- actual Windows self-hosted UI execution against approved DB57;
- SQL before/after assertions;
- no unexpected UI error/modal;
- final reconciliation;
- cleanup verification;
- QC direction/final QC journeys implemented and executed;
- pickup→staging/A0 and direct-A0 delivery scenarios;
- MP/SP 1-label/2-label and CustomerTableConfig scenarios;
- internal/customer defect and replacement scenarios.

## 7. Safety unchanged

DB14 remains read-only. DB57 is the only test-write target. SQL cannot create fake scan/delivery/QC/defect/replacement/history outcomes. Isolation guard remains enabled. Missing source shape is handled with explicit synthetic DB57 input/configuration, never by mutating DB14.
