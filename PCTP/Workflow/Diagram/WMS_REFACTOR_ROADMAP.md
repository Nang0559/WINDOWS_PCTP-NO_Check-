# WMS_REFACTOR_ROADMAP

## Target

Biến `PCTP` thành một WMS thống nhất gồm:

1. KhoCore
2. NhapKho
3. XuatKho + GiaoHangKhach
4. XuLyHangLoi

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

- [ ] Build Debug
- [ ] Build Release
- [ ] Static dependency scan
- [ ] Stock transaction integration tests
- [ ] Concurrency/locking tests
- [ ] Rework round-trip test
- [ ] Pick -> Delivery -> Export test
- [ ] Receiving -> Slot/Lot -> STOCKTP reconciliation test

No phase is marked complete based on code inspection alone; build/runtime evidence is required where applicable.
