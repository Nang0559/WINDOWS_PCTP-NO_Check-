# 00E – Source Inventory Findings & Migration Gates

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Source database:** `B7R2_FCC`  
**Target database:** `FVN_PCTP`  
**Review date:** 2026-11-13

## 1. Evidence snapshot

The checked-in 00 inventory and 00C evidence identify the source as SQL Server 2016 (13.0.1742.0), database `B7R2_FCC`. The 00C manifest records a READ_ONLY run at 2026-11-13 15:33:12.700. The evidence is metadata/application-usage evidence; it is not an approval to copy or alter source objects.

Latest 00C approximate row counts:

| Table | Rows |
|---|---:|
| dbo.LUUPHIEUGIAOHANG | 283,975 |
| dbo.STOCKTP | 70,959 |
| dbo.SlotLot | 9,285 |
| dbo.StockHistory | 3,157 |
| dbo.IFSPHIEUGIAOHANG | 13 |
| dbo.IFSPHIEUGIAHANG_SP | 42 |

Counts are approximate and reflect the 00C execution snapshot; earlier 00 output can differ because the source is live.

## 2. Correct ownership conclusion

The application review in `00C_ApplicationUsageOwnershipReview.md` confirms six tables as PCTP operational scope (`PCTP_OWNED`):

- `LUUPHIEUGIAOHANG`: delivery records and delivery trace.
- `STOCKTP`: production LOT and stock/FIFO attributes used by PCTP workflows.
- `SlotLot`: physical slot/LOT balance and QR/location evidence.
- `StockHistory`: stock movement history and voucher/QR/slot evidence.
- `IFSPHIEUGIAOHANG`, `IFSPHIEUGIAOHANG_SP`: PCTP-managed IFS order snapshots/staging.

The heuristic ownership labels in the original 00B export must not override this application evidence. Operational ownership is confirmed; physical placement in FVN_PCTP versus FVN_MES is still a separate architecture decision.

## 3. Schema and dependency findings

### 3.1 Slot/warehouse boundary

- `SlotLot.SlotId` has FK `FK_SlotLot_Slot` to `dbo.Slot(SlotId)`.
- The wider warehouse hierarchy includes `Slot -> Rack -> Warehouse`.
- `SlotLot` carries trace fields that must be retained: `QrData`, `NgaySX`, `SoPhieuTong`, `MaPhieu`, `ParentSoPhieu`, `PhieuStatus`.
- `usp_Slot_SaveLots` uses `SlotLotTableType` and replaces the full set of rows for one SlotId in a transaction. Any redesign must preserve atomic replacement, rollback, concurrency, and trace-field semantics.

**Gate:** do not create a standalone SlotLot migration until the authoritative owner of Slot/Rack/Warehouse and stock balance is decided.

### 3.2 STOCKTP side effects and cross-domain coupling

The 00C trigger export confirms `dbo.trgupdatetblDoctem` on `dbo.STOCKTP`. It:
- updates `STOCKTP.CASX` from LOT text in one insert path;
- reads `B20Item.MinCloseQty`;
- updates `tbl_DocTem.IsCreate`;
- sets `STOCKTP.SATUS` when `SLCONLAI = 0`;
- contains a three-part reference to `[B7R2_FCC].[dbo].[STOCKTP]`.

This trigger is not safe to transplant unchanged into FVN_PCTP. Its business effects must be assigned to the correct production/labeling domain and redesigned without retaining a dependency on the source database name.

The 00D review also identifies:
- `vNhapTP` coupling production receipt quantity to `STOCKTP.SLSX`;
- `usp_Trculotnoxuat_linhkien_na` joining `STOCKTP`, `B30AccDoc`, and `B30AccDocInventory`;
- `sp_GetCurrentStockStatus` reconciling `SlotLot` and `STOCKTP`;
- `sp_GetStockHistory` and `sp_BaoCao_DeliveryTrace` consuming stock history.

**Gate:** retain the voucher-to-LOT lineage, but do not create a second authoritative production-voucher or stock ledger.

### 3.3 Delivery and IFS staging

- `LUUPHIEUGIAOHANG` has a unique index `UQ_LUUPHIEU` across STT, MAHANG, NGAYGIAO, CUA, TRUYEN, SOLUONG, GIOGIAO, NHAMAY. Preserve and validate this natural-key behavior before designing a new document key.
- Both IFS staging tables have 29 columns. Most are `nvarchar(500)`; `SOLUONG` is `decimal(18,4)`. This reflects a permissive snapshot shape, not a final normalized order schema.
- Application evidence shows the snapshot tables are managed/recreated by PCTP during IFS ingestion. Preserve source provenance, refresh/run identity, and the distinction between MP/SP modes.
- The IFS source remains read-only and one-way into FVN_PCTP. Do not write back to IFS.

**Gate:** retain raw/staging compatibility while mapping into normalized order and delivery entities; do not use these 29-column snapshot tables as the final order model.

## 4. Missing or incomplete evidence

The current `SQL/00C_result` folder contains 00C_00 through 00C_06 and 00C_08b, but no `00C_07_SqlAgentJobSteps.csv` or `00C_08a_Synonyms.csv`. These areas are therefore **not evidenced by this package**; absence of files must not be interpreted as zero jobs or zero relevant synonyms.

The main 00 export also contains multi-megabyte module, trigger, and column extracts. The checked-in `Result_Export_SQL2_Complete_DDL_Metadata.txt` is approximately 6.58 MB and needs complete parsing/reconciliation before object-by-object DDL migration is generated. This review does not claim that all SQL objects have been classified.

## 5. Migration decisions and non-decisions

| Area | Current decision |
|---|---|
| Delivery workflow and delivery history | PCTP operational ownership confirmed; candidate for FVN_PCTP after dependency closure |
| IFS order snapshots | PCTP integration/staging ownership confirmed; candidate for FVN_PCTP |
| STOCKTP | PCTP operational use confirmed; target placement unresolved; production/MES boundary required |
| SlotLot and Slot/Rack/Warehouse | operational use confirmed; target placement unresolved |
| StockHistory | operational use confirmed; place with authoritative stock-movement owner |
| B30AccDoc / B30AccDocInventory | source-system/Bravo references; do not clone as an independent accounting subsystem |
| BRAVO7 generic tables and modules | do not bulk migrate by naming convention; require application ownership and dependency evidence |

No source objects have been renamed, copied, dropped, or modified. No data has been copied. No application connection string has been changed.

## 6. Next phase

Proceed with a **migration-scope and dependency-closure register**, not executable production DDL yet:

1. Reconcile the complete SQL2 DDL export with 00 table, column, key, index, trigger, module, and dependency inventories.
2. For each PCTP-used module, record exact object definition, caller/repository, read/write behavior, dynamic SQL, external dependencies, and active/legacy status.
3. Close dependency sets for delivery, IFS staging, production LOT, warehouse slot/LOT, stock history, QR and genealogy.
4. Mark each object as `MIGRATE_AS_IS`, `REWRITE_FOR_FVN_PCTP`, `MOVE_TO_FVN_MES`, `REPLACE_WITH_NEW_MODEL`, `EXCLUDE_WITH_EVIDENCE`, or `PENDING_REVIEW`.
5. Only after the register is reviewed, generate ordered SQL 01/02 deployment scripts with explicit old-to-new object mapping and SQL Server 2016 compatibility.

This document is an analysis artifact only. It is not a deployment script.
