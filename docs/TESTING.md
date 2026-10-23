# PCTP QC Test Platform

## Architecture

- **PCTP.QC.Tests** — .NET 8 xUnit suite. Pure unit tests link selected production source files so the test executes the same implementation without loading the legacy WinForms application.
- **PCTP.QC.TestRunner** — Windows Forms desktop launcher for the test suite. It runs `dotnet test` and streams output; it does not connect to SQL Server or mutate business data.
- **TestEnvironmentPolicy** — fail-closed policy for future SQL integration/performance fixtures. Only local SQL Server targets with database names `TEST_*`, `*_TEST`, or `*_TESTING` are accepted.
- **.github/workflows/pctp-qc-tests.yml** — CI restore, test, and desktop runner build.

The production application remains .NET Framework 4.7.2 / WinForms. The test harness targets .NET 8 to use a supported modern test SDK and runs on Windows CI. Existing application references, DevExpress assemblies, ClickOnce settings, SQL scripts, and production projects are not changed.

## Safety boundaries

1. No production connection string is stored in this repository.
2. No SQL migration, seed, truncate, delete, or update script is executed by the test suite.
3. Unit/regression tests run in-memory and do not require a database.
4. Future SQL tests must call `TestEnvironmentPolicy.EnsureSafeSqlTestTarget` before opening a connection. Remote hosts and database names without an explicit test suffix are rejected.
5. Performance tests must use a disposable local test database and synthetic data only. They are not part of the default CI suite until a dedicated isolated environment is provisioned.

## Current executable coverage

| Area | Current tests | Type |
|---|---|---|
| Customer delivery FIFO | in-memory ordering, compound LOT quantity matching, rejected scan state, rescan, release, reset | Unit/regression only; not persisted delivery |
| Customer delivery bulk adjustment | A0-first then physical-slot fallback, shortfall handling, caller-owned transaction failure, owned transaction commit/rollback | Unit tests with repository/movement fakes; SQL workflow still pending |
| QC Final domain rules | initial QC OK/NG reconciliation, rework OK/NG reconciliation, final scrap disposition, compensation separation, affected LOT non-negative quantities | Unit/domain |
| YMVN delivery validation | LOT character 13 → Gear, order quantity limit, missing Gear, completion totals, unexpected Gear, empty order | Unit/regression |
| Stock movement (KhoCore) | normal receiving, NG quarantine status, successful/blocked export, quantity guards, LOT identity, full/partial move QR rules, restore-picked, correction transaction guard | Unit/regression |
| Order-table line identity (`OrderLineKey`) | PO + part + delivery date + hour:minute as one key; same PO/part/date at different times are different lines; blank PO/part and group-hour values rejected (`DEL-009`) | Unit/regression |
| Saved-LOT merge (`OrderLotMerger`) | LOT bound to PO + part + hour:minute; no cross-PO or same-hour cross-minute assignment; legacy rows only when unambiguous (`DEL-011`) | Unit/regression |
| Order-table delivery marking SQL (`OrderTableSql`) | UPDATE filters PO + part + date + hour + minute and `IsDelivered = 0`; unsafe table names rejected (`DEL-010`, SQL text only) | Unit/regression |\n| Order-table source/finalization contracts | Source rows are loaded without `SUM`/`GROUP BY`; stock SP does not duplicate order-state updates; `PhieuKhoRepository` builds the full key (including `GIOGIAO` fallback) and calls `OrderTableSql.MarkDelivered` (`DEL-012`) | Source-contract regression; does not execute SQL Server |
| Environment safety | local test database allow-list; production/remote rejection | Unit/security |\n| Delivery reliability SQL contracts | replay with unique key; insufficient stock; post-write rollback; deadlock victim rollback; cancellation rollback (`SQL-001`, `SQL-003`, `SQL-004`) | Opt-in SQL Server synthetic fixtures; not production workflow proof |\n| Production same-LOT concurrency | two independent UnitOfWork instances compete through production `BulkStockSlotRepository.TakeLot`; one winner, no oversell, Slot header reconciled (`DEL-013`) | Opt-in SQL Server production repository integration |

## Order line identity

A line in the customer order table (`Purchase_Order_YMVN`, `Purchase_Order_HTN`, ...) is identified by **PO + part number + delivery date + delivery hour:minute**. PO + date + part is not unique: the same PO and part can be delivered at several times on the same day. `OrderLineKey` is the single definition of this key; `PhieuKhoRepository.DanhDauDaGiao` (marking a line delivered) and `OrderLotMerger` (binding saved LOTs to order rows) use it instead of ad-hoc string keys. Time precision is the minute. `TableOrderRepo.LoadPhieuTuBangRieng` preserves each source record and must not aggregate quantities with `SUM` or collapse records with `GROUP BY`. If an order row cannot be located by its full key, nothing is marked: leaving a line undelivered is visible to the operator, marking another delivery time as delivered is not.

