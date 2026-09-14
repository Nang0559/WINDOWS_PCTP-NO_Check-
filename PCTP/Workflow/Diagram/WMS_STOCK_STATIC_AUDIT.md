# WMS_STOCK_STATIC_AUDIT

## Scope

Static audit of the WMS stock write boundary after commit `19f3c80d31dab34c6bb7d50feae9e387dab8d1cc`.

The audit is intentionally schema/code grounded. A GitHub code-search miss is **not** treated as proof that a caller does not exist.

## Confirmed central write boundary

`StockMovementService` is the current central application write boundary for:

- `Receive`
- `Pick`
- `Export`
- `Move`
- `ReturnFromRework`
- `Correct`

The service delegates persistence through:

```text
IStockMovementService
    -> IStockBalanceRepository
    -> IStockSlotRepository
    -> IStockReceivingRepository
```

The current implementation is at:

`PCTP/Modules/KhoCore/Application/Services/StockMovementService.cs`

## Confirmed migrated workflows

### NhapKho

`NhapTpReceivingService` uses `IStockMovementService.Receive` for the combined STOCKTP + Slot/SlotLot receiving mutation. Receiving document/case/production state remains in NhapKho.

Composition is explicitly wired by `NhapTpModuleFactory`.

### XuatKho

`StockExportService` routes physical PICK/EXPORT mutations through `IStockMovementService`.

The legacy `IStockExportRepository` remains as a transitional storage adapter/read dependency; it is no longer the business service's stock-mutation boundary.

### GiaoHangKhach

`BulkStockAdjustService` routes A0 LOT removal through `IStockMovementService.Pick`.

`IBulkStockSlotRepository` is query/resolve/lock only. The former `SaveLots` and `UpdateSlotHeaderFromLots` mutation escape hatches have been removed.

### XuLyHangLoi

`ReworkStockService` routes Rework stock mutations through `IStockMovementService`:

- Rework export -> `Export`
- Rework OK receive -> `ReturnFromRework`
- Rework NG receive -> `Receive` with `REWORK_NG_RECEIVE`
- Rework cancel return -> `ReturnFromRework`

## Remaining legacy write surfaces requiring caller verification

### `IStockTpRepository`

`PCTP/Modules/NhapKho/Interfaces/IStockTpRepository.cs` still exposes these legacy mutation methods:

- `InsertStockTp(...)`
- `UpdateStockTp(...)`
- `XuatKhoThat(...)`
- `DieuChinhSlConLai(...)`

`InsertStockTp` and `UpdateStockTp` are intentionally retained behind `IStockReceivingRepository` during the transition because STOCKTP receiving has its own `SLNHAP/SLCONLAI/STATUS` semantics.

`XuatKhoThat` and `DieuChinhSlConLai` are higher-risk legacy escape hatches. They should not be deleted until all callers have been verified and migrated to `IStockMovementService.Export/Correct`.

### `StockExportRepository`

`StockExportRepository` still contains the physical SQL implementation for the transitional `IStockBalanceRepository` adapter:

- `DecreaseStockTp`
- `AdjustSlConLai`
- `TryDecreaseSlConLai`

This is expected transitional infrastructure, not a business-module write path.

## Important invariant identified for next code pass

For a physical `Export` using `SlotLotId`, the central movement service must verify that the requested `LotNo` matches the actual LOT stored by the `SlotLotId` before decrementing STOCKTP.

Otherwise a caller supplying a valid `SlotLotId` together with a different valid `LotNo` could remove quantity from one physical LOT while decrementing another STOCKTP LOT.

The same source LOT consistency check should be applied to `Move` when the request supplies `LotNo`.

## Composition root gate

`ReworkStockService` now requires `IStockMovementService`, but the repository tree does not yet expose a dedicated XuLyHangLoi composition factory comparable to `MainStockModuleFactory` / `NhapTpModuleFactory`.

The remaining task is therefore to locate the actual form/service composition path and ensure the central movement service is constructed once per workflow transaction boundary rather than being instantiated ad hoc by presentation code.

## Idempotency gate

`StockMovementRequest` contains `ReferenceType` and `ReferenceId`, but the current `StockHistory` persistence contract does not persist/query those fields. Therefore idempotency must **not** be added by pretending those request fields are already durable keys.

Before implementing idempotency, verify the real database schema and choose one of:

1. an existing workflow transaction key that is already unique/durable, or
2. a dedicated stock movement/idempotency table with a unique business key.

## Audit status

- Central movement boundary: **confirmed**
- Bulk Slot mutation escape hatch: **removed**
- NhapKho central receiving wiring: **confirmed**
- XuatKho central movement wiring: **confirmed**
- Rework movement calls: **confirmed**
- All legacy stock callers: **NOT YET PROVEN CLEAN**
- XuLyHangLoi composition root: **NOT YET VERIFIED**
- Idempotency: **NOT YET IMPLEMENTED**
- Integration/concurrency tests: **NOT YET IMPLEMENTED**
