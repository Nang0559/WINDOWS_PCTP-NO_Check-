# Deployment order and release gates

## Current state

There is not yet a complete FVN_PCTP clean-install deployment sequence. This file
prevents a folder listing from being mistaken for an executable installer.

## Candidate sequence

| Order | Path | Status |
|---:|---|---|
| 0 | `00_Governance` | Review/evidence only; never run as target install |
| 1 | `01_Baseline` | Not approved; candidate file is not the full baseline |
| 2 | `02_MasterData` | IFS one-way sync contract and SQL migration 012 added; target deployment not tested |
| 3 | `03_Warehouse` | Legacy candidates only; ownership gate open |
| 4 | `04_Delivery` | Legacy candidates only; normalized model not generated |
| 5 | `05_MES/Migrations/000..012` | MES schema candidate sequence plus IFS master/BOM snapshot sync; requires SQL Server 2016 test |
| 6 | `06_Programmability` | Legacy candidates only; not an install sequence |
| 7 | `07_DataMigration` | Not generated |
| 8 | `08_Security` | Placeholder; no user/login mapping |
| 9 | `09_Tests` | Isolated tests/fixtures only |

## Release gates

A release package may be labelled deployable only after all candidate migrations:
- are tested on the exact SQL Server major version;
- are safe to rerun or have a clear applied-version guard;
- fail atomically without partial schema/data changes;
- have explicit dependencies and rollback/forward-fix notes;
- have no source-database runtime dependency;
- pass integrity and workflow acceptance tests;
- have a reviewed permission model.

Do not execute the whole folder alphabetically. Execute only a release manifest
that has been explicitly marked approved for the target environment.
