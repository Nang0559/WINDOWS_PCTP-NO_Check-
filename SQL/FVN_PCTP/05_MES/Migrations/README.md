# FVN_PCTP / MES SQL deployment package

**Target:** the new FVN_PCTP database. Select the target database in SSMS before executing.
**Compatibility:** SQL Server 2016. Scripts do not use STRING_AGG, do not hardcode USE, and do not write to IFS9 or B7R2_FCC.

## Migration order

Run one file at a time, in order:

| Version | File | Contents |
|---|---|---|
| 000 | 000_MES_MigrationFramework.sql | MES schema and migration ledger |
| 001 | 001_MES_MasterAndBom.sql | IFS9 read models, staged IFS cost buckets, controlled BOM revision/components |
| 002 | 002_MES_InventoryProductionGenealogy.sql | PO, Lot, immutable stock ledger, balance projection, receipts, production and genealogy |
| 003 | 003_MES_Types.sql | TVP contracts for scans, movement batches and BOM components |
| 004 | 004_MES_Procedures.sql | Stock posting, FIFO candidates, trace queries and BOM approval |
| 005 | 005_MES_Indexes.sql | FIFO, stock history, production and genealogy indexes |
| 006 | 006_MES_Functions.sql | Inline TVFs for available stock and Lot movement timeline |
| 007 | 007_MES_IntegrityAndReservations.sql | Cross-site/BOM/PO integrity and reservation table |
| 008 | 008_MES_ReservationAndGenealogyProcedures.sql | Reserve, consume, release and link actual consumption to finished Lot |
| 009 | 009_MES_ValidationAndGuards.sql | PO receipt consistency trigger and ledger-vs-balance reconciliation |
| 010 | 010_MES_SecurityRoles.sql | Least-privilege database roles and object grants |
| 011 | 011_MES_BatchPostingProcedure.sql | Atomic TVP inventory movement batch (maximum 500 rows) |
| 012 | 012_IFS_MasterDataSync.sql | IFS one-way Part Catalog, Inventory Part and BOM snapshots with staging/apply |
| 013 | 013_FVN_TablePrefixStandardization.sql | Renames every physical user table in FVN_PCTP to FVN_* and creates legacy-name synonyms for compatibility |
| 014 | 014_PCTP_SiteScopedPartAndWarehouse.sql | Explicit PCTP site allow-list, (SiteCode, PartNo) extensions, Warehouse/Rack/Slot hierarchy and Slot→MES location mapping |
| 015 | 015_PCTP_TpLotSlotAndHistory.sql | TP LOT attributes, SlotLot projection and StockHistory evidence; no duplicate quantity ledger |
| 016 | 016_PCTP_IFSOrderSnapshotsAndDelivery.sql | IFS MP/SP delivery-order snapshots and PCTP delivery history |
| 017 | 017_PCTP_QrDeliveryAndInternalTransfers.sql | QR/delivery idempotency, quarantine audit and internal transfer/return documents |
| 018 | 018_PCTP_ReturnAndAbnormalWorkflow.sql | Return header/detail, QT Chung, affected LOT, initial QC, rework, disposition, compensation and workflow transitions |
| 019 | 019_PCTP_SlotLotProjectionContract.sql | Site-aware SlotLot TVP and atomic replacement guarded by MES balance reconciliation |
| 020 | 020_PCTP_QueuedDeliveryFifoAndPurchaseOrders.sql | Queued delivery/pick details, per-site FIFO configuration and special DB purchase-order snapshots |
| 021 | 021_PCTP_FifoPolicyAndSlotInventoryViews.sql | Site-scoped FIFO upsert and MES-backed slot inventory view |
| 022 | 022_PCTP_CustomerLotNormalizationAndQrWrite.sql | Customer-specific LOT normalization and site-aware QR insert contract |
| 023 | 023_PCTP_InspectionLog.sql | Site-scoped inspection/tem reconciliation history |
| 024 | 024_PCTP_LegacySiteMappingAndPreflight.sql | Optional data-migration mapping tables and read-only B7R2_FCC preflight; requires source DB on the same SQL Server |
| 025 | 025_PCTP_MESInventoryPostingAdapter.sql | PCTP slot-aware adapter to the canonical MES inventory posting procedure |
| 026 | 026_PCTP_StockMovementGuardAndLotAdjustmentHistory.sql | Idempotency guard and LOT adjustment audit |
| 027 | 027_IFS_WarehouseSyncAndEntityStatus.sql | Add IFS WAREHOUSE staging/apply, 800-character description and warehouse batch counts |
| 028 | 028_IFS_SupplierAndCanonicalComponentLot.sql | IFS supplier master sync, five-character canonical item identity allocation and component LOT builder |
| 029 | 029_FVN_InternalItemAndSupplierIdentity.sql | FVN-owned ItemId5 and SupplierId5 identity masters with internal IDENTITY keys and stable base-36 tokens |
| 030 | 030_CanonicalItemIdentityInIfsApply.sql | Makes MES.FVN_ItemIdentity the only ItemId5 allocator used by IFS apply; dbo.FVN_PartIdentity remains compatibility-only |\n| 031 | 031_MES_ComponentPurchaseOrderReleaseAndLabels.sql | MES-owned component PO and component LOT release |\n| 032 | 032_MES_ComponentLotCanonicalReceiptPosting.sql | Component LOT receipt through canonical MES stock ledger |\n| 033 | 033_MES_ShiftPlanningBomDemandAndPartToWip.sql | Shift production plan, IFS BOM demand and PART-to-WIP issue |\n| 034 | 034_MES_ProductionExecutionRoutingAndComponentTrace.sql | Routing, production LOT execution, direct/WIP transfer and actual component genealogy |\n| 035 | 035_MES_PerUnitProcessLabels.sql | Unique per-product labels reserved and printed at each operation |\n| 036 | 036_MES_ProductionLifecycleWorkflow.sql | Shared workflow transitions for plan/order/operation, state projection, guards, audit and timeline |

The applied versions are recorded in `MES.FVN_SchemaMigration` after migration 013. Before migration 013, query `MES.SchemaMigration`; after migration 013, use the physical table:

```sql
SELECT MigrationVersion,MigrationName,AppliedAtUtc,AppliedBy
FROM MES.FVN_SchemaMigration ORDER BY MigrationVersion;
```

Run migration 013 only after 000–012 have completed successfully. Run 014–030 after 013 in version order; migration 024 is optional for source-data preflight, but 025 and later still require their documented prerequisites. Migration 027 requires migration 026; migration 028 requires 027; migration 029 requires 028; migration 030 requires 029.

## Architecture rules

