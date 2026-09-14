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

## Confirmed migrated workflows

### NhapKho

`NhapTpReceivingService` uses `IStockMovementService.Receive` for the combined STOCKTP + Slot/SlotLot receiving mutation. Receiving document/case/production state remains in NhapKho.

### XuatKho

`StockExportService` routes physical PICK/EXPORT mutations through `IStockMovementService`.

The legacy `IStockExportRepository` remains transitional storage infrastructure behind central movement/read adapters; it is no longer the business service's stock-mutation boundary.

### GiaoHangKhach

`BulkStockAdjustService` routes A0 LOT removal through `IStockMovementService.Pick`.

`IBulkStockSlotRepository` is resolve/lock/query only. The former `SaveLots` and `UpdateSlotHeaderFromLots` mutation escape hatches have been removed.

### XuLyHangLoi

`ReworkStockService` routes Rework stock mutations through `IStockMovementService`:

- Rework export -> `Export`
- Rework OK receive -> `ReturnFromRework`
- Rework NG receive -> `Receive` with `REWORK_NG_RECEIVE`
- Rework cancel return -> `ReturnFromRework`

## Legacy STOCKTP writer cleanup

The following legacy business-facing write contracts have been removed:

- `IStockTpRepository.XuatKhoThat(...)`
- `IStockTpRepository.DieuChinhSlConLai(...)`
- `IStockTpLookupService.DieuChinhSlConLai(...)`

Their SQL implementations were removed from `StockTpRepository` as well.

The remaining `IStockTpRepository` mutation methods are deliberately limited to receiving semantics:

- `InsertStockTp(...)`
- `UpdateStockTp(...)`

These remain behind `IStockReceivingRepository` because receiving has distinct `SLNHAP/SLCONLAI/STATUS` semantics.

`StockExportRepository` still contains `DecreaseStockTp`, `AdjustSlConLai`, and `TryDecreaseSlConLai` as transitional infrastructure behind `IStockBalanceRepository`.

## Source LOT invariant

For a physical `Export` using `SlotLotId`, the central movement service verifies that the requested `LotNo` and `ItemCode` match the actual LOT identity stored by the `SlotLotId` before decrementing STOCKTP.

The same source LOT identity validation is applied to `Move`.

## XuLyHangLoi composition root

`XuLyHangLoiModuleFactory` is now the composition root for the stock graph. `WarehouseProcessNavigator.CreateFormQuanLyTienTrinhHangLoi(...)` creates one `PhieuSqlExecutor` + `UnitOfWork` and passes them into the factory. `GiaoBuNGService` and `StockExportService` reuse the same UoW and the same `IStockMovementService`.

The stale `StockExportService` construction path was corrected so it no longer creates a separate stock graph.

## Export idempotency gate

A durable business key already exists for XuatKho history through `StockHistory.MaPhieu`:

- `PGH#{id}`
- `CGB#{id}`
- `XLBT#{id}`
- `KTR#{id}`

`IStockExportHistoryRepository.ExistsHistoryForReference(...)` queries `StockHistory` by `ActionType + MaPhieu`.

`StockExportService` now checks this key before physical mutation for:

- `PickToChoGiao`
- `XuatTrucTiep`

`StockExportReferenceKey` was also corrected to use the same formatter as persistence; the previous `1#123` vs `PGH#123` mismatch would have made the lookup ineffective.

`ConfirmGiaoHangTuChoGiao` already has a second idempotency/state guard through `HangChoGiao.TrangThai == ChoGiao` and `GetForUpdate`.

This is **workflow-level idempotency for XuatKho**, not yet a universal `IStockMovementService` idempotency mechanism for every module.

## Remaining idempotency limitation

`StockMovementRequest.ReferenceType/ReferenceId` are still not persisted by the generic `IStockHistoryRepository`. Therefore generic central movement idempotency should not be claimed complete. If NhapKho/Rework/GiaoHangKhach require retry-safe central operations, they need their own verified durable business key or a dedicated movement/idempotency table.

## Test gate

Integration/concurrency tests still need to be added and executed against the real .NET Framework 4.7.2 environment. In particular:

- duplicate XuatKho reference does not mutate stock twice
- SlotLotId identity mismatch does not mutate either side
- concurrent Pick/Export respects row locks
- Rework round-trip remains balanced
- Receive -> Slot/Lot -> STOCKTP reconciliation remains consistent

## Audit status

- Central movement boundary: **confirmed**
- Bulk Slot mutation escape hatch: **removed**
- NhapKho central receiving wiring: **confirmed**
- XuatKho central movement wiring: **confirmed**
- Rework movement calls: **confirmed**
- Legacy NhapKho export/correction writer contracts: **removed**
- XuLyHangLoi composition root: **wired and shared-UoW graph confirmed by source review**
- XuatKho workflow idempotency: **implemented**
- Generic stock-movement idempotency: **NOT YET IMPLEMENTED**
- All legacy stock callers: **NOT YET PROVEN CLEAN** — Visual Studio compile remains the final caller gate
- Integration/concurrency tests: **NOT YET IMPLEMENTED**
