# SQL Server integration harness

## Purpose

`PCTP.QC.SqlIntegration` is an opt-in SQL Server integration harness. Unlike repository fakes, it opens a real SQL Server session and exercises transaction rollback, concurrent conditional updates, and command timeout behavior.

It is a **database-engine/transaction contract test** with direct coverage of the production `StockTpRepository`. It is not yet an end-to-end test of the production `StockMovementService` adapters, `SlotRepository`, or `BulkStockSlotRepository`.

## Safety gate

The runner reads only the `PCTP_QC_SQL_CONNECTION` environment variable. It refuses to run unless `TestEnvironmentPolicy` accepts exactly one local server alias and exactly one database named `TEST_*`, `*_TEST`, or `*_TESTING`.

Never place production credentials in this variable. Do not use remote SQL hosts. The guard also rejects attached MDF files, failover partners, SQL user-instance mode, context connections, malformed strings, and duplicate server/database aliases. It parses the connection string with `Microsoft.Data.SqlClient.SqlConnectionStringBuilder` and fails closed on ambiguity. The harness creates one uniquely named fixture table in the selected test database and drops it in a `finally` block. The production `StockTpRepository` scenario additionally creates a connection-local `#STOCKTP` temporary table inside its own UnitOfWork transaction; it does not create or alter a persistent business table. All rows are synthetic. The production `UnitOfWork.cs` and `IUnitOfWork.cs` are compiled directly into this harness; a test-only `SQLPROVIDER` stand-in supplies the safe connection string so production connection defaults are never loaded.

## Run locally

PowerShell:

```powershell
$env:PCTP_QC_SQL_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=TEST_PCTP_QC;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project PCTP.QC.SqlIntegration/PCTP.QC.SqlIntegration.csproj --configuration Release
Remove-Item Env:PCTP_QC_SQL_CONNECTION
```

Use an existing local test database. The harness does not create/drop databases and does not execute migrations.

To inspect whether a disposable copy of the production schema has the required columns, run the read-only preflight:

```powershell
dotnet run --project PCTP.QC.SqlIntegration/PCTP.QC.SqlIntegration.csproj --configuration Release -- --check-production-schema
```

This checks the `dbo.STOCKTP`, `dbo.SlotLot`, `dbo.Slot`, `dbo.Rack`, and `dbo.Warehouse` column contracts used by the production stock/slot repositories. It does not read business rows, modify data, create objects, or run migrations. It is a schema compatibility check only; it does not claim to execute those repositories.

Exit codes:
- `0`: all SQL scenarios passed, or read-only schema preflight passed
- `1`: connection, SQL, or assertion failure
- `2`: missing or unsafe connection configuration
- `3`: production schema preflight found missing columns

## Scenarios

1. **Production UnitOfWork nested rollback:** marks an outer transaction rollback-only through a nested rollback, verifies outer commit is rejected, and checks that both writes were reverted.
2. **Production UnitOfWork nested commit:** verifies a nested commit does not commit early and that the outer commit persists both mutations.
3. **Production UnitOfWork dispose rollback:** verifies disposing an active, uncommitted production UnitOfWork rolls back its write.
4. **Production SqlRepositoryBase enlistment:** compiles the production `SqlRepositoryBase.cs`, verifies it uses the active production `UnitOfWork.Connection` and `.Transaction`, executes an update through the protected repository method, and confirms rollback. A test-only executor throws if the base class incorrectly bypasses the active transaction.
5. **Production StockTpRepository:** compiles and executes the actual production `StockTpRepository.cs` through its repository interface. Insert, lookup, existence, received/remaining quantity, additive update, batch lookup and current-stock query run against an isolated connection-local `#STOCKTP` table using the production UnitOfWork transaction. Minimal test DTO/interface declarations isolate unrelated WinForms model dependencies; repository SQL and implementation are not replaced. This does not prove compatibility with every column/type/index in the deployed `dbo.STOCKTP` table.
6. **SQL engine atomic rollback:** performs two writes in one transaction, deliberately raises a SQL error, rolls back, and verifies both values are unchanged.
7. **Concurrent conditional decrement:** two independent SQL connections race to decrement a quantity of 10 by 7. Exactly one update must affect a row; final quantity must be 3.
8. **Command timeout:** updates a synthetic row, runs a command that exceeds a one-second timeout, rolls back, and verifies the original quantity.

## CI behavior and limitations

The default GitHub Actions workflow builds this project but does not execute it because CI does not provision a disposable SQL Server instance. A green build is not a SQL integration pass.

The current harness now exercises the production `StockTpRepository` and `BulkStockSlotRepository`; it still does not exercise `SlotRepository`, full production schema compatibility, or a workflow that mutates STOCKTP and SlotLot together. The bulk repository fixture is deliberately minimal and test-only DTO/QR shims are used to isolate UI dependencies. Keep SQL-001/002/003 and `STK-003`/`STK-004` pending until the scenarios execute successfully on a disposable SQL Server database and schema compatibility is separately confirmed.

## Cleanup and safety

The runner never runs against a database that fails the fail-closed policy. It never executes production migrations or scripts. It does not delete databases or existing tables; it drops only its own uniquely named fixture table.
