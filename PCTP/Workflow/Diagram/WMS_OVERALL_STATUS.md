# WMS OVERALL STATUS

> Snapshot at commit `8b17d32ae6f4436e8209e83bedfe08991f20be38`.

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
| Phase 2 - KhoCore consolidation | 🟡 70% | Contract mới và adapter boundary đã có; physical ownership chưa hoàn toàn về KhoCore. |
| Phase 3 - Central StockMovement | 🟡 85% | Central boundary + source LOT invariant + legacy NhapKho writer cleanup đã làm; còn composition, idempotency và tests. |
| Phase 4 - NhapKho | 🟢 95% | Receiving mutation qua StockMovement; legacy export/correction escape hatches đã loại khỏi NhapKho contract. |
| Phase 5 - XuatKho | 🟢 95% | PICK/EXPORT chính qua StockMovement; còn transitional adapter/read cleanup. |
| Phase 6 - GiaoHangKhach | 🟢 100% | 12A-12H hoàn tất; BulkStockAdjust không còn direct A0 SlotLot mutation. |
| Phase 7 - XuLyHangLoi | 🟡 75% | Rework mutation qua StockMovement; composition root cần chứng minh. |
| Phase 8 - Shared cleanup | 🔴 Chưa đóng | Chưa ưu tiên trước khi verification gate sạch. |
| Phase 9 - Legacy cleanup | 🟡 30% | Một nhóm legacy writer đã được loại; transitional adapters vẫn còn. |
| Phase 10 - Verification | 🟡 30% | Static source review đã tiến thêm; build/integration/concurrency tests còn thiếu. |

## 3. Bốn việc lớn — trạng thái thực tế

### 1. Legacy caller verification / writer cleanup — 🟢 ĐÃ XỬ LÝ PHẦN CODE

Đã loại khỏi business-facing NhapKho contracts:

- `IStockTpRepository.XuatKhoThat(...)`
- `IStockTpRepository.DieuChinhSlConLai(...)`
- `IStockTpLookupService.DieuChinhSlConLai(...)`

SQL implementation tương ứng cũng đã bị xóa khỏi `StockTpRepository`.

`InsertStockTp` / `UpdateStockTp` vẫn giữ vì đây là receiving semantics và đang được central movement gọi qua `IStockReceivingRepository`.

**Gate còn lại:** chạy Visual Studio build để chứng minh không còn caller compile-time nào bên ngoài source review.

### 2. XuLyHangLoi composition root — 🟡 ĐANG XỬ LÝ / CHƯA ĐÓNG

`ReworkStockService` bắt buộc nhận `IStockMovementService`.

Form layer cũng nhận `IReworkStockService` qua constructor, không tự tạo movement service.

Tuy nhiên chưa xác định được một composition factory riêng trong repository tree để chứng minh toàn bộ workflow đang dùng cùng dependency graph/UoW.

**Gate:** xác định đúng điểm tạo `ReworkStockService` và kiểm tra wiring thực tế.

### 3. Idempotency — 🔴 CHƯA IMPLEMENT

`StockMovementRequest` có `ReferenceType/ReferenceId`, nhưng `StockHistory` hiện chưa có durable/queryable business-key contract tương ứng.

Không dùng hai field này như idempotency key cho tới khi xác minh DB schema.

**Gate:** xác định business key thật trong DB hoặc thêm bảng movement/idempotency có unique key.

### 4. Tests / verification — 🟡 CHƯA ĐÓNG

Cần chạy/viết:

- build .NET Framework 4.7.2 / C# 7.3
- stock transaction integration tests
- concurrency/locking tests
- Rework round-trip
- Pick -> Delivery -> Export
- Receive -> Slot/Lot -> STOCKTP reconciliation
- static dependency scan

## 4. Những phần đã hoàn thành quan trọng

- `IStockMovementService` + `StockMovementService` là write boundary trung tâm.
- `Receive`, `Pick`, `Export`, `Move`, `ReturnFromRework`, `Correct` đã có contract.
- NhapKho receiving đã chuyển sang `Receive`.
- XuatKho physical PICK/EXPORT chính đã chuyển sang central movement.
- GiaoHangKhach `BulkStockAdjustService` không còn `SaveLots`/`UpdateSlotHeaderFromLots`.
- XuLyHangLoi `ReworkStockService` đã chuyển mutation sang central movement.
- `IStockSlotRepository.TakeLot` bắt buộc `ItemCode`.
- `StockMovementService` kiểm tra `SlotLotId -> LotNo/ItemCode` trước `Export/Move`.
- Legacy Slot mutation logic đã gom vào `LegacyStockSlotRepositoryAdapter`.
- `IBulkStockSlotRepository` chỉ resolve/lock/query.
- Legacy NhapKho export/correction writer contracts đã được loại bỏ.

## 5. Thứ tự tiếp theo

```text
[ĐÃ LÀM]
Legacy writer cleanup
        |
        v
[TIẾP]
XuLyHangLoi composition root
        |
        v
DB business-key verification
        |
        v
Durable idempotency
        |
        v
Build + integration + concurrency tests
        |
        v
Static dependency scan
        |
        v
KhoCore physical ownership
        |
        v
Legacy cleanup / Phase 9
        |
        v
Phase 10 closeout
```

## 6. Kết luận hiện tại

**WMS refactor chưa hoàn thành toàn bộ.**

Central stock-write architecture đã ở trạng thái khá ổn và nhóm legacy writer nguy hiểm của NhapKho đã được loại bỏ. Phần còn lại là các gate xác minh quan trọng: composition root của XuLyHangLoi, durable idempotency, build/test/concurrency và sau đó mới chuyển sang physical ownership/legacy cleanup.

Repository hiện không có CI status cho commit `8b17d32ae6f4436e8209e83bedfe08991f20be38`, vì vậy chưa được phép kết luận build pass.
