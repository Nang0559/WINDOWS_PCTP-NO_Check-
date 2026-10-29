# G1 Remediation Plan

## Baseline (2026-10-29)

- Workflow: [run #40794437869](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40794437869)
- Tested merge ref: `1307b0b80699da5c14b40cfbb26bc0b2f1f5c0c4`
- PR head at run time: `b7e3bd884e1772ca0ec40bfecaf5c3cc9eb0e1cf`
- Artifact: [G1 traceability evidence](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40794437869/artifacts/12862991922)
- Static result: **FAIL — 100 gaps** (87 code rows; 72 code rows with gaps; 15 SQL scripts).

## Gap breakdown

| Category | Count | Meaning |
|---|---:|---|
| Code → tests | 63 | Analyzer cannot find a unit or SQL integration test that names the concrete repository/service or its interface. This is a missing-evidence signal, not proof that no indirect coverage exists. |
| SQL tests only use fakes | 9 | Write-capable repositories have no linked SQL integration test for their production SQL path. Prioritize these before adding superficial unit tests. |
| SQL scripts not linked to code/tests | 13 | Scripts need explicit disposition: production deployment artifact, test fixture/cleanup, retired/manual diagnostic, or truly orphaned script. Do not add fake references solely to satisfy the analyzer. |
| TestMatrix status planned | 10 | Runtime acceptance has not been implemented/proven. |
| TestMatrix status partial | 5 | Existing evidence covers only part of the scenario; keep status partial until remaining acceptance is run. |
| **Total** | **100** | |

## Remediation order

### P0 — writes with only fake/unit evidence (9)

Use the exact artifact's `gaps.csv` for source paths. For each repository:
1. Add a SQL-engine integration scenario using session-local `#temp` fixtures or uniquely named isolated fixtures.
2. Instantiate the production repository, not a hand-reimplemented SQL statement.
3. Assert persisted values, duplicate/replay behavior, transaction ownership, and rollback after injected failure as applicable.
4. Add the scenario to `PCTP.QC.SqlIntegration/Program.cs` or the dedicated test runner so CI actually executes it.
5. Ensure the test only targets ephemeral CI SQL or isolated `B7R2_FCC_QC*`; never write to SQL14 or shared SQL57 `B7R2_FCC`.
6. Rerun G1 and SQL-engine CI; do not remove a gap without execution evidence.

### P1 — missing test links for write-capable repositories

For each write-capable source row, classify the actual risk first: stock quantity changes, delivery/order mutation, audit/history, quarantine, location/rack/slot, receiving/export. Add tests around observable invariants, not just constructor or mock invocation. If a repository delegates writes to another component, document the delegation and test the owner as well as the caller's contract.

### P2 — SQL script disposition (13 findings)

Review each script individually. Scripts that are runtime deployment dependencies must be referenced by the deployment/preflight contract and have contract tests. Fixture seed/cleanup scripts must be referenced by the integration harness and cleanup assertion. Retired/manual diagnostic scripts should be documented as non-runtime assets in a reviewed manifest, rather than fabricated into production-code references.

### P3 — TestMatrix planned/partial (15 findings)

Do not mark `QR-004`, UI journeys, performance cases, or delivery cases `implemented` just to clear the gate. Move status only after the stated expected behavior is demonstrated on the correct isolated target and evidence is attached to the same commit. UI and live SQL acceptance remain distinct gates.

## Exit criteria

- Every one of the 100 findings is either fixed with executable evidence or has an explicit, reviewed, reasoned exception in the traceability contract.
- The analyzer does not silently ignore dynamic SQL or delegated write paths.
- G1 rerun on the release commit reports zero unresolved gaps.
- G2/G3/G4 are rerun for code changes; G5/G6 remain independently blocked until their real Windows/UI and business acceptance evidence exists.
- SQL14 remains read-only; shared SQL57 `B7R2_FCC` is metadata-only/read-only; writes only use ephemeral CI fixtures or isolated `B7R2_FCC_QC*`.
