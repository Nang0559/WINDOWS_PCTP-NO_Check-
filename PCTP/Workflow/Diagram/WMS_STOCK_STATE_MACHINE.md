# WMS_STOCK_STATE_MACHINE

## 1. Mục tiêu

Phân biệt rõ **trạng thái vật lý của hàng** và **trạng thái nghiệp vụ của chứng từ**.

Không dùng `HangChoGiao`, `Rework`, `Abnormal` để suy ra tồn kho nếu chưa có stock movement.

## 2. Stock lifecycle

```text
RECEIVED
   ↓
STORED
   ├───────────────┐
   │               │
   ↓               ↓
RESERVED       WAIT_REWORK
   ↓               ↓
PICKED          REWORK
   ↓             /    \
WAIT_DELIVERY  OK      NG
   ↓            ↓        ↓
EXPORTED     STORED   ABNORMAL
```

## 3. Ý nghĩa

| State | Ý nghĩa | Tồn kho |
|---|---|---|
| RECEIVED | Đã tiếp nhận nhưng chưa hoàn tất lưu kho | Theo transaction nhập |
| STORED | Đang nằm trong Slot/LOT | Có |
| RESERVED | Được giữ cho yêu cầu xuất | Có |
| PICKED | Đã lấy khỏi vị trí vật lý | Theo mô hình Slot/stock projection |
| WAIT_DELIVERY | Đang chờ giao | Theo ownership thực tế của hệ thống |
| EXPORTED | Đã xuất khỏi kho | Không |
| WAIT_REWORK | Đang chờ xử lý lỗi | Theo source stock |
| REWORK | Đang xử lý | Theo physical state |
| ABNORMAL | Đang cách ly/xử lý bất thường | Theo stock command thực tế |

## 4. Quy tắc chuyển trạng thái

### Nhập

```text
RECEIVED -> STORED
```

Phải có Receive movement.

### Xuất

```text
STORED -> RESERVED -> PICKED -> WAIT_DELIVERY -> EXPORTED
```

Không được trừ `STOCKTP` hai lần giữa Pick và Export.

### Rework

```text
STORED -> WAIT_REWORK -> REWORK
REWORK -> STORED       (OK và trả kho)
REWORK -> ABNORMAL     (NG/cách ly)
```

Mỗi thay đổi vật lý phải có movement tương ứng.

## 5. State transition invariants

1. Không nhảy `STORED -> EXPORTED` nếu nghiệp vụ yêu cầu Pick/Delivery.
2. Không `EXPORTED -> STORED` nếu không có Return movement.
3. Không chuyển Rework -> STORED mà không xác định Item/Lot/Slot/Quantity.
4. Không tạo movement từ state terminal nếu không có nghiệp vụ Return/Correction rõ ràng.
5. State machine của chứng từ và stock state phải được audit độc lập.
