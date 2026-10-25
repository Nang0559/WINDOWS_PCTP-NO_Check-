# WMS — Kiến trúc tổng thể và trạng thái triển khai

Tài liệu này phân biệt kiến trúc đích với code đang chạy. Không mô tả ý định như tính năng đã hoàn thành.

## 1. Nguyên tắc kiến trúc đích

- KhoCore là owner của trạng thái tồn: STOCKTP (tổng khả dụng), Slot (vị trí) và SlotLot (chi tiết LOT tại vị trí).
- Module nghiệp vụ giữ trách nhiệm workflow: chứng từ, QR, PART, FIFO, phân bổ LOT, trạng thái và điều phối transaction.
- Stock mutation phải được thực hiện bởi application command thuộc KhoCore, không để repository nghiệp vụ tự cập nhật tồn.
- Audit/history thuộc workflow nghiệp vụ hoặc command audit chuyên biệt, nhưng phải cùng transaction với stock mutation.
- Không đồng nhất IStockExportService với IStockMovementService: một bên điều phối xuất kho; bên còn lại thực hiện movement primitive.

## 2. Các lớp hiện có

| Thành phần | Trách nhiệm |
|---|---|
| IStockMovementService / StockMovementService | Primitive commands: Receive, Pick, Export, Move, ReturnFromRework, Correct, RestorePicked. |
| IStockExportService / StockExportService | Workflow xuất: validate, lock, Pick, HangChoGiao, xác nhận Export, hủy chờ giao, chứng từ và history. |
| INhapTpReceivingService / NhapTpReceivingService | Workflow nhận QR/phiếu nhập, production metadata, receiving tracking, chống trùng, transaction và history; hiện vẫn có đường caller ghi SlotLot. |
| QuarantineReleaseService | Command chuyên biệt KhoCore để chuyển hàng cách ly sang khả dụng, cập nhật SlotLot + STOCKTP + audit trong transaction. |
| PhieuKhoRepository | Hiện vẫn có đường giao hàng khách gọi SP/SQL để trừ STOCKTP, sau đó đồng bộ SlotLot và đóng chứng từ. Đây là phần chưa đạt đích. |

## 3. Workflow thực tế hiện tại

### 3.1 Nhập thành phẩm

QR / phiếu sản xuất → NhapTpReceivingService parse, validate, chống trùng → StockMovementService.Receive → PhieuTrackingRepository.InsertPhieuMoi tạo chi tiết phiếu/SlotLot (caller-owned) → history + commit.

Trong transaction, workflow lấy application lock theo QR trước, sau đó theo LOT (`AcquireImportLock` → `AcquireLotImportLock`). Khóa QR chống hai máy nhập cùng tem; khóa LOT tuần tự hóa các tem khác nhau cùng đóng góp vào một STOCKTP LOT, để phép tính SLNHAP/status không dùng snapshot cũ. Khóa được giữ đến commit/rollback.

Điểm cần hoàn thiện: đưa việc tạo SlotLot detail vào atomic receiving command hoặc port KhoCore được ủy quyền rõ ràng. Không tạo LOT vật lý mới cho từng phiếu nếu LOT sản xuất là duy nhất.

### 3.2 Xuất kho chuẩn (Pick → Confirm)

StockExportService validate + Begin transaction + lock Slot → StockMovementService.Pick trừ SlotLot, chưa trừ STOCKTP → tạo HangChoGiao + pick details + history → Confirm gọi StockMovementService.Export trừ STOCKTP + SlotLot → cập nhật HangChoGiao + history + commit.

Hủy chờ giao gọi RestorePicked để hoàn SlotLot; không cộng STOCKTP vì Pick chưa trừ tồn tổng.

### 3.3 Giao hàng khách — hiện trạng chưa đạt đích

PhieuKhoService thực hiện FIFO gate / release chính xác STT / đánh giá lại. Sau đó PhieuKhoRepository.CapNhapKho và CapNhapKhoSP gọi stored procedure trừ STOCKTP, đọc result LOT/SOLUONG, gọi BulkStockAdjustService đồng bộ SlotLot (A0 và Slot thật), đóng HangChoGiao, ghi history và commit.

CapNhapKhoYMVN tự khóa/đọc STOCKTP, UPDATE STOCKTP theo FIFO, gọi BulkStockAdjustService trừ SlotLot, lưu chứng từ, đóng HangChoGiao, ghi history và commit.

Các đường trên vẫn ghi STOCKTP từ repository/SP ngoài stock command KhoCore. Đây là migration ưu tiên P0. Không xóa SP hoặc chuyển sang command mới cho đến khi command thay thế bảo toàn FIFO, phân bổ nhiều LOT/Slot, QR, PART, số lượng, chứng từ và rollback.

### 3.4 Hàng lỗi

- Rework xuất, nhập NG cách ly và trả OK hiện gọi IStockMovementService trong các đường đã rà.
- Giao bù điều phối qua IStockExportService; phải giữ riêng nghĩa vụ giao bù.
- QC release là command KhoCore riêng, không nhập chung với Rework OK hoặc coi NG tự động là giao bù.

## 4. Sơ đồ dependency

NhapKho workflow ─┐
XuatKho workflow ─┤
Rework / GiaoBu ──┼──> KhoCore application commands
Quarantine release ┘

GiaoHangKhach workflow ── hiện tại ──> PhieuKhoRepository ──> SP/SQL STOCKTP
GiaoHangKhach workflow ── đích ──────> delivery stock command KhoCore

## 5. Wave migration và điều kiện đóng

1. Giao Hàng Khách: đưa STOCKTP + SlotLot vào một atomic command; giữ FIFO/QR/PART/multi-LOT; bỏ direct SQL/SP sau khi có regression và SQL integration.
2. Nhập Kho: chuyển caller-owned SlotLot write vào atomic receiving command; giữ một LOT sản xuất có thể có nhiều phiếu nhập.
3. Hàng lỗi: xác minh và test riêng NG quarantine, Rework OK/NG, QC release, giao bù.
4. SQL/UI E2E: schema/fixtures được DBA phê duyệt, G6 journeys thao tác UI thật, assert hậu điều kiện và cleanup.
5. Tài liệu: workflow, dependency, transaction rules và deployment SQL được cập nhật theo source/test đã PASS.

Một wave chỉ hoàn tất khi production code, regression, SQL integration, tài liệu và CI evidence cùng xác nhận trên một commit SHA. Không dùng SQL production/shared cho test mutation.

## 6. Tài liệu con

- WORKFLOW_KHOCORE.md
- WORKFLOW_NHAPKHO.md
- WORKFLOW_XUATKHO.md
- WORKFLOW_HANGLOI.md
- WORKFLOW_GIAOHANGKHACH.md
- WMS_STOCK_TRANSACTION_RULES.md
- ARCHITECTURE_DEPENDENCIES.md
- WMS_IMPLEMENTATION_AUDIT.md
