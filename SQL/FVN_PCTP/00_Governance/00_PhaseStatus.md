# FVN_PCTP SQL consolidation — phase completion report

**Branch:** `feature/mes-product-genealogy-timeline`  
**Consolidation commit:** `41874d21ea731cb5ee9c49db11527e2f2f4ec373`  
**Follow-up commits:** `94fa5e0c222fe79223ebfe1d7df4353aa157c58d`, `661cd8e65c68ee13f69f93001e2950b63fcca694`, `b3a04f5608ec2e30d238a869a82e8d2f85324ef7`

## 1. Completed in this phase

- Consolidated the loose SQL root into `SQL/FVN_PCTP/`.
- Moved 124 source/evidence/tooling/test SQL-related files while preserving their Git blob content.
- Created the canonical domain folders: Governance, Baseline, MasterData, Warehouse, Delivery, MES, Programmability, DataMigration, Security, Tests and Archive.
- Moved the versioned MES migration package 000–011 and its smoke test under the FVN_PCTP package; added migrations 012–013 for IFS synchronization and database-wide physical table prefix standardization.
- Separated legacy candidates, diagnostics, test fixtures, source extraction tools and generated metadata from deployable migration folders.
- Added/updated package README and source disposition register.
- No source database objects or business data were changed by this repository work.
- No legacy SQL file was permanently deleted.

## 2. Static screening performed

For MES migrations 000–011:
- no hard-coded `USE [B7R2_FCC]` statement was found;
- no `STRING_AGG` usage was found;
- scripts declare SQL Server 2016 compatibility in their headers or design;
- migration sequence is guarded by `MES.SchemaMigration`.

This is a text-level static screen only. It is **not** SQL Server compilation, deployment, concurrency or rollback testing.

## 3. Not complete / not authorized for deployment

The following are intentionally left as gates, not silently guessed:

1. **Database boundary:** confirm whether all MES objects live inside `FVN_PCTP` under schema `MES`, or whether a separate `FVN_MES` database is required. Current folder placement reflects the combined PCTP_MES target direction but does not prove the final physical boundary.
2. **Warehouse authority:** finalize the single owner for Warehouse/Rack/Slot, LOT, stock balances and stock movements. Resolve `STOCKTP`, `SlotLot`, `StockHistory` and their writers before creating a second ledger.
3. **Delivery contracts:** normalize delivery documents, MP/SP IFS snapshots, QR association, pick detail and delivery history; preserve natural-key behavior and customer isolation.
4. **Master data:** define approved PCTP-owned master tables and one-way IFS synchronization contracts.
5. **Baseline:** approve the database baseline, schema ownership, migration registry and all keys/constraints.
6. **Legacy migration:** create explicit mapping, stage/load, restart checkpoints, reject handling, checksums, reconciliation and rollback.
7. **Programmability:** review every legacy procedure/view/trigger for actual callers, dynamic SQL, external dependencies, locking and transaction semantics.
8. **Security:** map roles to real users only after identity and environment approval.
9. **Runtime validation:** execute on a disposable SQL Server 2016 database; test fresh install, rerun, rollback, integrity, idempotency, race conditions and workflow acceptance.
10. **Retirement:** complete SQL Agent job-step, synonym, linked-server and external integration review before deleting anything.

## 4. Safe use

- Treat only the ordered migration files in `05_MES/Migrations` as a schema candidate sequence, and only after the database-boundary decision and disposable-database testing.
- Do not run files under `LegacyCandidates`, `Diagnostics`, `IndexCandidates`, `SourceReview`, `SchemaEvidence`, `SchemaTooling` or `90_Archive` as installation scripts.
- Do not run generated DB57 schema outputs as a complete FVN_PCTP baseline.
- Do not run repair, retirement, fixture cleanup or source inventory scripts against production.
- Keep `B7R2_FCC` unchanged throughout migration design.

## 5. Definition of done for the next phase

The next phase is complete only when:
- the database boundary and ownership register are approved;
- the clean baseline and domain migrations are versioned and idempotent;
- all legacy scripts are mapped to KEEP/REWRITE/MERGE/ARCHIVE/EXCLUDE with evidence;
- migration data is reconciled and restartable;
- SQL Server 2016 test evidence is checked in;
- the application cutover and rollback plan are approved.

This report records repository consolidation, not database deployment.


## 6. IFS one-way synchronization implementation (2026-11-16)

Added on this branch:
- `PCTP/Modules/IfsSync/IfsSyncService.cs`: streams the five approved IFSAPP
  Views through the existing Oracle Managed Data Access provider, stages in
  bounded 2,000-row chunks and applies one full batch to FVN_PCTP.
- `IFSPROVIDER` reads `IFS_ORACLE` from protected configuration and exposes
  strict reader/query paths that propagate Oracle errors instead of returning
  an empty DataTable.
