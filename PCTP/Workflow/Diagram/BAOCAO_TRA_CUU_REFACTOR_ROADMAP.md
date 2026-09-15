# BAOCAO / TRA CỨU — REFACTOR ROADMAP

## Progress snapshot — 2026-09-15

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| A. Main_APP / Shell integration | **DONE** | `WmsControlCenterBar` + `WmsWorklistBar` + `WarehouseDashboardBar` đã được host trong `Main_APP`; refresh dùng chung. |
| B. WMS Help / contextual routing | **MOSTLY DONE** | Help Service/Catalog/Context/Overlay/Guide đã có; routing giao hàng đi qua `HVN_PGH` + `CustomerTableConfig`; không dùng legacy customer form để routing. |
| C. Báo cáo / Tra cứu | **IN PROGRESS — DELIVERY TRACE REFACTORED** | Stock/Current Stock/QC/Inspection đã có. Delivery Trace đã sửa boundary, customer mapping và QR join; UI dùng read service mới. |
| D. Legacy report cleanup | **PARTIAL** | `FormStockHistory` và `FormInspectionHistory` đã move; caller-by-caller verification và các report/repository legacy còn lại chưa xong. |
| E. Repository/read-side decomposition | **DELIVERY TRACE SLICE DONE** | Delivery Trace contract/repository/read service đã được làm gọn; không còn nhồi slot-history/pending-delivery vào delivery trace repository. Các slice khác vẫn tiếp tục phân rã. |
| F. Verification / Build / Regression | **NOT SIGNED OFF** | Chưa có build Debug net472/C# 7.3 và SQL runtime test trên môi trường DB thực tế trong phiên này. Đây vẫn là gate cuối. |

> **Lưu ý:** trạng thái code đã refactor không đồng nghĩa với “đã verified”. Build Debug net472/C# 7.3 và regression thực tế vẫn phải được chạy trước khi merge về `master`.

## Phase 1 — Boundary

- [x] Tạo branch riêng cho slice BaoCao/TraCuu.
- [x] Tạo `Modules/BaoCao`.
- [x] Tạo query contract và read model baseline.
- [x] Chốt nguyên tắc read-only.

## Phase 2 — Legacy inventory

- [x] Rà soát report legacy hiện hữu trong `PCTP.csproj` và module trees.
- [x] Rà soát `FormStockHistory`, `FormInspectionHistory` và các history/lookup repository liên quan.
- [x] Xác định các nhóm read/write đang bị trộn.
- [x] Phân loại ban đầu `KEEP / MOVE / EXTRACT / DELETE`.
- [x] Ghi kết quả vào `BAOCAO_TRA_CUU_SYSTEM_ANALYSIS.md`.
- [ ] Hoàn tất caller-by-caller verification trước khi xóa namespace `VIEWSTOCK`.

## Phase 3 — Query ports

- [ ] `IItemHistoryQuery` — QR / LOT / Part / Document.
- [x] `IStockHistoryQuery` — lịch sử nhập/xuất kho.
- [x] `ICurrentStockQuery` — tồn hiện tại.
- [ ] `IReceivingHistoryQuery` — lịch sử nhập.
- [ ] `IExportHistoryQuery` — lịch sử xuất.
- [ ] `IDeliveryHistoryQuery` — lịch sử giao chuyên biệt.
- [x] `IQualityHistoryQuery` — QC / Inspection history.
- [x] `IQrTraceQuery` — tra cứu theo QR/carton.
- [x] `ILotTraceQuery` — tra cứu lịch sử theo LOT.
- [x] `ICustomerDeliveryQuery` — tra cứu lịch sử giao theo khách hàng.
- [x] `DeliveryTraceRow` là master theo delivery/carton evidence.
- [x] `DeliveryLotTraceRow` là detail theo LOT.
- [x] Tái sử dụng `PCTP.Common.LotCodeHelper.ParseCompositeLot`; không tạo parser LOT riêng trong BaoCao.

## Phase 4 — Delivery Trace read infrastructure

### 4.1 Source of truth

- [x] `dbo.LUUPHIEUGIAOHANG` là nguồn delivery evidence.
- [x] `dbo.LUUDOCQRCODE` là nguồn FCC/HVN QR/document evidence.
- [x] Không sử dụng `GIAOHANGYMN`.
- [x] Không sử dụng `YAMAHAQRCDE_SP`.
- [x] Không phát hiện/đoán cột bằng `INFORMATION_SCHEMA`.

### 4.2 Customer mapping

- [x] `100001` = `HON DA - VIET NAM`.
- [x] `100001` lọc `P.NHAMAY` bằng:
  - `HON DA - VIET NAM(NHA MAY VP)`
  - `HON DA - VIET NAM(NHA MAY HN)`
