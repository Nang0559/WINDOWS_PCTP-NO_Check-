# MES forms — shared construction pattern

## 1. Scope and entry points

The MES navigation is registered in `Shell/Main_APP.cs`. It opens these functional workspaces:

| Workspace | Form | Purpose |
|---|---|---|
| Component warehouse PART | `Modules/KhoLinhKien/UI/FormKhoLinhKien.cs` | Purchase request, QR receiving, PART stock, LOT transfer, issue to WIP, trace |
| Production plan upload | `Modules/MesPlanning/UI/FormUploadKeHoachSanXuat.cs` | Excel file/sheet selection, preview, validation and transactional import |
| Plan worklist/workflow | `Modules/MesPlanning/UI/FormProductionPlanWorkflow.cs` | Plan list, VALIDATE, FIRM, CANCEL, workflow timeline and selecting a FIRM plan line |
| Production execution | `Modules/MesPlanning/UI/FormDieuHanhSanXuat.cs` | Production order, production LOT, routing, labels, actual component consumption and operation execution |
| LOT genealogy | `Modules/MesPlanning/UI/FormTruyXuatLinhKien.cs` | Upstream/downstream trace from actual consumption and production facts |
| Workflow history | `Modules/MesPlanning/UI/FormProductionWorkflowTimeline.cs` | Read-only plan/order/LOT event history |

The PART warehouse is one permission-aware workspace. It does not create separate competing screens for receiving, stock and issue; each tab is shown only when its own permission is present.

## 2. Layering rule

A form is a presentation adapter. It must not issue SQL, select a stored procedure name from user input, or make a state transition by directly updating a table.

```text
Main_APP navigation
    |
    v
DevExpress XtraForm / GridControl / Editors
    |
    v
Application Service (authorization + validation + scope)
    |
    v
Repository interface
    |
    v
SQL Repository (parameters + fixed stored procedure/query)
    |
    v
MES SQL transaction, inventory ledger, LOT and genealogy
```

Responsibilities:

- **UI:** filters, editors, grid selection, confirmation prompts, validation feedback, printing and QR scanning.
- **Service:** trusted actor from `AuthorizationGuard.ActorCode`; action permission; Site scope; business input validation; calls repository.
- **Repository:** parameterized SQL and fixed stored-procedure binding. No SQL object name is accepted from a form.
- **SQL:** final concurrency, document state, inventory balance, idempotency, workflow transition, audit and genealogy guards.

A hidden button is only a usability rule. The Service and SQL remain authoritative.

## 3. Component flow

### 3.1 Purchase and receiving

Use `FormKhoLinhKien` and its existing PO/receiving services:

1. Upload component PO into the staging/import workflow.
2. Validate and release the purchase request through its configured workflow.
3. Open the receiving tab and scan the supplier QR label.
4. Resolve the destination PART slot from the active Site/area/slot lookup.
5. Post the receipt through `IComponentReceivingService`; do not insert stock from the UI.
6. Refresh LOT and stock projections after the SQL transaction succeeds.

Permissions are separated by function: `WAREHOUSE.COMPONENT_STOCK`, `WAREHOUSE.COMPONENT_RECEIPT`, PO permission, transfer permission, `MES.COMPONENT_DEMAND`, `MES.COMPONENT_ISSUE` and `REPORT.COMPONENT_TRACEABILITY`.

### 3.2 PART stock and PART → WIP

The same workspace reads the stock projection and uses the existing stock transfer / component demand services. The UI selects a source LOT and source slot, a target WIP slot, and a quantity. The service checks Site scope and the SQL posting routine rechecks stock, slot/area and idempotency before posting. The form never changes `FVN_InventoryBalance` directly.

## 4. Production plan flow

### 4.1 Import

`FormUploadKeHoachSanXuat` reads the chosen workbook and explicitly selected sheet. It validates required headers and values before enabling Upload. `ProductionPlanService.ImportRows` checks action/scope and business constraints; `ProductionPlanRepository.ImportRows` calls the fixed `MES.usp_FVN_ProductionPlan_ImportRow` procedure inside one SQL transaction. A failed row rolls back the batch.

### 4.2 Workflow

`FormProductionPlanWorkflow` is a read-only worklist. It reads `MES.usp_FVN_ProductionPlan_List` through `IProductionPlanService`; it does not edit status in the grid.

