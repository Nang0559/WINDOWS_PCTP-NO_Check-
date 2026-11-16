# FVN_PCTP current PCTP SQL consolidation audit

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Target:** `FVN_PCTP` — SQL Server 2016  
**Status:** audit only; this document does not authorize deployment.

## 1. Executive decision

There is currently no complete clean-install SQL sequence for all PCTP operational tables. The only ordered migration chain is `05_MES/Migrations/000..013`. It creates MES/IFS structures and standardizes the physical table names that already exist when migration 013 runs. It does not create the PCTP delivery, warehouse, QR, or XuLyHangLoi base schema.

The canonical home for all future target-database DDL/DML is `SQL/FVN_PCTP`. The older `PCTP/Modules/XuLyHangLoi/Database` folder is retained as historical source pending reviewed conversion; do not run it as an installer.

## 2. Confirmed operational PCTP tables

The application-usage review identifies these six tables as actively used by the PCTP application:

| Current/source name | Evidence-backed use | Target placement |
|---|---|---|
| `LUUPHIEUGIAOHANG` | Delivery rows, trace, QR association, delivery reports | Candidate `FVN_PCTP`; normalize delivery model |
| `IFSPHIEUGIAOHANG` | PCTP-managed IFS order snapshot/staging | `FVN_PCTP` integration/staging |
| `IFSPHIEUGIAOHANG_SP` | PCTP-managed SP order snapshot/staging | `FVN_PCTP` integration/staging |
| `STOCKTP` | Production LOT attributes, stock/FIFO validation and deduction | **Unresolved** — coordinate with MES stock ownership |
| `SlotLot` | Physical slot/LOT balances and warehouse location | **Unresolved** — decide warehouse execution owner |
| `StockHistory` | Stock movement history, QR/voucher/slot evidence | **Unresolved** — must live with the single movement-ledger owner |

Do not create independent duplicate balances or movement ledgers for `STOCKTP`, `SlotLot`, or `StockHistory`. Resolve the single authoritative owner and all writers before promoting warehouse migrations.

## 3. XuLyHangLoi / QT Chung source scripts

There are 13 source scripts in `PCTP/Modules/XuLyHangLoi/Database`:

| Source file | What it actually does | Conversion status |
|---|---|---|
| `PHASE2_AffectedLotSnapshot.sql` | Creates `FVN_PhieuXuLyBatThuongAffectedLot`; FK depends on `FVN_PhieuXuLyBatThuong` | Additive table candidate; base dependency missing from this folder |
| `PHASE3_InitialQC.sql` | Creates `FVN_PhieuXuLyBatThuongQCInitial`; FK depends on base abnormal-processing table | Additive table candidate; base dependency missing |
| `PHASE4_9_ReworkDispositionCompensationWorkflowReport.sql` | Creates six `FVN_PXLB_*` tables and view `vFVN_PXLB_ProcessReport`; reads QT Chung tables | Candidate; must reconcile with QT Chung persistence and business rules |
| `PHASE7_WorkflowSeed.sql` | Seeds QT_CHUNG workflow transitions | Seed data; requires `FVN_sys_WorkflowTransitions`; overlaps the 20→100 transition in script 05 |
| `PHASE10_PhieuTraHang_CustomerNo.sql` | Alters `FVN_PhieuTraHang` and migrates a column | Forward schema/data patch; requires base table and source-column validation |
| `PHASE11_PhieuTraHangCT_LienKetPhieuXuLy.sql` | Alters `FVN_PhieuTraHangCT` to add the abnormal-processing relationship | Forward schema patch; requires both base tables |
| `PHASE12_PhieuXuLyBatThuong_PhieuTraHangCTId.sql` | Alters `FVN_PhieuXuLyBatThuong` to add the return-detail link | Forward schema patch; requires base tables |
| `PHASE13_QTChung_ReworkTables.sql` | Creates four `FVN_TraHangQTChung*` tables | Additive table candidate; review FK/index/transaction behavior before promotion |
| `PHASE14_ReworkExportIdempotency.sql` | Adds `OperationId` and uniqueness/idempotency support to QT Chung export | Forward schema patch; requires PHASE13 table |
| `06_CREATE_InspectionLog.sql` | Creates `dbo.InspectionLog` (without required FVN_ prefix) | Must be converted to physical `dbo.FVN_InspectionLog`; update callers or provide a temporary synonym |
| `05_FIX_Workflow_QTChung_20_to_100.sql` | Seeds only transition 20→100 and hard-codes `USE B7R2_FCC` | Do not deploy to FVN_PCTP; consolidate into the reviewed PHASE7 seed |
| `07_BACKFILL_StockHistory_MaPhieu_XLBT.sql` | Updates existing `StockHistory` rows | Data migration only; depends on the final StockHistory owner and mapping |
| `04_FIX_Identity_cac_bang_QC_rong.sql` | Attempts to repair IDENTITY metadata on existing QC tables | Repair-only; never part of clean install |

Important missing base objects referenced by these scripts include `FVN_PhieuXuLyBatThuong`, `FVN_PhieuTraHang`, `FVN_PhieuTraHangCT`, and `FVN_sys_WorkflowTransitions`. The source folder does not provide a complete baseline definition for these tables. Do not run PHASE2–PHASE14 on an empty database until the base DDL and exact column contracts are supplied and reviewed.

## 4. Warehouse candidate inventory

The current `03_Warehouse/LegacyCandidates` folder contains candidates, not an approved schema:

