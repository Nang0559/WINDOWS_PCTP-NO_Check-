# WMS_MODULE_CONTRACTS

## 1. Mục tiêu

Các module nghiệp vụ phải giao tiếp bằng application contract, không phụ thuộc trực tiếp vào repository của module khác.

## 2. Contracts

### KhoCore

Cung cấp:

- Slot/Lot query
- Warehouse query
- Stock availability query
- Stock movement command
- Stock transaction result
- Audit/history query

Không cung cấp business workflow của Nhập/Xuất/Hàng lỗi.

### NhapKho

Cung cấp:

- Receiving command
- Receiving status/query
- Receiving reference

Khi hàng thực sự vào kho, gọi StockMovement contract `Receive`.

### XuatKho

Cung cấp:

- Reservation/Pick command
- HangChoGiao query/command
- Export command

Stock mutation phải qua KhoCore.

### GiaoHangKhach

Là workflow con của XuatKho.

Cung cấp:

- Delivery document/process
- QR/lot selection workflow
- Delivery confirmation

Không tạo stock writer thứ hai.

### XuLyHangLoi

Cung cấp:

- Abnormal ticket
- QC direction
- Rework workflow
- Giao bù workflow

Chỉ phát sinh stock command khi physical quantity thay đổi.

## 3. Integration patterns

Ưu tiên:

```text
Application Service -> Application Contract -> KhoCore
```

Cho các sự kiện cần loose coupling:

```text
Module A -> Domain/Integration Event -> Module B
```

Không dùng:

```text
Module A -> Module B Repository
Module A -> Module B SQL UPDATE
```

## 4. GiaoHangKhach boundary

`GiaoHangKhach` giữ toàn bộ workflow đặc thù của giao khách: QR, phiếu, lot selection, delivery UI.

Stock operation chỉ là dependency bên ngoài workflow:

```text
GiaoHangKhach
      │
      ├── delivery workflow
      └── stock command
              ↓
          KhoCore
```

## 5. XuLyHangLoi boundary

```text
QC / Rework / GiaoBù
          │
          ├── business state
          └── stock command (nếu có)
                         ↓
                     KhoCore
```

## 6. UI boundary

Forms chỉ làm:

- bind data
- nhận input
- gọi application service
- hiển thị Result/Error

Forms không được làm:

- transaction SQL
- update stock tables
- quyết định stock movement type
- ghi history trực tiếp

## 7. Migration order

1. Chuẩn hóa KhoCore/KhoVatLy ownership.
2. Tạo StockMovement contract.
3. Chuyển NhapKho sang contract.
4. Chuyển XuatKho sang contract.
5. Chuyển GiaoHangKhach sang XuatKho + contract.
6. Chuyển XuLyHangLoi/Rework/GiaoBù sang contract.
7. Xóa direct stock writes.
8. Xóa repository trùng và code legacy không còn caller.
9. Build + integration verification.
