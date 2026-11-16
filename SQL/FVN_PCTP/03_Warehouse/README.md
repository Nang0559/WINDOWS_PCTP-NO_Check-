# Warehouse and stock

**Decision:** follow [ADR-001](../00_Governance/ADR_001_PCTP_PreservingWorkflows_UnifiedInventoryLedger.md).

All physical warehouse tables created in this database use the `FVN_` prefix. Existing source concepts such as `STOCKTP`, `SlotLot` and `StockHistory` are preserved as business contracts, not retained as physical legacy table names.

## Preserve the working PCTP contracts

The TP receipt, TP issue, FIFO, QR, slot, and abnormal-processing workflows are operationally proven. Their business fields, meanings, document references, status behavior, quantity rules, trace evidence, and transaction semantics must be inventoried and preserved. Do not replace a business document with a stock-ledger row.

The target physical names will be FVN_-prefixed. The conversion must be caller-aware; no blind global rename and no unreviewed schema redesign.

## One authoritative stock ledger

- `MES.FVN_MaterialLot`: canonical LOT identity/genealogy.
- `MES.FVN_InventoryBalance`: canonical quantity by site, part, LOT, location and stock state.
- `MES.FVN_InventoryTransaction`: canonical immutable movement ledger.
- PCTP-specific warehouse, receipt, issue, QR, delivery and abnormal-processing tables remain distinct business objects and link to the canonical ledger.

Source mapping:
- `STOCKTP`: preserve TP production/LOT attributes and FIFO validation inputs; do not keep a second authoritative on-hand quantity.
- `SlotLot`: preserve slot/LOT association and all QR/voucher/parent-lot trace fields; quantity authority is the MES balance. A stable SlotId-to-MES location mapping is required.
- `StockHistory`: preserve historical event/audit details and map stock-affecting events to canonical MES transactions. Do not create a second movement ledger.
- `Warehouse`, `Rack`, `Slot`: preserve the physical hierarchy where needed and map each active slot to a canonical MES location.

The final column-level mapping is not yet approved. In particular, do not assume `SlotId` equals `LocationNo`, or that legacy quantity columns can be dropped without a compatible code conversion.

## Deployment status

Files in `LegacyCandidates/` and `Diagnostics/` remain evidence only; they are not deployable installers. Build ordered, SQL Server 2016-compatible migrations under this folder after the source schema, code callers, procedures, TVPs, triggers, and posting transactions have been mapped. Do not deploy a second stock balance or movement ledger.
