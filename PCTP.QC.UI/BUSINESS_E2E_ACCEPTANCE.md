# PCTP UI automation - business E2E acceptance matrix

This document separates source inspection, read-only UI navigation, and transaction-level acceptance. Do not report a business workflow as PASS until its UI action and database postconditions have both been executed on a run-scoped QC dataset.

## Approved test database boundary

- **DB57 / 192.168.200.57 / B7R2_FCC is the approved live-data test target for this project.** Run-scoped seed, business UI execution, assertions and cleanup are authorized there under the guards in `PctpSql.Common.ps1` and `Invoke-PctpQcGates.ps1 -LiveDb57`.
- Every run must use a unique `QCUI_<8 chars>` prefix and must prove cleanup. Never use broad cleanup, truncate, or delete/update outside the run scope.
- **DB14 / BRAVO is not an acceptance-test target.** No seed, business UI transaction, cleanup, or other test mutation may be run against DB14. Promotion to DB14 is a separate controlled deployment after DB57 evidence is accepted.
- A configured manifest is not runtime evidence. G6 PASS requires the real Windows runner to complete the UI action, SQL postconditions and full cleanup for that exact run.

## Source-mapped WinForms screens

| Workflow | Entry point / source control | Screen and observed controls | Current automated coverage | Remaining acceptance gate |
|---|---|---|---|---|
| Receive | `NHAccordionControlElement` -> `E_NhapTP` | `FormNhapKhoTienTrinh`: `receive_btnRefresh`, `receive_btnScan`, `receive_worklistGrid` | Run-scoped seed, real QR input/Enter steps, SQL before/after assertions and prefix cleanup are configured; no runtime evidence yet | Execute on DB57; verify receipt, LOT quantity, SlotLot, StockHistory and that cleanup removes every run-created row |
| Export | `cmdRackControl` opens warehouse map; physical slot opens `ExportFormSV` | `slotExport_quantity`, `slotExport_btnPick`, `slotExport_btnPrint`, `slotExport_btnCancel`, `slotExport_btnCancelPending` | Source inventory only; no automated slot selection | Provide disposable slot with stable UIA identity; export; assert SlotLot/STOCKTP delta and export history; prove rollback/replay and complete cleanup |
| Delivery | `accordionControlElement22` -> `E_GHHVN_MP` | `HVN_PGH`: `txt_DOCQRCODE`, `gridCtrDONHANG`, `gridCtrDOCQrCODE`, `UIButtonHOME` | Single-LOT and 1/2/3-LOT manifests have run-scoped seed, real QR input/Enter steps, SQL assertions and partial cleanup | Execute on DB57; assert delivered quantity, QR state, delivery ledger, pending status/history and replay idempotency. **Current cleanup/verify does not include PCTPStockMovementGuard claims or FVN_DeliveryCommand* ledger rows; delivery E2E remains BLOCKED until their run-scoped keys and cleanup are implemented and verified.** |
| QC | `E_Trahang` -> `accordionControlElement_QCDinhHuong` / `accordionControlElement_QCXacNhanCuoi` | Direction: `qcDirection_disposition`, `qcDirection_note`, `qcDirection_operator`, `qcDirection_btnConfirm`; final: `qcFinal_qtyOK`, `qcFinal_qtyNG`, `qcFinal_reworkLot`, `qcFinal_btnConfirm` | Navigation only; G6 direction/final manifests remain `configured:false` | Identify authoritative QC ticket tables/keys; seed isolated ticket; exercise OK/NG and invalid quantity boundaries; assert decision, quantities, workflow state and audit history; cleanup all rows |
| Rework | `E_Trahang` -> `trahangndHVN` | `FormQuanLyTienTrinhHangLoi`: `rework_search`, `rework_btnPrimaryAction`, `rework_btnCreateAbnormal`, `rework_worklistGrid`, `rework_step_01`...`rework_step_08` | Navigation only; no G6 business manifest | Seed run-scoped rework ticket; complete each permitted step; assert transitions, stock movement, rework LOT and QC disposition; cleanup all test records |

## Stable AutomationId policy

The current catalog is derived from WinForms source control `Name` values. Keep those names stable and use them as UIA AutomationIds where the installed DevExpress accessibility provider exposes them. For controls created dynamically:

1. Capture the UI tree on the target Windows workstation after opening the actual form.
2. Record the observed AutomationId, control type, framework, class, and owning window in `CONTROL_CATALOG.md`.
3. Prefer setting `Name` on the WinForms/DevExpress control in its designer/initialization code over matching localized captions.
4. Do not guess a control ID from a caption or use screen coordinates as a substitute.
5. If UIA exposes no Invoke/Value/SelectionItem pattern, document the provider limitation and implement a safe, explicit adapter; do not silently simulate success.

## Data assertion and cleanup contract

Every mutating journey must define a fixture ID and these postconditions before execution:

- **Before:** fixture exists, expected starting quantities/status are captured, and fixture is tagged as QC-only.
- **Action:** UI action result is observed; the action is not considered successful merely because a click returned.
- **After:** authoritative SQL rows/history are queried and compared to expected values (including stock/LOT, delivery or QC ledger, workflow state and idempotency key where applicable).
- **Negative path:** invalid LOT, over-quantity, duplicate QR/delivery and timeout/connection-loss paths must leave business quantities and workflow state unchanged.
- **Cleanup:** restore/delete only the uniquely tagged fixture rows using an approved reset procedure; record cleanup result and preserve evidence. The verify query must cover **every** table/ledger touched by the real UI transaction, not only the seed tables.
- If a generated ledger/guard row cannot yet be correlated safely to the run prefix, cleanup is incomplete and the journey must remain BLOCKED. Do not guess a delete key.

Queries used for verification must be read-only. DB57 is the approved test target as defined above; DB14 is prohibited for acceptance mutations.

## Evidence and result semantics

- Navigation PASS: expected window and declared controls were observed.
- Business UI PASS: action was performed and the UI showed the expected post-action state.
- Data PASS: SQL postconditions matched the fixture's expected state.
- E2E PASS: Business UI PASS + Data PASS + full cleanup PASS (including Remaining = 0 across every run-created table/ledger).
- FAIL: an assertion failed, an exception occurred, or cleanup failed.
- BLOCKED: a required fixture, stable UIA element, provider pattern, or safe run-scoped cleanup key is unavailable.

A suite containing only navigation manifests must be labeled **UI navigation smoke**, not business E2E. Do not infer successful receipt/export/delivery/QC/rework transactions from a screenshot or screen title.
