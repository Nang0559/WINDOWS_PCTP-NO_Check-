# PCTP QC ephemeral SQL schema

## Purpose

This directory is the **CI database schema source** for G3/G4/G6 SQL acceptance.
CI must create a disposable SQL Server database and must not connect to DB57 or DB14.

## Source of truth

- Source export for `01_Tables.sql`: `SQL_sql.sql` supplied from DB57/B7R2_FCC (kept outside Git).
- SHA-256: `cc21dbc1ebf16f62e3b3fe5d24ff1eff1ce73f88cd34d090408a1bff591c7785`
- `01_Tables.sql` contains only the dependency-closed tables required by the current G4 delivery acceptance and G6 stock/slot journeys. Table definitions are copied from the source export; they are not hand-designed test DDL.
- No production/business rows are copied into Git.

## Deployment order

1. `01_Tables.sql`
2. `05_Split.sql` (`dbo.Split`, definition exported from DB57; see below)
3. checked-in WMS procedures / hardening SQL
4. deterministic fixture seed
5. G3/G4/G6 execution
6. fixture cleanup

## `dbo.Split`

`docs/SQL.text` (SHA-256 `595aafa76194aa239710acd085645283e381899d4669a2141c5841fb01292380`) contains the DB57 definition of `dbo.Split`.
`05_Split.sql` is that definition verbatim, with `CREATE` changed to `CREATE OR ALTER` so the CI deployment can be repeated.
It replaces the former `STRING_SPLIT` shim, which returned a column named `value` and did not guarantee token order.
`Usp_Qrcode_Update_Stock2405.sql` reads it with `INSERT INTO #SplitResult SELECT * FROM dbo.Split(@_LOT, ',')`, so only the order and the single-column shape matter.
The file is for the disposable CI database; never apply it to DB57/DB14 from this repository.

## Tools

`tools/sql_batch_check.py` simulates how `sqlcmd -b -i` splits and loads the deploy files (no database access) and fails the CI job on loader defects.
`docs/SQL.text` is **not** a deploy file; its known defects and the G4 consequences are recorded in `docs/G4_SCHEMA_FROM_GIT.md`.

## Safety invariant

The CI connection is expected to be:

`127.0.0.1,1433 / PCTP_QC_CI`

The workflow sets `PCTP_QC_ALLOW_EPHEMERAL_SQL=true` and `PCTP_QC_REQUIRE_EPHEMERAL_SQL=true`. The test policy rejects production DB14 and, in CI-required-ephemeral mode, rejects DB57 as well.


## SQL2 metadata to reviewed DDL

The SQL2 upload can be used as a *metadata input*, not as a complete, executable schema export.
The repository now includes `tools/gen_schema_from_inventory.py`. It reads either the legacy fixed-width
SQL2 column section or a real CSV column export (`SQL2_ColumnInventory.csv`, with a `SchemaName` column),
selects only rows classified as `GAP-no-git-ddl` + `table` +
`on_db57_export=có` in `docs/SCHEMA_COVERAGE.csv`, and emits idempotent table-column DDL
for the selected scope. It preserves column order, SQL built-in types and lengths, nullability,
identity seed/increment, computed expressions and default expressions.

Example (run locally against the uploaded SQL2 export; do not deploy to DB57):

```powershell
python SQL/QC_Schema/tools/gen_schema_from_inventory.py `
  --inventory "docs\\SQL2_ColumnInventory.csv" `
  --coverage docs/SCHEMA_COVERAGE.csv `
  --type-map SQL/QC_Schema/tools/sql2-db57-verified-type-map.json `
  --out SQL/QC_Schema/02_Generated_From_DB57.sql
```

**Fail-closed behavior is intentional.** SQL2 shows user-defined aliases such as
`LongCodeType`, `CodeType`, `QuantityType`, `MoneyType`, `PercentageType`,
`ExchangeRateType`, and `UniqueKeyType`, but this export does not supply verified
`CREATE TYPE` definitions for them. The generator refuses to guess their base type.
Supply a reviewed JSON type map only after obtaining the exact definitions from a trusted
schema source, for example `{"longcodetype":"nvarchar(20)"}` only if that mapping is verified.

CSV must be exported as actual CSV (not copied fixed-width text) with headers `SchemaName`, `TableName`, `type_desc`, `column_id`, `ColumnName`, `DataType`, `MaxChars`, `precision`, `scale`, `is_nullable`, `is_identity`, `seed_value`, `increment_value`, `is_computed`, `ComputedDefinition`, and `DefaultDefinition`. The generator currently emits `dbo` tables only and fails closed if an allow-listed table is absent from that schema. `MaxChars` is SQL Server `sys.columns.max_length` in bytes; the generator converts Unicode string lengths to characters.\n\nThe generated file is deliberately **not yet treated as production-ready DDL**: the SQL2
column section does not provide a complete structured set of PK/unique indexes, FKs, CHECK
constraints, triggers or permissions. Run the read-only `Export_SQL2_Complete_DDL_Metadata.sql`
in the authorized DB57/B7R2_FCC context to export the missing metadata as separate result sets;
this query contains SELECTs only and does not change the database. Keep those exports as review
inputs, then add constraints through a separate reviewed migration. The generator's unit tests
run in the existing static schema-check step; they require no SQL Server connection. No SQL2-derived
DDL is automatically deployed to DB57 or production.
