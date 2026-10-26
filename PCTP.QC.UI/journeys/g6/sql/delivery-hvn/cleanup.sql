-- g6-delivery-hvn: template. Every mutating statement MUST be scoped by @Prefix (QCUI_<runId>) and have a WHERE.
-- DELETE FROM <table> WHERE <key> LIKE @Prefix + N'%';  (child tables first)
THROW 50000, N'G6 g6-delivery-hvn NOT CONFIGURED: provide this SQL for the isolated DB. Needed: seed.sql (dong TMPPHIEUGIAOHANG + LOT con ton, dung PCTP_QC_DEL_ACCEPTANCE_STT), buoc UI giao hang, assert.sql (LUUPHIEUGIAOHANG, STOCKTP, SlotLot, history), cleanup/verify.sql', 1;