- [x] `100002` = `YAMAHA - VIET NAM`.
- [x] `100002` lọc `P.NHAMAY = 'YAMAHA - VIET NAM'`.
- [x] Không chỉ stamp `CustomerNo` vào output rồi trả dữ liệu của maker khác.

### 4.3 QR / delivery join

- [x] Không dùng `STT` làm business join key giữa `LUUDOCQRCODE` và `LUUPHIEUGIAOHANG`.
- [x] Join theo `NHAMAY`.
- [x] Join theo `MAHANGFCC = MAHANG`.
- [x] Join theo ngày `NGAYXUAT = NGAYGIAO` ở DATE precision.
- [x] Honda và maker khác: `D.GIOXUAT = P.GIOGIAOFCC`.
- [x] Yamaha: `D.GIOGIAO` đối chiếu với `P.CUA`.
- [x] Yamaha time/code normalization không ép `GIOGIAO` sang `INT`; giữ được giá trị alphanumeric như `J0663`.
- [x] Yamaha normalization xử lý leading zero: `01`, `001`, `0001` → `1`; `0J0663` → `J0663`.
- [x] Dùng `LEFT JOIN` để không làm mất delivery row khi QR evidence chưa match.
- [x] Không dùng `TOP 1` tùy tiện để giải quyết duplicate delivery rows.

### 4.4 Repository / service boundary

- [x] `IDeliveryTraceRepository` chỉ giữ các read use case cần cho Delivery Trace.
- [x] `DeliveryTraceRepository` chỉ giữ SQL/read mapping của delivery trace.
- [x] Loại bỏ các method slot-history / pending-delivery khỏi delivery trace repository boundary khi chưa có source contract xác nhận.
- [x] `DeliveryTraceReadService` chỉ orchestration/delegation, không chứa SQL.
- [x] `FormBaoCaoTraceability` chuyển sang `DeliveryTraceReadService`.
- [x] `Directory.Build.targets` chuyển compile graph sang read service mới và exclude adapter cũ khỏi build graph.
- [ ] Tách tiếp thành các repository/query slice riêng khi source SQL của timeline/slot/pending đã được xác nhận đầy đủ.

## Phase 5 — UI

- [x] `FormBaoCaoMain` — navigation-only entry point.
- [x] `FormBaoCaoTraceability` — QR / LOT / Part / Customer search và master/detail LOT.
- [x] Customer lookup hiển thị tên nghiệp vụ ổn định, không phụ thuộc dữ liệu giả trong source table.
- [x] Delivery Trace UI không tự truy cập SQL.
- [ ] Timeline: Production → QC → Nhập kho → Xuất → Giao → Customer.
- [x] Báo cáo lịch sử kho + tồn hiện tại.
- [ ] Báo cáo nhập/xuất/giao hàng chuyên biệt.
- [x] History QC/Inspection.
- [x] Export Excel / print cho stock report.
- [x] Export Excel cho inspection master.

## Phase 6 — Main_APP / WMS Control Center

- [x] Main_APP giữ vai trò Shell/Navigation/Dashboard thay vì chứa query logic.
- [x] Tách composition dashboard khỏi Main_APP.
- [x] Tách QR machine switch khỏi Main_APP.
- [x] Thêm WMS Help Service/Catalog/Context dưới `Shell/Help`.
- [x] Thêm màn hình hướng dẫn sử dụng theo nghiệp vụ.
- [x] Nút `Hướng dẫn sử dụng` mở WMS guide có topic + sơ đồ Mermaid.
- [x] Chuẩn hóa tài liệu hướng dẫn theo flow: Mục đích → Điều kiện → Sơ đồ → Thao tác → Xác nhận → Lỗi → Cách xử lý.
- [ ] Redirect từng entry point tra cứu legacy còn lại sang BaoCao.
- [x] Quick Search QR/LOT/Part ở Shell.
- [x] Entry point Báo cáo/Tra cứu đi qua `BaoCaoNavigator`.
- [x] Worklist/Cảnh báo WMS dùng query source thực tế đã xác định.
- [x] F1 contextual help ở Shell overlay.

### Worklist / Dashboard checkpoint

- [x] `WmsControlCenterBar` được host bởi `Main_APP`.
- [x] `WmsWorklistBar` được host bởi `Main_APP`.
- [x] `WarehouseDashboardBar` được host bởi `Main_APP`.
- [x] Control Center `Làm mới` refresh dashboard + KPI + worklist.
- [x] Dashboard và Worklist dùng repository instances chung từ `MainAppDashboardFactory`.
- [x] Worklist dùng nguồn số liệu thực đã xác định.
- [ ] Chưa thêm KPI giao hàng mới khi chưa xác minh source dữ liệu thực.
- [ ] Navigation `Lệch A0` vẫn cần dedicated navigator nếu source màn hình chuyên biệt được xác nhận.

