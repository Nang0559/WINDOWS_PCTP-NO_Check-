# Trạng thái 6 Gate — canonical gate mapping và evidence

> **Nguồn định nghĩa duy nhất của G1–G6:** `PCTP/Workflow/Diagram/WMS_RELEASE_GATE.md`.
> File này là dashboard/evidence index, không được tự định nghĩa lại ý nghĩa gate.

> **Evidence snapshot note:** các run/SHA bên dưới là evidence lịch sử được ghi đúng theo candidate đã chạy. Sau các commit tài liệu mới, không được gọi chúng là evidence của branch HEAD hiện tại nếu chưa rerun.

## 1. Quy ước bắt buộc

`WMS_RELEASE_GATE.md` định nghĩa G1–G6 như sau:

| Gate | Định nghĩa canonical | Không được dùng để thay thế |
|---|---|---|
| G1 | Traceability/source ownership: requirement → code → SQL → transaction → test → evidence; gap phải được phân loại và đóng hoặc exception được duyệt | Chỉ có danh sách file/test |
| G2 | Unit/regression trên đúng candidate SHA, gồm production project compile items | Build/test ở SHA khác |
| G3 | SQL engine integration bằng implementation/repository code trên SQL Server cô lập, có assertions và rollback | Seed/protocol-only test |
| G4 | Production-like schema/procedure compatibility trên DB cô lập, rollback-only | Static schema inspection |
| G5 | **Hai session đồng thời** trên business key/quota dùng chung, serialization + rollback injection | Một session, UI navigation hoặc receiving journey |
| G6 | Full business E2E: **UI thật → DB/ledger/history assertions → cleanup**, gồm delivery và reconciliation | Self-test, worklist, seed, build hoặc manifest tồn tại |

### Quan trọng: số G5/G6 trong các script cũ không phải gate definition

Một số script/runbook lịch sử dùng mã G3–G6 cho các **execution phase** khác với gate canonical. Ví dụ một execution script có thể gọi journey nhập kho là “G5”. Điều đó **không có nghĩa canonical G5 là nhập kho**.

Trong tài liệu/test case mới:

- `Gate=G5` chỉ dùng cho concurrency two-session.
- `Gate=G6` dùng cho business E2E.
- Receiving, pickup, direct-A0, MP/SP, defect và replacement đều là **scenario thuộc G6**, không tạo gate mới.
- Mọi mã `G7-*` cũ được xem là **legacy scenario naming** và không được dùng để suy ra một gate thứ bảy.
- Chi tiết quy đổi G7 legacy → G6 TestCaseId nằm trong `PCTP/Workflow/Diagram/WMS_DOCUMENTATION_NORMATIVE_AMENDMENT_2026-10-30.md`.

## 2. Evidence status — snapshot lịch sử, không phải HEAD hiện tại

| Gate | Status | Evidence / remaining |
|---|---|---|
| G1 | **FAIL / OPEN** | Candidate-SHA traceability artifact từ CI run `40910630446`, SHA `b1a807fbdcef599c734b8a8e77d7f12dbfd5a4c3`: 145 code rows, 99 code rows có gap, 68 SQL scripts, 229 gaps. Đây là static evidence; chưa phải PASS. |
| G2 | **PASS LIMITED** | CI run `40910630446` thành công cho unit/regression và runner checks. Full WinForms production build trên exact candidate SHA vẫn cần evidence riêng trước khi gọi G2 đầy đủ. |
| G3 | **PASS LIMITED — CI isolated SQL** | CI disposable SQL Server đã chạy thành công. Không được coi đây là live DB57 evidence. |
| G4 | **PASS LIMITED — CI rollback/schema scope** | CI schema/procedure compatibility scope đã xanh trong database cô lập. Chưa thay thế representative DBA/live acceptance nếu gate yêu cầu thêm. |
| G5 | **OPEN / NOT VERIFIED** | Chưa có evidence canonical two-session shared-key/quota + no-double-consumption + injected rollback cho candidate SHA hiện tại của snapshot. |
| G6 | **BLOCKED / NOT VERIFIED** | Receive và delivery single/multi-LOT đã có UI automation source, nhưng chưa có live Windows + DB57 evidence. QC direction/final vẫn chưa configured. Pickup/A0, MP/SP, defect/replacement chưa có complete runtime journeys. |

## 3. CI allow-list status

Workflow `.github/workflows/pctp-qc-tests.yml` đã allow-list:

`pctp-db57-source-order-harness.yml`

và vẫn giữ hai điều kiện an toàn:

1. live workflow phải `workflow_dispatch` và không được tự chạy bởi `push`/`pull_request`;
2. isolation guard của CI disposable SQL job tiếp tục loại workflow được allow-list khỏi static live-secret scan nhưng **không** tắt guard của chính live workflow.

CI run `40910630446` đã chạy thành công sau thay đổi này. Vì vậy lỗi allow-list của run `40876969690` là **đã được sửa**, không còn là blocker hiện tại của snapshot đó.

## 4. Traceability numbers — không copy số giữa các SHA

`docs/TRACEABILITY_MATRIX.md`, `PCTP.QC.UI/G1-gaps.csv` và CI artifact có thể được sinh ở các SHA khác nhau. Do đó các số như 105, 208 hoặc 229 **không được cộng/trộn hoặc xem là cùng một snapshot**.

Canonical rule:

> Mỗi candidate SHA phải sinh lại `gate-summary.json` và `gaps.csv`; report phải ghi đúng SHA và run ID. Chỉ artifact của đúng candidate mới được dùng để quyết định G1.

CI hiện đã có step `Regenerate G1 traceability matrix` và upload artifact `pctp-g1-traceability-evidence`.

## 5. TestCaseId convention

Các testcase business dùng mã ổn định trong `PCTP/Workflow/Diagram/WMS_TEST_CASE_CATALOG.md`.

- `G1-*`: traceability
- `G2-*`: unit/regression/build
- `G3-*`: isolated SQL implementation integration
- `G4-*`: production-like schema/procedure compatibility
- `G5-*`: two-session concurrency
- `G6-REC-*`: receiving E2E
- `G6-PICK-*`: slot → staging/A0 → delivery
- `G6-A0-*`: direct A0 → delivery
- `G6-MP-*`, `G6-SP-*`: MP/SP + label/configuration
- `G6-CFG-*`: CustomerTableConfig behavior
- `G6-FIFO-*`: FIFO/multi-LOT/partial-LOT
- `G6-QC-*`: QC direction/final QC
- `G6-DEF-*`: internal/customer defect
- `G6-REP-*`: replacement/giao bù
- `G6-RECNC-*`: final reconciliation/cleanup

`G7-*` không còn là gate/scenario ID chuẩn.

## 6. Rule cập nhật

Khi có run mới:

1. ghi exact candidate SHA;
2. ghi workflow run ID;
3. cập nhật evidence link/artifact;
4. không chuyển status chỉ vì source file đã tồn tại;
5. nếu G5/G6 thiếu runtime evidence, giữ `OPEN`/`BLOCKED`;
6. nếu branch HEAD đã thay đổi sau run, không gán evidence cũ cho HEAD mới.
