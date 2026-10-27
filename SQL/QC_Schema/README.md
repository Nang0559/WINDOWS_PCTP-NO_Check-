# PCTP QC ephemeral SQL schema

## Purpose

This directory is the **CI database schema source** for G3/G4/G6 SQL acceptance.
CI must create a disposable SQL Server database and must not connect to DB57 or DB14.

## Source of truth

- Source export: `SQL_sql.sql` supplied from DB57/B7R2_FCC.
- SHA-256: `cc21dbc1ebf16f62e3b3fe5d24ff1eff1ce73f88cd34d090408a1bff591c7785`
- `01_Tables.sql` contains only the dependency-closed tables required by the current G4 delivery acceptance and G6 stock/slot journeys. Table definitions are copied from the source export; they are not hand-designed test DDL.
- No production/business rows are copied into Git.

## Deployment order

1. `01_Tables.sql`
2. `05_TestDependencyShim.sql` (CI-only compatibility dependency; see below)
3. checked-in WMS procedures / hardening SQL
4. deterministic fixture seed
5. G3/G4/G6 execution
6. fixture cleanup

## Important exception: `dbo.Split`

The supplied DB57 export does not contain the source definition of `dbo.Split`, while the checked-in `Usp_Qrcode_Update_Stock2405.sql` depends on it. CI therefore uses `05_TestDependencyShim.sql`, implemented with SQL Server `STRING_SPLIT`.

This file is **not** presented as a DB57 schema export and must not be deployed to DB57/DB14. If an authoritative DB57 definition of `dbo.Split` is later exported, replace the shim and update the source hash/evidence.

## Safety invariant

The CI connection is expected to be:

`127.0.0.1,1433 / PCTP_QC_CI`

The workflow sets `PCTP_QC_ALLOW_EPHEMERAL_SQL=true` and `PCTP_QC_REQUIRE_EPHEMERAL_SQL=true`. The test policy rejects production DB14 and, in CI-required-ephemeral mode, rejects DB57 as well.