- IMPORTED (0) → VALIDATED (10)
- VALIDATED (10) → FIRM (20)
- IMPORTED/VALIDATED → CANCELLED (900)

All transitions pass through `ProductionWorkflowService`, which checks Site/action scope and calls the shared `IWorkflowTransitionService.EnsureCanTransition`. SQL then rechecks the configured transition and current row state. FIRM uses `MES.usp_FVN_ProductionPlan_Firm`; migration 038 generates component demand from the effective IFS BOM inside the same transaction. The UI does not generate BOM rows itself.

The plan picker returns only a selected line whose header status is FIRM.

## 5. Production order, LOT and routing

### 5.1 Order and LOT

The execution form opens the FIRM plan worklist instead of requiring the operator to type an internal plan-line ID. `ProductionExecutionService.CreateOrder` also checks that the source plan is FIRM before calling the order creation procedure. The SQL procedure snapshots the effective BOM/routing, generates the order material requirements and assigns the order status. The application service rejects a non-FIRM source line, and migration 046 adds a database trigger to reject direct-SQL inserts that are not linked to a FIRM plan line.

LOT creation calls `MES.usp_FVN_ProductionLot_Create`. The configured part policy decides whether the output is WIP or FG. LOT identity and numbering remain in the SQL MES domain; the form does not construct a LOT number.

### 5.2 Operation flow

The form loads the effective routing steps for the selected LOT through `GetRoutingSteps`. Selecting a row supplies the trusted RoutingStepId and OperationNo to the workflow/execution actions.

Operation workflow states:

- 0 → 10 PREPARE
- 10 → 20 READY (SQL checks previous operation and prepared labels)
- 20 → 30 START
- 30 → 40 PAUSE
- 40 → 30 RESUME
- actual completion is recorded only by `CompleteOperation`; migration 036's SQL trigger synchronizes the operation workflow state to 50 and appends audit/timeline events.

This avoids a second UI-only completion path. DIRECT/VIA_WIP transfer behavior stays in the existing production-operation SQL routine and shared inventory ledger.

### 5.3 Labels and component consumption

Label preparation, print authorization, QR apply and void use the existing `IProductionExecutionService` methods. Actual component consumption selects the order material line, production LOT, source slot and quantity, then scans the supplier/component QR in `FVN1|LOT` format. The service resolves that LOT only within the production LOT's Site; the repository calls the existing consumption procedure and preserves the raw QR for audit. SQL validates the component PartNo, available balance and slot before posting the transaction and genealogy link. BOM demand is not treated as actual consumption.

Order creation starts in CREATED. RELEASE (0 → 10) is an explicit APPROVE transition, followed by START (10 → 20), PAUSE, RESUME, COMPLETE and CLOSE through the shared workflow service. SQL guards prevent completion while routing execution is incomplete and prevent close while labels remain unused.

## 6. Genealogy and timeline

- `FormTruyXuatLinhKien` queries `IProductionTraceService.Trace` with UPSTREAM or DOWNSTREAM.
- The repository calls the fixed `MES.usp_FVN_ComponentLot_Trace` procedure.
- Results are based on actual material LOT consumption, production LOT and execution records, not only planned BOM.
- `FormProductionWorkflowTimeline` reads `MES.usp_FVN_ProductionWorkflow_Timeline`; it is read-only.

## 7. Shared workflow composition

`MesWorkflowComposition.Create()` is the composition root for MES forms. It builds the existing shared `WorkflowRepository` / `WorkflowTransitionService` and injects that service into `ProductionWorkflowService`. MES does not create another transition engine or duplicate workflow table.

## 8. Document Framework and metadata boundary

The trusted document contract is defined in `Framework/Documents/DocumentFormDefinition.cs`:

- C# `DocumentRegistry` is the allow-list for modules, approved views, workflow codes and commands.
- `DocumentCommandDefinition` binds a command to a fixed, code-owned procedure and permission.
- `DocumentMetadataGuard` verifies SQL configuration against that trusted definition.
- Migration 039 stores captions, regions/layout, field display properties and command visibility. Metadata must never supply arbitrary SQL, a procedure name, C# type, expression, event handler or callback.

The MES screens listed above are currently strongly typed DevExpress workspaces backed by their existing application services. Migration 039 and the registry are the safety foundation for a later metadata-driven generic document host; these screens do not pretend to be dynamically rendered from metadata before their approved views and field mappings have been registered. A module should be moved to the generic host only after its header/detail views, field allow-list, command bindings and workflow mapping have been verified.

