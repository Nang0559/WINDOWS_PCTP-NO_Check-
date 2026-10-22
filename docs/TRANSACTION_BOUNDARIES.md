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

## Customer-delivery transaction findings

The production path is composed through `GiaoHangKhachModuleFactory.Build()`: it creates one `phieuUow`, passes that same instance to `BulkStockSlotRepository`, `StockHistoryRepository`, `HangChoGiaoRepository`, and `PhieuRepository`; `PhieuRepository` delegates stock confirmation to `PhieuKhoRepository`. The changes below are in the repository actually reached by `PhieuKhoService`.

- **Stored-procedure paths (`CapNhapKho` / `CapNhapKhoSP`)**: begin `phieuUow` before executing the stock stored procedure and call the transaction-aware `SqlRepositoryBase.ExecuteStoredProcedureDataSet` helper. SlotLot deductions use `manageTransaction: false`, so the SP, every LOT adjustment, pending-delivery closure, and history writes share the same connection/transaction. The stock-changed notification is raised only after commit.
- **YMVN direct/manual path (`CapNhapKhoYMVN`)**: the available-stock check is performed inside the transaction with `UPDLOCK, HOLDLOCK`; FIFO deduction uses guarded updates (`SLCONLAI >= @sl`) and verifies exactly one affected row. SlotLot deduction, QR/history persistence, TMP status update, pending-delivery closure/history, and the configured order-table update execute before the same transaction commits. The stock-changed notification is raised only after commit.
- **Pending-delivery finalization (`HoanTatSauKhiTruKho`)**: no longer opens a nested transaction or suppresses exceptions. It requires the caller's active UoW and a configured `IHangChoGiaoRepository`; closure and associated history are executed through the active connection/transaction. Failure now propagates to the caller and triggers rollback.
- **Stored-procedure result validation**: when a stored procedure returns rows, the result must include valid `LOT` and positive `SOLUONG` values. Missing columns or malformed rows throw rather than silently skipping SlotLot adjustment.
- **YMVN LOT parsing**: malformed LOT/quantity tokens and an empty parsed list are rejected before any stock mutation; invalid tokens are no longer silently ignored.

These are source-level changes and transaction-composition checks. They are not yet proof of SQL rollback under injected failures, concurrent delivery, stored-procedure behavior, or duplicate/replayed delivery confirmation.

## Important limitations and follow-up

1. Unit tests with fake repositories prove validation and call ordering, not SQL rollback or locking behavior.
2. The current default CI does not provision a disposable SQL Server database. No scenario should be labelled SQL integration, deadlock, timeout, retry, or concurrent-write coverage until it runs against that isolated database.
3. `StockMovementService.Move` performs source decrease followed by target insertion. Its atomicity therefore depends on callers using the documented shared transaction. Before adding a new direct caller, verify that it begins the same UoW and rolls back when the movement result is unsuccessful.
4. `LegacyStockSlotRepositoryAdapter` delegates through `ISlotService`; transaction sharing depends on constructing that service with a repository bound to the same UoW, as the factories above currently do.
5. Delivery tests now additionally cover the production `BulkStockAdjustService` with fakes (`DEL-006`); this proves branch/ordering/transaction-callback behavior only. It does not execute the actual SQL repository graph. The required delivery integration scenarios are `DEL-003` (cross-repository rollback), `DEL-004` (duplicate/replayed confirmation), `DEL-005` (insufficient stock/shortfall), and `DEL-007` (stored-procedure/finalization transaction boundary). All remain pending; do not treat the factory's shared-UoW wiring as proof of workflow atomicity.
6. Source review found a concrete delivery consistency gap: stored-procedure routes mutate STOCKTP before the later independently managed SlotLot transaction; YMVN keeps STOCKTP and SlotLot together but performs pending-delivery closure/history finalization after commit. See the findings section above. These are production behavior risks, not merely missing tests.
5. A future SQL integration suite should deliberately fail after the first mutation (for example, after source decrease but before target insert) and assert that both source and target state are unchanged after rollback. It must use only a local database accepted by `TestEnvironmentPolicy.EnsureSafeSqlTestTarget`.

## Safety

This audit reads source code and records production source changes on the test branch. It does not open a database, execute SQL, or alter production data.
