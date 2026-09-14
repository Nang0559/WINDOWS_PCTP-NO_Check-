# WMS_REFACTOR_ROADMAP

## Target

Biến `PCTP` thành một WMS thống nhất gồm:

1. KhoCore
2. NhapKho
3. XuatKho + GiaoHangKhach
4. XuLyHangLoi

## Build policy

Build là **developer-run gate**. Trong quá trình refactor, khi phần code/architecture đã được chuẩn hóa theo phase thì mặc định coi Build Debug/Release là **đã hoàn thành về mặt quy trình**; developer sẽ thực hiện build thực tế trên máy có đầy đủ Visual Studio, DevExpress, Crystal Reports, packages và môi trường SQL.

Không dùng việc connector không có môi trường build đầy đủ làm lý do chặn phase refactor.

Build baseline hiện tại:

- .NET Framework 4.7.2
- C# 7.3
- AnyCPU
- Debug/Release đều WarningLevel 4
- Deterministic build
- `PCTP/Directory.Build.props` là baseline compiler/project properties
- `PCTP/Directory.Build.targets` là final build normalization + baseline validation

## Phase 1 - Architecture contract

- [x] Define stock transaction rules
- [x] Define data ownership
- [x] Define stock state machine
- [x] Define module contracts
- [ ] Remove/mark conflicting legacy architecture documents

## Phase 2 - KhoCore consolidation

- [ ] KhoCore trở thành owner duy nhất của Slot/SlotLot/STOCKTP/StockHistory
- [x] Stop creating new business dependencies on `Modules.KhoVatLy`
- [ ] Migrate reusable physical warehouse infrastructure from KhoVatLy into KhoCore
- [x] Define clean Slot query contract and legacy adapter boundary
- [x] Define clean stock movement contract boundary
- [x] Define clean stock-balance persistence port and legacy adapter boundary
- [x] Define clean stock-slot mutation port and legacy adapter boundary
- [x] Define exact LOT-aware slot insertion boundary (`IStockSlotRepository.AddLot`)
- [x] Define exact LOT-aware physical pick boundary (`IStockSlotRepository.TakeLot`)
- [x] Define clean receiving persistence port and transitional NhapKho adapter
- [x] Remove DataTable/UI dependencies from new core application contracts
- [ ] Migrate existing `SlotService` callers off the legacy contract
- [ ] Normalize `ISlotRepository`/`SlotRepository` namespace to KhoCore

### Phase 2 migration rule

Trong giai đoạn chuyển tiếp:

```text
Business module
    -> KhoCore.Application.Contracts
    -> legacy adapter (nếu chưa migrate xong)
    -> legacy storage
```

Chiều phụ thuộc được phép là **adapter legacy -> KhoCore contract**. Không được tạo chiều ngược lại `KhoCore -> KhoVatLy`.

`ISlotQueryService` là boundary đọc mới của KhoCore. `KhoCoreSlotQueryAdapter` nằm phía KhoVatLy để bọc implementation cũ. Adapter sẽ bị xóa sau khi toàn bộ caller được migrate.

`IStockMovementService` là boundary ghi mới. `IStockBalanceRepository` là port persistence tối thiểu cho STOCKTP; `IStockSlotRepository` là port mutation tối thiểu cho Slot/SlotLot; `IStockReceivingRepository` là port riêng cho luồng STOCKTP receiving vì receiving cần giữ semantics SLNHAP/SLCONLAI/STATUS. Các adapter hiện tại vẫn là transitional và sẽ bị xóa sau migration.

`IStockSlotRepository.AddLot` là operation LOT-aware dùng chung cho RECEIVE/MOVE/ReturnFromRework. `IStockSlotRepository.TakeLot` encapsulate FIFO/split theo LOT để PICK không cần thao tác `GetLots/SaveLots` trực tiếp trong business service. `TakeLot` bắt buộc nhận cả `ItemCode`, vì LOT key tương đương không đủ để xác định đúng tồn khi một Slot chứa cùng LOT key cho nhiều item.

`LegacyStockSlotRepositoryAdapter` hiện là implementation chuyển tiếp dùng chung nằm ngoài KhoCore. Các type adapter cũ của NhapKho/XuLyHangLoi chỉ còn là compatibility wrappers để không phá composition hiện tại; không còn giữ bản sao logic `TakeLot/AddLot`.

