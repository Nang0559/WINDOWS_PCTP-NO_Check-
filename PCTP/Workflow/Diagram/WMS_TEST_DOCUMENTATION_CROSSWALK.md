# WMS — Test Documentation Crosswalk / Bộ tài liệu chuẩn kiểm thử

Branch kiểm thử: `feature/wms-live-data-test-harness`

Đây là crosswalk chính thức giữa bộ thiết kế trong `PCTP/Workflow/Diagram` và `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md`. Nó không tự tạo PASS và không thay thế evidence chạy thật.

**Normative amendment:** `PCTP/Workflow/Diagram/WMS_DOCUMENTATION_NORMATIVE_AMENDMENT_2026-10-30.md` giải thích việc quy đổi G7 legacy → G6 TestCaseId và là tài liệu bắt buộc khi đọc các section G7 cũ trong Master Spec.

## 1. Thứ bậc tài liệu

| Lớp | Nguồn | Vai trò |
|---|---|---|
| A | `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md` | Safety boundary, DB14/DB57, fixture, UI/procedure, assertions, cleanup, promotion |
| A+ | `WMS_DOCUMENTATION_NORMATIVE_AMENDMENT_2026-10-30.md` | Giải quyết legacy G7 wording, naming và precedence cho test documentation |
| B | `PCTP/Workflow/Diagram/WMS_RELEASE_GATE.md` | **Nguồn định nghĩa duy nhất G1–G6** |
| C | `WORKFLOW_WMS.md`, `WORKFLOW_NHAPKHO.md`, `WORKFLOW_KHOCORE.md`, `WORKFLOW_XUATKHO.md`, `WORKFLOW_GIAOHANGKHACH.md`, `WORKFLOW_HANGLOI.md`, `XULYHANGLOI_DESIGN.md` | Expected actor, transition, business outcome |
| D | `ARCHITECTURE_DEPENDENCIES.md`, `WMS_MODULE_CONTRACTS.md`, `WMS_DATA_OWNERSHIP.md`, `WMS_STOCK_STATE_MACHINE.md`, `WMS_STOCK_TRANSACTION_RULES.md` | Data ownership, stock invariant, transaction/rollback/idempotency |
| E | `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `GIAOHANGKHACH_USER_GUIDE.md`, `DOCQR_PHASE6_AUDIT.md`, `WMS_B0_01_DELIVERY_PARITY.md` | Delivery, MP/SP, QR, FIFO, label/configuration parity |
| F | `BAOCAO_TRACEABILITY_DATA_MODEL.md`, `BAOCAO_TRACEABILITY_SOURCE_INVENTORY.md`, `BAOCAO_LEGACY_REPORTING_INVENTORY.md` | Traceability/cardinality/read-only reporting |
| G | `WMS_IMPLEMENTATION_AUDIT.md`, `WMS_OVERALL_STATUS.md`, `WMS_REFACTOR_ROADMAP.md`, `GIAOHANGKHACH_FUNCTIONAL_HARDENING_PROGRESS.md`, `PHASE4_9_COMPLETION_STATUS.md` | Implementation debt/status; not evidence |
| H | `Dashboard.md`, `WMS_USER_GUIDE.md`, `XULYHANGLOI_USER_GUIDE.md` | UI operation/navigation |
| I | `WMS_TEST_CASE_CATALOG.md`, `WMS_TEST_EXECUTION_STATUS.md` | TestCaseId, execution status, candidate-SHA evidence ledger |

Khi tài liệu mâu thuẫn: giữ safety boundary của Master Spec → áp dụng amendment cho legacy naming → dùng workflow/contract làm expected behavior → inspect code/schema/procedure → ghi gap/decision → tạo test/evidence. Không lấy status từ branch/SHA khác.

## 2. Canonical G1–G6

| Gate | Canonical meaning | TestCaseId families |
|---|---|---|
| G1 | Requirement → code → SQL → transaction → test → evidence; gap disposition | `G1-TRC-*` |
| G2 | Exact-SHA unit/regression + production compile | `G2-UT-*`, `G2-BLD-*` |
| G3 | Isolated SQL integration using implementation/repository code + assertions/rollback | `G3-SQL-*` |
| G4 | Production-like schema/procedure compatibility, isolated + rollback | `G4-SCHEMA-*`, `G4-SP-*` |
| G5 | **Two-session concurrency**, shared business key/quota, serialization, rollback injection | `G5-CONC-*` |
| G6 | Full business E2E: real UI/procedure → DB/ledger/history assertions → cleanup | all `G6-*` below |

**Important:** old execution scripts may use G5/G6 labels for navigation/phase names. Those labels are not gate definitions. Receiving is not canonical G5. `G7-*` is legacy naming and is retired below.

## 2A. Design-source coverage contract

The following design areas must have at least one explicit TestCaseId family and a runtime assertion path. A row without a testcase is a traceability gap.

| Design area | Required source documents | Required TestCaseId family | Minimum assertion boundary |
|---|---|---|---|
| WMS orchestration | `WORKFLOW_WMS.md` | `G6-*` | actor/transition/outcome |
| Receiving | `WORKFLOW_NHAPKHO.md`, `WORKFLOW_KHOCORE.md` | `G6-REC-*` | QR → stock → location → history |
| Stock ownership | `WMS_DATA_OWNERSHIP.md`, `WMS_STOCK_TRANSACTION_RULES.md` | `G3-*`, `G5-*`, relevant `G6-*` | atomic mutation/rollback/audit |
| Stock state machine | `WMS_STOCK_STATE_MACHINE.md` | `G6-PICK-*`, `G6-A0-*`, `G6-DEL-*`, `G6-DEF-*`, `G6-REP-*` | legal state transition |
| Pickup/A0/export | `WORKFLOW_XUATKHO.md` | `G6-PICK-*`, `G6-A0-*`, `G6-FIFO-*` | location + quantity + history |
| Customer delivery | `WORKFLOW_GIAOHANGKHACH.md`, `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md` | `G6-DEL-*`, `G6-CFG-*` | customer/order/QR/LOT/quantity |
| QR/label | `DOCQR_PHASE6_AUDIT.md` | `G6-MP-*`, `G6-SP-*`, relevant `G6-DEL-*` | QR and label rule actual vs expected |
| Delivery parity | `WMS_B0_01_DELIVERY_PARITY.md` | `G6-FIFO-*`, `G6-DEL-*` | P1–P13 parity, idempotency, rollback |
| Defect/QC | `WORKFLOW_HANGLOI.md`, `XULYHANGLOI_DESIGN.md` | `G6-QC-*`, `G6-DEF-*` | status/quantity/location/reference/history |
| Replacement | defect + delivery workflows | `G6-REP-*` | original linkage + stock + delivery |
| Reconciliation | Master Spec + stock/transaction contracts | `G6-RECNC-*` | stock/location/QR/delivery/history + cleanup |

This table is a **coverage obligation**, not a claim that the current implementation is complete.

## 3. Scenario/TestCase crosswalk

| TestCaseId | Gate | Scenario | Design source | Implementation/evidence |
|---|---|---|---|---|
| `G1-TRC-001` | G1 | Candidate-SHA traceability matrix | `WMS_RELEASE_GATE.md` | `Build-TraceabilityMatrix.py`, CI artifact |
| `G2-UT-001` | G2 | Unit/regression suite | `WMS_RELEASE_GATE.md` | `PCTP.QC.Tests` |
| `G2-BLD-001` | G2 | Full production WinForms build | `WMS_RELEASE_GATE.md` | Windows build evidence required |
| `G3-SQL-001` | G3 | Delivery command/QR allocation isolated SQL path | `WMS_STOCK_TRANSACTION_RULES.md`, delivery design | `PCTP.QC.SqlIntegration` |
| `G3-SQL-002` | G3 | Rework movement/rollback implementation path | `XULYHANGLOI_DESIGN.md` | `ProductionReworkRepositorySqlScenario`, related SQL scenarios |
| `G4-SP-001` | G4 | Representative delivery procedure compatibility + rollback | `WMS_B0_01_DELIVERY_PARITY.md` | G4 disposable DB acceptance |
| `G5-CONC-001` | G5 | Two sessions export same rework quota | `WMS_RELEASE_GATE.md`, rework design | `ReworkTwoSessionScenario` + real transaction evidence |
| `G5-CONC-002` | G5 | Two sessions deliver same remaining rework quota | `WMS_RELEASE_GATE.md`, rework design | SQL two-session scenario + before/after |
| `G5-CONC-003` | G5 | Same OperationId retry / no double mutation | stock transaction/idempotency contracts | SQL + source contract + runtime |
| `G5-CONC-004` | G5 | Injected failure after first ledger/movement write | transaction contracts | rollback evidence |
| `G6-REC-001` | G6 | Receive valid production carton/total QR | `WORKFLOW_NHAPKHO.md`, `WORKFLOW_KHOCORE.md` | `journeys/g6/g6-receive.json` |
| `G6-REC-002` | G6 | Duplicate/invalid receive QR | receive workflow + stock rules | planned/implement only after schema verified |
| `G6-PICK-001` | G6 | **Slot → staging/A0 → delivery** | `WORKFLOW_XUATKHO.md`, `WORKFLOW_KHOCORE.md` | planned; requires real pickup controls/procedure |
| `G6-PICK-002` | G6 | Pickup cancel/restore | `WORKFLOW_XUATKHO.md` | planned |
| `G6-A0-001` | G6 | **Direct A0 → delivery**, no pickup | `WORKFLOW_XUATKHO.md`, delivery design | planned |
| `G6-A0-002` | G6 | Direct A0 duplicate QR/idempotency | delivery/QR contracts | planned |
| `G6-DEL-001` | G6 | One pending delivery / one QR | delivery functional design | `journeys/g6/g6-delivery-hvn.json` |
| `G6-DEL-002` | G6 | Multi-LOT delivery / six QR inputs | delivery/FIFO design | `g6-delivery-hvn-multilot.json` |
| `G6-MP-001` | G6 | MP — one label | `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `DOCQR_PHASE6_AUDIT.md` | planned; source/config provenance required |
| `G6-MP-002` | G6 | MP — two labels | same | planned |
| `G6-SP-001` | G6 | SP — one label | same | planned |
| `G6-SP-002` | G6 | SP — two labels | same | planned |
| `G6-CFG-001` | G6 | CustomerTableConfig exact match | delivery design/config inventory | planned; must execute real config lookup |
| `G6-CFG-002` | G6 | Missing/no-match CustomerTableConfig | delivery design | planned; expected validation/error |
| `G6-FIFO-001` | G6 | FIFO multi-LOT / partial LOT | delivery parity | planned |
| `G6-DEF-001` | G6 | Internal defect | `WORKFLOW_HANGLOI.md`, `XULYHANGLOI_DESIGN.md` | `g6-qc-direction` is incomplete; real UI + SQL assert required |
| `G6-DEF-002` | G6 | Customer-originated defect | same | planned |
| `G6-QC-001` | G6 | QC direction | rework/QC workflow | `g6-qc-direction.json` currently `configured:false` |
| `G6-QC-002` | G6 | Final QC | rework/QC workflow | `g6-qc-final.json` currently `configured:false` |
| `G6-REP-001` | G6 | Valid replacement/giao bù linked to original delivery/defect | `WORKFLOW_HANGLOI.md`, delivery workflow | planned |
| `G6-REP-002` | G6 | Duplicate/orphan replacement rejected | same | planned |
| `G6-RECNC-001` | G6 | Final stock/location/QR/delivery/history reconciliation | all relevant contracts | planned |
| `G6-RECNC-002` | G6 | Dependency-safe cleanup + Remaining=0 | Master Spec | implemented in runner; runtime evidence required |

