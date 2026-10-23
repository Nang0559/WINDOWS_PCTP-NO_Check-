# PCTP WinForms UI automation

## 1. Startup and UI discovery

Run on the Windows workstation where PCTP and its required DevExpress/runtime dependencies are installed:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\PCTP.QC.UI\Invoke-PctpUiSmoke.ps1 `
  -ExePath "H:\95 - Project\09. QRcode\PCSYS160926\PCSYS160926\PCTP\bin\Debug\PCTP.exe" `
  -OutputDirectory ".\artifacts\pctp-ui-smoke" `
  -ExpectedWindowTitleRegex "PCTP|FCC|Auto Ware"
```

The smoke runner verifies startup, exports the accessible control tree (AutomationId, Name, control type, class, framework, process, bounds and focus state), captures the main window and writes a JSON report. It is a startup/accessibility check only.

## 2. Source-mapped UI journeys

The journey runner now tracks all visible top-level windows owned by the PCTP process. It can interact with controls in opened child forms, wait for a specific window title, verify a window title, expand/collapse menu groups, click, set values and assert accessible names.

Supported actions:
- `expand`, `collapse`: UIA ExpandCollapsePattern
- `click`: UIA InvokePattern or SelectionItemPattern
- `set-value`: UIA ValuePattern
- `assert-exists`, `assert-name-contains`
- `wait`, `wait-for-window`, `assert-window-title`
- `-AllowMutations` is a required explicit opt-in when a manifest declares `dataMutation: true` or a step declares `mutatesData: true`; use only with disposable QC data and a verified reset contract.

Example: run the receiving navigation journey (read-only; no receipt is posted):

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\PCTP.QC.UI\Invoke-PctpUiJourney.ps1 `
  -ExePath "H:\95 - Project\09. QRcode\PCSYS160926\PCSYS160926\PCTP\bin\Debug\PCTP.exe" `
  -ManifestPath ".\PCTP.QC.UI\journeys\receive-navigation.json" `
  -OutputDirectory ".\artifacts\pctp-ui-receive" `
  -ExpectedWindowTitleRegex "PCTP|FCC|Auto Ware" `
  -CloseOnFinish
```

Run all five read-only journeys sequentially and produce one suite summary:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\PCTP.QC.UI\Invoke-PctpUiSuite.ps1 `
  -ExePath "H:\95 - Project\09. QRcode\PCSYS160926\PCSYS160926\PCTP\bin\Debug\PCTP.exe" `
  -OutputDirectory ".\artifacts\pctp-ui-suite"
```

The suite writes one report per journey plus `pctp-ui-suite-report.json`. A suite PASS means every navigation manifest found its expected window; it does not mean a business transaction passed.

## 3. Current source-mapped navigation journeys

| Journey | What it opens | Mutation |
|---|---|---|
| `receive-navigation.json` | Receiving worklist via `NHAccordionControlElement` → `E_NhapTP` | None |
| `delivery-hvn-navigation.json` | HVN delivery workspace via `accordionControlElement22` → `E_GHHVN_MP` | None |
| `rework-navigation.json` | Defect/rework worklist via `E_Trahang` → `trahangndHVN` | None |
| `qc-direction-navigation.json` | QC direction queue | None |
| `qc-final-navigation.json` | Final QC queue | None |

Control names and source titles are recorded in [CONTROL_CATALOG.md](CONTROL_CATALOG.md). These journeys verify menu accessibility and screen navigation; they do not submit receipt, export, delivery, QC or rework transactions.

## 4. What remains before business E2E can be called complete

The codebase has several dynamically composed DevExpress forms. Some controls are created at runtime and may not expose a stable AutomationId or Invoke/Value pattern until the application is inspected on the target workstation. In particular:

- **Receiving:** opening the worklist is covered; QR scan and receipt posting need a dedicated disposable receiving reference and verified control IDs.
- **Export:** `ExportFormSV` is opened from a slot double-click/context action on the warehouse canvas. The runner does not guess screen coordinates or choose an arbitrary slot. Provide a seeded disposable slot and map its UIA element before testing the export commit.
- **Delivery:** opening `HVN_PGH` is covered; QR scanning and final delivery commit require isolated order/LOT fixtures and verified runtime UIA controls.
- **QC/Rework:** queue navigation is covered; decision/confirmation buttons must not be invoked against live work items. A disposable test ticket and explicit reset/cleanup contract are required.
- **Data assertions:** UI screenshots and titles are not proof of SQL persistence. Each mutating journey must assert the corresponding stock, SlotLot, history, pending-delivery and workflow state against a dedicated test database.

Never run mutating UI journeys against production. The SQL harness has a separate fail-closed environment policy; UI journeys must use an equivalent explicit QC environment configuration before enabling submit actions.

## 5. Evidence and acceptance rules

Each step writes a screenshot and the run writes `pctp-ui-journey-report.json`. A navigation test is PASS only when the target window is actually observed. A business E2E test must additionally prove persisted postconditions and cleanup. A planned journey must remain planned until it has been run on the Windows workstation against isolated test data and its artifacts have been reviewed.


## 6. Consolidated evidence summary

After the suite has completed, generate a Markdown and CSV roll-up from all per-journey JSON reports:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PCTP.QC.UI\Invoke-PctpUiEvidenceSummary.ps1 `
  -EvidenceDirectory ".\artifacts\pctp-ui-suite"
```

Outputs:
- `pctp-ui-evidence-summary.md` — journey-level PASS/FAIL, step counts, screenshot counts, report paths and failure messages.
- `pctp-ui-evidence-summary.csv` — machine-readable summary.

The evidence summary intentionally preserves navigation semantics. It does not upgrade navigation PASS into a transaction PASS. See [BUSINESS_E2E_ACCEPTANCE.md](BUSINESS_E2E_ACCEPTANCE.md) for the workflow-by-workflow acceptance matrix, data assertions and cleanup requirements.
