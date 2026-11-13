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

The applied versions are recorded in MES.SchemaMigration. Verify with:
SELECT MigrationVersion,MigrationName,AppliedAtUtc,AppliedBy FROM MES.SchemaMigration ORDER BY MigrationVersion;

## Architecture rules

- IFS9 is read-only and authoritative for synchronized part/site/location data. MES.Ifs* tables are read models, not manually maintained masters.
- Part and location identity is scoped by SiteCode plus source code until global uniqueness is proven.
- IFS cost bucket data is staged in MES.IfsBomBucketStage and remains PENDING_ENGINEERING_VERIFICATION. It is not an approved production BOM.
- MES.InventoryTransaction is the immutable quantity movement ledger. MES.InventoryBalance is the transactionally maintained projection.
- MES.MaterialExecutionEvent records production execution and references the stock movement; it is not a second stock ledger.
- MES.FinishedLotGenealogy must be linked from actual CONSUME events, never inferred from BOM quantities or planned quantities.
- No price, cost, currency, valuation, GL/AP/AR or accounting posting columns are introduced.
- The scripts create new objects only. They do not drop, rename or alter existing PCTP objects.

## Important deployment limitation

These are schema migrations and core posting/query routines. They are **not yet the legacy data migration** from B7R2_FCC/PCTP. That migration must be built only after source-to-target mapping for STOCKTP, SlotLot, StockHistory, LUUPHIEUGIAOHANG, IFSPHIEUGIAOHANG and IFSPHIEUGIAHANG_SP, including the Slot/Rack/Warehouse dependency closure, is reconciled against the live source metadata and application usage.

## Required validation before production

1. Confirm target context with SELECT DB_NAME() AS TargetDatabase, @@VERSION AS SqlServerVersion.
2. Confirm IFS9 PartNo, SiteCode, warehouse and location key semantics and maximum lengths.
3. Confirm receipt acceptance/quarantine workflow, unit conversion, negative-stock policy, return and adjustment approval.
4. Confirm FIFO priority (expiry-first vs manufacture-date-first) with operations.
5. Confirm the approved Engineering BOM source. Until verified, do not promote cost bucket snapshots.
6. Run migration scripts in a clean non-production database first.
7. Run MES.usp_ValidateInventoryBalance after test movements; it must return no rows.
8. Test duplicate idempotency keys, simultaneous reservations, simultaneous issues, rollback, split/merge and genealogy allocation.
9. Do not give application users direct INSERT/UPDATE/DELETE permission on inventory ledger or balance tables. Grant EXECUTE on approved procedures and SELECT on approved views/functions.

## Known next build items

- Bulk posting procedure that consumes MES.TVP_InventoryMovement atomically with per-row result diagnostics.
- PO receipt posting procedure with over-receipt and duplicate-document policy.
- Production order create/release procedure validating approved BOM, effective dates, site and product.
- Finished Lot receipt procedure and QR/document binding.
- Data migration scripts with source/target counts, rejected-row staging, reconciliation and restart checkpoints.
- Security roles, grants, audit trail and deployment smoke tests.