- IFS9 is read-only and authoritative for synchronized part/site/location data. MES.Ifs* tables are read models, not manually maintained masters.
- Part and location identity is scoped by SiteCode plus source code until global uniqueness is proven.
- IFS cost bucket data is staged in MES.IfsBomBucketStage and remains PENDING_ENGINEERING_VERIFICATION. It is not an approved production BOM.
- MES.InventoryTransaction is the immutable quantity movement ledger. MES.InventoryBalance is the transactionally maintained projection.
- MES.MaterialExecutionEvent records production execution and references the stock movement; it is not a second stock ledger.
- MES.FinishedLotGenealogy must be linked from actual CONSUME events, never inferred from BOM quantities or planned quantities.
- No price, cost, currency, valuation, GL/AP/AR or accounting posting columns are introduced.
- Migration 013 applies the FVN_ prefix to every physical user table in every schema in FVN_PCTP. It preserves the former two-part names as synonyms for SQL compatibility; all new physical PCTP tables in dbo or other domain schemas must be created as dbo.FVN_<Name> (or <Schema>.FVN_<Name>). Synonyms are compatibility aliases, not physical tables.
- Direct bulk-load APIs such as SqlBulkCopy must target the physical FVN_ table name, not a compatibility synonym.
- Migrations 000–012 create the initial unprefixed MES/IFS tables; migration 013 normalizes their physical names after those migrations have run.

## Important deployment limitation

These migrations now provide the PCTP site-scoped schema foundation and several business document/workflow tables. They are **not yet the complete production installer or legacy data migration** from B7R2_FCC/PCTP. That migration must be built only after source-to-target mapping for STOCKTP, SlotLot, StockHistory, LUUPHIEUGIAOHANG, IFSPHIEUGIAHANG and IFSPHIEUGIAHANG_SP, including the Slot/Rack/Warehouse dependency closure, is reconciled against the live source metadata and application usage.

## Required validation before production

1. Confirm target context with SELECT DB_NAME() AS TargetDatabase, @@VERSION AS SqlServerVersion.
2. Confirm IFS9 PartNo, SiteCode, warehouse and location key semantics and maximum lengths.
3. Confirm receipt acceptance/quarantine workflow, unit conversion, negative-stock policy, return and adjustment approval.
4. Confirm FIFO priority (expiry-first vs manufacture-date-first) with operations.
5. Confirm the approved Engineering BOM source. Until verified, do not promote cost bucket snapshots. The approval procedure intentionally blocks SourceKind=IFS_VERIFIED pending a dedicated source-mapping gate.
6. Run migration scripts in a clean non-production database first.
7. Run MES.usp_ValidateInventoryBalance after test movements; it must return no rows.
8. Test duplicate idempotency keys, simultaneous reservations, simultaneous issues, rollback, split/merge and genealogy allocation.
9. Map actual database users into MES_Reader, MES_Operator and MES_BOMApprover only after role review. Migration 010 creates roles; it does not create users or logins.
10. Do not give application users direct INSERT/UPDATE/DELETE permission on inventory ledger or balance tables. Grant EXECUTE on approved procedures and SELECT on approved views/functions.

## Known next build items

- Reviewed conversion of the remaining PCTP QR/LOT/stock/delivery stored procedures and triggers to physical FVN_ names and MES ledger posting.
- PCTP C# repository conversion to pass SiteCode/PartNo and call the new FVN_ procedures/TVPs.
- PO receipt posting procedure with over-receipt and duplicate-document policy.
- Production order create/release procedure validating approved BOM, effective dates, site and product.
- Finished Lot receipt procedure and QR/document binding.
- Incremental IFS watermark policy after validating source change semantics; migration 012 currently performs full snapshot upserts.
- Data migration scripts with source/target counts, rejected-row staging, reconciliation and restart checkpoints.
- Deployment smoke tests, concurrency tests and rollback rehearsal.


## Migration 012 — IFS synchronization

Run `012_IFS_MasterDataSync.sql` after the preceding migrations 000–011 (it checks that 000 and 001 prerequisites exist). It creates the
`IFS` schema, raw-payload staging, typed Part Catalog / Inventory Part / BOM
snapshots and `IFS.usp_ApplySyncBatch`. It also refreshes the compatibility
projection `MES.IfsInventoryPart` used by existing MES foreign keys.
No Oracle connection is made by SQL Server; the PCTP C# service performs the
read-only Oracle extraction and SQL Server bulk staging.


## PCTP site setup after migration 020

Before creating PCTP warehouses or writing PCTP documents, register only the IFS sites that PCTP is authorized to use in `dbo.FVN_PCTPSite`. Then create each `dbo.FVN_Warehouse` with its `SiteCode`, preserve/assign its numeric `WarehouseId`, and create Rack/Slot rows. Map every active Slot to exactly one primary `MES.FVN_IfsInventoryLocation` using `dbo.FVN_SlotMesLocation`. Do not infer SiteCode from PartNo or equate SlotId with LocationNo.

PCTP part-extension records (`FVN_PartIdentity`, `FVN_PartOperationalProfile`, `FVN_PartCustomerRule`, FIFO configuration) must reference `IFS.FVN_InventoryPart` with both SiteCode and PartNo. New return/abnormal/delivery C# writes must supply SiteCode and the canonical PartNo; the current source repositories do not yet do so.

`019` intentionally makes `usp_FVN_Slot_SaveLots` a projection-only operation: it replaces QR/LOT rows only when aggregate quantities equal MES on-hand at the mapped location. It does not post stock movements. Post stock changes through the MES inventory posting contract first.

`021` adds `dbo.usp_FVN_ItemFifoConfig_Upsert` and `dbo.vFVN_PCTP_SlotInventory`; `022` adds `dbo.ufn_FVN_NormalizeCustomerLot` and `dbo.usp_FVN_DocQrCode_Insert`. The customer LOT rule is data-driven. Configure it only for confirmed customer/part combinations after mapping source codes to IFS `(SiteCode, PartNo)`.

Migration 024 is **not required to create the empty FVN_PCTP schema**. It creates explicit mapping tables and a read-only preflight procedure; it does not copy or update any B7R2_FCC data.

Migration 025 requires the MES posting procedure from migration 004. It does not depend on optional data-migration preflight 024.

- `026_PCTP_StockMovementGuardAndLotAdjustmentHistory.sql`: creates `dbo.FVN_StockMovementGuard` and `dbo.FVN_StockLotAdjustmentHistory` (replace `PCTPStockMovementGuard` / `PCTPStockLotAdjustmentHistory`). Prerequisite: 025.


## Migration 027 — IFS Warehouse synchronization

Run `027_IFS_WarehouseSyncAndEntityStatus.sql` after migration 026 and after the FVN_ table-name standardization in migration 013. It extends the existing IFS sync batch/staging contract to include `IFSAPP.WAREHOUSE`, applies the warehouse snapshot to `MES.FVN_IfsWarehouse`, and keeps the IFS source read-only. Oracle `CONTRACT`, `WAREHOUSE_ID`, `DESCRIPTION`, `OBJKEY`, and `OBJVERSION` are mapped to SiteCode, WarehouseCode, Description, SourceRowKey, and source-payload version respectively. IFS WAREHOUSE has no active-state column; a row returned by IFS is projected as IsActive=1, and missing rows are not automatically deactivated. The IFS sync form displays per-entity source batch counts, target counts, latest batch state and errors.


## Migration 028 — Supplier master and LOT identity

