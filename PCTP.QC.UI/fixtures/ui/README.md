# fixtures/ui

Optional `*.sql` files (applied in name order) that give the isolated G5/G6 database the reference data the
WinForms shell needs at startup (users, menu/permission rows, master data). Every file is scanned by
`Start-PctpIsolatedSql.ps1`: `USE <non PCTP_QC_ db>`, DB57/production host names and linked servers are refused.

Use synthetic data only. If G5 fails at startup on the fixture database, the evidence in
`artifacts\gates\<commit>\G5\ui\*\startup-windows.json` shows which dialog/table is missing; add it here.
