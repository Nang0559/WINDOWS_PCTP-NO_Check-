# GIAO HÀNG KHÁCH – FUNCTIONAL HARDENING PROGRESS

Branch: `feature/baocao-deliverytrace-refactor`

Mục tiêu: xử lý từng phase nghiệp vụ sau refactor kiến trúc, commit độc lập để dễ review/revert.

## Progress

| Phase | Phạm vi | Trạng thái | Commit |
|---|---|---|---|
| A | YMVN load order + delivery-hour selection | 🟡 Đã rà soát, còn kiểm tra restore `LuuPhieuGiaoHang` | — |
| **B** | **YMVN QR: Gear + quantity validation** | **🟢 Đã xử lý logic scan + completion** | `99767beda97fc0c8425e85ea8ebda3dbeca1cab3`, `785110de3b9d7e81de56a954fef2e52c3cac931a`, `65351702399003a95356d5113e57bdea48bce28e` |
| **C** | **Restore delivered hours từ `LuuPhieuGiaoHang`** | **🟡 Đã xử lý phần đọc lịch sử + restore YMVN checklist; chưa build/runtime verify** | `92f6fdfc02e2b7b913a5d229531d65f1d9721c11`, `e2a0611451387e42b6a21a458964e07119e1a47f` |
| **D** | **Delivery-hour state: 100001 radio + 100002 checklist** | **🟢 Đã hoàn tất logic state item-level + QR-session lock/unlock** | `fbbc942bd2352672ef55e621e22f6cbd58ebd233` |
| **E** | **Hoàn Thành + unlock/reload state** | **🟢 Đã xử lý completion reload trước unlock** | `c2b3281468776810bbd363f5d47d6dde5684584a` |
| F | FIFO regression/unit tests | ⬜ Chưa xử lý | — |
| G | Build/runtime verification + final cleanup | ⬜ Chưa xử lý | — |

## Phase D – Delivery-hour state

### Đã hoàn tất logic

- YMVN `CheckedListBoxControl` lưu tập giờ đã giao trong `_deliveredYmvnHours`.
- `SetCheckedGiosYMVN(...)` áp dụng state theo từng item:
  - Delivered → `checked + disabled`.
  - Undelivered → `unchecked + enabled`.
- `LockCheckListYMVN()` khóa toàn bộ checklist trong QR session nhưng không làm mất checked state.
- `UnlockCheckListYMVN()` khôi phục item-level state: giờ đã giao tiếp tục disabled, giờ chưa giao được enable.
- Cancel/clear QR không làm giờ đã giao trở thành selectable.
- Completion reload phiếu trước khi unlock, vì vậy lịch sử `LuuPhieuGiaoHang` có thể đưa giờ vừa giao vào trạng thái `checked + disabled`.
- 100001 tiếp tục dùng `LockRadioExcept(...)`: chỉ giờ hiện tại còn active, các giờ khác không thể chuyển trong QR session.

### State contract

```text
Delivered   -> checked + disabled
Undelivered -> unchecked + enabled
InProgress  -> controls locked
Completed   -> Delivered + controls locked
Cancelled   -> Undelivered + controls enabled
```

### Ghi chú kỹ thuật

`CheckedListBoxControl` không có API `SetItemEnabled(...)`; implementation dùng trực tiếp `checkList.Items[index].Enabled`, phù hợp với API `CheckedListBoxItem.Enabled` của DevExpress.

Phase D được coi là hoàn tất ở mức **code logic**. Build/runtime với DB thật vẫn thuộc Phase G.

## Phase E – Completion / cancellation

- Completion flow: `HoanThanhYMVN` -> `LoadPhieuHienTai()` -> `UnlockQrSession()`.
- Cancellation/clear gọi `UnlockQrSession()`.
- Sau reload, delivered-hour history được restore trước khi checklist trở lại interactive.