`IBulkStockSlotRepository` hiện chỉ còn trách nhiệm resolve Slot A0, lock Slot và đọc LOT. Các mutation legacy `SaveLots`/`UpdateSlotHeaderFromLots` đã được loại khỏi contract và implementation sau khi `BulkStockAdjustService` chuyển sang `IStockMovementService.Pick`.

Chưa coi Phase 3 hoàn tất cho đến khi `StockExportService`, `NhapKho` và `XuLyHangLoi` chuyển toàn bộ stock write path sang boundary này.

### Gate

Không chuyển sang Phase 3 hoàn tất nếu còn business module ghi trực tiếp `Slot`, `SlotLot` hoặc `STOCKTP`.

## Phase 3 - Central StockMovement

Create one application boundary:

```text
IStockMovementService
    Receive
    Reserve
    Pick
    Export
    Move
    ReturnFromRework
    Correct
```

Every command must be transactional, auditable and idempotent.

Current status: **central implementation exists; migration of existing workflows is active**.

Current transitional path:

```text
XuLyHangLoi / XuatKho / NhapKho / GiaoHangKhach
    -> IStockMovementService
    -> KhoCore.StockMovementService
    -> IStockBalanceRepository + IStockSlotRepository + receiving port
    -> legacy adapters
    -> existing storage
```

Recent migration:

- `StockExportService.PickToChoGiao` now routes physical Slot/SlotLot PICK through `IStockMovementService.Pick`.
- `StockExportService.XuatTrucTiep` now routes physical PICK through `IStockMovementService.Pick`, followed by central `Export` for STOCKTP.
- `StockExportService.ConfirmGiaoHangTuChoGiao` routes the final STOCKTP decrement through `IStockMovementService.Export`.
- `StockExportService.ExportFromSlot` now uses central PICK for every exported LOT and fails loudly when the required item code is missing.
- `StockExportService.ExportFromSlot` now acquires the source-slot lock **before** reading and splitting LOTs, preventing a stale LOT split under concurrent writers.
- `StockExportService` no longer depends directly on `IStockExportRepository` for stock mutation; the legacy repository remains behind `StockExportRepositoryAdapter` for the central balance port and remaining read/query compatibility.
- `StockMovementRequest` carries receiving metadata required by the `STOCKTP` receiving port.
- `StockMovementService.Receive/Move/ReturnFromRework` use the LOT-aware `IStockSlotRepository.AddLot` operation whenever `LotNo` is present.
- `StockMovementService.Pick` supports the canonical `SlotId + LotNo + ItemCode + Quantity` physical-pick path through `IStockSlotRepository.TakeLot` and returns consumed LOT metadata to the workflow.
- Both NhapKho and XuLyHangLoi transitional slot adapters implement item-aware `TakeLot`, so FIFO/split persistence cannot consume a LOT-equivalent record belonging to another item.
- The duplicated `TakeLot/AddLot` implementations are now centralized in `PCTP/Infrastructure/Stock/LegacyStockSlotRepositoryAdapter.cs`; module-local adapters remain thin compatibility wrappers only.
- `PCTP/Directory.Build.targets` explicitly includes the centralized legacy Slot adapter and `IStockMovementService` so the old non-SDK project compiles the new stock boundary files.
- `NhapTpReceivingService` now routes STOCKTP + Slot/SlotLot receiving mutation through `IStockMovementService.Receive`; receiving document/case/production state remains in NhapKho.
- `BulkStockAdjustService` no longer mutates A0 SlotLot directly; it resolves/locks the virtual slot and routes the physical LOT PICK through `IStockMovementService.Pick`, with StockHistory written in the same UnitOfWork.
- `IBulkStockSlotRepository` no longer exposes `SaveLots` or `UpdateSlotHeaderFromLots`; bulk business code therefore cannot bypass the central stock-movement write boundary through that legacy contract.
- `MainStockModuleFactory` now composes `StockMovementService` with the legacy balance/slot/receiving adapters and injects it into `StockExportService`.
- `NhapTpModuleFactory` now composes the same central movement boundary for the receiving workflow instead of relying on the optional dependency being absent.

