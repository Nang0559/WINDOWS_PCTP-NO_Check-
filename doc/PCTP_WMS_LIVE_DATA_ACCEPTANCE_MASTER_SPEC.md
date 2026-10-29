# PCTP / WMS — Live Data Acceptance Master Specification

> **Tài liệu chuẩn định hướng triển khai, kiểm thử và nghiệm thu end-to-end.**  
> Mục tiêu là giữ nhất quán hướng xử lý giữa code, SQL/procedure, UI journey, fixture, CI và báo cáo; mọi thay đổi phải đối chiếu tài liệu này trước khi thực hiện.

- **Repository:** Nang0559/WINDOWS_PCTP-NO_Check-
- **Branch:** feature/wms-live-data-test-harness
- **DB14 (source):** 192.168.200.14\\BRAVO / B7R2_FCC — chỉ đọc.
- **DB57 (test target):** 192.168.200.57 / B7R2_FCC — môi trường duy nhất được phép ghi dữ liệu kiểm thử.
- **Runner:** Windows self-hosted, labels [self-hosted, windows, pctp-ui].
- **Workflow chính:** pctp-db57-source-order-harness.yml.

## 1. Nguyên tắc bất biến — không được lệch hướng

1. DB14 luôn **READ-ONLY**. Không insert/update/delete, không tạo dữ liệu nghiệp vụ giả trên DB14.
2. Chỉ DB57 được ghi dữ liệu kiểm thử. Target phải được pin/validate trong script; không tin connection string do input tự do cung cấp.
3. SQL chỉ được dùng để:
   - đọc/extract source fixture từ DB14;
   - tạo đầu vào test cô lập trên DB57 trước khi chạy;
   - observe/assert trạng thái sau mỗi bước nghiệp vụ;
   - cleanup đúng dữ liệu thuộc run.
4. **Không dùng SQL để giả lập kết quả nghiệp vụ**: không update QR như thể đã scan, không tự ghi kết quả nhập kho/giao hàng/pickup/hàng lỗi/giao bù, không chèn history giả.
5. Các nghiệp vụ phải được thực hiện bằng **UI và procedure/service thật**. SQL chỉ quan sát và xác nhận kết quả.
6. Dùng cùng một identity chain xuyên suốt vòng đời khi nghiệp vụ cho phép; không tách thành các fixture rời làm mất quan hệ nguồn.
7. DB14 thiếu dữ liệu cho một nhánh thì báo **NOT AVAILABLE IN SOURCE DATA** và, khi cần kiểm thử nhánh đó, tạo synthetic input/configuration trên DB57.
8. Synthetic scenario chỉ tạo đầu vào hoặc cấu hình cần thiết để kích hoạt nhánh. Không seed kết quả cuối như thể người dùng đã thao tác.
9. Không broad-delete. Cleanup phải có run prefix, kiểm tra dependency và xác minh trước/sau.
10. Không đưa credentials, passwords, token hoặc connection string chứa secret vào source, log, artifact hay report.
11. Không tắt isolation guard để làm workflow chạy qua. Phải allow-list workflow cụ thể và giữ các xác nhận an toàn.
12. Không kết luận PASS hoặc SAFE FOR PROMOTION chỉ từ build/unit test hoặc SQL seed thành công; phải có evidence của UI/procedure và assertion nghiệp vụ.

## 2A. Trạng thái G1–G6 — KHÔNG mặc định là đã ổn

**Quyết định quan trọng:** việc mở rộng sang G7 không xác nhận G1–G6 đã hoàn tất. G1–G6 phải được kiểm kê và nghiệm thu riêng; hiện trạng chỉ được đánh dấu PASS khi có bằng chứng chạy thực tế phù hợp.

Bằng chứng repository đã kiểm tra ở thời điểm cập nhật tài liệu:

- `PCTP.QC.UI/journeys/g6/README.md` ghi `g6-selftest.json` là self-test cơ chế runner với bảng `#temp`, không đụng bảng nghiệp vụ. Đây **không phải** bằng chứng nghiệp vụ WMS G6 đã pass.
- README ghi `g6-qc-direction.json` và `g6-qc-final.json` **chưa cấu hình**.
- README ghi `g6-receive.json` mới seed/worklist, **chưa tự động quét QR**.
- README ghi `g6-delivery-hvn.json` mới seed/dòng chờ giao, **chưa tự động quét QR**.
- Các manifest giao hàng nhiều LOT và các journey khác phải được kiểm tra theo nội dung và kết quả run cụ thể; sự tồn tại của file không đồng nghĩa đã nghiệm thu.

