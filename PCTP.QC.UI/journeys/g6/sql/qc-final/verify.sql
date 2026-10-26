-- g6-qc-final: template. Every mutating statement MUST be scoped by @Prefix (QCUI_<runId>) and have a WHERE.
-- SELECT SUM(n) AS Remaining FROM (SELECT COUNT(*) n FROM <table> WHERE <key> LIKE @Prefix + N'%' UNION ALL ...) x;
THROW 50000, N'G6 g6-qc-final NOT CONFIGURED: provide this SQL for the isolated DB. Needed: seed.sql (phieu da qua QC dinh huong), AutomationId nut xac nhan cuoi, assert.sql (STOCKTP/SlotLot/history), cleanup/verify.sql', 1;
