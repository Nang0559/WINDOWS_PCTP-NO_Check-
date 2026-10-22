# Stock mutation transaction-boundary audit

Audit target: `feature/pctp-qc-test-platform`

This document records a source-level review of the current stock-mutation composition. It is not a SQL integration-test result.

## Verified composition

| Workflow | UnitOfWork composition | Stock mutation participants |
|---|---|---|
| Main stock / physical warehouse | `MainStockModuleFactory.Build()` creates one `UnitOfWork` | `StockExportRepository`, `SlotRepository` through `SlotService`, `StockTpRepository`, history and delivery repositories receive that same instance |
| Finished-goods receiving | `NhapTpModuleFactory.Build()` creates one `UnitOfWork` | `StockTpRepository`, `SlotRepository` through `SlotService`, tracking, import guard, status and history repositories receive that same instance |
| Rework / QC | `XuLyHangLoiModuleFactory.Build()` creates one `UnitOfWork`; the overload also accepts the workflow-owned instance | Slot, STOCKTP, history, rework/QT and abnormal-ticket repositories are composed with that instance |
| Customer delivery | `GiaoHangKhachModuleFactory.Build()` creates `phieuUow` | `BulkStockSlotRepository`, `StockHistoryRepository`, `HangChoGiaoRepository`, and `LegacyStockBalanceRepositoryAdapter` share the same executor/UoW |

The stock-slot adapter delegates to the workflow's `SlotService`; the balance adapters delegate to repositories constructed with the same workflow UoW. This is the intended path for SQL mutations to participate in one transaction. This row verifies factory composition only; it is not evidence that the complete customer-delivery operation has been executed or rolled back in a SQL integration test.

## Verified workflow ownership

- `NhapTpReceivingService.NhapTpVaoSlot` begins the UoW before the in-transaction duplicate-QR check and stock movement; it commits on success and rolls back on failure.
- `StockExportService` begins a transaction around pick/export, pending-delivery records, history, and delivery confirmation. A failed stock movement causes rollback; exception paths use `SafeRollback`.
- `ReworkStockService` begins a transaction around rework stock movements and associated QT/history writes; failed movements use `FailAndRollback` or the exception rollback path.
- `StockMovementService` intentionally does not begin/commit a transaction for ordinary Receive/Export/Move/Pick operations. Its class contract assigns transaction ownership to the calling workflow. `Correct` is an exception: it requires an injected UoW and owns its local transaction.

## Important limitations and follow-up

1. Unit tests with fake repositories prove validation and call ordering, not SQL rollback or locking behavior.
2. The current default CI does not provision a disposable SQL Server database. No scenario should be labelled SQL integration, deadlock, timeout, retry, or concurrent-write coverage until it runs against that isolated database.
3. `StockMovementService.Move` performs source decrease followed by target insertion. Its atomicity therefore depends on callers using the documented shared transaction. Before adding a new direct caller, verify that it begins the same UoW and rolls back when the movement result is unsuccessful.
4. `LegacyStockSlotRepositoryAdapter` delegates through `ISlotService`; transaction sharing depends on constructing that service with a repository bound to the same UoW, as the factories above currently do.
5. Delivery test coverage is currently limited to in-memory `FifoSessionState` and YMVN Gear validation. It does not execute `PhieuRepository`/`BulkStockAdjustService` with `BulkStockSlotRepository`, `StockHistoryRepository`, `HangChoGiaoRepository`, and `LegacyStockBalanceRepositoryAdapter` in one transaction. The required delivery integration scenarios are `DEL-003` (cross-repository rollback), `DEL-004` (duplicate/replayed confirmation), and `DEL-005` (insufficient stock/shortfall). All remain pending; do not treat the factory's shared-UoW wiring as proof of workflow atomicity.
5. A future SQL integration suite should deliberately fail after the first mutation (for example, after source decrease but before target insert) and assert that both source and target state are unchanged after rollback. It must use only a local database accepted by `TestEnvironmentPolicy.EnsureSafeSqlTestTarget`.

## Safety

This audit only reads source code. It does not open a database, execute SQL, alter production data, or change production source files.
