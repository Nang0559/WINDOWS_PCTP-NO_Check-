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

- [ ] Tách query contracts theo use case, không tạo God repository.
- [ ] `IItemHistoryQuery` — QR / LOT / Part / Document.
- [ ] `IStockHistoryQuery` — lịch sử nhập/xuất kho.
- [ ] `ICurrentStockQuery` — tồn hiện tại.
- [ ] `IReceivingHistoryQuery` — lịch sử nhập.
- [ ] `IExportHistoryQuery` — lịch sử xuất.
- [ ] `IDeliveryHistoryQuery` — lịch sử giao.
- [ ] `IQualityHistoryQuery` — QC / Inspection / NG / Rework history.

## Phase 4 — Query infrastructure

- [ ] Implement SQL read-only repository/adapters.
- [ ] Không gọi WinForms hoặc business service write-side từ query layer.
- [ ] Mapping DB -> BaoCao read models.
- [ ] Parity với stored procedures/legacy queries trước khi thay UI.
- [ ] Batch query khi có thể; tránh N+1 query theo từng LOT/Part.
- [ ] Chuẩn hóa null/date/quantity/status.

## Phase 5 — UI

- [ ] `FormBaoCaoMain`.
- [ ] Tra cứu QR / LOT / Part / Document.
- [ ] Timeline lịch sử.
- [ ] Báo cáo nhập/xuất/tồn.
- [ ] Báo cáo giao hàng.
- [ ] History QC/Inspection.
- [ ] Export Excel / print.

## Phase 6 — Main_APP migration

- [ ] Main_APP chỉ giữ navigation/dashboard.
- [ ] Redirect các entry point tra cứu sang `BaoCao`.
- [ ] Xóa SQL tra cứu khỏi Main_APP theo từng use case.
- [ ] Xóa dependency tới namespace legacy sau khi caller verification.

## Phase 7 — Legacy removal

- [ ] Tách read methods khỏi `IPhieuTrackingRepository`.
- [ ] Chuyển read implementation của `PhieuTrackingRepository` sang BaoCao query infrastructure.
- [ ] Giữ write methods với owner nghiệp vụ thích hợp.
- [ ] Xóa `FormStockHistory` cũ sau parity.
- [ ] Xóa `FormInspectionHistory` cũ sau parity.
- [ ] Chỉ xóa report tổng hợp cũ khi có replacement tương đương.
- [ ] Giữ report chứng từ thuộc đúng business module.
- [ ] Xóa compile entry / using / repository không còn dùng.
- [ ] Loại bỏ `PCTP.VIEWSTOCK` khi toàn bộ dependency đã được phân rã.

## Phase 8 — Verification

- [ ] Build Debug net472/C# 7.3 trên môi trường phát triển thực tế.
- [ ] Kiểm tra startup Main_APP.
- [ ] Kiểm tra các module hiện hữu không thay đổi behavior.
- [ ] Kiểm tra parity query mới so với legacy.
- [ ] Kiểm tra export/print.
- [ ] Kiểm tra không còn write path trong BaoCao.
- [ ] Chỉ merge về `master` sau khi branch chạy ổn định.

## Quy tắc bắt buộc

> Không xóa legacy chỉ vì tên có chữ `Report`, `History`, `Lookup` hoặc `TraCuu`.
> Phải xác định ownership, caller và behavior trước.

> Không chuyển report chứng từ nghiệp vụ sang BaoCao nếu việc chuyển làm BaoCao trở thành owner của transaction.

> BaoCao chỉ sở hữu **read use case và read model**; business module vẫn sở hữu transaction và write-side.
