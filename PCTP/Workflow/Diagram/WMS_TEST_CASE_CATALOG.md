# WMS — Test Case Catalog

> Catalog thực thi chuẩn cho `feature/wms-live-data-test-harness`. Gate meaning lấy từ `WMS_RELEASE_GATE.md`; scenario naming không tạo gate mới.

## A. G1 — Traceability

| TestCaseId | Preconditions | Action | Expected | Evidence |
|---|---|---|---|---|
| G1-TRC-001 | Candidate SHA fixed | Generate traceability matrix | Every row has source/test/evidence or classified gap | `gate-summary.json`, `gaps.csv` |
| G1-TRC-002 | Gap inventory available | Review each gap and assign disposition | No unowned/undispositioned release gap | triage artifact |

## B. G2 — Unit / regression / build

| TestCaseId | Preconditions | Action | Expected | Evidence |
|---|---|---|---|---|
| G2-UT-001 | Exact SHA checkout | `dotnet test PCTP.QC.Tests` | 0 failed/skipped where gate requires full suite | TRX |
| G2-BLD-001 | Windows runner | Build production WinForms `PCTP.csproj` | Compile succeeds at exact SHA | build log |
| G2-UI-001 | Windows runner | Parse/validate UI runner PowerShell + manifests | No parse/schema/guard error | CI log |

## C. G3 — Isolated SQL implementation integration

| TestCaseId | Action | Expected |
|---|---|---|
| G3-SQL-001 | Execute delivery implementation/repository scenario against disposable SQL | Real implementation path, data assertions and rollback pass |
| G3-SQL-002 | Execute rework movement implementation scenario | Movement/ledger/history are atomic |
| G3-SQL-003 | Inject failure after movement | Stock + ledger + history roll back |

## D. G4 — Production-like schema/procedure compatibility

| TestCaseId | Action | Expected |
|---|---|---|
| G4-SCHEMA-001 | Generate isolated schema from verified DB57/SQL2 metadata | Required dependencies deploy without drift |
| G4-SP-001 | Run representative delivery procedure acceptance | Procedure executes against isolated schema and rollback leaves clean state |
| G4-SP-002 | Run deterministic delivery fixture | Exact expected stock/QR/delivery mutation, then cleanup = 0 |

## E. G5 — Two-session concurrency

G5 is **not** receiving. It is exclusively concurrency.

| TestCaseId | Session setup | Expected |
|---|---|---|
| G5-CONC-001 | A/B export same rework ticket/quota | Combined committed export ≤ approved quota; no double consume |
| G5-CONC-002 | A/B deliver same remaining rework quantity | Combined committed delivery ≤ remaining/approved quantity |
| G5-CONC-003 | A/B same OperationId | Exactly one business mutation |
| G5-CONC-004 | Session A fails after first ledger/movement mutation | Transaction rollback removes all partial state |
| G5-CONC-005 | A/B same QR delivery | One QR cannot create two committed deliveries |

Required evidence: session timeline, transaction/lock resource, before/after quantities, committed rows, rollback proof.

## F. G6 — Receiving E2E

### G6-REC-001 — valid receive

Current source implementation exists in `PCTP.QC.UI/journeys/g6/g6-receive.json`.

Required flow:

1. DB57 seed creates only production input/worklist + unread QR.
2. UI opens `FormEnterItemSV`.
3. UI submits generated QR values through `receive_qrInput` + Enter.
4. Assert STOCKTP, SlotLot, StockHistory and quantity reconciliation.
5. Cleanup + `Remaining = 0`.

Status: **IMPLEMENTED IN SOURCE / NOT VERIFIED AT RUNTIME**.

### G6-REC-002 — duplicate receive

Seed an input that can be submitted twice, but do not pre-write the result. Submit the same QR twice through UI. Expected: second submission is rejected/idempotent according to the actual receiving contract; no double quantity/history.

Status: **PLANNED — requires verified UI/business rule before automation**.

### G6-REC-003 — invalid receive QR

Submit malformed/nonexistent QR through the real UI. Expected: validation/error without stock mutation.

Status: **PLANNED**.

## G. G6 — Delivery paths

### G6-PICK-001 — slot → staging/A0 → delivery

1. Receive valid input into a real slot.
2. Select/execute pickup using the real UI/procedure.
3. Assert slot quantity decreases and staging/A0 quantity increases according to the actual stock state machine.
4. Execute customer delivery through the real UI/procedure.
5. Assert stock, QR, delivery and history.
6. Cleanup.