Vì vậy, tại thời điểm này **không được ghi G1–G6 là PASS/ổn định chỉ dựa trên tài liệu, file journey hoặc unit tests**. Với các gate chưa có evidence, trạng thái đúng là `NOT VERIFIED`; với journey còn thiếu bước thì ghi `INCOMPLETE` hoặc `BLOCKED`.

### Quy tắc nghiệm thu áp dụng cho từng gate G1–G6

1. Liệt kê từng scenario/journey và nghiệp vụ mà nó đại diện.
2. Kiểm tra manifest có cấu hình đầy đủ; không còn `configured: false` hoặc TODO/blocker chưa xử lý.
3. Xác minh seed tạo đúng input cô lập, assertion kiểm tra hậu điều kiện thực, cleanup và verify cleanup chạy cả khi UI fail.
4. Với nghiệp vụ scan/nhập/giao/QC, phải thực hiện thao tác qua UI/procedure thật; không coi việc mở màn hình, thấy worklist hoặc seed SQL là hoàn tất nghiệp vụ.
5. Chạy trên DB57 với runner/app đã xác minh cùng target cô lập; thu workflow/run ID, log, assertion và cleanup evidence.
6. Phân loại từng kết quả: `PASS`, `FAIL`, `INCOMPLETE`, `BLOCKED`, `NOT VERIFIED`, hoặc `NOT AVAILABLE IN SOURCE DATA`.
7. Chỉ chuyển gate sang `PASS` khi tất cả bước bắt buộc của scenario và hậu điều kiện nghiệp vụ đều có evidence.

### Bảng kiểm G1–G6 bắt buộc bổ sung

| Gate | Việc cần xác minh | Điều kiện không được coi là PASS |
|---|---|---|
| G1 | Lấy đúng scope và inventory các test/schema/luồng hiện có theo định nghĩa G1 trong repo | Chỉ có build/unit test hoặc chưa có evidence |
| G2 | Xác minh các contract/invariant dữ liệu và SQL liên quan theo định nghĩa G2 trong repo | Chỉ đọc source hoặc kiểm tra tĩnh |
| G3 | Xác minh logic nghiệp vụ/service/procedure theo định nghĩa G3 trong repo | Có code nhưng chưa chạy test tương ứng |
| G4 | Xác minh tích hợp SQL/repository và các invariant dữ liệu theo định nghĩa G4 trong repo | Seed thành công nhưng chưa assert hậu điều kiện |
| G5 | Xác minh UI navigation/control và hành vi thực tế theo định nghĩa G5 trong repo | Chỉ thấy control/menu hoặc chỉ kiểm tra navigation |
| G6 | Xác minh business journey seed → UI thật → SQL assert → cleanup/verify; hoàn tất các journey còn thiếu scan/QR/QC | Self-test `#temp`, worklist xuất hiện, hoặc seed dữ liệu không đủ để kết luận nghiệp vụ đã chạy |

> Bảng trên là khung kiểm soát không được bỏ qua; tên và phạm vi chính thức của từng G1–G6 phải được đối chiếu với tài liệu/test runner trong repo. Không tự suy diễn G1–G5 từ tên gate nếu repository định nghĩa khác. Nếu phát hiện khác biệt, cập nhật bảng này theo nguồn trong repo và lưu đường dẫn/evidence.

## 2. Mục tiêu end-to-end

Identity chain cần được duy trì:

> DB14 source order/item/LOT/QR → DB57 isolated fixture → Nhập kho → A0 hoặc slot/rack → Pickup hoặc Direct delivery → Xử lý hàng lỗi → Giao bù/replacement → Final reconciliation → Cleanup.

Acceptance phải chứng minh đúng số lượng, danh tính item/LOT/QR, vị trí hàng, quan hệ customer/order/delivery/defect/replacement và lịch sử chuyển trạng thái ở từng bước.

## 3. Kiến trúc chạy test chuẩn

1. **Discover source:** đọc DB14 bằng kết nối read-only; chọn order/LOT/QR có shape phù hợp.
2. **Normalize:** chuẩn hóa dữ liệu thành manifest trong thư mục tạm của runner; không upload raw fixture.
3. **Seed DB57:** sinh identity riêng, gắn run prefix và transaction; chỉ tạo đầu vào cần thiết.
4. **Real UI journey:** thao tác trên UI thật để kích hoạt nghiệp vụ.
5. **Observe/assert:** query DB57 sau mỗi bước; không sửa dữ liệu để làm assertion pass.
6. **Final reconciliation:** đối chiếu stock, location, QR, delivery, defects, replacement và history.
7. **Cleanup:** xóa theo prefix và dependency đã xác minh.
8. **Report:** kết quả từng gate, evidence và phân loại root cause.

