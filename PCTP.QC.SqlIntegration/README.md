# PCTP QC SQL Integration

Read-only SQL diagnostics for the existing shared test database.

## Fixed target

- SQL Server: `192.168.200.57`
- Database: `B7R2_FCC`
- Production target `192.168.200.14\\BRAVO` is rejected by `TestEnvironmentPolicy`.
- Credentials are entered at runtime and are not stored in source control.

## Run from the repository root

Read-only schema diagnostics:

```powershell
dotnet run --project .\\PCTP.QC.SqlIntegration\\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD
```

Schema-only check (same safe read-only behavior):

```powershell
dotnet run --project .\\PCTP.QC.SqlIntegration\\PCTP.QC.SqlIntegration.csproj --configuration Release -- --user YOUR_SQL_USER --password YOUR_SQL_PASSWORD --preflight
```

## Safety contract

The default SQL 57 test is **read-only**. It connects to the fixed target and checks whether the existing `dbo.STOCKTP`, `dbo.SlotLot`, `dbo.Slot`, `dbo.Rack`, and `dbo.Warehouse` tables contain the columns expected by the current QC repository contract.

- If a table or column is missing, the output lists each fully qualified item (for example, `dbo.SlotLot.QrData`) and tells you to review/fix the schema on SQL 57.
- The tester does not automatically create, alter, or drop existing tables.
- It does not insert, update, or delete business rows.
- It does not run write/concurrency/rollback scenarios against the shared database, because doing so could affect real test data or require destructive fixture cleanup.
- A schema pass means only that the required column names exist. It is not a claim that production CRUD, delivery, concurrency, or rollback behavior has passed.

## WinForms TestRunner

Run:

```powershell
dotnet run --project .\\PCTP.QC.TestRunner\\PCTP.QC.TestRunner.csproj
```

Enter SQL credentials and use:

- **Unit / Regression** — xUnit suite.
- **SQL 57 Read-only Check** — schema compatibility diagnostics.
- **SQL 57 Diagnostics** — same safe diagnostic path; no database writes.

## Interpreting results

- `PASS`: all declared required columns were found; no schema/data changes were made.
- `FAIL`: review the listed missing table/column names against the actual schema and repository SQL. Apply approved fixes manually on SQL 57, then rerun the diagnostics.
- SQL errors such as permission denied, connection timeout, or login failure are reported as failures; they are not auto-corrected.
