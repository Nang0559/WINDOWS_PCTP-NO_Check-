# Hướng dẫn sử dụng — Xử lý Hàng lỗi / QT Chung và liên hệ FIFO Giao Hàng Khách

## 1. Mục đích

Module dùng để xử lý một phiếu hàng lỗi/bất thường từ lúc tiếp nhận đến khi hoàn tất xử lý, gồm truy vết LOT, QC, Rework, Disposition và/hoặc Giao bù.

Khi hàng lỗi phát sinh từ hoặc ảnh hưởng đến **Giao Hàng Khách**, người dùng phải phân biệt rõ:

- QR giao hàng đang hợp lệ hay đã bị FIFO reject;
- LOT nào đang nằm trong phạm vi FIFO của đơn hàng;
- hàng lỗi nào cần truy vết LOT thực tế trong kho/sản xuất/khách trả;
- không được dùng kết quả FIFO RAM của phiên giao hàng làm bằng chứng tồn kho cuối cùng.

---

## 2. Quy trình xử lý hàng lỗi

```text
Tạo phiếu
  ↓
Truy vết LOT
  ↓
QC định hướng
  ↓
Initial QC
  ├── Từ chối giao bù
  ├── Giao bù
  └── Rework
          ↓
      Xác nhận kết quả
          ↓
       Đóng xử lý
```

Nếu hàng lỗi liên quan đến một LOT đang/đã giao cho khách, phải giữ nguyên LOT thực tế để truy vết. Không tự thay LOT chỉ vì FIFO của một đơn hàng khác.

---

## 3. FIFO trong Giao Hàng Khách

FIFO được áp dụng khi mã hàng có `EnforceFifo = 1`.

Khi bấm **Đọc QR**:

```text
GridView
  ├── PART
  └── SL cần giao
       ↓
RAM FIFO
       ↓
Quét QR
```

RAM FIFO được tạo từ FIFO stock với các điều kiện chính:

- `SLCONLAI > 0`;
- FIFO được sắp theo `FIFO_RANK`;
- key LOT dùng `LEFT(LOT,13)`;
- chỉ nạp lượng LOT cần thiết cho số lượng còn phải giao.

---

## 4. Khi quét QR

### 4.1. QR hợp lệ

```text
QR
 ↓
PART đúng
 ↓
SL đúng
 ↓
FIFO PASS
 ↓
TMP
```

QR hợp lệ được giữ trong phiên để CNK xử lý.

### 4.2. QR sai FIFO

```text
QR
 ↓
FIFO FAIL
 ↓
Cảnh báo FIFO
 ↓
Xóa QR vừa quét
 ↓
Không giữ QR lỗi trong TMP hợp lệ
```

Không tiếp tục dùng QR bị reject để thực hiện CNK.

---

## 5. LOT ghép

LOT ghép phải có dạng:

```text
LOT_A-50,LOT_B-10
```

Điều kiện bắt buộc:

```text
50 + 10 = SL của QR
```

Từng LOT thành phần đều phải pass FIFO.

Nếu chỉ một thành phần sai:

```text
LOT_A-50,LOT_X-10
            ↑
         sai FIFO
```

thì **toàn bộ QR bị reject**, không consume một phần LOT_A.

---

## 6. CNK và final FIFO

FIFO RAM chỉ là kiểm tra sớm. Trước khi cập nhật tồn kho, DB phải kiểm tra lại FIFO thực tế.

```text
TMP
 ↓
DB FIFO Check
 ↓
PASS ───────────────→ Usp_Qrcode_Update_Stock2405

FAIL
 ↓
LayLaiLotNo()
 ↓
LOT = ''
 ↓
QR lỗi không được update stock
```

Điều này cần thiết vì tồn kho có thể thay đổi sau khi người dùng đã đọc QR.

---

## 7. Xử lý hàng lỗi sau khi QR/LOT bị reject

Nếu QR bị FIFO reject, người dùng phải xác định nguyên nhân thực tế trước khi xử lý hàng lỗi:

1. kiểm tra `PART`;
2. kiểm tra LOT trên QR/tem;
3. kiểm tra số lượng;
4. kiểm tra LOT FIFO hiện tại;
5. nếu là hàng NG, truy vết LOT thực tế trong các nguồn liên quan;
6. ghi nhận kết quả QC/Disposition theo quy trình hàng lỗi.

Không sửa dữ liệu FIFO RAM thủ công để biến một QR sai thành QR hợp lệ.

---

## 8. Truy vết LOT hàng NG

Khi khách hàng hoặc nội bộ phát hiện hàng NG, trình tự nghiệp vụ phải là:

```text
Phát hiện NG
   ↓
Xác định PART + LOT
   ↓
Tra cứu toàn bộ phạm vi LOT
   ├── Kho
   ├── Sản xuất
   ├── Đã giao
   └── Khách trả (nếu có)
   ↓
Xác định phạm vi ảnh hưởng
   ↓
QC / Disposition
   ↓
Rework / Giao bù / Từ chối
```

FIFO của Giao Hàng Khách chỉ trả lời câu hỏi **LOT nào được phép xuất trước theo tồn kho tại thời điểm giao**. Nó không thay thế nghiệp vụ truy vết hàng lỗi.

---

## 9. Cảnh báo người dùng

### Không được làm

- Không bỏ qua cảnh báo FIFO để tiếp tục giao.
- Không nhập LOT khác vào QR chỉ để vượt FIFO.
- Không chỉnh `LOT` trong TMP để làm mất cảnh báo.
- Không cập nhật stock thủ công cho QR đã bị FIFO reject.
- Không coi RAM FIFO là tồn kho cuối cùng.

### Được phép

- Quét lại QR hợp lệ sau khi QR sai đã bị loại.
- Sửa số lượng theo chức năng nghiệp vụ; hệ thống phải release reservation cũ trước khi consume lại.
- Kiểm tra RAM FIFO bằng `GetRamSnapshot()` khi cần hỗ trợ kỹ thuật.

---

## 10. Hướng dẫn kỹ thuật khi cần debug

Đặt breakpoint theo thứ tự:

```text
DocQRService.InitializeFifo()
        ↓
FifoSessionService.Initialize()
        ↓
FifoSessionState.Initialize()
        ↓
DocQRService.ApplyRamFifo()
        ↓
FifoSessionState.TryConsume()
        ↓
PhieuKhoService.CapNhapKho()
        ↓
ReleaseFifoViolations()
```

Snapshot RAM gồm:

```text
ItemCode
NeedQty
LotKey
DisplayLot
OriginalAvailableQty
RemainingAllowedQty
FifoRank
```

Nếu RAM có LOT đúng nhưng scan vẫn PASS với LOT sai, phải kiểm tra `ApplyRamFifo()` và `TryConsume()`.

Nếu scan đã FAIL nhưng CNK vẫn update QR đó, phải kiểm tra `ReleaseFifoViolations()` và `Usp_Qrcode_Update_Stock2405`.

---

## 11. Checklist người vận hành

| Kiểm tra | Yêu cầu |
|---|---|
| PART | Có trong phiếu |
| Số lượng | Không vượt SL cần giao |
| LOT | Đúng QR/tem |
| FIFO | PASS nếu mã được cấu hình FIFO |
| LOT ghép | Tổng thành phần = SL QR |
| QR FIFO fail | Không tiếp tục dùng QR đó |
| CNK | Chờ final DB FIFO |
| Hàng NG | Truy vết LOT thực tế, không sửa FIFO để né cảnh báo |