Run prefix chuẩn: **QCUI_<8 ký tự>**. Prefix phải được lưu trong manifest/report và áp dụng cho mọi identity có thể kiểm soát. Nếu bảng ledger bên ngoài không có prefix, phải xác minh dependency và quy tắc cleanup riêng; tuyệt đối không xóa rộng.

## 4. Source fixture từ DB14

Extractor hiện có: Invoke-PctpLiveOrderFixture.ps1.

- Tìm ngày giao gần nhất từ LUUPHIEUGIAOHANG.NGAYGIAO.
- Đọc LUUPHIEUGIAOHANG và LUUDOCQRCODE; nhóm candidate theo STT giao hàng và đánh giá độ phủ QR/số dòng/LOT.
- Quan hệ đã xác minh để nối dữ liệu: MAHANG/PART + LEFT(LOT,13) với STOCKTP.
- **LUUDOCQRCODE.STT là số thứ tự bản ghi scan**, không được nhầm với khóa order/phiếu giao.
- Nên chọn các shape 1-LOT, 2-LOT và 3-LOT nếu dữ liệu nguồn thực sự có.
- Cần mở rộng extractor lấy baseline STOCKTP liên quan để hiểu số lượng nhập/xuất/tồn và không chỉ suy ra tồn từ order quantity.
- Ghi rõ source shape không có sẵn. Không tự tạo dữ liệu nguồn hoặc gán một case synthetic thành source case.

Manifest phải thể hiện tối thiểu source order/customer nếu có, item/part, LOT gốc, quantity, QR source mapping, source shape, thời điểm extract và mapping sang identity DB57. Không chứa credential hay thông tin bí mật không cần thiết.

## 5. DB57 fixture và identity mapping

- Tạo identity item mới (ví dụ B20Item theo cơ chế hiện có) và LOT test mới, giữ nguyên quan hệ/shape nghiệp vụ cần kiểm tra.
- LOT mapping phải tuân thủ format thật của hệ thống; không tự giả định nếu chưa kiểm tra constraint và procedure.
- Dùng prefix QCUI_<8 ký tự> để định danh run.
- Nếu UI yêu cầu QR record tồn tại trước khi scan, seed record ở trạng thái unread/NULL đúng schema. Không đặt trước trạng thái scanned/issued.
- Không seed các result row của delivery, pickup, defect, replacement hoặc history chỉ để làm cho assertion pass.
- Seed phải chạy transaction và rollback khi thất bại.
- Kiểm tra target DB57 ngay trước mutation; từ chối chạy nếu target không đúng.
- Synthetic customer/configuration phải được đánh dấu riêng, giới hạn trong DB57 và có cleanup plan.
- Manifest mapping phải được dùng xuyên suốt các journey để giữ identity chain.

## 6. G7 — Nhập kho

UI/procedure thật phải thực hiện nhập kho và scan QR.

### Assertions sau nhập kho

- STOCKTP: số lượng nhập/xuất/tồn phù hợp business rule thực tế.
- DOCQRCODE/QR: trạng thái chuyển đổi đúng sau scan; không duplicate.
- Item/LOT/QR mapping đúng với fixture.
- Vị trí sau nhập kho đúng theo luồng thực tế: A0/virtual rack hoặc slot/rack.
- Lịch sử/audit ghi nhận transition thật.
- Quantity nhận thực tế khớp input và không vượt/nhân đôi số lượng.
- Không có result/history giả do seed script tạo.

## 7. G7 — Giao hàng: bắt buộc có hai đường

### G7-A — Pickup từ slot → staging/A0 → delivery

1. Nhập kho và đưa hàng vào slot/rack thật theo UI/procedure.
2. Thực hiện pickup bằng UI/procedure.
3. Assert quantity rời slot, quantity vào staging/A0, trạng thái QR nếu áp dụng và history.
4. Thực hiện delivery bằng UI/procedure.
5. Assert stock issued, delivery rows, QR state, customer/order link và history.

### G7-B — Direct A0 → delivery

1. Fixture ở A0/virtual location theo đúng flow nhập kho.
2. Không chạy pickup từ slot.
3. Thực hiện delivery trực tiếp từ A0 bằng UI/procedure.
4. Assert stock/location, delivery/QR linkage, quantity và history riêng.

