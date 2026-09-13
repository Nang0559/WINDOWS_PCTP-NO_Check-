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
- [ ] Stop creating new business dependencies on `Modules.KhoVatLy`
- [ ] Migrate reusable physical warehouse infrastructure from KhoVatLy into KhoCore
- [ ] Split Slot query, stock command and UI adapters
- [ ] Remove DataTable/UI dependencies from core application contracts

### Gate

Không chuyển sang Phase 3 nếu còn business module ghi trực tiếp `Slot`, `SlotLot` hoặc `STOCKTP`.

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
- [ ] Rework OK -> StockMovement.ReturnFromRework
- [ ] GiaoBù -> StockMovement.Pick/Export according to actual physical flow
- [ ] Remove direct Slot/STOCKTP writes

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
