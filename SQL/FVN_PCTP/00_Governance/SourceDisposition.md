# Source SQL disposition register — consolidated layout

**Branch:** `feature/mes-product-genealogy-timeline`  
**Consolidation commit:** `41874d21ea731cb5ee9c49db11527e2f2f4ec373` plus follow-up move  
**Status:** paths consolidated; individual legacy SQL behavior is not yet approved for deployment.

The original files were moved into this package using their existing Git blobs.
The original source path is shown below for traceability. Moving a script is not
approval to execute it.

## 1. Governance and source evidence

| Original source | Current destination | Disposition |
|---|---|---|
| `SQL/00_FVN_PCTP_SourceInventory.sql` | `00_Governance/00_FVN_PCTP_SourceInventory.sql` | Read-only source inventory |
| `SQL/00_PCTP_Source_Inventory.sql` | `00_Governance/00_PCTP_Source_Inventory.sql` | Earlier inventory; compare before retiring |
| `SQL/00B_FVN_PCTP_OwnershipDependencyMap.sql` | `00_Governance/00B_FVN_PCTP_OwnershipDependencyMap.sql` | Read-only ownership/FK/dependency evidence |
| `SQL/00C_ApplicationUsageOwnershipEvidence.sql` | `00_Governance/00C_ApplicationUsageOwnershipEvidence.sql` | Read-only application evidence |
| `SQL/00C_ApplicationUsageOwnershipReview.md` | `00_Governance/00C_ApplicationUsageOwnershipReview.md` | Active application-use review |
| `SQL/00D_SQLModuleEvidence_CrossDomainReview.md` | `00_Governance/00D_SQLModuleEvidence_CrossDomainReview.md` | Cross-domain dependency review |
| `SQL/00E_SourceInventory_Findings.md` | `00_Governance/00E_SourceInventory_Findings.md` | Source findings and migration gates |
| `SQL/00_result/*`, `SQL/00B_result/*`, `SQL/00C_result/*` | `00_Governance/Evidence/{00_result,00B_result,00C_result}/*` | Historical source evidence snapshots |
| `SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt` | `00_Governance/Result_Export_SQL2_Complete_DDL_Metadata.txt` | Metadata export, not executable target DDL |
| `SQL/List_SQL_ON57.csv` | `00_Governance/List_SQL_ON57.csv` | Object/change inventory |
| `SQL/README_FVN_PCTP_REBUILD.md` | `00_Governance/README_FVN_PCTP_REBUILD.md` | Historical rebuild plan |
| `SQL/QC_Schema/tools/*` | `00_Governance/SchemaTooling/tools/*` | Generators and tests; tooling only |
| `SQL/QC_Schema/01..07, README, metadata reports` | `00_Governance/SchemaEvidence/*` and `SchemaTooling/*` | Fixture/generated evidence; stale outputs must be regenerated before use |
| `SQL/createpsfunon57.sql`, `createtableon57.sql`, `lis_sql_change_on57.sql` | `00_Governance/SourceExtractionTools/*` | Source extraction helpers only |

## 2. Baseline and warehouse

| Original source | Current destination | Disposition |
|---|---|---|
| `SQL/FVN_PCTP_RuntimeTables.sql` | `01_Baseline/Candidates/FVN_PCTP_RuntimeTables.sql` | Candidate only; not approved baseline |
| `SQL/PCTPQuarantineRelease.sql` | `03_Warehouse/LegacyCandidates/*` | Reconcile with MES quality/stock states |
| `SQL/PCTPStockMovementGuard.sql` | `03_Warehouse/LegacyCandidates/*` | Reconcile with one immutable movement ledger |
| `SQL/PCTP_InternalTransfer.sql` | `03_Warehouse/LegacyCandidates/*` | Transfer semantics to be reviewed |
| `SQL/PCTP_StockLotAdjustmentHistory.sql` | `03_Warehouse/LegacyCandidates/*` | Align audit with movement ledger |
| `SQL/PCTP_EnforceUniqueStockTpLot.sql` | `03_Warehouse/LegacyCandidates/*` | Verify uniqueness semantics and existing duplicates |
| `SQL/PHASE14_SlotLot_NgQuarantine.sql` | `03_Warehouse/LegacyCandidates/*` | Legacy phase patch |
| `SQL/A0_SlotLot_Diagnose_Repair.sql`, `diag_StockTpScientificLot.sql`, `PCTP_StockTpIdentityAudit.sql` | `03_Warehouse/Diagnostics/*` | Diagnostics/repair only; never clean-install scripts |
| `SQL/sp_GetCurrentStockStatus.sql` | `06_Programmability/LegacyCandidates/Warehouse/*` | Rewrite after balance authority is decided |

