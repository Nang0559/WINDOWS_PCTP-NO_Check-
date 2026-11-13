# FVN_PCTP rebuild from the current PCTP application

## Goal

Rebuild the PCTP-owned database surface in the new `FVN_PCTP` database while
keeping the legacy BRAVO7 database `B7R2_FCC` intact during the transition.

**Do not treat every object in `B7R2_FCC` as PCTP-owned.** The application
currently reads/writes a mixture of PCTP-specific tables, older PCTP tables,
BRAVO7 master/transaction tables, and shared reference data. Ownership must be
decided per business entity before any table or data is copied.

## What is in the current SQL folder

The folder contains 42 files plus `QC_Schema/`. They are not one ordered,
idempotent database build:

- `createtableon57.sql` and `createpsfunon57.sql` are **source-database
  extraction/generation helpers**. They inspect the database in which they are
  run; they are not a self-contained build for an empty target database.
- `List_SQL_ON57.csv` is an object/change inventory, not executable DDL.
- `Result_Export_SQL2_Complete_DDL_Metadata.txt` is a captured metadata export;
  it must be reconciled with the current application and current source catalog.
- Many operational scripts are patches for an already-existing database.
  Some stored-procedure scripts explicitly select `B7R2_FCC`; they must not be
  executed unchanged as part of a new-database build.
- `QC_G4_DeliveryFixture_Seed.sql` and `QC_G4_DeliveryFixture_Cleanup.sql`
  are test-fixture scripts, not production migration scripts.
- Diagnostic/repair scripts such as `A0_SlotLot_Diagnose_Repair.sql` must not
  be included in the clean-install sequence.

## Current safe next step

1. Back up `B7R2_FCC` using the normal DBA backup procedure.
2. Connect SSMS to `B7R2_FCC`.
3. Run `00_FVN_PCTP_SourceInventory.sql`.
4. Export every result set to files (CSV or Excel) and retain the output with
   the migration review. The script is read-only and checks the current database
   name before it runs.
5. Review the inventory against C# repositories, SQL scripts and test contracts.
   Mark each object as:
   - `PCTP_OWNED`: PCTP is the source of truth and owns writes.
   - `BRAVO7_OWNED`: remain in `B7R2_FCC`; PCTP accesses through a legacy adapter.
   - `SHARED_REFERENCE`: ownership and write policy must be explicitly assigned.
   - `DERIVED_OR_TEST`: reporting/cache/test data; rebuild or regenerate as designed.
6. Only after approval of that classification generate the target DDL, seed data,
   data-copy scripts and compatibility views/adapters.

## Planned deployment sequence (not yet generated)

| Package | Contents | Target |
|---|---|---|
| 00 | Source inventory and ownership map | Read-only against `B7R2_FCC` |
| 01 | Database baseline, schemas, migration registry | `FVN_PCTP` |
| 02 | PCTP-owned tables, keys, defaults, checks, indexes | `FVN_PCTP` |
| 03 | PCTP-owned views, functions, procedures and triggers | `FVN_PCTP` |
| 04 | Reviewed reference/configuration seed data | `FVN_PCTP` |
| 05 | Selective data migration with row counts/checksums/reconciliation | `FVN_PCTP` |
| 06 | Compatibility layer and application connection cutover | Application, after acceptance |
| 07 | Verification and rollback checks | Both databases |

This plan is deliberately not a one-click script. The object inventory must be
approved first so BRAVO7 objects are not accidentally cloned into `FVN_PCTP`.

## Hard safety rules

- Never drop, rename, truncate or bulk-copy objects in `B7R2_FCC` as part of
  this rebuild.
- Do not run every SQL file alphabetically.
- Do not run SQL that starts with `USE [B7R2_FCC]` against the new database.
- Do not migrate BRAVO7 accounting/inventory tables (for example `B20*` /
  `B30*`) merely because PCTP reads them.
- Do not copy temporary order staging tables as historical business records
  without a defined retention and replay policy.
- Do not switch the application connection string until the new database passes
  object, data, workflow and concurrency acceptance tests.
- No SQL in this package has been executed against the user's SQL Server.

## Source branch

`feature/mes-product-genealogy-timeline`
