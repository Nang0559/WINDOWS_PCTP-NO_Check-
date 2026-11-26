# PCTP MES — Quantity Management Implementation Plan

- **Document:** MES-QTY-001
- **Version:** 0.2 — Repository audit; status updated to migration 060 (see section 9)
- **Date:** 2026-11-13
- **Target branch:** `feature/mes-product-genealogy-timeline`
- **Scope:** Quantity-only MES; no purchasing price, unit cost, inventory valuation, or accounting value.

> This document records what the current repository actually provides and defines the safe implementation order. It is not approval to mutate IFS or existing production/warehouse records. Unverified IFS objects, keys, and QR payload semantics remain TBD until checked against the connected IFS environment and sample labels.

## 1. Target business flow

`IFS Part Master + BOM`
→ `Purchase Order Upload`
→ `Incoming Material Receipt (QR scan)`
→ `Raw Material Inventory by PartNo + Supplier LOT`
→ `Production Plan Upload`
→ `BOM Requirement Explosion`
→ `Production Order + Material Reservation`
→ `QR Issue to WIP`
→ `QR Consumption / Return / Scrap`
→ `Finished Goods Receipt (existing vNhapTP flow)`
→ `Finished Goods Inventory / Delivery`
→ `Forward + Backward Product Genealogy`

Business rules:
- IFS remains authoritative for PartNo, description, UOM and BOM/structure. PCTP stores a synchronized, versioned snapshot for operational use.
- Quantities are the only inventory measures in scope. Do not add price, currency, unit cost or monetary totals.
- Every stock-changing action must create an immutable, auditable movement with document reference, actor, time, PartNo, LOT, quantity and source/destination.
- Distinguish warehouse available, reserved, issued to WIP, consumed, returned, scrapped, quarantined and finished-goods quantities. Never collapse them into one on-hand number.
- Production orders must retain the BOM revision/snapshot used at release; later IFS BOM changes must not silently rewrite released orders.
- A QR scan is an input event, not proof of consumption by itself. The operator action must identify whether it is receiving, issuing, consuming, returning, scrapping or moving.
- Existing delivery and finished-goods receipt flows remain operational and are extended only through explicit, regression-tested integration points.

## 2. Repository audit — existing assets

### 2.1 Reusable capabilities

| Existing area | Evidence in branch | Reuse decision |
|---|---|---|
| IFS access | `PCTP/Modules/GiaoHangKhach/Repositories/IFSRepository.cs`, `OrderLoading/IFS/IfsOrderLoadStrategy.cs`, `IfsOrderSource.cs` | Reuse provider/connectivity patterns only. Existing query is customer delivery order data; it does not yet prove access to Part Master or BOM. Add a dedicated read-only IFS master-data adapter after confirming IFS views/API and key columns. |
| QR parsing | `PCTP/Shared/Helpers/QRCodeParser.cs`, `QRCodeBuilder.cs`, `QRCodeWorkflowAdapter.cs`, `Modules/GiaoHangKhach/Services/DocQRScanEngine.cs` | Reuse parser infrastructure where QR contracts match. Add explicit raw-material label profiles; do not assume finished-goods/customer QR has the same payload. |
| Warehouse / Location / Slot | `Modules/KhoCore`, `Modules/KhoVatLy`, `WarehouseRepository`, `SlotRepository`, `RackRepository`, `SlotLocation` | Reuse the existing warehouse/location definitions. User confirms `Part`, `WIP`, and `FG` are already defined. Do not create duplicate masters; map existing codes/IDs in Phase 0. |
| Inventory Part | Existing Inventory Part definition and active item lookup through `B20Item` | Reuse the existing Inventory Part catalog and synchronize it from IFS; do not create a parallel `MesPart` catalog unless schema audit proves the existing definition cannot support the required fields. |\n| Central stock movement | `IStockMovementService`, `StockMovementService`, `StockMovementRequest` | Existing central boundary is useful, but `Reserve()` currently returns “not implemented”. It is not yet a complete reservation ledger. Extend with explicit raw-material commands and transaction/idempotency rules; do not pretend Reserve is functional. |
| Finished-goods receipt | `Modules/NhapKho/Services/NhapTpReceivingService.cs`, `INhapTpReceivingService.cs`, `SQL/PCTP_vNhapTP.sql` | Reuse for finished goods only. Service comment and view logic show this is the existing FG/Slot receipt flow, not raw-material receiving. Keep it as the downstream FG receipt boundary. |
| LOT history / traceability | `Modules/BaoCao/Infrastructure/Repositories/ProductionLotTraceRepository.cs`, `LotTraceTimelineBuilder.cs`, `FormBaoCaoTraceability.cs` | Reuse the read-only timeline and delivery/stock evidence composition. Current production query reads STOCKTP production facts; it does not establish component issue/consumption genealogy. Add material genealogy as a new evidence source only after source keys are verified. |
| Existing genealogy design | `docs/MES_PRODUCT_GENEALOGY_TRACEABILITY_DESIGN.md` | Keep as the product genealogy read-model design. It explicitly marks source joins and quantity semantics as TBD; do not bypass its verification gates. |
| Automated checks | `PCTP.QC.Tests`, `PCTP.QC.SqlIntegration`, `PCTP.QC.UI` | Extend contract/unit/SQL integration tests for each MES phase. Preserve delivery regression suites. |