Migration 028 adds the read-only mirror `IFS.FVN_Supplier` from
`IFSAPP.SUPPLIER_INFO_GENERAL`. Supplier identity is global in IFS and is
therefore keyed by `SupplierCode`, not by PCTP SiteCode. It adds
`SupplierRows` to the sync batch and extends the existing IFS apply procedure.
The PCTP sync reads only `SUPPLIER_ID`, `NAME`, `OBJKEY`, and `OBJVERSION`.

The old Bravo name `Code0` is not used in the new domain model. Use
`SupplierCode` for the exact IFS supplier key and `SupplierId5` for the FVN-owned
five-character LOT token. Component LOTs retain the supplier identity using the FVN-owned SupplierId5 token
and follow this semantic layout:

```text
YYMMDD + SupplierId5 + ItemId5 + DocNo
```

`ItemId5` is a stable five-character MES item identity linked to the IFS
`(SiteCode, PartNo)`; it is not generated differently by each import or screen.
Existing `LegacyQrItemCode` values are preserved. Migration 028 extends the IFS apply procedure to allocate a stable `ItemId5` for every newly synchronized `(SiteCode, PartNo)`; `dbo.usp_FVN_PartIdentity_AllocateItemId5` is the serialized fallback for any part identity not yet assigned. The shared component LOT
builder is `MES.usp_FVN_BuildComponentLotNo`. Production/finished LOTs use a separate builder, `MES.usp_FVN_BuildProductionLotNo`, and the configured 2+3+4-character shift/line/department codes. It requires a supplier already
present in the synchronized IFS supplier master; migration 029 resolves that IFS key
to the stable internal `SupplierId5` before building the LOT.

The supplier foreign key is added WITH NOCHECK to preserve historical Bravo
LOT values while enforcing supplier validity for new/changed values. Reconcile
old supplier codes against IFS before trusting the legacy rows.

The 20-character production/finished-goods LOT is a separate identity domain:
YYMMDD (6) + ItemId5 (5) + ShiftCode (2: shift + gear) + LineCode (3) + DepartmentCode (4). Migration 028 creates `dbo.FVN_ProductionLine` and `dbo.FVN_ProductionDepartment`; configure the active site/line/department mapping before generating production LOTs. `MES.usp_FVN_BuildProductionLotNo` validates the mapping and returns exactly 20 characters.
It does not contain the printed-label sequence or quantity suffix. Do not add
SupplierCode to production/finished-goods LOTs. The QR token may append those
print-only fields, but the stock identity remains the first 20 characters.

Important source prerequisite: confirm the live IFS view metadata for
`SUPPLIER_INFO_GENERAL` before running the first sync. The C# extraction
expects `SUPPLIER_ID`, `NAME`, `OBJKEY`, and `OBJVERSION`; a source-view
column mismatch will stop the batch and record entity `SUPPLIER`.


## Migration 029 — FVN-owned ItemId5 and SupplierId5

Run `029_FVN_InternalItemAndSupplierIdentity.sql` after migration 028.

IFS keys remain unchanged and read-only:
- Item key: `(SiteCode, PartNo)` from `IFS.FVN_InventoryPart`.
- Supplier key: `SupplierCode` = the exact IFS `SUPPLIER_ID` string (including alphanumeric values such as `100002A`).

FVN_PCTP owns separate identity tables:
- `MES.FVN_ItemIdentity`: numeric `ItemIdentityId IDENTITY` plus stable unique `ItemId5` for each `(SiteCode, PartNo)`.
- `MES.FVN_SupplierIdentity`: numeric `SupplierIdentityId IDENTITY` plus stable unique `SupplierId5` for each IFS `SupplierCode`.

The 5-character tokens are FVN-generated base-36 identifiers in an alphabetic namespace (`A0001` etc.) so they cannot collide with migration 028's legacy numeric `ItemId5` values. Existing `ItemId5` values issued by migration 028 are copied without change. New tokens are allocated under application locks and are never recycled. Supplier names are refreshed from the IFS mirror; the IFS `SUPPLIER_ID` is never cast to a number.

Migration 029 replaces the component LOT builder to use:
`YYMMDD + SupplierId5 + ItemId5 + DocNo`.
Production LOT remains exactly 20 characters:
`YYMMDD + ItemId5 + ShiftCode(2) + LineCode(3) + DepartmentCode(4)`.

The compatibility procedure `dbo.usp_FVN_PartIdentity_AllocateItemId5` delegates to the new MES identity allocator. New application code should call the canonical MES procedures and must not generate the 5-character values itself.


## Migration 030 — Canonical ItemId5 allocation

Run `030_CanonicalItemIdentityInIfsApply.sql` after migration 029. Migration 028 originally embedded a second allocator in the IFS apply procedure and wrote new item tokens directly into `dbo.FVN_PartIdentity`. Migration 029 introduced the authoritative `MES.FVN_ItemIdentity` master, but the old IFS apply block must also be replaced to prevent two allocation authorities. Migration 030 patches `IFS.usp_ApplySyncBatch` so it delegates missing item tokens to `MES.usp_FVN_AllocateItemId5`. The old `dbo.FVN_PartIdentity` remains only as a compatibility projection for legacy readers; its trigger rejects changes to already-issued tokens. No IDENTITY columns are added to every IFS, BOM, inventory, or transaction table: the internal identity keys live only in the two dedicated identity masters.


## Migration 031 — MES-owned component purchase orders and LOT release

Run `031_MES_ComponentPurchaseOrderReleaseAndLabels.sql` after migration 030. Component purchase orders and their statuses are owned entirely by MES_PCTP; no IFS purchase-order status is read or required. IFS is referenced only as the read-only Site/Part/Supplier master source. Excel import creates/updates MES-owned DRAFT orders; validation moves an order to VALIDATED. Only the MES release procedure issues a stable component LOT per order line and a persistent `FVN1|<LOTNO>` QR payload. Releasing an already released order is idempotent. Reprinting records an audit row and returns the exact original LOT and QR payload; it never allocates a new LOT. Receipt processing must consume this issued LOT and post stock through the established MES stock-movement contract; this migration does not directly update inventory balance or claim to implement the WinForms Excel/scan UI.


## Migration 032 — Component LOT receiving

Run `032_MES_ComponentLotCanonicalReceiptPosting.sql` after 031. It links each MES-issued component QR LOT to `MES.FVN_MaterialLot` at release time (initially BLOCKED), then posts every accepted receiving quantity through `dbo.usp_FVN_PCTP_PostStockMovement`. Partial receipts are supported across multiple receipt documents; the receipt-line idempotency key prevents duplicate posting. The procedure requires an active `dbo.FVN_SlotMesLocation` mapping for the selected Site/Slot. It does not write inventory balances directly. Use `MES.usp_FVN_ComponentReceipt_Post` for the receiving transaction.


## Migration 033 — Production shifts, BOM demand and PART-to-WIP issue

