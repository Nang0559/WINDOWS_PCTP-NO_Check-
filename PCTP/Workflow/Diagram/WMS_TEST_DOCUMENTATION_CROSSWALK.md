# WMS — Test Documentation Crosswalk / Bộ tài liệu chuẩn kiểm thử

Branch kiểm thử: `feature/wms-live-data-test-harness`

Tài liệu này nối bộ thiết kế nghiệp vụ/kiến trúc WMS với `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md`. Đây là **bản đồ truy vết và quy tắc điều phối tài liệu**, không thay thế thiết kế gốc, không tự khẳng định implementation đã đúng và không thay thế evidence chạy thật.

**Sổ evidence chạy thật:** xem [`WMS_TEST_EXECUTION_STATUS.md`](WMS_TEST_EXECUTION_STATUS.md). Tài liệu này giữ trạng thái gate theo run/SHA; không lấy checklist hay trạng thái branch khác thay cho evidence.

## 1. Thứ bậc tài liệu và xử lý xung đột

| Lớp | Nguồn | Dùng để quyết định | Không được dùng để kết luận |
|---|---|---|---|
| A — Quy tắc an toàn/acceptance | `doc/PCTP_WMS_LIVE_DATA_ACCEPTANCE_MASTER_SPEC.md` | DB14 read-only, DB57 write-only-for-test, real UI/procedure, fixture isolation, assertions, cleanup, report, promotion gate | Không tự định nghĩa thay mọi chi tiết nghiệp vụ |
| B — Gate nghiệm thu WMS | `WMS_RELEASE_GATE.md` | Định nghĩa G1–G6 và điều kiện release | Không tự chuyển trạng thái gate hoặc evidence từ branch/SHA khác sang branch hiện tại |
| C — Workflow nghiệp vụ | `WORKFLOW_WMS.md`, `WORKFLOW_NHAPKHO.md`, `WORKFLOW_KHOCORE.md`, `WORKFLOW_XUATKHO.md`, `WORKFLOW_GIAOHANGKHACH.md`, `WORKFLOW_HANGLOI.md`, `XULYHANGLOI_DESIGN.md` | Actor, bước nghiệp vụ, nhánh, state transition và business outcome cần kiểm thử | Không chứng minh code hiện tại thực hiện đúng |
| D — Contract/invariant kiến trúc | `ARCHITECTURE_DEPENDENCIES.md`, `WMS_MODULE_CONTRACTS.md`, `WMS_DATA_OWNERSHIP.md`, `WMS_STOCK_STATE_MACHINE.md`, `WMS_STOCK_TRANSACTION_RULES.md` | Quyền sở hữu dữ liệu, stock invariants, transaction/rollback, idempotency, module boundaries | Không chứng minh runtime/DB thật đã pass |
| E — Nghiệp vụ giao khách/QR | `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `GIAOHANGKHACH_USER_GUIDE.md`, `DOCQR_PHASE6_AUDIT.md`, `WMS_B0_01_DELIVERY_PARITY.md` | MP/SP, QR order, Gear/quantity, FIFO, delivery-hour state, parity scenarios, QR parsing | Không được coi refactor/build/unit test là E2E acceptance |
| F — Traceability/report | `BAOCAO_TRACEABILITY_DATA_MODEL.md`, `BAOCAO_TRACEABILITY_SOURCE_INVENTORY.md`, `BAOCAO_LEGACY_REPORTING_INVENTORY.md` | Quan hệ Delivery → QR/carton → LOT/quantity → customer/stock/production/QC/NG; cardinality và read-only | Không được đoán cột/khóa chưa xác minh hoặc biến report thành writer |
| G — Triển khai/trạng thái | `WMS_IMPLEMENTATION_AUDIT.md`, `WMS_OVERALL_STATUS.md`, `WMS_REFACTOR_ROADMAP.md`, `GIAOHANGKHACH_FUNCTIONAL_HARDENING_PROGRESS.md`, `PHASE4_9_COMPLETION_STATUS.md` | Nợ kỹ thuật, việc còn mở, migration sequencing | Trạng thái “hoàn tất” ở branch khác không phải evidence cho branch test hiện tại |
| H — UI/ops | `Dashboard.md`, `WMS_USER_GUIDE.md`, `XULYHANGLOI_USER_GUIDE.md` | Điều hướng, thao tác người dùng, bước thực thi trên UI | Không chỉ dựa vào hình/UI guide để khẳng định hậu điều kiện DB |

**Quy tắc khi mâu thuẫn:** (1) bảo toàn ranh giới an toàn của master spec; (2) dùng workflow/contract làm expected behavior; (3) inspect code/schema/procedure thực tế để xác định implementation hiện tại; (4) ghi khác biệt thành gap hoặc decision cần xác nhận; (5) tạo test và evidence. Không âm thầm chọn một tài liệu thuận tiện hơn.

## 2. Gate G1–G6 chính thức cần áp dụng

Nguồn định nghĩa: `WMS_RELEASE_GATE.md`. Các trạng thái trong file đó có branch, baseline SHA và run ID riêng; chỉ dùng định nghĩa gate, **không copy trạng thái PASS/BLOCKED sang branch này** khi chưa xác minh đúng SHA.

| Gate | Định nghĩa chuẩn | Artifact/evidence bắt buộc ở branch test |
|---|---|---|
| G1 — Traceability/source ownership | Requirement → code → SQL → transaction → test → evidence; gap phải được phân loại và đóng hoặc có exception được duyệt | Traceability matrix có source path, symbol/procedure/schema, test ID, evidence link, owner/status; mọi GAP có disposition |
| G2 — Unit/regression | Tests chạy trên đúng candidate SHA, bao gồm compile items của production project | Commit SHA, build log, test result, failed-test details; không suy ra PASS từ run SHA khác |
| G3 — SQL engine integration | Implementation/repository code trên SQL Server cô lập, có data assertions và rollback | SQL integration run, before/after assertions, rollback proof; phân biệt protocol fixture với việc gọi business method thật |
| G4 — Production-like schema/procedure compatibility | Schema/procedure đại diện, cô lập và rollback-only | Schema provenance/hash, procedure compatibility evidence, assertion + rollback; không ghi lên DB14/production |
| G5 — Two-session concurrency | Hai session trên business keys/quota dùng chung, serialization và rollback injection | Session A/B timeline, lock/idempotency assertions, no-double-consumption, rollback evidence |
| G6 — Full business E2E | UI thật → DB/ledger/history assertions → cleanup; gồm delivery và reconciliation | UI action evidence, SQL before/after assertions, workflow/run ID, final reconciliation, cleanup verification |

### Trạng thái ban đầu trên branch test

Không tự suy diễn gate nào đã pass. Mặc định `NOT VERIFIED` cho đến khi có evidence gắn đúng commit SHA và target. Các ghi chú hiện có về G6 cho biết self-test `#temp`, worklist được seed, hoặc manifest chưa cấu hình không phải bằng chứng business E2E.