### 2.2 Confirmed gaps

1. No complete MES workflow was found for PO receipt, supplier LOT receiving, material reservation, WIP issue, actual consumption, return and scrap. This is a workflow gap, not a reason to recreate existing Warehouse/Location or Inventory Part masters.
2. The current `NhapTpReceivingService` is specifically for finished goods.
3. The current `vNhapTP` view is built around `B30AccDoc` finished-goods receipt fields and `STOCKTP`; it is not a raw-material receiving model.
4. Existing `StockMovementService.Reserve()` is a stub and returns a failure result.
5. Existing IFS order loading is customer-order/delivery oriented. No confirmed Part Master/BOM query contract was found in the reviewed IFS source.
6. Current production LOT timeline reads finished-goods `STOCKTP` fields. It cannot prove which component LOTs were issued or consumed.
7. The repository targets .NET Framework 4.7.2 / WinForms / DevExpress 25.2 in `PCTP/PCTP.csproj`; new runtime code must follow this compatibility target. Unit tests target .NET 8 and can link/source-test compatible domain logic.

## 2.3 Reuse existing Inventory Part and warehouse/location definitions

The user confirms that the current inventory feature already defines the warehouse/location categories `Part`, `WIP`, and `FG`, and already has an Inventory Part definition. This changes the implementation decision:

- Reuse the existing Warehouse, Location, Rack/Slot and Inventory Part masters. Do not create duplicate MES warehouse/location or Part master tables.
- Synchronize the existing Inventory Part catalog from IFS. PartNo remains the common key; synchronize description, UOM, active/status, IFS source identity and change metadata as supported by the existing schema.
- Use the existing location categories: `Part` for raw-material warehouse stock, `WIP` for quantities issued to production, and `FG` for finished goods. Phase 0 must map actual database codes/IDs, not assume display labels are keys.
- Add only MES transaction capabilities that are missing: PO upload, material receipt/LOT events, BOM snapshots, production plan/order, reservation, issue, consumption, return, scrap and genealogy.
- If Inventory Part needs additional IFS sync fields, extend its existing schema only after checking current consumers and migration/deployment conventions.

## 3. Target module boundaries

Create a separate `PCTP/Modules/Mes` feature area (or equivalent naming aligned with repository conventions) with these subdomains. It must consume the existing Inventory Part and Warehouse/Location masters rather than creating duplicates:

- **MasterData:** synchronize the existing Inventory Part catalog from IFS; add BOM header/detail snapshots, source revision and sync history. Do not create a duplicate Part master.
- **Purchasing:** uploaded PO header/lines and import batch/error rows. Quantity only.
- **MaterialReceiving:** receipt document/lines, QR scan records, supplier LOT identity, inspection/hold status.
- **MaterialInventory:** warehouse/location/LOT balances and immutable inventory movements.
- **ProductionPlanning:** uploaded plan header/lines, validation and import history.
- **MaterialRequirements:** BOM explosion result with source BOM revision and calculation trace.
- **ProductionOrders:** order header, released BOM snapshot and required material lines.
- **MaterialExecution:** reservation, issue-to-WIP, scan/consume, return, scrap and reconciliation.
- **Genealogy:** links from finished LOT / production order to component LOT and quantities by event.
- **FinishedGoodsIntegration:** adapter to existing `vNhapTP` / finished-goods receipt; do not fork that process.