## 4. Legacy G7 mapping — no seventh gate

Các mã cũ được quy đổi như sau:

| Legacy | Canonical TestCaseId | Canonical Gate |
|---|---|---|
| `G7` / “Nhập kho” | `G6-REC-001` | G6 |
| `G7-A` / “Pickup → staging/A0 → delivery” | `G6-PICK-001` | G6 |
| `G7-B` / “Direct A0 → delivery” | `G6-A0-001` | G6 |

Từ change set này, tài liệu mới, report và artifact **không được tạo `G7-*`**. Nếu một legacy document còn chữ G7 thì phải hiểu theo bảng quy đổi trên và `WMS_DOCUMENTATION_NORMATIVE_AMENDMENT_2026-10-30.md` cho đến khi tài liệu đó được sửa.

## 5. Chuẩn TestCase bắt buộc

Mỗi TestCaseId phải có:

- Gate + exact candidate SHA.
- Requirement/design source + section.
- Source shape hoặc `SYNTHETIC_DB57`.
- Preconditions + run prefix.
- UI/procedure/service actions thật.
- SQL seed chỉ tạo input/config; SQL observe/assert sau nghiệp vụ.
- Expected + actual + assertion IDs.
- Cleanup + verify cleanup.
- Evidence run ID/artifact.
- Status: `PASS`, `FAIL`, `INCOMPLETE`, `BLOCKED`, `NOT VERIFIED`, `NOT AVAILABLE IN SOURCE DATA`.
- Root cause nếu fail: `CODE BUG`, `SQL/PROCEDURE BUG`, `DATA-MODEL BUG`, `TEST-HARNESS BUG`, `CONFIGURATION BUG`, `NOT AVAILABLE IN SOURCE DATA`.

## 6. Evidence rule

Một manifest tồn tại không phải PASS. Một worklist hiển thị không phải PASS. Seed thành công không phải PASS. Build/unit/isolated SQL xanh không phải G6 PASS.

G6 chỉ PASS khi UI/procedure thật đã thực hiện nghiệp vụ, hậu điều kiện DB/ledger/history được assert, final reconciliation đạt và cleanup được verify.