Run `033_MES_ShiftPlanningBomDemandAndPartToWip.sql` after migration 032. Configure each active Site's shift codes in `MES.FVN_ProductionShift` and map physical Slots to exactly one inventory area using `dbo.FVN_InventoryAreaSlot` (`PART` or `WIP`). Upload rows carry Site, PlanNo, production date, shift, line, product, quantity and UOM; each upload batch is committed atomically. Demand generation reads the synchronized IFS BOM header/alternative/component snapshots, rejects missing or ambiguous effective alternatives, and calculates net requirements against unreserved PART-area on-hand. It does not use unverified cost-bucket staging. Component issue posts a single MES `MOVE` transaction from PART/AVAILABLE to WIP/WIP through the existing PCTP stock adapter; the stock ledger and balances remain the sole quantity authority.


## Production shift setup

Migration 033 intentionally does not assume a universal shift calendar. Configure the real Site-specific shifts before importing a plan, for example:

```sql
INSERT MES.FVN_ProductionShift(SiteCode,ShiftCode,ShiftName,StartTime,EndTime,CrossesMidnight)
VALUES(N'<SITE>',N'<SHIFT_CODE>',N'<SHIFT_NAME>','07:00','15:00',0);
```

Use the actual times and codes approved by production; overnight shifts set `CrossesMidnight=1`. Map each physical slot to exactly one inventory area after checking its MES location mapping:

```sql
INSERT dbo.FVN_InventoryAreaSlot(SiteCode,SlotId,AreaCode,CreatedBy)
VALUES(N'<SITE>',<SLOT_ID>,'PART',N'<USER>');
-- Configure a separate destination slot as WIP:
INSERT dbo.FVN_InventoryAreaSlot(SiteCode,SlotId,AreaCode,CreatedBy)
VALUES(N'<SITE>',<WIP_SLOT_ID>,'WIP',N'<USER>');
```

Do not assign one slot to both areas. Shift setup and PART/WIP area assignment are explicit configuration, not inferred from slot or warehouse names.


## Migration 034 — production execution and component genealogy

Run `034_MES_ProductionExecutionRoutingAndComponentTrace.sql` after 033.
It extends the existing MES production-order/material and genealogy structures;
it does not create a second stock ledger. Production output LOTs are canonical
`MES.FVN_MaterialLot` records created through
`MES.usp_FVN_BuildProductionLotNo`.

Before releasing production orders, configure:
- `MES.FVN_ProductionPartPolicy`: each internally produced (SiteCode, PartNo)
  must be explicitly classified as `WIP` (manufactured subassembly) or `FG`
  (finished product). A component with this policy is `MAKE`; a component
  without it is treated as purchased `BUY`. MAKE demand reads available stock
  from the configured WIP/FG area and is never issued from PART.
  The policy is checked when a production LOT is created.
- `MES.FVN_ProductionLotCodeMap`: map the plan's production line code to the
  three-character LOT line code and four-character department code.
- `MES.FVN_ProductionRouting` and `MES.FVN_ProductionRoutingStep`: configure
  the effective operation sequence and `DIRECT` / `VIA_WIP` transfer mode.
  Exactly one final step must have `IsFinalOutput=1`; all earlier steps must
  have `IsFinalOutput=0`. The selected routing revision is snapshotted on
  the production order so later route edits do not rewrite an active order.
- Map WIP/FG physical Slots in `dbo.FVN_InventoryAreaSlot` and
  `dbo.FVN_SlotMesLocation`. Migration 034 adds the `FG` area.

`DIRECT` records the same production LOT handoff without a stock posting.
`VIA_WIP` posts output into the WIP Slot and issues that same LOT when the next
operation starts. Actual component consumption posts an ISSUE to the canonical
MES ledger and inserts the matching `MES.FVN_FinishedLotGenealogy` edge.
This supports multi-level trace because a manufactured subassembly LOT is
itself the finished LOT of its own production order and the component LOT of
its parent order.

The 20-character LOT builder has no order sequence component. Therefore only
one production order can exist for the same Site + Part + ProductionDate +
Shift + mapped Line + Department identity. The create procedure and filtered
unique index reject a second order with the same LOT identity; combine the
plan quantity into one plan line/order for that identity.

The PCTP production receipt lookup is switched to
`dbo.vFVN_MES_ProductionReceiptWorklist`; the production repository no longer
reads Bravo `vNhapTP`. The existing PCTP receipt service still performs the
physical receipt through the canonical stock movement service, posting
`PROD_RECEIPT` with reference type `NHAP_TP` into MES AVAILABLE stock. The
worklist derives received quantity from those MES ledger transactions and
current balance. The worklist exposes only completed FG production orders;
finished product is not pre-received into WIP, preventing double stock when
PCTP receives it.

Trace report: `MES.usp_FVN_ComponentLot_Trace` supports `UPSTREAM` (produced
LOT to its component LOTs, recursively) and `DOWNSTREAM` (component LOT to
all produced LOTs using it, recursively). Quantities shown are actual
genealogy allocations, not BOM planned quantities.

Important: migration 034 does not configure site-specific routes, part output
policies, line/department mappings, or WIP/FG slots automatically. These are
master-data decisions and must be configured before production execution.


### Multi-level manufactured components

The demand generator is level-aware for the immediate BOM: a child with an
active `FVN_ProductionPartPolicy` is identified as MAKE and its availability
is read from WIP/FG, while an unclassified child is BUY and reads PART.
MAKE lines cannot be posted through PART → WIP; actual use must be recorded
with `usp_FVN_ProductionMaterial_Consume`, which issues the exact child LOT
and writes the parent-to-child genealogy edge.

BOM demand generation remains one direct IFS Structure level per plan line.
A plan line is released only when exactly one effective routing revision and
one final operation are configured. The 20-character LOT format has no order
sequence; a second order with the same product/date/shift/line/department key
is rejected.
A manufactured child must have its own plan line/order and its own operation
execution before its LOT can be consumed by the parent. Automatic recursive
subassembly order scheduling is not inferred because each child may require a
different production line, shift, route and effective schedule. The genealogy
report itself traverses all recorded levels recursively.

## Migration 035 — per-product labels prepared at each process

Run `035_MES_PerUnitProcessLabels.sql` after migration 034. Each physical
product receives a unique MES `LabelCode`; the label references, but never
replaces, the existing 20-character production LOT. Labels are reserved for
the selected routing operation when production prepares that process. The
target quantity is the cumulative number of labels required for that LOT and
operation; increasing it creates only the additional labels.

Use `MES.usp_FVN_ProductionLabel_Prepare` before printing,
`MES.usp_FVN_ProductionLabel_Print` to authorize initial print/reprint,
`MES.usp_FVN_ProductionLabel_Apply` after scanning the label at the process,
and `MES.usp_FVN_ProductionLabel_Void` to cancel an unused label with a
mandatory reason. Applied labels cannot be voided. Reprints return the same
LabelCode and QR payload; they do not allocate another product identity.
Label reservations and print/apply/void events are audited independently of
actual production quantities and genealogy.

The WinForms print surface generates a real QR Code from the exact `QrPayload`
using the project's DevExpress barcode controls and prints one 75x50 mm label
per product. The process scan accepts either the scanned LabelCode or the full
`FVN2|LabelCode|LOT|OperationNo` QR payload.


