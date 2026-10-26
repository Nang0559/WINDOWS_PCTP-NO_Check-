# WMS — BẢNG TỔNG HỢP TOÀN BỘ

> **Ngày rà soát:** 2026-10-26  
> **Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
> **Branch:** `feature/wms-end-to-end-consistency`  
> **Code audit baseline:** `a41d5df18ee7cb3920915d47548e1db7899eb2f1` (2026-10-26; branch HEAD at evidence capture).  
> **CI exact-SHA evidence:** push run `40262798818` and PR run `40262804379` both SUCCESS on `a41d5df18ee7cb3920915d47548e1db7899eb2f1`; these cover configured QC workflow jobs only, not full WinForms build or SQL two-session/UI E2E.  
> **Last green baseline:** `a41d5df18ee7cb3920915d47548e1db7899eb2f1` (runs `40262798818` push and `40262804379` PR). **Current change candidate:** `bb15a604a292a75c61455d25094940ee80aa5671`; runs `40264579206` and `40264584779` were cancelled before checkout, so current candidate is NOT VERIFIED.  
> **PR:** #3 — Draft  
> **Nguyên tắc:** bảng này ghi nhận trạng thái theo bằng chứng code/SQL/test đang có; không dùng phần trăm ước lượng và không suy diễn PASS từ việc có class, migration hoặc source-contract test.

## 1. Kết luận điều hành

WMS đã có các ranh giới xử lý tồn kho tập trung qua KhoCore / `IStockMovementService`, các workflow nhập/xuất/Rework và lớp audit. Tuy nhiên, **chưa đạt Single Writer toàn hệ thống và chưa hoàn tất kiểm thử nghiệp vụ end-to-end**.

Ba điểm chặn quan trọng nhất:

1. **Giao Hàng Khách:** một số nhánh vẫn trừ `STOCKTP` bằng stored procedure hoặc SQL trực tiếp tại `PhieuKhoRepository`, sau đó mới điều chỉnh `SlotLot`. Đây là đường ghi tồn ngoài stock command thống nhất.
2. **Rework:** B0-02 đã được nối vào đường xuất thật: `ReworkStockService` resolve Phase 4 provider, fail-closed nếu thiếu và gọi `EnsureCanExport` trước movement; B0-03 đã khóa theo ticket và đọc ledger sau lock trong cùng transaction. Source-contract test đã có; SQL two-session/rollback vẫn chưa được chứng minh.
3. **Verification:** CI gần nhất đã xác nhận 3 job tự động thành công, nhưng chưa chứng minh build ứng dụng WinForms đầy đủ trên môi trường mục tiêu, G5/G6 UI journey nghiệp vụ, schema SQL sản xuất đại diện hoặc kiểm thử đồng thời xuyên toàn workflow.

**Quyết định:** giữ PR #3 ở Draft; không tuyên bố WMS hoàn thành cho tới khi các điều kiện đóng wave ở mục 7 đạt đủ.

## 2. Bảng tổng hợp phạm vi nghiệp vụ WMS

