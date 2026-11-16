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

The applied versions are recorded in `MES.FVN_SchemaMigration` after migration 013. Before migration 013, query `MES.SchemaMigration`; after migration 013, use the physical table:

```sql
SELECT MigrationVersion,MigrationName,AppliedAtUtc,AppliedBy
FROM MES.FVN_SchemaMigration ORDER BY MigrationVersion;
```

Run migration 013 only after 000–012 have completed successfully. Run 014–023 only after 013, in strict order.

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
