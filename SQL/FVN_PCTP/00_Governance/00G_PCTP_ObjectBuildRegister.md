# 00G — PCTP physical objects build register

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Target:** `FVN_PCTP`, SQL Server 2016  
**Decision basis:** ADR-001 accepted 2026-11-16  
**Status:** build register; not a deployment script

## 1. Purpose and release rule

This register is the execution contract for rebuilding the PCTP database while preserving the proven PCTP business architecture. It consolidates the SQL2-generated table evidence, the 00C active-table snapshot, the application runtime inventory, the static SQL dependency export, the existing MES/IFS migrations and the reviewed XuLyHangLoi/Warehouse/Delivery candidates.

The final package must contain the complete approved PCTP schema and programmability in `SQL/FVN_PCTP`. The historical module SQL folders are evidence/source only.

Do not run this register as SQL. A physical object is deployable only after its source definition, target owner, dependencies, application callers and SQL Server 2016 behavior have been checked.

## 2. Non-negotiable architecture

1. Every physical user table in the target database uses `FVN_<BusinessName>`, including customer-specific, QR, warehouse and rework tables. Do not create old-name physical tables.
2. Keep the existing PCTP business document model and workflow semantics. Do not replace delivery documents, return documents, abnormal-processing records, QT Chung records or QR evidence with inventory ledger rows.
3. MES owns the only authoritative LOT identity, stock balance and stock movement ledger:
   - `MES.FVN_MaterialLot`
   - `MES.FVN_InventoryBalance`
   - `MES.FVN_InventoryTransaction`
4. `FVN_STOCKTP` may retain production LOT attributes and legacy fields needed for compatibility/reconciliation, but its quantity fields must not become a second authoritative on-hand balance.
5. `FVN_SlotLot` must retain slot association and all trace fields. Its quantity is a location-level projection/reconciliation value, not a second independent inventory authority. An explicit SlotId-to-MES location map is required.
6. `FVN_StockHistory` must preserve historical evidence and legacy references, but all new stock movements must post to MES exactly once. Decide whether this object becomes a read-only archive, a compatibility view, or a non-quantity evidence extension before creating active writers.
7. Do not clone the BRAVO accounting/GL/AP/AR subsystem. Shared master dependencies such as B20 item/gear/department data require an explicit read-only mirror or source integration decision; they must not be copied merely because a PCTP query joins them.
8. IFS remains a one-way source. Preserve batch, source and raw-payload provenance.
9. New C# SQL, dynamic table resolution, TVP callers, SqlBulkCopy and MERGE targets must use the physical FVN_ names. No new code may depend on migration-013 compatibility synonyms.
10. SQL Server 2016 syntax only. Do not use `STRING_AGG`.

## 3. Table source inventory and target naming

The SQL2-generated schema evidence contains 61 table definitions. The five B20* tables below are shared master/reference dependencies and are deliberately not approved for blind cloning. The remaining names are target candidates; their status is not the same as deployment approval.

