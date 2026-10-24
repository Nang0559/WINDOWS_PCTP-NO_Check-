-- g6-receive: template. Every mutating statement MUST be scoped by @Prefix (QCUI_<runId>) and have a WHERE.
-- SELECT the postcondition columns, e.g.: SELECT Status, SoLuongConLai FROM ... WHERE Key LIKE @Prefix + N'%';
THROW 50000, N'G6 g6-receive NOT CONFIGURED: provide this SQL for the isolated DB. Needed: seed.sql (1 tem/QR nhap hop le + slot trong), buoc UI quet/nhap QR, assert.sql (STOCKTP, SlotLot, StockHistory, PCTPStockMovementGuard), cleanup/verify.sql', 1;