The UI should be separate menu groups, but all stock mutations must pass through one application boundary and a single transaction per business document.

## 4. Minimum quantity ledger model

The implementation must preserve the distinction between current balance and event history.

### Master and planning
- **IFS9 Part Catalog + Inventory Part:** IFS9 is the sole authority. PCTP mirrors only required attributes into `MES.IfsPartCatalog` and `MES.IfsInventoryPart`, keyed by PartNo and SiteCode + PartNo. Existing PCTP item master may remain for legacy modules, but MES must resolve parts through the IFS mirror; it is not an MES master source.
- `MES.BomHeader`: synchronized IFS9 structure header/alternate, keyed by Site + parent PartNo + revision + structure type + alternate.
- `MES.BomLine`: synchronized IFS9 structure line, component PartNo, quantity per, UOM and operation/consumption attributes only where IFS9 provides them.
- `MesPurchaseOrder` / `MesPurchaseOrderLine`: PO identity, supplier, PartNo, ordered quantity, due date, received quantity derived from receipts.
- `MesProductionPlan` / `MesProductionPlanLine`: imported plan identity, FG PartNo, planned quantity and schedule.
- `MesProductionOrder` / `MesProductionOrderMaterial`: released order, BOM snapshot/revision, planned FG quantity and required component quantity.

### Execution and inventory
- `MesMaterialLot`: PartNo + supplier LOT identity; retain supplier and internal LOT separately if labels require internal relabeling.
- `MesMaterialReceipt` / `MesMaterialReceiptLine`: receipt document and accepted/quarantined quantities.
- `MesInventoryMovement`: immutable signed quantity events, LOT, PartNo, from/to location or stock state, event type, reference document/line, idempotency key, operator and timestamp.
- `MesInventoryBalance`: derived or transactionally maintained balance by PartNo + LOT + location + stock state. Reconciliation against movement ledger is mandatory.
- `MesMaterialReservation`: quantity reserved for a production order; cannot exceed available quantity.
- `MesMaterialIssue`: quantity transferred from warehouse to WIP against a production order and component requirement.
- `MesMaterialConsumption`: actual consumed quantity tied to production order, component PartNo, component LOT and scan/action evidence.
- `MesMaterialReturn` / `MesMaterialScrap`: explicit disposition events; do not edit issue/consumption history.
- `MesQrScanEvent`: raw QR, parsed profile/version, event purpose, resolution outcome and link to the resulting business event. A duplicate scan must be idempotent within a documented scope.

The logical entities are implemented as an additive SQL Server baseline in `SQL/MES/01_MES_Quantity_Core.sql`. `MES.Ifs*` tables are read models synchronized from IFS9 and are not manually maintained. Existing PCTP masters may remain for legacy features but are not authoritative for MES Part/BOM/Location. Treat the DDL as a reviewed baseline, not production-approved deployment: validate IFS9 source fields and target DB constraints before applying.

## 5. Required stock state equations

For each PartNo + LOT + location/state:

- Warehouse available = accepted warehouse on-hand − active reservations − quarantined quantity.
- WIP remaining = issued to WIP − consumed − returned − scrapped.
- PO open quantity = ordered quantity − accepted receipt quantity − explicitly cancelled quantity.
- Production material shortage = max(0, requirement − eligible available quantity).
- FG receipt quantity is posted through the existing finished-goods receiving flow; do not double-post it into raw-material stock.

Every equation must use a consistent UOM. UOM conversion is allowed only from a synchronized, effective-dated IFS conversion rule. Never compare or aggregate quantities with incompatible UOMs.

## 6. Implementation phases and exit gates

### Phase 0 — Foundation audit (current)
- Confirm existing DB tables, views, procedures and ownership for slots, raw/finished goods, LOT, QR and stock history.
- Confirm IFS connectivity and discover approved read-only Part Master/BOM sources, keys, revisions, UOM and effective-date semantics.
- Inspect current Excel import patterns and define versioned templates for PO and production plan.
- Inspect actual supplier QR labels and define supported payload profiles.
- Record all confirmed/rejected assumptions and test fixtures.
- **Exit:** schema/source inventory and evidence-backed interface contracts; no new stock DDL before this gate.