Hai đường phải được kiểm tra độc lập và đều đi qua final reconciliation. Không dùng SQL để chuyển hàng giữa location hoặc ghi kết quả giao.

## 8. MP/SP, loại tem và CustomerTableConfig

Đây là acceptance gate bắt buộc của delivery.

Với mỗi case cần xác định và ghi lại:

- Customer/order và item/part/LOT liên quan.
- Phân loại hàng **MP hay SP** theo dữ liệu và rule thực tế.
- Chế độ tem mong đợi: **1 loại tem**, **2 loại tem** hoặc nhánh special configuration theo hệ thống.
- Bản ghi/rule CustomerTableConfig được áp dụng; các khóa cấu hình liên quan và điều kiện match.
- UI/procedure thực tế có đọc và áp dụng CustomerTableConfig hay không.
- Chế độ tem thực tế và số lượng/loại tem phát sinh.
- Kết quả mapping QR/LOT và history.

Không suy đoán ý nghĩa cột hoặc quy tắc MP/SP từ tên trường. Phải inspect schema, code, stored procedure và cấu hình hiện hành trước khi chốt expected result. Không bypass CustomerTableConfig bằng SQL và không hardcode expected behavior khi chưa có evidence từ nghiệp vụ.

## 9. Synthetic special-configuration scenarios

DB14 hiện có thể không chứa các trường hợp cần thiết để kiểm thử toàn bộ nhánh MP/SP/tem/custom. Khi đó:

- Ghi **NOT AVAILABLE IN SOURCE DATA** cho source case tương ứng.
- Tạo synthetic **input/configuration** tương tự nghiệp vụ trên DB57.
- Dùng UI/procedure thật để thực hiện giao hàng.
- Không sửa DB14 và không chèn kết quả cuối để giả lập giao hàng.
- Lưu expected behavior, actual behavior, evidence và cleanup cho từng case.

Ma trận cần xem xét sau khi đã xác minh rule thực tế:

| Scenario | Mục tiêu |
|---|---|
| MP — 1 tem | Xác minh rule MP và đường tem đơn |
| SP — 1 tem | Xác minh rule SP và đường tem đơn |
| MP/SP — 2 tem | Xác minh nhánh hai loại tem đúng điều kiện |
| CustomerTableConfig đặc biệt | Xác minh cấu hình customer làm thay đổi hành vi giao |
| Config thiếu/không match | Xác minh xử lý lỗi/validation, không tự áp dụng rule mặc định sai |

Không bắt buộc mọi dòng trên phải dùng cùng một customer nếu rule nghiệp vụ yêu cầu customer riêng. Tuy nhiên, identity chain của mỗi scenario phải nhất quán từ input tới reconciliation. Synthetic case phải được báo riêng, không được tính nhầm là dữ liệu source DB14.

## 10. Xử lý hàng lỗi

### 10.1 Hàng lỗi nội bộ

- Tạo và xử lý phiếu hàng lỗi nội bộ qua UI/procedure thật.
- Assert quantity, status, location, item/LOT/QR linkage, reference và history.
- Xác minh hàng lỗi có bị trừ/chuyển tồn theo đúng business rule; không giả định công thức trước khi inspect procedure.

### 10.2 Hàng lỗi từ customer

- Tạo và xử lý luồng lỗi có nguồn từ customer qua UI/procedure thật.
- Assert customer, original delivery/request, item/LOT/QR, quantity, status, location và history.
- Phân biệt rõ với internal defect; không gộp hai loại thành một scenario nếu nghiệp vụ xử lý khác nhau.

Nếu source fixture không có lịch sử phù hợp, tạo synthetic input trên DB57 khi cần. Không tự tạo record kết quả đã xử lý.

## 11. Giao bù / replacement

- Giao bù phải liên kết được với original delivery, customer/request và defect case theo schema/rule thật.
- Không tạo orphan delivery hoặc replacement không có nguồn gốc nghiệp vụ.
- Thực hiện tạo/xử lý giao bù bằng UI/procedure thật.
- Assert quantity giao bù, item/LOT/QR, stock decrement, delivery record, customer/original case linkage và history.
- Xác minh quy tắc cho phép/cấm giao bù theo case thực tế.
- Nếu source không hỗ trợ case tự nhiên, ghi NOT AVAILABLE IN SOURCE DATA và dùng synthetic DB57 scenario khi cần kiểm tra nhánh đó.

## 12. Assertion và final reconciliation

