# BaoCao — Traceability source inventory

## 1. Source đã xác nhận trong code

`LUUPHIEUGIAOHANG` được đọc bởi `PhieuLuuTruRepository`.

Các cột hiện đã được xác nhận qua implementation:

- `STT`
- `CUA`
- `TRUYEN`
- `MAHANG`
- `TENHANG`
- `LOT`
- `DV`
- `SOLUONG`
- `NGAYGIAO`
- `GIOGIAO`
- `STATUS`
- `TTPHIEU`
- `NHAMAY`
- `HOP`
- `STATUSDOC`
- `Note`
- `PO_NO`
- `PO_ITEM`

`LuuPhieuSP()` còn xác nhận rule cũ:

```text
LOT rỗng + PO_NO có giá trị
    => LOT = PO_NO + '-' + PO_ITEM
```

## 2. Chưa được phép đoán

Các thông tin sau là bắt buộc cho traceability nhưng chưa được chứng minh bằng source code hiện tại:

- cột QRCode thực tế của `LUUPHIEUGIAOHANG`;
- cột chứa dữ liệu tem khách hàng sau lần scan thứ hai;
- khóa liên kết QR/carton → LOT nếu ngoài chuỗi `LOT`;
- nguồn CustomerCode/CustomerName thực tế;
- khóa liên kết LOT → Production;
- khóa liên kết LOT/QR → Receiving/Stock;
- khóa liên kết LOT → Inspection/QC;
- khóa liên kết LOT → NG/Rework.

**Không viết SQL giả định các tên cột trên.**

## 3. Quy tắc migration

1. `LUUPHIEUGIAOHANG` là evidence của giao hàng đã lưu.
2. Một QRCode đại diện cho một carton/thùng giao hàng.
3. `LOT` có thể chứa nhiều allocation; phải parse thành detail rows.
4. BaoCao chỉ đọc; không gọi `QTChungService`, `PhieuKhoRepository` hoặc UI của GiaoHangKhach để truy vấn.
5. Khi schema QR/customer-label được xác nhận, implement adapter tại `BaoCao.Infrastructure`, sau đó mới mở UI QR/LOT/customer.
6. Chỉ xóa implementation đọc cũ sau khi caller parity được kiểm chứng. Không xóa report nghiệp vụ của GiaoHangKhach.
