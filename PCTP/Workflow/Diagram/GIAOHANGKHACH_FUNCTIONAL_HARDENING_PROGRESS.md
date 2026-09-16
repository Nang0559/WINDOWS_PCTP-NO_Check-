# GIAO HÀNG KHÁCH – FUNCTIONAL HARDENING PROGRESS

Branch: `feature/baocao-deliverytrace-refactor`

Mục tiêu: xử lý từng phase nghiệp vụ sau refactor kiến trúc, commit độc lập để dễ review/revert.

## Progress

| Phase | Phạm vi | Trạng thái | Commit |
|---|---|---|---|
| A | YMVN load order + delivery-hour selection | 🟡 Đã rà soát, còn kiểm tra restore `LuuPhieuGiaoHang` | — |
| **B** | **YMVN QR: Gear + quantity validation** | **🟢 Đã xử lý logic scan + completion** | `99767beda97fc0c8425e85eaeb8da3dbeca1cab3`, `785110de3b9d7e81de56a954fef2e52c3cac931a`, `65351702399003a95356d5113e57bdea48bce28e` |
| C | Restore delivered hours từ `LuuPhieuGiaoHang` | ⬜ Chưa xử lý | — |
| D | Delivery-hour locking: 100001 radio + 100002 checklist | 🟢 Đã xử lý phần lock checklist trong QR session | `bb27b3965f3dada7965978e8f2fdbad2e55e420d` |
| E | Hoàn Thành + unlock/reload state | 🟢 Đã xử lý unlock checklist khi hoàn thành/cancel | `bb27b3965f3dada7965978e8f2fdbad2e55e420d` |
| F | FIFO regression/unit tests | ⬜ Chưa xử lý | — |
| G | Build/runtime verification + final cleanup | ⬜ Chưa xử lý | — |

## Phase B – YMVN Gear + quantity

### Quy tắc đã khóa

- Gear nghiệp vụ được xác định từ **ký tự thứ 13 của LOT FCC**.
- Mapping hiện tại: `1 -> A`, `2 -> B`, `3 -> D`.
- Nếu QR có trường Gear riêng thì trường đó phải khớp Gear suy ra từ LOT.
- Order được aggregate theo `PART + GEAR`.
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

## Phase D/E – chi tiết commit hiện tại

`YmvnPresenter` hiện:

1. Khóa checklist giờ giao khi bắt đầu flow đọc QR YMVN.
2. Không xử lý thay đổi checklist trong lúc QR session đang bị khóa.
3. Mở lại checklist khi xóa toàn bộ QR/session bị hủy.
4. Mở lại checklist sau Hoàn Thành YMVN.
5. Giữ việc reload phiếu sau khi hoàn thành.

### Chưa coi Phase D/E là hoàn tất toàn bộ

Phần restore trạng thái đã giao từ `LuuPhieuGiaoHang` vẫn phải xử lý ở Phase C. Khi Phase C hoàn thành, Phase D sẽ được rà lại để bảo đảm:

```text
Delivered  -> checked + disabled
Undelivered -> unchecked + enabled
InProgress  -> controls locked
Completed   -> Delivered + controls locked
Cancelled   -> Undelivered + controls enabled
```
