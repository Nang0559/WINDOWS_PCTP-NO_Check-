# PCTP UI automation - business E2E acceptance matrix

This document separates source inspection, read-only UI navigation, and transaction-level acceptance. Do not report a business workflow as PASS until its UI action and database postconditions have both been executed on a disposable QC dataset.

## Source-mapped WinForms screens

| Workflow | Entry point / source control | Screen and observed controls | Current automated coverage | Remaining acceptance gate |
|---|---|---|---|---|
| Receive | `NHAccordionControlElement` -> `E_NhapTP` | `FormNhapKhoTienTrinh`: `receive_btnRefresh`, `receive_btnScan`, `receive_worklistGrid` | Open worklist; assert scan and grid controls exist | Seed one unique receipt/LOT; scan or enter QR; submit; assert receipt record, LOT quantity and stock movement; cleanup fixture |
| Export | `cmdRackControl` opens warehouse map; physical slot opens `ExportFormSV` | `slotExport_quantity`, `slotExport_btnPick`, `slotExport_btnPrint`, `slotExport_btnCancel`, `slotExport_btnCancelPending` | Source inventory only; no automated slot selection | Provide disposable slot with stable UIA identity; map slot selection; export; assert SlotLot/STOCKTP delta and export history; prove rollback/replay |
| Delivery | `accordionControlElement22` -> `E_GHHVN_MP` | `HVN_PGH`: `txt_DOCQRCODE`, `gridCtrDONHANG`, `gridCtrDOCQrCODE`, `UIButtonHOME` | Open HVN delivery workspace; assert QR input and order grid | Seed isolated order and LOT; scan/select; confirm; assert delivered quantity, QR state, delivery ledger, pending status and history; replay must not double-deliver |
| QC | `E_Trahang` -> `accordionControlElement_QCDinhHuong` / `accordionControlElement_QCXacNhanCuoi` | Direction: `qcDirection_disposition`, `qcDirection_note`, `qcDirection_operator`, `qcDirection_btnConfirm`; final: `qcFinal_qtyOK`, `qcFinal_qtyNG`, `qcFinal_reworkLot`, `qcFinal_btnConfirm` | Open direction/final queues; assert fields and confirm controls exist (never click confirm) | Seed isolated QC ticket; exercise OK/NG and invalid quantity boundaries; assert QC decision, quantities, workflow state and audit history |
| Rework | `E_Trahang` -> `trahangndHVN` | `FormQuanLyTienTrinhHangLoi`: `rework_search`, `rework_btnPrimaryAction`, `rework_btnCreateAbnormal`, `rework_worklistGrid`, `rework_step_01`...`rework_step_08` | Open rework worklist; assert search, grid and primary action exist | Seed disposable rework ticket; complete each permitted step; assert state transitions, stock movement, rework LOT and QC disposition; cleanup all test records |

## Stable AutomationId policy

The current catalog is derived from WinForms source control `Name` values. Keep those names stable and use them as UIA AutomationIds where the installed DevExpress accessibility provider exposes them. For controls created dynamically:

1. Capture the UI tree on the target Windows workstation after opening the actual form.
2. Record the observed AutomationId, control type, framework, class, and owning window in `CONTROL_CATALOG.md`.
3. Prefer setting `Name` on the WinForms/DevExpress control in its designer/initialization code over matching localized captions.
4. Do not guess a control ID from a caption or use screen coordinates as a substitute.
5. If UIA exposes no Invoke/Value/SelectionItem pattern, document the provider limitation and implement a safe, explicit adapter; do not silently simulate success.

## Data assertion contract

Every mutating journey must define a fixture ID and these postconditions before execution:

- **Before:** fixture exists, expected starting quantities/status are captured, and fixture is tagged as QC-only.
- **Action:** UI action result is observed; the action is not considered successful merely because a click returned.
- **After:** authoritative SQL rows/history are queried and compared to expected values (including stock/LOT, delivery or QC ledger, workflow state and idempotency key where applicable).
- **Negative path:** invalid LOT, over-quantity, duplicate QR/delivery and timeout/connection-loss paths must leave business quantities and workflow state unchanged.
- **Cleanup:** restore/delete only the uniquely tagged fixture rows using an approved reset procedure; record cleanup result and preserve evidence.

Queries used for verification must be read-only. Mutating acceptance must run only in a DBA-provisioned disposable database with an explicit seed/reset contract. Do not use the shared SQL 57 database or production SQL 14 for transaction acceptance.

## Evidence and result semantics

- Navigation PASS: expected window and declared controls were observed.
- Business UI PASS: action was performed and the UI showed the expected post-action state.
- Data PASS: SQL postconditions matched the fixture's expected state.
- E2E PASS: Business UI PASS + Data PASS + cleanup PASS.
- FAIL: an assertion failed, an exception occurred, or cleanup failed.
- BLOCKED: a required fixture, stable UIA element, provider pattern, or disposable database is unavailable.

A suite containing only navigation manifests must be labeled **UI navigation smoke**, not business E2E. Do not infer successful receipt/export/delivery/QC/rework transactions from a screenshot or screen title.
