# CI isolation for G3-G6

Creating a database in GitHub Actions does not make a gate use it. Each gate must be handed the fixture
connection explicitly, and anything that could still reach DB57 (192.168.200.57 / B7R2_FCC), production
(192.168.200.14\\BRAVO) or the IFS/Oracle host (192.168.2.10) must be blocked and proven blocked.

| Gate | Where it runs | Database | How isolation is enforced |
|---|---|---|---|
| G3, G4 | job `sql-engine-integration` (ubuntu) | SQL Server 2022 service container, `PCTP_QC_CI` | `iptables` REJECT for 192.168.200.0/24 and 192.168.2.10 + TCP canary; workflow guard fails on SQL secrets or DB57/DB14 names; runner only accepts `PCTP_QC_EPHEMERAL_SQL_CONNECTION` |
| G5, G6 | job `ui-isolated-g5-g6` (self-hosted Windows, label `pctp-ui`) | LocalDB / SQL Express on that machine, `PCTP_QC_UI`, built from the Linux job's bundle | `Start-PctpIsolatedSql.ps1` creates it; `PCTP_QC_SQL_CONNECTION` makes `PCTP.exe` use it (`QcSqlOverride`); `Test-PctpIsolation.ps1` blocks and asserts egress; gates refuse CI runs without `-Isolated` |

## G4 marker failures
G4 in CI checks procedures deployed from the source in the commit under test. A FAIL on DB57 only says the
procedure deployed there lacks the markers; it says nothing about the repository.

## QcSqlOverride (PCTP/Infrastructure/Sql/QcSqlOverride.cs)
Inert unless `PCTP_QC_SQL_CONNECTION` is set. When set it requires `PCTP_QC_ISOLATED_FIXTURE=1`, a local server,
a database named `PCTP_QC_*`, and no mention of DB57/production/`B7R2_FCC`; otherwise the app throws at startup
instead of falling back to DB57. It replaces the compiled-in defaults of `SQLPROVIDER.B7R2_FCCdb`,
`B7R2_FCCdbb`, `WH4SQLPROVIDER.AutoWH` and `SqlTableCreator.B7R2_FCCdb`.

## Not covered
* The Oracle/IFS provider (`WMSPROVIDER`, host 192.168.2.10) is not redirected; it is blocked at the network level.
* `App.config` / `Settings.Designer.cs` still contain DB57/DB14 strings; no non-designer code reads them
  (checked by search at this commit), and egress is blocked anyway.
* G6 business journeys stay `BLOCKED` until their seed/assert/cleanup SQL exist (`journeys/g6/README.md`).
* The Windows job needs DevExpress 25.2 + MSBuild + SQL LocalDB/Express on the runner. Its steps (MSBuild restore,
  LocalDB provisioning, firewall rules) were not executed when this patch was written.
* If G5 fails on the empty fixture, add reference data under `PCTP.QC.UI/fixtures/ui/*.sql`.
