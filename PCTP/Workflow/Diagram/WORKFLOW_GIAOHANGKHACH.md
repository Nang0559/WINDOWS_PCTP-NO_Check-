# WORKFLOW GIAO HÀNG KHÁCH — QR / FIFO / CNK

## 1. Mục tiêu

Module **Giao Hàng Khách** phải bảo đảm QR giao hàng chỉ được xuất kho khi thỏa mãn đồng thời:

- đúng `PART/MAHANG` của phiếu;
- đúng số lượng còn phải giao;
- đúng thứ tự FIFO nếu mã hàng được cấu hình `EnforceFifo = 1`;
- LOT ghép phải hợp lệ và tổng số lượng LOT thành phần phải bằng số lượng QR;
- FIFO RAM và FIFO DB đều phải pass trước khi cập nhật tồn kho.

FIFO RAM chỉ là lớp kiểm tra sớm để chặn QR sai ngay khi quét. **DB/`STOCKTP` là nguồn xác thực cuối cùng** trước `Usp_Qrcode_Update_Stock2405`.

---

## 2. Luồng chuẩn

```text
GridView phiếu giao
   │
   ├── PART / MAHANG
   └── SL cần giao
          │
          ▼
   FifoSessionService.Initialize()
          │
          ├── EnforceFifo = 0 → không áp FIFO
          │
          └── EnforceFifo = 1
                  │
                  ▼
             STOCKTP / FIFO source
                  │
                  ├── SLCONLAI > 0
                  ├── PART
                  └── LOT key = LEFT(LOT,13)
                  │
                  ▼
              FifoSessionState
              ┌─────────────────┐
              │ PART             │
              │ NeedQty          │
              │ FIFO Rank        │
              │ LOT              │
              │ RemainingAllowed │
              └────────┬────────┘
                       │
                       ▼
                    Quét QR
                       │
                ┌──────┴──────┐
                │             │
              PASS           FAIL
                │             │
                │       Warning FIFO
                │       Xóa QR vừa chèn
                │       Không giữ trong TMP
                │
                ▼
               TMP
                │
                ▼
          Hoàn thành / CNK
                │
                ▼
        DB FIFO final validation
                │
          ┌─────┴─────┐
          │           │
        PASS         FAIL
          │           │
          │      LayLaiLotNo()
          │      LOT = ''
          │      giữ QR lỗi ngoài phạm vi
          │      update stock
          │
          ▼           │
 Usp_Qrcode_Update_Stock2405
          │
          ▼
     Chỉ QR hợp lệ
```

---

## 3. RAM FIFO

### 3.1. Nguồn dữ liệu

Khi người dùng bấm **Đọc QR**, `PhieuService.SyncIfsPhieuChoDocQR()` hoàn tất đồng bộ phiếu. `DocQRService.InitializeFifo(orderRows)` tạo RAM FIFO từ các dòng GridView.

`FifoSessionService`:

1. nhóm các dòng theo `MAHANG`;
2. cộng `SL cần giao`;
3. chỉ áp dụng cho mã có `EnforceFifo = 1`;
4. đọc FIFO từ kho;
5. bỏ dòng có `SLCONLAI <= 0`;
6. chuẩn hóa key LOT bằng `LEFT(LOT,13)`;
7. lấy LOT theo `FIFO_RANK`;
8. chỉ nạp lượng FIFO cần thiết cho số lượng còn phải giao.

### 3.2. State RAM

State nằm tại `FifoSessionState` và gồm:

```text
PART
NeedQty
FIFO_RANK
LOT KEY
Display LOT
OriginalAvailableQty
RemainingAllowedQty
```

`GetRamSnapshot()` cung cấp snapshot để debug/test mà không làm thay đổi state.

---

## 4. Quét QR

`DocQRService.ProcessScan()` gọi `DocQRScanEngine` trước. Chỉ khi parser/validation QR gốc pass mới chạy `ApplyRamFifo()`.

### 4.1. LOT đơn

Có thể nhận:

```text
LOT1234567890
```

Trong trường hợp này số lượng lấy từ dữ liệu QR (`SLTEMFCC/SLTEMHVN`).

### 4.2. LOT ghép

Dạng chuẩn:

```text
LOT_A-50,LOT_B-10
```

Điều kiện:

```text
50 + 10 = SL của QR
```

Tất cả LOT thành phần phải cùng pass FIFO. Không được consume một phần rồi mới phát hiện thành phần sau sai.

### 4.3. PASS

`FifoSessionState.TryConsume()` trừ `RemainingAllowedQty` và tạo reservation theo `STT` QR.

### 4.4. FAIL