| Source/evidence name | Required target physical name | Disposition / gate |
|---|---|---|
| B20Dept | No direct clone approved | Shared reference; decide FVN department mirror/source |
| B20Gear | No direct clone approved | Shared reference; decide FVN gear mirror/source |
| B20ImageStore | No direct clone approved | Shared image service; determine whether PCTP still needs it |
| B20Item | No direct clone approved | Shared item master; map required fields (including MinCloseQty) to approved FVN/IFS reference contract |
| B20ItemQuyCach | No direct clone approved | Shared packaging master; map to approved FVN item-packaging reference |
| docqrcode | dbo.FVN_DOCQRCODE | Convert QR schema and callers; preserve QR fields and customer scope |
| docqrcode_100003 | dbo.FVN_DOCQRCODE_100003 | Customer-specific variant; determine whether to retain separate table or consolidate with CustomerCode |
| docqrcode_sp | dbo.FVN_DOCQRCODE_SP | SP variant; preserve mode distinction |
| FVN_DeliveryCommandLedger | dbo.FVN_DeliveryCommandLedger | Keep; verify idempotency contract and relation to delivery command |
| FVN_DeliveryCommandQr | dbo.FVN_DeliveryCommandQr | Keep; child of delivery command ledger |
| FVN_HangChoGiao | dbo.FVN_HangChoGiao | Keep; verify queue lifecycle and source links |
| FVN_HangChoGiaoPickDetail | dbo.FVN_HangChoGiaoPickDetail | Keep; validate parent keys and pick semantics |
| FVN_ItemFifoConfig | dbo.FVN_ItemFifoConfig | Keep; verify FIFO policy contract |
| FVN_ItemFifoConfig_History | dbo.FVN_ItemFifoConfig_History | Keep; preserve audit history |
| FVN_PCTPQuarantineReleaseAudit | dbo.FVN_PCTPQuarantineReleaseAudit | Keep; reconcile with MES LOT quality state |
| FVN_PhieuTraHang | dbo.FVN_PhieuTraHang | Keep; preserve return header contract |
| FVN_PhieuTraHangCT | dbo.FVN_PhieuTraHangCT | Keep; preserve return detail and abnormal-processing link |
| FVN_PhieuXuLyBatThuong | dbo.FVN_PhieuXuLyBatThuong | Keep; core abnormal-processing header |
| FVN_PhieuXuLyBatThuongAffectedLot | dbo.FVN_PhieuXuLyBatThuongAffectedLot | Keep; preserve affected-LOT snapshot |
| FVN_PhieuXuLyBatThuongQCInitial | dbo.FVN_PhieuXuLyBatThuongQCInitial | Keep; preserve initial QC evidence |
| FVN_Purchase_Order_HVNDB | dbo.FVN_Purchase_Order_HVNDB | Keep; verify HVN order contract |
| FVN_PXLB_Compensation | dbo.FVN_PXLB_Compensation | Keep; preserve compensation workflow |
| FVN_PXLB_CompensationMovement | dbo.FVN_PXLB_CompensationMovement | Keep; preserve compensation movement audit; must not duplicate MES stock ledger |
| FVN_PXLB_Disposition | dbo.FVN_PXLB_Disposition | Keep; preserve disposition decision |
| FVN_PXLB_ReworkMovement | dbo.FVN_PXLB_ReworkMovement | Keep as rework process evidence; post stock effects through MES |
| FVN_PXLB_ReworkPlan | dbo.FVN_PXLB_ReworkPlan | Keep; preserve rework plan |
| FVN_PXLB_ReworkQC | dbo.FVN_PXLB_ReworkQC | Keep; preserve rework QC |
| FVN_PXLB_WorkflowAudit | dbo.FVN_PXLB_WorkflowAudit | Keep; immutable workflow audit |
| FVN_sys_WorkflowTransitions | dbo.FVN_sys_WorkflowTransitions | Keep; add missing status reference table and reviewed seeds |
| FVN_TraHangQTChungGiao | dbo.FVN_TraHangQTChungGiao | Keep; preserve QT Chung delivery evidence |
| FVN_TraHangQTChungNhapNG | dbo.FVN_TraHangQTChungNhapNG | Keep; preserve NG receipt evidence |
| FVN_TraHangQTChungQC | dbo.FVN_TraHangQTChungQC | Keep; preserve QC evidence |
| FVN_TraHangQTChungXuat | dbo.FVN_TraHangQTChungXuat | Keep; preserve export and OperationId idempotency |
| inspectionconfig | dbo.FVN_InspectionConfig | Convert name; verify active C# callers and item master mapping |
| inspectionlog | dbo.FVN_InspectionLog | Convert name; include reviewed inspection log DDL |
| luudocqrcode | dbo.FVN_LUUDOCQRCODE | Convert name; preserve delivery QR history |
| luuphieugiaohang | dbo.FVN_LUUPHIEUGIAOHANG | Convert name; active delivery table; preserve unique natural-key behavior and customer isolation |
| purchase_order_htn | dbo.FVN_Purchase_Order_HTN | Convert name; verify HTN customer runtime use |
| purchase_order_ymvn | dbo.FVN_Purchase_Order_YMVN | Convert name; verify YMVN runtime use |
| qrcode_changetime | dbo.FVN_QRCODE_CHANGETIME | Convert name; verify YMVN time-change flow |
| rack | dbo.FVN_Rack | Convert name; retain physical warehouse hierarchy |
| slot | dbo.FVN_Slot | Convert name; retain physical slot identity; map to MES location explicitly |
| slotlot | dbo.FVN_SlotLot | Convert name; retain QrData, NgaySX, SoPhieuTong, MaPhieu, ParentSoPhieu and PhieuStatus |
| sp_docqrcode | dbo.FVN_SP_DOCQRCODE | Convert name; preserve SP QR flow |
| sp_tmpphieugiaohang | dbo.FVN_SP_TMPPHIEUGIAOHANG | Convert name; preserve SP staging semantics |
| stockhistory | dbo.FVN_StockHistory | Ownership is MES movement ledger; decide archive/evidence projection before allowing writes |
| stocktp | dbo.FVN_STOCKTP | Convert name; preserve production LOT attributes; quantity authority remains MES |
| tbl_qr_comparepart | dbo.FVN_TBL_QR_ComparePart | Convert name; verify current QR compare callers |
| tbl_qr_may_docqr | dbo.FVN_TBL_QR_May_DocQR | Convert name; verify machine QR reader flow |
| tmplotghep | dbo.FVN_TMPLOTGHEP | Convert name; verify current LOT merge callers and trace contract |
| tmplotgheptest | dbo.FVN_TMPLOTGHEPTest | Pending review; do not deploy until proven runtime, likely test/legacy |
| tmplottach | dbo.FVN_TMPLOTTACH | Convert name; verify LOT split callers and trace contract |
| FVN_Purchase_Order_DB_HD | dbo.FVN_Purchase_Order_DB_HD | Keep; delivery DB header |
| tmpphieugiaohang | dbo.FVN_TMPPHIEUGIAOHANG | Convert name; delivery staging |
| tmpphieugiaohang_100002 | dbo.FVN_TMPPHIEUGIAOHANG_100002 | Convert name; YMVN/customer staging |
| tmpphieugiaohang_100003 | dbo.FVN_TMPPHIEUGIAOHANG_100003 | Convert name; HTN/customer staging |
| tmpphieugiaohang_sp | dbo.FVN_TMPPHIEUGIAOHANG_SP | Convert name; SP staging |
| FVN_Purchase_Order_DB_CT | dbo.FVN_Purchase_Order_DB_CT | Keep; delivery DB detail |
| warehouse | dbo.FVN_Warehouse | Convert name; retain physical warehouse hierarchy |
| ymvn_docqrcode | dbo.FVN_YMVN_DOCQRCODE | Convert name; preserve YMVN QR flow |
| ymvn_tmpphieugiaohang | dbo.FVN_YMVN_TMPPHIEUGIAOHANG | Convert name; preserve YMVN delivery staging |

