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
| Customer delivery FIFO | ordering, compound LOT, quantity consistency, rejected scan atomicity, rescan, release, reset | Unit/regression |
| QC Final domain rules | initial QC OK/NG reconciliation, rework OK/NG reconciliation, final scrap disposition, compensation separation, affected LOT non-negative quantities | Unit/domain |
| YMVN delivery validation | LOT character 13 → Gear, order quantity limit, missing Gear, completion totals, unexpected Gear, empty order | Unit/regression |
| Stock movement (KhoCore) | normal receiving, NG quarantine status, successful/blocked export, quantity guards, LOT identity, full/partial move QR rules, restore-picked, correction transaction guard | Unit/regression |
| Environment safety | local test database allow-list; production/remote rejection | Unit/security |

## Coverage evidence integrity

Every scenario marked `implemented` in `TestMatrix.json` must include a non-empty `testEvidence` entry naming its executable xUnit class/test. `TestMatrixIntegrityTests` enforces this rule and verifies that production database access, production data mutation, and automatic SQL script execution remain disabled. A scenario without executable evidence must remain `planned`.

The evidence check proves the matrix is traceable to test code; it does not convert unit tests into SQL integration or UI end-to-end tests. Those require separate disposable-environment evidence.

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

1. Add unit tests for stock movement invariants using repository fakes.
2. Add receiving/export/rework workflow integration tests against a disposable local SQL Server database.
3. Add SQL timeout, retry, cancellation, deadlock, and concurrent LOT scenarios with deterministic fixtures.
4. Add WinAppDriver/ FlaUI smoke tests for high-value WinForms journeys.
5. Expand regression matrix until each critical business rule has an executable test and evidence.
