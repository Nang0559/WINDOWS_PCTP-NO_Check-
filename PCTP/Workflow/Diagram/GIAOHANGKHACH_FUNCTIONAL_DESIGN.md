# GIAO HÀNG KHÁCH – FUNCTIONAL DESIGN

Branch: `feature/baocao-deliverytrace-refactor`

## 1. Mục đích

Tài liệu này chốt contract nghiệp vụ sau refactor `GiaoHangKhach`, đặc biệt cho:

- load đơn hàng IFS / TableOrder / GiaoDB;
- phân loại MP/SP;
- QR/TMP working state;
- YMVN delivery-hour checklist;
- Gear và quantity validation;
- FIFO tại scan và trước cập nhật kho;
- completion/cancellation;
- traceability và verification.

Mục tiêu là **không thay đổi nghiệp vụ đã chốt**, nhưng chuyển responsibility về đúng service/repository/control owner.

---

## 2. Kiến trúc runtime

```text
HVN_PGH
   |
   v
HVN_Presenter
   |
   +--> PhieuService / orchestration facade
   |
   +--> PhieuLoadService
   |      +--> IOrderSourceFactory
   |      |      +--> IFS
   |      |      +--> TableOrder / MilkRun
   |      |      +--> GiaoDB
   |      |
   |      +--> Standard Order
   |      +--> MP/SP classification
   |      +--> HOP enrichment
   |      +--> QR/TMP working state
   |
   +--> DocQRScanEngine / DocQRService
   |      +--> Gear validation
   |      +--> Quantity validation
   |      +--> FIFO RAM validation
   |
   +--> GiaoHangKhach services
          +--> stock / delivery persistence
          +--> FIFO DB recheck
          +--> completion / history
```

UI không được tự tạo repository cho business responsibility đã có service owner.

---

## 3. Source và Category

### 3.1 Source

```text
IFS        = baseline/customer order source
TableOrder = actual order / MilkRun source
GiaoDB     = special delivery document source
```

Source chỉ load và normalize source data; không cập nhật kho và không điều khiển UI.

### 3.2 Category

```text
MP
SP
```

Category là dimension độc lập với Source.

IFS 100001 xác định category theo giờ xuất; TableOrder/MilkRun có thể lọc row theo cấu hình dock; toggle UI là session input.

---

## 4. Delivery-hour state

### 4.1 Contract

```text
Delivered   -> checked + disabled
Undelivered -> unchecked + enabled
InProgress  -> controls locked
Completed   -> Delivered + controls locked
Cancelled   -> Undelivered + controls enabled
```

### 4.2 100002 YMVN checklist

`PhieuHeaderControl` duy trì `_deliveredYmvnHours`.

Khi load history:

1. đọc `LUUPHIEUGIAOHANG`;
2. normalize `GIOGIAO` về hour code;
3. mark delivered item;
4. disable item delivered;
5. enable item chưa giao.

Khi bắt đầu QR:

- lock toàn checklist;
- không cho đổi giờ trong lúc scan.

Khi cancel/clear:

- unlock checklist;
- delivered vẫn disabled;
- undelivered trở lại enabled.

Khi completion:

- reload phiếu/history trước;
- sau đó mới unlock.

### 4.3 100001 radio

Trong QR session chỉ radio của giờ hiện tại còn active; radio khác bị khóa bằng `LockRadioExcept(...)`. Cancel/clear restore bằng `UnlockAllRadio()`.

---

## 5. YMVN Gear validation

Gear được suy ra từ FCC LOT:

```text
LOT character 13 (index 12)

1 -> A
2 -> B
3 -> D
```

Nếu QR cung cấp Gear thì Gear QR phải khớp Gear suy ra.

Order quantity được aggregate theo:

```text
PART + GEAR
```

và quantity chuẩn là **FCC quantity**.

Không dùng quantity HVN/customer để thay đổi quantity FCC.

---

## 6. YMVN quantity contract

### FCC

- `SlTemFCC` là source of truth cho quantity giao.
- scan FCC không được vượt quantity order của đúng `PART + GEAR`.

### HVN/customer

- `SlTemHVN` là quantity đối chiếu.
- nếu khác FCC: cảnh báo mismatch.
- user có thể xác nhận pass; hệ thống ghi nhận mismatch status nhưng **không sửa `SlTemHVN`**.

Downstream inventory/FIFO/delivery sử dụng quantity FCC.

---

## 7. QR order contract