## 9. Adding the next MES form

1. Define the document/module code and its action permissions.
2. Reuse an existing application service if the domain operation already exists; otherwise add interface → service → repository.
3. Add only fixed repository methods/procedures. Keep SQL object names out of UI and metadata.
4. Register transitions in the shared workflow configuration and add SQL-side guards/audit in the same transaction.
5. Build a DevExpress `XtraForm`: filters/commands at the top, read-only `GridControl` worklist, detail region, and status/error footer.
6. Resolve selection into domain IDs from the focused data row; do not ask operators to type internal IDs when a picker can provide them.
7. Check action and Site scope in the service. Recheck concurrency/state/inventory in SQL.
8. Add navigation only after the form's VIEW permission and service-side authorization are in place.
9. Add tests for valid transition, invalid transition, unauthorized Site, duplicate/idempotent posting, and rollback on SQL failure.


## 10. MES Control Tower — one production flow, role-specific views

The read-only entry point is `UI/FormMesProductionControlTower.cs`.
It is backed by `IProductionFlowService` → `ProductionFlowService` →
`IProductionFlowRepository` → `ProductionFlowRepository` →
`MES.usp_FVN_ProductionFlow_Dashboard` (migration 047).

The five visible stages are:

1. **Kế hoạch** — imported/validated/firm plan.
2. **Cấp linh kiện** — BOM demand, required/issued/outstanding quantities.
3. **Lệnh sản xuất** — released order and planned/completed quantity.
4. **Sản xuất thực tế** — production LOTs and completed/total routing operations.
5. **Hoàn thành** — completed/closed order.

The worklist also shows the department, line, shift, product, last workflow event,
the current waiting stage and a derived waiting reason. The latter is a monitoring
indicator inferred from canonical facts (e.g. outstanding component quantity or
incomplete routing); it is not a separate MES HOLD transaction or an approval.

### Read-only boundary and action ownership

- Control Tower has no command that changes a plan, demand, issue, order, LOT,
  label, inventory balance or operation state.
- Cross-department rows are marked `Liên quan · chỉ xem`; the actionable-work
  column is suppressed for those rows.
- Task labels mean **work that the signed-in user's current permissions and
  department scope allow**, not a claim that a task has been individually assigned
  to that employee.
- Opening a workflow form is separately gated by its VIEW permission. The form
  disables action buttons without the matching action permission.
- ProductionExecutionService now requires both Site scope and owning
  DEPARTMENT scope for CREATE/CONFIRM/APPROVE/PRINT/CANCEL operations.
  ProductionWorkflowService applies the same department guard to order and
  operation transitions. SQL remains the final state/inventory guard.
- Planner plan transitions remain protected by `MES.PLAN_UPLOAD` and Site scope;
  warehouse issue remains protected by the separate `MES.COMPONENT_ISSUE`
  permission and Site scope.

### Department relationship configuration

Migration 047 adds `MES.FVN_ProductionDepartmentRelation`.
Configure rows for each actual Site and product dependency:

- `OwnerDepartmentCode`: department whose users need the context.
- `RelatedDepartmentCode`: department producing/handling a related component or
  upstream/downstream step.
- `OwnerProductPartNo`: optional finished/subassembly product made by the owner department.
- `RelatedProductPartNo`: optional component/subassembly produced by the related department.
  Either may be NULL to make that side of the dependency general for the department pair.
- `RelationType`: COMPONENT, UPSTREAM, DOWNSTREAM or SHARED.

The read model returns the related department's production rows only when an
active relation matches the owner's allowed department scope. This configuration
does not grant write permission to the related department. Grant `MES.PRODUCTION_FLOW
/ VIEW` and `SITE` or `DEPARTMENT` data scopes using the existing security
administration workflow.

### Freshness and future assignment integration

The Control Tower supports manual refresh and a 30-second polling refresh. It
reads current canonical MES rows and workflow events; it does not copy them into
a second operational database. The current "Việc thuộc quyền xử lý" is a
permission-and-scope-derived work queue. If the plant later requires individual
assignment, due dates, escalation or explicit HOLD ownership, those must be
connected to the existing approval/task assignment source (or a formally
configured workflow task model); they must not be guessed from department
membership.
