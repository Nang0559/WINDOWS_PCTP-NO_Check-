# GIAO HÀNG KHÁCH – HƯỚNG DẪN SỬ DỤNG

## 1. Phạm vi

Hướng dẫn vận hành flow giao hàng khách sau refactor, tập trung vào 100001, 100002/YMVN, QR, Gear, quantity và FIFO.

---

## 2. Quy trình chung

```text
1. Chọn / load phiếu
2. Kiểm tra đơn hàng và giờ xuất
3. Chọn giờ giao
4. Bắt đầu QR nếu máy hỗ trợ
5. Scan theo đúng thứ tự
6. Xử lý cảnh báo nếu có
7. Kiểm tra đủ quantity
8. Hoàn thành giao
9. Hệ thống reload lịch sử
10. Giờ đã giao chuyển sang checked + disabled
```

---

## 3. 100001 – giờ xuất

### Khi chưa scan

Có thể chọn giờ xuất theo phiếu.

### Khi đang QR

Hệ thống khóa các radio giờ khác và giữ giờ hiện tại active. User không chuyển sang giờ khác giữa session QR.

### Khi cancel/clear QR

Các radio được mở lại.

---

## 4. 100002 / YMVN – checklist giờ giao

### Ý nghĩa trạng thái

| Trạng thái | Hiển thị / thao tác |
|---|---|
| Chưa giao | unchecked + enabled |
| Đã giao | checked + disabled |
| Đang QR | checklist locked |
| Hoàn thành | giờ vừa giao trở thành checked + disabled |
| Hủy | giờ chưa giao trở lại selectable |

### Khi mở lại phiếu

Hệ thống đọc lịch sử `LUUPHIEUGIAOHANG` và restore các giờ đã giao.

Do đó user không cần tick lại giờ đã giao.

---

## 5. Bắt đầu QR

1. Chọn giờ giao cần xử lý.
2. Bấm chức năng bắt đầu giao/scan QR.
3. Checklist và các control liên quan sẽ bị khóa trong session.
4. Scan QR theo thứ tự hệ thống yêu cầu.

Không chuyển giờ trong khi session QR đang chạy.

---

## 6. YMVN Gear

Gear của FCC LOT được suy ra từ ký tự thứ 13:

```text
1 -> A
2 -> B
3 -> D
```

Nếu QR YMVN có Gear, Gear phải khớp Gear suy ra từ FCC LOT.

### Ví dụ

```text
FCC LOT:  ............2....
             ^ ký tự 13
Gear suy ra: B
```

Nếu QR khai báo Gear khác `B`, scan bị chặn.

---

## 7. Quantity

### Quy tắc

`SlTemFCC` là quantity chuẩn để giao.

`SlTemHVN` chỉ dùng để đối chiếu.

Nếu quantity HVN khác FCC:

1. Hệ thống cảnh báo.
2. User xác nhận có tiếp tục hay không.
3. Nếu tiếp tục, mismatch được ghi nhận.
4. Không sửa quantity HVN thành quantity FCC.

FIFO và cập nhật tồn kho sử dụng quantity FCC.

---

## 8. Lỗi sai thứ tự / sai mã hàng

Khi xuất hiện:

- `Sai Thứ tự bắn!`
- `Mã Hàng HVN không khớp với FCC!`

hệ thống phải dừng scan cho đến khi user acknowledge cảnh báo.

Sau khi đóng cảnh báo:

```text
scanner enabled
input cleared
user rescan QR đúng
```

Không cố ghi nhận QR sai.

---

## 9. FIFO

### Khi FIFO được bật

`FVN_ItemFifoConfig.EnforceFifo = 1`.

Hệ thống chỉ cho xuất LOT đúng thứ tự FIFO của `STOCKTP`.

### Khi FIFO tắt

`EnforceFifo = 0` thì validation FIFO được bypass.

### Quy tắc thực tế

```text
LOT FIFO đầu tiên
   ↓
scan / reserve
   ↓
đủ quantity
   ↓
LOT tiếp theo
```

LOT có `SLCONLAI <= 0` không được xem là nguồn FIFO hợp lệ.

---

## 10. LOT ghép

