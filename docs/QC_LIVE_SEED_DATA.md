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
- Seed xuất kho có ở mục bên dưới; chưa có seed cho luồng QC (`g6-qc-direction`, `g6-qc-final`).

## Seed xuất kho (`g6-delivery-hvn`, journey `delivery-hvn`)

Một lần seed tạo, tất cả có tiền tố `QCUI_<runId8>`:

- 1 mã hàng `QCUI_<runId8>_DL` trong `B20Item` (hậu tố `_DL` để không trùng với `_FG` của luồng nhập kho).
- 1 dòng `STOCKTP` (LOT 20 ký tự, `SLCONLAI = 10`, `STATUSNHAP = QC_FIXTURE`).
- 1 dòng `TMPPHIEUGIAOHANG` chờ giao (`SOLUONG = 10`, `Note = <prefix>_DEL`, STT lấy trong dải dành riêng 30000..32500, cột `LOT` = `LOT20-10` theo `QC_G4_DeliveryFixture_Seed.sql`).

Chạy tay: `Invoke-PctpQcSeed.ps1 -Journey delivery-hvn -Action Seed -ConfirmWrites -SqlUser <user>` (in chuỗi QR tem thùng và STT cần giao); dọn bằng `-Action Cleanup -Prefix QCUI_xxxxxxxx`.

Giới hạn:

- Quét QR chưa tự động hóa; journey chỉ chứng minh lớp dữ liệu (`TmpRows=1`, `StockRemaining=10`, chưa có `LUUPHIEUGIAOHANG`).
- Giả định cần xác nhận bằng một lần quét tay: màn hình ghép LOT 28 ký tự của QR với `STOCKTP.LOT` (LOT20) và `TMPPHIEUGIAOHANG.LOT` (`LOT20-qty`). Nếu màn hình từ chối, gửi thông báo lỗi để chỉnh seed.
- Cleanup chỉ xóa `LUUDOCQRCODE`, `DOCQRCODE` (theo `MAHANGFCC`), `LUUPHIEUGIAOHANG`, `TMPPHIEUGIAOHANG`, `STOCKTP`, `B20Item`. Các dòng `PCTPStockMovementGuard` và `FVN_DeliveryCommand*` do một lần giao thật ghi ra không có khóa tiền tố nên **không** bị xóa (mỗi lần seed có mã hàng/LOT mới nên không đụng nhau).

## Guard cho script ghi dữ liệu

- `Assert-PctpTestServerName` / `Assert-PctpConnectedToTestServer` (`PctpSql.Common.ps1`): trước khi ghi, kiểm tra tên server, và sau khi mở kết nối kiểm tra `@@SERVERNAME` (từ chối `BRAVO`) và `DB_NAME()`. Mặc định chỉ `192.168.200.57` / `B7R2_FCC`; đổi bằng `PCTP_QC_ALLOWED_TEST_SERVER` / `PCTP_QC_ALLOWED_TEST_DATABASE`.
- Mọi `LIKE @Prefix` trong SQL của journey đã escape `_` (`REPLACE(@Prefix, N'_', N'[_]')`), vì `_` là ký tự đại diện một ký tự trong `LIKE`.
- Một step có thể đặt `"timeoutSeconds"` (5..120, mặc định 15) cho truy vấn chậm như `vNhapTP`.
- `Test-PctpG6SqlGuards.ps1` chạy trong CI (không cần database): kiểm tra SQL journey qua `Assert-PctpMutationScoped`, escape `LIKE`, journey đã cấu hình có đủ file, và guard server.
