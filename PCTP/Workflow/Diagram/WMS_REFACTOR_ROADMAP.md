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

`IStockMovementService` là boundary ghi mới. `IStockBalanceRepository` là port persistence tối thiểu cho STOCKTP; `IStockSlotRepository` là port mutation tối thiểu cho Slot/SlotLot. Các adapter hiện tại vẫn là transitional và sẽ bị xóa sau migration.

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
XuLyHangLoi / XuatKho / NhapKho
    -> IStockMovementService
    -> KhoCore.StockMovementService
    -> IStockBalanceRepository + IStockSlotRepository
    -> legacy adapters
    -> existing storage
```

`StockMovementService` owns stock mutation rules. The surrounding workflow still owns its transaction when it must include module-specific audit/state writes in the same UnitOfWork. This is an intermediate step; full transaction ownership moves to KhoCore after all participating persistence ports are migrated.

The adapters are intentionally transitional. They do **not** claim that the single-writer rule is complete until all direct write callers are removed.

### Gate

Mỗi loại stock movement có đúng một write path.

## Phase 4 - NhapKho

- [ ] Move receiving stock mutations to StockMovement
- [ ] Keep receiving document state inside NhapKho
- [ ] Remove direct stock/history writes

## Phase 5 - XuatKho

- [ ] Separate Pick from Export explicitly
- [ ] Move stock mutation to StockMovement
- [ ] Keep HangChoGiao ownership in XuatKho

## Phase 6 - GiaoHangKhach

- [ ] Keep QR/lot/delivery workflow in GiaoHangKhach
- [ ] Treat it as outbound workflow under XuatKho boundary
- [ ] Remove any remaining direct stock mutation
- [ ] Preserve the completed 12A-12H presentation/application refactor

## Phase 7 - XuLyHangLoi

- [ ] Move business logic out of large Forms
- [ ] Separate Abnormal/Rework/GiaoBù state from stock state
- [x] Rework OK -> StockMovement.ReturnFromRework
- [ ] GiaoBù -> StockMovement.Pick/Export according to actual physical flow
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

Runtime/integration evidence is still required before production release, but absence of connector-side build execution does not block architectural phases.