| ID | Phân hệ / nghiệp vụ | Luồng chuẩn cần bảo đảm | Trạng thái code đã rà | Rủi ro / phần còn thiếu | Ưu tiên |
|---|---|---|---|---|---|
| WMS-01 | KhoCore — tồn tổng | Một owner cho STOCKTP, Slot, SlotLot; transaction và audit thống nhất | Đã có `IStockMovementService / StockMovementService`; có adapter và command chuyên biệt | Chưa Single Writer toàn repo; còn writer legacy và QuarantineRelease là command chuyên biệt cần được quy định rõ ownership | P0 |
| WMS-02 | Nhập thành phẩm | QR → LOT → Slot/SlotLot → STOCKTP → history trong cùng UoW | Đã chuyển đường nhập chuẩn qua `StockMovementService.Receive` và `Infrastructure.Stock.StockReceivingRepository`; có applock theo QR/LOT | Cần schema/seed DBA đại diện; kiểm QR trùng, nhiều QR cùng LOT, nhập một phần, reopen LOT, capacity, metadata và rollback xuyên các bảng | P1 |
| WMS-03 | Xuất kho — Pick | Khóa Slot, phân bổ LOT, giảm lượng khả dụng/picked, tạo HangChoGiao và history | Đã điều phối qua `StockExportService` + `IStockMovementService.Pick` | Cần kiểm thử đồng thời Pick/Export, phân bổ Slot/LOT và đối chiếu số dư | P1 |
| WMS-04 | Hủy chờ giao | Khôi phục đúng lượng đã Pick, không cộng STOCKTP lần hai | Có `RestorePicked` và cập nhật trạng thái/histories trong workflow | Cần test retry, partial cancel, double cancel và rollback | P1 |
| WMS-05 | Xác nhận giao từ chờ giao | Export trừ STOCKTP + SlotLot, đóng HangChoGiao và ghi history một transaction | Đã có `ConfirmGiaoHangTuChoGiao` qua `IStockMovementService.Export`; có bảo vệ trạng thái/GetForUpdate | Cần SQL integration và UI E2E trên schema đại diện | P1 |
| WMS-06 | Xuất trực tiếp | Phân bổ FIFO/QR/PART/LOT, chứng từ và tồn đồng nhất | Có `StockExportService.XuatTrucTiep` và workflow idempotency | Rà hết nhánh Slot/A0, allocation nhiều LOT, retry và duplicate | P1 |
| WMS-07 | Giao Hàng Khách | FIFO RAM/DB, QR/PART/multi-LOT, STOCKTP + SlotLot + chứng từ + history cùng UoW | Đã có transaction/rollback ở các nhánh đã rà; một phần SlotLot qua movement | **P0 blocker:** `PhieuKhoRepository` còn gọi `Usp_Qrcode_Update_Stock2405`, `Usp_Qrcode_Update_Stock_SP`, SQL YMVN/`TruStockTpFifo` để trừ STOCKTP rồi mới điều chỉnh SlotLot | P0 |
| WMS-08 | Hàng lỗi — truy vết LOT | Tập hợp nguồn sản xuất, tồn kho, hàng trả; snapshot ảnh hưởng | Có `AffectedLotTraceService` và dữ liệu AffectedLot | Cần kiểm tra độ đầy đủ nguồn trace, snapshot bất biến và đồng bộ khi tồn thay đổi | P1 |
| WMS-09 | Hàng lỗi — Initial QC | QC đủ lượng ảnh hưởng; OK + NG = inspected; NG = Rework + loại bỏ | Có validation và lưu Initial QC trong `IAffectedLotTraceService.cs` | Cần SQL integration xác nhận unique ticket, concurrency, update affected LOT và rollback | P1 |
| WMS-10 | Rework — kế hoạch và xuất | Chỉ xuất theo `SoLuongRework` đã duyệt; retry không xuất trùng | Có ledger `OperationId`, app lock theo phiếu, unique filtered index migration Phase 14 | **Implementation đã đóng:** `ReworkStockService` gọi Phase 4 `EnsureCanExport` fail-closed trước Export; `QTChungService` giữ app lock/OperationId. SQL concurrency/rollback còn mở | P0 |
| WMS-11 | Rework — giao sản xuất | Chỉ giao lượng đã xuất và không vượt lượng QC duyệt | `GiaoHangRework`: Begin → transaction-owned applock → reload ticket → đọc ledger → validate → insert/status → Commit; có source-contract test thứ tự | SQL two-session và injected rollback sau insert vẫn cần test | P0 |
| WMS-12 | Rework — QC cuối | Chỉ QC sau khi xuất/giao đủ; OK + NG = lượng Rework | Có code `HangLoiPhase5To9Service.ConfirmReworkQC` kiểm Initial QC, `GetPlan`, `DaXuatDu`, `DaGiaoDu`, tổng OK+NG và kết quả đã tồn tại (`IQTChungService.cs:142–158`) | Cần xác minh các kiểm tra được serialize trong cùng transaction; kiểm tra DB unique/CHECK và SQL concurrency chưa được chứng minh bằng test tích hợp | P1 |
| WMS-13 | Disposition / loại bỏ | Tổng loại bỏ cuối = loại bỏ ban đầu + NG sau Rework | Có code `ConfirmDisposition` tính `SoLuongLoaiBoCuoi = SoLuongLoaiBoBanDau + ReworkNG` và lưu bảng Disposition (`IQTChungService.cs:170–188`) | Chưa chứng minh unique constraint/serialize tạo disposition đồng thời, quyền phê duyệt đầy đủ và test rollback/duplicate trên SQL thật | P1 |
| WMS-14 | Giao bù NG | Nghĩa vụ giao bù là chứng từ độc lập, số lượng theo yêu cầu khách hàng | `GiaoBuNGService` điều phối qua `IStockExportService` trong đường code đã rà | Phải chứng minh không suy ra giao bù từ NG; chống giao trùng/vượt nghĩa vụ; rollback và audit | P1 |
| WMS-15 | Quarantine / QC release | Hàng cách ly không được tính tồn khả dụng; chỉ release theo QC/quyền hợp lệ | Có `QuarantineReleaseService` thuộc KhoCore, khóa Slot và cập nhật SlotLot/STOCKTP/audit | Cần ghi rõ đây là command chuyên biệt thuộc KhoCore; kiểm concurrency, quyền và đối chiếu tồn | P1 |
| WMS-16 | Lịch sử / audit | Mỗi biến động có chứng từ, người thao tác, thời điểm, loại movement và liên kết nguồn | Workflow ghi history trong cùng UoW ở các luồng đã rà | `StockMovementService` không tự ghi StockHistory; phải rà mọi caller để tránh thiếu hoặc ghi hai lần | P1 |
| WMS-17 | Idempotency / retry | Một business operation retry không làm tăng/giảm tồn lần hai | Có key theo workflow cho một số luồng Xuất kho và Rework | Chưa có generic durable idempotency cho mọi `StockMovementRequest`; phải xác định key theo nghiệp vụ, không dùng GUID mới mỗi lần retry | P1 |
| WMS-18 | Legacy cleanup | Không còn caller ghi tồn ngoài owner đã quy định | Một số contract legacy đã được loại khỏi Nhập kho | Còn stored procedure/repository legacy, transitional adapters và caller cần static scan + bằng chứng | P1 |