### 3.1 Additional tables outside the 61-table SQL2 allowlist

These appear in the PCTP object checklist/runtime inventory and need exact DDL/source confirmation before inclusion:

| Observed name | Target direction | Required evidence |
|---|---|---|
| IFSPHIEUGIAOHANG | dbo.FVN_IFSPHIEUGIAOHANG or normalized IFS order staging | 00C column contract and current IFS sync caller |
| IFSPHIEUGIAOHANG_SP | dbo.FVN_IFSPHIEUGIAOHANG_SP or normalized IFS order staging | 00C column contract and SP caller |
| IFSPHIEUGIAOHANG_100002 / _100003 | Customer-keyed normalized staging or FVN_ variants | Full customer config and dynamic SQL caller |
| IFSPHIEUGIAOHANGView | Determine whether this is a table-backed snapshot or a view | Exact object definition and caller |
| PCTPStockMovementGuard | dbo.FVN_PCTPStockMovementGuard | Convert candidate DDL and verify all writers/idempotency contract |
| TMPPHIEUGIAOHANGDB / TMPPHIEUGIAOHANGDB_IFS | FVN_ equivalents or consolidated staging | Exact DDL and delivery DB caller |
| TMPPHIEUGIAOHANGView_DK_PC21 | FVN_ equivalent or replace with a real view | Exact DDL, lifecycle and host/customer isolation |