| File | Objects / behavior | Main blocker |
|---|---|---|
| `PCTPQuarantineRelease.sql` | `FVN_PCTPQuarantineReleaseAudit` | Reconcile with MES quality/lot status and stock movement |
| `PCTPStockMovementGuard.sql` | `PCTPStockMovementGuard` | Physical table name lacks `FVN_`; idempotency key contract needs review |
| `PCTP_InternalTransfer.sql` | `PCTP_InternalLocation`, `PCTP_InternalTransfer`, `PCTP_InternalStock`, `PCTP_InternalReturn` | All physical table names need FVN_ conversion; reconcile stock posting owner |
| `PCTP_StockLotAdjustmentHistory.sql` | `PCTPStockLotAdjustmentHistory` | Name lacks FVN_; do not create a second movement history |
| `PCTP_EnforceUniqueStockTpLot.sql` | Adds uniqueness enforcement to legacy `STOCKTP` | Cannot apply until the final STOCKTP owner/name and duplicate policy are decided |
| `PHASE14_SlotLot_NgQuarantine.sql` | Constraint review/patch for `SlotLot` | Depends on final SlotLot owner and state model |

Diagnostics under `03_Warehouse/Diagnostics` are investigation/repair scripts, not installation scripts.

## 5. Delivery candidate inventory

| File | Objects / behavior | Main blocker |
|---|---|---|
| `PCTP_DeliveryCommandLedger.sql` | `FVN_DeliveryCommandLedger`, `FVN_DeliveryCommandQr` | Candidate idempotency ledger; map to normalized delivery command |
| `PCTP_GiaoDB_DocQrCode_DB.sql` | `DOCQRCODE_DB` | Table lacks FVN_; preserve exact QR/document relationship |
| `PCTP_GiaoDB_RenameOrderTables.sql` | Renames legacy temporary order tables to `FVN_Purchase_Order_DB_HD/CT` | Historical rename/data-preserving migration, not clean install |
| `PCTP_LuuPhieuGiaoHang_CustomerIsolation.sql` | Adds customer-isolation behavior to `LUUPHIEUGIAOHANG` | Must target normalized FVN_ table and preserve customer key semantics |
| `PCTP_OrderTables_AddIsDelivered.sql` | Dynamic ALTER of order tables to add delivery state/date | Must be folded into the final order-table baseline |
| `PHASE15_HangChoGiaoPickDetails.sql` | `FVN_HangChoGiaoPickDetail` | Candidate; validate parent order/detail keys |

The delivery baseline must also define the normalized physical replacement for the confirmed active `LUUPHIEUGIAOHANG` path and the required QR/document relationships. Do not assume the rename patch creates missing tables.

## 6. Programmability and IFS integration

- `06_Programmability/LegacyCandidates/QR`, `Reports`, and `Warehouse` contain old procedures/functions and must be reviewed against current C# callers, dynamic SQL, table types, and the final FVN_ physical names.
- IFS snapshots are PCTP-managed staging data, not the IFS system of record. Preserve the one-way IFS source provenance and do not let dynamic table configuration bypass the FVN_ naming rule.
- Migration 013 renames physical user tables that exist at the time it runs and adds old-name compatibility synonyms. It does not create absent PCTP tables. Synonyms are not a substitute for physical table names in DDL, SqlBulkCopy, or MERGE targets.

## 7. Safe target deployment order — not yet release-approved

1. Confirm the target is `FVN_PCTP`; retain the source database unchanged.
2. Approve the complete baseline: schema, migration registry, core PCTP tables, keys, constraints, types, and defaults.
3. Approve the ownership boundary for LOT, stock balance, Slot/Rack, and stock movement.
4. Deploy approved master-data and IFS integration migrations.
5. Deploy normalized warehouse tables and one authoritative balance/movement model.
6. Deploy normalized delivery, MP/SP order staging, QR/document, pick-detail, and delivery-command model.
7. Deploy XuLyHangLoi base tables first, then PHASE2/3/4–9/13 tables and views, then reviewed PHASE10–12/14 forward changes and workflow seeds.
8. Deploy procedures, functions, views, triggers, and table types after their physical table dependencies.
9. Run controlled data migration with mapping, checkpoints, rejects, reconciliation, and rollback.
10. Run SQL Server 2016 fresh-install, rerun, integrity, concurrency, workflow, QR, delivery, and regression tests.

## 8. Naming and conversion rules

- Every physical user table in `FVN_PCTP`, in every schema, must be named `FVN_<BusinessName>`.
- Do not prefix views, procedures, functions, triggers, table types, constraints, or indexes merely because they are SQL objects.
- New application code, dynamic SQL, SqlBulkCopy, and MERGE targets must use physical FVN_ table names.
- Old-name synonyms may be temporary compatibility only; they do not make old names valid for new DDL or bulk copy.
- Keep SQL Server 2016 compatibility; do not use `STRING_AGG`.

## 9. Release blockers

This audit does not establish the exact source column definitions for all legacy tables, nor prove all SQL Agent/external/dynamic callers have been found. The source application-usage review explicitly leaves `STOCKTP`, `SlotLot`, and `StockHistory` placement unresolved. The complete PCTP baseline and data migration package remain ungenerated. No SQL Server execution/build test was performed by this audit.

**Conclusion:** there is no safe single SQL file to run today to create every current PCTP table in a clean `FVN_PCTP` database. Promote candidates only after the missing baseline and ownership gates are resolved.
