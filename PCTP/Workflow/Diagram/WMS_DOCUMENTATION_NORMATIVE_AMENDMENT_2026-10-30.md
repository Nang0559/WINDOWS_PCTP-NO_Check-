# WMS — Normative Documentation Amendment 2026-10-30

> **Purpose:** reconcile legacy wording in the Master Spec and older design documents with the canonical G1–G6 gate model. This amendment does not change business behavior; it changes only documentation governance and TestCaseId naming.

## 1. Precedence

This amendment is part of the WMS test-documentation contract and applies to all new test reports/artifacts on `feature/wms-live-data-test-harness`.

- `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md` remains authoritative for safety: DB14 read-only, DB57-only writes, real UI/procedure execution, assertions and cleanup.
- `PCTP/Workflow/Diagram/WMS_RELEASE_GATE.md` is authoritative for the meaning of G1–G6.
- This amendment resolves legacy **G7 wording** in the Master Spec and older documents: G7 is **not a seventh gate**.
- `WMS_TEST_DOCUMENTATION_CROSSWALK.md` is authoritative for design-document → TestCaseId mapping.
- `WMS_TEST_EXECUTION_STATUS.md` is authoritative for dynamic candidate-SHA status and evidence.

## 2. Legacy G7 wording is retained only as scenario history

The Master Spec contains older sections titled `G7`, `G7-A`, and `G7-B`. They are interpreted as follows:

| Legacy wording | Canonical meaning | TestCaseId | Gate |
|---|---|---|---|
| G7 / Nhập kho | Receiving business journey | `G6-REC-001` | G6 |
| G7-A / Pickup → staging/A0 → delivery | Pickup delivery journey | `G6-PICK-001` | G6 |
| G7-B / Direct A0 → delivery | Direct A0 delivery journey | `G6-A0-001` | G6 |

No new document, workflow artifact, testcase catalog row or CI report may create `G7-*` as a gate or new scenario family.

## 3. Required design → test linkage

Every G6 business journey must link to the actual design source that defines expected behavior. At minimum:

| Business area | Design authority | Test family |
|---|---|---|
| WMS overall orchestration | `WORKFLOW_WMS.md` | `G6-*` |
| Receiving | `WORKFLOW_NHAPKHO.md`, `WORKFLOW_KHOCORE.md` | `G6-REC-*` |
| Stock ownership/transaction | `WMS_DATA_OWNERSHIP.md`, `WMS_STOCK_STATE_MACHINE.md`, `WMS_STOCK_TRANSACTION_RULES.md` | G3/G5 + relevant G6 |
| Pickup/A0/export | `WORKFLOW_XUATKHO.md`, `WORKFLOW_KHOCORE.md` | `G6-PICK-*`, `G6-A0-*`, `G6-FIFO-*` |
| Customer delivery | `WORKFLOW_GIAOHANGKHACH.md`, `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `WMS_B0_01_DELIVERY_PARITY.md` | `G6-DEL-*`, `G6-CFG-*`, `G6-MP-*`, `G6-SP-*` |
| QR/label | `DOCQR_PHASE6_AUDIT.md` plus verified implementation/config | `G6-DEL-*`, `G6-MP-*`, `G6-SP-*` |
| Defect/QC | `WORKFLOW_HANGLOI.md`, `XULYHANGLOI_DESIGN.md` | `G6-QC-*`, `G6-DEF-*` |
| Replacement | `WORKFLOW_HANGLOI.md` + delivery workflow | `G6-REP-*` |
| Final reconciliation | Master Spec + stock/transaction contracts | `G6-RECNC-*` |

A TestCaseId without a design source is a traceability gap, not a PASS.

## 4. Status separation

Do not place dynamic gap counts, workflow run IDs or candidate statuses into canonical design/gate definitions.

- Canonical definitions: `WMS_RELEASE_GATE.md`.
- Scenario mapping: `WMS_TEST_DOCUMENTATION_CROSSWALK.md`.
- Scenario inventory: `WMS_TEST_CASE_CATALOG.md`.
- Runtime evidence: `WMS_TEST_EXECUTION_STATUS.md` and CI/DB57 artifacts.

Gap counts such as 105, 208 or 229 are meaningful only when paired with the exact candidate SHA and generated artifact.

## 5. Required closure interpretation

A complete documentation chain is:

`Design source → expected behavior → TestCaseId → precondition/fixture → real UI/procedure action → DB assertion → reconciliation → cleanup → exact-SHA evidence`.

The existence of a manifest, seed, worklist, build, source-contract test or isolated SQL test does not close a G6 business scenario.

## 6. Migration rule for legacy documents

Legacy documents may retain G7 labels temporarily for historical context, but each such occurrence must either:

1. point to this amendment; or
2. be accompanied by the canonical G6 TestCaseId mapping.

New or materially edited documents must use the canonical G1–G6/TestCaseId vocabulary directly.
