# Legacy XuLyHangLoi database scripts

This folder is retained as historical source material for the XuLyHangLoi / QT Chung module.

**Do not execute these files as a clean-install sequence on `FVN_PCTP`.** They are incremental patches from the prior PCTP database, contain dependencies on base tables not created in this folder, include a hard-coded `USE [B7R2_FCC]` script, a data backfill, and a repair-only IDENTITY script. One script creates `dbo.InspectionLog` without the required `FVN_` physical-table prefix.

The canonical target SQL workspace is:

`SQL/FVN_PCTP/`

The audited conversion and disposition of every script in this folder is tracked in:

`SQL/FVN_PCTP/00_Governance/00F_PCTP_CurrentSqlConsolidationAudit.md`

Until a reviewed, versioned migration is created under `SQL/FVN_PCTP`, treat these files as read-only historical references. Do not delete them; retain their Git history and source provenance.