## Order-table row preservation and SQL contract\n\n`OrderTableSourceContractTests` reads the linked production source files copied into the test output. It guards that `LoadPhieuTuBangRieng` selects source `Slgiao`, `Part_name`, and `NgayGiao` directly without `SUM`/`MAX`/`GROUP BY`; the stock procedure does not perform a second order-state update; and `PhieuKhoRepository` builds the full `OrderLineKey` from TMP (including the `GIOGIAO` fallback) before calling `OrderTableSql.MarkDelivered`. This avoids maintaining two competing interpretations of delivery time in SQL and C#. These are source-contract tests only: they do not parse or execute T-SQL, validate deployed schema, or prove the stored procedure transaction. The real SQL fixture for `OrderTableSql.MarkDelivered` remains opt-in and is not counted as executed until run against a disposable SQL Server.\n\n## Coverage evidence integrity

Every scenario marked `implemented` in `TestMatrix.json` must include a non-empty `testEvidence` entry naming its executable xUnit class/test. `TestMatrixIntegrityTests` enforces this rule and verifies that production database access, production data mutation, and automatic SQL script execution remain disabled. A scenario without executable evidence must remain `planned`.

The evidence check proves the matrix is traceable to test code; it does not convert unit tests into SQL integration or UI end-to-end tests. Those require separate disposable-environment evidence.

## SQL Server integration harness

See [SQL_INTEGRATION.md](SQL_INTEGRATION.md) for the opt-in SQL Server harness. It exercises real SQL rollback, concurrent conditional decrement, command timeout, and the production `StockTpRepository` and `BulkStockSlotRepository` against isolated fixtures. CI only builds the harness; it does not connect to SQL or claim these scenarios passed. Customer-delivery unit coverage includes FIFO session state, YMVN Gear mapping, and direct tests of the production `BulkStockAdjustService` (`DEL-006`), including A0-first/physical-slot fallback, movement failure, partial movement, shortfall, and transaction ownership. The delivery persistence workflow is still not executed end-to-end and shared SQL transaction behavior is not verified. That work is tracked as `DEL-003`/`DEL-004`/`DEL-005`/`DEL-007` and remains pending. `DEL-006` uses fake repository and movement dependencies; it is not a SQL rollback proof. Repository SQL scenarios `STK-003`/`STK-004` also remain pending until executed against a disposable local SQL Server.

## Transaction boundary audit

See [TRANSACTION_BOUNDARIES.md](TRANSACTION_BOUNDARIES.md) for the source-verified UnitOfWork composition and transaction ownership across receiving, exporting, rework, physical warehouse, and delivery workflows. It also records why unit tests do not prove SQL rollback and defines the required failure-injection scenario for future SQL integration coverage.

## Full module acceptance matrix

See [TestMatrix.json](../PCTP.QC.Tests/TestMatrix.json). It tracks receiving, exporting, physical warehouse, customer delivery, rework/quarantine, reports, SQL reliability, concurrency, timeout, and UI workflows. Items marked planned are explicitly not represented as passing tests.

## Local execution

```powershell
dotnet test .\PCTP.QC.Tests\PCTP.QC.Tests.csproj --configuration Release
dotnet build .\PCTP.QC.TestRunner\PCTP.QC.TestRunner.csproj --configuration Release
```

Run the desktop app on Windows after building it. The runner must be launched from a checkout containing the solution file. No production database configuration is requested or loaded.

## Next implementation slices

1. **Production transaction correction (separate production-code change; not part of this test-only branch):** change `CapNhapKho`/`CapNhapKhoSP` to execute the stock stored procedure through the transaction-aware repository helper on the shared `phieuUow`; include every LOT's SlotLot adjustment in that same transaction rather than committing one LOT at a time. Confirm the stored procedures do not independently commit, use another connection, or perform non-transactional side effects before relying on this design.
2. **Make finalization observable and recoverable:** stop swallowing `HoanTatSauKhiTruKho` failures. Either include pending-delivery closure/history and order-table delivery marking in the same transaction, or persist a durable finalization/outbox state with retry, idempotency, and operator-visible diagnostics. A post-commit notification should be emitted only after the corresponding transaction succeeds.
3. Add SQL integration coverage for customer delivery through the actual delivery service/repository graph: stock deduction, SlotLot FIFO, stock history, pending-delivery state and rollback through shared `phieuUow`.
4. Add duplicate/replayed delivery-confirmation protection and insufficient-stock/shortfall behavior tests (`DEL-004`/`DEL-005`).
5. Execute the cross-repository STOCKTP + SlotLot rollback scenario on a disposable SQL Server, then add direct production `SlotRepository` coverage (`DEL-008`, `STK-003`/`STK-004`).
6. Execute stored-procedure and finalization failure-injection scenarios on a disposable SQL Server after the production transaction correction (`DEL-003`/`DEL-007`).
7. Add SQL timeout, retry, cancellation, deadlock, and concurrent LOT scenarios with deterministic fixtures.
8. Add WinAppDriver/FlaUI smoke tests for high-value WinForms journeys and expand the matrix with executable evidence.
