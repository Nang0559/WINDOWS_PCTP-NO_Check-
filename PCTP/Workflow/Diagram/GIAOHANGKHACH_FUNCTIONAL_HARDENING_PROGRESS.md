# GIAO HÀNG KHÁCH – FUNCTIONAL HARDENING PROGRESS

Branch: `feature/baocao-deliverytrace-refactor`

Mục tiêu: xử lý các phase nghiệp vụ sau refactor kiến trúc, giữ commit độc lập và có kiểm chứng tự động.

## Progress

| Phase | Phạm vi | Trạng thái | Commit / kết quả |
|---|---|---|---|
| A | YMVN load order + delivery-hour selection | 🟢 Hoàn tất | Behavior được chốt; restore lịch sử được hoàn thiện ở C |
| B | YMVN QR: Gear + quantity validation | 🟢 Hoàn tất | `99767beda97fc0c8425e85ea8ebda3dbeca1cab3`, `785110de3b9d7e81de56a954fef2e52c3cac931a`, `65351702399003a95356d5113e57bdea48bce28e` |
| C | Restore delivered hours từ `LuuPhieuGiaoHang` | 🟢 Hoàn tất | `92f6fdfc02e2b7b913a5d229531d65f1d9721c11`, `e2a0611451387e42b6a21a458964e07119e1a47f` |
| D | Delivery-hour state: 100001 radio + 100002 checklist | 🟢 Hoàn tất | `fbbc942bd2352672ef55e621e22f6cbd58ebd233` |
| E | Hoàn Thành + unlock/reload state | 🟢 Hoàn tất | `c2b3281468776810bbd363f5d47d6dde5684584a` |
| F | FIFO regression/unit tests | 🟢 Hoàn tất | `6356bb79ed97bb20ce8737b5d3e060a4adbb8761`, `29bbb3bf85427f7bdb0c4668a227721ddc94dfb9` |
| G | Build/runtime verification + final cleanup | 🟢 Hoàn tất ở mức repository/CI | CI build + architecture checks + FIFO regression được cấu hình; runtime DB thật là environment acceptance gate |
| H | Design + User Guide | 🟢 Hoàn tất | `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`, `GIAOHANGKHACH_USER_GUIDE.md` |

## Phase B – YMVN QR validation

- Gear được suy ra từ ký tự thứ 13 của FCC LOT (`index 12`).
- Mapping: `1 -> A`, `2 -> B`, `3 -> D`.
- Nếu QR có Gear thì Gear phải khớp Gear suy ra từ FCC LOT.
- Order được aggregate theo `PART + GEAR` và dùng số lượng FCC làm quantity chuẩn.
- Scan không được vượt quantity order của đúng `PART + GEAR`.
- Gear không tồn tại trong order bị chặn.
- Completion yêu cầu toàn bộ `PART + GEAR` đạt đúng quantity.
- Candidate column names được normalize để tương thích dữ liệu YMVN hiện tại.

## Phase C – Restore delivered hours

- `PhieuService.GetGioDaGiao(...)` đọc `LUUPHIEUGIAOHANG` qua `LoadLuuPhieuCaNgay`.
- `GIOGIAO` được normalize từ các dạng như `06`, `06H`, `06:00`, `06+08`, `06,08` thành hour code.
- Delivered hours được restore vào YMVN checklist.
- Khi toàn bộ YMVN hours đã giao, current hour không bị reload thành một lựa chọn mới.

## Phase D – Delivery-hour state

### State contract

```text
Delivered   -> checked + disabled
Undelivered -> unchecked + enabled
InProgress  -> controls locked
Completed   -> Delivered + controls locked
Cancelled   -> Undelivered + controls enabled
```

### 100002 checklist

- `_deliveredYmvnHours` giữ tập giờ đã giao.
- `SetCheckedGiosYMVN(...)` áp dụng state theo từng item.
- Giờ đã giao: checked + disabled.
- Giờ chưa giao: unchecked + enabled.
- Trong QR session, toàn checklist bị lock; cancel/clear không làm mất lịch sử delivered.
- Sau completion, reload phiếu trước unlock để giờ vừa giao được restore ngay.

### 100001 radio

- Trong QR session sử dụng `LockRadioExcept(...)`.
- Chỉ giờ hiện tại còn active; các giờ khác không thể chuyển.
- Cancel/clear gọi `UnlockAllRadio()`.

## Phase E – Completion / cancellation

Completion:

```text
HoanThanhYMVN()
      ↓
LoadPhieuHienTai()
      ↓
Restore delivered history
      ↓
UnlockQrSession()
```

Cancellation/clear:

```text
Cancel / Clear QR
      ↓
Clear working QR state
      ↓
UnlockQrSession()
      ↓
Delivered stays disabled
      ↓
Undelivered becomes selectable
```

## Phase F – FIFO regression

Test project độc lập, không kéo dependency DevExpress/EF vào test:

`PCTP/Workflow/Tests/FifoSessionStateRegressionTests.csproj`

Coverage:

1. FIFO đúng thứ tự A → B → C.
2. Scan B trước A bị reject.
3. Scan bị reject không tiêu hao FIFO budget.
4. LOT ghép hợp lệ `A-3,B-2` được chấp nhận.
5. LOT ghép malformed bị reject strict.
6. Quantity tổng của LOT ghép phải bằng quantity QR.
7. Release reservation cho phép tái sử dụng FIFO quantity.

CI chạy test bằng `dotnet run` trước architecture checks.

## Phase G – Verification

Workflow:

`.github/workflows/pctp-refactor-verification.yml`

Thực hiện:

1. NuGet restore `PCTP.sln`.
2. Release build `PCTP.sln` bằng MSBuild.
3. Chạy FIFO regression project.
4. Kiểm tra dependency boundary của `GiaoHangKhach`, `BaoCao`, `Shell`.
5. Kiểm tra receiving boundary.
6. Kiểm tra DeliveryTrace contracts.

### Environment acceptance gate

Repository không có quyền truy cập trực tiếp vào DB/máy scan production của FCCVN, vì vậy không đánh dấu giả rằng runtime với dữ liệu thật đã được thực thi. Sau khi CI xanh, acceptance cuối cùng cần chạy trên máy/DB thực tế với bộ case trong User Guide.

## Phase H – Documentation

Đã cập nhật:

- `GIAOHANGKHACH_FUNCTIONAL_DESIGN.md`: design contract, state machine, QR/Gear/quantity/FIFO invariants, source of truth, completion flow và verification strategy.
- `GIAOHANGKHACH_USER_GUIDE.md`: hướng dẫn vận hành 100001/100002, QR, YMVN, FIFO, lỗi thứ tự, lỗi Gear, quantity mismatch và checklist nghiệm thu.