## Migration 036 — production lifecycle workflow

Run `036_MES_ProductionLifecycleWorkflow.sql` after 035. It reuses
`dbo.FVN_sys_WorkflowTransitions` and the existing
`IWorkflowTransitionService`; it does not create a second workflow engine.
The three process codes are:

- `MES_PRODUCTION_PLAN`: 0 IMPORTED → 10 VALIDATED → 20 FIRM; cancellation = 900.
- `MES_PRODUCTION_ORDER`: 0 CREATED → 10 RELEASED → 20 RUNNING ↔ 30 PAUSED
  → 40 COMPLETED → 50 CLOSED; cancellation = 900.
- `MES_PRODUCTION_OPERATION`: 0 WAITING → 10 PREPARING → 20 READY
  → 30 IN_PROGRESS ↔ 40 PAUSED → 50 COMPLETED; cancellation before execution = 900.

The plan/order projections are kept aligned with existing legacy status writers.
Each production LOT gets an independent state row for every active routing step;
therefore one order may have different operations in different states. The
migration adds immutable transition history in
`MES.FVN_ProductionWorkflowEvent` and process execution evidence in
`MES.FVN_ProductionOperationEvent`. Query a plan/order/operation history with
`MES.usp_FVN_ProductionWorkflow_Timeline`.

The application boundary is:
`IProductionWorkflowService` → `IWorkflowTransitionService` →
`IProductionWorkflowRepository` → `MES.usp_FVN_ProductionWorkflow_Transition`.
The SQL procedure rechecks configured transitions and business guards inside
the state/audit transaction. Plan FIRM is guarded by the follow-up migration 038, which generates
component demand for every plan line in the same transaction as the FIRM transition. Order completion is blocked while any LOT has unfinished
routing operations; close is blocked while reserved/printed labels remain.
Operation READY requires prior operations complete and labels prepared.
Actual operation execution remains the source of production quantities and
automatically updates the operation workflow projection.

This migration is additive and does not post stock, consume material, issue
labels or receive finished goods. Those facts remain owned by the existing MES
production, label and canonical inventory procedures. Apply in a test database
first, then configure/verify the shared transition rows and exercise all three
workflow levels before production use.


## Migration 038 — automatic component demand on FIRM

Run `038_MES_FirmPlanAutoComponentDemand.sql` after migrations 033 and 036.
A production plan entering `FIRM` now generates one component-demand document
for each plan line from the effective IFS BOM and planned quantity. The
generation runs inside the plan status transaction: if BOM resolution fails
(missing or ambiguous effective alternative, or no usable component rows), the
FIRM transition is rolled back instead of leaving a firm plan without material
requirements.

The migration adds `MES.usp_FVN_GenerateComponentDemandV2`, an idempotent
entry point used by the plan trigger and the application. Repeated calls return
the existing demand ID rather than creating duplicates. The earlier manual
demand action can therefore be retained as a safe retry/reconciliation action;
it is no longer the normal production workflow. IFS remains a read-only BOM
source and all demand documents remain MES-owned.

Apply migrations 033, 036 and 038 in order on a test database. Then validate
both the successful FIRM transition and rollback behavior with a plan whose
IFS BOM is missing or ambiguous. Migration 038 has not been executed by the
application developer; database deployment and validation remain an operator
step.


## Migration 039 — trusted Document Runtime metadata

Run `039_MES_DocumentMetadataRegistry.sql` after migration 038. It creates
presentation metadata tables for document definitions, layouts, fields,
toolbar commands and configuration audit. These tables are not an executable
workflow or SQL-command registry.

The trusted C# `PCTP.Framework.Documents.DocumentRegistry` remains the
allow-list for module codes, schema-qualified read views, workflow codes,
command codes, permission codes and stored-procedure bindings. SQL metadata
may configure captions, field order, editor types, visibility and layout only.
The runtime must reject a SQL definition that does not exactly match the
registered module/view/workflow/command contract. Never execute SQL text,
procedure names or expressions supplied by metadata or by the client.

Migration 039 intentionally does not seed a document definition until the
actual production read views and command procedures have been verified against
the existing MES schema. It does not grant broad DML permissions; deployment
must assign only the least-privilege administration and runtime permissions
after reviewing the existing SEC/database-role model.


## Migration 046 — production orders require a FIRM plan

Run `046_MES_ProductionOrderRequiresFirmPlan.sql` after migrations 036 and 038.
The production execution service checks the selected plan line is FIRM before
creating an order. Migration 046 adds a SQL trigger on new production orders
as a second enforcement boundary, so direct SQL or another client cannot
create an order from an imported/validated/nonexistent plan line.

This trigger does not create orders, alter BOM snapshots or post inventory; it
only rejects an insert that is not linked to a FIRM plan line. The existing
`MES.usp_FVN_ProductionOrder_CreateFromPlan` remains the order creation path.


## Migration 047 — MES Production Flow Control Tower

Run `047_MES_ProductionFlowControlTower.sql` after 036, 038 and 046.
It adds the read-only `MES.PRODUCTION_FLOW` permission catalog entry, the
`MES.FVN_ProductionDepartmentRelation` configuration table and the
`MES.usp_FVN_ProductionFlow_Dashboard` read model.

The dashboard combines canonical plan, component-demand, production-order,
production-LOT, operation-workflow and latest workflow-event facts. It does
not update business status, issue inventory or execute transitions.

Grant `MES.PRODUCTION_FLOW / VIEW` and data scope deliberately:
- Site-level scope allows a site-wide read-only overview.
- Department-level scope limits the owner departments visible to the user.
- Component-department visibility is derived from the IFS BOM alternative
  snapshotted on each FIRM plan line's component-demand header.
- Additional upstream/downstream/shared visibility can be configured in
  `MES.FVN_ProductionDepartmentRelation` using owner and related product PartNo.
- A related department row is marked as read-only context in the dashboard.
The form's task-navigation buttons are independently enabled only when the
user has the corresponding workflow VIEW permission; the destination service
continues to enforce action permissions and data scope.

Configure department/product dependencies using actual site, department and
PartNo values from the plant. Do not copy the illustrative INSERT in the
migration without replacing its sample values. After applying the migration,
assign `MES.PRODUCTION_FLOW / VIEW` and Site/Department scopes in the existing
security administration screen.


## Migration 048 — require fulfilled component demand before order creation

Run `048_MES_ProductionOrderRequiresFulfilledComponents.sql` after 047.
The production execution service gives an early validation message; the SQL
trigger is the final guard and rejects an order insert if the FIRM plan line has
no active component-demand header or any component line still has
`NetRequiredQty > IssuedQty`. It does not post inventory or change the demand;
the existing PART → WIP issue workflow must fulfill it first.


## Migration 049 — Control Tower BOM visibility and change-version polling

Run `049_MES_ProductionFlowChangeVersionAndBomVisibility.sql` after 047.
This is intentionally an ALTER migration: it replaces the dashboard procedure
for databases where 047 is already registered, removes the invalid outer-alias
reference from the PlanFlow CTE, and derives upstream department visibility
from effective IFS BOM rows without requiring a ComponentDemandHeader.

