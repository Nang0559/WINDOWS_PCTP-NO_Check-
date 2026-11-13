# 00C – Application Usage & Ownership Review (PCTP)

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Source database inventory:** `B7R2_FCC`  
**Target architecture:** `FVN_PCTP` + future `FVN_MES`

> This review corrects the earlier heuristic classification. The six tables below are actively used by the PCTP application. Their legacy naming is not evidence that they are BRAVO7-owned. Treat them as **PCTP operational scope / PCTP_OWNED** for the ownership inventory. The final physical destination (`FVN_PCTP` or `FVN_MES`) remains a separate domain-boundary decision.

## 1. Confirmed application evidence

| Current table | Application evidence in this branch | Usage evidenced | Ownership decision |
|---|---|---|---|
| `LUUPHIEUGIAOHANG` | `BaoCao/Infrastructure/Repositories/DeliveryTraceRepository.cs`; `GiaoHangKhach/Repositories/PhieuKhoRepository.cs`; `PhieuLuuTruRepository.cs`; `TableOrderRepo.cs` | Delivery record read, trace and QR association; insert/update delivery rows; delivery report lookup | `PCTP_OWNED` – confirmed operational |
| `STOCKTP` | `BaoCao/Infrastructure/Repositories/ProductionLotTraceRepository.cs`; `GiaoHangKhach/Repositories/PhieuKhoRepository.cs`; `PhieuLotRepository.cs`; `PhieuValidationRepository.cs`; `TableOrderRepo.cs` | Production LOT lookup; stock balance/FIFO validation and deduction; delivery processing | `PCTP_OWNED` – confirmed operational |
| `SlotLot` | `BaoCao/Infrastructure/Repositories/StockHistoryRepository.cs`; `LotLocationRepository.cs`; `KhoCore/Repositories/SlotRepository.cs`; `KhoCore/Repositories/BulkStockSlotRepository.cs` | Physical slot/LOT balance; location; QR evidence enrichment; stock movement | `PCTP_OWNED` – confirmed operational |
| `StockHistory` | `BaoCao/Infrastructure/Repositories/StockHistoryRepository.cs`; SQL dependency inventory | Stock movement history; QR, voucher and slot evidence; report and trace | `PCTP_OWNED` – confirmed operational |
| `IFSPHIEUGIAOHANG` | `PCTP/Shared/Models/CustomerTableConfig.cs`; `GiaoHangKhach/Repositories/PhieuTmpRepository.cs`; `PhieuRepository.cs`; SQL dependency inventory | Configured IFS order snapshot/staging table used by delivery workflow; stored procedures and view depend on it | `PCTP_OWNED` – confirmed PCTP staging |
| `IFSPHIEUGIAOHANG_SP` | `PCTP/Shared/Models/CustomerTableConfig.cs`; `GiaoHangKhach/Configuration/GiaoHangKhachConfiguration.cs`; `PhieuTmpRepository.cs` | Configured SP order snapshot/staging table for Honda delivery workflow | `PCTP_OWNED` – confirmed PCTP staging |

### Evidence details

1. **Delivery rows:** `DeliveryTraceRepository` reads `dbo.LUUPHIEUGIAOHANG` and joins QR evidence from `dbo.LUUDOCQRCODE`. `PhieuKhoRepository` inserts delivery rows into `LUUPHIEUGIAOHANG`. `PhieuLuuTruRepository` reads and updates the same delivery table.
2. **Production LOT and stock:** `ProductionLotTraceRepository` reads production attributes from `dbo.STOCKTP`. Delivery processing also reads and deducts `STOCKTP` under transaction/locking safeguards. This is not merely an old report table.
3. **Slot inventory:** `StockHistoryRepository` supplements missing QR/location evidence from `dbo.SlotLot`; `LotLocationRepository` reads `SlotLot` to resolve warehouse/rack/slot. Slot repositories implement the operational slot/LOT path.
4. **Stock history:** `StockHistoryRepository` reads `dbo.StockHistory` directly and uses `sp_GetStockHistory` as a compatibility fallback if the direct query fails. The repository comment documents that the procedure does not expose all QR/voucher/slot fields, so direct table access is intentional.
5. **IFS snapshots:** `CustomerTableConfig.cs` configures `IfsTable = IFSPHIEUGIAOHANG` and `IfsTableSP = IFSPHIEUGIAOHANG_SP` for Honda Vietnam. `PhieuTmpRepository.PushIfsSnapshot` validates the configured table name, drops/recreates the snapshot table from the loaded data shape, then bulk inserts the IFS result. These are PCTP-managed staging snapshots, not the IFS system of record.

