# Rework two-session SQL acceptance (B0-02 / B0-03)

Scenario: `PCTP.QC.SqlIntegration/ReworkTwoSessionScenario.cs`  
Run: automatically inside `dotnet run --project PCTP.QC.SqlIntegration -- --engine-only` (job `sql-engine-integration`, isolated SQL Server).  
Fixtures: uniquely named `##PCTP_QC_<guid>_*` tempdb tables; no `dbo` business table is touched.

## What it proves and what it does not

| Proven (protocol level, on SQL Server) | Not proven |
|---|---|
| The **exact** `sp_getapplock` text in `QTChungService.cs` (single shared resource `QT_REWORK_TICKET:` used by both export and delivery) is Exclusive, Transaction-owned, times out at 15 s and fails closed (`THROW 51000`). The scenario reads it from the production source, so drift breaks the test. | That `QTChungService.GiaoHangRework` and `ReworkStockService.XuatKhoRework` execute these steps in this order against real repositories. Order is covered only by the source-contract tests. |
| The row lock produced by production `UpdateStatusIfCurrentIs` serializes two exporters by itself (second line of defense). | Behaviour on the production schema, triggers, indexes or isolation level (G4). |
| The filtered unique index on `OperationId` blocks a duplicate ledger row even with no lock (last line of defense). | UI journeys (G5/G6). |

Negative controls (D2, E2) run the same steps without locks and **must** oversell; if they do not, the scenario fails because the race would be undetectable.

## Scenario matrix

| ID | Gate item | Setup | Expected |
|---|---|---|---|
| D1 | B0-03 | exported 10, two sessions deliver 10 under the delivery lock | A committed, B rejected, delivered 10 |
| D2 | control | same, no lock, both read before either writes | delivered 20 (oversell) |
| D3 | B0-03 | lock holder A, session B with 500 ms timeout | B gets error 51000, writes nothing |
| D4 | B0-03 | two ledger lines, failure injected after the first insert | 0 rows, lock released, retry commits |
| E1 | B0-02 | quota 10, two sessions export 6 under export lock + row lock | A committed, B rejected, exported 6 |
| E2 | control | same, no locks | exported 12 (exceeds quota) |
| E3 | B0-02 | same `OperationId` from two sessions | one row, second `AlreadyProcessed` |
| E4 | B0-02 | same `OperationId`, no locks | one commit, one unique-index violation (2601/2627) |
| E5 | B0-02 | row lock only, no application lock | A committed, B rejected, exported 6 |
| F1 | B0-02/03 | export holds the ticket lock, delivery on the same ticket with 500 ms timeout | delivery gets 51000, writes nothing, export committed |
| F2 | B0-02/03 | delivery holds the ticket lock, export on the same ticket with 500 ms timeout | export gets 51000, writes nothing, delivery committed |

## Status rule

Do **not** mark B0-02 / B0-03 `VERIFIED` from this scenario alone. Record it as "protocol-level two-session evidence, CI run `<id>` on SHA `<sha>`". `VERIFIED` additionally needs a run of the real methods on a Windows/.NET Framework harness with SQL-backed repositories, plus the production-schema gate (G4).

## Findings for the owner (not changed by this patch)

1. **Audit noise.** The export row lock is a same-value `UPDATE` through `UpdateStatusIfCurrentIs`, which also rewrites `UpdatedAt`/`UpdatedBy` on the ticket for every export. Consider a dedicated `SELECT ... WITH (UPDLOCK, ROWLOCK)` helper so the ticket audit columns keep meaning "last status change".
2. **Resolved in 5999007.** Export and delivery now share one `QT_REWORK_TICKET:<id>` resource, so they serialize against each other. Scenario F1/F2 below proves the cross-path blocking at protocol level. Whether a late export after "delivered in full" must be rejected is still a business-status question and needs its own guard + test.
