# WMS — Test Execution Status / Bằng chứng chạy kiểm thử

Branch: `feature/wms-live-data-test-harness`

> **Evidence ledger rule:** dynamic status belongs here. Canonical gate meaning belongs only to `WMS_RELEASE_GATE.md`. Documentation-only commits after an evidence run do not inherit or upgrade the evidence result; every PASS/FAIL remains bound to the exact tested candidate SHA.

## Current documentation baseline

- Latest documentation commit on this branch: `c07406cfbdbc8c5a8f731940286890a77358612c`.
- This commit updates the documentation contract only; it has **no runtime evidence yet**.
- Therefore the latest runtime evidence below remains historical evidence for its recorded SHA and MUST NOT be described as evidence for the current branch head.

## Latest automated CI evidence

- Tested commit: `1e4f8e42cb5970fdb974f6ad5489c0510e20b72b`
- Workflow run: `40909973309` — success
- Scope: Windows PowerShell syntax + offline G6/write-scope/cleanup guards; desktop test-runner build; candidate-SHA G1 traceability matrix; SQL Server container isolated from DB14/DB57; SQL2-driven schema deployment; G3 SQL engine integration; G4 rollback-only delivery procedure acceptance; deterministic fixture cleanup.
- G1 artifact: `pctp-g1-traceability-evidence`. Static matrix result: **FAIL — 145 code rows, 99 code rows with gaps, 68 SQL scripts, 229 gaps total**. G1 remains OPEN; the CI step publishes the gap report and intentionally does not hide gaps or fail unrelated CI jobs solely because G1 remains open.
- Result: all three CI jobs succeeded. The isolation guard passed; DB57/DB14 were not used as CI test targets.
- Important limit: this run is **not** evidence that the real PCTP UI executed the live DB57 receiving/delivery journeys. The UI journey runner is built/validated, but a real Windows runner execution with DB57 is still required.

## Gate status — do not infer PASS beyond the evidence scope

| Gate | Current status | Evidence / remaining work |
|---|---|---|
| G1 — Traceability/source ownership | **OPEN** | Candidate-SHA artifact from run `40909973309`: 145 code rows, 99 with gaps, 68 SQL scripts, 229 gaps. Use that artifact only for SHA `1e4f8e42...`; regenerate for any newer candidate. |
| G2 — Unit/regression + production build | **PASS LIMITED** | Unit/regression and runner build passed in the recorded CI scope. Full production `PCTP.exe` compile must be verified on the exact candidate SHA before calling G2 fully PASS. |
| G3 — SQL engine integration | **PASS — CI scope** | Isolated SQL engine integration passed on disposable SQL Server. This is not live DB57 evidence. |
| G4 — Production-like schema/procedure compatibility | **PASS — rollback-only CI scope** | SQL2-driven schema deployment/procedure acceptance and rollback-only delivery acceptance passed on disposable DB. Does not authorize deployment to DB14/production. |
| G5 — Two-session concurrency | **NOT VERIFIED / OPEN** | No candidate evidence for two simultaneous sessions on shared business keys/quota, no-double-consumption and injected rollback. Unit/source-order checks do not close this gate. |
| G6 — Full business E2E | **BLOCKED / NOT VERIFIED** | Receive and the two delivery manifests have real UI input/Enter steps and post-scan assertions in source, but no live Windows + DB57 evidence in this ledger. `g6-qc-direction.json` and `g6-qc-final.json` remain `configured:false`; pickup/A0, MP/SP, defect/replacement still lack complete runtime journeys. |

## Required next evidence

1. Preserve run `40909973309` and its `pctp-g1-traceability-evidence` artifact as evidence for SHA `1e4f8e42...` only.
2. Regenerate G1 traceability for the next candidate SHA and classify every gap with owner/disposition/approved exception.
3. Run the approved manual `pctp-db57-acceptance.yml` only after the operator explicitly confirms DB57 test writes and cleanup; retain run prefix and artifacts.
4. Resolve the two QC journey blockers using verified schema/source and stable UIA row/action selection; do not seed a completed QC outcome.
5. Add a true two-session concurrency run for G5, including rollback injection and before/after assertions.
6. Complete the G6 journey chain required by the crosswalk: receive → pickup/direct-A0 → delivery → QC/defect → replacement → reconciliation → cleanup, with `NOT AVAILABLE IN SOURCE DATA` used explicitly where DB14 lacks the required shape.
7. Keep release/promotion blocked until required gates and cleanup evidence pass on the same candidate SHA.

## Evidence linkage contract

Every runtime result must record:

- `candidateSha` — exact commit tested;
- `workflowRunId` — GitHub Actions run, or operator run identifier for approved live acceptance;
- `TestCaseId` — canonical ID from `WMS_TEST_DOCUMENTATION_CROSSWALK.md` / catalog;
- `designSource` — exact workflow/contract document and section;
- `fixturePrefix` — run-owned identity prefix;
- `sourceShape` — DB14 source shape or `SYNTHETIC_DB57`;
- `assertions` — expected/actual plus assertion IDs;
- `cleanup` — execution result and verification;
- `artifact` — log/report path or artifact name.

A manifest, worklist, seed success, build, unit test or isolated SQL test is not sufficient to mark a G6 business scenario PASS.

## Safety contract

- DB14 / `192.168.200.14\\BRAVO` remains read-only.
- DB57 / `192.168.200.57/B7R2_FCC` is the only approved shared test-write target.
- CI SQL integration uses a disposable container and must not be given credentials or a route to DB14/DB57.
- SQL may seed prerequisite inputs/configuration and observe/assert. It must not insert fake scan, receipt, delivery, QC, defect, replacement or history outcomes.
- A failed cleanup, missing runtime evidence, or an unconfigured manifest means the journey is not PASS.

## Historical evidence policy

Numbers such as 105, 208 and 229 are not contradictory by themselves; they belong to different generated snapshots. Never merge them. The decision rule is:

> **exact candidate SHA + exact generated artifact + exact run ID → one traceability snapshot.**

When a new candidate is tested, regenerate the matrix rather than copying prior counts.
