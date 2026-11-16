# 00K — PCTP stored procedure / view cutover map (B7R2_FCC → FVN_PCTP)

**Branch:** `feature/mes-product-genealogy-timeline` · Generated from commit `1e5d715` by static text scan (not a compile, not executed on SQL Server).

## 1. Naming standard (as actually implemented by migrations 004–026)

| Object | Rule | Examples already in the package |
|---|---|---|
| Physical table | `<schema>.FVN_<BusinessName>` | `dbo.FVN_SlotLot`, `MES.FVN_InventoryBalance` |
| Stored procedure, schema `dbo` | `dbo.usp_FVN_<Domain>_<Action>` | `usp_FVN_Slot_SaveLots`, `usp_FVN_PCTP_PostStockMovement`, `usp_FVN_DocQrCode_Insert` |
| Stored procedure, schema `MES` / `IFS` | `<Schema>.usp_<Action>` (schema carries the domain) | `MES.usp_PostInventoryMovement`, `IFS.usp_ApplySyncBatch` |
| View | `dbo.vFVN_<Name>` | `vFVN_STOCKTP`, `vFVN_PCTP_SlotInventory` |
| Scalar/table function | `dbo.ufn_FVN_<Name>` / `MES.fn_<Name>` | `ufn_FVN_NormalizeCustomerLot` |

`06_Programmability/README.md` previously said the `FVN_` prefix applies to physical tables only; migrations 019–025 do not follow that. The README is corrected to match the implemented standard above. Legacy names (`Usp_Qrcode_*`, `sp_Get*`, `Usp_QrReader*`) must **not** be recreated in FVN_PCTP; each is replaced by a renamed rewrite.

Hard rules for every rewrite: SQL Server 2016 syntax; reference only physical `FVN_` tables (no legacy synonyms, no `B7R2_FCC`, no `B20*`); parts keyed by `(SiteCode, PartNo)` from `IFS.FVN_InventoryPart`; no direct quantity writes to `FVN_STOCKTP` / `FVN_SlotLot` — stock changes go through `dbo.usp_FVN_PCTP_PostStockMovement`, then `dbo.usp_FVN_Slot_SaveLots` projection.

## 2. Already present in FVN_PCTP (C# already calls the new name)

| Object | Migration |
|---|---|
| `dbo.usp_FVN_Slot_SaveLots` | 019 |
| `dbo.usp_FVN_PCTP_PostStockMovement` | 025 |
| `dbo.usp_FVN_DocQrCode_Insert`, `dbo.ufn_FVN_NormalizeCustomerLot` | 022 |
| `dbo.usp_FVN_ItemFifoConfig_Upsert`, `dbo.vFVN_PCTP_SlotInventory` | 021 |
| `dbo.vFVN_STOCKTP`, `dbo.vFVN_PCTP_Part`, `dbo.vFVN_PXLB_ProcessReport` | 015 / 014 / 018 |
| `IFS.usp_ApplySyncBatch` | 012 |

## 3. Still called by C# but NOT present in FVN_PCTP (will fail at runtime after cutover)

