# 00I — Replace Bravo B20Item with the IFS InventoryPart master

**Repository:** `Nang0559/WINDOWS_PCTP-NO_Check-`  
**Branch:** `feature/mes-product-genealogy-timeline`  
**Target:** `FVN_PCTP`, SQL Server 2016  
**Source evidence:** user-provided `00H_Export_PCTP_RuntimeObjectDefinitions_result.csv` (170,812,932 bytes; eight concatenated result sets)  
**Status:** approved architecture direction; composite key `(SiteCode, PartNo)` implemented in migrations 014–022; live site/warehouse/location seed mapping remains a deployment gate

## 1. Owner clarification

PCTP must not recreate or depend on the Bravo item master `B20Item`. The authoritative part master for PCTP is the IFS-synchronized `IFS.FVN_InventoryPart`.

The existing MES projection `MES.FVN_IfsInventoryPart` remains the MES-side projection used by MES inventory/genealogy. It is not a second PCTP-owned item master. `IFS.FVN_PartCatalog` may supply global catalog attributes where the site-specific InventoryPart does not carry them.

Do not create physical target tables named `B20Item`, `B20Dept`, `B20Gear`, `B20ImageStore` or `B20ItemQuyCach`. Do not create a local duplicate table containing a second copy of IFS part description/UOM/status.

## 2. Existing IFS master contract already in the repository

After migration 013, the physical IFS tables are:

- `IFS.FVN_InventoryPart`: site-specific operational part. Primary key `(SiteCode, PartNo)`.
- `IFS.FVN_PartCatalog`: global part catalog keyed by `PartNo`.
- `MES.FVN_IfsInventoryPart`: MES projection keyed by `(SiteCode, PartNo)`.

The source migration 012 defines the IFS InventoryPart contract with these business attributes:

`SiteCode`, `PartNo`, `Description`, `UnitMeas`, `PartStatus`, `AccountingGroup`, `AssetClass`, `PartProductCode`, `PartProductFamily`, `PlannerBuyer`, `SupplyCodeDb`, `TypeCodeDb`, `StockManagementDb`, `AvailabilityActivityStatusDb`, `LifecycleStageDb`, `InventoryValuationMethodDb`, `NegativeOnHandDb`, `CreateDate`, `LastActivityDate`, `DeclineDate`, `ExpiredDate`, plus source provenance and sync columns.

The PartCatalog carries global `Description`, `InfoText`, `UnitCode`, `PartMainGroup`, lot/serial tracking attributes, weight/volume and source provenance.

The IFS sync procedure in migration 012 already maps IFS InventoryPart to MES projection, including description, UOM, type/status and lot/serial tracking. Migration 013 rewrites the physical MERGE targets to the FVN_ names. Reuse this sync chain; do not introduce a second IFS ingestion pipeline.

## 3. Bravo source evidence from the uploaded export

The export includes exact source columns for the five Bravo master tables. These facts describe the old source only; they are not approval to recreate those tables.

### 3.1 B20Item — 55 columns

PCTP-linked module definitions actually use these fields:

| Bravo field | Observed PCTP usage | New source / disposition |
|---|---|---|
| `Code` (nvarchar(100)) | Joins to STOCKTP.PART, delivery ItemCode, receipt item code and LOT procedures | `IFS.FVN_InventoryPart.PartNo`, scoped by SiteCode |
| `Name` (nvarchar(384)) | Printed item name in barcode/trace reports | `IFS.FVN_InventoryPart.Description` |
| `Unit` (nvarchar(16)) | Used by a generic open-inventory/warehouse routine | `IFS.FVN_InventoryPart.UnitMeas` |
| `Id` (int IDENTITY) | Encoded as a five-character item identifier in legacy QR generation | Preserve in a technical cross-reference, not as a duplicate item master |
| `MinCloseQty` (QuantityType, precision 15, scale 4) | Used as package/lot splitting quantity in QR/LOT allocation | PCTP-owned packaging/operational rule; not an IFS InventoryPart field in the current contract |
| `Model` (nvarchar(48)) | Used by LOT/stock display; one procedure joins `B20Model` to obtain ModelType | PCTP-owned product/model extension or approved IFS attribute mapping; source definition for B20Model is still missing |
| `MaxCloseQty` | Present in source schema; no confirmed use in the inspected active PCTP module bodies | Preserve only if caller/data review confirms need |
| `BarCode` and other Bravo classification/cost/tax fields | Not proven required by the inspected PCTP module set | Do not copy without a concrete PCTP caller and IFS source mapping |

