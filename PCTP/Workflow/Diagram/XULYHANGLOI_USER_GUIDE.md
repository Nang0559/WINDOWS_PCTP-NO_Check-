# Hướng dẫn sử dụng — XuLyHangLoi / QT Chung

## 1. Mục đích

Dùng module để xử lý một phiếu hàng lỗi/bất thường từ lúc tiếp nhận đến khi hoàn tất xử lý, gồm truy vết LOT, QC, Rework, Disposition và/hoặc Giao bù.

## 2. Quy trình chuẩn

```text
Tạo phiếu
  ↓
Truy vết LOT
  ↓
QC định hướng
  ↓
Initial QC
  ├── TuChoiGiaoBu
  ├── GiaoBu
  └── Rework
          ↓
      Xuất Rework
          ↓
      Giao sản xuất
          ↓
      QC Rework
          ↓
      Disposition nếu có NG

GiaoBu là nhánh độc lập và không được suy ra tự động từ NG.
```

## 3. Tạo phiếu

1. Mở màn hình **Xử lý hàng lỗi / QT Chung**.
2. Chọn tạo phiếu.
3. Chọn nguồn phát sinh:
   - Khách trả.
   - Trả nội bộ.
4. Nhập/kiểm tra mã sản phẩm, Model, LOT, số lượng và nội dung bất thường.
5. Lưu phiếu.

## 4. Truy vết LOT

1. Mở phiếu vừa tạo.
2. Chọn **Truy vết LOT**.
3. Hệ thống tìm dữ liệu từ kho thành phẩm, WIP/sản xuất và hàng khách trả.
4. Kiểm tra từng dòng nguồn, LOT, slot và số lượng.
5. Chỉ tiếp tục khi trace hoàn chỉnh và tổng số lượng ảnh hưởng lớn hơn 0.

Sau khi trace, dữ liệu được snapshot để QC làm việc ổn định ngay cả khi tồn kho thay đổi.

## 5. QC định hướng

Chọn hướng xử lý phù hợp:

### TuChoiGiaoBu
Dùng khi QC xác định không phát sinh nghĩa vụ xử lý hàng/giao bù.

### ChiGiaoBu
Dùng khi phát sinh nghĩa vụ thay thế/giao bù nhưng không cần Rework.

### CanRework
Dùng khi một phần NG có thể được sửa chữa.

## 6. Initial QC

Nhập kết quả theo từng LOT đã snapshot.

Các cột chính:

- `DaKiemTra` — số lượng đã kiểm tra.
- `OK` — đạt.
- `NG` — không đạt.
- `Rework` — phần NG được phép sửa.
- `LoaiBoBanDau` — phần NG loại bỏ ngay.

Kiểm tra trước khi bấm xác nhận:

```text
DaKiemTra = OK + NG
NG = Rework + LoaiBoBanDau
```

Tổng `DaKiemTra` phải bằng tổng số lượng ảnh hưởng của snapshot.

## 7. Thực hiện Rework

Chỉ thực hiện nếu Initial QC có `Rework > 0` và hướng xử lý là `CanRework`.

### 7.1. Xuất kho Rework

1. Chọn phiếu.
2. Chọn LOT cần xuất.
3. Scan/chọn hàng và slot.
4. Nhập số lượng.
5. Xác nhận xuất.

Hệ thống không cho tổng số đã xuất vượt `InitialQC.SoLuongRework`.

Có thể xuất nhiều lần nếu mỗi lần hợp lệ và tổng lũy kế không vượt kế hoạch.

### 7.2. Giao sản xuất

Sau khi đã xuất đủ Rework:

1. Chọn **Giao Rework**.
2. Nhập LOT và số lượng giao.
3. Ghi nhận người/bộ phận nhận.
4. Xác nhận.

Không thể giao vượt kế hoạch và không thể hoàn thành QC Rework khi chưa giao đủ.

## 8. QC sau Rework

Sau khi toàn bộ Rework đã được giao sản xuất:

1. Mở **QC xác nhận cuối/Rework QC**.
2. Nhập OK và NG.
3. Xác nhận.

Bắt buộc:

```text
OK + NG = tổng Rework đã được duyệt
```

