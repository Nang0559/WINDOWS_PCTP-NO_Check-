# Production Acceptance & Release Gate

Repository: `Nang0559/WINDOWS_PCTP-NO_Check-`  
Target branch: `feature/pctp-qc-test-platform`  
Status: **IN PROGRESS — no production gate is declared PASS by this document.**

## Gate policy

A gate is PASS only when its required command/test completed successfully and the named evidence is retained for the same commit, test run, executable, and database fixture. Source inspection, build success, a screenshot, schema preflight, or temporary-table test alone cannot prove a committed business workflow. Use a DBA-provisioned disposable database for all writes; do not use production SQL 14 or shared SQL 57 for mutating acceptance.

| Gate | Acceptance objective | Code / SQL under test | Existing tests / commands | Required evidence | Current status / gap |
|---|---|---|---|---|---|
| G1 — Production inventory & traceability | Map requirements to production code, SQL effects, transaction boundary, tests and evidence | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs`; `PCTP/Modules/KhoCore/Repositories/BulkStockSlotRepository.cs`; `PCTP/Modules/KhoCore/Application/Services/StockMovementService.cs`; `SQL/Usp_Qrcode_Update_Stock2405.sql`; `SQL/PCTPStockMovementGuard.sql` | Source review; `PCTP.QC.Tests`; `PCTP.QC.SqlIntegration` | Requirement→Code→SQL→Test→Evidence matrix; reviewed stored-procedure definition; transaction/UOW call graph | **PARTIAL** — repository and SQL test inventory exists, but end-to-end traceability and deployed procedure equivalence must be verified against target DB. |
| G2 — Unit / regression | Business invariants and error paths remain correct | Stock movement service, repositories, delivery/rework domain logic | `dotnet test .\PCTP.QC.Tests\PCTP.QC.Tests.csproj --configuration Release` (confirm project path exists first) | TRX/JUnit output, commit SHA, test counts, failures and build log | **NOT VERIFIED HERE** — user-reported PASS is not an independently captured run in this acceptance record. |
| G3 — SQL schema & repository integration | Verify schema compatibility and production repository behavior on isolated fixtures | `PCTP.QC.SqlIntegration/Program.cs`; `ProductionStockTpRepositoryScenario.cs`; `ProductionBulkStockSlotRepositoryScenario.cs`; `ProductionBulkStockSlotConcurrencyScenario.cs` | SQL Integration default run; `--preflight` for schema only | Console log with target identity; scenario results; fixture isolation confirmation | **PARTIAL** — repository behavior uses temporary fixtures; this does not prove production workflow adapters or stored-procedure effects. |
| G4 — SQL transaction / reliability | Atomicity, rollback, duplicate replay, concurrency and ledger consistency | `SQL/Usp_Qrcode_Update_Stock2405.sql`; `SQL/PCTPStockMovementGuard.sql`; delivery repository/UOW; quarantine/release code | SQL transaction/reliability/order marking/workflow scenarios; `--delivery-preflight` is metadata-only | Isolated DB seed snapshot/version; before/after SQL assertions; rollback/replay/concurrency logs; cleanup result | **PARTIAL** — engine/temporary-fixture checks exist; full stored-procedure and production repository acceptance needs a disposable restored DB. |
| G5 — QC.UI navigation | Open real forms and assert visible titles and controls, with diagnostic tree and screenshots | `PCTP.QC.UI/Invoke-PctpUiSuite.ps1`; `Invoke-PctpUiJourney.ps1`; `PctpUi.Common.ps1`; `journeys/*.json` | Windows-only UI suite, per-journey reports | Fresh run directory, startup inventory, per-step screenshots/control trees, JSON/HTML summary | **FAIL for supplied run** — uploaded evidence shows receiving worklist controls were not found; startup smoke passed but navigation did not. Re-run after UIA mapping fix. |
| G6 — Business E2E & release decision | UI action + authoritative SQL postconditions + cleanup; publish auditable release decision | Receiving, stock/LOT, delivery ledgers, QR state, QC/rework workflow; production repository adapters | Mutating QC.UI journeys plus SQL acceptance in isolated DB | Fixture/run ID; pre-state; UI action and post-state screenshots; SQL before/after assertions; transaction logs; cleanup proof; signed gate summary | **BLOCKED** — current UI journeys are navigation-only; no complete UI→production repository→SQL transaction acceptance evidence exists yet. |

## Requirement-to-code traceability starter

| Requirement / invariant | Production code | SQL / transaction boundary | Test coverage present in repository | Missing evidence / next action |
|---|---|---|---|---|
| Stock movement idempotency | `StockMovementService`; `BulkStockSlotRepository.TryRegisterMovement` | `SQL/PCTPStockMovementGuard.sql`; claim must share the stock mutation transaction | `PCTP.QC.Tests/StockMovementServiceTests.cs`; SQL guard replay/race scenarios | Capture test logs and verify migration + transactional adapter deployed to the acceptance DB. |
| LOT FIFO and slot totals remain consistent | `BulkStockSlotRepository`; `StockTpRepository` | Same UOW transaction for LOT and slot header changes | `ProductionBulkStockSlotRepositoryScenario.cs`; concurrency scenario | Existing fixtures are temporary; retain real run logs and verify actual schema mapping. |
| Delivery confirmation updates all ledgers atomically | `PhieuKhoRepository.cs` and delivery orchestration | `dbo.Usp_Qrcode_Update_Stock2405`, delivery ledger, QR ledger, stock and history | Delivery transaction/reliability scenarios and explicit `--delivery-acceptance` path | Execute only against a DBA-provisioned disposable DB; verify full repository orchestration, all SQL postconditions, rollback and cleanup. |
| Receiving UI navigates to intended worklist | `FormNhapKhoTienTrinh`; manifest `receive-navigation.json` | Read-only; no SQL mutation expected | QC.UI receiving navigation journey | Fix/verify actual runtime UIA tree; assert the form title and controls in the same fresh run. |
| Delivery/QC/rework UI action persists expected business state | Corresponding production forms and repositories | Same transaction/UOW plus workflow/history ledgers | Current navigation manifests only | Build isolated seeded fixtures, add mutating journeys guarded by explicit opt-in, and assert SQL before/after state. |

## Evidence bundle contract

Every run must retain:

- commit SHA, UTC start/end time, run ID, executable absolute path and executable version/hash;
- exact command line and effective journey filter;
- suite and per-journey JSON/HTML reports;
- screenshots and UI Automation trees for failed/asserted steps;
- SQL target identity (server/database only; never passwords), fixture ID, pre/postcondition output, rollback and cleanup status;
- final result per gate: PASS, FAIL, BLOCKED, or NOT RUN.

A missing required artifact means the gate is BLOCKED/NOT VERIFIED, not PASS. Never put SQL credentials, production data, or secrets in the evidence ZIP.

## Safe execution sequence

1. G1 source inventory and review deployed SQL definition (read-only).
2. G2 unit/regression tests.
3. G3 schema preflight, then isolated repository scenarios.
4. G4 disposable-database transaction/rollback/replay/concurrency acceptance.
5. G5 Windows UI navigation and UIA discovery, all read-only.
6. G6 mutating UI-to-SQL E2E on the disposable database, then cleanup and independent evidence review.

Do not run `--delivery-acceptance` against shared SQL 57 or production SQL 14. The current CLI's fixed-target policy and transaction wrapper are not a substitute for a dedicated disposable database and explicit fixture/reset contract.


## Build blocker observed on Windows workstation (2026-10-24)

The supplied Visual Studio output reports `MSB3027` / `MSB3021`: the build could not replace `PCTP.exe` and dependencies because process `PCTP (21584)` held files under `PCTP\bin\Debug`. The QC test projects compiled, but the application project failed to copy its output; overall result was **3 succeeded, 1 failed**. This is a file-lock failure, not evidence of a C# compile error in the listed nullable warnings.

Before rebuilding, close the PCTP window normally and confirm the exact process executable path. Do not terminate an arbitrary process by name if it could be a user's active session. Example diagnostic commands in an elevated or same-user PowerShell:

```powershell
Get-CimInstance Win32_Process -Filter "ProcessId = 21584" |
  Select-Object ProcessId, ExecutablePath, CommandLine

Get-Process -Id 21584 -ErrorAction SilentlyContinue |
  Select-Object Id, ProcessName, StartTime, Path
```

After confirming it is the test instance, close it normally; only if it does not exit and the operator approves, terminate that exact PID and rebuild. Avoid starting QC.UI against an executable while Visual Studio is trying to replace it. Build a fresh executable first, record its path/hash, then run read-only QC.UI against that artifact. This workstation log does not establish a successful rebuild.

## QC.UI failure diagnosis change

The receiving manifest previously coupled the grid assertion to a separate refresh-button expectation. These are now separate checks so the failure identifies which control is missing. When a step fails, the journey runner now exports the selected tree plus each visible top-level PCTP window's UI Automation tree; this helps distinguish a missing child control from the wrong form being active. The suite and child journey share a run ID, and journey timestamps record actual start and completion.

These source changes are **not runtime-verified yet**. Re-run on the Windows test workstation with a fresh output directory; retain the new per-window trees and review the actual `receive-navigation` JSON/HTML report before adjusting any further AutomationIds.
