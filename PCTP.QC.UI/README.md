# PCTP WinForms UI automation

## Startup/accessibility smoke test

Run on a Windows workstation where the application can be launched with the current user's normal permissions:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\PCTP.QC.UI\Invoke-PctpUiSmoke.ps1 `
  -ExePath "H:\path\to\PCTP.exe" `
  -OutputDirectory ".\artifacts\pctp-ui-smoke" `
  -ExpectedWindowTitleRegex "PCTP|FCC|Auto Ware"
```

The runner launches the app, waits for an accessible main window, checks the title regex, exports the Windows UI Automation control tree, captures the main-window screenshot, and writes a JSON report. It fails if the process exits during startup, the window is inaccessible, or the title does not match.

To close the main window after a successful smoke check, add `-CloseOnSuccess`.

## Full business journeys

The smoke script intentionally does not click guessed controls or submit business data. Before automating receive, export, delivery, and rework journeys, add stable `AutomationId` values to the corresponding WinForms controls and define an isolated test-data/reset contract. Use a dedicated QC account and never point a UI run at production data. Capture the resulting report and screenshot as CI artifacts.

This is currently a startup/accessibility smoke test; it is not evidence that the full business journeys have passed.
