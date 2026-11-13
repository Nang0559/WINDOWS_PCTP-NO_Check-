# Source SQL disposition register (initial pass)

**Reviewed branch head before this register:** `98fd90247b4c7a98ce72e9a78422621b390730e8`  
**Canonical package introduced at:** `SQL/FVN_PCTP/`  
**Status:** initial repository-path and evidence review; not yet a statement-by-statement approval.

This register separates deployable business SQL from inventory evidence, generators,
fixtures, diagnostics and legacy patches. It is intentionally conservative.

## A. Keep as migration evidence / governance inputs

| Existing path | Decision | Reason / next action |
|---|---|---|
| `SQL/00_FVN_PCTP_SourceInventory.sql` | KEEP_AS_EVIDENCE | Read-only source inventory; retain under 00_Governance after path move is prepared. |
| `SQL/00_PCTP_Source_Inventory.sql` | COMPARE_THEN_ARCHIVE | Earlier inventory variant; compare result coverage with the FVN-specific version before retiring. |
| `SQL/00B_FVN_PCTP_OwnershipDependencyMap.sql` | KEEP_AS_EVIDENCE | Read-only ownership/FK/dependency map; heuristic labels must not override application evidence. |
| `SQL/00C_ApplicationUsageOwnershipEvidence.sql` | KEEP_AS_EVIDENCE | Read-only application-usage evidence; rerun/export missing SQL Agent and synonym result sets. |
| `SQL/00C_ApplicationUsageOwnershipReview.md` | KEEP_AS_EVIDENCE | Confirms six active PCTP operational tables and explains dynamic table usage. |
| `SQL/00D_SQLModuleEvidence_CrossDomainReview.md` | KEEP_AS_EVIDENCE | Cross-domain coupling analysis. |
| `SQL/00E_SourceInventory_Findings.md` | KEEP_AS_EVIDENCE | Current source inventory findings and migration gates. |
| `SQL/00_result/`, `SQL/00B_result/`, `SQL/00C_result/` | KEEP_AS_EVIDENCE | Captured evidence; retain with run manifest and source snapshot date. |
| `SQL/List_SQL_ON57.csv` | KEEP_AS_EVIDENCE | Change/object inventory, not executable DDL. |
| `SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt` | KEEP_AS_EVIDENCE | Large metadata export; reconcile against fresh catalog before generating DDL. |
| `SQL/README_FVN_PCTP_REBUILD.md` | KEEP_AS_EVIDENCE | Earlier rebuild plan; supersede its planned package map with this folder's structure after review. |

## B. Existing runtime SQL: review individually before promotion