## 2. Static SQL dependency evidence

The checked-in `SQL/00B_result/00B_03_StaticDependencies.csv` contains references including:

- `getDeliveredQty`, `getDOCQRCODEDAT`, `sp_BaoCao_DeliveryTrace`, `ST_ReportLuuPhieuGiao`, `Usp_Qrcode_LOAD_PHIEU_DOCQR*`, `Usp_Qrcode_LUU_PHIEU`, and delivery views referencing `LUUPHIEUGIAOHANG`.
- `getTonStockTP*`, `loadSTOCKTP*`, `Usp_Qrcode_gheplot*`, `Usp_Qrcode_Update_Stock*`, `ST_Report`, `vSTOCKTP`, and `trgupdatetblDoctem` referencing `STOCKTP`.
- `sp_GetCurrentStockStatus` references both `SlotLot` and `STOCKTP`.
- `sp_GetStockHistory` and `sp_BaoCao_DeliveryTrace` reference `StockHistory`.
- `Usp_Qrcode_gheplot_Lot20`, `Usp_Qrcode_gheplot_Old13`, `Usp_Qrcode_gheplotView`, `V_PGH` and related objects reference `IFSPHIEUGIAOHANG`.

Static dependency metadata does not reliably reveal dynamic SQL. The absence of a literal `IFSPHIEUGIAOHANG_SP` reference in the static dependency export does not negate the application configuration and dynamic table usage described above.

## 3. Corrected ownership classification

For the final ownership register, replace the previous heuristic label `PCTP_LEGACY_SCOPE_REVIEW` for these six tables with:

- `ProposedOwnership = PCTP_OWNED`
- `ReviewStatus = CONFIRMED_APPLICATION_USAGE`
- `EvidenceSource = APPLICATION_CODE + SQL_DEPENDENCY` (for tables with both evidence types)
- For `IFSPHIEUGIAOHANG_SP`, use `APPLICATION_CONFIG + DYNAMIC_TABLE_WRITE`; static dependency metadata is not a complete detector of this path.

Do not infer ownership from row counts or table prefixes.

## 4. Keep ownership separate from target database placement

PCTP operational ownership is now confirmed for these six tables. This does **not** mean all six must automatically be created in `FVN_PCTP`.

| Data responsibility | Placement decision to validate |
|---|---|
| Delivery documents and PCTP delivery workflow | Likely `FVN_PCTP`, subject to delivery module boundary and dependencies |
| Production LOT attributes and production stock balances (`STOCKTP`) | Evaluate `FVN_MES` as the production/inventory domain owner |
| Physical warehouse slot/LOT state (`SlotLot`) | Evaluate whether warehouse execution remains PCTP-owned or is a MES inventory subdomain |
| Stock movement history (`StockHistory`) | Place with the owner of stock movement transactions; do not duplicate as a second source of truth |
| IFS order snapshots (`IFSPHIEUGIAOHANG*`) | PCTP integration/staging scope; preserve source-system provenance and refresh semantics |

The above placement column is a design review, not a final migration instruction. Before moving a table, map all writers, readers, procedures, views, triggers, SQL Agent jobs, external integrations, transaction boundaries, and cross-database references.

## 5. Immediate next investigation

1. Extract complete definitions and call chains for the active SQL objects touching these six tables.
2. Inspect runtime configuration for all customer/order modes (MP, SP, Giao DB, IFS and table-order paths).
3. Identify the actual write owner for `StockHistory` and all `SlotLot` mutations, including bulk movement and stored procedures.
4. Review dynamic SQL and SQL Agent job steps not captured by `sys.sql_expression_dependencies`.
5. Produce `00_ObjectOwnership_Final.csv` and `00_MigrationScope.csv` only after the above evidence is reconciled.

## 6. Safety boundary

This review is documentation only. It does not rename, copy, delete, or modify any database object or data, and it does not change application connection strings.