## 3. Bảng tổng hợp kiến trúc và quyền sở hữu dữ liệu

| Thành phần | Trách nhiệm đúng | Trạng thái / quy tắc kiểm soát |
|---|---|---|
| Workflow services | FIFO, chọn chứng từ/LOT, kiểm tra nghiệp vụ, trạng thái, điều phối transaction | Được phép điều phối; không tự ý tạo đường ghi tồn thứ hai |
| `IStockMovementService` / `StockMovementService` | Receive, Pick, RestorePicked, Export, Move, ReturnFromRework và movement chuẩn | Caller sở hữu UoW với movement thông thường; phải rollback khi result fail |
| KhoCore application commands | Các nghiệp vụ stock chuyên biệt như Quarantine release | Được phép là command riêng nếu cùng tuân thủ ownership, khóa, transaction, audit và test |
| STOCKTP repository/adapter | Persistence tồn tổng theo contract | Không để repository nghiệp vụ tự trừ tồn ngoài command owner |
| Slot / SlotLot repository | Persistence tồn vị trí và LOT | Phải cùng transaction với STOCKTP khi nghiệp vụ thay đổi cả hai |
| StockHistory / workflow audit | Dấu vết biến động và chuyển trạng thái | Ghi cùng transaction với dữ liệu nghiệp vụ; xác định một nơi chịu trách nhiệm để không thiếu/trùng |
| Legacy stored procedures | Compatibility/đối chiếu trong giai đoạn chuyển đổi | Không được đồng thời trừ tồn với application command; chỉ loại bỏ sau khi đối chiếu FIFO/QR/PART và rollback |
| Business idempotency ledger | Ghi nhận một lần thực thi theo khóa nghiệp vụ | Kiểm tra trong transaction/lock trước physical mutation; unique constraint là lớp bảo vệ cuối |

## 4. Bảng tổng hợp Phase triển khai

| Phase / Wave | Phạm vi | Đánh giá hiện tại | Điều kiện đóng |
|---|---|---|---|
| Phase 1 | Architecture contract, ownership, state machine | 🟡 Một phần đã có | Chốt một mô hình ownership không mâu thuẫn giữa StockMovement và command chuyên biệt; loại tài liệu legacy sai |
| Phase 2 | KhoCore consolidation | 🟡 Một phần đã có | Các writer STOCKTP/Slot/SlotLot có owner rõ; dependency scan không còn bypass ngoài danh sách ngoại lệ được duyệt |
| Phase 3 | Central StockMovement | 🟡 Đã có command trung tâm | Kiểm toàn bộ caller; idempotency/business key, rollback và concurrency có SQL integration |
| Implementation Phase 4 — Receiving | Nhập kho | 🟡 Đã chuyển đường nhập chuẩn | Chạy đủ QR/LOT/multi-QR/partial/reopen/capacity/metadata/rollback trên schema DBA đại diện |
| Phase 5 | Xuất kho | 🟡 Đã có Pick/Export workflow | Đóng mọi nhánh trực tiếp, chờ giao, retry, duplicate, FIFO, Slot/LOT và concurrency |
| Phase 6 | Giao Hàng Khách | 🔴 Chưa đóng | Thay đường trừ STOCKTP tại repository/SP bằng command thống nhất; giữ parity FIFO/QR/PART/multi-LOT; test rollback |
| Implementation Phase 7 — Defect workflows | Hàng lỗi / Rework / Giao bù / Quarantine | 🔴 Chưa đóng | Nối **Rework process Phase 4 guard** vào export và delivery thật; QC cuối/disposition/compensation đúng số lượng; test đồng thời |
| Phase 8 | Shared cleanup | 🟡 Chưa đóng | Xóa trùng logic, thống nhất DI/composition root, transaction, audit, idempotency |
| Phase 9 | Legacy cleanup | 🟡 Chưa đóng | Static scan toàn repo; loại legacy callers/adapters sau khi chứng minh parity và không còn consumer |
| Phase 10 | Verification / release | 🔴 Chưa đóng | G1–G6 đủ bằng chứng, build ứng dụng thật, SQL integration, UI E2E, cleanup sạch, evidence đúng commit SHA |

