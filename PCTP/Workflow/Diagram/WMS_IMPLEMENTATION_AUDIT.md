# WMS Implementation Audit — 2026-10-25

> Phạm vi: đối chiếu code thực tế, SQL, test và tài liệu trên branch `feature/wms-end-to-end-consistency`.
> Code snapshot đã rà soát cho cập nhật này: `6a5351c40ad19ec20135ec4a43b06670ad3861e6`. CI của HEAD tài liệu đang chạy; xem evidence trên GitHub Actions.
> Đây là biên bản hiện trạng đã xác minh, không phải tuyên bố hệ thống đã đạt Single Writer.

## 1. Kết luận điều hành

Kiến trúc đã có lớp biến động tồn trung tâm `IStockMovementService / StockMovementService`, nhưng chưa thể kết luận toàn repo đã tuân thủ Single Writer. Một số luồng vẫn thực hiện mutation tại repository nghiệp vụ hoặc stored procedure; một số luồng dùng StockMovementService cho một phần mutation nhưng vẫn để caller ghi phần còn lại.

Phân biệt ba vai trò:

- **Workflow/business orchestration:** kiểm tra nghiệp vụ, FIFO, chứng từ, trạng thái, chọn LOT/phiếu, quản lý transaction và điều phối các bước.
- **Stock command/mutation:** thay đổi số lượng tổng `STOCKTP`, tồn vị trí `Slot` và chi tiết `SlotLot`.
- **Audit/history:** ghi lịch sử nghiệp vụ trong cùng transaction; hiện không phải trách nhiệm tự động của `StockMovementService`.

Không gộp `IStockExportService` với `IStockMovementService`: một bên điều phối quy trình xuất, bên kia thực hiện stock movement.

## 2. Luồng đã xác minh từ source

| Luồng | Code thực tế | Mutation đã xác minh | Trạng thái |
|---|---|---|---|
| Nhập thành phẩm | `NhapTpReceivingService.NhapTpVaoSlot` | Lấy applock QR → LOT; gọi `IStockMovementService.Receive`; command gọi receiving adapter cập nhật STOCKTP, `BulkStockSlotRepository.AddLot` ghi SlotLot đầy đủ metadata và refresh Slot header; workflow ghi history trước commit. | **SlotLot + Slot header đã chuyển vào KhoCore command**; STOCKTP vẫn đi qua `StockReceivingRepositoryAdapter` → `StockTPRepository` legacy, nên chưa Single Writer hoàn toàn ở persistence layer. |
| Xuất kho — Pick | `StockExportService.PickToChoGiao` | Khóa Slot, gọi `IStockMovementService.Pick`, tạo `HangChoGiao`, lưu history trong cùng UoW. Pick không trừ STOCKTP. | Dùng workflow + movement boundary. |
| Hủy chờ giao | `StockExportService.CancelChoGiao` | Gọi `RestorePicked` để hoàn chi tiết đã pick; cập nhật trạng thái chờ giao và history. Không cộng lại STOCKTP vì Pick chưa trừ STOCKTP. | Dùng workflow + movement boundary. |
| Xác nhận giao | `StockExportService.ConfirmGiaoHangTuChoGiao` | Gọi `IStockMovementService.Export` để trừ STOCKTP và SlotLot; cập nhật HangChoGiao, ghi history trong cùng UoW. | Dùng workflow + movement boundary. |
| Xuất trực tiếp | `StockExportService.XuatTrucTiep` | Điều phối Pick/Export và chứng từ/history trong transaction. | Cần tiếp tục kiểm tra mọi nhánh nguồn Slot/A0 và đối chiếu allocation. |
| Rework xuất | `ReworkStockService.XuatKhoRework` | Gọi `IStockMovementService.Export`; kiểm tra trạng thái SlotLot, LOT, ItemCode, số lượng và STOCKTP trước; ghi audit/history trong UoW. | Đã chuyển sang movement boundary trong đường code đã rà. |
| Rework nhập NG | `ReworkStockService` | Gọi `IStockMovementService.Receive` với loại Rework NG; SlotLot cách ly `PhieuStatus=3`, không cộng tồn khả dụng STOCKTP. | Đã chuyển sang movement boundary trong đường code đã rà. |
| Rework trả OK | `ReworkStockService` | Gọi `ReturnFromRework`; ghi history/audit trong transaction. | Đã chuyển sang movement boundary trong đường code đã rà. |
| Giao bù NG | `GiaoBuNGService` | Điều phối qua `IStockExportService`, không trực tiếp cập nhật tồn trong service đã rà. | Phải giữ nghĩa vụ giao bù riêng, không suy ra chỉ từ việc có hàng NG. |
| QC release hàng cách ly | `QuarantineReleaseService` | Application service thuộc KhoCore gọi repository Slot/Balance, khóa Slot, đọc lại trạng thái sau lock, cập nhật SlotLot + STOCKTP + audit trong transaction. | Là command chuyên biệt trong KhoCore; không đi qua `IStockMovementService`, cần thể hiện rõ trong kiến trúc. |