Nếu FIFO fail:

1. QR vừa insert vào bảng DocQR bị xóa ngay;
2. reservation không được giữ;
3. phát event `FIFO_REJECTED` để UI refresh;
4. trả `ScanResult.FifoFail()` để UI hiển thị cảnh báo;
5. QR lỗi không được đi vào TMP hợp lệ để CNK.

---

## 5. Atomic reservation

Mỗi QR có reservation theo `STT`.

Khi scan lại cùng `STT`, reservation cũ được release trước khi thử payload mới. Điều này tránh tình trạng:

```text
QR cũ consume FIFO
   ↓
scan lại QR
   ↓
consume lần 2
```

làm giảm RAM hai lần.

LOT ghép cũng được kiểm tra trên bản `trial` trước khi commit. Nếu một thành phần sai, toàn bộ QR bị reject và không consume thành phần nào.

---

## 6. Final FIFO tại CNK

RAM không được xem là nguồn sự thật cuối cùng.

`PhieuKhoService.CapNhapKho()` gọi repository final gate trước mỗi lần update:

```text
ReleaseFifoViolations(tmpTable, docQrTable)
        │
        ├── QR FIFO sai → LayLaiLotNo / LOT = ''
        │
        └── QR FIFO đúng → giữ nguyên
                 │
                 ▼
      Usp_Qrcode_Update_Stock2405
```

Nếu stored procedure vẫn trả lỗi FIFO, service thực hiện vòng kiểm tra/giải phóng lại tối đa một lần trước khi kết thúc.

Các cảnh báo FIFO cuối cùng được merge vào `DataTable errors` và phát qua `KhoUpdatedEvent` để UI hiển thị.

**Nguyên tắc bắt buộc:** QR có LOT đã bị xóa/blank do FIFO fail không được phép được xử lý như QR hợp lệ trong update tồn kho.

---

## 7. Phân biệt Hoàn thành và CNK

Trong UI hiện tại, **Hoàn thành** là bước kết thúc phiên bắn QR/điều hướng nghiệp vụ; **CNK** là bước cập nhật kho cuối cùng.

Final DB FIFO gate bắt buộc phải chạy tại bước CNK. Nếu nghiệp vụ sau này yêu cầu Hoàn thành cũng phải chặn ngay khi còn QR chưa hợp lệ, cần gọi cùng một validation service trước khi đóng phiên; không được copy logic FIFO vào Form.

---

## 8. Debug RAM FIFO

Đặt breakpoint theo thứ tự:

1. `DocQRService.InitializeFifo()`.
2. `FifoSessionService.Initialize()`.
3. `FifoSessionState.Initialize()`.
4. `DocQRService.ApplyRamFifo()`.
5. `FifoSessionState.TryConsume()`.
6. `PhieuKhoService.CapNhapKho()`.
7. repository `ReleaseFifoViolations()`.

Trong Immediate/Watch có thể gọi:

```csharp
_fifoState.GetRamSnapshot()
```

hoặc từ `FifoSessionService`:

```csharp
fifoService.GetRamSnapshot(fifoState)
```

Kiểm tra tối thiểu:

```text
ItemCode
NeedQty
LotKey
DisplayLot
OriginalAvailableQty
RemainingAllowedQty
FifoRank
```

---

## 9. Quy tắc không được phá vỡ

- Không bypass FIFO vì `_parts` không có mã hàng khi mã đã được cấu hình FIFO.
- Không consume FIFO trước khi toàn bộ LOT ghép được validate.
- Không update stock bằng QR đã bị FIFO reject.
- Không dùng FIFO RAM thay cho DB final validation.
- Không để Form tự tính FIFO; business rule nằm trong service/state/repository.
- `STOCKTP`/DB vẫn là nguồn xác thực cuối cùng vì tồn kho có thể thay đổi giữa lúc đọc QR và CNK.

---

## 10. Checklist nghiệm thu

| Test | Kết quả yêu cầu |
|---|---|
| LOT FIFO đầu tiên | PASS |
| LOT FIFO tiếp theo | PASS |
| LOT không phải FIFO | Warning + QR bị loại |
| LOT đơn sai | Không vào TMP hợp lệ |
| LOT ghép đúng | PASS, consume atomic |
| LOT ghép sai 1 thành phần | Reject toàn bộ QR |
| Tổng LOT ghép khác SL QR | Reject |
| Scan lại cùng STT | Không double-consume |
| LOT FIFO bị thay đổi ở DB trước CNK | Final DB gate reject |
| QR FIFO lỗi | LOT bị blank/loại khỏi stock update |
| QR hợp lệ cùng batch | Vẫn được update |