The remaining B20Item fields belong to the Bravo master model and must not be cloned wholesale into FVN_PCTP.

### 3.2 B20ItemQuyCach — 14 columns

Source fields include `CustomerCode`, `ItemCode`, `Quantity`, `MaxCloseQty`, `BuiltinOrder`, `IsActive` and audit fields.

The inspected QR/LOT procedures use `ItemCode` and `CustomerCode`; for customer code `0100002`, they apply a different LOT normalization rule (trim the last six characters for long LOTs). Some older routines join this table but do not use its columns. A newer YMVN routine explicitly removed an unused join to avoid duplicate stock rows.

Disposition: replace with a PCTP-owned customer/part rule table, for example `dbo.FVN_PartCustomerRule`, keyed by the resolved IFS site/part plus CustomerCode. Preserve Quantity/MaxCloseQty only after confirming their live data meaning and callers. The customer-specific LOT normalization rule must be explicit and deterministic; do not implement it by a join that can multiply inventory rows.

### 3.3 B20Gear — 13 columns

PCTP QR/LOT procedures use `Code` (int), `Name` (nvarchar(20)) and `IsActive`. Gear code is encoded in character 13 of certain LOT strings; the QR routines parse a Gear name from text such as `Gear D : ...` and resolve it to a code.

Disposition: this is not an IFS part. Rebuild as a small PCTP-owned `dbo.FVN_Gear` lookup, preserving the code/name/active semantics and legacy LOT character contract. Do not replace Gear with PartNo or an IFS product group.

### 3.4 B20Dept — 17 columns

The PCTP-linked barcode procedures use `Code` and `Name` to display the employee department; one routine validates `Code`, `IsActive` and `IsGroup`. This is organizational master data, not part master data.

Disposition: do not clone Bravo B20Dept. Resolve department display/validation from the approved HRM/FVN department source. The exact HRM table/view and its key mapping must be confirmed before writing the join.

### 3.5 B20ImageStore — 11 columns

No independent PCTP operational procedure in the reviewed set requires this as a PCTP-owned master. It appears in the very large legacy trigger body for generic Bravo image handling.

Disposition: exclude from the PCTP clean-install schema unless a current PCTP UI/repository is shown to need image persistence. If required, use the approved FVN attachment/image service rather than cloning the Bravo table.

## 4. Required FVN design components (no duplicate IFS master)

The following are design components, not yet approved physical DDL. Final keys depend on the PCTP-to-IFS SiteCode decision.

1. **Part lookup view** — a read-only PCTP view (e.g. `dbo.vFVN_PCTP_Part`) over `IFS.FVN_InventoryPart`, optionally joined to `IFS.FVN_PartCatalog` and PCTP extension tables. It exposes the part number, description, UOM and IFS status with the selected SiteCode. It must not own or duplicate those values.
2. **Legacy QR identity cross-reference** — a narrow table such as `dbo.FVN_PartIdentity` keyed by `(SiteCode, PartNo)`, retaining the old `B20Item.Id` where required to decode existing five-character QR identifiers. This is a technical identity map, not a second item master.
3. **Part operational profile** — a narrow extension for PCTP-only values that IFS does not currently supply, especially ModelCode and any verified default package quantity. Do not put description/UOM/status in this table.
4. **Customer-part rule** — a narrow extension for the verified customer-specific packaging/LOT rules formerly represented by B20ItemQuyCach. Ensure one deterministic active rule per site/part/customer (or a version/effective-date rule if the source evidence requires history).
5. **Gear lookup** — `dbo.FVN_Gear`, PCTP-owned, for LOT character-13 Gear codes and names.
6. **Department lookup** — use the approved HRM/FVN source; no Bravo B20Dept clone.

