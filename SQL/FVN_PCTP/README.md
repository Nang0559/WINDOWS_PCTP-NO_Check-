# FVN_PCTP SQL Package

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Target:** `FVN_PCTP` (SQL Server 2016 compatible)

This folder is the intended canonical home for reviewed SQL that builds, upgrades,
validates, migrates, and operates the new FVN_PCTP database.

## Important status

This is a controlled consolidation, not a bulk copy of the old `/SQL` folder.
The source scripts remain in their original locations until each one has been
reviewed for callers, object dependencies, source ownership, SQL Server version,
idempotency, and data-loss risk. Do not delete or execute old scripts merely
because they are not yet listed here.

## Target structure

| Folder | Responsibility |
|---|---|
| `00_Governance` | Scope, source inventory, ownership, migration decisions, dependency register |
| `01_Baseline` | Database baseline, schemas, migration ledger, core types |
| `02_MasterData` | PCTP-owned reference/master data and one-way IFS staging contracts |
| `03_Warehouse` | Warehouse, rack, slot, LOT, stock balance and movement |
| `04_Delivery` | Customer orders, delivery documents, QR binding and delivery trace |
| `05_MES` | Production orders, BOM, material issue/return, WIP, finished LOT and genealogy |
| `06_Programmability` | Reviewed views, functions, procedures and triggers, ordered by dependency |
| `07_DataMigration` | Source-to-target mappings, staged loads, checkpoints, rejects and reconciliation |
| `08_Security` | Roles, grants and permission verification |
| `09_Tests` | Smoke, integrity, workflow, concurrency and rollback tests |
| `90_Archive` | Superseded scripts retained for traceability; never part of deployment |

## Rules for this package

1. Every deployable SQL file must state its target database/schema assumptions,
   SQL Server minimum version, purpose, prerequisites, and whether it changes data.
2. Deployments must be ordered and recorded in a migration ledger. A file must
   be safe to re-run or explicitly fail with a clear already-applied guard.
3. No script in this package may depend at runtime on `B7R2_FCC`, BRAVO, or write
   to IFS. IFS is a one-way read-only source.
4. Do not clone BRAVO accounting/GL/AP/AR objects just because the legacy PCTP
   application reads them.
5. Do not create two authoritative stock ledgers. Warehouse stock, production
   LOT and stock movement ownership must be explicit before a table is moved.
6. Preserve source traceability: original path, source commit, review decision,
   and replacement path must be recorded before an old script is retired.
7. SQL Server 2016 compatibility is mandatory; do not use `STRING_AGG`.
8. Test destructive/data-changing scripts only on a disposable database or
   approved test copy. No script here is assumed deployed merely because it is
   committed.

## Current source decisions

- The six operational tables identified in `../00E_SourceInventory_Findings.md`
  are confirmed as actively used by PCTP, but their final physical placement
  between PCTP and MES still needs dependency closure.
- `STOCKTP`, `SlotLot`, `StockHistory`, and the Slot/Rack/Warehouse hierarchy
  must be treated as one stock-ownership design problem, not copied independently.
- `LUUPHIEUGIAOHANG` and IFS MP/SP snapshots are PCTP operational scope; their
  normalized target contracts and migration mapping still need review.
- The current `../MES/Migrations` package is a committed MES schema foundation,
  not a completed legacy-data migration and not runtime-tested against SQL Server.
  It must be reconciled with the final FVN_PCTP/FVN_MES boundary before promotion
  into this canonical package.

## Promotion workflow

1. Inventory each source script and identify the database/object it targets.
2. Classify it as `KEEP_AS_IS`, `REWRITE`, `MERGE`, `ARCHIVE`,
   `EXCLUDE_WITH_EVIDENCE`, or `PENDING_REVIEW`.
3. Resolve dependencies and ownership, including dynamic SQL, application SQL,
   SQL Agent jobs, synonyms and external integrations.
4. Add or rewrite the reviewed script in the appropriate folder above.
5. Run static checks and SQL Server 2016 syntax checks; then test on a disposable
   SQL Server database with deployment, rerun, rollback and concurrency cases.
6. Only after replacement and verification, move the superseded source to
   `90_Archive` or delete it in a separate reviewed cleanup commit.

## Source evidence

- `../00E_SourceInventory_Findings.md`
- `../00C_ApplicationUsageOwnershipReview.md`
- `../00B_FVN_PCTP_OwnershipDependencyMap.sql`
- `../MES/Migrations/README.md`

These are evidence and design inputs, not blanket approval to migrate every
listed object.