### Phase 1 — IFS Part Master + BOM synchronization
- Read-only IFS adapter, batch sync, validation, sync history and error report.
- Idempotent upsert into PCTP master snapshot; never write to IFS.
- BOM revision/effective-date preservation.
- **Exit:** PartNo/BOM sample parity against IFS and tests for changed/obsolete parts and revisions.

### Phase 2 — PO upload + QR material receiving + LOT inventory
- Template download, preview, validation, import batch, row-level errors and idempotent commit.
- Receiving by QR scan, duplicate/unknown/PartNo mismatch handling, accepted/quarantine split.
- Quantity ledger and LOT/location balances.
- **Exit:** receive/return/adjust/move reconciliation and supplier LOT trace.

### Phase 3 — Production plan upload + BOM explosion
- Import plans and validate FG PartNo.
- Resolve the correct effective BOM revision.
- Calculate gross requirement, existing eligible stock, reservations and shortage by component LOT/UOM.
- **Exit:** calculation reconciles with controlled fixtures and preserves BOM snapshot.

### Phase 4 — Production orders + reservation
- Create/release/cancel order from plan.
- Freeze BOM snapshot at release.
- Reserve component quantities atomically with concurrency guard.
- **Exit:** no negative available stock; release/cancel is idempotent and auditable.

### Phase 5 — QR issue, WIP consumption, return and scrap
- Scan component LOT and production order.
- Validate component membership, LOT status, UOM and remaining quantity.
- Record issue separately from actual consumption.
- Return unused quantity or scrap explicitly.
- **Exit:** per-order and per-LOT WIP reconciliation, duplicate-scan tests, genealogy source evidence.

### Phase 6 — Finished goods and product genealogy
- Integrate completed order quantity to existing `vNhapTP`/receipt flow without double posting.
- Link FG LOT to production order and consumed component LOT events.
- Forward/backward genealogy and impact analysis.
- **Exit:** one-to-many/many-to-many trace verified against source documents.

### Phase 7 — MES dashboard and operational acceptance
- Material shortages, PO overdue/open quantities, receiving queues, WIP remaining, production order progress, FG output and LOT impact.
- Role permissions, audit, performance and production deployment.
- **Exit:** end-to-end acceptance and rollback/reconciliation plan.

## 7. Non-negotiable controls

- No money fields or inventory valuation in MES quantity scope.
- IFS is read-only.
- Do not change or repurpose `STOCKTP` to hold raw materials.
- Do not infer actual consumption from BOM requirement, issue quantity or an ambiguously named legacy field.
- Do not treat a scan as a completed stock movement until the associated command commits.
- No negative balance; enforce concurrency and idempotency in SQL transaction boundaries.
- Preserve source document, source row and operator on every movement.
- Do not update/delete posted movement history; correct with reversal/adjustment events.
- Existing delivery and finished-goods receipt flows require regression tests on every shared stock change.

## 8. Immediate next engineering tasks

1. Produce a database object inventory for raw material, supplier LOT, slot/location, QR and stock movement tables using the existing live-data/schema-audit harness.
2. Produce an IFS source contract for Part Master and BOM using approved read-only connection and representative PartNo/BOM examples.
3. Produce a supplier QR label matrix (payload examples with sensitive data redacted) and parser contract.
4. Produce PO and production-plan Excel template contracts with stable column names, required fields, duplicate keys and row-level error rules.
5. Only after tasks 1–4 are evidenced, create Phase 1 sync contracts and Phase 2 DDL.

**Current implementation status:** superseded by section 9 (this paragraph described the 0.1 baseline, before migrations 025–060).

## 9. Implementation status as of migration 060 (verified against the repository, not against a live database)

Sections 1–8 are the original baseline and remain the target. This section records what exists now. "Verified" means read in the
repository source; nothing here was run against DB57 unless stated.

### 9.1 Delivered, by phase

