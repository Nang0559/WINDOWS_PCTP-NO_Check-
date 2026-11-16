# ADR-001 — Preserve PCTP workflows and unify the inventory ledger

**Status:** Accepted by project owner in conversation on 2026-11-16  
**Target database:** `FVN_PCTP` (SQL Server 2016)  
**Repository branch:** `feature/mes-product-genealogy-timeline`

## 1. Decision

The project owner approved the unified inventory ownership direction:

- `MES.FVN_MaterialLot` is the canonical LOT identity and genealogy record.
- `MES.FVN_InventoryBalance` is the canonical on-hand/reserved balance by site, part, LOT, location and stock state.
- `MES.FVN_InventoryTransaction` is the canonical immutable stock movement ledger.
- PCTP warehouse and business-document tables remain separate where they represent real business concepts: receipt/issue/return/delivery/QT Chung/abnormal processing/QR evidence, etc.
- A business document is not replaced by an inventory transaction. Posting/confirming a document creates its corresponding inventory transaction in the same controlled transaction boundary (or via a durable outbox/idempotent posting workflow where cross-system boundaries require it).

## 2. Preserve proven PCTP behavior

The current PCTP flows for TP receipt, TP issue, QR, delivery, and abnormal/rework processing are considered operationally proven and must be preserved.

The normalization is not permission to redesign those workflows. Preserve, unless evidence proves a defect:

- business columns and their meanings;
- primary/business keys and document numbering behavior;
- status codes and transition rules;
- quantity calculations, FIFO checks and validation rules;
- customer/order isolation (including MP/SP variants);
- QR payloads and trace fields;
- source-document references and audit fields;
- transaction boundaries, idempotency and error behavior.

Any necessary structural extension must be additive and justified by an explicit mapping. Do not rename business columns, change status semantics, change quantity units, or remove fields merely to make the model look uniform.

## 3. Physical naming policy

Every physical user table in `FVN_PCTP`, in every schema, must use the `FVN_<BusinessName>` prefix.

- Keep schemas such as `dbo`, `MES`, and `IFS` as domain boundaries.
- New DDL, C#, EF mappings, raw SQL, dynamic SQL, SqlBulkCopy, MERGE targets, TVPs and stored procedures must reference the physical `FVN_` table names.
- Do not use legacy-name synonyms in new code. Existing synonyms created by migration 013 are temporary compatibility only and must be inventoried and retired after every caller is migrated.
- The prefix applies to physical tables, not automatically to views, procedures, functions, triggers, constraints, indexes or table types.
- Keep SQL Server 2016 compatibility; do not use `STRING_AGG`.

## 4. Source-to-target inventory ownership

| Existing PCTP source concept | Canonical target responsibility | Decision |
|---|---|---|
| `STOCKTP` | LOT identity and TP-specific production attributes | Preserve its full business shape as an FVN_-named PCTP extension or detail linked to `MES.FVN_MaterialLot`; do not maintain a second on-hand balance. |
| `SlotLot` | Physical placement/slot association and trace attributes | Preserve slot, QR, voucher and parent-lot trace fields. Quantity authority moves to `MES.FVN_InventoryBalance`; slot assignment is represented in the canonical location/balance model. |
| `StockHistory` | Historical source-document/movement evidence | Preserve the full audit/trace payload and map each stock-affecting event to one canonical `MES.FVN_InventoryTransaction`. Do not create a competing balance ledger. |
| `Warehouse`, `Rack`, `Slot` | Physical warehouse/location hierarchy | Retain as distinct FVN_-named master/location tables where the MES location catalog does not already express the PCTP-specific physical hierarchy; define an explicit key mapping to MES locations. |
| PCTP receipt/issue/QT Chung/delivery tables | Business documents and workflow records | Retain their structures and workflow semantics; post inventory transactions exactly once when confirmed. |

This mapping is a design direction, not yet a finalized column-by-column migration. In particular, no automatic equivalence is assumed between legacy `SlotId` and `MES.LocationNo`; a stable mapping must be defined and validated.

## 5. Required posting contract

Every stock-affecting PCTP operation must:

1. validate the business document and its current status;
2. resolve the canonical site, part, LOT and location;
3. create one deterministic idempotency key from the source document, line/event and operation;
4. write the canonical `MES.FVN_InventoryTransaction`;
5. update `MES.FVN_InventoryBalance` under a concurrency-safe transaction;
6. persist the posting reference on the source business record or a dedicated posting-link table;
7. commit document status and stock posting atomically where they share one SQL database transaction;
8. reject duplicate retries without applying quantity twice;
9. retain enough source information to reverse a movement using a compensating transaction, never by deleting the original ledger row.

The exact movement types, sign conventions, units, state transitions and transaction procedure contracts must be derived from current PCTP code and source database evidence before implementation.

## 6. Migration and code conversion rules

- Do not directly rewrite or execute the historical scripts under `PCTP/Modules/XuLyHangLoi/Database` as a clean installer.
- Promote their business DDL into ordered migrations under `SQL/FVN_PCTP`, after supplying missing base-table definitions and reconciling all later phase patches.
- Create fresh-install migrations in dependency order. Keep data backfill/repair scripts separate from clean-install DDL.
- Preserve source database `B7R2_FCC`; migration is one-way and must be staged, restartable, reconciled and reversible at the application cutover level.
- Change code only after a table/column/caller inventory is complete. Do not perform blind global string replacements.
- Update table configuration, repositories, TVPs, procedures, triggers, bulk-copy destinations and connection configuration together.
- The application must not be switched to `FVN_PCTP` until all required modules and regression tests pass.

## 7. Implementation phases

1. Freeze a source schema and code evidence snapshot for the working branch.
2. Inventory every physical table, column, PK/FK, index, check/default, trigger, view, procedure, TVP, dynamic SQL reference and C# caller.
3. Produce an explicit source-to-target mapping and preserve the current operational contracts.
4. Build PCTP Core and physical warehouse/location masters.
5. Integrate LOT/balance/movement posting with the existing MES ledger; do not create duplicate stock tables.
6. Build delivery, QR, MP/SP and XuLyHangLoi migrations.
7. Migrate programmable objects and application code to physical FVN_ table names.
8. Add staged data migration and reconciliation.
9. Test clean install, rerun, idempotency, concurrency, FIFO, receive, issue, return, QR, MP/SP, customer isolation, abnormal processing, rework, compensation, delivery and rollback on disposable SQL Server 2016.
10. Cut over only after acceptance; keep the source database unchanged until sign-off.

## 8. Explicit non-goals

- No deletion of operational fields merely because they are duplicated or legacy-looking.
- No conversion of business documents into ledger rows as a substitute for document history.
- No second authoritative LOT quantity, slot balance or movement ledger.
- No source database rename or destructive cleanup.
- No claim of SQL Server execution/build success until those tests are actually run.

## 9. Acceptance criteria

- All physical tables in `FVN_PCTP` start with `FVN_`.
- All application database references use physical FVN_ table names; old-name synonyms are no longer required.
- The canonical MES ledger is the only source of truth for stock quantity and movements.
- Existing PCTP workflows preserve their observable behavior and traceability.
- Every legacy source object is classified as KEEP / REWRITE / MERGE / ARCHIVE / EXCLUDE with evidence.
- Fresh install and migration tests pass on SQL Server 2016, with reconciliation proving no quantity/document loss.