## 5. Bảng Gate kiểm thử và bằng chứng

| Gate | Nội dung phải chứng minh | Evidence hiện có | Trạng thái |
|---|---|---|---|
| G1 — Traceability | Mỗi yêu cầu/luồng nối được code ↔ SQL object ↔ transaction ↔ test ↔ tài liệu; không còn gap chưa xử lý | Snapshot `PCTP.QC.UI/G1-gaps.csv` SHA `ab064ed03a6ff3d3c12bba192e24f4fafdd87f17`: 122 data rows. Chưa có remediation/evidence đóng đủ từng dòng | 🔴 OPEN |
| G2 — Unit/regression | Unit/source-contract và regression bảo vệ quy tắc nghiệp vụ | Runs `40254418356`/`40254422559` PASS 3/3 jobs trên baseline `fe73e91…`; chúng chạy trước thay đổi compile-item. Candidate hiện tại `e5f0eab…` đang chờ CI | 🟡 NOT VERIFIED ON CANDIDATE |
| G3 — SQL integration | Chạy implementation/repository thật với SQL engine cô lập, kiểm dữ liệu và rollback | CI runs `40253439019` và `40253444406` PASS SQL engine integration trên HEAD `a8393df…`; chỉ chứng minh SQL fixture/engine, không thay thế schema DBA representative | PASS LIMITED |
| G4 — Production procedure compatibility | Kiểm procedure thật trong DB thử nghiệm được cô lập, rollback-only và có G3 PASS cùng commit | Chưa có bằng chứng mới xác nhận schema/seed/procedure nghiệp vụ đại diện | 🔴 BLOCKED |
| G5 — Two-session concurrency | Hai session SQL trên cùng business key; chứng minh application lock serialize, tổng lượng không vượt quota và rollback không để lại mutation dở dang | Chưa có execution evidence SQL two-session trên exact candidate SHA | 🔴 OPEN |
| G6 — Full business E2E | Seed → thao tác UI thật → assert DB/ledger/history → cleanup; Remaining = 0; database cô lập | Các journey receive, QC direction, QC final, delivery HVN và UI assertions chưa được xác nhận PASS | 🔴 OPEN |

### CI snapshot — consolidated history

| Classification | Run IDs | SHA | Use |
|---|---|---|---|
| Current exact-SHA evidence at capture | `40262798818` push; `40262804379` PR | `a41d5df18ee7cb3920915d47548e1db7899eb2f1` | SUCCESS; configured QC workflow jobs only |
| Historical, not current-candidate evidence | `40254418356`, `40254422559`, `40254095397`, `40253439019`, `40253444406`, `40195012228`, `40199959465` | Multiple earlier SHAs (see original CI artifacts) | Retained only as historical records; never combine as one green candidate |

The current workflow does not build the complete WinForms `PCTP.csproj`, execute SQL two-session concurrency, or perform UI-to-database G6 journeys. The release gate owns the authoritative G1–G6 definitions and current status.

## 6. Danh sách blocker theo thứ tự ưu tiên

> **Quy ước Phase:** `Implementation Phase 1–10` là các wave triển khai WMS trong mục 4. `Rework process Phase 4–9` là các bước nghiệp vụ xử lý hàng lỗi trong `PHASE4_9_COMPLETION_STATUS.md`; hai hệ đánh số độc lập, không được viết tắt cùng một nhãn “Phase 4”.