- `012_IFS_MasterDataSync.sql`: creates IFS source snapshots, raw JSON payload
  preservation, batch/error logs, BOM key constraints and transactional apply.
- `09_Tests/IFS_SyncSmokeTest.sql`: read-only batch and BOM relationship check.
- The SQL target catalog is validated as `FVN_PCTP`; the service does not use
  the legacy SQL connection factory pinned to B7R2_FCC.

Migration 013 now standardizes every physical user table in FVN_PCTP to `FVN_<Name>`, retains old two-part names as compatibility synonyms, and repoints the IFS synchronization MERGE targets and SqlBulkCopy destination to physical FVN_ tables. This is a forward migration for databases where 000–012 have already run.

This implementation is a deployment candidate. It has **not** been built or
executed against the user's Oracle IFS or SQL Server. It performs full snapshot
upserts, not incremental watermark sync. Source rows missing in a run are not
deleted or deactivated. Validate on a disposable FVN_PCTP database before use.


## 7. Current PCTP application SQL audit (2026-11-16)

Added `00F_PCTP_CurrentSqlConsolidationAudit.md` after reviewing both
`PCTP/Modules/XuLyHangLoi/Database` and the Warehouse, Delivery, MES, and
Programmability folders under `SQL/FVN_PCTP`.

The audit confirms that the repository still has no complete PCTP clean-install
baseline. The XuLyHangLoi folder contains incremental table patches, workflow
seeds, a data backfill, and an IDENTITY repair script; several depend on core
tables not defined in that folder. Warehouse and Delivery remain candidate
scripts. The active application evidence confirms six operational tables,
while ownership of `STOCKTP`, `SlotLot`, and `StockHistory` remains unresolved.

A README was added to the old XuLyHangLoi SQL folder to mark it as historical
source and direct future target SQL work to `SQL/FVN_PCTP`. No old SQL file was
deleted or executed. This audit is documentation/static inspection only; no SQL
Server execution or application build was performed.


## 8. Approved PCTP workflow preservation and unified inventory ownership (2026-11-16)

The project owner accepted ADR-001:
- Preserve the current PCTP TP receipt, issue, FIFO, QR, delivery and abnormal-processing business contracts and operational behavior.
- Standardize physical table names to `FVN_<BusinessName>`; do not use legacy table names in new code.
- Use `MES.FVN_MaterialLot`, `MES.FVN_InventoryBalance`, and `MES.FVN_InventoryTransaction` as the single canonical LOT, stock-balance and movement ledger.
- Keep PCTP business documents and workflow tables distinct; post exactly-once ledger movements when documents are confirmed.
- Preserve source trace fields and use explicit SlotId-to-MES-location mapping.

This is an accepted architecture decision, not evidence that the migrations or application code have already been converted. Column-level mapping, SQL migrations, C# caller conversion, data migration, SQL Server 2016 testing and cutover remain to be completed. No source database changes or SQL Server execution were performed by this documentation commit.

See `ADR_001_PCTP_PreservingWorkflows_UnifiedInventoryLedger.md`.


## 9. Complete PCTP object build register (2026-11-16)

Added `00G_PCTP_ObjectBuildRegister.md` as the implementation register for the full PCTP rebuild. It maps all 61 SQL2-generated table definitions to FVN_ target names, excludes blind cloning of shared B20* masters, identifies additional runtime tables outside the generated allowlist, inventories PCTP-linked views/procedures/functions/trigger from static dependency evidence, and defines the ordered deployment phases.

This commit is a schema/programming scope artifact. It does not claim that the complete table/view/procedure migrations have been generated or executed. Exact module definitions, missing runtime table DDL, shared master mapping, and the MES stock projection/posting contract remain explicit gates. No SQL Server execution, source database modification, C# cutover or application build was performed.


## 10. Full source module extraction (2026-11-16)

Added `00G_PCTP_ObjectBuildRegister.md` and read-only source export `00H_Export_PCTP_RuntimeObjectDefinitions.sql`. Inspection of the 6.58 MB SQL2 export confirmed that its module-definition section is truncated to short previews; the 00C module-reference CSV is empty. 00H collects full SQL module definitions (result 5), target-table columns (result 1), indexes (result 2), foreign keys (result 3), check constraints (result 4), table types (result 6), triggers (result 7) and relevant synonyms (result 8) from `B7R2_FCC`, with a hard database-name guard and SELECT-only behavior.

This is an evidence-collection step. The full PCTP view/procedure/function/trigger migration cannot be completed accurately until these definitions and table-type contracts are available. No SQL Server execution or source database modification was performed.


## 11. PCTP part master source correction (2026-11-16)