Do not add `MinCloseQty` to `IFS.FVN_InventoryPart`: it is not part of the current IFS sync contract and it represents a PCTP packaging/LOT behavior, not the authoritative IFS part master.

## 5. SiteCode is a required part of the part key

IFS InventoryPart is keyed by `(SiteCode, PartNo)`; the legacy PCTP fields `STOCKTP.PART`, `SlotLot.ItemCode` and delivery/return item-code columns generally contain only PartNo.

Therefore, a safe FK or join cannot be created on PartNo alone unless the business explicitly guarantees one IFS site for all PCTP activity. The final schema must resolve SiteCode from a configured PCTP site or from a warehouse-to-site mapping, then use the composite key.

Do not silently choose the first matching IFS row, use a global PartCatalog row as a substitute for the site-specific inventory part, or assume every PartNo is unique across sites.

## 6. PCTP-linked Bravo module evidence

The uploaded result set contains 449 full SQL module definitions that referenced one or more objects in the export allowlist. Cross-checking module bodies against the PCTP table candidates found 87 modules with a PCTP object reference; 40 of those also reference one or more B20 master tables. This is a static source inventory, not proof that every version is called by the current application.

### 6.1 High-priority PCTP modules to rewrite against IFS/FVN

- Stock/LOT reads: `getTonStockTP`, `getTonStockTP_Full`, `getTonStockTP_old`, `getTonStockTPNew`, `capnhapStokTPtmp`, `loadSTOCKTP`, `loadSTOCKTPtotal`.
- QR/LOT allocation and parsing: `Usp_Qrcode_gheplot`, `Usp_Qrcode_gheplot_New20`, `Usp_Qrcode_gheplot_Lot20`, `Usp_Qrcode_gheplot_Old13`, `Usp_Qrcode_gheplot_PHONG`, `Usp_Qrcode_gheplot_PHONG1`, `Usp_Qrcode_gheplotYM1`, `Usp_Qrcode_gheplotYM2`, `Usp_Qrcode_gheplotYMVN`, `Usp_Qrcode_Take_Lot`, `Usp_Qrcode_Take_Lot2405(OLD0911)`, `Usp_Qrcode_Update_Stock1`, `ups_YMVN_GHEPLOT_SLIP`, `YMVN_GHEPLOT`.
- Barcode/trace routines: `Usp_BarCodeView_ThongKe_Tmp1`, `Usp_BarCodeView_ThongKe_Tmp1_YAM`, `Usp_BarCodeView_ThongKe_Tmp3`, `Usp_BarCodeView_ThongKe_Tmp3TMP`, `Usp_BarCodeView_ThongKe_Tmp5`, `Usp_BarCodeView_ThongKe_Tmp5_YMVN`, `Usp_DAT_BarCodeView_ThongKe`, `Usp_gheplotPrint`, `usp_Trculotnoxuat_linhkien_na`.
- Delivery/warehouse candidates: `getDeliveredQty`, `getDOCQRCODEDAT`, `sp_BaoCao_DeliveryTrace`, `sp_GetCurrentStockStatus`, `sp_GetStockHistory`, `usp_EntryAndAxistByLot`, `usp_sys_DeliveryWarning`.

The 40-module overlap also includes generic Bravo inventory/report procedures such as `usp_Vcd_*` and `usp_Check_Unique_B30AccDoc`. Do not clone the Bravo accounting/reporting subsystem simply because a procedure mentions B20Item. First confirm that the PCTP application actually calls it; if it does, rewrite only the PCTP-required output/validation over FVN/MES sources.

### 6.2 Large legacy trigger is not portable

`trgupdatetblDoctem` is approximately 580 KB in the exported definition and references all five B20 master tables plus numerous Bravo tables, PCTP tables and source-database-qualified objects. It must not be copied to FVN_PCTP. Decompose only the proven PCTP behavior into explicit FVN/MES procedures or application services. Any label-generation side effect must be separately specified and tested.

## 7. Build/migration consequences

