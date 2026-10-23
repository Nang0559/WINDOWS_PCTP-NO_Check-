# PCTP QC SQL Integration

Safe SQL schema diagnostics, production `StockTpRepository` integration checks against a session-local `#STOCKTP` fixture, SQL engine workflow checks, and concurrent LOT reservation checks using temporary fixtures.

## Fixed target

- SQL Server: `192.168.200.57`
- Database: `B7R2_FCC`
- Production target `192.168.200.14\\BRAVO` is rejected by `TestEnvironmentPolicy`.
- Credentials are entered at runtime and are not stored in source control.

## Run from the repository root

SQL integration (schema preflight + isolated repository behavior checks):

```powershell
dotnet run --project .\\PCTP.QC.SqlIntegration\\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD
```

Schema-only preflight (does not execute repository behavior checks):

```powershell
dotnet run --project .\\PCTP.QC.SqlIntegration\\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD --preflight
```

## Stock movement idempotency migration

Before enabling `ReferenceType` / `ReferenceId` or QR/tem duplicate enforcement in an application environment, review and deploy [`SQL/PCTPStockMovementGuard.sql`](../SQL/PCTPStockMovementGuard.sql) to that environment. The migration creates `dbo.PCTPStockMovementGuard` with unique filtered indexes for the reference pair, QR SHA-256, and tem SHA-256.

- The production `BulkStockSlotRepository.TryRegisterMovement` requires an active `IUnitOfWork` transaction and inserts the claim into the same transaction as stock changes.
- A duplicate returns `false`; `StockMovementService` converts it to `StockMovementResult.Duplicate` without applying a second stock mutation.
- If the application uses `LegacyStockSlotRepositoryAdapter` for a request that carries a reference/QR/tem, it fails closed because the legacy adapter cannot prove that the claim and stock writes share one SQL transaction. Wire the transactional `BulkStockSlotRepository` or implement an equivalent transaction-aware adapter before enabling those request fields.
- Callers must rollback the surrounding UnitOfWork whenever the stock movement result is unsuccessful. Do not commit a transaction containing a claimed reference after a later stock mutation fails.
- SQL integration validates the production guard against unique temporary `tempdb` fixtures, including a two-connection duplicate-reference race; it does not create the permanent guard table in the shared SQL 57 database.

## Safety contract

The default SQL 57 test first checks whether the existing `dbo.STOCKTP`, `dbo.SlotLot`, `dbo.Slot`, `dbo.Rack`, and `dbo.Warehouse` tables contain the columns declared by the QC repository contract. If that passes, it runs `ProductionStockTpRepositoryScenario` against a session-local SQL Server temporary table named `#STOCKTP`.

- The repository scenario exercises the actual production `StockTpRepository` implementation: insert, lookup, exists, received quantity, update, batch lookup, current-stock listing, LOT listing, selected-LOT lookup, and rejection of a missing LOT update.
- SQL engine smoke checks verify transaction rollback/commit using a uniquely named local `#` temporary table.
- `SqlEngineWorkflowScenario` uses session-local `#` fixtures to verify warehouse/rack/slot LOT lookup, quarantine export blocking, release status, duplicate reference behavior, opening + received - issued = closing, and rollback across stock/history writes.
- Its concurrency check uses a GUID-named `##` temporary table in `tempdb` so two independent SQL sessions can contend for one LOT. The creator connection remains open during the test and the fixture is dropped in `finally`. No `dbo` business table is created or changed.
- These are SQL Server engine-level contract tests. They do **not** yet prove that production delivery/rework/report UI orchestration invokes the same SQL transaction or idempotency implementation.
- The temporary fixture is scoped to the scenario's SQL session and is discarded when its connection closes. Its test rows are not inserted into `dbo.STOCKTP`.
- If a required table/column is missing, the output lists each fully qualified item and stops before repository tests.
- The tester does not create, alter, or drop existing `dbo` business tables and does not mutate shared business rows.
- `--preflight` runs schema diagnostics only.
- Production `BulkStockSlotRepository`, delivery finalization, cross-repository transaction enlistment, production LOT concurrency, and WinForms automation remain unverified against real production adapters. These require a disposable database with a documented schema/seed/cleanup contract or UI automation infrastructure; do not mark them passed based only on the isolated engine fixture.
- A schema pass alone is not a business-test pass; the default mode now reports the repository scenario separately.

## WinForms TestRunner

Run:

```powershell
dotnet run --project .\\PCTP.QC.TestRunner\\PCTP.QC.TestRunner.csproj
```

Enter SQL credentials and use:

- **Unit / Regression** — xUnit suite.
- **SQL 57 Read-only Check** — schema compatibility diagnostics only (`--preflight`).
- **SQL 57 Diagnostics** — schema check, isolated production StockTpRepository checks against `#STOCKTP`, SQL rollback/commit smoke tests, temporary-fixture workflow checks, and two-session LOT reservation test.

## Interpreting results

- `PASS: existing dbo schema...`: required column names were found; this alone does not prove business behavior.
- `PASS: production StockTpRepository...`: isolated repository behavior checks also passed using `#STOCKTP`.
- `PASS: SQL transaction rollback/commit...`: the isolated engine transaction checks passed.
- `PASS: isolated SQL workflow...`: temporary-fixture location, quarantine/release, ledger, duplicate-reference, rollback, and concurrency checks passed. This is not a substitute for production delivery adapter or UI end-to-end tests.
- `FAIL`: review the listed missing table/column names against the actual schema and repository SQL. Apply approved fixes manually on SQL 57, then rerun the diagnostics.
- SQL errors such as permission denied, connection timeout, or login failure are reported as failures; they are not auto-corrected.
