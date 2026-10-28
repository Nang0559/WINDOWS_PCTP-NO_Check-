# PCTP MP test-data workbench — operator runbook

## 1. Purpose and non-goals

This workbench profiles legacy MP inventory on SQL14 so a small, representative fixture can later be built for the newer PCTP schema on SQL57.

| Environment | Role | Rule |
|---|---|---|
| SQL14: `192.168.200.14\\BRAVO / B7R2_FCC` | Legacy source | Read-only SELECT/metadata. No DDL, DML, procedure execution, or bulk exports. |
| SQL57: `192.168.200.57 / B7R2_FCC` | Current PCTP development schema | Read-only inventory only. No test writes to the shared catalog. |
| SQL57: `B7R2_FCC_QC*` | Future isolated fixture target | Writes only after schema mapping, permission and isolation preflight have been reviewed. |

This phase does not synchronize schemas and does not deploy SQL57 changes to SQL14.

## 2. Documents and scripts

- [MP dataset manifest](MP_TEST_DATASET_MANIFEST.md): architecture, mapping rules, quantities and phase gates.
- [MP object candidate matrix](MP_TEST_DATASET_OBJECTS.csv): first-pass classification of source and target objects.
- `PCTP.QC.UI/Invoke-PctpMpSourcePreview.ps1`: read-only aggregate profile; it does not emit LOT/item/order identifiers or business values.
- `.github/workflows/pctp-mp-source-profile.yml`: manually dispatched, explicitly confirmed source profile on the `pctp-ui` Windows runner.
- `PCTP.QC.UI/PctpLiveData.Common.ps1`: shared host/catalog guards, source read-only permission probe, and stale-isolation check.
- `.github/workflows/pctp-qc-tests.yml`: isolated regression tests; SQL integration runs on a disposable SQL Server container with no SQL14/SQL57 credentials.

## 3. Before the source-profile run

1. Confirm the workflow changes are present on the repository default branch. GitHub only offers manual dispatch for a workflow present on the default branch.
2. Configure repository secret `PCTP_QC_SOURCE_SQL_CONNECTION` for SQL14/BRAVO, catalog `B7R2_FCC`, using the dedicated login `PCTP_QC_InventoryReader` or an equivalent SELECT/metadata-only account.
3. Do not use a SQL login that is sysadmin, db_owner, db_datawriter, db_ddladmin, or has INSERT/UPDATE/DELETE/EXECUTE/ALTER permission. The script fails closed if the source permission probe finds those privileges.
4. Ensure the FCCIT self-hosted runner has no leftover `PCTP-QC-ISOLATION-*` firewall rules. The script fails closed when stale isolation rules are found.
5. Never add SQL credentials to repository files, workflow text, command-line arguments, output, or artifacts.

The current metadata artifact reports the SQL57 inventory login as `sa`. Before any future SQL57 fixture write phase, replace it with a least-privilege dedicated account. This source-profile workflow does not use the SQL57 connection.

## 4. Run the aggregate profile

After the workflow is available on the default branch:

1. Open **Actions → PCTP MP source profile (read-only) → Run workflow**.
2. Select the default branch or the approved phase-2 branch after it has been merged.
3. Check `confirm_read_only_profile`. Leaving it false intentionally skips the job.
4. Wait for the job on `[self-hosted, windows, pctp-ui]` to finish.
5. Download `pctp-mp-source-profile-<run>` and inspect `mp-source-profile-manifest.json`.

The output contains only aggregate counts for `STOCKTP`, positive `SLCONLAI`, populated `PART`/`SP`, and existence matches against `B20Item.Code` and `Purchase_Order_YMVN.Part_no`. It does not reveal which rows matched. This is a profiling signal, not proof that `PART` is the correct natural key or that a row is an MP/HVN/YMVN fixture candidate.

The workflow does not export business rows, does not write to SQL14, does not connect to SQL57, and does not execute business procedures.

## 5. Interpreting the result

- If a required table/column is absent, stop and review the metadata; do not guess the join.
- If the positive-stock count is zero or the item/PO overlap is unexpectedly low, review the source mapping and MP identification rules before proceeding.
- The counts are aggregate profiling values. Do not infer source row identity, validity, or recency from them.
- The current field names suggest possible joins (`STOCKTP.PART` to `B20Item.Code` and `Purchase_Order_YMVN.Part_no`) but do not prove the business relationship. Confirm through production SQL and keys before selecting rows.

## 6. Fixture build gate — not yet enabled

Do not create a loader that writes business rows until all of these are approved:

- exact natural keys and parent-child joins verified from repository SQL plus source metadata;
- deterministic selection rule and maximum selected row counts;
- data sanitization and controlled artifact retention;
- target mapping reviewed for type, nullability, defaults and target-only columns;
- a dedicated `B7R2_FCC_QC*` catalog exists and is proven isolated;
- target account has only the minimum required permissions on that dedicated test catalog;
- idempotent run ID, transaction/rollback and cleanup checks are defined.

Generate new SQL57 Warehouse/Rack/Slot/SlotLot identifiers. Never copy SQL14 location IDs.

## 7. Required quantity invariants

The fixture must not make a test pass by artificially increasing header quantities. At minimum, validate:

1. For each slot, `Slot.Quantity = SUM(SlotLot.Quantity WHERE PhieuStatus = 0)`.
2. For each LOT, the sum of active allocations across all slots reconciles with the intended remaining stock under the confirmed application rules.
3. The `STOCKTP` equation `SLNHAP - SLXUAT = SLCONLAI` is checked only where those fields have the confirmed semantics and applicable adjustments are included.
4. Historical, cancelled and quarantined SlotLot rows are not included as available stock.
5. Receipt/issue movements and history reconcile with both the slot-level and lot-level quantities.
6. An over-issue or rejected operation leaves all stock, Slot, SlotLot and history state unchanged.
7. Re-running the same fixture seed does not duplicate rows; cleanup removes only the current run's records.

## 8. Test execution sequence

1. Run unit/source-contract regression tests.
2. Run `Usp_Qrcode_Update_Stock2405` on disposable SQL fixture; assert rollback and no leakage.
3. Run `PhieuKhoRepository` receipt/issue/history/status cases.
4. Run `ProductionBulkStockSlotRepositoryScenario`, including multi-slot LOT, FIFO, shortage rejection, quarantine and rollback.
5. Run the remaining FIFO and stock invariants suite.
6. Save sanitized logs and the aggregate manifest. Do not upload business rows from SQL14 to a public artifact.

## 9. Acceptance status

- [x] Metadata-only inventory completed; run [40688306068](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40688306068).
- [x] Source/target object candidate matrix created.
- [x] Read-only aggregate profiling script and manually gated workflow added to the phase-2 branch.
- [ ] CI regression run for this branch is green.
- [ ] Aggregate profile completed on FCCIT and reviewed.
- [ ] Exact MP selection and key mapping approved.
- [ ] Isolated target fixture loader implemented and tested.
- [ ] Business scenarios pass with quantity invariants and rollback proof.
- [ ] Separate production migration plan reviewed after PCTP is accepted.

A green syntax/CI run does not mean the MP dataset or end-to-end business flow is complete. Keep the acceptance boxes above truthful.
