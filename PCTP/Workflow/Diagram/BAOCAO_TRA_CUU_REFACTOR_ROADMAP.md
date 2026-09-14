# BAOCAO / TRA CỨU — REFACTOR ROADMAP

## Progress snapshot — 2026-09-15

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| A. Main_APP / Shell integration | **DONE** | `WmsControlCenterBar` + `WmsWorklistBar` + `WarehouseDashboardBar` đã được host trong `Main_APP`; refresh được nối chung. |
| B. WMS Help / contextual routing | **MOSTLY DONE** | Help Service/Catalog/Context/Overlay/Guide đã có; routing giao hàng theo `HVN_PGH` + `CustomerTableConfig`; F1 đã được gắn ở overlay toàn ứng dụng. Chỉ còn rà caller/module-specific edge cases. |
| C. Báo cáo / Tra cứu | **PARTIAL** | Stock/Current Stock/QC/Inspection đã có; Delivery Trace có master/detail nhưng QR/customer/timeline/parity schema vẫn chưa đủ để gọi là hoàn tất. |
| D. Legacy report cleanup | **PARTIAL** | `FormStockHistory` và `FormInspectionHistory` đã move; caller-by-caller verification và các report/repository legacy còn lại chưa xong. |
| E. Repository/read-side decomposition | **PARTIAL** | BaoCao query adapters đã tách cho các slice hiện có; `IPhieuTrackingRepository` chưa được phân rã hoàn toàn và `VIEWSTOCK` vẫn còn dependency. |
| F. Verification / Build / Regression | **NOT DONE** | Chưa có bằng chứng build Debug net472/C# 7.3 thực tế trên môi trường phát triển; startup/regression/parity chưa được sign-off. |

> **Không đánh dấu Phase 8 hoàn thành chỉ vì code đã cập nhật.** Build Debug net472/C# 7.3 và regression thực tế vẫn là gate cuối.

## Phase 1 — Boundary

- [x] Tạo branch riêng từ `master`.
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
- [ ] `IDeliveryHistoryQuery` — lịch sử giao.
- [x] `IQualityHistoryQuery` — QC / Inspection history.
- [x] `IQrTraceQuery` — tra cứu theo QR/carton.
- [x] `ILotTraceQuery` — tra cứu lịch sử theo LOT.
- [x] `ICustomerDeliveryQuery` — tra cứu lịch sử giao theo khách hàng.
- [x] Xác định `DeliveryTraceRow` là master theo carton/QRCode và `DeliveryLotTraceRow` là detail theo LOT.
- [x] Tái sử dụng `PCTP.Common.LotCodeHelper.ParseCompositeLot` cho `LUUPHIEUGIAOHANG.LOT` dạng composite; không tạo parser riêng trong BaoCao.

## Phase 4 — Query infrastructure

- [x] Implement SQL read-only adapter cho stock history/current stock.
- [x] Implement SQL read-only adapter cho InspectionLog history.
- [x] Không gọi WinForms hoặc business service write-side từ query layer.
- [x] Mapping DB -> BaoCao read models cho stock history/current stock.
- [x] Mapping DB -> BaoCao read models cho inspection master/detail.
- [x] Kiểm kê schema đã xác nhận của `LUUPHIEUGIAOHANG`.
- [ ] Xác nhận chính xác cột QRCode/carton.
- [ ] Xác nhận chính xác cột dữ liệu tem khách hàng.
- [ ] Xác nhận nguồn CustomerCode/CustomerName.
- [ ] Xác nhận khóa liên kết LOT -> Production / Receiving / QC / NG-Rework.
- [x] Implement `DeliveryTraceQueryService` theo `DeliveryKey`, chỉ đọc `LUUPHIEUGIAOHANG`, không join `DOCQRCODE` khi chưa có khóa lịch sử xác nhận.
- [ ] Hoàn tất parity với toàn bộ stored procedures/legacy queries.
- [ ] Batch query khi có thể; tránh N+1 query theo từng LOT/Part.
- [x] Chuẩn hóa null/date/quantity/status ở read-model mapping.

## Phase 5 — UI

- [x] `FormBaoCaoMain` — navigation-only entry point.
- [x] `FormBaoCaoTraceability` — tra cứu Delivery/QR/LOT/Part/Customer và master/detail LOT.
- [ ] Hoàn tất tra cứu QR/carton lịch sử sau khi cột QR persisted được xác nhận.
- [ ] Hoàn tất tra cứu tên khách hàng sau khi source CustomerCode/CustomerName được xác nhận.
- [ ] Timeline: Production → QC → Nhập kho → Xuất → Giao → Customer.
- [x] Báo cáo lịch sử kho + tồn hiện tại.
- [ ] Báo cáo nhập/xuất/giao hàng chuyên biệt.
- [x] History QC/Inspection.
- [x] Export Excel / print cho stock report.
- [x] Export Excel cho inspection master.