| Mã | Blocker | Hậu quả nếu chưa xử lý | Hành động đóng |
|---|---|---|---|
| B0-01 | Giao Hàng Khách còn trừ STOCKTP qua SP/SQL trong `PhieuKhoRepository.cs:76–88, 160–170` | Hai nguồn mutation có thể lệch STOCKTP và SlotLot | **Owner:** WMS/KhoCore + Giao Hàng Khách. **Ngày rà:** 2026-10-25. Thiết kế delivery stock command theo allocation LOT/Slot; giữ FIFO/QR/PART/multi-LOT parity; migrate từng nhánh; tuyệt đối không chạy hai writer song song |
| B0-02 | Guard Rework quota | Guard được gọi trong đường stock mutation thật, sau lock phiếu và trước Export; provider thiếu thì từ chối | `ReworkStockService.cs:222–234` resolve `_phase4Provider`, fail-closed và gọi `EnsureCanExport` trước `_stockMovement.Export` (`:263`). Factory tạo lazy provider sau khi dựng `InitialQCService` (`XuLyHangLoiModuleFactory.cs:70–101`). `QTChungService` giữ transaction/app lock ngoài (`:318–359`); `UnitOfWork` hỗ trợ nested scope cùng transaction (`UnitOfWork.cs:30–40, 93–120`) | **Owner:** XuLyHangLoi/WMS. **Ngày rà:** 2026-10-26. Implementation blocker: **CLOSED**. Source-contract test mới bổ sung kiểm thứ tự lock → fail-closed → quota guard → movement; SQL two-session/concurrency/rollback acceptance vẫn là test gap của G3/G6, không được suy ra đã PASS |
| B0-03 | Rework delivery concurrency | Serialize authorization reads and writes | `QTChungService.cs` `GiaoHangRework`: UoW Begin → transaction-owned `QT_REWORK_DELIVERY` applock → reload ticket → GetXuat/GetGiao → validate → InsertGiao → status update → Commit. Test `ReworkDelivery_LocksAndReadsLedgersInsideTransactionBeforeInsert` added in `ReworkExportIdempotencyContractTests.cs` | **Owner:** XuLyHangLoi/WMS. **Ngày rà:** 2026-10-26. Implementation: CLOSED; SQL two-session + injected rollback acceptance: OPEN |
| B1-01 | G1 traceability chưa PASS | Không biết đầy đủ đường code/SQL/test nào thiếu bảo vệ | Hoàn thiện mapping thực tế; không sửa CSV để giảm tiêu chí |
| B1-02 | G4 thiếu schema/seed DBA xác nhận | Không chứng minh stored procedure thật tương thích | Xin schema-only export và seed đã làm sạch; chạy rollback-only trên DB cô lập |
| B1-03 | G5 SQL concurrency và G6 business UI journeys chưa PASS | Không chứng minh thao tác thực tế xuyên UI và dữ liệu | Lấy AutomationId từ UI catalog; bật journey thật; assert stock/history/guard; cleanup Remaining=0 |
| B1-04 | Generic movement idempotency chưa có cho mọi workflow | Retry ở workflow chưa có business key có thể lặp mutation | Chốt key bền vững từng nghiệp vụ; unique index/ledger; concurrent retry test |
| B1-05 | Static legacy writer/caller scan chưa đóng | Có thể còn đường ghi tồn ngoài owner | Scan toàn repository và SQL; phân loại caller; chỉ xóa sau parity |
| B1-06 | Build WinForms đầy đủ chưa được chứng minh trên môi trường mục tiêu | CI desktop runner không thay thế build ứng dụng thật | Build toàn solution/project trên môi trường .NET Framework mục tiêu và lưu log theo SHA |



### 6.1. Evidence index — file/line, owner và test

