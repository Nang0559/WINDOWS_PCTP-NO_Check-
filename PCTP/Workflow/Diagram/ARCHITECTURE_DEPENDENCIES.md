# ARCHITECTURE_DEPENDENCIES

## 1. Mục đích

Tài liệu xác định module nào được gọi module nào và quyền read/write. Nó không thay thế workflow nghiệp vụ.

## 2. Phân lớp thực tế

| Phân khu | Thành phần | Trách nhiệm |
|---|---|---|
| KhoCore | IStockMovementService / StockMovementService | Stock primitives: Receive, Pick, Export, Move, Correct, ReturnFromRework, RestorePicked |
| KhoCore | QuarantineReleaseService | Command chuyên biệt release hàng cách ly, cập nhật SlotLot + STOCKTP + audit |
| KhoCore | IStockSlotRepository, IStockBalanceRepository | Ports lưu trữ; implementation hiện có BulkStockSlotRepository, LegacyStockBalanceRepositoryAdapter |
| Xuất Kho | IStockExportService / StockExportService | Workflow Pick/Confirm/Cancel, HangChoGiao, history, transaction |
| Nhập Kho | INhapTpReceivingService / NhapTpReceivingService | QR/phiếu, production metadata, receiving tracking, transaction, history |
| Giao Hàng Khách | PhieuKhoService / PhieuKhoRepository | FIFO gate, QR/LOT, chứng từ; repository hiện còn direct SP/SQL STOCKTP |
| Xử Lý Hàng Lỗi | ReworkStockService, GiaoBuNGService, QTChungService | Workflow Rework/NG/Giao bù; gọi KhoCore hoặc Xuất Kho tùy command |

## 3. Dependency đích

NhapKho workflow ──────────────> KhoCore stock commands
XuatKho workflow ──────────────> KhoCore stock commands
XuLyHangLoi workflow ──────────> XuatKho workflow / KhoCore commands
GiaoHangKhach workflow ────────> delivery stock command thuộc KhoCore (đích; chưa hoàn tất)

## 4. Dependency thực tế cần migration

| Nguồn | Đích hiện tại | Loại | Đánh giá |
|---|---|---|---|
| NhapTpReceivingService | IStockMovementService.Receive | Application command | KhoCore ghi SlotLot detail + Slot header; STOCKTP còn adapter sang StockTPRepository legacy |
| NhapTpReceivingService | IStockTpImportGuardRepository (QR → LOT) | Transaction application locks | Tuần tự hóa nhập cùng QR và QR khác nhau cùng LOT trước khi đọc SLNHAP; KhoCore ghi SlotLot detail |
| StockExportService | IStockMovementService | Application command | Đã dùng cho Pick/Export/RestorePicked |
| ReworkStockService | IStockMovementService | Application command | Đã dùng ở các luồng Rework đã rà |
| GiaoBuNGService | IStockExportService | Workflow | Giữ nguyên workflow giao bù |
| QuarantineReleaseService | Stock repositories + UoW | KhoCore command | Hợp lệ, nhưng là command chuyên biệt ngoài StockMovementService |
| PhieuKhoRepository | Usp_Qrcode_Update_Stock2405 / Usp_Qrcode_Update_Stock_SP | Direct stock mutation | Vi phạm đích Single Writer; Wave 1 |
| PhieuKhoRepository.CapNhapKhoYMVN | SQL UPDATE STOCKTP + BulkStockAdjustService | Direct stock mutation | Vi phạm đích Single Writer; Wave 1 |
| PhieuKhoRepository | BulkStockAdjustService | SlotLot sync | Một phần mutation; phải chuyển cùng command atomic với STOCKTP |

## 5. Read/write policy

- Form chọn Slot/LOT chỉ đọc dữ liệu qua query/service read-only.
- Module nghiệp vụ được đọc tồn để validate/allocate nhưng không được direct mutate stock tables.
- Chỉ application command thuộc KhoCore được thay đổi STOCKTP, Slot, SlotLot. Nhập thành phẩm đã chuyển SlotLot/Slot header qua Receive; adapter STOCKTP vẫn là bước chuyển tiếp cần loại bỏ.
- Workflow owner được ghi chứng từ/history trong cùng UnitOfWork theo contract.
- Quarantine release là command riêng, không được bypass kiểm tra QC/status.
- Legacy delivery SQL/SP chỉ tồn tại tạm thời đến khi Wave 1 có parity tests và cutover; không chạy legacy và command mới cùng một giao dịch.

## 6. Rule về idempotency và transaction

- Workflow cấp cao sở hữu transaction khi stock + chứng từ + history cần commit cùng nhau.
- StockMovementService đa số không tự Begin/Commit; caller phải rollback mọi Fail/Duplicate.
- Correct tự quản transaction.
- Không claim cùng reference hai lần ở hai phase Pick và Export của một workflow. Reference uniqueness phải gắn với workflow/movement phase thích hợp.
- Mọi thay đổi contract phải kèm unit regression, SQL transaction/concurrency test và evidence CI cùng commit.

## 7. Tài liệu liên quan

WORKFLOW_WMS.md
WMS_STOCK_TRANSACTION_RULES.md
WMS_IMPLEMENTATION_AUDIT.md
WORKFLOW_GIAOHANGKHACH.md
WORKFLOW_HANGLOI.md
