# FVN_PCTP SQL Package

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Target database:** `FVN_PCTP` (SQL Server 2016 compatibility)

This is now the canonical SQL workspace for the FVN_PCTP rebuild. The former
loose SQL files have been organized into domain folders. Source files were moved
with their original Git blobs; they were not silently rewritten or deleted.

## Folder map

| Folder | Contents |
|---|---|
| `00_Governance` | Source inventory, ownership decisions, evidence exports, schema tooling |
| `01_Baseline` | Baseline candidates; approved clean-install baseline is still a gate |
| `02_MasterData` | IFS one-way master data and BOM synchronization contract |
| `03_Warehouse` | Warehouse candidates, diagnostics, stock/LOT review |
| `04_Delivery` | Delivery/QR/order candidates and repair scripts |
| `05_MES` | Versioned MES schema migrations and legacy source-review scripts |
| `06_Programmability` | Legacy procedures, views, triggers and index candidates awaiting review |
| `07_DataMigration` | Reserved for controlled source-to-target migration |
| `08_Security` | Reserved for approved FVN_PCTP roles and grants |
| `09_Tests` | MES smoke test, isolated fixtures and legacy regression scripts |
| `90_Archive` | Retirement candidates and superseded source retained for traceability |

## Deployment status

**This is a standardized and classified SQL package, not yet a production-ready
one-click installer.** No source SQL Server has been changed and no target SQL
script has been executed by this repository operation.

- The MES/PCTP migrations `05_MES/Migrations/000..020` are an ordered schema
  foundation. Migrations 014–020 add the PCTP site-scoped schema and workflow tables. They have not been runtime-tested against the user's SQL Server.
- Files in `LegacyCandidates`, `Diagnostics`, `IndexCandidates`,
  `SourceReview`, `SchemaEvidence` and `SchemaTooling` are not deployment steps.
- `01_Baseline/Candidates/FVN_PCTP_RuntimeTables.sql` is not an approved
  baseline; promote it only after its full contracts and callers are verified.
- `07_DataMigration` intentionally has no data-copy scripts yet. Source/target
  mapping, checkpointing, rejects and reconciliation must be designed first.
- Nothing in `90_Archive` is approved for irreversible deletion.

## Non-negotiable design rules

1. SQL Server 2016 syntax only; do not use `STRING_AGG`.
2. Every physical table in `FVN_PCTP` must be named `FVN_<BusinessName>` in its domain schema (for example `dbo.FVN_LUUPHIEUGIAOHANG`, `MES.FVN_MaterialLot`, `IFS.FVN_InventoryPart`). Schema names remain domain-oriented; do not prefix procedures/views/functions/types as if they were tables.
3. No runtime dependency on `B7R2_FCC` or BRAVO and no write-back to IFS.
4. IFS is a one-way read-only source; preserve source provenance and sync identity.
5. Do not clone BRAVO accounting/GL/AP/AR objects because PCTP happens to read them.
6. Do not create duplicate stock ledgers. Decide the authority for LOT, warehouse
   balance and stock movement across PCTP/MES before promoting tables.
7. Preserve MP/SP/customer-specific behavior where evidence proves it is distinct.
8. Every deployable migration needs prerequisites, idempotency, transaction/error
   behavior, rollback notes and a ledger entry.
9. No old patch is production-ready merely because it has been moved into a
   domain folder.

## Evidence and review

Start with:
- `00_Governance/00_PhaseStatus.md`
- `00_Governance/SourceDisposition.md`
- `00_Governance/00E_SourceInventory_Findings.md`
- `00_Governance/00C_ApplicationUsageOwnershipReview.md`
- `00_Governance/README.md`
- `05_MES/Migrations/README.md`

Use `00_Governance/Evidence` as a dated snapshot, not as proof of the live
source catalog today. SQL Agent job-step and synonym evidence is still incomplete.

## Consolidation of the current PCTP application SQL

The 13 XuLyHangLoi / QT Chung scripts still located under
`PCTP/Modules/XuLyHangLoi/Database` are historical incremental patches, not a
clean-install schema. Their audited classification, dependencies, naming
corrections, and the remaining blockers are recorded in
`00_Governance/00F_PCTP_CurrentSqlConsolidationAudit.md`. The old folder now
contains a README pointing to that canonical audit.

The six application-confirmed operational tables are documented in the audit.
The physical placement of `STOCKTP`, `SlotLot`, and `StockHistory` is now represented by PCTP extension/evidence tables linked to the MES ledger. Their retained quantity columns are not authoritative balances. Migrations 014–020 create the initial warehouse, delivery, QR, return and QT Chung schema. They do not yet port every legacy QR/LOT/stock procedure or perform source data migration.

**Current instruction:** do not run the legacy XuLyHangLoi, Warehouse, or
Delivery candidates against a clean `FVN_PCTP` database. First approve and
generate the missing baseline and domain migrations.


## Complete PCTP object build register

The approved architecture is now translated into a single object build register:
`00_Governance/00G_PCTP_ObjectBuildRegister.md`.

It inventories the 61 SQL2-generated table definitions, maps physical target names to the FVN_ standard, separates shared B20* master dependencies from PCTP-owned tables, lists the PCTP-linked views/procedures/functions/trigger found in the static dependency evidence, and defines the ordered build phases and release gates.

The register is an implementation contract, not a deployable SQL script. The complete module definitions, missing runtime-table DDL, shared master-data mapping and MES stock projection/posting contracts must be closed before the clean-install migrations are promoted.


## Full PCTP SQL module source export

The SQL2 module section in the checked-in metadata export contains truncated definitions, and the 00C module-reference CSV is empty. To collect the full definitions needed for safe rewriting, run the read-only script:

`00_Governance/SourceExtractionTools/00H_Export_PCTP_RuntimeObjectDefinitions.sql`

Run it only against `B7R2_FCC`, export every result set, and provide Results 1–4 (table columns, indexes, foreign keys and CHECK constraints), Result 5 (full view/procedure/function/trigger definitions), Result 6 (table-valued types), Result 7 (trigger bodies) and Result 8 (synonyms). It does not modify source data or schema.


## IFS part master replaces Bravo B20Item

PCTP's authoritative part master is now explicitly `IFS.FVN_InventoryPart`, keyed by `(SiteCode, PartNo)`. Do not recreate Bravo B20 master tables or maintain a duplicate part master. See `00_Governance/00I_PCTP_IFS_PartMaster_Replacement.md` for the uploaded B7R2 source-export analysis, field mapping, PCTP module dependencies and the SiteCode decision required before DDL.


## PCTP schema build 014–020

The owner approved the composite part identity `(SiteCode, PartNo)`. The ordered migrations now include the explicit `dbo.FVN_PCTPSite` allow-list, PCTP warehouse/rack/slot hierarchy, Slot-to-MES location map, IFS-backed part extensions, TP LOT/SlotLot/StockHistory, MP/SP order snapshots, delivery/QR/idempotency, internal transfer, return/QT Chung, FIFO configuration, queued delivery and special DB purchase-order snapshots.

The current SQL package is still a **schema foundation**, not a cutover-ready application release. Before enabling runtime writes, the application repositories must pass SiteCode/PartNo and use the physical FVN_ tables. Remaining PCTP SQL modules (especially QR/LOT allocation and old STOCKTP/SlotLot procedures), source-to-target data migration, view/trigger conversion, permissions, and SQL Server 2016 execution/regression evidence remain open. Do not run against production or switch the app connection until those gates pass.
