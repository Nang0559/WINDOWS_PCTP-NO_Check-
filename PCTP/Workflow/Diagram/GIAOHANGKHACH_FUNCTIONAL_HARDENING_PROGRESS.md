# GIAO HÀNG KHÁCH – FUNCTIONAL HARDENING PROGRESS

Branch: `feature/baocao-deliverytrace-refactor`

Mục tiêu: xử lý từng phase nghiệp vụ sau refactor kiến trúc, commit độc lập để dễ review/revert.

## Progress

| Phase | Phạm vi | Trạng thái | Commit |
|---|---|---|---|
| A | YMVN load order + delivery-hour selection | 🟡 Đã rà soát, còn kiểm tra restore `LuuPhieuGiaoHang` | — |
| B | YMVN QR: Gear + quantity validation | ⬜ Chưa xử lý | — |
| C | Restore delivered hours từ `LuuPhieuGiaoHang` | ⬜ Chưa xử lý | — |
| D | Delivery-hour locking: 100001 radio + 100002 checklist | 🟢 Đã xử lý phần lock checklist trong QR session | `bb27b3965f3dada7965978e8f2fdbad2e55e420d` |
| E | Hoàn Thành + unlock/reload state | 🟢 Đã xử lý unlock checklist khi hoàn thành/cancel | `bb27b3965f3dada7965978e8f2fdbad2e55e420d` |
| F | FIFO regression/unit tests | ⬜ Chưa xử lý | — |
| G | Build/runtime verification + final cleanup | ⬜ Chưa xử lý | — |

## Quy tắc không thay đổi

- FCC quantity là nguồn số lượng nghiệp vụ duy nhất.
- YMVN Gear phải được kiểm tra độc lập với tổng quantity.
- FIFO vẫn theo `STOCKTP`; không đổi source of truth.
- Không sửa quantity khách hàng để ép khớp FCC.
- Không đánh dấu delivery-hour là Delivered nếu chưa hoàn thành giao thực tế.

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