| Legacy name (C# string) | Call-site files | Proposed new name | Disposition / blocker |
|---|---|---|---|
| `sp_GetCurrentStockStatus` | CurrentStockRepository, StockHistoryRepository, StockOnHandSummary, TONKHOTP(.TonKho) | `dbo.usp_FVN_Report_CurrentStock` | REWRITE (read-only). Legacy source in `LegacyCandidates/Warehouse`. Replace `STOCKTP.SLCONLAI` with MES balance via `vFVN_STOCKTP`/`vFVN_PCTP_SlotInventory`. Output columns must stay identical. |
| `sp_GetStockHistory` | StockHistoryRepository, LotTraceTimelineBuilder, TraceTimelineNode | `dbo.usp_FVN_Report_StockHistory` | **No definition in Git** (code comment says so). Needs 00H Result 5 export. |
| `Usp_Qrcode_LOAD_PHIEU_DOCQR2405` / `…View2405` | PhieuService, PhieuLoadService, PhieuGiaoDbService, PhieuSqlExecutor, PhieuGiaoDBRepository, PhieuTmpRepository, IfsOrderLoadStrategy, GiaoDbOrderLoadStrategy, OrderTableLoadStrategy | `dbo.usp_FVN_Delivery_LoadOrderDocQr` (one versioned contract; View variant becomes a parameter, not a second SP) | REWRITE. Parameter-name difference of the View variant is documented in PhieuSqlExecutor. Needs `FVN_LUUPHIEUGIAOHANG` / IFS snapshot column mapping (016). |
| `Usp_Qrcode_LOAD_HANGTHIEU` / `…View` | PhieuKhoRepository | `dbo.usp_FVN_Delivery_LoadShortage` | REWRITE; preserve shortage calculation. |
| `Usp_Qrcode_Update_Stock2405` | PhieuKhoRepository, PhieuKhoService, IfsOrderLoadStrategy | `dbo.usp_FVN_Delivery_ConfirmStock` | **REDESIGN** (51 KB). Updates STOCKTP quantities directly → must post MES ledger idempotently (025) and write `FVN_LUUPHIEUGIAOHANG`/`FVN_LUUDOCQRCODE`. Needs owner sign-off on idempotency key and PO-HTN handling. |
| `Usp_Qrcode_Take_Lot2405`, `Usp_Qrcode_Take_LotYMVN2405`, `Usp_Qrcode_Take_LotYMVN` | PhieuLotRepository, YMVNDOCQRCODE | `dbo.usp_FVN_Delivery_TakeLot` (+ customer rule from `FVN_PartCustomerRule`) | REWRITE using FIFO policy (020/021) and `ufn_FVN_NormalizeCustomerLot`; cursor logic should become set-based. |
| `Usp_Qrcode_gheplot`, `…gheplotYM1`, `…gheplotYM2` | PhieuService, PhieuLotRepository, GIAOHANGYMN | `dbo.usp_FVN_Delivery_MergeLot` | **REDESIGN** (25 KB, 9× `UPDATE STOCKTP`). Customer-specific Gear branch needs `FVN_Gear` mapping decision. |
| `Usp_gheplotPrint`, `…PrintYM1` | FormGhepLotThuCong, SqlRepository, GIAOHANGYMN | `dbo.usp_FVN_Report_MergeLotLabel` | **No definition in Git.** Needs 00H export. Truncation limits (`@_LOT NVARCHAR(25)`) are enforced in C#. |
| `Usp_BarCodeView_ThongKe_Tmp5`, `…_YMVN`, `Usp_BarCodeView_Ghep_YMVN` | UF_TACHLOT, GIAOHANGYMN | `dbo.usp_FVN_Report_SplitLotLabel` (+ `_Ymvn` parameter) | REWRITE. Depends on `B20item`, `B20Gear`, `B00CtTmp`, `usp_sys_DefaultTable`: replace by `IFS.FVN_InventoryPart` / `FVN_Gear`, and remove the `#CtTmp` clone-from-Bravo-table trick. |
| `Usp_QrReaderLock_*`, `Usp_QrReaderMachine_*` | CustomerQrLockService, QrReaderMachineService | `dbo.usp_FVN_QrReaderLock_*`, `dbo.usp_FVN_QrReaderMachine_*` | RENAME. Already written against `dbo.FVN_QrReaderMachine/Lock` in `LegacyCandidates/QR/PCTP_QrReaderLock*.sql`; mechanical, but must be promoted to an ordered migration and C# strings changed together. |
| `Usp_Qrcode_LUU_PHIEU` | (no C# caller found) | — | RETIRE (`…RetireUnsafeLegacy.sql` already exists). |
| `trgupdatetblDoctem` | — | — | Do **not** copy: references `[B7R2_FCC].[dbo].[STOCKTP]`, `B20Item`. Split into explicit services (00G §4.3). |

Views read directly by C#: `vNhapTP` (8 uses), `vB20Item` (4 uses) have no FVN_PCTP definition. Target: `dbo.vFVN_PCTP_Part` over `IFS.FVN_InventoryPart` for `vB20Item`; `vNhapTP` needs its source definition (not in Git).

## 4. Order of work

1. Mechanical, low risk: QrReader SP rename; `usp_FVN_Report_CurrentStock`.
2. Needs missing source definitions (run 00H, send Results 5–7): `sp_GetStockHistory`, `Usp_gheplotPrint*`, `vNhapTP`.
3. Redesign with owner decisions: `Update_Stock2405`, `gheplot*`, `Take_Lot*`, `LOAD_PHIEU_DOCQR*`.
4. Change C# strings in the same commit as each promoted migration; run on a disposable SQL Server 2016 DB (fresh install, rerun, rollback, concurrency).