### Sau từng bước

- **Stock:** SLNHAP, SLXUAT, SLCONLAI và các quantity field liên quan theo business rule đã xác minh.
- **Location:** A0, slot/rack, staging và số lượng chuyển giữa các vị trí.
- **QR:** unread → scanned/issued theo đúng flow; không duplicate; mapping về stock bằng quan hệ đã xác minh.
- **Delivery:** quantity, LOT, QR, customer/order và delivery linkage.
- **MP/SP/label:** expected vs actual, CustomerTableConfig được áp dụng.
- **Defect:** status, quantity, location, original reference và history.
- **Replacement:** reference tới original case, quantity, stock, delivery và history.
- **Audit/history:** có transition thật; timestamp/actor nếu schema hỗ trợ; không có record giả do harness.
- **Cleanup:** chỉ xóa dữ liệu thuộc run, không ảnh hưởng dữ liệu khác.

### Final reconciliation

Final reconciliation phải dựa trên schema và business rules thực tế của code/procedure. Không tự áp đặt công thức khi chưa xác minh luồng chuyển trạng thái, location, QR và hàng lỗi.

SQL assertion hiện có (assert-after-scan.sql) kiểm tra một phần invariant STOCKTP, delivery không vượt stock issued, QR mapping và duplicate QR. Đây **chưa đủ** để nghiệm thu toàn lifecycle.

Các điều kiện như LUU.SOLUONG = SUM(QR), SLCONLAITMP, STATUS/SATUS, LUUQR.STT, GIOGIAOFCC/GIOXUAT và LOTHVN chỉ được đưa thành gate khi đã xác minh ý nghĩa và rule từ schema/code/procedure.

## 13. CI workflow và isolation guard

- Workflow source-driven: pctp-db57-source-order-harness.yml.
- Chỉ chạy thủ công qua workflow_dispatch.
- Phải xác nhận rõ DB14 read-only và DB57 test writes.
- Các phase seed/assert/cleanup phải tách biệt và có log/evidence.
- Concurrency group: pctp-db57-acceptance; cancel-in-progress=false.
- Live SQL isolation guard phải allow-list chính xác workflow mới; không disable guard.
- Run 40876969690 từng bị guard từ chối do workflow mới chưa được allow-list với PCTP_QC_SOURCE_SQL_CONNECTION. Phân loại ban đầu là configuration/test-harness failure, không phải bằng chứng lỗi DB hoặc business logic.
- Chỉ báo workflow PASS khi đã có run thực tế và các acceptance assertions thành công. Không suy diễn từ build/unit tests.

## 14. Hạng mục kỹ thuật cần hoàn thiện

1. Sửa allow-list của live workflow isolation guard, vẫn giữ toàn bộ safety checks.
2. Inspect schema/code/procedure của CustomerTableConfig, MP/SP và rule 1-tem/2-tem.
3. Inspect schema/code/procedure của A0, slot/rack, staging và pickup.
4. Mở rộng source extractor để lấy STOCKTP baseline và shape 1/2/3 LOT nếu source có.
5. Hoàn thiện manifest mapping source → DB57.
6. Xác nhận DB57 seed và QR unread state đúng schema.
7. Hoàn thiện receive UI journey + assertions.
8. Hoàn thiện G7-A pickup → staging/A0 → delivery.
9. Hoàn thiện G7-B direct A0 → delivery.
10. Hoàn thiện synthetic MP/SP, 1-tem/2-tem và CustomerTableConfig cases.
11. Hoàn thiện internal defect và customer defect journeys.
12. Hoàn thiện replacement/giao bù và linkage assertions.
13. Hoàn thiện final reconciliation và dependency-safe cleanup.
14. Chạy live workflow trên self-hosted runner, lưu evidence.
15. Lập root-cause ledger và promotion decision.

## 15. Root Cause Ledger — phân loại lỗi

Mỗi failure phải được gán một nhóm chính, có evidence và next action:

- **CODE BUG:** UI/service/client/server logic sai trong khi fixture và cấu hình đầu vào đúng.
- **SQL/PROCEDURE BUG:** query, stored procedure, transaction hoặc business routing trong SQL sai.
- **DATA-MODEL BUG:** schema, constraint hoặc relationship không thể hiện đúng nghiệp vụ.
- **TEST-HARNESS BUG:** fixture, normalizer, assertion, UI journey, cleanup, runner hoặc workflow guard sai.
- **CONFIGURATION BUG:** CustomerTableConfig, MP/SP, label mode, permission hoặc setting sai.
- **NOT AVAILABLE IN SOURCE DATA:** DB14 không có shape cần thiết; cần synthetic DB57 case nếu mục tiêu test vẫn yêu cầu.