| ID | Code evidence đã đối chiếu trên code snapshot `e334fed…` | Test hiện có | Owner xử lý / trạng thái |
|---|---|---|---|
| B0-01 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs:76–88, 160–170` gọi hai stored procedure giao hàng; đường SQL/FIFO YMVN cần rà tiếp toàn file và các caller | `PCTP.QC.Tests/DeliveryTransactionContractTests.cs` (source-contract cho procedure/transaction); chưa có test chứng minh chuyển hẳn sang movement allocation | WMS/KhoCore + Giao Hàng Khách; open |
| B0-02 | `PCTP/Modules/XuLyHangLoi/Services/QTChungService.cs:298–359`; composition `PCTP/Shared/Common/WarehouseProcessNavigator.cs:123–132`; factory tạo guard tại `PCTP/Modules/XuLyHangLoi/Application/XuLyHangLoiModuleFactory.cs:86–101` | `PCTP.QC.Tests/ReworkExportIdempotencyContractTests.cs` có source-contract cho ID/lock/lookup, fail-closed quota guard và transaction/lock-before-read cho delivery; **chưa** có SQL test vượt hạn mức, hai session và rollback | XuLyHangLoi/WMS; open |
| B0-03 | `PCTP/Modules/XuLyHangLoi/Services/QTChungService.cs:384–435` | `PCTP.QC.Tests/ReworkWorkflowTransactionTests.cs` kiểm invariant/rollback abstraction; **chưa** chứng minh concurrency của giao Rework trên SQL | XuLyHangLoi/WMS; open |
| WMS-12 | `PCTP/Modules/XuLyHangLoi/Services/IQTChungService.cs:142–158` | Chưa thấy test SQL concurrency/duplicate cho ConfirmReworkQC | XuLyHangLoi/WMS; verify |
| WMS-13 | `PCTP/Modules/XuLyHangLoi/Services/IQTChungService.cs:170–188` | Chưa thấy test SQL concurrency/duplicate cho ConfirmDisposition | XuLyHangLoi/WMS; verify |

> Owner trong bảng là **nhóm chịu trách nhiệm kỹ thuật**, không phải cá nhân được assign. Ngày rà source: 2026-10-25. Các line number là theo code snapshot `e334fed…`; khi code thay đổi phải cập nhật lại line và SHA. Những test ghi “chưa có” nghĩa là chưa xác minh được test đáp ứng điều kiện đó, không khẳng định tuyệt đối không tồn tại test ở nơi khác.



### 6.2. B0-02 verification — CLOSED at implementation level (2026-10-26)

`QTChungService.XuatKhoRework` mở UoW và transaction-owned application lock, kiểm `OperationId`, rồi gọi `ReworkStockService.XuatKhoRework`. Trong stock service, sau khi lấy row lock bằng `UpdateStatusIfCurrentIs` và kiểm tra phiếu, `_phase4Provider()` được resolve; nếu null thì rollback/fail-closed; `EnsureCanExport(phieuXuLyId, soLuong, nguoiXuat)` chạy trước `_stockMovement.Export`. Factory cung cấp lazy provider để phá vòng phụ thuộc `ReworkStockService → AffectedLotTraceService → InitialQCService → ReworkPhase4Service` mà không bỏ guard. `UnitOfWork.Begin()` nested scope dùng chung transaction đang mở.

Test mới `ReworkExport_EnforcesPhase4QuotaFailClosedBeforeStockMovement` kiểm tra thứ tự lock → provider → fail-closed → quota guard → stock movement. Chỉ đóng implementation blocker; không tuyên bố đã có SQL two-session test hoặc full G6 Rework E2E.

## 7. Lộ trình đóng WMS theo cuốn chiếu

| Thứ tự | Gói công việc | Đầu ra bắt buộc | Tiêu chí hoàn tất |
|---|---|---|---|
| 1 | Chốt traceability inventory (G1) | Ma trận requirement → code → SQL → transaction → test → doc | Không còn gap không được phân loại; mọi ngoại lệ có owner và lý do |
| 2 | Rework Phase 4–9 | Guard export/delivery, QC cuối, disposition, compensation, workflow audit | Test vượt hạn mức, thiếu QC, retry, duplicate, concurrency, rollback đều PASS |
| 3 | Delivery Single Writer | Stock command theo allocation FIFO/QR/PART/multi-LOT | STOCKTP = tổng SlotLot hợp lệ; rollback xuyên tồn/chứng từ/history |
| 4 | Receiving hardening | SQL test QR/LOT/multi-QR/partial/reopen/capacity/metadata | Không duplicate; mọi bảng cùng transaction; retry an toàn |
| 5 | Movement idempotency + concurrency | Business key ledger và test nhiều session | Một operation chỉ có một physical mutation; retry trả lại kết quả cũ |
| 6 | Legacy cleanup | Dependency scan + danh sách caller còn lại | Không còn writer bypass ngoài command owner được phê duyệt |
| 7 | G4/G5/G6 | Schema/seed cô lập, UI Automation, DB assertion và cleanup | G4 rollback-only; G5 concurrency PASS; G6 UI-to-DB business journeys PASS; Remaining = 0 |
| 8 | Release closeout | Build log, CI SHA, SQL report, test artifacts, tài liệu VI/JP nếu cần | Tất cả evidence cùng commit SHA; PR mới chuyển Ready khi đủ tiêu chí |

## 8. Quy tắc nghiệm thu thống nhất

Một luồng WMS chỉ được đánh dấu **DONE** khi có đủ tất cả:

- [ ] Code production thực sự gọi đúng command/guard; không chỉ có class hoặc factory registration.
- [ ] Business rule và hậu điều kiện số lượng được xác định bằng công thức rõ ràng.
- [ ] STOCKTP, Slot, SlotLot, chứng từ, trạng thái và history nhất quán trong cùng transaction khi nghiệp vụ yêu cầu.
- [ ] Lock/concurrency và idempotency ngăn duplicate hoặc vượt hạn mức.
- [ ] Test có cả trường hợp thành công, lỗi, partial, retry, duplicate, concurrent và rollback.
- [ ] SQL integration chạy implementation thật; schema/seed có nguồn xác nhận.
- [ ] UI journey thao tác trên ứng dụng thật và assert DB sau thao tác.
- [ ] Cleanup sạch; không để fixture tồn đọng.
- [ ] Tài liệu phản ánh đúng code đã kiểm chứng.
- [ ] CI/test evidence thuộc đúng commit SHA đang nghiệm thu.



### 8.1. Invariant đối chiếu tồn kho dùng trong SQL integration

Mẫu kiểm tra dưới đây là **logic invariant**, chưa phải câu SQL chạy được trên mọi schema. Trước khi đưa vào test phải xác minh kiểu dữ liệu, cột quantity, trạng thái SlotLot hợp lệ và quy tắc có/không tính hàng quarantine. Chỉ so sánh tồn khả dụng cùng phạm vi PartNo/LOT/Slot; không cộng các trạng thái cách ly hoặc hàng đã xuất.

```sql
-- Pseudocode: thay tên cột/trạng thái sau khi DBA xác nhận schema.
SELECT s.PartNo
FROM dbo.STOCKTP AS s
LEFT JOIN (
    SELECT PartNo, SUM(Qty) AS SlotLotQty
    FROM dbo.SlotLot
    WHERE <điều kiện SlotLot được tính vào tồn khả dụng>
    GROUP BY PartNo
) AS l ON l.PartNo = s.PartNo
WHERE ISNULL(s.Qty, 0) <> ISNULL(l.SlotLotQty, 0);
-- Kỳ vọng: 0 dòng sau mỗi test đã commit; khi test rollback phải khôi phục snapshot ban đầu.
```

Không copy nguyên câu query mẫu vào production hoặc CI trước khi thay placeholder bằng schema contract đã xác nhận.

## 9. Quyết định hiện tại

**WMS: IN PROGRESS — NOT READY FOR RELEASE.**

- PR #3 tiếp tục để Draft.
- B0-02 implementation fail-closed và B0-03 transaction-owned applock/read-after-lock đã có source-contract regression tests; CI unit/regression PASS trên code SHA `dd5ad9e…`. SQL concurrency/rollback acceptance của cả hai vẫn OPEN. Chưa coi SQL concurrency acceptance của Rework đã PASS.
- B0-01 vẫn OPEN: `CapNhapKho`/`CapNhapKhoSP` dùng stored procedure trừ STOCKTP rồi `TruKhoAoTuKetQuaSP` pick SlotLot; `CapNhapKhoYMVN` tự trừ STOCKTP qua `TruStockTpFifo` rồi pick SlotLot. G1/G4/G5/G6 vẫn OPEN.
- Không đánh dấu Phase 6/7/10 hoàn tất khi các blocker và gate tương ứng chưa có bằng chứng nghiệm thu.
- Không dùng phần trăm hoàn thành ước lượng làm chỉ số nghiệm thu.
- Không sửa tiêu chí test hoặc xóa regression chỉ để làm xanh CI.
- Không thực hiện mutation trên production/shared SQL.
- Mọi lần cập nhật bảng này phải ghi commit SHA, workflow run, phạm vi test và giới hạn bằng chứng.


## 10. Rà soát bổ sung 2026-10-26 — B0-01 và CI HEAD

### B0-01: phạm vi writer xác minh trực tiếp

`PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs`:

- `CapNhapKho` gọi `Usp_Qrcode_Update_Stock2405`, sau đó gọi `TruKhoAoTuKetQuaSP` để pick SlotLot, rồi đóng hàng chờ giao và commit.
- `CapNhapKhoSP` gọi `Usp_Qrcode_Update_Stock_SP`, sau đó thực hiện cùng bước pick SlotLot.
- `CapNhapKhoYMVN` đọc `STOCKTP` bằng `UPDLOCK, HOLDLOCK`, gọi `TruStockTpFifo` để cập nhật `SLXUAT/SLCONLAI`, rồi `BulkStockAdjustService.TruKhoAoTheoLot` pick SlotLot.
- `BulkStockAdjustService` đã fail-fast nếu không cấu hình `IStockMovementService`; khi cùng UoW (`manageTransaction:false`), thiếu SlotLot sẽ throw để caller rollback. Tuy nhiên, nó vẫn chỉ là bước SlotLot sau khi STOCKTP đã bị mutate ở SP/SQL; do đó chưa phải Single Writer.
- `Usp_Qrcode_Update_Stock2405.sql` có quy tắc LOT vật lý, QR/DG, transaction/savepoint và khóa theo identity giao; `Usp_Qrcode_Update_Stock_SP.sql` có FIFO, applock transaction-owned và cập nhật STOCKTP + TMP. Các hành vi này phải được giữ parity khi chuyển allocation sang command.

**Không được xóa hoặc bypass hai stored procedure trong bước sửa nhỏ.** Cần thay đổi theo một delivery command nhận kết quả allocation đã xác thực (QR/PART/FIFO/multi-LOT), phân bổ chính xác LOT/SlotLot, rồi thực hiện STOCKTP + SlotLot + chứng từ + HangChoGiao + history trong cùng UoW. Chỉ xóa writer cũ sau khi SQL integration chứng minh parity, concurrency, idempotency và rollback.

### CI evidence trên HEAD tài liệu hiện tại

- HEAD: `a8393dfbca2dca7755e6abc7f6efc6d5ddae95da`.
- Run `40253439019`: SUCCESS, jobs `unit-regression-tests`, `sql-engine-integration`, `desktop-runner-build` đều success.
- Run `40253444406`: SUCCESS, cùng 3 jobs đều success.
- Hai run dùng HEAD docs-only. Bằng chứng unit/source-contract của thay đổi code B0-02/B0-03 thuộc code SHA `dd5ad9e07f6b35c83cfb036c25514152e51dc679`; không diễn giải CI docs-only như build code mới.
- G1 snapshot: 122 data rows ở CSV SHA `ab064ed03a6ff3d3c12bba192e24f4fafdd87f17`.


## 11. Compile inventory — exact branch snapshot `fe73e91cf25737def51a85ab0642723e5711085f`

The recursive tree contains 24 C# files absent from the legacy explicit-include `PCTP/PCTP.csproj` at this snapshot. Classification is based on source location and current startup wiring, not an assertion that all excluded screens are permanently obsolete.

| Group | Files | Decision |
|---|---:|---|
| KhoCore QuarantineRelease production types/services/repository | 4 | Add to PCTP compile items |
| ReworkWorkflowTransaction + QRCodeWorkflowAdapter | 2 | Add to PCTP compile items |
| `Workflow/Tests/FifoSessionStateRegressionTests.cs` | 1 | Test-only; do not compile into WinForms production assembly |
| YMVN delivery forms | 10 | Keep excluded pending explicit product-owner confirmation; `Program.cs` has the YMVN entry points commented and current startup is `Main_APP` |
| NhanLaiNG forms | 5 | Keep excluded pending caller/entry-point confirmation |
| `Reports/IN_GHP_LOT_YMVN` | 2 | Keep excluded pending confirmation that YMVN report workflow is active |

After the production compile-item correction, 18 files remain intentionally outside `PCTP.csproj`: 17 legacy/UI candidates requiring an explicit business decision and 1 test-only source. Do not bulk-include designer/code-behind pairs without their resource/dependency review.

The six production compile items are now included in this project file. This repository-side edit is not a substitute for a real MSBuild of the full solution on Windows with the required DevExpress, NuGet and signing dependencies.


## 9. CI evidence normalization (2026-10-26)

The last green exact-SHA baseline is push `40262798818` and PR `40262804379`, both SUCCESS on `a41d5df18ee7cb3920915d47548e1db7899eb2f1`. Current change candidate `bb15a604a292a75c61455d25094940ee80aa5671` had push `40264579206` and PR `40264584779` cancelled before checkout. Earlier run IDs and SHAs are historical context only; they must not be described as current-candidate evidence. Canonical G1/G5 definitions and release decisions are owned by `WMS_RELEASE_GATE.md`.


## 10. B0-01 design decision (2026-10-26)

- F-Y1 = `part`: one delivery PART line owns one or more LOT allocation slices; quantity is the sum of those LOT slices. Do not flatten away PART-to-LOT traceability.
- F-Y4 = `atomic`: all STT/parts in one business command commit or roll back together. A larger daily batch may consist of multiple independently retryable business commands; it is not one transaction for the whole day.
- The new `DeliveryStockCommand` / `IDeliveryStockCommandHandler` are contract scaffolding only. No production handler is wired yet. Legacy `Usp_Qrcode_Update_Stock2405`, `Usp_Qrcode_Update_Stock_SP`, `Usp_Qrcode_Take_LotYMVN2405`, and `TruStockTpFifo` remain untouched until parity tests pass.
- Rework export and delivery now use the same transaction-owned application-lock resource `QT_REWORK_TICKET:{PhieuXuLyId}`. The ReworkStockService row status guard remains an additional conditional guard, not a substitute for the shared resource lock. SQL two-session execution remains required.


## 11. Current change candidate — verification state

The delivery allocation contract and shared Rework application-lock resource were committed after the last green baseline. The current candidate has no completed CI result: both observed workflow runs were cancelled before checkout. Do not mark G2, G5, B0-01 or release as PASS on this basis. Full Windows MSBuild and real-method SQL/UI harness execution have not been run in this environment.