Chỉ có một kết quả QC Rework cuối cho một phiếu.

## 9. Disposition

Nếu QC Rework có NG, thực hiện disposition theo quy định của nhà máy.

Số lượng loại bỏ cuối được theo dõi theo:

```text
Loại bỏ cuối = Loại bỏ ban đầu + NG sau Rework
```

Không tự tạo Rework mới từ NG sau Rework trong cùng phiếu.

## 10. Giao bù

Giao bù là nghĩa vụ độc lập.

1. Tạo/xác nhận nghĩa vụ giao bù.
2. Hệ thống xác định sản phẩm và số lượng phải thay thế.
3. Scan QR hàng thay thế.
4. Hệ thống kiểm tra mã hàng, LOT, slot và số lượng.
5. Xuất/giao hàng theo tồn kho/FIFO.
6. Có thể giao nhiều lần.
7. Không được giao vượt `CompensationRemaining`.

**Không nhập số lượng giao bù bằng cách lấy số NG nếu nghiệp vụ khách hàng không quy định như vậy.**

## 11. Theo dõi trạng thái

Các trạng thái chính:

| Trạng thái | Ý nghĩa |
|---|---|
| `Moi` | Phiếu mới |
| `DaTaoPhieuBatThuong` | Đã tạo phiếu |
| `DaDinhHuong` | Đã có hướng xử lý |
| `TuChoiGiaoBu` | Kết luận không giao bù |
| `ChoGiaoBu` | Đang chờ giao bù |
| `DaGiaoBu` | Đã giao đủ giao bù |
| `DaXuatKhoRework` | Đã xuất Rework |
| `DaGiaoSanXuat` | Đã giao sản xuất |
| `DaQCXacNhanCuoi` | Đã QC sau Rework |
| `DaNhapLaiKho` | Đã xử lý nhập lại/NG |
| `HoanTat` | Hoàn tất |
| `Huy` | Phiếu bị hủy |

## 12. Khi hệ thống báo lỗi

### "Chưa có snapshot LOT"
Quay lại bước Truy vết LOT và hoàn thành trace.

### "Số lượng QC không đủ/không khớp"
Kiểm tra:

```text
DaKiemTra = OK + NG
NG = Rework + LoaiBoBanDau
```

### "Xuất Rework vượt kế hoạch"
Kiểm tra tổng số đã xuất và `InitialQC.SoLuongRework`.

### "Chưa thể giao Rework"
Phải xuất đủ số lượng Rework trước.

### "Chưa thể QC Rework"
Phải giao đủ toàn bộ Rework cho sản xuất trước khi QC.

### "Giao bù vượt số lượng còn thiếu"
Kiểm tra `Required - Delivered`. Không được giao vượt nghĩa vụ.

### "Không thể chuyển trạng thái"
Thao tác hiện tại không có transition hợp lệ trong workflow `QT_CHUNG`.

## 13. Quy tắc người dùng phải nhớ

1. Trace trước, QC sau.
2. QC theo LOT snapshot, không tự sửa số lượng ảnh hưởng.
3. Initial QC quyết định phần nào OK, NG, Rework và loại bỏ ban đầu.
4. Rework lấy từ Initial QC.
5. Không xuất Rework vượt kế hoạch.
6. Không giao Rework trước khi xuất đủ.
7. Không QC Rework trước khi giao đủ.
8. NG sau Rework đi vào Disposition.
9. Giao bù là nghĩa vụ riêng, không tự suy ra từ NG.
10. Không sửa tồn kho trực tiếp trên màn hình QT Chung.

## 14. Checklist trước khi hoàn tất phiếu

- [ ] LOT đã được trace đầy đủ.
- [ ] Initial QC đã xác nhận đủ số lượng.
- [ ] Nếu có Rework: đã xuất đủ.
- [ ] Nếu có Rework: đã giao sản xuất đủ.
- [ ] Nếu có Rework: đã QC đủ.
- [ ] NG sau Rework đã được Disposition.
- [ ] Nếu có giao bù: đã giao đủ nghĩa vụ.
- [ ] Workflow đã ở `HoanTat`.
- [ ] Có thể truy ngược báo cáo từ phiếu xuống LOT/lịch sử.