Không phân loại chỉ dựa trên tên bước fail. Cần đối chiếu log UI, SQL state trước/sau, fixture manifest, procedure và evidence.

## 16. Acceptance report bắt buộc

Report của mỗi run cần có:

- Source DB14 và target DB57 (không có credential).
- Run ID, fixture prefix, commit SHA, workflow run.
- Source order/shape; item/LOT/QR mapping đã mask nếu cần.
- MP/SP classification và CustomerTableConfig match.
- Expected label mode / actual label mode.
- Receive result.
- A0/slot/rack result.
- G7-A pickup/delivery result.
- G7-B direct delivery result.
- Internal defect result.
- Customer defect result.
- Replacement result.
- Stock, QR, delivery, location và history reconciliation.
- Cleanup result.
- Evidence DB14 không bị ghi.
- Root-cause ledger và corrective action.
- Kết luận từng gate: PASS / FAIL / NOT AVAILABLE IN SOURCE DATA.
- Final decision: ACCEPTED / NOT ACCEPTED / SAFE FOR PROMOTION / NOT SAFE FOR PROMOTION.

## 17. Promotion gate

Chỉ xem xét promotion khi:

- Unit/regression CI pass.
- DB57 fixture seed/verify pass.
- UI receive pass.
- Cả G7-A và G7-B pass.
- Stock, location, QR guard, delivery và history assertions pass.
- MP/SP, tem và CustomerTableConfig cases pass hoặc có disposition rõ ràng được phê duyệt.
- Internal/customer defect và replacement được nghiệm thu hoặc được ghi rõ ngoài phạm vi với lý do.
- Cleanup pass và không còn fixture rác.
- Có evidence DB14 không bị ghi.
- Không còn failure chưa phân loại.

Promotion chỉ gồm code/schema/procedure/config đã được duyệt; **không promotion fixture rows**.

## 18. Anti-drift checklist trước mỗi thay đổi

- [ ] Có xác nhận target ghi là DB57?
- [ ] DB14 vẫn chỉ đọc?
- [ ] Dùng cùng identity chain xuyên lifecycle khi phù hợp?
- [ ] UI/procedure thật thực hiện nghiệp vụ?
- [ ] SQL chỉ seed input, observe/assert hoặc cleanup?
- [ ] Đã kiểm tra MP/SP và CustomerTableConfig khi test delivery?
- [ ] Có synthetic DB57 case nếu source thiếu dữ liệu?
- [ ] Có assertion sau từng bước?
- [ ] Cleanup giới hạn theo prefix/dependency?
- [ ] Isolation guard vẫn bật?
- [ ] Evidence đủ để phân loại root cause?
- [ ] Không tuyên bố PASS/promotion-safe khi chưa có live UI evidence?

## 19. Thứ tự triển khai chính thức

1. Sửa workflow allow-list an toàn.
2. Inspect CustomerTableConfig, MP/SP, label rules.
3. Inspect A0/slot/rack/staging/pickup schemas và procedures.
4. Hoàn thiện source fixture + STOCKTP baseline.
5. Hoàn thiện DB57 seed + QR unread state + manifest.
6. Hoàn thiện UI nhập kho.
7. Hoàn thiện G7-A pickup → staging/A0 → delivery.
8. Hoàn thiện G7-B direct A0 → delivery.
9. Thêm synthetic MP/SP + 1-tem/2-tem + special CustomerTableConfig.
10. Thêm internal defect và customer defect.
11. Thêm replacement/giao bù.
12. Hoàn thiện final reconciliation + cleanup.
13. Chạy live CI/runner và thu evidence.
14. Lập root-cause ledger; quyết định promotion.

## 20. Quy tắc kết luận

Chỉ ghi **ACCEPTED / SAFE FOR PROMOTION** khi toàn bộ acceptance gates đã được chứng minh bằng evidence thực tế trên DB57 và UI/procedure thật, DB14 vẫn READ-ONLY, fixture đã cleanup sạch và không còn lỗi chưa phân loại.

Nếu phát hiện schema hoặc business rule thực tế khác với giả định trong tài liệu, phải cập nhật tài liệu và evidence trước khi đổi kiến trúc test. Không tự ý bỏ gate, bypass CustomerTableConfig hoặc chuyển sang SQL simulation để làm test pass.
