# G6 - journey nghiệp vụ có seed / reset

Mỗi journey chạy theo thứ tự: `sql-seed` → thao tác UI thật → `sql-assert` → `sql-cleanup` → xác minh `Remaining = 0`.

| Manifest | Trạng thái | Phạm vi |
|---|---|---|
| `g6-selftest.json` | Sẵn sàng | Kiểm tra cơ chế prefix/assert/cleanup bằng bảng `#temp`, không ghi bảng nghiệp vụ |
| `g6-qc-direction.json` | Chưa cấu hình | QC định hướng |
| `g6-qc-final.json` | Chưa cấu hình | QC xác nhận cuối |
| `g6-receive.json` | Đã cấu hình mã nguồn; cần xác minh trên Windows runner | Tạo một dòng `B30AccDoc` riêng, kiểm tra `vNhapTP`, tự điền QR tổng, nhập kho qua UI, assert tồn kho/lịch sử/guard rồi cleanup |
| `g6-delivery-hvn.json` | Chưa cấu hình | Giao hàng HVN |

**Giới hạn hiện tại:** `g6-receive` mới bao phủ chiều nhập kho. Kịch bản QR thùng/xuất kho nối tiếp chưa được đánh dấu hoàn tất; cần thêm fixture cho slot có thể xuất và các bảng giao nhận trước khi coi toàn bộ luồng WMS end-to-end là PASS.

## Cơ chế fixture và UI

- `seed.sql` clone một dòng `B30AccDoc` hợp lệ để giữ các cột ERP bắt buộc, chỉ ghi đè ngày/ca/model/mã hàng/số lượng/mã máy/LOT cho run hiện tại; `B20Item` chỉ được đọc.
- `Model` và `ReferenceId` dùng tiền tố `@Prefix` (`QCUI_<runId8>`); LOT 20 ký tự và QR tổng 28 ký tự được sinh riêng từ `@RunId`.
- `captureColumns` cho phép seed trả `itemCode`, `baseLot`, `fullLot` và `qr` cho các bước UI sau đó. QR không cần hard-code trong manifest.
- UI runner có `set-value` và `press-enter`; form nhập kho cung cấp AutomationId ổn định cho ô QR và trạng thái nhập hàng loạt.
- `assert.sql` chỉ PASS khi UI đã tạo đúng một dòng STOCKTP, SlotLot, StockHistory và movement guard với số lượng mong đợi.
- `cleanup.sql` chỉ xóa LOT/QR/phiếu của run; cập nhật lại tổng hợp Slot từ SlotLot còn lại. `verify.sql` phải trả `Remaining = 0`.

## Ràng buộc an toàn

- G6 cần `-AllowMutations`; seed/cleanup đều khai báo `mutatesData: true`.
- DB57 acceptance chỉ nhắm `192.168.200.57/B7R2_FCC`; DB14/BRAVO luôn bị từ chối.
- Không `TRUNCATE`, không `DROP` bảng nghiệp vụ, không `DELETE/UPDATE` thiếu `WHERE`.
- Cleanup chạy trong `finally`, kể cả khi UI thất bại giữa chừng. Nếu cleanup/verify thất bại, gate phải FAIL, không được coi là PASS.
- Script seed phụ thuộc schema thật của `dbo.B30AccDoc`, `dbo.B20Item`, `dbo.vNhapTP` và `dbo.PCTPStockMovementGuard`; nếu thiếu bảng/cột hoặc không có dòng template phù hợp, script fail-closed trước khi thêm dữ liệu.

Chạy trên runner Windows có quyền ghi DB57, sau khi đã xác nhận cả hai input trong workflow manual:

```powershell
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <PCTP.exe> -SqlUser <user> -AllowMutations -LiveDb57
```

Không dùng `-ConfirmAppUsesIsolatedDb` để thay cho DB57 target check; workflow acceptance gọi `-LiveDb57` và xác minh app config trước khi mở UI.
