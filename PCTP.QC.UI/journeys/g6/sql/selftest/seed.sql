-- G6 framework self-test: only a session-local # temp table, never a business table.
CREATE TABLE #QCUI_SELFTEST (Name nvarchar(64) NOT NULL);
INSERT INTO #QCUI_SELFTEST (Name) VALUES (@Prefix + N'_A'), (@Prefix + N'_B');