## 3. Ma trận ánh xạ nghiệp vụ → test

| Nghiệp vụ / nhánh | Tài liệu thiết kế chính | Test cần có | Assertion trọng tâm |
|---|---|---|---|
| Nhập kho hàng OK | `WORKFLOW_NHAPKHO.md`, `WORKFLOW_KHOCORE.md`, `WMS_STOCK_TRANSACTION_RULES.md` | Receive valid, duplicate QR, invalid inspection, concurrent same QR, concurrent different QR/same LOT, rollback | STOCKTP + SlotLot + Slot header + IMPORT history cùng transaction; duplicate không làm thay đổi dữ liệu |
| Vị trí kho / A0 / slot | `WORKFLOW_KHOCORE.md`, `WMS_STOCK_STATE_MACHINE.md`, `Dashboard.md` | Receive to A0, receive to chosen slot, move, pick/restore | Quantity/location trước-sau, slot capacity, no negative, QR/LOT đúng slot |
| Xuất kho 2 pha | `WORKFLOW_XUATKHO.md`, `WORKFLOW_WMS.md` | Pick → WAIT → confirm; cancel/restore; failed confirm | Pick giảm SlotLot nhưng chưa giảm STOCKTP; confirm giảm cả tồn theo contract; cancel phục hồi SlotLot; history atomic |
| Giao trực tiếp A0 | `WORKFLOW_XUATKHO.md`, `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `WMS_B0_01_DELIVERY_PARITY.md` | Direct A0 delivery | Không tạo pickup/WAIT giả; stock/QR/delivery/history/reconciliation đúng |
| Giao khách QR / FIFO / MP-SP | `WORKFLOW_GIAOHANGKHACH.md`, `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `DOCQR_PHASE6_AUDIT.md`, `WMS_B0_01_DELIVERY_PARITY.md` | 1 PART/1 LOT, multi-LOT, multi-PART, duplicate QR, FIFO wrong order, partial LOT, Gear, quantity mismatch, MP/SP table selection | PART/LOT/QR linkage, FIFO order, exact quantity, correct table/config, no replay, transaction rollback |
| CustomerTableConfig / 1-tem / 2-tem | Functional design + actual config/schema/procedure inventory | Configured case, missing config, no-match config, 1-label and 2-label variants | Expected config actually read by UI/procedure; correct QR/TMP table and label behavior; no guessed rule |
| Hàng lỗi nội bộ / khách hàng | `WORKFLOW_HANGLOI.md`, `XULYHANGLOI_DESIGN.md`, `WORKFLOW_XUATKHO.md` | Internal defect vs customer-originated defect, QC direction/final QC, rework OK/NG | Distinct source/type, original references, quantity/state/location, QC/history; NG not counted as available stock unless explicit rule |
| Giao bù / replacement | `WORKFLOW_HANGLOI.md`, `WORKFLOW_XUATKHO.md`, `WMS_B0_01_DELIVERY_PARITY.md` | Valid replacement, duplicate retry, invalid/orphan source, partial failure | Link to original delivery/customer/request/defect; stock, QR, delivery and history atomically consistent |
| Traceability/report | `BAOCAO_TRACEABILITY_DATA_MODEL.md`, `BAOCAO_TRACEABILITY_SOURCE_INVENTORY.md` | Search by QR, LOT, PartNo, customer, customer label; multi-QR/multi-LOT cardinality; malformed LOT token | No row multiplication, each QR/carton preserved, LOT quantity parsed accurately, read-only behavior |
| Cleanup / safety | Master spec + harness scripts | Success cleanup, UI failure cleanup, interrupted-run recovery, wrong target rejection | Only run-owned rows removed; DB14 remains unchanged; no secret in logs/artifacts |