It also creates `MES.FVN_ProductionFlowChangeVersion` and change triggers for
plan, plan-line, component demand/header lines, production order, LOT,
operation workflow and workflow events. The desktop client polls only this
single version value every five seconds and reloads the dashboard projection
only when the value changes. This is database-backed cross-machine signaling;
it is not a push channel. It intentionally avoids per-client full stored
procedure refreshes on every timer tick.

DB57 verification after applying 049:

```sql
SELECT ChangeVersion, ChangedAtUtc
FROM MES.FVN_ProductionFlowChangeVersion
WHERE ChangeVersionId=1;

EXEC MES.usp_FVN_ProductionFlow_Dashboard
    @SiteCode = N'<SITE>',
    @FromDate = NULL,
    @ToDate = NULL,
    @DepartmentScopeXml = N'<Scopes><Department>*</Department></Scopes>';
```

Use a real Site and a least-privilege department scope when verifying access.
Do not use the wildcard in production role tests.


## Migration 050 — Component Demand Document views and metadata

Run `050_MES_ComponentDemandDocumentViewsAndMetadata.sql` after 049 and 039. It creates read-only Component Demand header/detail views and seeds approved presentation metadata for `COMPONENT_DEMAND`. Before running 050, verify all five metadata tables created by 039 exist. If migration 039 is already recorded but one or more tables are missing, rerun `039_MES_DocumentMetadataRegistry.sql`; the repair-safe script recreates missing tables without duplicating the migration ledger row. Migration 050 does not grant arbitrary SQL execution rights or replace the C# Trusted Registry.

## Migration 051 — Document Designer (metadata write path)

Run `051_MES_DocumentDesignerMetadataWrite.sql` after 050. It adds the RBAC function `ADMIN.DOCUMENT_DESIGNER` (VIEW = open/read/audit, EDIT = save presentation, APPROVE = declare a code-registered document or complete missing rows) and three procedures: `MES.usp_FVN_DocumentMetadata_Save`, `_Provision`, `_ListAudit`. Every procedure re-checks the actor's permission in SQL through `MES.fn_FVN_DocumentDesigner_HasAction` (role grants + user overrides), so hiding a button is never the control. No role is granted automatically: grant the function in the security administration screen first.

`Save` is update-only (caption, sequence, width, display format, visibility), uses `ConfigVersion` optimistic concurrency under `UPDLOCK, HOLDLOCK`, requires every field to be a real column of the registered view and writes before/after JSON to `MES.FVN_DocumentConfigAudit`. It cannot add fields/commands/layouts or change editor, read-only, required, lookup, status restriction, view or workflow. `Provision` only accepts `MES.vw_FVN_*` views that exist, seeds fields read-only and never overwrites existing rows. The C# registry remains the allow-list: a mismatch still disables the document at startup.


## Migration 053 — PCTP UI Designer / Trusted Registry enforcement

Run `053_MES_UiDesignerTrustedRegistryOnly.sql` after 051 and, if already applied, 052. Migration 052 introduced a SQL-created screen declaration route; that route is retired because SQL metadata must not expand the C# Trusted Registry. Migration 053 quarantines module codes created through that route, removes its create/list-view/list-function procedures and declaration table, and changes menu metadata to reference only the existing seeded `MES.FVN_DocumentDefinition` catalogue. It adds `MES.usp_FVN_UiScreenMenu_List` and `MES.usp_FVN_UiScreenMenu_Save` for presentation-only menu placement. It does not register new screens or authorize arbitrary views/procedures/workflows.

## Migration 054 — menu configuration concurrency

Run `054_MES_UiDesignerMenuConcurrency.sql` after 053. It replaces the menu-save procedure with optimistic concurrency on `FVN_DocumentDefinition.ConfigVersion`, under `UPDLOCK, HOLDLOCK`. A stale designer receives SQL error 55131, which the C# menu store maps to `DocumentConfigConflictException`; changed menu metadata increments the shared configuration version and writes before/after audit. The migration changes presentation-only menu columns and cannot expand the Trusted Registry. Apply on DB57 after 053, then test with two designer sessions: save from the first, and verify the second must reload before saving.


## Migration 055 — shell runtime menu configuration

Run `055_MES_UiMenuRuntimeList.sql` after 054. It adds the read-only `MES.usp_FVN_UiScreenMenu_Runtime` procedure used by the desktop shell. It returns only configured menu placement and visibility for active, non-quarantined definitions. The C# `DocumentMenuProvider` applies a second allow-list check against registered modules and each screen's VIEW permission; SQL cannot create a shell item or supply an event handler. If the procedure is unavailable or the query fails, the shell keeps its standard navigation. After applying 055 on DB57, sign in with users who have and do not have the relevant screen VIEW permission, then verify menu visibility, group and sequence.

## UI Layout Designer (Bravo style) - migrations 056-058

| Version | File | Contents |
|---|---|---|
| 056 | 056 MES UiLayoutDesigner.sql | Approved data sources (`MES.FVN_UiDataSource`), structured layouts (`FVN_UiScreen`, `FVN_UiScreenColumn`, `FVN_UiScreenChild`, `FVN_UiScreenFilter`), audit, procedures (`usp_FVN_UiDataSource_*`, `usp_FVN_UiScreen_Create/Load/Save`), seed screen `COMPONENT_PO` (Phiếu đặt hàng linh kiện) |
| 057 | 057_MES_UiScreenCatalog.sql | `usp_FVN_UiScreen_List`: approved screens the actor may open (drives the shell menu only) |
| 058 | 058_MES_UiScreenAuditList.sql | `usp_FVN_UiScreen_ListAudit`: read-only change history for the designer "LỊCH SỬ" button |

Run in order after 055, in FVN_PCTP. Rules: a layout names only columns of an approved `MES.vw_FVN_*` view (re-validated against `sys.columns` on every save); the permission that guards reading a source belongs to the source row and is set by APPROVE; the Site column is mandatory; quick filters are column + operator + (closed token list | typed literal); there is no SQL text, expression, procedure, DLL or class name in any layout table. Layout screens are read-only; commands stay registered in C#.

After applying on DB57: sign in with a user holding `WAREHOUSE.COMPONENT_PO` VIEW and a SITE scope, open Kho → Phiếu đặt hàng linh kiện, check the quick filters, the two child tabs and the grouping by status; then with a user holding `ADMIN.DOCUMENT_DESIGNER` EDIT right-click → Khai báo layout, change a caption/width, save, open from a second session to see the conflict message, and check LỊCH SỬ.

## UI Menu Designer (Bravo "Tùy chọn các mục trình đơn") - migration 059

| Version | File | Contents |
|---|---|---|
| 059 | 059_MES_UiMenuProfiles.sql | `MES.FVN_UiMenuProfile`, `FVN_UiMenuProfileRole`, `FVN_UiMenuNode`; procedures `usp_FVN_UiMenuProfile_List/ListRoles/Load/Create/Save/Runtime`; seed profile `DEFAULT` (empty = standard shell menu) |

