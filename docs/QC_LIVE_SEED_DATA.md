# Dữ liệu giả lập trên DB57 (QC live seed)

Mục tiêu: tạo dữ liệu thử có thể truy vết, có tiền tố `QCUI_<runId8>`, để chạy quy trình thật trên DB57 và xoá sạch sau đó.
Không đọc hay sửa dòng nghiệp vụ có sẵn. Môi trường đích cố định: `192.168.200.57 / B7R2_FCC` (SQL14/BRAVO luôn bị từ chối).

## Chuỗi dữ liệu của luồng Nhập kho (journey `receive`)

| Bước | Bảng / đối tượng | Dữ liệu seed |
|---|---|---|
| 1 | `B20Item` | 1 mã hàng `<prefix>_FG` (Id mới) |
| 2 | `B30AccDoc` | 2 phiếu sản xuất, mỗi phiếu 10 pcs, cùng 1 LOT 20 ký tự |
| 3 | `dbo.vNhapTP` | tự xuất hiện 1 dòng: SL sản xuất 20, SL đã nhập 0 |
| 4 | `Warehouse` / `Rack` / `Slot` | kho `<prefix>_WH`, kệ `<prefix>_RK`, 2 slot trống |
| 5 | Quét QR tem tổng (làm tay) | ghi `STOCKTP`, `Slot`, `SlotLot`, `StockHistory` |
| 6 | QR tem thùng khi xuất (làm tay) | dùng chuỗi 4 phần từ `qr.sql` |

Định dạng LOT (theo `LotCodeHelper`): `YYMMDD + Id(5) + Ca(1) + Gear(1) + Line(3) + Machine(4)` = 20 ký tự;
LOT in trên tem = LOT20 + Counter(4) + SL(4) = 28 ký tự. Định dạng QR (theo `QRCodeParser`):

- Tem tổng nhập kho (6 phần): `LOT28:ITEM:dd/MM/yyyy:SL:SoPhieuTong:MaPhieu`
- Tem thùng xuất kho (4 phần): `LOT28:ITEM:dd/MM/yyyy:SL`

`SoPhieuTong` và `MaPhieu` là giá trị giữ chỗ theo tiền tố để dễ truy vết.

## Cách dùng

Tự động (G6): `g6-receive` seed → mở màn hình Nhập thành phẩm - QR → SQL xác nhận dòng trên `vNhapTP` → cleanup về `Remaining = 0`.
Bước quét QR chưa được tự động hoá nên journey này chỉ chứng minh lớp dữ liệu, không phải toàn bộ luồng nhập kho.

Thủ công (giữ lại dữ liệu để thử tay):

```powershell
$env:PCTP_QC_SQL_PASSWORD = '<mật khẩu>'   # hoặc để script hỏi
.\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Seed -ConfirmWrites -SqlUser <user>
.\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Qr -Prefix QCUI_xxxxxxxx -SqlUser <user>
.\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Cleanup -Prefix QCUI_xxxxxxxx -ConfirmWrites -SqlUser <user>
```

Khi quét thử, chọn slot thuộc kệ `<prefix>_RK` (màn hình quét liệt kê cả slot thật của DB57).

## Giới hạn đã biết

- DDL thật của `dbo.B30AccDoc` không có trong repo. `seed.sql` dựng câu INSERT từ `sys.columns` và dừng với thông báo rõ nếu bảng có cột NOT NULL không default thuộc kiểu chưa hỗ trợ, hoặc thiếu cột mà `vNhapTP` đọc.
- Script SQL chưa được chạy trên SQL Server thật khi viết. Lần chạy đầu trên DB57 cần được xem như lần kiểm chứng.
- Runner đặt timeout 15 giây cho mỗi lệnh. Nếu `vNhapTP` chậm trên dữ liệu thật, bước assert có thể quá hạn.
- Cleanup và `verify.sql` chỉ phủ các bảng trên (khoá theo mã hàng/kệ/kho có tiền tố). Nếu thao tác quét ghi thêm bảng khác, cần bổ sung.
- Chưa có seed cho luồng xuất kho (`TMPPHIEUGIAOHANG`, `DOCQRCODE`) và QC; xem `SQL/QC_G4_DeliveryFixture_Seed.sql` làm mẫu.