## Phase 7 — Legacy removal

- [ ] Tách read methods khỏi `IPhieuTrackingRepository`.
- [ ] Chuyển read implementation của `PhieuTrackingRepository` sang BaoCao query infrastructure.
- [ ] Giữ write methods với owner nghiệp vụ thích hợp.
- [x] Chuyển `FormStockHistory` sang `Modules/BaoCao/UI/FormBaoCaoStockHistory.cs`.
- [x] Chuyển `FormInspectionHistory` sang `Modules/BaoCao/UI/FormBaoCaoQualityHistory.cs`.
- [x] Xóa form/designer legacy của Inspection History.
- [ ] Chỉ xóa report tổng hợp cũ khi replacement tương đương đã được xác nhận.
- [x] Giữ report chứng từ thuộc đúng business module.
- [ ] Xóa compile entry / using / repository không còn dùng của các slice tiếp theo.
- [ ] Loại bỏ `PCTP.VIEWSTOCK` khi toàn bộ dependency đã được phân rã.

## Phase 8 — Verification

- [ ] Build Debug net472/C# 7.3 trên môi trường phát triển thực tế.
- [ ] Kiểm tra startup `Main_APP`.
- [ ] Kiểm tra các module hiện hữu không thay đổi behavior.
- [ ] Runtime-test Delivery Trace với DB thật.
- [ ] Regression các trường hợp Honda VP / Honda HN / Yamaha.
- [ ] Regression QR có match và QR không match.
- [ ] Regression duplicate delivery rows.
- [ ] Regression `GIOGIAO`: `01`, `001`, `0001`, `J0663`, `0J0663`.
- [ ] Parity query mới so với legacy.
- [x] Kiểm tra export path đã được giữ cho stock/inspection report.
- [x] Kiểm tra BaoCao query layer không tham chiếu write-side service.
- [x] Kiểm tra Delivery Trace adapter không có write path.
- [ ] Chỉ merge về `master` sau khi branch chạy ổn định.

> **Verification note:** code đã được cập nhật trực tiếp trên branch `feature/baocao-deliverytrace-refactor`, nhưng phiên làm việc này không có SQL Server/Visual Studio build runtime để ký xác nhận Debug net472 hoặc SQL execution. Không coi các checkbox verification chưa chạy là DONE.

## Phase 9 — User Guide / Training Documentation

Tài liệu vận hành chuẩn:

`PCTP/Workflow/Diagram/WMS_USER_GUIDE.md`

- [x] Sơ đồ Mermaid tổng thể WMS.
- [x] Sơ đồ luồng nghiệp vụ hàng → QC → nhập → tồn → xuất → giao.
- [x] Hướng dẫn Dashboard / Control Center.
- [x] Hướng dẫn Nhập kho QR.
- [x] Hướng dẫn Nhập kho không QR.
- [x] Hướng dẫn Xử lý hàng lỗi.
- [x] Hướng dẫn Giao hàng HVN / YMVN.
- [x] Hướng dẫn Báo cáo & Traceability.
- [x] Hướng dẫn Tra cứu LOT.
- [x] Hướng dẫn Tra cứu QR.
- [x] Hướng dẫn chuyển máy bắn QR.
- [x] Quy trình xử lý lỗi và chuẩn thông tin báo IT.
- [x] Quy định chuẩn để tạo một hướng dẫn nghiệp vụ mới.

## Quy tắc bắt buộc — CLEAN AFTER MOVE

> Khi một use case đã được chuyển sang BaoCao và có replacement chạy qua query port, phải dọn sạch implementation cũ của chính use case đó trong cùng slice: UI cũ, designer/resource không còn dùng, compile entry, using/repository trung gian không còn caller.

> Không xóa legacy chỉ vì tên có chữ `Report`, `History`, `Lookup` hoặc `TraCuu`. Phải xác định ownership, caller và behavior trước.

> Không chuyển report chứng từ nghiệp vụ sang BaoCao nếu việc chuyển làm BaoCao trở thành owner của transaction.

> BaoCao chỉ sở hữu **read use case và read model**; business module vẫn sở hữu transaction và write-side.

> Không để lại compatibility facade chỉ để che compile error. Nếu caller cũ còn tồn tại, caller đó phải được chuyển sang contract/UI mới trước khi xóa implementation cũ.
