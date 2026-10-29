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

## Step 1 result: profile of 2026-10-28 (run 40840127480) and what it fixes for the fixture

Full day: 199 LUUPHIEUGIAOHANG rows, 23 slips, 3108 LUUDOCQRCODE rows, 112 STOCKTP lots. No raw values were exported.

**Join keys (measured, then confirmed in `Usp_Qrcode_Update_Stock2405`)**

| Relation | Key | Result |
|---|---|---|
| LUU <-> STOCKTP, LUUQR <-> STOCKTP | `MAHANG`/`PART` + `LEFT(LOT,13)` of every comma-separated LOT element | 100% (any-key) |
| LUU <-> LUUQR | same key, **no** day filter | 95.5% any-key, 91.5% all-keys in the day; 17 of the 18 missing LOTs have QR on earlier days |
| LUU <-> LUUQR by `STT` | **not a key** | `LUUDOCQRCODE.STT` is the scan-record number (230 values) copied from `DOCQRCODE.STT`; the slip number is `DOCQRCODE.GIO`, which is not stored in LUUQR. The 100% "STT" match is only small-integer overlap (reverse direction 20.8%) |
| `GIOGIAOFCC`/`GIOXUAT`, `LOTHVN` | not keys | 87.9% and 0% |

**Business invariants that held on 100% of live rows** (used by the fixture asserts): `SLNHAP - SLXUAT = SLCONLAI` (112/112), `SLCONLAI >= 0`, `SLXUAT <= SLNHAP`, one STOCKTP row per LOT root13 (112/112), `SUM(LUU.SOLUONG of single-LOT lines) <= SLXUAT` (50/50 lots).

**Not invariants (do not assert):** `LUU.SOLUONG = SUM(QR)` (equal on 8 of 50 keys; QR may be scanned on other days), `SLCONLAI = SLCONLAITMP` (84/112, `SLCONLAITMP` is a YMVN working column), `SATUS`.

**Shape the fixture copies:** 140/51/8 LUU rows with 1/2/3 LOT elements (multi-LOT stored as `LOT20-qty,LOT20-qty`); lots of the same item differ by production date, so root13 stays unique; Yamaha-type labels (plant 3) carry a huge `SLTEMHVN` (338750 vs 3550 for `SLTEMFCC`), so quantities are always measured with `SLTEMFCC`.

### Fixture `g6-delivery-hvn-multilot` (HVN path: `TMPPHIEUGIAOHANG` / `DOCQRCODE`)

`journeys/g6/sql/delivery-hvn-multilot/` seeds only prerequisites keyed by the run prefix `QCUI_<runId8>` (3 items, 6 STOCKTP lots, 3 pending lines with 1/2/3 LOT). The result tables `DOCQRCODE`, `LUUPHIEUGIAOHANG`, `LUUDOCQRCODE` are written by the real UI scan through the real procedure, never seeded.

```powershell
# seed + print carton QRs (one per lot, quantity taken from the pending line)
.\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Seed -Journey delivery-hvn-multilot -ConfirmWrites -SqlUser <user>
# scan the QRs on the HVN - MP screen by hand, then check the invariants (read-only)
.\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Assert -Journey delivery-hvn-multilot -Prefix QCUI_xxxxxxxx -SqlUser <user>
# remove everything for that prefix, Remaining must be 0
.\PCTP.QC.UI\Invoke-PctpQcSeed.ps1 -Action Cleanup -Journey delivery-hvn-multilot -Prefix QCUI_xxxxxxxx -ConfirmWrites -SqlUser <user>
```

`assert-after-scan.sql` fails the run on `StockViolations`, `LuuOverIssued` and `QrWithoutStockViolations`; the other columns (`QrFccQty` vs `StockIssued`, `QrDuplicateRows`, lots without QR) are information only. `QrDuplicateRows` tests the open hypothesis that QR rows are duplicated when `DOCQRCODE.GIO` is empty and several pending lines share a `MAHANG`.

Limits that remain: the QR scan is still manual; `PCTPStockMovementGuard` and `FVN_DeliveryCommand*` ledger rows written by a real scan carry no prefix and are not removed by cleanup; the YMVN (`YMVN_DOCQRCODE`) and SP (`SP_DOCQRCODE`) paths are not covered yet; the new SQL was syntax-checked only, it has not run on SQL Server.

### Running the fixture from GitHub Actions (self-hosted runner on the dev machine)

Workflow **PCTP DB57 delivery fixture (seed / assert / cleanup)** (`.github/workflows/pctp-db57-delivery-fixture.yml`, manual dispatch only, runner labels `self-hosted, windows, pctp-ui`, secrets `PCTP_QC_TEST_USER` / `PCTP_QC_TEST_PASSWORD`, target pinned to the SQL57 test database by `PCTP_QC_LIVE_MODE`). Inputs: tick `confirm_db57_test_writes`, pick `phase`.

1. `phase = seed`: creates the journey data under a fresh prefix and prints the prefix and the carton QR strings in the **job summary** (also in the artifact `pctp-db57-delivery-fixture-seed-<run>`).
2. Scan those QRs by hand on the HVN - MP screen of `PCTP.exe` on the runner machine (its `PCTP.exe.config` must point at the test database).
3. `phase = assert`, `prefix = QCUI_xxxxxxxx`: read-only; the run **fails** when `StockViolations`, `LuuOverIssued` or `QrWithoutStockViolations` is not 0. Information columns never fail the run.
4. `phase = cleanup`, same prefix: removes the rows; the run fails unless `Remaining = 0`.

It shares the concurrency group `pctp-db57-acceptance` with the full G6 acceptance workflow, so the two never write to the same database at the same time. `pctp-db57-acceptance.yml` (G5 + G6, now including `g6-delivery-hvn-multilot` as a seed -> open screen -> assert -> cleanup journey) remains the end-to-end gate; this workflow adds the manual-scan phases it cannot automate.

Why the scan is not automated: the runner can type into `txt_DOCQRCODE` and press Enter (`set-value`, `press-enter`), but the delivery also needs the pending grid rows selected (checkbox column) and the save action on `HVN_PGH`; their AutomationIds have not been captured from a `pctp-ui-tree-*.json` on the runner yet. Dispatch G5 once on the runner, send the tree file, and the scan steps can be added to the journey.