These must not be guessed from their names. The two 29-column IFS snapshot tables have a permissive legacy shape; retain their source provenance and MP/SP/customer distinctions when normalizing.

## 4. SQL views, procedures, functions and triggers

The 00C static dependency export identifies the following PCTP-linked programmable objects. Static dependencies are not proof that every version is active; caller and definition review is required.

### 4.1 Views

| Source view | Target decision |
|---|---|
| ST_Report | Rewrite as PCTP report view over FVN_ objects and MES quantity authority |
| ST_ReportLuuPhieuGiao | Rewrite; preserve delivery history/report semantics |
| V_PGH | Rewrite against normalized FVN_ IFS/order staging |
| vFirstStock | Review caller; rewrite or archive with evidence |
| vNhapTP | Rewrite; receipt quantity must reconcile with MES posting |
| vSTOCKTP | Rewrite as a compatibility/report projection; no independent quantity authority |
| vWDinhDanhPhieuGiao | Rewrite; preserve delivery identity and trace fields |
| View_DanhSachHoaDon20262708 and dated/OLD variants | Determine current caller; keep one approved view, archive superseded versions |

### 4.2 Stored procedures and table-valued function

| Source procedure/function | Target decision |
|---|---|
| capnhapStokTPtmp | Review current caller; rewrite only if still used |
| getDeliveredQty | Rewrite over FVN delivery documents |
| getDOCQRCODEDAT | Rewrite over FVN QR tables |
| getTonStockTP / getTonStockTP_0402 / getTonStockTP_Full / getTonStockTP_old / getTonStockTPNew | Consolidate to one approved read-only stock projection over MES; archive obsolete variants |
| loadSTOCKTP / loadSTOCKTPtotal | Replace with controlled FVN production LOT import/read path; do not overwrite MES balance |
| NewInsertCommand / NewSelectCommand | Verify whether generated legacy adapter objects are still referenced |
| sp_BaoCao_DeliveryTrace | Rewrite; retain voucher/LOT/QR/slot evidence |
| sp_GetCurrentStockStatus | Rewrite to read MES balance and explicit Slot mapping |
| sp_GetStockHistory | Rewrite as history projection from MES plus preserved legacy evidence |
| ups_YMVN_GHEPLOT_SLIP | Verify YMVN workflow and preserve parent/child LOT trace |
| usp_Check_Unique_B30AccDoc | Do not clone BRAVO accounting; retain only a PCTP-owned validation contract if callers still require it |
| usp_HVN_HisLot | Rewrite if active; preserve HVN delivery/LOT history |
| Usp_Qrcode_gheplot and YMVN/PHONG/Lot20/Old13/New20/View variants | Consolidate only after identifying the active QR/LOT caller per customer; archive OLD variants |
| Usp_Qrcode_LOAD_HANGTHIEU | Rewrite if active; preserve shortage calculation |
| Usp_Qrcode_LOAD_PHIEU_DOCQR and 123/2405/OLD/R2/View variants | Identify active caller; consolidate to one versioned QR document-load contract |
| Usp_Qrcode_LOAD_PHIEU_HVNTRA | Rewrite if active; preserve customer return QR contract |
| Usp_Qrcode_LUU_PHIEU | Rewrite as atomic QR/document persistence; preserve duplicate prevention and transaction behavior |
| Usp_Qrcode_Update_Stock and 1/2/2405/OLD variants | Replace stock side effects with idempotent MES posting; do not keep parallel STOCKTP/SlotLot balances |
| usp_Slot_SaveLots | Preserve TVP contract or version it; retain atomic replace/rollback and concurrency behavior |
| usp_Trculotnoxuat_linhkien_na | Review dependency on B30AccDoc/B30AccDocInventory; do not clone BRAVO accounting subsystem |
| YMVN_GHEPLOT (table-valued function) | Rewrite only after confirming active YMVN caller and LOT semantics |

### 4.3 Trigger