Các dòng là yêu cầu coverage, không phải tuyên bố test đã được viết/chạy. Mỗi scenario cần ID ổn định, input provenance, preconditions, thao tác UI thật, expected/actual, assertions, cleanup và evidence.

## 4. Chuẩn hóa testcase

Mỗi testcase phải chứa tối thiểu:

- `TestCaseId`, gate, business scenario, branch/commit SHA.
- Nguồn requirement và đường dẫn tài liệu/section.
- Source shape (DB14) hoặc nhãn `SYNTHETIC_DB57`; không trộn hai loại.
- Run prefix, mapping item/LOT/QR/customer/order; không chứa secrets.
- Preconditions và snapshot trước thao tác.
- Các thao tác UI/procedure/service thực tế; đánh dấu rõ mọi bước SQL chỉ là seed input hoặc observe/assert.
- Expected result có nguồn từ business contract; mọi rule chưa xác minh ghi `NEEDS_RULE_CONFIRMATION`.
- Assertions theo từng bước và final reconciliation.
- Cleanup plan, cleanup result và verify không còn run-owned rows.
- Status: `PASS`, `FAIL`, `INCOMPLETE`, `BLOCKED`, `NOT VERIFIED`, `NOT AVAILABLE IN SOURCE DATA`.
- Root-cause class: `CODE BUG`, `SQL/PROCEDURE BUG`, `DATA-MODEL BUG`, `TEST-HARNESS BUG`, `CONFIGURATION BUG`, `NOT AVAILABLE IN SOURCE DATA`.

## 5. Bất biến về test data và business result

1. DB14 là source read-only; DB57 là target duy nhất được ghi test.
2. SQL seed chỉ tạo input/configuration cần thiết. Không seed QR đã scan, delivery đã hoàn tất, stock movement result, defect/replacement result hoặc history giả.
3. Nghiệp vụ phải đi qua UI và service/procedure thật. SQL chỉ quan sát/assert sau bước đó.
4. Cùng identity chain từ source mapping tới final reconciliation; synthetic scenarios phải có provenance rõ.
5. Test failure không được biến thành pass bằng cách sửa state trước assertion.
6. Cleanup luôn có, kể cả UI fail/timeout; nếu cleanup không xác minh được, run không được báo SAFE.
7. Không chuyển code/schema/procedure sang production chỉ vì build/unit test xanh; cần gate tương ứng và approval/evidence.

## 6. Tài liệu cần duy trì đồng bộ

- Master acceptance spec: nguyên tắc chạy, test target, lifecycle và promotion gates.
- File này: crosswalk, coverage map và chuẩn testcase.
- Design/workflow docs: expected behavior và kiến trúc; chỉ sửa khi rule thay đổi có chủ đích.
- Test manifests/scripts: thực thi scenario, không phải nguồn định nghĩa nghiệp vụ.
- Run reports: evidence thực tế gắn với SHA và run ID.

Khi thay đổi workflow, thêm/sửa testcase và assertions trong cùng change set; khi phát hiện tài liệu thiết kế lệch code, không tự chọn một bên — ghi gap, xác minh owner/rule, rồi cập nhật expected behavior hoặc implementation có review.
