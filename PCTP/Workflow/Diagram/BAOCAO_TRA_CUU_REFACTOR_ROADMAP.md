# BAOCAO / TRA CỨU — REFACTOR ROADMAP

## Phase 1 — Boundary

- [x] Tạo branch riêng từ `master`.
- [x] Tạo `Modules/BaoCao`.
- [x] Tạo query contract và read model.
- [x] Chốt nguyên tắc read-only.

## Phase 2 — Legacy inventory

- [ ] Liệt kê toàn bộ report legacy trong `PCTP`.
- [ ] Liệt kê toàn bộ chức năng tra cứu trong `Shell/Main_APP` và các namespace legacy.
- [ ] Xác định caller cho từng chức năng.
- [ ] Phân loại `KEEP / MOVE / REDIRECT / DELETE`.

## Phase 3 — Query infrastructure

- [ ] Implement query repository read-only.
- [ ] Implement search QR / LOT / Part / Document.
- [ ] Implement stock history.
- [ ] Implement receiving history.
- [ ] Implement export history.
- [ ] Implement delivery history.
- [ ] Implement NG / Rework / QC history.
- [ ] Hợp nhất thành item timeline.

## Phase 4 — UI

- [ ] `FormBaoCaoMain`.
- [ ] Tra cứu item.
- [ ] Timeline lịch sử.
- [ ] Báo cáo nhập/xuất/tồn.
- [ ] Báo cáo giao hàng.
- [ ] Export Excel/print.

## Phase 5 — Main_APP migration

- [ ] Main_APP chỉ giữ navigation/dashboard.
- [ ] Redirect các entry point tra cứu sang `BaoCao`.
- [ ] Xóa SQL tra cứu khỏi Main_APP.
- [ ] Xóa dependency tới namespace legacy sau khi verify.

## Phase 6 — Legacy removal

- [ ] Xóa implementation tra cứu cũ sau khi parity test.
- [ ] Xóa report tổng hợp cũ đã có replacement.
- [ ] Giữ lại report chứng từ thuộc đúng business module.
- [ ] Xóa compile entry / using / repository không còn dùng.

## Phase 7 — Verification

- [ ] Build Debug net472/C# 7.3.
- [ ] Kiểm tra startup Main_APP.
- [ ] Kiểm tra các module hiện hữu không thay đổi behavior.
- [ ] Kiểm tra kết quả query mới so với legacy.
- [ ] Chỉ merge về `master` sau khi branch chạy ổn định.

## Quy tắc bắt buộc

> Không xóa legacy chỉ vì tên có chữ `Report` hoặc `TraCuu`.
> Phải xác định ownership và caller trước.

> Không chuyển report chứng từ nghiệp vụ sang BaoCao nếu việc chuyển làm BaoCao trở thành owner của transaction.
