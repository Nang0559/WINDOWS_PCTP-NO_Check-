# Chuyển hàng sang vị trí nội bộ ngoài hệ thống Slot

## Phạm vi

Chức năng này chuyển một LOT từ Slot vật lý sang một vị trí nội bộ không được quản lý bằng bảng `Slot` (ví dụ khu vực sản xuất, QC nội bộ hoặc khu chờ sửa). Đây không phải giao hàng cho khách và không tạo `PhieuGiao`, `HangChoGiao` hay đơn khách hàng.

## Luồng dữ liệu

1. Tra LOT trong các dòng `SlotLot` đang hoạt động (`PhieuStatus = 0`).
2. Hiển thị vị trí nguồn và số lượng từng dòng SlotLot; người dùng chọn đúng dòng nguồn.
3. Kiểm tra tổng tồn Slot theo LOT với `STOCKTP.SLCONLAI`. Nếu hai số không bằng nhau, màn hình cảnh báo và không cho chuyển theo dữ liệu lệch.
4. Trong một UnitOfWork transaction:
   - Khoá Slot nguồn.
   - Tạo chứng từ `PCTP_InternalTransfer` và resolve/tạo vị trí nội bộ.
   - Gọi `IStockMovementService.Export` theo `SlotLotId`, reference `INTERNAL_TRANSFER`. KhoCore kiểm tra trạng thái, LOT/ItemCode, số lượng và idempotency; sau đó giảm SlotLot và `STOCKTP.SLCONLAI`.
   - Tăng `PCTP_InternalStock` tại vị trí đích.
   - Ghi `StockHistory.ActionType = MOVE_INT`.
   - Commit.
5. Nếu bất kỳ bước nào lỗi, rollback toàn bộ. Không có thao tác cập nhật `SLXUAT` vì đây là điều chuyển nội bộ, không phải xuất bán/giao khách.

## Sổ tồn vị trí nội bộ

- `PCTP_InternalLocation`: danh mục vị trí đích. Màn hình tạo vị trí mới nếu tên chưa tồn tại.
- `PCTP_InternalTransfer`: chứng từ/audit của từng lần chuyển, gồm LOT, ItemCode, số lượng, Slot nguồn, vị trí đích, lý do và người thao tác.
- `PCTP_InternalStock`: số dư hiện tại theo vị trí + ItemCode + LOT. Có unique key chống nhân đôi số dư.

Màn hình có nút **Xem tồn nội bộ** để tra số dư này, có thể lọc theo LOT.

## Triển khai database

Chạy `SQL/PCTP_InternalTransfer.sql` trên database PCTP QC được phê duyệt (DB57 theo cấu hình QC). Script chỉ tạo bảng/index bổ sung; không sửa dữ liệu hay schema DB14. Không chạy migration này trên DB14.

## Kiểm soát và giới hạn hiện tại

- Không cho chuyển dòng SlotLot cách ly/NG vì chỉ nạp `PhieuStatus = 0`.
- Không cho chuyển quá số lượng dòng nguồn.
- Kiểm tra tổng Slot theo LOT so với STOCKTP trước khi cho thao tác.
- Idempotency của thao tác trừ tồn dùng reference `INTERNAL_TRANSFER` + TransferId qua cơ chế guard hiện có.
- Phiên bản đầu ghi nhận tồn nhập vào vị trí nội bộ và hiển thị số dư. Nghiệp vụ xuất/tiêu hao/hoàn trả từ vị trí nội bộ cần được triển khai thành movement type riêng, không được sửa trực tiếp số dư bằng SQL ngoài transaction.

## Kiểm thử cần chạy

1. LOT có tồn khớp: chuyển toàn bộ và chuyển một phần.
2. LOT không có STOCKTP hoặc tổng Slot lệch STOCKTP: phải từ chối, không thay đổi bất kỳ bảng nào.
3. SlotLot NG/cách ly: không xuất hiện trong nguồn khả dụng.
4. STOCKTP không đủ hoặc SlotLot thay đổi đồng thời: rollback.
5. Lỗi khi ghi tồn đích hoặc StockHistory: rollback cả SlotLot và STOCKTP.
6. Double submit/reference đã xử lý: không trừ tồn lần hai.
7. Xem tồn nội bộ theo LOT và đối chiếu tổng số lượng chuyển.