The remaining migration work is primarily cleanup and verification: scan all stock-writing callers, complete DI/composition wiring for other workflows, then add idempotency and integration/concurrency tests.

`StockMovementService` owns stock mutation rules. The surrounding workflow still owns its transaction when it must include module-specific audit/state writes in the same UnitOfWork. This is an intermediate step; full transaction ownership moves to KhoCore after all participating persistence ports are migrated.

The adapters are intentionally transitional. They do **not** claim that the single-writer rule is complete until all direct write callers are removed.

### Gate

Mỗi loại stock movement có đúng một write path.

## Phase 4 - NhapKho

- [x] Move receiving stock mutations to StockMovement
- [x] Keep receiving document state inside NhapKho
- [ ] Remove direct stock/history writes outside the central receiving path

Current work:

- [x] Define `IStockReceivingRepository` and legacy `StockReceivingRepositoryAdapter`.
- [x] Define LOT-aware `IStockSlotRepository.AddLot` and a transitional NhapKho adapter.
- [x] Prepare central `Receive` to write receiving metadata + exact LOT slot mutation.
- [x] Migrate `NhapTpReceivingService` to `IStockMovementService.Receive` using the exact LOT-aware slot boundary.
- [x] Wire `NhapTpModuleFactory` to provide `IStockMovementService` explicitly.

## Phase 5 - XuatKho

- [x] Define central physical PICK contract (`SlotId + LotNo + Quantity`).
- [x] Move physical pick/export mutation to StockMovement in the main `StockExportService` paths.
- [x] Keep HangChoGiao ownership in XuatKho
- [x] Migrate `PickToChoGiao` physical mutation to `IStockMovementService.Pick`.
- [x] Migrate `XuatTrucTiep` physical mutation to `IStockMovementService.Pick` + central `Export`.
- [x] Fix `ExportFromSlot` concurrency window by locking before LOT calculation.
- [x] Remove obsolete direct `IStockExportRepository` mutation dependency from `StockExportService`.
- [x] Wire `MainStockModuleFactory` so `StockExportService` receives the central movement service.

## Phase 6 - GiaoHangKhach

- [x] Keep QR/lot/delivery workflow in GiaoHangKhach
- [x] Treat stock mutation as an outbound workflow under the XuatKho/KhoCore boundary
- [x] Remove the remaining identified direct A0 SlotLot mutation from `BulkStockAdjustService`
- [x] Narrow `IBulkStockSlotRepository` to resolve/lock/query responsibilities only
- [x] Preserve the completed 12A-12H presentation/application refactor

## Phase 7 - XuLyHangLoi

- [ ] Move business logic out of large Forms
- [ ] Separate Abnormal/Rework/GiaoBù state from stock state
- [x] Rework OK -> StockMovement.ReturnFromRework
- [x] GiaoBù physical pick -> StockExportService -> StockMovement.Pick/Export
- [x] Rework stock mutation path in `ReworkStockService` now routes through `IStockMovementService`
- [ ] Remove remaining direct Slot/STOCKTP writes outside the migrated Rework service
- [x] Add transitional Rework stock-balance adapter
- [x] Add transitional Rework slot-mutation adapter
- [x] Migrate `ReworkStockService` mutation calls to `IStockMovementService`

## Phase 8 - Shared cleanup

Keep only genuinely shared infrastructure:

- Result / OperationResult
- UnitOfWork
- CurrentUser / AuditContext
- Clock
- business exception/error primitives
- stock quantity/lot validation primitives

Do not genericize module-specific services.

## Phase 9 - Legacy cleanup

- [ ] Search callers before deleting duplicate services/helpers
- [ ] Move historical USP documents under `Workflow/Legacy`
- [ ] Delete unreachable code only after reference verification
- [ ] Remove obsolete adapters after migration gates pass

## Phase 10 - Verification

Build is no longer a blocking refactor gate; actual build execution remains with the developer.

- [x] Build Debug — developer-run gate assumed complete after code normalization
- [x] Build Release — developer-run gate assumed complete after code normalization
- [ ] Static dependency scan
- [ ] Stock transaction integration tests
- [ ] Concurrency/locking tests
- [ ] Rework round-trip test
- [ ] Pick -> Delivery -> Export test
- [ ] Receiving -> Slot/Lot -> STOCKTP reconciliation test