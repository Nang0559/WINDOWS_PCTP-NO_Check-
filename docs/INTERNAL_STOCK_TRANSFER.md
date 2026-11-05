# Điều chuyển và thu hồi hàng tại vị trí nội bộ

## Điều chuyển ra nội bộ
- Chọn LOT khả dụng trong Slot (PhieuStatus = 0).
- Đối chiếu tổng Slot theo LOT/PART với STOCKTP; chênh lệch thì khóa thao tác.
- Một transaction: trừ SlotLot + STOCKTP, tăng PCTP_InternalStock, ghi StockHistory và PCTP_InternalTransfer.
- Không tạo phiếu giao khách.

## Thu hồi về kho
- Chọn một lần điều chuyển gốc còn số lượng chưa thu hồi.
- Chọn Slot nhận tương thích mã hàng, còn sức chứa.
- Một transaction: khóa chứng từ gốc, khóa số dư nội bộ và Slot nhận; kiểm tra số lượng còn được thu hồi; giảm PCTP_InternalStock; gọi KhoCore StockMovementService.Receive để thêm SlotLot và tăng STOCKTP; đồng bộ Slot header; ghi StockHistory và PCTP_InternalReturn.
- Thu hồi được một phần hoặc toàn bộ; tổng số đã thu hồi của một lần điều chuyển không thể vượt số lượng đã chuyển.
- RequestKey và ReferenceType/ReferenceId bảo vệ gửi lặp. Nếu bất kỳ bước nào lỗi, rollback cả sổ nội bộ, SlotLot, Slot header, STOCKTP và lịch sử.

## UI
Hai màn hình riêng trong nhóm menu **Điều chuyển nội bộ** (đặt cạnh nhóm Kho và Giao Hàng):
- Chuyển hàng sang vị trí nội bộ.
- Thu hồi hàng từ vị trí nội bộ về kho.

Cả hai là XtraForm và dùng DevExpress GridControl/GridView, GroupControl, TextEdit, SpinEdit, LookUpEdit, MemoEdit, SimpleButton; không dùng DataGridView.

## Database
Migration `SQL/PCTP_InternalTransfer.sql` tạo bảng vị trí, điều chuyển, số dư nội bộ và thu hồi. Chỉ áp dụng trên database QC được phê duyệt (DB57); tuyệt đối không chạy trên DB14.
