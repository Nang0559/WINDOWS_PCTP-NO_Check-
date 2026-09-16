# GIAO HÀNG KHÁCH – FUNCTIONAL HARDENING PROGRESS

Branch: `feature/baocao-deliverytrace-refactor`

Mục tiêu: xử lý từng phase nghiệp vụ sau refactor kiến trúc, commit độc lập để dễ review/revert.

## Progress

| Phase | Phạm vi | Trạng thái | Commit |
|---|---|---|---|
| A | YMVN load order + delivery-hour selection | 🟡 Đã rà soát, còn kiểm tra restore `LuuPhieuGiaoHang` | — |
| **B** | **YMVN QR: Gear + quantity validation** | **🟢 Đã xử lý logic scan + completion** | `99767beda97fc0c8425e85ea8ebda3dbeca1cab3`, `785110de3b9d7e81de56a954fef2e52c3cac931a`, `65351702399003a95356d5113e57bdea48bce28e` |
| **C** | **Restore delivered hours từ `LuuPhieuGiaoHang`** | **🟡 Đã xử lý phần đọc lịch sử + restore YMVN checklist; chưa build/runtime verify** | `92f6fdfc02e2b7b913a5d229531d65f1d9721c11`, `e2a0611451387e42b6a21a458964e07119e1a47f` |
| **D** | **Delivery-hour state: 100001 radio + 100002 checklist** | **🟡 Đã xử lý QR-session lock; per-item Delivered/Undelivered state còn tiếp** | `bb27b3965f3dada7965978e8f2fdbad2e55e420d`, `c2b3281468776810bbd363f5d47d6dde5684584a` |
| **E** | **Hoàn Thành + unlock/reload state** | **🟡 Reload sau hoàn thành đã chuyển lên trước unlock; cần hoàn tất per-item state** | `c2b3281468776810bbd363f5d47d6dde5684584a` |
| F | FIFO regression/unit tests | ⬜ Chưa xử lý | — |
| G | Build/runtime verification + final cleanup | ⬜ Chưa xử lý | — |

## Phase C – Restore delivered hours

### Đã xử lý

- `PhieuService.GetGioDaGiao(...)` đọc lịch sử từ `LUUPHIEUGIAOHANG` qua `LoadLuuPhieuCaNgay`.
- Chuẩn hóa `GIOGIAO` về mã giờ `00..23`, hỗ trợ chuỗi `HH:mm`, `HHH`, nhiều giờ phân tách bằng `,` hoặc `+`.
- YMVN load giờ giao hiện:
  1. lấy toàn bộ giờ từ Order/YMVN;
  2. đọc các giờ đã giao trong `LuuPhieuGiaoHang`;
  3. restore các giờ đã giao vào checklist;
  4. chỉ dùng giờ chưa giao để tạo `GioXuatHienTai` cho flow tiếp theo.
- Khi không còn giờ chưa giao, `GioXuatHienTai` được đặt rỗng để tránh vô tình load lại giờ đã hoàn thành.

### Chưa coi Phase C hoàn tất

- Chưa build trên máy/runtime với DB thực tế.
- Chưa xác nhận chính xác `GIOGIAO` thực tế trong `LUUPHIEUGIAOHANG` của YMVN có đúng format mà parser đang hỗ trợ.
- Trạng thái UI `Delivered = checked + disabled` sẽ được hoàn thiện trong Phase D.

## Phase B – YMVN Gear + quantity

### Quy tắc đã khóa

- Gear nghiệp vụ được xác định từ **ký tự thứ 13 của LOT FCC**.
- Mapping hiện tại: `1 -> A`, `2 -> B`, `3 -> D`.
- Nếu QR có trường Gear riêng thì trường đó phải khớp Gear suy ra từ LOT.
- Order được aggregate theo **PART + GEAR**.
- Số lượng nghiệp vụ dùng **SL FCC**.
- Khi scan: `đã bắn + SL FCC hiện tại <= số lượng Order của PART + GEAR`.
- Tổng quantity đúng nhưng Gear sai **không được pass**.
- Khi Hoàn Thành: mọi nhóm `PART + GEAR` trong Order phải đạt đúng quantity; Gear phát sinh ngoài Order cũng bị chặn.
- Không sửa `SlTemHVN` để ép khớp FCC.

### Files

- `PCTP/Modules/GiaoHangKhach/Services/YmvnGearQuantityValidator.cs`
- `PCTP/Presentation/Presenters/DocQrPresenter.cs`
- `PCTP/Presentation/Presenters/YmvnPresenter.cs`

### Lưu ý còn phải xác nhận bằng dữ liệu thực tế

Validator hỗ trợ nhiều tên cột Order (`PART/MAHANG/...`, `GEAR`, `SL/QTY/...`) để không phụ thuộc alias UI. Trước khi coi Phase B + G hoàn tất, cần build và test bằng một Order YMVN thực tế có tối thiểu:

```text
PART A = 50
PART B = 100
PART D = 30
```

và QR có Gear tương ứng từ ký tự thứ 13 của LOT.

## Phase D – Delivery-hour state

### Đã xử lý

- Khi bắt đầu QR YMVN: checklist bị khóa.
- Trong QR session: thay đổi checklist không được xử lý.
- Khi xóa toàn bộ QR/session bị hủy: checklist được mở lại.
- 100001 hiện tiếp tục khóa các radio không phải giờ hiện tại bằng `LockRadioExcept(...)`.
- Sau Hoàn Thành YMVN, reload phiếu được thực hiện **trước** khi unlock checklist để lịch sử `LuuPhieuGiaoHang` có cơ hội restore trạng thái mới nhất trước khi người dùng thao tác tiếp.

### Còn phải sửa

Mục tiêu state cuối cùng:

```text
Delivered  -> checked + disabled
Undelivered -> unchecked + enabled
InProgress  -> controls locked
Completed   -> Delivered + controls locked
Cancelled   -> Undelivered + controls enabled
```

Phần còn thiếu là state **theo từng item** của `CheckedListBoxControl` 100002. Hiện code mới khóa/mở toàn control trong QR session; chưa được coi là hoàn tất cho tới khi item đã giao thực sự disabled còn item chưa giao vẫn enabled.

## Phase E – Completion / cancellation

- Completion flow đã đổi thứ tự thành: `HoanThanhYMVN` -> `LoadPhieuHienTai()` -> `UnlockQrSession()`.
- Cancellation/clear vẫn gọi `UnlockQrSession()`.
- Chưa đánh dấu xanh vì state item-level của 100002 chưa hoàn tất.
