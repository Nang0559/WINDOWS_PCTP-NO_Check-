# WMS_STOCK_STATIC_AUDIT

## Scope

Static audit of the WMS stock write boundary after the central movement migration.

The audit is intentionally schema/code grounded. A GitHub code-search miss is **not** treated as proof that a caller does not exist; repository-tree inspection is used together with targeted source review.

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

The legacy `IStockExportRepository` remains only as transitional storage infrastructure behind the central movement adapter/read operations; it is no longer the business service's stock-mutation boundary.

### GiaoHangKhach

`BulkStockAdjustService` routes A0 LOT removal through `IStockMovementService.Pick`.

`IBulkStockSlotRepository` is query/resolve/lock only. The former `SaveLots` and `UpdateSlotHeaderFromLots` mutation escape hatches have been removed.

### XuLyHangLoi

`ReworkStockService` routes Rework stock mutations through `IStockMovementService`:

- Rework export -> `Export`
- Rework OK receive -> `ReturnFromRework`
- Rework NG receive -> `Receive` with `REWORK_NG_RECEIVE`
- Rework cancel return -> `ReturnFromRework`

## Legacy STOCKTP writer cleanup

The following legacy business-facing write contracts have now been removed:

- `IStockTpRepository.XuatKhoThat(...)`
- `IStockTpRepository.DieuChinhSlConLai(...)`
- `IStockTpLookupService.DieuChinhSlConLai(...)`

Their SQL implementations were removed from `StockTpRepository` as well.

The remaining `IStockTpRepository` mutation methods are deliberately limited to receiving semantics:

- `InsertStockTp(...)`
- `UpdateStockTp(...)`

These remain behind `IStockReceivingRepository` because receiving has distinct `SLNHAP/SLCONLAI/STATUS` semantics.

`StockExportRepository` still contains:

- `DecreaseStockTp`
- `AdjustSlConLai`
- `TryDecreaseSlConLai`

These are transitional infrastructure behind `IStockBalanceRepository`; they are not exposed through the NhapKho business contract.

## Important source LOT invariant

For a physical `Export` using `SlotLotId`, the central movement service verifies that the requested `LotNo` and `ItemCode` match the actual LOT identity stored by the `SlotLotId` before decrementing STOCKTP.

The same source LOT identity validation is applied to `Move` when the request supplies source LOT/item information.

This prevents a caller from supplying one valid LOT together with another valid `SlotLotId` and corrupting STOCKTP versus physical SlotLot quantities.

## Composition root gate

A dedicated `XuLyHangLoiModuleFactory` now exists at:

`PCTP/Modules/XuLyHangLoi/Application/XuLyHangLoiModuleFactory.cs`

`WarehouseProcessNavigator.CreateFormQuanLyTienTrinhHangLoi(...)` now creates one `PhieuSqlExecutor` + `UnitOfWork` and passes that same graph into the factory. The resulting `ReworkStockService`, `QTChungService`, workflow repositories, and form therefore share the same UoW for the XuLyHangLoi workflow.

The factory owns construction of:

- `IStockMovementService`
- `ReworkStockService`
- `SlotService`
- `IStockExportRepository` transitional adapter source
- `StockHistoryRepository`
- `PhieuXuLyBatThuongRepository`
- `TraHangQTChungRepository`

The UI no longer constructs `ReworkStockService` directly in the navigator.

## Idempotency gate

`StockMovementRequest` contains `ReferenceType` and `ReferenceId`, but the current `StockHistory` persistence contract does not persist/query those fields. Therefore idempotency must **not** be added by pretending those request fields are already durable keys.

Before implementing idempotency, verify the real database schema and choose one of:

1. an existing workflow transaction key that is already unique/durable, or
2. a dedicated stock movement/idempotency table with a unique business key.

## Test gate

The central boundary and source LOT invariant are implemented, but integration/concurrency tests still need to be added and executed against the real .NET Framework 4.7.2 build environment.

## Audit status

- Central movement boundary: **confirmed**
- Bulk Slot mutation escape hatch: **removed**
- NhapKho central receiving wiring: **confirmed**
- XuatKho central movement wiring: **confirmed**
- Rework movement calls: **confirmed**
- Legacy NhapKho export/correction writer contracts: **removed**
- XuLyHangLoi composition root: **wired through WarehouseProcessNavigator**
- All legacy stock callers: **NOT YET PROVEN CLEAN** — Visual Studio compile remains the final caller gate
- Idempotency: **NOT YET IMPLEMENTED**
- Integration/concurrency tests: **NOT YET IMPLEMENTED**