| Existing path | Initial decision | Review requirement |
|---|---|---|
| `SQL/FVN_PCTP_RuntimeTables.sql` | REWRITE_OR_PROMOTE_PENDING | Confirm application callers, complete table contracts, keys, FKs and seed requirements. |
| `SQL/FVN_PhieuTraHangCT_LotGuard_Index.sql` | KEEP_CANDIDATE | Validate target table/index existence, duplicate data and deployment guard. |
| `SQL/NHAP_TP_HIS_Retire.sql` | PENDING_REVIEW | Potentially destructive/retirement operation; never include in baseline. |
| `SQL/PCTPQuarantineRelease.sql` | KEEP_CANDIDATE | Reconcile with MES inventory/quality state model; avoid duplicate quarantine authority. |
| `SQL/PCTPStockMovementGuard.sql` | KEEP_CANDIDATE | Reconcile with immutable stock ledger and concurrency rules. |
| `SQL/PCTP_DeliveryCommandLedger.sql` | KEEP_CANDIDATE | Delivery idempotency/command history; map to new delivery model. |
| `SQL/PCTP_EnforceUniqueStockTpLot.sql` | REVIEW_AND_REWRITE | Verify source duplicates, actual key semantics and new LOT identity. |
| `SQL/PCTP_GiaoDB_DocQrCode_DB.sql` | KEEP_CANDIDATE | Preserve Giao DB QR/document relationship; review against current delivery workflow. |
| `SQL/PCTP_GiaoDB_RenameOrderTables.sql` | ARCHIVE_CANDIDATE | Historical rename patch; inspect whether target naming has already been adopted. |
| `SQL/PCTP_GiaoDB_RepairDeliveredLines.sql` | PENDING_REVIEW | Data repair, not clean-install DDL; require a separately approved repair plan. |
| `SQL/PCTP_InternalTransfer.sql` | KEEP_CANDIDATE | Warehouse transfer behavior; reconcile with stock movement ledger. |
| `SQL/PCTP_LuuPhieuGiaoHang_CustomerIsolation.sql` | KEEP_CANDIDATE | Preserve customer isolation and delivery transaction semantics. |
| `SQL/PCTP_OrderTables_AddIsDelivered.sql` | MERGE_CANDIDATE | Fold final column/state contract into delivery baseline after caller review. |
| `SQL/PCTP_QrReaderLock.sql` | KEEP_CANDIDATE | QR reader concurrency; inspect lock scope and MP/SP behavior. |
| `SQL/PCTP_QrReaderLock_MP_SP_Migration.sql` | MERGE_OR_ARCHIVE_PENDING | Compare with current QR reader lock implementation and MP/SP schema. |
| `SQL/PCTP_StockLotAdjustmentHistory.sql` | KEEP_CANDIDATE | Preserve adjustment audit; align with immutable movement history. |
| `SQL/PCTP_StockTpIdentityAudit.sql` | KEEP_CANDIDATE | Audit/identity evidence; determine whether permanent runtime object or one-time diagnostic. |
| `SQL/PCTP_Ymvn_DocQr_AddSoPhieu.sql` | CUSTOMER_VARIANT_REVIEW | Keep only if YMVN workflow remains supported; map to common document key model. |
| `SQL/PCTP_vNhapTP.sql` | REWRITE_CANDIDATE | View currently coupled to STOCKTP production quantity; resolve MES ownership first. |
| `SQL/Usp_BarCodeView_ThongKe_Tmp5.sql` | PENDING_REVIEW | Determine if current production caller exists; TMP naming alone is not proof of obsolete. |
| `SQL/Usp_BarCodeView_ThongKe_Tmp5_YMVN.sql` | CUSTOMER_VARIANT_REVIEW | Verify YMVN runtime configuration and schema compatibility. |
| `SQL/Usp_Qrcode_LOAD_PHIEU_DOCQR2405.sql` | REWRITE_CANDIDATE | Legacy QR load procedure; map callers and document/QR target model. |
| `SQL/Usp_Qrcode_LUU_PHIEU_RetireUnsafeLegacy.sql` | RETIREMENT_REVIEW | Explicit retirement candidate; preserve history and prove no caller before removal. |
| `SQL/Usp_Qrcode_Take_Lot2405.sql` | REWRITE_CANDIDATE | Legacy LOT allocation; compare with FIFO/lot reservation semantics. |
| `SQL/Usp_Qrcode_Take_LotYMVN2405.sql` | CUSTOMER_VARIANT_REVIEW | Compare YMVN behavior with common allocation contract. |
| `SQL/Usp_Qrcode_Update_Stock2405.sql` | REWRITE_CANDIDATE | Legacy stock update; do not transplant as a second stock ledger. |
| `SQL/Usp_Qrcode_Update_Stock_SP.sql` | CUSTOMER_VARIANT_REVIEW | Determine if SP path differs materially or can use a common procedure. |
| `SQL/Usp_Qrcode_gheplot.sql` | REWRITE_CANDIDATE | LOT merge/trace; preserve genealogy and quantity invariants. |
| `SQL/Usp_Qrcode_gheplotYMVN.sql` | CUSTOMER_VARIANT_REVIEW | Verify current YMVN use and merge with common genealogy model if equivalent. |
| `SQL/sp_GetCurrentStockStatus.sql` | REWRITE_CANDIDATE | Reconcile SlotLot and STOCKTP against one authoritative balance projection. |

## C. Diagnostics, generated metadata, fixtures and tooling