Run after 058, in FVN_PCTP. A profile is a tree (max 3 levels, 300 nodes) of GROUP and ITEM nodes. An ITEM stores only `ItemKey`, the Name of a menu item that already exists in the C# shell (or of a dynamic UI screen item). The shell resolves keys against the items it really has, drops unknown keys and empty groups, and never makes visible an item that RBAC hid, so the menu cannot gain a screen, command or permission from SQL. A profile may be assigned to roles (a role belongs to one profile); a user with no matching role gets `DEFAULT`. VIEW on `ADMIN.DOCUMENT_DESIGNER` opens the designer, EDIT saves or creates a profile; `usp_FVN_UiMenuProfile_Runtime` needs only an active signed-in user. History is in `MES.FVN_UiScreenAudit` with ScreenCode `MENU:<ProfileCode>` (read with `usp_FVN_UiScreen_ListAudit`).

After applying: Quản trị hệ thống -> Thiết kế menu, drag items into groups, Lưu, restart the app and sign in; then assign a profile to a role and check a user of that role sees it while another user still sees DEFAULT / the standard menu.

## UI Menu Designer - priority, delete profile, full catalogue - migration 060

| Version | File | Contents |
|---|---|---|
| 060 | 060_MES_UiMenuProfileOrderDelete.sql | `FVN_UiMenuProfile.ProfileOrder` (1-9999, default 100); `usp_FVN_UiMenuProfile_SetOrder`, `usp_FVN_UiMenuProfile_Delete`, `usp_FVN_UiMenuProfile_ListScreens`; `List`, `Load`, `Runtime` re-created |
| 061 | 061_MES_ComponentPackSpecAndQrLabels.sql | `MES.FVN_ComponentPackSpec`; `usp_FVN_ComponentPackSpec_Upsert`; `usp_FVN_ComponentPO_Release` re-created (one QR LOT per pack, stops listing parts without an active pack spec); `usp_FVN_ComponentPO_LabelData`, `usp_FVN_ComponentPO_LabelLog` |
| 062 | 062_MES_ComponentReceiptIdempotencyAndLabelLog.sql | `usp_FVN_ComponentReceipt_Post` re-created: replay of an already-posted IdempotencyKey (same site/QR/quantity) returns the original document instead of failing, same key with different content -> 53233; `usp_FVN_ComponentPO_LabelLog` re-created: INITIAL_PRINT logged once per LOT, serialized per PO |
| 063 | 063_MES_ComponentPalletLabels.sql | Pallet labels from `MaxCloseQty`: `FVN_ComponentPallet` (+ `FVN_ComponentLot.ComponentPalletId`, `FVN_ComponentPalletLabelHistory`), `usp_FVN_ComponentPO_PalletBuild / PalletData / PalletLabelLog`, `usp_FVN_ComponentPallet_Lots` (QR `FVN2\|<PalletNo>`); REPRINT allowed for CLOSED PO |
| 064 | 064_MES_ComponentPackSpecImportBravo.sql | `usp_FVN_ComponentPackSpec_ImportBravo`: loads `FVN_ComponentPackSpec` from Bravo `dbo.B20ItemQuyCach` (CustomerCode→SupplierCode, ItemCode→PartNo, Quantity→PackQty, MaxCloseQty, IsActive; IsGroup rows skipped), dry-run by default, lists rows it cannot load with the reason |

Run after 059, in FVN_PCTP, **before** deploying the matching build (the new designer reads `ProfileOrder`). A user whose roles sit in several profiles gets the profile with the smallest `ProfileOrder` (ties: `ProfileCode`). Delete removes the profile, its nodes and role links (roles fall back to `DEFAULT`); a JSON copy stays in `MES.FVN_UiScreenAudit` (`MENU:<ProfileCode>`, action `DELETE`). `ListScreens` lists every active approved UI screen for the designer's left list, whatever the designer may open; the shell still creates a screen item only for users who may open it.

After applying: open Thiết kế menu, change a profile and press Lưu, close the dialog and check the menu changes without restarting; give two profiles to one user through two roles, set different Ưu tiên and check the smaller one wins after restart; delete a test profile and check its role now gets DEFAULT; sign in as a designer without VIEW on some screen source and check that screen is still in the left list.

### Menu migrations 059/060 - schema drift (Msg 207 on DisplayName / NodeId / ParentNodeId)

Cause: a table named `MES.FVN_UiMenuProfile`, `FVN_UiMenuProfileRole` or `FVN_UiMenuNode` already existed with other columns, and `CREATE TABLE ... IF OBJECT_ID IS NULL` kept it. Fix: re-run `059_MES_UiMenuProfiles.sql` (it now renames the three tables with their constraints and indexes to `*_Legacy_<UTC time>` and re-creates them; no data is deleted, nothing is dynamic SQL), then run `060_MES_UiMenuProfileOrderDelete.sql`. 060 now stops with error 60004 and skips its remaining batches when the structure is still wrong. Menu profiles stored in the legacy tables must be re-entered in the designer (or copied from `*_Legacy_*` by hand).

### Migration 067 - IFS PRODUCTION_LINE sync and production line setup

Apply after 066, in FVN_PCTP. Adds `IFS.FVN_ProductionLine` (copy of IFSAPP.PRODUCTION_LINE, key SiteCode = CONTRACT + ProductionLineCode), the `PRODUCTION_LINE` entity in the selective IFS sync, `MES.vFVN_ProductionLineSetup` and `MES.usp_FVN_ProductionLotCodeMap_Save`.
After applying: run Đồng bộ dữ liệu IFS with PRODUCTION_LINE ticked, then Sản xuất -> Khai báo chuyền sản xuất theo Site to give each IFS line its 3-character LOT line code and 4-character department code (this is what the plan upload screen lists). Typed columns are read from the IFS payload by column name (DESCRIPTION, CALENDAR_ID, TAKT_*, DAILY_OPERATING_MINUTES, ...); if a name differs in your IFS version the typed column stays NULL but the full row is kept in `SourcePayloadJson`.


### Migration 068 - IFS SITE / WORK_CENTER sync and Site-Department-Line link

Apply after 067, in FVN_PCTP. Adds `IFS.FVN_Site` / `IFS.FVN_WorkCenter` (copies of IFSAPP.SITE / IFSAPP.WORK_CENTER), the `SITE` and `WORK_CENTER` entities in the selective IFS sync (own apply procedure `IFS.usp_ApplySiteWorkCenterBatch`; `IFS.usp_ApplySyncBatch` and the `PRODUCTION_LINE` entity from 067 are untouched), `dbo.FVN_ProductionDepartment.WorkCenterNo` and `MES.usp_FVN_SiteDepartmentLine_Link` / `_Unlink`. Sync never creates lines, departments or links by itself.


### Migration 069 - IFS sync runtime SelectedEntities compatibility

Apply after 068 in `FVN_PCTP`. Fixes the schema mismatch between the current application runtime (which uses `IFS.SyncBatch` / `IFS.SyncStageRow`) and migration 037 (which added `SelectedEntities` to `IFS.FVN_SyncBatch`). Adds `SelectedEntities`, `SiteRows`, and `WorkCenterRows` to the runtime batch table and permits the `SITE` / `WORK_CENTER` staging entity names. It is idempotent and does not rename or delete existing sync data.


