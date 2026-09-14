# WMS OVERALL STATUS

> Snapshot after commit `112a44f2980b406a3df1abbac445a1376287b9d8`.

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
| Phase 3 - Central StockMovement | 🟢 92% | Central boundary, source LOT invariant, NhapKho writer cleanup và XuLy composition đã làm; generic idempotency + tests còn thiếu. |
| Phase 4 - NhapKho | 🟢 95% | Receiving mutation qua StockMovement; legacy export/correction escape hatches đã loại khỏi NhapKho contract. |
| Phase 5 - XuatKho | 🟢 98% | PICK/EXPORT qua StockMovement; workflow-level idempotency đã thêm cho PickToChoGiao/XuatTrucTiep. |
| Phase 6 - GiaoHangKhach | 🟢 100% | 12A-12H hoàn tất; BulkStockAdjust không còn direct A0 SlotLot mutation. |
| Phase 7 - XuLyHangLoi | 🟢 90% | Rework mutation qua StockMovement; composition root + shared UoW đã wired. |
| Phase 8 - Shared cleanup | 🔴 Chưa đóng | Chưa ưu tiên trước verification gate. |
| Phase 9 - Legacy cleanup | 🟡 30% | Transitional adapters vẫn còn. |
| Phase 10 - Verification | 🟡 35% | Static review tiến thêm; build/integration/concurrency tests còn thiếu. |

## 3. Gate đã đóng

### Legacy writer cleanup

Đã loại khỏi business-facing NhapKho contracts:

- `IStockTpRepository.XuatKhoThat(...)`
- `IStockTpRepository.DieuChinhSlConLai(...)`
- `IStockTpLookupService.DieuChinhSlConLai(...)`

### XuLyHangLoi composition root

`XuLyHangLoiModuleFactory` là composition root. `WarehouseProcessNavigator` tạo một `PhieuSqlExecutor` + `UnitOfWork` cho workflow và truyền cùng graph vào `XuLyHangLoiModuleFactory`, `GiaoBuNGService`, `StockExportService`.

### XuatKho workflow idempotency

XuatKho đã có durable reference trong `StockHistory.MaPhieu`. `StockExportService` kiểm tra `ActionType + MaPhieu` trước physical mutation cho `PickToChoGiao` và `XuatTrucTiep`.

`StockExportReferenceKey` đã được sửa để dùng cùng formatter với dữ liệu được persist (`PGH#id`, `CGB#id`, `XLBT#id`, `KTR#id`).

`ConfirmGiaoHangTuChoGiao` tiếp tục được bảo vệ bởi trạng thái `HangChoGiao` + `GetForUpdate`.

## 4. Chưa đóng

### Generic central idempotency

`StockMovementRequest.ReferenceType/ReferenceId` chưa được persist trong generic `StockHistory` contract. Vì vậy chưa được phép tuyên bố `IStockMovementService` có idempotency tổng quát.

Các workflow NhapKho/Rework/GiaoHangKhach nếu cần retry-safe central operation phải có durable business key riêng hoặc bảng movement/idempotency riêng.

### Verification / tests

Cần chạy trong Visual Studio / môi trường .NET Framework 4.7.2:

- build toàn solution
- duplicate XuatKho reference
- SlotLotId mismatch không được mutate
- concurrent Pick/Export
- Rework round-trip
- Receive -> Slot/Lot -> STOCKTP reconciliation
- static dependency scan toàn repository

## 5. Thứ tự tiếp theo

```text
[ĐÃ LÀM]
XuLyHangLoi composition root
        |
        v
[ĐÃ LÀM]
XuatKho workflow idempotency
        |
        v
[TIẾP]
Generic business-key verification cho NhapKho/Rework/GiaoHangKhach
        |
        v
Integration + concurrency tests
        |
        v
Static dependency scan / legacy caller proof
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

**WMS refactor chưa hoàn thành toàn bộ.** Central stock-write architecture, source LOT invariant, XuLyHangLoi composition và XuatKho workflow-level idempotency đã được xử lý. Phần còn lại là generic idempotency cho các workflow chưa có durable key, integration/concurrency verification, static caller proof và sau đó mới chuyển sang physical ownership/legacy cleanup.

Không có CI/build status cho các commit vừa thực hiện, vì vậy chưa kết luận build pass.
