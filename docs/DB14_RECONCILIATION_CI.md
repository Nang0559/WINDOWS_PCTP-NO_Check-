# DB57 → DB14 reconciliation

- `PCTP_QC_TARGET_SQL_CONNECTION` is DB57 (authoritative source).
- `PCTP_QC_SOURCE_SQL_CONNECTION` is DB14 (target).
- The workflow uses read-only SQL connections and never executes the generated SQL.
- Download the artifact and manually review `DB14_Migration.sql`, `DB14_Reconciliation_Report.json`, and `README.md`.

## Run

Actions → **PCTP DB57 to DB14 SQL artifact** → Run workflow → enter `GENERATE-DB14-SQL`. Pushes changing the inventory/generator also create an artifact.

## Runner

The workflow requires a self-hosted Windows runner labelled `self-hosted`, `windows`, with network access to both SQL Server endpoints and Microsoft ODBC Driver 18 installed. It installs Python pyodbc.

## Guardrails

Inventory names are candidates only. The generator verifies each against live DB57 metadata; missing names are unverified, not assumed to exist. It emits additive CREATE TABLE, nullable ADD COLUMN, missing PK/UNIQUE constraints, CHECK/FK constraints (untrusted until manually validated), indexes, and missing programmable-module definitions. Existing column incompatibilities and module drift are blockers. PK/UNIQUE additions may fail if DB14 contains duplicates; the report flags these for pre-check. No DROP, TRUNCATE, DELETE, UPDATE, INSERT, data copy, table replacement, or automated deployment is generated. NOT NULL columns are not auto-added to existing tables because SQL Server may initialize existing rows.

Configuration/dictionary row synchronization is deliberately not generated: the current inventory does not specify an approved natural key and exact column/value mapping. Copying these rows blindly could overwrite DB14-specific settings or operational data. This remains a manual verification item.

## Scope and outputs

- Scope = `docs/PCTP_SQL_Runtime_Inventory_A-K.csv` **plus** every table/proc/view/function in `docs/SCHEMA_COVERAGE_EFFECTIVE.csv` (any class, so objects classed `info` such as the `Usp_Qrcode_*` procedures are included). Use `--no-coverage` to restrict to the inventory. Names missing from DB57 are reported as `NOT_IN_DB57`, no SQL is emitted for them.
- Triggers on in-scope tables are included.
- Artifact files:
  - `DB14_Migration.sql` - run on DB14 (starts with `USE [<db14>]`). Additive and idempotent: CREATE TABLE, ADD COLUMN, PK/UNIQUE, CHECK/FK (`WITH NOCHECK`), indexes, and CREATE for views/procs/functions/triggers missing on DB14, ordered functions → views → procs → triggers.
  - `DB14_Module_Updates.sql` - `ALTER` for views/procs/functions whose definition differs from DB57 (whitespace ignored). This overwrites the DB14 version: review first.
  - `DB14_Manual_Review.sql` - items that are not generated (type/nullability changes as commented `ALTER COLUMN`, modules that depend on objects outside the scope and missing on DB14).
  - `DB14_Differences.csv` - every difference: `GENERATED`, `REVIEW`, `MANUAL`, `NOT_IN_DB57`.
- A DB57 column that is `NOT NULL` without a default is added as `NULL` on DB14 (flagged `REVIEW`) so existing rows are untouched.
- DB57/DB14 may be older than SQL Server 2017: the generator avoids `STRING_AGG`, `CREATE OR ALTER` and similar newer syntax.