Owner clarification: do not recreate the Bravo B20 master tables. Use IFS-synchronized `IFS.FVN_InventoryPart` as PCTP's site-specific part master, `IFS.FVN_PartCatalog` for global catalog attributes where appropriate, and keep `MES.FVN_IfsInventoryPart` as the MES projection. Added `00I_PCTP_IFS_PartMaster_Replacement.md` based on the uploaded 00H source export. It records exact observed B20 source columns, actual PCTP module references, technical extension needs and the non-portable 580 KB trigger.

The 00H export analysis found 449 matching SQL module definitions, 87 with PCTP candidate references and 40 that also reference B20 master objects. This is static evidence, not proof of active callers. No B20 clone or PCTP DDL was created in this step. Final schema generation is blocked on the PCTP-to-IFS SiteCode/warehouse mapping and the approved HRM department source.


## 12. PCTP site-scoped schema implementation 014–020 (2026-11-16)

Owner decision: all PCTP part references use the composite key `(SiteCode, PartNo)`. This is not restricted to a single IFS site. Added ordered migrations:

- `014_PCTP_SiteScopedPartAndWarehouse.sql`: PCTP site allow-list, IFS-backed part identity/extensions, warehouse/rack/slot and explicit Slot-to-MES location mapping.
- `015_PCTP_TpLotSlotAndHistory.sql`: TP LOT attributes, SlotLot projection and StockHistory evidence; legacy quantity columns are not stock authority.
- `016_PCTP_IFSOrderSnapshotsAndDelivery.sql`: MP/SP IFS order snapshots and delivery history.
- `017_PCTP_QrDeliveryAndInternalTransfers.sql`: QR command idempotency, separate DB QR, quarantine audit and internal transfer/return documents.
- `018_PCTP_ReturnAndAbnormalWorkflow.sql`: return header/detail, QT Chung, affected LOT, initial QC, rework, disposition, compensation and workflow transitions, based on current PCTP models/repositories and the reviewed legacy workflow scripts.
- `019_PCTP_SlotLotProjectionContract.sql`: new site-aware TVP and atomic SlotLot projection replacement; it rejects projection quantities that differ from MES on-hand.
- `020_PCTP_QueuedDeliveryFifoAndPurchaseOrders.sql`: queued delivery/pick details, per-site FIFO settings and special DB purchase-order snapshots.

The scripts are ordered after 000–013 and use physical FVN_ table names. They are repository changes only: **no SQL Server execution, SQL compilation, data migration, C# repository conversion, app build, or production cutover was performed**. The 014–020 scripts still require a disposable SQL Server 2016 install/rerun/rollback test. Runtime activation remains blocked until the PCTP SiteCode allow-list and Warehouse/Slot-to-MES location mappings are seeded, all C# writes pass SiteCode/PartNo, remaining QR/LOT/stock stored procedures and triggers are converted, and the legacy data is reconciled.

The `019` SlotLot save contract is deliberately projection-only. It does not create or remove MES stock; every stock-affecting command must post through the canonical MES movement ledger before saving the slot projection.


## 13. FIFO, Slot inventory and QR/LOT contracts 021–022 (2026-11-16)

- `021_PCTP_FifoPolicyAndSlotInventoryViews.sql`: site-aware FIFO upsert preserves the no-op/no-history behavior when a value is unchanged; `vFVN_PCTP_SlotInventory` reads on-hand/reserved/available quantities from `MES.FVN_InventoryBalance`.
- `022_PCTP_CustomerLotNormalizationAndQrWrite.sql`: customer-specific LOT normalization is configured per `(SiteCode, PartNo, CustomerCode)`; QR insert validates optional FCC/HVN part references against IFS.
- No global customer trim rule is hard-coded. The legacy customer `0100002` trim-six behavior is an example only and must be seeded only for the source-confirmed part/customer combinations.
- Updated ordered migration index to 000–022. Still no SQL Server execution or C# integration test.


## 14. Inspection/tem reconciliation history 023 (2026-11-16)

Added `023_PCTP_InspectionLog.sql`, based on the current `06_CREATE_InspectionLog.sql` candidate. The physical target is `dbo.FVN_InspectionLog`, with site-scoped optional IFS part and MES LOT links. The ordered migration index is now 000–023. SQL execution and application repository conversion remain pending.


## 15. Optional legacy site mapping/preflight 024 (2026-11-16)

Added `024_PCTP_LegacySiteMappingAndPreflight.sql` with explicit warehouse→SiteCode/target warehouse, legacy part→IFS part, legacy LOT→MES MaterialLot, factory→SiteCode and document→SiteCode mapping tables. It adds `dbo.usp_FVN_PCTP_LegacyPreflight`, which reads `B7R2_FCC` and returns unresolved rows without modifying the source. Migration 024 is optional for an empty FVN_PCTP schema and is run only when preparing source data migration on an instance where B7R2_FCC is visible. It does not perform the copy.