### Migration 075 - IFS Site / Production Line mapping consistency

Apply after 067 and 068 in `FVN_PCTP`. Rebuilds `MES.vFVN_ProductionLineSetup` so the setup UI shows IFS Site name/presence, PCTP Site activation, all IFS production lines (including unmapped), and existing stale mappings. Adds SQL-side checks to `MES.usp_FVN_ProductionLotCodeMap_Save` and `MES.usp_FVN_ProductionPlan_ImportRow`: IFS Site must be present, IFS line must be active for the same Site, and the MES mapping must be active for the composite key `(SiteCode, PlanProductionLineCode)`. The matching C# build uses IFS descriptions in the upload line selector and disables mapping edits unless the user has `MES.PRODUCTION_LINE_SETUP / CREATE`.

Do not drop similarly named objects: `IFS.FVN_ProductionLine` is the IFS mirror; `dbo.FVN_ProductionLine` is the 3-character LOT-line master; `MES.FVN_ProductionLotCodeMap` is the bridge. `IFS.FVN_Site` is the IFS mirror while `dbo.FVN_PCTPSite` is the PCTP allow-list. They are separate responsibilities, not duplicate tables. Optional stale-map preview/deactivation is in `SQL/FVN_PCTP/05_MES/Diagnostics/ProductionLineMappingInventoryAndCleanup.sql` (read-only by default).

### Migration 076 - Site / Line / Department master data unification

Apply after 067, 068 and 075 in `FVN_PCTP`. Nothing is dropped or merged: `IFS.FVN_Site`, `IFS.FVN_ProductionLine` and `IFS.FVN_WorkCenter` are IFS mirrors (source of truth for names and IFS lines); `dbo.FVN_PCTPSite` is the PCTP allow-list (activation); `dbo.FVN_ProductionLine` / `dbo.FVN_ProductionDepartment` are the LOT line / department masters; `MES.FVN_ProductionLotCodeMap` is the bridge. 076 fills empty `dbo.FVN_PCTPSite.SiteName` from IFS, adds `dbo.vFVN_Site`, adds the missing link `FK_FVN_ProductionLotCodeMap_IfsLine` (`NOCHECK`, so legacy rows stay readable), adds `MES.vw_FVN_MasterDataConsistency` (every remaining disagreement, one row each) and `MES.usp_FVN_MasterData_Reconcile` (preview by default; `@Apply = 1` only creates missing LOT line / department rows implied by active mappings; `@DeactivateStaleMappings = 1` deactivates mappings whose IFS line is gone; never deletes). Run `SELECT * FROM MES.vw_FVN_MasterDataConsistency;` first and review it before using `@Apply`.

### Migration 077 - Work Center / Department / IFS line consistency and Site management

Apply after 076 in `FVN_PCTP`. IFS is the source of truth for the production line of a work center (`IFS.FVN_WorkCenter.IfsProductionLine`); `MES.FVN_ProductionLotCodeMap` maps that IFS line to a LOT line + department. 077 makes the two agree: `MES.vw_FVN_MasterDataConsistency` gains `MAP_DEPT_WORKCENTER_IFS_LINE_MISMATCH` (ERROR) and `DEPT_WORKCENTER_NO_IFS_LINE` (INFO); `MES.vw_FVN_LotCodeMapIntegrity` gains `WORKCENTER_IFS_LINE_MISMATCH`; `MES.usp_FVN_SiteDepartmentLine_Link` refuses a work center whose IFS line differs from an active mapping of the department (error 57700); trigger `MES.trg_FVN_ProductionLotCodeMap_WorkCenterLine` refuses the opposite direction, an active mapping whose department already holds a work center of another IFS line (error 57701). Only rows being inserted / updated are checked; legacy rows stay readable and are listed by the view. Site management: `MES.FVN_PCTPSiteAudit`, `MES.usp_FVN_PCTPSite_Save` (allow / enable / disable / rename; a site can be turned on only when `IFS.FVN_Site` has it, the name comes from IFS) and the SEC function `MES.SITE_SETUP` (grant VIEW / EDIT to the administrators). Menus: **Khai báo Site PCTP** and **Liên kết Work Center IFS ↔ Bộ phận** under *Sản xuất MES*. Run `SELECT * FROM MES.vw_FVN_MasterDataConsistency;` after applying and fix the ERROR rows by hand.

### Migration 078 - Work Center link: department / LOT line codes derived from IFS

Apply after 077 in `FVN_PCTP`. The link dialog no longer asks to type codes or names. Department code (4 chars) = IFS `WorkCenterNo` left-padded with `0` (`19A` -> `019A`); a work center number longer than 4 characters or with characters outside A-Z / 0-9 (for example `1100E`) must still be typed. LOT line code (3 chars) is generated from the IFS production line code by `MES.usp_FVN_LotLineCode_Resolve` (existing LOT line of that IFS line first, otherwise the first 3 letters/digits, with a number replacing the tail on a clash inside the Site) and stored in the new `dbo.FVN_ProductionLine.IfsProductionLineCode` (filtered unique per Site; backfilled from active `MES.FVN_ProductionLotCodeMap` rows that point to one IFS line). Names come from IFS (department = work center description, line = IFS production line description). `MES.usp_FVN_SiteDepartmentLine_Link` keeps its old parameters (now optional) and gains `@IfsProductionLine`; old callers still work. `MES.vw_FVN_WorkCenterLinkCandidates` suggests the padded department code.

### Migration 079 - 5-character line code (reference) <-> 3-character LOT line code

Apply after 078 in `FVN_PCTP`, then optionally `079b_MES_LineCodeCrossRefSeed.sql` (the 140 codes in use today: 84 VN2W, 56 VN4W, with their generated 3-character code; review the 3-character column first). The LOT keeps the 3-character `dbo.FVN_ProductionLine.LineCode`; the 5-character code is only a reference shown beside it. Adds `MES.FVN_LineCodeSitePrefix` (VN2W -> V, VN4W -> 4), `MES.FVN_LineCodeCrossRef` (both codes unique per Site, optional IFS line), `MES.vw_FVN_LineCodeCrossRef` and `MES.usp_FVN_LineCodeCrossRef_Generate` (`@Preview = 1` writes nothing). 5-character standard: prefix + family (2-3 letters) + sequence for numbered machines (`4DC01`, `VSK10`, `4NFP1`), or prefix + 4 characters for a single machine (`VPRDR`). 3-character code: 2 family letters + base-36 sequence (`SK01` -> `SK1`, `SK10` -> `SKA`), or the first free of body characters 123 / 124 / 134 / 234, or typed by hand. `MES.usp_FVN_LotLineCode_Resolve` consults the cross reference first, so linking a work center to an IFS line uses the cross-referenced 3-character code. Menu: **Mã chuyền 5 ký tự <-> 3 ký tự (LOT)** under *Sản xuất MES*.
