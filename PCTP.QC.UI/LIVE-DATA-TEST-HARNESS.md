# PCTP live-data test harness

## Current phase: metadata inventory only

The first phase compares schema metadata on:

- Source: 192.168.200.14\\BRAVO / B7R2_FCC — read-only account only.
- Test target: 192.168.200.57 / B7R2_FCC — inventory only at this stage.

The inventory script exports table/column definitions, primary and foreign keys, indexes, stored-procedure names, trigger metadata, and approximate row counts. It does not export business rows and does not execute DDL/DML.

## Configure runner secrets

In the repository's GitHub Actions secrets, configure:

- PCTP_QC_SOURCE_SQL_CONNECTION: connection string for SQL14/BRAVO with SELECT/metadata permissions only. Do not use a sysadmin account.
- PCTP_QC_TARGET_SQL_CONNECTION: connection string for SQL57/B7R2_FCC, limited to metadata visibility for the inventory phase.

The script validates the server and catalog values. Secrets are passed only as process environment variables and are not written to logs or inventory reports.

## Run

After the workflow is available in the repository's default branch, run PCTP live-data inventory (read-only) manually and explicitly confirm the read-only inventory. The existing pctp-qc-tests.yml workflow also parses this PowerShell script on pushes to this feature branch.

Artifact: pctp-live-data-inventory-<run>, retained for 7 days. The artifact contains schema metadata and approximate counts, not business row contents.

## Safety boundaries

- Never point the source connection at a writable SQL14 account.
- Do not run the isolated G5/G6 workflow against SQL57. Its isolation checks deliberately block SQL57 and SQL14.
- Do not add live-data loading to the normal push/PR CI path.
- Before the next phase, review the inventory and explicitly select the delivery dates/tables and the expected write scope. The next phase should export only approved rows from SQL14 and apply changes to SQL57 with a repeatable run ID, pre/post checks, and rollback/recovery plan.
- This phase does not create/drop databases, alter schema, insert/update/delete rows, or execute business procedures.

## Hardening added after review (this patch)

- `PctpLiveData.Common.ps1` centralises the guards: exact host match (the old `-like "*192.168.200.14*"` also accepted `192.168.200.140`), SQL14 login proven read-only by a permission probe (rejects sysadmin, db_owner, db_datawriter, db_ddladmin, INSERT/UPDATE/DELETE/EXECUTE/ALTER), and a rule that any *write* phase may only target `192.168.200.57 / B7R2_FCC_QC*`, never the shared `B7R2_FCC`.
- The inventory fails fast when leftover `PCTP-QC-ISOLATION-*` firewall rules exist on the runner (a crashed G5/G6 job would otherwise make SQL14/SQL57 connections time out). Fix with `Test-PctpIsolation.ps1 -Mode Unblock` from an elevated session.
- `-TargetDatabase` (workflow input `target_database`) lets the same inventory run against `B7R2_FCC_QC*` once it exists.
- `drift_columns_pctp_scope.csv` compares SQL14 vs SQL57 columns only for objects classed `OK-*` in `docs/SCHEMA_COVERAGE_EFFECTIVE.csv`, so unrelated tables of the shared B7R2 database are ignored.
- Manifests record login, `GITHUB_SHA` and run id. `procedure_modules.csv` holds SHA-256 of procedure definitions (optional; needs VIEW DEFINITION).
- `PCTP_QC_LIVE_MODE=1` makes `PctpSql.Common.ps1` accept only `B7R2_FCC_QC*` on SQL57 for seed/cleanup.


## Run failure "SQL57 metadata target login is not read-only"

The inventory requires a read-only login on both servers. The `PCTP_QC_TARGET_SQL_CONNECTION` secret uses a privileged login (sysadmin / db_owner), so the guard stopped the run before any query. Two valid fixes:

1. Preferred: a catalog-only login for SQL57 (and, if not done yet, the same pattern for SQL14):
   ```sql
   CREATE LOGIN pctp_qc_ro WITH PASSWORD = '<strong password>', CHECK_POLICY = ON;
   USE B7R2_FCC;
   CREATE USER pctp_qc_ro FOR LOGIN pctp_qc_ro;
   ALTER ROLE db_datareader ADD MEMBER pctp_qc_ro;
   GRANT VIEW DEFINITION TO pctp_qc_ro;
   ```
   Put its connection string in the secret.
2. SQL57 is the agreed writable test database, so the workflow also has the input `allow_privileged_target`. When ticked, the SQL57 login may be privileged; the script still runs catalog SELECTs only and records `privileged_login_opt_in=true` in the manifest. This opt-in never applies to SQL14.

The upload step no longer fails with "No files were found" when the script stops early; that message hid the real error.

## Step 1: profile the newest delivery day on SQL14

Workflow `PCTP live-data profile (SQL14, read-only)` (`Invoke-PctpLiveDataProfile.ps1`). It reads `LUUPHIEUGIAOHANG` and `LUUDOCQRCODE` for one day (default: the newest `NGAYGIAO` not in the future) plus the directly related `STOCKTP`, `TMPPHIEUGIAOHANG` and `DOCQRCODE` rows, in memory only, and writes:

- `column_profiles.csv`: per column type, null/distinct counts, length and range, value *shapes* (digits become 9, letters A/a), and the values of short status columns only.
- `join_candidates.csv`: match rate of each candidate relation (STT, LOT exact / first 20 / first 13 characters, item code) so the real chain is measured, not assumed.
- `invariants.csv` and `profile.md`: counts, sums, delivered vs scanned quantity per STT, STOCKTP sanity counts.

No raw part code, LOT, QR string or customer value is written. After the run, the profile decides the fixture format (step 2) and the UI flow (step 3).
