# DB57 → DB14 reconciliation

- `PCTP_QC_TARGET_SQL_CONNECTION` is DB57 (authoritative source).
- `PCTP_QC_SOURCE_SQL_CONNECTION` is DB14 (target).
- The workflow uses read-only SQL connections and never executes the generated SQL.
- Download the artifact and manually review `DB14_Migration.sql`, `DB14_Reconciliation_Report.json`, and `README.md`.

## Run

Actions → **PCTP DB57 to DB14 SQL artifact** → Run workflow → enter `GENERATE-DB14-SQL`. Pushes changing the inventory/generator also create an artifact.

## Runner

The workflow requires a self-hosted Windows runner labelled `self-hosted`, `windows`, `sql-network` with network access to both SQL Server endpoints and ODBC Driver 18. It installs Python pyodbc.

## Guardrails

Inventory names are candidates only. The generator verifies each against live DB57 metadata; missing names are unverified, not assumed to exist. It emits additive CREATE TABLE, safe ADD COLUMN and missing programmable-module definitions only. Existing column incompatibilities and module drift are blockers. No DROP, TRUNCATE, DELETE, UPDATE, INSERT, data copy, table replacement, or automated deployment is generated.

Configuration/dictionary row synchronization is deliberately not generated: the current inventory does not specify an approved natural key and exact column/value mapping. Copying these rows blindly could overwrite DB14-specific settings or operational data. This remains a manual verification item.