| Phase | Migration(s) | What exists |
|---|---|---|
| 0 / master data | 013–030, 037 | `FVN_` table prefix standard; site-scoped Part/Warehouse; IFS part/warehouse/supplier sync (selective sync in 037); `MES.FVN_ItemIdentity` as the only ItemId5 allocator |
| 2 receiving | 025, 026, 031, 032, 045 | Slot-aware adapter `dbo.usp_FVN_PCTP_PostStockMovement` to the MES ledger; idempotency guard and LOT adjustment history; MES-owned component PO + component LOT release; canonical component LOT receipt posting; PO label print |
| 3 planning / BOM | 033, 038, 046, 048, 049 | Shift production plan, IFS BOM demand, firm plan with automatic component demand; production order requires a firm plan and fulfilled components; change version and BOM visibility |
| 5 issue (PART → WIP) | 033, 050 | `usp_FVN_ComponentIssue_Post` (idempotent, locks the demand line, checks PART source slot and WIP destination slot, posts a `MOVE` AVAILABLE → WIP); document views over component demand |
| 4 / 5 execution | 034, 035, 036 | Routing, production order from plan, `usp_FVN_ProductionLot_Create`, operation completion, `usp_FVN_ProductionMaterial_Consume`, component LOT trace, per-unit process labels, shared workflow transitions and audit |
| Control / reporting | 047 | Production flow control tower |
| Document runtime / UI designer | 039, 050–058 | Document metadata registry and hardened runtime, designer with versioned writes, trusted-registry-only rule, UI layout designer and screen catalogue, audit list |
| Menu designer | 054, 055, 059, 060 | Menu profiles (tree of GROUP/ITEM keys), role assignment, priority, delete with audit copy; 059 now quarantines a menu table whose structure differs (see 9.4) |

Numbers 040–044 are not present in the repository.

### 9.2 Confirmed gaps (what is NOT connected yet)

1. **Finished goods are not bridged to the FG receipt flow.** `usp_FVN_ProductionLot_Create` creates a `MES.FVN_MaterialLot` of type `FG`
   and a `MES.FVN_ProductionLot`, but writes no inventory balance or movement for it. The existing receipt flow
   (`vNhapTP`, `NhapTpReceivingService`, `STOCKTP`) is fed from legacy documents; no code reads the MES FG LOT. Only the BaoCao
   traceability module reads it. The bridge needs the live `vNhapTP`/`STOCKTP` contract and an idempotency key per
   `ProductionLotId` to avoid double posting; it is deliberately not implemented without that evidence (section 7).
2. **No UI for returning components from WIP to the PART warehouse, or for scrapping them.** Only the generic movement procedure
   (migration 004/025) can do it.
3. **Reservation is not part of the live flow.** `MES.usp_ReserveMaterial` / `usp_ConsumeMaterialReservation` /
   `usp_ReleaseMaterialReservation` (migration 008) operate on the generic `MES.ProductionOrderMaterial` model. The live path
   (033/034) uses `FVN_ComponentDemandLine`, and `usp_FVN_ComponentIssue_Post` only checks `OnHandQty - ReservedQty >= Quantity`;
   nothing in 033/034/038 ever increases `ReservedQty`. Two concurrent orders can therefore both plan on the same LOT.
   The production-order guard (048) checks that demand is fulfilled, not that a LOT is locked.
4. **Test coverage is thin outside the document/menu areas.** MES tests cover `ProductionFlow*`, the document runtime and the menu
   designer. There are no regression tests for receipt, issue or consumption procedures, although section 7 requires them for
   every shared stock change.

### 9.3 Suggested order for the remaining work

1. Evidence first (Phase 0 tasks in section 8): dump `vNhapTP`, `STOCKTP` and the FG slot mapping from DB57 and agree the
   FG receipt contract.
2. Reservation on the live model: reserve at demand-line level, keyed `(ComponentDemandLineId, MaterialLotId, SlotId)`,
   increase `FVN_InventoryBalance.ReservedQty` under `UPDLOCK, HOLDLOCK`, consume/release in `usp_FVN_ComponentIssue_Post`, with
   concurrency tests.
3. Return/scrap procedures on the same pattern as issue (explicit events, never edit issue history), then their screens.
4. FG bridge: one idempotent procedure per `ProductionLotId`, plus a pending-receipt list for the existing receiving screen.
5. SQL integration tests (`PCTP.QC.SqlIntegration`) for each of the above before enabling it in production.

### 9.4 Migration 059/060 note

If a menu table with the same name but another structure already exists (an earlier draft), `CREATE TABLE ... IF NOT EXISTS`
used to keep it and the procedures failed with Msg 207 (`DisplayName`, `NodeId`, `ParentNodeId`). 059 now renames the three
tables, their constraints and indexes to `*_Legacy_<UTC time>` (nothing is deleted) and re-creates them; 060 stops with error
60004 and instructions if the structure is still wrong. Re-run 059, then 060.