Các lỗi nghiệp vụ phải block scan và yêu cầu acknowledgement trước khi tiếp tục:

- sai thứ tự FCC/HVN;
- mã hàng HVN không khớp FCC;
- Gear không hợp lệ;
- quantity vượt order;
- PART/Gear không tồn tại trong order.

Sau khi đóng cảnh báo, scanner được enable và input được clear để user rescan.

---

## 8. FIFO contract

### 8.1 Config

```text
FVN_ItemFifoConfig.EnforceFifo = 0 -> bypass
FVN_ItemFifoConfig.EnforceFifo = 1 -> enforce
```

### 8.2 Source of truth

FIFO đọc stock từ `STOCKTP`.

- chỉ row `SLCONLAI > 0` được đưa vào FIFO;
- cùng `PART + LEFT(LOT,13)` được coi là cùng LOT FIFO key;
- `SlotLot` không quyết định FIFO.

### 8.3 Canonical order

```sql
ORDER BY LEFT(LotKey,6),
         CASE SUBSTRING(LotKey,12,1)
           WHEN '0' THEN 0
           WHEN '1' THEN 1
           WHEN '2' THEN 2
           WHEN '3' THEN 3
           ELSE 9
         END,
         LotKey
```

### 8.4 Two-level validation

```text
QR scan
  -> RAM FIFO
  -> reserve quantity

Completion / stock mutation
  -> DB FIFO recheck
  -> only valid LOT rows continue
  -> invalid LOT is cleared/rejected
  -> valid rows proceed to stock update
```

FIFO-failed QR không được gửi vào `Usp_Qrcode_Update_Stock2405`.

### 8.5 Compound LOT

Format:

```text
LOT_A-50,LOT_B-10
```

Mỗi component phải parse độc lập.

Malformed component, empty component, non-numeric quantity hoặc quantity <= 0 đều reject strict. Tổng quantity component phải bằng quantity QR.

---

## 9. FIFO session state

`FifoSessionState` giữ budget FIFO trong RAM theo PART.

`TryConsume(...)` chỉ commit reservation sau khi toàn bộ LOT component pass. Vì vậy scan bị reject không làm mất FIFO budget.

`Release(...)` hoàn trả reservation khi row/QR bị hủy hoặc thay đổi.

Session FIFO phải reset sau khi hoàn tất workflow để không rò state sang phiếu tiếp theo.

---

## 10. Completion state machine

```text
Loaded
  |
  +--> Delivery-hour selected
  |
  +--> QR Started
  |      |
  |      +--> Scan / validate
  |      +--> FIFO reserve
  |      |
  |      +--> Cancel -> unlock -> Undelivered selectable
  |
  +--> HoanThanhYMVN
         |
         +--> persist delivery
         +--> reload phiếu/history
         +--> restore delivered hours
         +--> unlock controls
         +--> clear session state
```

Completed hour phải trở thành:

```text
checked + disabled
```

và không thể được chọn lại như một giờ mới.

---

## 11. Data ownership

| Dữ liệu | Owner / source of truth |
|---|---|
| IFS order | IFS source |
| Actual MilkRun/TableOrder | TableOrder source |
| GiaoDB document | GiaoDB source |
| QR working state | DOCQRCODE/TMP |
| FCC delivery quantity | `SlTemFCC` |
| HVN/customer quantity | `SlTemHVN`, chỉ đối chiếu |
| Delivered hour history | `LUUPHIEUGIAOHANG` |
| FIFO stock | `STOCKTP` |
| FIFO session reservation | `FifoSessionState` |
| Final stock mutation | stock movement boundary |

---

## 12. Error handling

Các lỗi validation phải:

1. giải thích PART/LOT/Gear/quantity liên quan;
2. chặn thao tác sai;
3. yêu cầu user acknowledge;
4. clear scanner input;
5. cho phép rescan.

Không nuốt lỗi bằng `catch { }` ở boundary nghiệp vụ.

---

## 13. Verification

CI kiểm tra:

- Release build `PCTP.sln`;
- architecture boundary;
- unified receiving boundary;
- DeliveryTrace contracts;
- FIFO regression suite.

Regression suite bao phủ FIFO đúng thứ tự, wrong-order rejection, compound LOT, malformed LOT, quantity mismatch, no-consumption-on-reject và reservation release.

Runtime acceptance với DB/máy scan thật phải được thực hiện trong môi trường FCCVN sau khi CI xanh.
