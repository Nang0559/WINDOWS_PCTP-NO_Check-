-- g6-qc-direction: template. Every mutating statement MUST be scoped by @Prefix (QCUI_<runId>) and have a WHERE.
-- SELECT the postcondition columns, e.g.: SELECT Status, SoLuongConLai FROM ... WHERE Key LIKE @Prefix + N'%';
THROW 50000, N'G6 g6-qc-direction NOT CONFIGURED: provide this SQL for the isolated DB. Needed: seed.sql (tao 1 phieu cho QC dinh huong, khoa = @Prefix), AutomationId cua dong phieu/nut ''QC dinh huong'' trong FormQCDinhHuong, assert.sql (trang thai phieu + SlotLot/STOCKTP sau khi chon huong), cleanup/verify.sql', 1;
