# FVN_PCTP SQL consolidation — phase completion report

**Branch:** `feature/mes-product-genealogy-timeline`  
**Consolidation commit:** `41874d21ea731cb5ee9c49db11527e2f2f4ec373`  
**Follow-up commits:** `94fa5e0c222fe79223ebfe1d7df4353aa157c58d`, `661cd8e65c68ee13f69f93001e2950b63fcca694`, `b3a04f5608ec2e30d238a869a82e8d2f85324ef7`

## 1. Completed in this phase

- Consolidated the loose SQL root into `SQL/FVN_PCTP/`.
- Moved 124 source/evidence/tooling/test SQL-related files while preserving their Git blob content.
- Created the canonical domain folders: Governance, Baseline, MasterData, Warehouse, Delivery, MES, Programmability, DataMigration, Security, Tests and Archive.
- Moved the versioned MES migration package 000–011 and its smoke test under the FVN_PCTP package.
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
