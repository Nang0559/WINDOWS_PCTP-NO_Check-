# PCTP QC Test Dashboard

WinForms dashboard for running and reviewing QC test suites on Windows.

## Run

From the repository root:

```powershell
dotnet run --project .\PCTP.QC.TestRunner\PCTP.QC.TestRunner.csproj
```

Or build a Release executable:

```powershell
dotnet build .\PCTP.QC.TestRunner\PCTP.QC.TestRunner.csproj --configuration Release
```

## Dashboard features

- **Unit / Regression** runs `PCTP.QC.Tests`.
- **SQL 57 Read-only Check** validates the fixed test database schema only.
- **Full SQL Integration** runs SQL integration scenarios against the fixed target `192.168.200.57/B7R2_FCC`.
- Live log output and a parsed event table with PASS / FAIL / INFO / WARN entries.
- Run summary, exit code and elapsed duration.
- Stop button terminates the running `dotnet` process tree.
- Run history survives app restarts; double-click a history row to open its log.
- Export an HTML report with history, current run events and current console log.
- Open the report directory from the dashboard.

## SQL credentials and safety

Enter SQL credentials at runtime. The dashboard passes them to the child process through environment variables (`PCTP_QC_SQL_USER` and `PCTP_QC_SQL_PASSWORD`), not command-line arguments, and does not write them to logs or reports.

The target is fixed to `192.168.200.57/B7R2_FCC`. Production `192.168.200.14\BRAVO` remains blocked by the SQL integration environment policy. The default integration suite uses isolated temporary fixtures and rollback-only checks; it is not permission to run destructive tests against shared business data.

## Reports

Reports are written to:

`TestResults\PCTP.QC.TestRunner\`

Files include per-run `.log` files, `history.json`, and exported `PCTP-QC-report-*.html` files. This folder can contain test diagnostics; review it before sharing externally.
