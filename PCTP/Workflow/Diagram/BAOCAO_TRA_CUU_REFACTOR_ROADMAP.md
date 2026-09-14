# BAOCAO / TRA CỨU — REFACTOR ROADMAP

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
- [x] Xác định parser riêng cho `LUUPHIEUGIAOHANG.LOT` dạng composite.

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
- [ ] Implement delivery trace SQL adapter sau khi schema nguồn được xác nhận.
- [ ] Hoàn tất parity với toàn bộ stored procedures/legacy queries.
- [ ] Batch query khi có thể; tránh N+1 query theo từng LOT/Part.
- [x] Chuẩn hóa null/date/quantity/status ở read-model mapping.

## Phase 5 — UI

- [x] `FormBaoCaoMain` — navigation-only entry point.
- [ ] Tra cứu QR / LOT / Part / Document.
- [ ] Tra cứu theo tên khách hàng.
- [ ] Hiển thị master theo QR/carton và detail LOT + quantity.
- [ ] Timeline: Production → QC → Nhập kho → Xuất → Giao → Customer.
- [x] Báo cáo lịch sử kho + tồn hiện tại.
- [ ] Báo cáo nhập/xuất/giao hàng chuyên biệt.
- [x] History QC/Inspection.
- [x] Export Excel / print cho stock report.
- [x] Export Excel cho inspection master.

## Phase 6 — Main_APP migration

- [ ] Main_APP chỉ giữ navigation/dashboard.
- [ ] Redirect các entry point tra cứu sang `BaoCao`.
- [ ] Xóa SQL tra cứu khỏi Main_APP theo từng use case.
- [ ] Xóa dependency tới namespace legacy sau khi caller verification.

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
- [ ] Kiểm tra không còn write path trong BaoCao.
- [ ] Chỉ merge về `master` sau khi branch chạy ổn định.

## Quy tắc bắt buộc — CLEAN AFTER MOVE

> Khi một use case đã được chuyển sang BaoCao và có replacement chạy qua query port, phải dọn sạch implementation cũ của chính use case đó trong cùng slice: UI cũ, designer/resource không còn dùng, compile entry, using/repository trung gian không còn caller.

> Không xóa legacy chỉ vì tên có chữ `Report`, `History`, `Lookup` hoặc `TraCuu`.
> Phải xác định ownership, caller và behavior trước.

> Không chuyển report chứng từ nghiệp vụ sang BaoCao nếu việc chuyển làm BaoCao trở thành owner của transaction.

> BaoCao chỉ sở hữu **read use case và read model**; business module vẫn sở hữu transaction và write-side.

> Không để lại compatibility facade chỉ để che compile error. Nếu caller cũ còn tồn tại, caller đó phải được chuyển sang contract/UI mới trước khi xóa implementation cũ.
