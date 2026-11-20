# MES Shared Presentation Controls

This folder contains reusable DevExpress WinForms controls for MES presentation.
Controls in this folder are presentation-only: they receive an already-authorized model or
DataTable from a Form/Application Service and must not open SQL connections, execute stored
procedures, or make workflow state transitions.

## Current controls

| Control | Responsibility | Contract |
|---|---|---|
| `FvnKpiStageStripControl` | Five-stage production KPI strip | `SetValues(plan, components, orders, production, done)` |
| `FvnProgressSummaryControl` | Material issue and operation completion progress | `SetProgress(componentPercent, operationPercent)` |
| `FvnScopeBadgeControl` | Shows owned-scope vs related read-only context | `SetRelated(bool)` |
| `FvnHoldAlertControl` | Shows current dependency / waiting point and reason | `SetDependency(waitingAt, reason, relatedReadOnly)` |
| `FvnDepartmentTaskGridControl` | Standard non-editable department task grid | `Grid`, `View`, `Bind(dataSource)` |
| `FvnSiteDateRangeFilterControl` | Reusable Site and date-range filter | `SiteCode`, `FromDate`, `ToDate`, `RefreshRequested`, `SetBusy(bool)` |
| `FvnWorkflowTimelineControl` | Standard non-editable workflow event timeline | `Bind(events, entityType, entityId)`, `Clear(message)` |

## Access-mode rules

- **Own**: the row belongs to a department in the signed-in user's authorized scope.
  The owning workflow screen may expose commands, but the Application Service must authorize
  each command and its Site/Department scope again.
- **RelatedReadOnly**: a configured dependency makes another department's work visible.
  Show status, waiting point, reason and timeline as context only. Do not expose commands.
- **Monitor**: aggregate/read-only overview within the Site/Department scope returned by the
  Application Service. A monitoring control must not imply that the viewer can act.

UI enabled/disabled state is a usability aid, not a security boundary. Authorization remains
in the Application Service / repository / SQL command boundary.

## Composition

- `FormMesProductionControlTower` composes Site/date filters, KPI strip, task grid, progress summary,
  scope badge and dependency alert. It retains the orchestration, refresh lifecycle and navigation.
- `FormProductionWorkflowTimeline` composes the shared timeline control and delegates reads
  to `IProductionWorkflowService`.

## Extension rules

1. Extract a UserControl only when it has a stable responsibility used by at least two forms
   or is a standard interaction that should be consistent across modules.
2. Pass typed models or read-only tabular data into controls. Never put business SQL in a control.
3. Keep workflow transitions in existing Application Services and shared workflow/approval engine.
4. Related-department content is always read-only, including double-click and context-menu actions.
5. Prefer small composition controls over a giant all-purpose MES UserControl.


### Production Flow Control Tower

- `FvnFlowStepperControl`: five-stage horizontal flow with pending, active,
  complete, blocked, true HOLD (paused) and related-department states.
- `FvnHoldListControl`: reusable read-only grid for multiple blockers. Current
  Control Tower supplies derived blockers; it does not pretend they are manually
  registered HOLD records.
- The owning module service supplies task/access enums and the controls remain
  presentation-only. No workflow transition or stock-posting method is exposed.
