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

- The MES migrations `05_MES/Migrations/000..013` are an ordered schema
  foundation. They have not been runtime-tested against the user's SQL Server.
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
The final physical placement of `STOCKTP`, `SlotLot`, and `StockHistory`
remains unresolved; do not create a second stock balance or movement ledger.
Migration 013 only renames tables that already exist and does not create the
missing PCTP delivery/warehouse/XuLyHangLoi base tables.

**Current instruction:** do not run the legacy XuLyHangLoi, Warehouse, or
Delivery candidates against a clean `FVN_PCTP` database. First approve and
generate the missing baseline and domain migrations.