## Phase 6 — Main_APP / WMS Control Center

- [x] Main_APP tiếp tục giữ vai trò Shell/Navigation/Dashboard thay vì chứa query logic.
- [x] Tách composition dashboard khỏi Main_APP.
- [x] Tách QR machine switch khỏi Main_APP.
- [x] Thêm WMS Help Service/Catalog/Context dưới `Shell/Help`.
- [x] Thêm màn hình hướng dẫn sử dụng theo nghiệp vụ.
- [x] Nút `Hướng dẫn sử dụng` trên Main_APP mở WMS guide có topic + sơ đồ Mermaid.
- [x] Chuẩn hóa tài liệu hướng dẫn theo flow: Mục đích → Điều kiện → Sơ đồ → Thao tác → Xác nhận → Lỗi → Cách xử lý.
- [ ] Redirect từng entry point tra cứu legacy sang BaoCao.
- [x] Thêm Quick Search QR/LOT/Part ở Shell.
- [x] Tách entry point Báo cáo/Tra cứu khỏi Shell bằng `BaoCaoNavigator`; Shell không còn tự new trực tiếp các form BaoCao.
- [x] Thêm Worklist/Cảnh báo WMS sau khi xác định query source cho từng KPI.
- [x] Gắn F1 contextual help ở Shell overlay; routing topic dùng `WmsHelpContext`.

### Worklist / Dashboard implementation checkpoint

- [x] `WmsControlCenterBar` được host bởi `Main_APP`.
- [x] `WmsWorklistBar` được host bởi `Main_APP`.
- [x] `WarehouseDashboardBar` được host bởi `Main_APP`.
- [x] Control Center `Làm mới` refresh dashboard legacy + dashboard KPI + worklist.
- [x] Dashboard và Worklist dùng chung repository instances từ `MainAppDashboardFactory`.
- [x] Worklist chỉ dùng nguồn số liệu thực: QC status, `DemPhieuChoNhap()`, `DemLechDoiChieu()`.
- [ ] Chưa thêm KPI giao hàng mới khi chưa xác minh source dữ liệu thực.
- [ ] Navigation `Lệch A0` vẫn đang dùng entry point Nhập kho hiện hữu; cần dedicated navigator nếu repo xác nhận có màn hình chuyên biệt.

## Phase 7 — Legacy removal

- [ ] Tách read methods khỏi `IPhieuTrackingRepository`.
- [ ] Chuyển read implementation của `PhieuTrackingRepository` sang BaoCao query infrastructure.
- [ ] Giữ write methods với owner nghiệp vụ thích hợp.
- [x] Chuyển `FormStockHistory` sang `Modules/BaoCao/UI/FormBaoCaoStockHistory.cs` và xóa form/designer/resx cũ.
- [x] Chuyển `FormInspectionHistory` sang `Modules/BaoCao/UI/FormBaoCaoQualityHistory.cs`.
- [x] Xóa `FormInspectionHistory.cs` và `FormInspectionHistory.Designer.cs` cũ.
- [x] Xóa compile graph của `FormInspectionHistory` khỏi legacy path.
- [ ] Chỉ xóa report tổng hợp cũ khi có replacement tương đương.
- [x] Giữ report chứng từ thuộc đúng business module.
- [ ] Xóa compile entry / using / repository không còn dùng của các slice tiếp theo.
- [ ] Loại bỏ `PCTP.VIEWSTOCK` khi toàn bộ dependency đã được phân rã.

## Phase 8 — Verification

- [ ] Build Debug net472/C# 7.3 trên môi trường phát triển thực tế.
- [ ] Kiểm tra startup Main_APP.
- [ ] Kiểm tra các module hiện hữu không thay đổi behavior.
- [ ] Kiểm tra parity query mới so với legacy.
- [x] Kiểm tra export path đã được giữ cho stock/inspection report.
- [x] Kiểm tra BaoCao query layer không tham chiếu `IInspectionLogRepository` hoặc `IWarehouseService`.
- [x] Kiểm tra không có write path trong delivery trace adapter/UI.
- [ ] Chỉ merge về `master` sau khi branch chạy ổn định.

## Phase 9 — User Guide / Training Documentation

Tài liệu vận hành chuẩn đã được bổ sung tại:

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

> Không xóa legacy chỉ vì tên có chữ `Report`, `History`, `Lookup` hoặc `TraCuu`.
> Phải xác định ownership, caller và behavior trước.

> Không chuyển report chứng từ nghiệp vụ sang BaoCao nếu việc chuyển làm BaoCao trở thành owner của transaction.

> BaoCao chỉ sở hữu **read use case và read model**; business module vẫn sở hữu transaction và write-side.

> Không để lại compatibility facade chỉ để che compile error. Nếu caller cũ còn tồn tại, caller đó phải được chuyển sang contract/UI mới trước khi xóa implementation cũ.