## 3. Delivery and QR

| Original source | Current destination | Disposition |
|---|---|---|
| `SQL/PCTP_DeliveryCommandLedger.sql` | `04_Delivery/LegacyCandidates/*` | Idempotency candidate; map to normalized delivery |
| `SQL/PCTP_GiaoDB_DocQrCode_DB.sql` | `04_Delivery/LegacyCandidates/*` | Preserve document/QR relationship; review contract |
| `SQL/PCTP_LuuPhieuGiaoHang_CustomerIsolation.sql` | `04_Delivery/LegacyCandidates/*` | Preserve customer isolation and transaction semantics |
| `SQL/PCTP_OrderTables_AddIsDelivered.sql` | `04_Delivery/LegacyCandidates/*` | Merge final state into delivery baseline |
| `SQL/PCTP_GiaoDB_RenameOrderTables.sql` | `04_Delivery/LegacyCandidates/*` | Historical rename patch; compare current schema |
| `SQL/PHASE15_HangChoGiaoPickDetails.sql` | `04_Delivery/LegacyCandidates/*` | Pick-detail candidate |
| `SQL/PCTP_GiaoDB_RepairDeliveredLines.sql` | `04_Delivery/Diagnostics/*` | Data repair only; separately approve and backup |
| QR reader and `Usp_Qrcode_*` scripts | `06_Programmability/LegacyCandidates/QR/*` | Preserve as source behavior; rewrite before deployment |
| Barcode report procedures | `06_Programmability/LegacyCandidates/Reports/*` | Confirm live callers and customer variants |

## 4. MES and test assets

| Original source | Current destination | Disposition |
|---|---|---|
| `SQL/MES/Migrations/000..011` | `05_MES/Migrations/000..011` | Ordered schema foundation; not runtime-tested |
| `SQL/MES/Migrations/README.md` | `05_MES/Migrations/README.md` | Deployment notes |
| `SQL/MES/00_MES_SourceDiscovery_ReadOnly.sql` | `05_MES/SourceReview/*` | Source discovery evidence |
| `SQL/MES/01_MES_Quantity_Core.sql` | `05_MES/SourceReview/*` | Superseded candidate; do not deploy beside migrations 000..011 |
| `SQL/MES/Tests/01_MES_DeploymentSmokeTest.sql` | `09_Tests/MES/*` | Disposable database test only |
| `SQL/QC_G4_DeliveryFixture_Seed.sql`, `Cleanup.sql` | `09_Tests/Fixtures/*` | Isolated test fixtures |
| `SQL/PHASE16_QC_Final_ExecutionTimeout_Fix.sql` | `09_Tests/LegacyRegression/*` | Regression artifact; inspect before use |
| `SQL/NHAP_TP_HIS_Retire.sql` | `90_Archive/PendingRetirement/*` | Retirement candidate; not approved to execute |

## 5. Deletion policy

**No script has been permanently deleted in this consolidation.** The Git move
preserves each source blob and its history. The following evidence is still
missing or incomplete: SQL Agent job steps, synonyms, external integrations and
some dynamic SQL/runtime call paths. Therefore no global "unused" determination
can yet be made.

Permanent deletion requires all of:
1. application and dynamic SQL caller search;
2. SQL Agent, synonym, linked server and external integration review;
3. verified replacement and regression tests;
4. data/audit retention decision;
5. rollback/retirement plan.

## 6. Outstanding phase gates

- Refresh the source inventory and export missing SQL Agent/synonym results.
- Decide one authoritative owner for LOT, Slot/Rack/Warehouse, stock balance and movement.
- Resolve the FVN_PCTP versus MES domain boundary for `STOCKTP`, `SlotLot`, `StockHistory`.
- Review each candidate script's complete definition and caller chain before promotion.
- Build approved baseline and master-data scripts.
- Build delivery/warehouse normalized model and controlled data migration.
- Run SQL Server 2016 deployment, rerun, rollback, integrity and concurrency tests.

This register is a repository organization record, not approval to execute SQL.
