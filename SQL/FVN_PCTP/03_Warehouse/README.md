# Warehouse and stock

This is the intended domain for the approved warehouse model: Warehouse/Rack/Slot, LOT, stock state, reservations, immutable movements, balances and reconciliation. Files in `LegacyCandidates/` and `Diagnostics/` are retained for analysis only; they are not deployable in their current form. Do not create a second authoritative ledger alongside MES inventory. Resolve the ownership boundary for STOCKTP, SlotLot and StockHistory before promotion.