| Existing path | Decision | Reason |
|---|---|---|
| `SQL/A0_SlotLot_Diagnose_Repair.sql` | KEEP_OUT_OF_DEPLOYMENT | Diagnostic/repair utility; do not run as a clean-install migration. |
| `SQL/diag_StockTpScientificLot.sql` | KEEP_OUT_OF_DEPLOYMENT | Diagnostic query; preserve for investigation only. |
| `SQL/QC_G4_DeliveryFixture_Seed.sql` | TEST_ONLY | Test fixture seed; never production baseline. |
| `SQL/QC_G4_DeliveryFixture_Cleanup.sql` | TEST_ONLY | Test fixture cleanup; destructive outside isolated test database. |
| `SQL/QC_Schema/01_Tables.sql` | FIXTURE_ONLY | Historical test schema, explicitly not a production baseline. |
| `SQL/QC_Schema/02_Generated_From_DB57.sql` | STALE_GENERATED | Regenerate from current approved PCTP runtime scope before use. |
| `SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql` | STALE_GENERATED | Regenerate with the same fresh scope as table DDL. |
| `SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql` (and similarly named generated constraints artifact) | STALE_GENERATED | Resolve duplicate historical naming and generator output before promotion. |
| `SQL/QC_Schema/04_SQL2_Metadata_Gaps.md` | KEEP_AS_EVIDENCE | Metadata gap report; regenerate with current source evidence. |
| `SQL/QC_Schema/05_Split.sql` | REVIEW_DEPENDENCY | Shared helper used by legacy procedures; retain only if promoted callers still require it. |
| `SQL/QC_Schema/06_Fixture_NhapTP_SourceTables.sql` | TEST_ONLY | Fixture source tables, not target production schema. |
| `SQL/QC_Schema/07_B20Item_Catalog_Dependencies.sql` | REVIEW_ONLY | BRAVO catalog dependency evidence; do not clone B20 domain. |
| `SQL/QC_Schema/Export_SQL2_Complete_DDL_Metadata.sql` | TOOLING | Source metadata export helper; not deployable target DDL. |
| `SQL/QC_Schema/SQL2_Metadata_Review_Report.json` | GENERATED_EVIDENCE | Retain as evidence with source snapshot identity. |
| `SQL/QC_Schema/tools/` | TOOLING_AND_TESTS | Keep only as engineering tooling; do not put in deployment sequence. |

## D. MES package already present

| Existing path | Initial decision | Next action |
|---|---|---|
| `SQL/MES/00_MES_SourceDiscovery_ReadOnly.sql` | KEEP_AS_EVIDENCE | Retain as read-only source discovery; ensure it cannot write to source. |
| `SQL/MES/01_MES_Quantity_Core.sql` | SUPERSEDED_CANDIDATE | Do not deploy beside `SQL/MES/Migrations/000..011`; compare definitions and callers, then archive if confirmed duplicate. |
| `SQL/MES/Migrations/000..011` | KEEP_AS_VERSIONED_CANDIDATE | Current ordered schema foundation; statically review and reconcile PCTP/MES boundary before moving under this folder. |
| `SQL/MES/Migrations/README.md` | KEEP_AS_DOCUMENTATION | Preserve current deployment order and explicit runtime-test caveat. |
| `SQL/MES/Tests/01_MES_DeploymentSmokeTest.sql` | TEST_ONLY | Move/copy to `09_Tests` only after package path is finalized. |

## E. Non-SQL artifacts

- `SQL/QC_Schema/tools/*.py`: generators and unit tests; retain under tooling, not as database deployment scripts.
- `SQL/00*_result/*.csv`: source catalog evidence; preserve snapshots and manifest.
- `SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt`: source catalog export, not a deployment script.
- `SQL/List_SQL_ON57.csv`: inventory evidence, not executable SQL.

## F. Not yet safe to delete

Nothing is approved for permanent deletion in this initial pass.

Before cleanup, verify at minimum:
1. no application repository or dynamic SQL caller references the object;
2. no SQL Agent job, synonym, linked server, external integration or manual runbook depends on it;
3. its behavior is replaced and tested in FVN_PCTP;
4. source data/audit history is retained or intentionally migrated;
5. the replacement has a rollback/retirement plan.

The current evidence package is missing SQL Agent job-step and synonym result exports.
Therefore, a "no dependency" conclusion is not yet available for affected objects.
