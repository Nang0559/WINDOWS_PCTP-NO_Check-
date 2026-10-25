# WMS_STOCK_TRANSACTION_RULES

## 1. Mục tiêu và phân lớp trách nhiệm

Contract này áp dụng cho mọi nghiệp vụ thay đổi tồn kho. Một nghiệp vụ tồn kho = một transaction nguyên tử = một audit trail có thể truy vết.

1. Workflow owner: kiểm tra nghiệp vụ, FIFO, QR/PART, chứng từ, trạng thái, phân bổ LOT, chọn Slot và điều phối transaction.
2. KhoCore stock command: validate bất biến tồn, khóa dữ liệu và thay đổi STOCKTP/Slot/SlotLot.
3. Audit/history writer: ghi lịch sử nghiệp vụ trong cùng transaction. Hiện StockMovementService không tự ghi StockHistory; caller workflow đang ghi history.

## 2. Owner và command hiện có

- StockMovementService: Receive, Pick, Export, Move, ReturnFromRework, Correct, RestorePicked.
- QuarantineReleaseService: command chuyên biệt trong KhoCore cho release hàng cách ly; phải giữ cùng transaction cho SlotLot, STOCKTP và audit.
- StockExportService: workflow xuất, không phải stock writer thay thế KhoCore.
- NhapTpReceivingService: workflow nhập; hiện request có SlotLotWrittenByCaller=true, nên SlotLot detail còn được caller/repository tạo.
- Nhập thành phẩm lấy khóa transaction theo thứ tự QR → LOT. Khóa LOT phải được lấy trước khi đọc SLNHAP tích lũy và tính trạng thái hoàn thành; không thay thế khóa QR/idempotency.
- PhieuKhoRepository: hiện còn SP/SQL direct STOCKTP cho giao hàng khách; đây là lỗi lệch kiến trúc cần migration.

## 3. Transaction matrix

| Nghiệp vụ | Slot / SlotLot | STOCKTP | History | Owner hiện tại / lưu ý |
|---|---:|---:|---:|---|
| Nhập thành phẩm OK | + | + | + | Receiving workflow + StockMovementService; KhoCore ghi SlotLot detail và Slot header, STOCKTP qua adapter legacy trong cùng UoW |
| Pick | - | 0 | + | StockExportService + StockMovementService |
| Confirm Export | - | - | + | StockExportService + StockMovementService |
| Hủy chờ giao | + | 0 | + | RestorePicked; Pick chưa trừ STOCKTP |
| Move | - / + | 0 | + | StockMovementService; caller quản transaction |
| Rework OK receive | + | + | + | StockMovementService + workflow audit |
| Rework NG receive | + quarantine | 0 | + | PhieuStatus=3; không cộng tồn khả dụng |
| QC release | quarantine → active | + | + | QuarantineReleaseService thuộc KhoCore |
| Giao hàng khách legacy | - | - | + | Hiện repository/SP trừ STOCKTP rồi đồng bộ SlotLot; chưa đạt Single Writer |
| Giao bù | tùy command | tùy command | + | Nghĩa vụ giao bù riêng; không suy ra từ NG |
| Correct | +/- | +/- | caller audit | StockMovementService tự quản transaction |

## 4. Transaction ownership

- AddToSlot, RemoveFromSlot, Move bắt exception và trả Fail; các phương thức này không tự rollback. Caller phải mở transaction trước khi gọi và rollback nếu kết quả không Success.
- Pick theo Slot + LOT dùng TakeLot; transaction thuộc caller.
- Correct tự Begin/Commit/Rollback và yêu cầu UnitOfWork.
- RestorePicked chỉ phục hồi SlotLot; caller quản transaction và history.
- Không ghi history trước khi stock mutation thành công; history và chứng từ phải commit/rollback cùng stock.
- Mọi caller phải kiểm tra Success và rollback khi Fail hoặc Duplicate. Không tiếp tục cập nhật chứng từ khi stock command thất bại.

## 5. Bất biến bắt buộc

1. Quantity hợp lệ và dương cho movement thông thường.
2. Không âm tồn STOCKTP hoặc SlotLot.
3. Không vượt capacity Slot.
4. LotNo phải thuộc đúng ItemCode.
5. Không xuất SlotLot có trạng thái cách ly/không active.
6. Một movement không được ghi một phần.
7. Reference/idempotency phải enforce ở đúng cấp workflow command. Không claim cùng ReferenceId riêng lẻ ở cả Pick và Export nếu đây là hai phase của cùng workflow.
8. STOCKTP và SlotLot phải được điều chỉnh trong cùng transaction đối với nghiệp vụ thay đổi đồng thời cả hai.
9. History phải nằm cùng transaction với stock writes.
10. Module nghiệp vụ không được direct UPDATE STOCKTP/Slot/SlotLot ngoài KhoCore command.
11. Không để SP legacy và application command cùng trừ một lần giao.

## 6. Locking và retry

- Lock Slot và LOT theo thứ tự ổn định; nếu có nhiều Slot/Lot phải sort khóa nhất quán để giảm deadlock.
- Kiểm tra tồn lại sau khi lấy lock, không dựa vào dữ liệu đọc trước UI.
- Retry chỉ an toàn khi command idempotent và reference scope được xác định theo toàn nghiệp vụ (không nhầm Pick phase với Export phase).
- Khi command trả Duplicate, caller phải dừng workflow hoặc trả kết quả idempotent đã lưu; không tiếp tục ghi chứng từ.

## 7. Audit tối thiểu

ItemCode, LotNo, SourceSlot, DestinationSlot, Quantity, MovementType, ReferenceType, ReferenceId, PerformedBy, timestamp, reason; mã phiếu/QR/tem khi nghiệp vụ yêu cầu.

## 8. Anti-patterns

Cấm:
- Form/repository nghiệp vụ tự UPDATE STOCKTP.
- Module nghiệp vụ tự thay đổi Slot/SlotLot trực tiếp.
- Gọi SP trừ STOCKTP rồi gọi command khác trừ SlotLot mà không có atomic transaction và kiểm tra kết quả.
- Bỏ qua kết quả Fail/Duplicate của movement.
- Ghi history hoặc đóng chứng từ khi stock transaction đã rollback.

Cho phép legacy delivery SQL/SP tồn tại tạm thời trong migration có kiểm soát, có owner, test bảo vệ và không chạy đồng thời với command mới.