## 3. Vi phạm / đường mutation còn tồn tại

### 3.1 Giao Hàng Khách — mức ưu tiên P0

File: `PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs`

- `CapNhapKho` gọi trực tiếp `Usp_Qrcode_Update_Stock2405`.
- `CapNhapKhoSP` gọi trực tiếp `Usp_Qrcode_Update_Stock_SP`.
- `CapNhapKhoYMVN` tự đọc `STOCKTP` với `UPDLOCK, HOLDLOCK`, gọi `TruStockTpFifo` và thực hiện `UPDATE STOCKTP`.
- Sau khi SP/SQL trừ STOCKTP, repository gọi `BulkStockAdjustService` để trừ SlotLot bằng `IStockMovementService.Pick`, rồi đóng các dòng HangChoGiao và ghi history.

Kết luận: `IStockMovementService` hiện chỉ quản lý một phần SlotLot trong đường giao khách; mutation STOCKTP vẫn ở repository/SP. Đây là bypass thực tế của kiến trúc trung tâm, dù transaction và rollback đã được kiểm soát ở một số nhánh. Không được mô tả luồng này như đã chuyển hoàn toàn sang `IStockExportService`.

### 3.2 Nhập kho — mức ưu tiên P1, đã chuyển một phần

`NhapTpReceivingService` hiện không gọi `_phieuRepo.InsertPhieuMoi` trong luồng nhập thành phẩm chuẩn và không bật `SlotLotWrittenByCaller`. Workflow truyền TemCode, QrData, NgaySX, SoPhieuTong, MaPhieu và ParentSoPhieu vào `StockMovementRequest`. `StockMovementService.AddToSlot` gọi receiving adapter để cập nhật STOCKTP, sau đó gọi `BulkStockSlotRepository.AddLot` ghi SlotLot và cập nhật Slot header. `AddLot` fail-closed nếu không có transaction. `ImportDate` của nhận hàng giữ timestamp thực tế, không lấy ngày sản xuất.

Phần còn lại: `StockReceivingRepositoryAdapter` vẫn ủy quyền STOCKTP cho `StockTPRepository`; cần chuyển persistence này vào KhoCore khi có contract/storage migration phù hợp. Phải tiếp tục test QR duplicate, multi-QR same LOT, partial receipt, reopen LOT, capacity, metadata và rollback trên schema được DBA xác nhận.

### 3.3 KhoCore chuyên biệt

`QuarantineReleaseService` là application service khác trực tiếp gọi stock repositories. Đây không phải bypass ra ngoài KhoCore, nhưng chứng minh `StockMovementService` không phải application service mutation duy nhất. Kiến trúc nên quy định **KhoCore application commands là owner**, trong đó standard movement và quarantine release có command riêng.

## 4. Transaction contract đã xác minh

- Comment của `StockMovementService` xác định transaction thông thường do caller sở hữu để stock writes và workflow audit cùng tham gia UoW.
- Các nhánh `AddToSlot`, `RemoveFromSlot`, `Move` bắt exception và trả `StockMovementResult.Fail`; các nhánh này không tự rollback. Caller bắt buộc rollback khi kết quả không thành công.
- `Correct` tự mở/commit/rollback transaction và từ chối chạy nếu không có UoW.
- Các workflow `StockExportService` và `ReworkStockService` đã có mẫu caller Begin → movement → kiểm tra Success → rollback khi fail → history/audit → commit.
- Phải tiếp tục rà tất cả caller của `Move`, `Correct`, `Pick`, `Receive`, `Export`, `RestorePicked`; không được suy rộng từ hai service mẫu.

## 5. Tình trạng test/SQL trên HEAD

### Đã có

- Unit/source-contract: `PCTP.QC.Tests`, gồm StockMovement, Delivery, FIFO, Rework transaction, Quarantine release, test environment policy.
- SQL integration: `PCTP.QC.SqlIntegration`, gồm delivery transaction boundary, production delivery acceptance, STOCKTP repository, concurrency và quarantine release.
- UI automation: `PCTP.QC.UI`, G1–G6, SQL seed/assert/cleanup cho các journey.

### Giới hạn quan trọng

- Run `40172884697` trên code snapshot trước thay đổi nhập SlotLot đã PASS ba job. Run `40173319239` phát hiện hai vấn đề mới trong test: một assertion source-contract tìm nhầm AddLot ở Correct thay vì AddToSlot, và fixture `#SlotLot` thiếu CreatedDate/metadata columns. Đã sửa assertion, mở rộng fixture và schema audit; run `40173498043` trên commit `6a5351c40ad19ec20135ec4a43b06670ad3861e6` đang chờ chạy. Chưa công nhận các thay đổi nhập SlotLot PASS cho đến khi run này hoàn tất.
- SQL integration hiện dùng database CI cô lập/fixture engine; không chứng minh toàn bộ stored procedure nghiệp vụ chạy đúng trên schema sản xuất đại diện.
- G6 business journeys `receive`, `qc-direction`, `qc-final`, `delivery-hvn` còn cấu hình chưa hoàn tất theo tài liệu G6; chỉ self-test đã cấu hình. Không được gọi G6 business E2E là PASS.
- `DeliveryTransactionContractTests` và `DeliveryProductionBoundaryTests` hiện đang kiểm tra/đòi hỏi repository gọi stored procedure trừ tồn, sau đó xử lý kết quả và hoàn tất chờ giao. Các test này bảo vệ transaction contract hiện hữu, nhưng chưa bảo vệ mục tiêu Single Writer mới. Khi migrate giao hàng, phải thay đổi assertion có chủ đích; không xóa test rollback/idempotency.
- Các stored procedure nghiệp vụ cần DBA cung cấp schema/seed chuẩn để kiểm thử tích hợp đại diện; không tự tạo lại định nghĩa procedure thiếu nguồn.

