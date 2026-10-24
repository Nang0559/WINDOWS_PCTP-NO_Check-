# PCTP QC SQL Integration

Safe SQL schema diagnostics plus production `StockTpRepository` and `BulkStockSlotRepository` checks against session-local fixtures, production idempotency-guard tests, SQL engine workflow checks, and concurrent LOT/reference reservation checks using temporary fixtures.

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
- `ProductionBulkStockSlotRepositoryScenario` executes the actual production `BulkStockSlotRepository` against session-local `#Warehouse`, `#Rack`, `#Slot`, and `#SlotLot`; it checks FIFO `TakeLot`, Slot header refresh, and rollback of the real `StockTpRepository` + `BulkStockSlotRepository` changes alongside a synthetic `#DeliveryState` fixture.
- `SqlEngineWorkflowScenario` uses session-local `#` fixtures to verify warehouse/rack/slot LOT lookup, quarantine export blocking, release status, duplicate reference behavior, opening + received - issued = closing, and rollback across stock/history writes.
- The production movement guard is tested using a temporary global guard table through the actual `BulkStockSlotRepository`, including two concurrent SQL connections competing for the same reference.
- Its concurrency check uses a GUID-named `##` temporary table in `tempdb` so two independent SQL sessions can contend for one LOT. The creator connection remains open during the test and the fixture is dropped in `finally`. No `dbo` business table is created or changed.
- These are SQL Server engine-level contract tests. They do **not** yet prove that production delivery/rework/report UI orchestration invokes the same SQL transaction or idempotency implementation.
- The temporary fixture is scoped to the scenario's SQL session and is discarded when its connection closes. Its test rows are not inserted into `dbo.STOCKTP`.
- If a required table/column is missing, the output lists each fully qualified item and stops before repository tests.
- The tester does not create, alter, or drop existing `dbo` business tables and does not mutate shared business rows.
- `--preflight` runs schema diagnostics only.
- The actual delivery-confirmation repository and production history repository are not yet invoked end-to-end; `#DeliveryState` is a synthetic finalization fixture. Production `BulkStockSlotRepository` LOT consumption concurrency and WinForms UI automation remain unverified. These require a disposable database with a documented schema/seed/cleanup contract or UI automation infrastructure; do not mark them passed based only on isolated fixtures.
- A schema pass alone is not a business-test pass; the default mode now reports the repository scenario separately.

## Delivery acceptance gates (not implied by the default PASS)

The default SQL integration command intentionally does **not** execute `dbo.Usp_Qrcode_Update_Stock2405` against the shared `B7R2_FCC` business tables. The procedure writes stock and delivery-ledger state; wrapping it in a transaction is not a sufficient safety guarantee if the procedure, triggers, or nested code change transaction state. Do not treat the engine-level fixture PASS messages as proof that the full delivery procedure ran.

Run the read-only deployment preflight first:

```powershell
dotnet run --project .\\PCTP.QC.SqlIntegration\\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD --delivery-preflight
```

This checks the deployed procedure and key ledger metadata/transaction-lock markers only. It does not execute the procedure.

The read-only `--delivery-preflight` checks the six operator-confirmed active procedures:
- `dbo.Usp_Qrcode_Update_Stock_SP`
- `dbo.Usp_Qrcode_Update_Stock2405`
- `dbo.Usp_Qrcode_LOAD_PHIEU_DOCQR2405`
- `dbo.Usp_Qrcode_Take_Lot2405`
- `dbo.Usp_Qrcode_Take_LotYMVN2405`
- `dbo.sp_GetCurrentStockStatus`

It also checks the delivery ledgers and the `STOCKTP`, `TMPPHIEUGIAOHANG`, and `DOCQRCODE` tables, and verifies selected transaction/LOT markers in the deployed definitions. It does not execute any of those procedures and does not prove their runtime behavior.

The remaining full acceptance work requires:
1. A DBA-provisioned disposable database restored from a representative schema/data snapshot, with an explicit cleanup/seed contract. Execute the delivery and FIFO procedures only in that isolated database and assert STOCKTP, delivery ledgers, QR state, pending-delivery status, rollback, replay, and concurrent duplicate behavior.
2. Exercise `Usp_Qrcode_LOAD_PHIEU_DOCQR2405`, `Usp_Qrcode_Take_Lot2405`, and `Usp_Qrcode_Take_LotYMVN2405` against isolated seeded data, including FIFO ordering, LOT uniqueness/ambiguity, and insufficient-stock boundaries.
3. Run the production `PhieuKhoRepository` orchestration against the same isolated database, with production history/slot adapters wired into the same UnitOfWork. Source-contract unit tests are regression guards, not a runtime integration pass.
4. Run the WinForms journey on a Windows test agent with a dedicated test account and disposable test records. Capture screenshots and assert both UI state and SQL postconditions.

Until these gates have run and produced artifacts, report delivery acceptance as **PARTIAL**, even if the default SQL integration runner exits with code 0. Never substitute the shared SQL 57 database or production SQL 14 for the disposable acceptance database.

## WinForms TestRunner

Run:

```powershell
dotnet run --project .\\PCTP.QC.TestRunner\\PCTP.QC.TestRunner.csproj
```

Enter SQL credentials and use:

- **Unit / Regression** — xUnit suite.
- **SQL 57 Read-only Check** — schema compatibility diagnostics only (`--preflight`).
- **SQL 57 Diagnostics** — schema check, isolated production StockTpRepository checks against `#STOCKTP`, SQL rollback/commit smoke tests, production BulkStockSlotRepository FIFO/rollback checks, production idempotency guard replay/race checks, temporary-fixture workflow checks, and two-session LOT reservation test.

## Interpreting results

- `PASS: existing dbo schema...`: required column names were found; this alone does not prove business behavior.
- `PASS: production StockTpRepository...`: isolated repository behavior checks also passed using `#STOCKTP`.
- `PASS: SQL transaction rollback/commit...`: the isolated engine transaction checks passed.
- `PASS: isolated SQL workflow...`: temporary-fixture location, quarantine/release, ledger, duplicate-reference, rollback, and concurrency checks passed. This is not a substitute for production delivery confirmation/history adapters or UI end-to-end tests.
- `FAIL`: review the listed missing table/column names against the actual schema and repository SQL. Apply approved fixes manually on SQL 57, then rerun the diagnostics.
- SQL errors such as permission denied, connection timeout, or login failure are reported as failures; they are not auto-corrected.


## Delivery reliability SQL-engine scenarios

The runner now invokes `DeliveryReliabilityScenario` after the savepoint/duplicate-lock checks. It creates a GUID-named `##PCTP_QC_REL_<guid>` table in `tempdb` and keeps the creator connection open for the full scenario. It verifies:

- replay is idempotent and changes quantity/history/pending state once;
- insufficient stock creates no delivery state;
- an injected exception after writes rolls quantity/history/pending state back together;
- a two-session deadlock rolls back the selected victim transaction;
- SQL cancellation rolls back the preceding write.

The fixture is dropped in `finally`. These are SQL-engine contract checks over a synthetic tempdb fixture; they do not invoke `Usp_Qrcode_Update_Stock2405` against the full business schema or prove that the WinForms orchestration is atomic end-to-end.