- Continue from the existing migration chain 000–013; do not rewrite those already-run migrations in place.
- The next PCTP migration should create the FVN-owned extension/identity/rule/gear tables only after the SiteCode resolution and source field meanings are approved.
- Rewrite PCTP views and procedures to use `IFS.FVN_InventoryPart` for site-specific part master data, `IFS.FVN_PartCatalog` for global attributes where appropriate, and `MES.FVN_InventoryBalance` / `MES.FVN_InventoryTransaction` for quantity and movement.
- Preserve the old five-character QR identity through a cross-reference while old labels remain in circulation.
- Remove all direct Bravo B20 references from PCTP runtime SQL and C#; do not introduce new compatibility synonyms for B20 names.
- Preserve existing document numbers, LOT parsing, FIFO ordering, customer-specific LOT normalization, package split behavior and transaction boundaries. Any behavior change requires explicit approval and regression evidence.

## 8. Required decision before final DDL

Confirm whether PCTP uses exactly one IFS `SiteCode`, or multiple sites. If multiple, provide the mapping from each PCTP warehouse to IFS SiteCode. This determines the composite part key for STOCKTP, SlotLot, delivery, return and abnormal-processing references and is a hard prerequisite for final FK/index design.


## 9. Additional Bravo dependencies exposed by full module bodies

The full definitions also reference Bravo objects that were not included in the 00H target-object metadata list. They must not be cloned automatically, but they must be resolved when rewriting the corresponding PCTP procedure:

| Referenced source object | Observed use | Required replacement/review |
|---|---|---|
| `B20Model` | `getTonStockTPNew` joins `B20Item.Model` to obtain `ModelType` | Obtain exact source definition/data and decide IFS attribute mapping vs PCTP-owned model lookup |
| `B20ItemGroup` | Generic `usp_Vcd_*` reports filter by `ItemAccount` | Do not port generic Bravo accounting reports unless current PCTP caller proves need; otherwise replace only required PCTP classification |
| `B20Employee` | Barcode routines display employee/department information | Use approved HRM/FVN employee source; no Bravo employee clone |
| `B20ItemLot` | `usp_Trculotnoxuat_linhkien_na` resolves item name/model from a LOT prefix | Prefer MES LOT/part genealogy; preserve only required PCTP output contract |
| `B20Lines` | Generic Bravo trigger/report logic resolves line names | Use approved FVN/HRM production-line source if a current PCTP caller requires it |
| `tbl_DocTem` | Large `trgupdatetblDoctem` label side effects | Define an explicit FVN label-generation contract only if PCTP still needs it |
| `B20ItemCatg`, `B20ItemClass`, `B20ItemBarcode`, `B20ItemLotDetail`, `B20ItemsSetDef`, and other Bravo DML dependencies | Occur in the large legacy trigger and Bravo framework code | Exclude unless an independently verified PCTP workflow requires a specific behavior |

The 00H module-definition result returned 449 matching definitions: 378 stored procedures, 58 triggers, 11 scalar functions and 2 table-valued functions. No view definition row appeared in that result set. This does not prove that PCTP has no views; the previously inventoried view candidates still need exact source definitions and caller verification before being rewritten.



## 7. Owner approval and implemented DDL (2026-11-16)

The owner explicitly selected `(SiteCode, PartNo)` as the canonical part key. This removes the previous single-site-versus-multi-site design blocker: the schema is site-aware in either case. Migration 014 introduces `dbo.FVN_PCTPSite` as an explicit allow-list; do not auto-enable every IFS site. `dbo.FVN_Warehouse.SiteCode` is required, and every Slot has an explicit primary mapping to `MES.FVN_IfsInventoryLocation` through `dbo.FVN_SlotMesLocation`.

Migrations 014–023 now create the PCTP extensions, business tables, FIFO policy and QR/LOT contracts using the composite key for every known part reference. The IFS description/UOM/status remain sourced from `IFS.FVN_InventoryPart`; `dbo.FVN_PartIdentity`, `dbo.FVN_PartOperationalProfile`, `dbo.FVN_PartCustomerRule`, and `dbo.FVN_Gear` contain only PCTP-owned identity/rule data.

Deployment still requires the actual SiteCode values and the mapping from each PCTP Warehouse/Rack/Slot to the correct MES location. The schema does not guess these values. The C# repositories also still need to pass SiteCode/PartNo; the existing source code is not yet compatible with the new required columns.