Định dạng hợp lệ:

```text
LOT_A-50,LOT_B-10
```

Tổng quantity component phải bằng quantity QR.

Ví dụ:

```text
QR quantity = 60
LOT_A-50,LOT_B-10 -> hợp lệ về quantity
```

Các trường hợp bị từ chối:

```text
LOT_A-50,,LOT_B-10
LOT_A-X,LOT_B-10
LOT_A-0,LOT_B-10
LOT_A-50,LOT_B-10   nhưng QR quantity = 55
```

Không bỏ qua component malformed.

---

## 11. FIFO lỗi nhưng QR không được ghi vào kho

Khi FIFO fail:

```text
FIFO validation FAIL
        ↓
QR/LOT không được stock decrement
        ↓
không gọi update tồn kho cho LOT sai
```

Ở bước completion, hệ thống recheck FIFO từ DB trước khi mutate stock.

Nếu một row fail FIFO, row đó không được chuyển sang stock update.

---

## 12. Hoàn thành YMVN

Khi đủ toàn bộ order quantity:

```text
HoanThanhYMVN
   ↓
persist delivery
   ↓
LoadPhieuHienTai
   ↓
restore LUUPHIEUGIAOHANG
   ↓
unlock controls
```

Giờ vừa hoàn thành phải lập tức hiển thị:

```text
checked + disabled
```

---

## 13. Hủy / clear QR

Khi user hủy hoặc xóa toàn bộ QR:

- working QR state được clear theo flow hiện tại;
- delivered history không bị biến thành giờ chưa giao;
- checklist được unlock;
- giờ chưa giao có thể chọn lại.

---

## 14. Checklist nghiệm thu

### Order / YMVN

- [ ] Load đúng order theo giờ.
- [ ] Gear được suy ra đúng từ FCC LOT.
- [ ] Sai Gear bị block.
- [ ] Quantity FCC là quantity chuẩn.
- [ ] Quantity HVN khác FCC tạo cảnh báo nhưng không tự sửa HVN.
- [ ] Completion yêu cầu đủ `PART + GEAR`.

### Delivery-hour state

- [ ] Giờ đã giao mở lại vẫn checked.
- [ ] Giờ đã giao không thể tick lại.
- [ ] Giờ chưa giao vẫn selectable.
- [ ] Trong QR không thể chuyển giờ.
- [ ] Cancel unlock đúng state.
- [ ] Completion reload history trước unlock.

### FIFO

- [ ] FIFO OFF cho phép bypass.
- [ ] FIFO ON chặn LOT sai thứ tự.
- [ ] LOT hết tồn không vào FIFO.
- [ ] Scan reject không làm mất FIFO budget.
- [ ] LOT ghép hợp lệ được reserve đúng component.
- [ ] LOT ghép malformed bị reject.
- [ ] Quantity component phải khớp QR.
- [ ] DB FIFO recheck trước stock mutation.
- [ ] FIFO fail không decrement stock.

### Regression

- [ ] Build Release `PCTP.sln` xanh.
- [ ] FIFO regression suite xanh.
- [ ] Architecture boundary check xanh.
- [ ] Receiving boundary check xanh.
- [ ] DeliveryTrace contract check xanh.

---

## 15. Bộ test FIFO tự động

Chạy tại repository root:

```text
dotnet run --project PCTP/Workflow/Tests/FifoSessionStateRegressionTests.csproj --configuration Release
```

Kết quả mong đợi:

```text
FIFO regression tests passed: 17
```

Test project cố ý độc lập với DevExpress/EF để có thể chạy nhanh trong CI.

---

## 16. Lưu ý vận hành

Nếu behavior thực tế khác tài liệu, không sửa trực tiếp UI để né validation. Trước tiên xác định source of truth và boundary tương ứng:

```text
Order        -> IFS / TableOrder / GiaoDB
Delivery     -> LUUPHIEUGIAOHANG
FCC quantity -> SlTemFCC
HVN quantity -> SlTemHVN
FIFO stock   -> STOCKTP
QR working  -> DOCQRCODE / TMP
```

Mọi thay đổi nghiệp vụ mới phải cập nhật đồng thời Design và User Guide.
