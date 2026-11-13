# 00D – SQL Module Evidence & Cross-Domain Review

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Source database:** `B7R2_FCC`  
**Related review:** `SQL/00C_ApplicationUsageOwnershipReview.md`

This file records SQL-module evidence extracted from the checked-in `SQL/00_result/12_Modules.csv` and `SQL/00B_result/00B_03_StaticDependencies.csv`. It is an analysis artifact, not a deployment script.

## 1. SQL objects touching the six operational PCTP tables

| SQL object | Object type | Observed relationship | Migration implication |
|---|---|---|---|
| `getDeliveredQty` | Procedure | Reads and parses LOT allocations from `LUUPHIEUGIAOHANG` | Delivery reporting dependency |
| `NewInsertCommand` | Procedure | Inserts into `LUUPHIEUGIAOHANG` | Legacy SQL writer must be mapped/replaced |
| `NewSelectCommand` | Procedure | Reads `LUUPHIEUGIAOHANG` | Reader dependency |
| `sp_BaoCao_DeliveryTrace` | Procedure | References `LUUPHIEUGIAOHANG` and `StockHistory` | Cross-table delivery/history query |
| `ST_ReportLuuPhieuGiao` | View | Projects delivery columns from `LUUPHIEUGIAOHANG` | Recreate or replace after consumers are mapped |
| `vWDinhDanhPhieuGiao` | View | Builds delivery identity from factory/date/time/PO/STT | Preserve business key semantics |
| `getTonStockTP*`, `loadSTOCKTP*` | Procedures | Read `STOCKTP` stock/production attributes | Stock and production query dependencies |
| `Usp_Qrcode_gheplot*`, `Usp_Qrcode_Update_Stock*` | Procedures | Read/update stock LOT and delivery state | Critical QR/FIFO workflow dependency; inspect exact active versions |
| `YMVN_GHEPLOT` | Table-valued function | Reads `STOCKTP` and calls `capnhapStokTPtmp` | Function/procedure chain and temp stock field semantics must be preserved |
| `sp_GetCurrentStockStatus` | Procedure | References `SlotLot` and `STOCKTP` | Reconciliation between physical slot stock and production stock |
| `usp_Slot_SaveLots` | Procedure | Deletes all `SlotLot` rows for a SlotId, then inserts TVP rows | High-risk replacement writer; transaction and full-set semantics must be preserved |
| `sp_GetStockHistory` | Procedure | Reads `StockHistory` plus Warehouse/Rack/Slot | Stock-history query dependency |
| `usp_Check_Unique_B30AccDoc` | Procedure | References `StockHistory` | Check exact role in voucher/duplicate protection before moving history |
| `Usp_Qrcode_gheplot_Lot20`, `Usp_Qrcode_gheplot_Old13`, `Usp_Qrcode_gheplotView` | Procedures | Reference `IFSPHIEUGIAOHANG` and `STOCKTP` | IFS snapshot is consumed by QR/LOT allocation paths |
| `V_PGH` | View | Unions `IFSPHIEUGIAOHANG` with `TMPPHIEUGIAOHANG` | Preserve order-source staging semantics |

The object names above come from the metadata export; not every similarly named `OLD`, `Tmp`, or dated version is necessarily an active production path. Confirm active call sites and SQL Agent jobs before selecting a migration set.

## 2. Important PCTP ↔ MES coupling

### 2.1 `vNhapTP`

The exported definition documents the following behavior:

- Production receipt rows are sourced from `B30AccDoc` and `vB20Item`.
- The view reads saved stock values from `STOCKTP`.
- `SLSX_DA_LUU` is mapped from `STOCKTP.SLSX`.
- The comment explicitly compares current MES production quantity (`SL_DA_SAN_XUAT`) against the quantity previously saved in `STOCKTP.SLSX` to decide whether a completed LOT may be reopened.

**Conclusion:** `STOCKTP` participates in a cross-domain production-to-warehouse workflow. It cannot be assigned to `FVN_PCTP` or `FVN_MES` based only on its current PCTP usage. Identify the transaction that creates/updates the production receipt, the owner of `B30AccDoc`, and which system owns the authoritative production quantity.

### 2.2 `usp_Trculotnoxuat_linhkien_na`

The exported procedure reads:

- `STOCKTP` for finished-product LOT/production/stock fields;
- `B30AccDoc` for production voucher and finished-product LOT link;
- `B30AccDocInventory` for component material, quantity and finished-product LOT link.

**Conclusion:** this is a concrete MES/Bravo document trace chain. Preserve the voucher-to-LOT relationship and do not duplicate the same production-voucher facts into a second independent source of truth.

### 2.3 `usp_Slot_SaveLots` and `SlotLotTableType`

The exported procedure performs a full replacement of all rows for one SlotId inside a transaction:

1. `DELETE dbo.SlotLot WHERE SlotId = @SlotId`
2. `INSERT dbo.SlotLot ... SELECT ... FROM @Lots`

It consumes `dbo.SlotLotTableType`, which is present in the user-defined type inventory.

**Migration implication:** preserve the table type contract or replace it with an explicitly versioned API. Test concurrent updates, rollback, QR/receipt metadata preservation, and empty-set behavior. Do not change this to row-by-row merge semantics without validating callers.

The checked-in `SQL/A0_SlotLot_Diagnose_Repair.sql` also documents a prior failure mode where a legacy slot rewrite path removed `QrData`, `MaPhieu`, `NgaySX`, and `SoPhieuTong`. Treat these columns as operational trace evidence, not optional display fields.

## 3. Cross-domain placement: provisional matrix

| Table / object group | Operational evidence | Target database decision |
|---|---|---|
| `LUUPHIEUGIAOHANG` and delivery views/procedures | PCTP delivery workflow owns current read/write path | Candidate `FVN_PCTP`; validate consumers and voucher references |
| `IFSPHIEUGIAOHANG`, `IFSPHIEUGIAOHANG_SP` | PCTP-created IFS order snapshots | Candidate `FVN_PCTP` integration/staging |
| `STOCKTP` and stock/production procedures | PCTP delivery/stock workflow plus MES production references | **Unresolved**: evaluate `FVN_MES` as stock/production domain owner |
| `SlotLot`, `SlotLotTableType`, `usp_Slot_SaveLots` | PCTP warehouse slot operations; linked to `STOCKTP` and QR/stock history | **Unresolved**: decide warehouse execution boundary and authoritative stock balance |
| `StockHistory`, `sp_GetStockHistory` | PCTP stock movement/reporting and voucher checks | **Unresolved**: place with the owner of stock movement transactions |

## 4. Required next SQL evidence

Before creating the final migration scope, extract/confirm:

1. Full definitions of all active procedures/functions/views listed above, including referenced objects not present in the 00B static graph.
2. SQL Agent job steps and external scheduler calls that invoke these objects.
3. All write paths for `STOCKTP`, `SlotLot`, and `StockHistory` (C# repositories, procedures, triggers, jobs).
4. Whether `sp_GetStockHistory` and `sp_GetCurrentStockStatus` are active application paths or compatibility/report paths.
5. Actual schema/column definitions, indexes, triggers, and runtime row counts for the six tables, captured from the same database snapshot.
6. Any cross-database and dynamic SQL calls to `B30AccDoc`, `B30AccDocInventory`, `Bravo63`, `Bravo70`, and `msdb`.

Do not migrate a procedure just because it references a table. First determine whether it is active, which application calls it, whether it writes, and whether it belongs to the same domain as the target table.

## 5. Safety boundary

Documentation only. No database DDL/DML, no rename, no data copy, no connection-string change, and no retirement of source objects.