`trgupdatetblDoctem` must not be copied as-is. Its source definition updates `[B7R2_FCC].[dbo].[STOCKTP]`, reads `B20Item.MinCloseQty`, writes `tbl_DocTem.IsCreate`, and changes STOCKTP status. Split these responsibilities into explicit FVN/MES production and label-generation services/procedures. Any required `tbl_DocTem` object needs its own ownership and schema evidence.

## 5. Ordered build sequence

The deployment package will be built in these phases, with migration numbers continuing after the existing MES/IFS 000–013 chain:

| Phase | Deliverable | Dependencies |
|---|---|---|
| A | Final table ownership and source-to-target column map | SQL2 metadata, 00C evidence, C# callers, ADR-001 |
| B | PCTP base/master and workflow tables | Approved local reference/master contract |
| C | Warehouse hierarchy and LOT/location mapping | MES location contract; no duplicate quantity ledger |
| D | Delivery, MP/SP/customer staging, QR and order tables | CustomerTableConfig and delivery repository map |
| E | XuLyHangLoi/QT Chung base + extension tables | Base return/abnormal tables before PHASE2/3/4–14 |
| F | PK/FK/default/check/index/TVP contracts | All table definitions |
| G | Views, functions, stored procedures and triggers | Final physical FVN_ table names |
| H | Controlled data migration and reconciliation | Mapping, checkpoints, reject handling, checksums |
| I | C# caller conversion and regression | No old physical table names or compatibility-synonym dependencies |
| J | Fresh-install, rerun, rollback, concurrency and workflow acceptance | Disposable SQL Server 2016 database |

## 6. Evidence gaps that block a safe complete installer

1. The checked-in 00C module-reference export is empty; the static dependency export provides names/references, not complete definitions.
2. The SQL Agent job-step and synonym exports are absent from the evidence package.
3. Some runtime tables in the object checklist are not in the 61-table generated SQL2 allowlist; their full column DDL is not present here.
4. Shared B20* master dependencies need a target source/mirror decision.
5. `FVN_STOCKTP`, `FVN_SlotLot`, and `FVN_StockHistory` require caller conversion and an explicit MES posting/projection contract before enabling writes.
6. `usp_Slot_SaveLots` requires its exact table-valued parameter definition and repository call contract to be migrated together.
7. The source trigger references `B7R2_FCC` and `tbl_DocTem`; it is not portable.
8. The existing 61-table SQL2-generated DDL is a schema-evidence/CI artifact, not an approved clean-install baseline. It must be filtered, renamed, dependency-closed and tested before promotion.

## 7. Acceptance criteria

- Every deployed physical table starts with `FVN_`; no legacy physical table names remain.
- All approved source columns, nullability, precision/scale, identity/default behavior, PK/FK/unique/check/index rules and TVP contracts are preserved or explicitly mapped.
- All PCTP receipt, issue, FIFO, QR, delivery, MP/SP, return, abnormal-processing, QC, rework and compensation workflows pass regression.
- Every stock-affecting confirmation posts one idempotent MES transaction in the same atomic business boundary (or a reliable outbox contract); retries never double the balance.
- No second writable stock balance or movement ledger exists.
- No new C# SQL references legacy table names or compatibility synonyms.
- Fresh install and rerun pass on disposable SQL Server 2016; source `B7R2_FCC` remains unchanged.
- Cutover is not approved until reconciliation and rollback evidence is recorded.


## 8. Full source-definition extraction

The checked-in `Result_Export_SQL2_Complete_DDL_Metadata.txt` contains useful schema metadata, but its programmable-object section is only a short SQL-definition preview (the stored-procedure/view bodies are truncated). The 00C module-reference CSV in this branch is empty. Therefore, the exact production definitions for all legacy PCTP views/procedures/functions/triggers are not yet available as deployable source.

A read-only export script has been added:

`00_Governance/SourceExtractionTools/00H_Export_PCTP_RuntimeObjectDefinitions.sql`

Run it only while connected to `B7R2_FCC`. It checks the database name and performs SELECT-only metadata extraction. Export all seven result sets, especially Result 4 (`FullSqlDefinition`), Result 5 (table-valued types) and Result 6 (trigger bodies). Do not execute any generated DDL from the export. This is the missing source evidence required to rewrite the PCTP views and stored procedures safely.
