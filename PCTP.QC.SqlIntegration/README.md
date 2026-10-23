# PCTP QC SQL Integration

Safe SQL schema diagnostics plus production `StockTpRepository` integration checks using a session-local temporary table.

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

## Safety contract

The default SQL 57 test first checks whether the existing `dbo.STOCKTP`, `dbo.SlotLot`, `dbo.Slot`, `dbo.Rack`, and `dbo.Warehouse` tables contain the columns declared by the QC repository contract. If that passes, it runs `ProductionStockTpRepositoryScenario` against a session-local SQL Server temporary table named `#STOCKTP`.

- The repository scenario exercises the actual production `StockTpRepository` implementation: insert, lookup, exists, received quantity, update, batch lookup, current-stock listing, LOT listing, selected-LOT lookup, and rejection of a missing LOT update.
- The temporary fixture is scoped to the scenario's SQL session and is discarded when its connection closes. Its test rows are not inserted into `dbo.STOCKTP`.
- If a required table/column is missing, the output lists each fully qualified item and stops before repository tests.
- The tester does not create, alter, or drop existing `dbo` business tables and does not mutate shared business rows.
- `--preflight` runs schema diagnostics only.
- Bulk Slot/Rack/Warehouse, delivery, cross-repository transaction, and concurrency scenarios remain disabled against shared SQL 57. They require a dedicated disposable test database or a separately designed isolated fixture and must not be re-enabled by simply removing safety checks.
- A schema pass alone is not a business-test pass; the default mode now reports the repository scenario separately.

## WinForms TestRunner

Run:

```powershell
dotnet run --project .\\PCTP.QC.TestRunner\\PCTP.QC.TestRunner.csproj
```

Enter SQL credentials and use:

- **Unit / Regression** — xUnit suite.
- **SQL 57 Read-only Check** — schema compatibility diagnostics only (`--preflight`).
- **SQL 57 Diagnostics** — schema check plus isolated production StockTpRepository checks against `#STOCKTP`.

## Interpreting results

- `PASS: existing dbo schema...`: required column names were found; this alone does not prove business behavior.
- `PASS: production StockTpRepository...`: isolated repository behavior checks also passed using `#STOCKTP`.
- `FAIL`: review the listed missing table/column names against the actual schema and repository SQL. Apply approved fixes manually on SQL 57, then rerun the diagnostics.
- SQL errors such as permission denied, connection timeout, or login failure are reported as failures; they are not auto-corrected.