## 6. Kế hoạch cuốn chiếu đề xuất

### Wave 0 — Baseline và inventory (đang thực hiện)

1. Chốt danh sách mọi writer của STOCKTP, Slot, SlotLot, status/metadata và history.
2. Phân loại SQL stored procedure, trigger, repository adapter và caller.
3. Xác minh transaction owner và idempotency của từng command.
4. Ghi rõ test nào là unit/source-contract, SQL fixture, production-schema integration, UI journey.

### Wave 1 — Giao Hàng Khách về một workflow stock command

1. Bảo toàn FIFO RAM, FIFO DB, phân bổ QR/LOT, PART và quantity validation.
2. Thiết kế một command giao hàng theo LOT/Slot allocation có thể nhận kết quả phân bổ nhiều LOT; command phải khóa, kiểm tra tồn, trừ SlotLot và STOCKTP cùng UoW.
3. Chỉ sau khi command stock thành công mới cập nhật chứng từ giao, HangChoGiao và history trong cùng transaction.
4. Giữ các stored procedure legacy ở chế độ compatibility/validation cho tới khi có kiểm thử đối chiếu; không để SP và application command cùng trừ một tồn.
5. Thay source-contract tests đang khóa vào SP bằng tests khóa vào contract mới, vẫn giữ regression về rollback, idempotency, FIFO và partial failure.
6. Thêm SQL integration kiểm tra nhiều LOT, nhiều Slot, thiếu tồn, retry/duplicate, transaction rollback và đối chiếu STOCKTP = tổng SlotLot Active theo LOT/PART.

### Wave 2 — Chuẩn hóa Nhập Kho (đang thực hiện)

1. **Đã làm:** chuyển SlotLot detail + metadata từ `PhieuTrackingRepository.InsertPhieuMoi` vào `StockMovementService.Receive` → `BulkStockSlotRepository.AddLot`; bắt buộc transaction; giữ ImportDate là thời điểm nhập thực tế.
2. **Đã làm:** applock theo QR → LOT để tuần tự hóa hai tem khác nhau cùng cập nhật một LOT sản xuất.
3. **Đã làm:** bổ sung source-contract test và SQL fixture kiểm tra persistence của metadata; fixture được mở rộng theo schema cột thực tế mà legacy `InsertPhieuMoi` đã sử dụng.
4. **Còn lại:** đưa STOCKTP persistence ra khỏi `StockReceivingRepositoryAdapter`/`StockTPRepository` legacy.
5. **Còn lại:** kiểm thử tích hợp nhận thật trên schema/seed DBA xác nhận cho QR duplicate, số phiếu trùng, nhiều tem cùng lệnh sản xuất, receive partial/retry, reopen LOT, capacity và rollback xuyên STOCKTP + Slot + SlotLot + history.

### Wave 3 — Hàng lỗi / Rework / Giao bù / Quarantine

1. Giữ riêng các nhánh NG cách ly, Rework OK, Rework NG, giao bù và QC release.
2. Không cộng STOCKTP cho hàng cách ly; chỉ release theo kết luận QC và quyền hợp lệ.
3. Đảm bảo giao bù là một nghĩa vụ/chứng từ riêng.
4. Test transition, quantity reconciliation, duplicate, concurrent confirmation và rollback xuyên workflow.

### Wave 4 — SQL và UI end-to-end

1. Đồng bộ versioned SQL với DBA-approved schema/seed.
2. Bật G6 journey nghiệp vụ bằng AutomationId thật từ UI catalog; không giả PASS bằng self-test.
3. Kiểm chứng seed → thao tác UI → assert DB → cleanup và Remaining=0 trên database cô lập.
4. Chạy regression đầy đủ, lưu artifact theo commit SHA và chỉ công nhận evidence đúng HEAD.

## 7. Quy tắc hoàn tất một wave

Một wave chỉ được đóng khi đồng thời có:

- code production đã sửa và review được;
- test mới bắt được lỗi trước sửa và PASS sau sửa;
- SQL fixture/integration chứng minh hậu điều kiện và rollback;
- tài liệu workflow/dependency/transaction cập nhật theo code đã xác minh;
- CI evidence thuộc đúng commit SHA;
- không có thao tác ghi vào database production hoặc SQL dùng chung.
