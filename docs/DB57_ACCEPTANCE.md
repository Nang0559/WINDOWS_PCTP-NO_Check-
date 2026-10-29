# PCTP DB57 acceptance (G5/G6)

## Target

The live acceptance workflow targets SQL Server `192.168.200.57`, catalog `B7R2_FCC`. SQL14/BRAVO is always forbidden. The disposable SQL Server and LocalDB fixtures are not accepted as evidence for G5/G6.

## One-time repository setup

Configure these repository Actions secrets:

- `PCTP_QC_TEST_USER`: dedicated SQL login for DB57 acceptance.
- `PCTP_QC_TEST_PASSWORD`: password for that login.

The login must be able to read the schema and perform the specific inserts/updates/deletes used by the G6 journey manifests in `PCTP.QC.UI/journeys/g6/sql`. Do not use `sa` if a narrower QC login can be provisioned. The self-hosted runner must be labelled `self-hosted`, `windows`, and `pctp-ui`, have network access to DB57, and have the legacy WinForms build dependencies (MSBuild, NuGet, DevExpress).

## Run acceptance

1. Open **Actions → PCTP DB57 Acceptance → Run workflow**.
2. Check both confirmations: `confirm_db57_test_writes` and `confirm_qc_cleanup`.
3. Inspect the `pctp-db57-acceptance-evidence` artifact. A green workflow is required; skipped or blocked runs are not PASS.

## Mutation and cleanup contract

- Each journey gets a unique run ID; seeded rows use the `QCUI_<runId>` prefix.
- Every mutating SQL step must reference `@Prefix`; broad `TRUNCATE` and destructive DDL are rejected.
- Each journey must execute `sql-seed`, `sql-assert`, and `sql-cleanup`; cleanup must verify `Remaining = 0`.
- A failed journey is not treated as passed even if the UI opens.
- Do not deploy DB57 changes to SQL14 automatically. DB14 promotion remains a separate reviewed deployment with rollback evidence.

The normal `PCTP QC Test Platform` workflow still runs isolated CI/unit and SQL-engine regression checks. Those are useful regression signals, but they do not replace this DB57 acceptance run.