Status: **BLOCKED until pickup UI/control and exact procedure contract are verified**. No fake pickup rows may be seeded.

### G6-A0-001 — direct A0 → delivery

1. Prepare a valid A0/virtual-location input through the real receiving procedure or an approved input configuration.
2. Do not execute pickup.
3. Execute delivery directly from A0 through the real UI/procedure.
4. Assert stock, QR, delivery, customer/order link and history.

Status: **BLOCKED until A0 direct-delivery path is verified in source/code/procedure**.

### G6-DEL-001 — one QR delivery

Current source implementation: `journeys/g6/g6-delivery-hvn.json`.

Status: **IMPLEMENTED IN SOURCE / NOT VERIFIED AT RUNTIME**.

### G6-DEL-002 — multi-LOT delivery

Current source implementation: `journeys/g6/g6-delivery-hvn-multilot.json`.

Required: each QR must be captured in-memory and entered through the real UI; no DOCQRCODE/LUU result rows may be seeded as if scanned.

Status: **IMPLEMENTED IN SOURCE / NOT VERIFIED AT RUNTIME**.

## H. G6 — MP/SP / label / CustomerTableConfig

| TestCaseId | Scenario | Required provenance |
|---|---|---|
| G6-MP-001 | MP, 1 tem | Source/config or synthetic DB57 configuration; actual UI/procedure lookup |
| G6-MP-002 | MP, 2 tem | Same |
| G6-SP-001 | SP, 1 tem | Same |
| G6-SP-002 | SP, 2 tem | Same |
| G6-CFG-001 | CustomerTableConfig exact match | Config row + UI/procedure read + actual selected table |
| G6-CFG-002 | Config missing/no match | Real validation/error; no fallback assumption |
| G6-FIFO-001 | Multi-LOT FIFO | Actual LOT allocation/order and final stock reconciliation |
| G6-FIFO-002 | Partial LOT | Exact quantity allocation and remaining LOT balance |

These cases remain **NOT VERIFIED** until a concrete source/synthetic dataset and actual CustomerTableConfig execution evidence are attached.

## I. G6 — QC / defects

| TestCaseId | Scenario | Current |
|---|---|---|
| G6-QC-001 | QC direction | `g6-qc-direction.json` currently configured=false; BLOCKED |
| G6-QC-002 | Final QC | `g6-qc-final.json` currently configured=false; BLOCKED |
| G6-DEF-001 | Internal defect | PLANNED / schema + UI contract verification required |
| G6-DEF-002 | Customer-originated defect | PLANNED / original delivery/customer linkage required |

Do not seed processed QC/defect results. Seed only the input state needed to make the real UI/procedure create them.

## J. G6 — Replacement / giao bù

| TestCaseId | Scenario | Expected |
|---|---|---|
| G6-REP-001 | Valid replacement | Linked to original delivery/customer/request/defect; stock + delivery + history atomic |
| G6-REP-002 | Duplicate replacement | Retry does not double mutate |
| G6-REP-003 | Orphan/invalid replacement | Rejected without mutation |

Status: **NOT VERIFIED / PLANNED** until actual replacement UI/procedure and schema links are verified.

## K. G6 — Reconciliation and cleanup

| TestCaseId | Assertion |
|---|---|
| G6-RECNC-001 | STOCKTP + SlotLot + location quantities reconcile after every transition |
| G6-RECNC-002 | QR/LOT/delivery/customer linkage has no unexpected duplicate/multiplication |
| G6-RECNC-003 | History/ledger contains only transitions produced by the real operation |
| G6-RECNC-004 | Final reconciliation matches business contract; formulas not assumed without source verification |
| G6-RECNC-005 | Cleanup removes only run-owned rows and verifies `Remaining = 0` |
| G6-RECNC-006 | DB14 before/after evidence proves no mutation |

## L. Status policy

- `PASS`: exact SHA + real execution + all required assertions + cleanup evidence.
- `FAIL`: execution occurred and a required assertion failed.
- `INCOMPLETE`: implementation is missing required steps.
- `BLOCKED`: prerequisite code/config/UI/schema is not sufficiently verified.
- `NOT VERIFIED`: source implementation exists but no qualifying runtime evidence yet.
- `NOT AVAILABLE IN SOURCE DATA`: source DB14 lacks the shape; synthetic DB57 case is required if the business branch must still be tested.

A green CI build never upgrades a G6 testcase from `NOT VERIFIED` to `PASS`.
