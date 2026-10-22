# SQL Server integration harness

## Purpose

`PCTP.QC.SqlIntegration` is an opt-in SQL Server integration harness. Unlike repository fakes, it opens a real SQL Server session and exercises transaction rollback, concurrent conditional updates, and command timeout behavior.

It is a **database-engine/transaction contract test**, not yet an end-to-end test of the production `StockMovementService` adapters or legacy STOCKTP/SlotLot schema.

## Safety gate

The runner reads only the `PCTP_QC_SQL_CONNECTION` environment variable. It refuses to run unless `TestEnvironmentPolicy` accepts exactly one local server alias and exactly one database named `TEST_*`, `*_TEST`, or `*_TESTING`.

Never place production credentials in this variable. Do not use remote SQL hosts. The harness creates one uniquely named table in the selected test database and drops it in a `finally` block. The table contains synthetic rows only. The production `UnitOfWork.cs` and `IUnitOfWork.cs` are compiled directly into this harness; a test-only `SQLPROVIDER` stand-in supplies the safe connection string so production connection defaults are never loaded.

## Run locally

PowerShell:

```powershell
$env:PCTP_QC_SQL_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=TEST_PCTP_QC;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project PCTP.QC.SqlIntegration/PCTP.QC.SqlIntegration.csproj --configuration Release
Remove-Item Env:PCTP_QC_SQL_CONNECTION
```

Use an existing local test database. The harness does not create/drop databases and does not execute migrations.

Exit codes:
- `0`: all SQL scenarios passed
- `1`: connection, SQL, or assertion failure
- `2`: missing or unsafe connection configuration

## Scenarios

1. **Production UnitOfWork nested rollback:** executes the actual production `UnitOfWork` implementation, marks an outer transaction rollback-only through a nested rollback, verifies outer commit is rejected, and checks that both writes were reverted.
2. **Production UnitOfWork nested commit:** verifies a nested commit does not commit early and that the outer commit persists both mutations.
3. **Production UnitOfWork dispose rollback:** verifies disposing an active, uncommitted production UnitOfWork rolls back its write.
4. **SQL engine atomic rollback:** performs two writes in one transaction, deliberately raises a SQL error, rolls back, and verifies both values are unchanged.
5. **Concurrent conditional decrement:** two independent SQL connections race to decrement a quantity of 10 by 7. Exactly one update must affect a row; final quantity must be 3.
6. **Command timeout:** updates a synthetic row, runs a command that exceeds a one-second timeout, rolls back, and verifies the original quantity.

## CI behavior and limitations

The default GitHub Actions workflow builds this project but does not execute it because CI does not provision a disposable SQL Server instance. A green build is not a SQL integration pass.

The next stage for full application integration coverage is a disposable SQL Server database with the production STOCKTP and SlotLot schema, then running the real production stock/slot repositories and workflow services against it. The current harness already executes the real production UnitOfWork, but uses a synthetic table rather than the production business schema. Until that fixture exists, do not mark SQL-001/002/003 as implemented in `TestMatrix.json`.

## Cleanup and safety

The runner never runs against a database that fails the fail-closed policy. It never executes production migrations or scripts. It does not delete databases or existing tables; it drops only its own uniquely named fixture table.
