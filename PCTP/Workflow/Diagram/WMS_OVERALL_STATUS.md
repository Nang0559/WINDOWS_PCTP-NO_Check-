# WMS OVERALL STATUS

> Snapshot at commit `61d1064910bc1dd3b1a4cfcdceaf7311ebe2164b`.

## 1. Tổng quan kiến trúc hiện tại

```text
NhapKho ───────────────┐
XuatKho ───────────────┤
GiaoHangKhach ─────────┤
XuLyHangLoi ───────────┤
                       v
              IStockMovementService
                       |
              StockMovementService
                 /      |      \
                v       v       v
      IStockBalance  IStockSlot  IStockReceiving
          |              |             |
          v              v             v
      legacy DB      legacy Slot    STOCKTP receive
       adapter         adapter         adapter
```

## 2. Phase status

| Phase | Status | Ghi chú |
|---|---|---|
| Phase 1 - Architecture contract | 🟡 90% | Contract/rules/ownership/state-machine đã có; còn dọn tài liệu legacy mâu thuẫn. |
| Phase 2 - KhoCore consolidation | 🟡 70% | Contract mới và adapter boundary đã có; Slot/SlotLot/STOCKTP/History chưa hoàn toàn về KhoCore. |
| Phase 3 - Central StockMovement | 🟡 80% | Central write boundary đã hoạt động; còn legacy caller proof, XuLyHangLoi composition, idempotency và tests. |
| Phase 4 - NhapKho | 🟢 90% | Receiving mutation đã qua StockMovement; còn audit/direct-write cleanup. |
| Phase 5 - XuatKho | 🟢 95% | PICK/EXPORT chính đã qua StockMovement; còn legacy adapter/read cleanup. |
| Phase 6 - GiaoHangKhach | 🟢 100% | 12A-12H hoàn tất; BulkStockAdjust đã bỏ direct A0 SlotLot mutation. |
| Phase 7 - XuLyHangLoi | 🟡 75% | Rework stock mutation đã qua StockMovement; composition root và remaining direct writes cần chứng minh sạch. |
| Phase 8 - Shared cleanup | 🔴 Chưa đóng | Chưa phải ưu tiên trước khi stock write boundary sạch. |
| Phase 9 - Legacy cleanup | 🔴 Chưa đóng | Chỉ xóa sau khi caller/reference verification hoàn tất. |
| Phase 10 - Verification | 🟡 25% | Build là developer-run gate; static scan/integration/concurrency/reconciliation tests còn thiếu. |

## 3. Những phần đã hoàn thành quan trọng

- `IStockMovementService` + `StockMovementService` là write boundary trung tâm.
- `Receive`, `Pick`, `Export`, `Move`, `ReturnFromRework`, `Correct` đã có contract.
- NhapKho receiving đã chuyển sang `Receive`.
- XuatKho physical PICK/EXPORT chính đã chuyển sang central movement.
- GiaoHangKhach `BulkStockAdjustService` không còn `SaveLots`/`UpdateSlotHeaderFromLots`.
- XuLyHangLoi `ReworkStockService` đã chuyển mutation sang central movement.
- `IStockSlotRepository.TakeLot` bắt buộc `ItemCode` để tránh lấy nhầm LOT tương đương của item khác.
- `StockMovementService` đã kiểm tra `SlotLotId -> LotNo/ItemCode` trước khi `Export/Move`, ngăn lỗi decrement STOCKTP LOT A nhưng giảm SlotLot LOT B.
- Legacy Slot mutation logic đã được gom vào `LegacyStockSlotRepositoryAdapter`.
- `IBulkStockSlotRepository` hiện chỉ resolve/lock/query.

## 4. Các điểm CHƯA được coi là hoàn thành

### A. Legacy stock caller verification — BLOCKER

Chưa có bằng chứng đủ mạnh rằng toàn bộ caller của:

- `IStockTpRepository.XuatKhoThat`
- `IStockTpRepository.DieuChinhSlConLai`

đã biến mất. GitHub code-search hiện không trả kết quả, nhưng code-search miss không được coi là proof.

### B. XuLyHangLoi composition root — BLOCKER

`ReworkStockService` yêu cầu `IStockMovementService`, nhưng chưa xác định được một composition factory riêng trong tree. Cần tìm đúng điểm khởi tạo service và bảo đảm movement service được compose một lần, không `new` ad-hoc trong Form.

### C. Idempotency — CHƯA LÀM

`StockMovementRequest` có `ReferenceType/ReferenceId`, nhưng `StockHistory` hiện chưa có durable/queryable business-key contract tương ứng. Không được giả lập idempotency bằng field chưa được persist.

### D. KhoCore ownership — CHƯA ĐÓNG

KhoCore chưa phải physical sole owner của toàn bộ Slot/SlotLot/STOCKTP/StockHistory. Hiện vẫn dùng transitional adapters vào KhoVatLy/NhapKho/XuatKho.

### E. Verification tests — CHƯA ĐÓNG

Còn cần:

- stock transaction integration tests
- concurrency/locking tests
- Rework round-trip
- Pick -> Delivery -> Export
- Receiving -> Slot/Lot -> STOCKTP reconciliation
- static dependency scan

## 5. Bước tiếp theo theo đúng thứ tự

```text
1. Xác minh caller legacy thực tế
       |
       +--> XuatKhoThat
       +--> DieuChinhSlConLai
       |
2. Xác minh composition XuLyHangLoi
       |
3. Nếu còn direct writer -> migrate
       |
4. Nếu không còn caller -> thu hẹp/xóa legacy mutation API
       |
5. Xác minh DB business key
       |
6. Thiết kế idempotency durable
       |
7. Integration + concurrency tests
       |
8. Static dependency scan
       |
9. KhoCore physical ownership migration
       |
10. Legacy cleanup / Phase 9
```

## 6. Kết luận hiện tại

**Chưa thể đánh dấu toàn bộ WMS refactor là hoàn thành.**

Điểm đã đạt được là kiến trúc stock write đã chuyển từ nhiều đường ghi trực tiếp sang một central movement boundary. Phần còn lại hiện chủ yếu là **proof/verification + loại bỏ legacy escape hatches + tests**, sau đó mới chuyển sang physical ownership và legacy cleanup.

Build thực tế vẫn là developer-run gate theo roadmap; repository hiện không cung cấp CI status đủ để kết luận build pass.
