# WMS — Test Execution Status / Bằng chứng chạy kiểm thử

Branch: `feature/wms-live-data-test-harness`

## Latest automated CI evidence

- Tested commit: `1e4f8e42cb5970fdb974f6ad5489c0510e20b72b`
- Workflow run: [40909973309 — success](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40909973309)
- Scope: Windows PowerShell syntax + offline G6/write-scope/cleanup guards; desktop test-runner build; candidate-SHA G1 traceability matrix; SQL Server container isolated from DB14/DB57; SQL2-driven schema deployment; G3 SQL engine integration; G4 rollback-only delivery procedure acceptance; deterministic fixture cleanup.
- G1 artifact: `pctp-g1-traceability-evidence` (available from the workflow run artifacts). Static matrix result: **FAIL — 145 code rows, 99 code rows with gaps, 68 SQL scripts, 229 gaps total**. G1 remains OPEN; the CI step publishes the gap report and intentionally does not hide gaps or fail unrelated CI jobs solely because G1 remains open.
- Result: all three CI jobs succeeded. The isolation guard passed; DB57/DB14 were not used as CI test targets.
- Important limit: this run is **not** evidence that the real PCTP UI executed the live DB57 receiving/delivery journeys. The UI journey runner is built/validated, but a real Windows runner execution with DB57 is still required.

## Gate status — do not infer PASS beyond the evidence scope

| Gate | Current status | Evidence / remaining work |
|---|---|---|
| G1 — Traceability/source ownership | **OPEN** | The checked-in `PCTP.QC.UI/G1-gaps.csv` has 105 rows, but the freshly regenerated candidate-SHA artifact found 229 total gaps (145 code rows, 99 with gaps, 68 SQL scripts). Use the run artifact as the candidate-specific inventory; classify each gap and assign owner/disposition. A row referenced by a test is not proof of behavior. |
| G2 — Unit/regression + production build | **PASS LIMITED** | Unit/regression and runner build passed in run 40909084468. Full production `PCTP.exe` compile must be verified on the exact candidate SHA through the Windows DB57 acceptance workflow before calling G2 fully PASS. |
| G3 — SQL engine integration | **PASS — CI scope** | Run 40909084468 passed the isolated SQL engine integration job against a disposable SQL Server container. This is not live DB57 evidence. |
| G4 — Production-like schema/procedure compatibility | **PASS — rollback-only CI scope** | Run 40909084468 passed SQL2-driven schema deployment, procedure acceptance and rollback-only delivery acceptance on the disposable database. Does not authorize deployment to DB14/production. |
| G5 — Two-session concurrency | **NOT VERIFIED / OPEN** | No current candidate evidence for two simultaneous sessions on shared business keys/quota, no-double-consumption and injected rollback. Unit/source-order checks do not close this gate. |
| G6 — Full business E2E | **BLOCKED / NOT VERIFIED** | The receive and two delivery manifests now submit generated QR inputs through real UI controls and contain post-scan SQL assertions/cleanup. They have not been executed against the real Windows UI/DB57 in this evidence set. `g6-qc-direction.json` and `g6-qc-final.json` remain `configured: false`; the gate runner must keep reporting BLOCKED until those journeys are genuinely implemented and run. |

## Required next evidence

1. Preserve run 40909973309 and its `pctp-g1-traceability-evidence` artifact; rerun `pctp-qc-tests.yml` for every candidate SHA.
2. Run the approved manual `pctp-db57-acceptance.yml` only after the operator explicitly confirms DB57 test writes and cleanup; retain the run prefix and artifact report.
3. Resolve the two QC journey blockers using verified schema/source and stable UIA row/action selection; do not seed a completed QC outcome.
4. Add a true two-session concurrency run for G5, including rollback injection and before/after assertions.
5. Regenerate G1 traceability for the candidate SHA and disposition every gap.
6. Keep release/promotion blocked until all required gates and cleanup evidence pass.

## Safety contract

- DB14 / `192.168.200.14\\BRAVO` remains read-only.
- DB57 / `192.168.200.57/B7R2_FCC` is the only approved shared test-write target.
- CI SQL integration uses a disposable container and must not be given credentials or a route to DB14/DB57.
- SQL may seed prerequisite inputs/configuration and observe/assert. It must not insert fake scan, receipt, delivery, QC, defect, replacement or history outcomes.
- A failed cleanup, missing runtime evidence, or an unconfigured manifest means the journey is not PASS.
