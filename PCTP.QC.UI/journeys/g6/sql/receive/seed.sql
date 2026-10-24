-- g6-receive: template. Every mutating statement MUST be scoped by @Prefix (QCUI_<runId>) and have a WHERE.
-- Insert the rows needed by this journey, keyed with @Prefix.
THROW 50000, N'G6 g6-receive NOT CONFIGURED: provide this SQL for the isolated DB. Needed: seed.sql (1 tem/QR nhap hop le + slot trong), buoc UI quet/nhap QR, assert.sql (STOCKTP, SlotLot, StockHistory, PCTPStockMovementGuard), cleanup/verify.sql', 1;
