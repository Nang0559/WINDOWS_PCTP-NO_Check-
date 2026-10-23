# PCTP QC SQL Integration

Development SQL integration harness for the PCTP QC Test Platform.

## Fixed test target

- SQL Server: `192.168.200.57`
- Database: `B7R2_FCC`
- Production target `192.168.200.14\BRAVO` is rejected by `TestEnvironmentPolicy`.

Credentials are entered at runtime and are not stored in source control.

## CLI

From the repository root:

```powershell
dotnet run --project .\PCTP.QC.SqlIntegration\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD
```

Schema-only preflight:

```powershell
dotnet run --project .\PCTP.QC.SqlIntegration\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD --preflight
```

## What FULL SQL TEST executes

1. SQL transaction rollback/commit engine checks.
2. Production `StockTPRepository` contract checks using isolated `#STOCKTP` fixture.
3. Production `BulkStockSlotRepository` checks using isolated fixtures.
4. Exact order-line delivery marking checks.
5. Idempotent delivery replay.
6. Insufficient-stock/no-partial-mutation checks.
7. Injected failure rollback.
8. Deadlock victim rollback.
9. SQL cancellation rollback.

All synthetic test objects use unique names or temporary tables and are removed by the test harness.

## WinForms TestRunner

Run:

```powershell
dotnet run --project .\PCTP.QC.TestRunner\PCTP.QC.TestRunner.csproj
```

Enter the SQL test credentials and use:

- **Unit / Regression** — xUnit suite only.
- **SQL Preflight** — read-only production-schema check.
- **FULL SQL TEST** — complete SQL integration/reliability suite.
