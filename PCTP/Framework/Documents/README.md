# PCTP UI Runtime

## Three layers

| Layer | Where | Owns | Cannot do |
|---|---|---|---|
| 1. Trusted Registry | C# `Framework/Documents/Contracts` + `Registry`, per-module definition (e.g. `Modules/MesPlanning/Documents/ComponentDemandDefinition.cs`) | module code, header/detail views, registered columns and editor types, commands, procedures and their typed parameters, workflow binding, RBAC function/action | be changed without a release |
| 2. Approved Metadata | SQL migration 039 tables (`MES.FVN_Document*`) | captions, order, width, display format, region captions/visibility, command caption/visibility | add a field, view, procedure, command, editor outside the allow-list, or make a read-only field editable (restrict-only) |
| 3. Enforcement | SQL procedures + existing `AuthorizationGuard` | RBAC and data scope, workflow transition, preconditions, document state, transaction/concurrency, audit | n/a - this is the security boundary |

Hiding or disabling a DevExpress button is **never** authorization. Every command goes through
`DocumentCommandDispatcher` (RBAC action + SITE scope), the shared workflow gate when the command
requires a transition, and finally the registered stored procedure's own guards.

## Layout

```
Framework/Documents/
  Contracts/  DocumentFormDefinition, DocumentFieldDefinition, DocumentCommandDefinition(+parameters),
              DocumentMetadata (plain SQL-metadata models), DocumentIdentifiers (strict validators)
  Registry/   IDocumentRegistry, DocumentRegistry, DocumentDefinitionValidator (fail-closed metadata check)
  Runtime/    DocumentLayoutResolver -> ResolvedDocument, DocumentCommandDispatcher, SessionDocumentAuthorizer,
              SqlDocumentMetadataRepository, SqlDocumentDataProvider, SqlDocumentCommandExecutor,
              DocumentRuntimeServices (composition)
  UI/         DocumentListForm, DocumentEditorForm, DocumentDetailControl, DocumentGridConfigurator,
              IDocumentCommandInputProvider (module-supplied special editors)
Modules/MesPlanning/Documents/  ComponentDemandDefinition, MesDocumentModule
```

## Rules

- Metadata never carries SQL text, expressions, type names, procedure names or callbacks.
- Parameter source is fixed in code: `Input` (caller, undeclared keys rejected), `Actor` (always the
  signed-in user), `Site` (the authorized site), `IdempotencyKey` (generated).
- Views, columns and procedures are bracket-quoted two-part names validated at registration time.
- A mismatch between SQL metadata and the registry throws; there is no silent fallback to SQL values.
  Missing metadata falls back to code defaults (visible, read-only, captioned by field code).
- `WorkflowCode = NONE` marks documents whose state machine is a SQL status check rather than a
  workflow-engine entity. A command with `RequiresWorkflowTransition` fails closed without an
  `IDocumentWorkflowGate`.

## PCTP UI Designer (layer-2 UI)

Menu: **Công cụ → Thiết kế giao diện (PCTP UI Designer)**, visible with `ADMIN.DOCUMENT_DESIGNER` VIEW. Code:
`Design/DocumentDesignerService` (rules), `Design/SqlDocumentMetadataWriter` (calls migration 051), `UI/DocumentDesignerForm`.

| Action | Permission | What it can do |
|---|---|---|
| Open / preview / audit | VIEW | list documents registered in code, see SQL metadata, drift against the registry, change history |
| Lưu | EDIT | caption, order, width, display format, visibility of header/detail columns, commands and the two regions |
| Tạo giao diện | APPROVE | create SQL presentation configuration from an already C#-registered module; never creates a module or business operation |

What it deliberately cannot do: add a screen/module, view, procedure, command, field or editor; make a field editable; change
workflow or status restriction. A new document or field is a code release (layer 1) plus a seed migration. The same limits
are enforced again in SQL, together with RBAC, `ConfigVersion` conflict detection and audit.
After a save the runtime revalidates; other running clients pick the change up at next sign-in.

## Status

Done: contracts, registry, validator, resolver, dispatcher, SQL metadata/data/command bindings,
DevExpress list/editor/detail, COMPONENT_DEMAND definition, migration 050 (views + seed), contract tests
(`PCTP.QC.Tests/DocumentRuntimeTests.cs`, `DocumentRuntimeHardeningTests.cs`).

Hardening added on top of that:

- **Schema validation** - `DocumentDefinitionValidator.DemandMatchesSchema` checks the trusted registry against the
  real SQL objects through `IDocumentSchemaProbe` (`SqlDocumentSchemaProbe`: sys.columns / sys.parameters). Views must exist
  and expose every registered column with a compatible type; every command procedure must exist and its parameter list must
  match the declared one exactly (name, type, decimal precision/scale, no undeclared parameter).
- **Startup validation** - `MesDocumentModule.Initialize()` (called from `Program.cs` after sign-in, in the background, never
  throws) runs `DocumentRuntimeServices.ValidateAll()`: SQL metadata vs registry, then registry vs real schema.
  `MesDocumentModule.GetRuntime(module)` refuses to return a runtime when validation did not pass, so the menu entry fails closed
  with the reason.
- **Reads** - `DocumentListQuery` (paging, ordering, up to 8 filters on registered header fields) is turned into SQL by the pure
  `DocumentSqlBuilder`: identifiers come from the registry, values are always parameters, `LIKE` input is escaped, page size <= 500.
  The old `TOP 5000` path is gone (`ListHeaders(..., top)` now delegates to a single page).
- **Department data scope** - a definition may register `DepartmentField`. `SessionDocumentAuthorizer.GetDataScope` resolves the
  user's DEPARTMENT scopes with the same rules as `ProductionFlowService` (USER policy is a complete allow-list; SITE/ALL or
  DEPARTMENT `*` means every department). A scoped document with no granted department returns nothing (fail-closed); the detail
  view is scoped through its header row. `COMPONENT_DEMAND` registers no `DepartmentField`, so its behaviour is unchanged.
- **Workflow gate** - `DocumentWorkflowGate` implements `IDocumentWorkflowGate`: current status from `IDocumentStatusReader`
  (`SqlDocumentStatusReader`, reads `StatusField`), target status from `DocumentCommandDefinition.WorkflowTargetStatus`, decision
  from `IDocumentTransitionPolicy` (`WorkflowTransitionPolicyAdapter` over the shared `IWorkflowTransitionService`). No workflow,
  no target, no status field, or no key parameter all throw. Convention: a gated command declares one `Input` parameter whose
  `sourceFieldCode` is the document key. Not wired into `CreateRuntime()` because COMPONENT_DEMAND uses `NONE`.
- **Metadata** - `RequiredStatusCode` is interpreted as a restriction (button enabled only when the header `StatusField` equals it)
  and is rejected when the definition registers no `StatusField`. `LookupCode` is accepted only on a LOOKUP editor with a safe code.
- **UI** - `DocumentListForm` pages (100/page), filters one registered header field and shows the total; menu entry
  "Màn hình yêu cầu linh kiện" under Kho (permission `MES.COMPONENT_DEMAND`);
  `ComponentDemandIssueInputProvider` picks LOT/slot, WIP slot and quantity via the existing `IComponentDemandService`.

Not done / known limits:

- **Designer is not built or run on a real machine.** The service and the migration text are covered by contract tests
  (`DocumentDesignerTests.cs`); `DocumentDesignerForm`, `SqlDocumentMetadataWriter` and migration 051 need a Windows build and an
  acceptance run on a non-production DB (grant the function, save, conflict from two sessions, audit rows).
- No draft/approve workflow for layout changes: EDIT applies immediately (audited). A two-step approval needs a draft table.
- Editors, read-only/required flags, `LookupCode` and `RequiredStatusCode` are not editable in the designer.
- Layout of columns inside a region (tabs, groups, positions) is not modelled yet; `LayoutJson` is still not read.
- No rollback-to-version screen; the audit tab shows before/after JSON to revert by hand.

- **Not built or run on a real machine.** The non-UI code compiles and its 23 contract tests pass against stubs in a sandbox;
  the DevExpress UI files, `Program.cs`, `Main_APP.Menu.cs` and every SQL path need a Windows build and an acceptance run on DB57.
- `LayoutJson` is still stored but not read or interpreted. `LookupCode` is validated but no lookup editor consumes it yet.
- Department scope is applied to reads only. Commands are still authorized by RBAC action + SITE scope, and the SQL procedure
  remains the department/state enforcement point.
- `ISSUE_COMPONENT` input provider assumes source area `PART` (the detail view has no SupplyMode/SourceAreaCode); a MAKE
  component is rejected by the procedure.
- The menu entry takes the first Site in the user's data scopes (same approach as the Control Tower); users with only an ALL
  scope and no Site are told to be assigned one.
- No SQL integration test yet for `SqlDocumentSchemaProbe` / `SqlDocumentDataProvider`; only the pure builder and validator are tested.
- Header editing and save are out of scope for v1: all header editors are read-only.
- Migration 050 replaces `UQ_FVN_DocumentField_Code` with a per-region unique key so key/site columns can appear in both regions.


## UI Designer hardening — migration 053

Migration 052's SQL-created screen declaration flow is retired because SQL must not extend the trusted screen catalogue. Run `053_MES_UiDesignerTrustedRegistryOnly.sql` after 051/052. It quarantines module codes created through the retired SQL declaration path, removes the declaration procedures/table, and makes menu metadata reference only existing `MES.FVN_DocumentDefinition` entries. The new `MES.usp_FVN_UiScreenMenu_List` and `MES.usp_FVN_UiScreenMenu_Save` procedures configure menu placement only for registered screen metadata; they cannot create views, fields, commands, procedures or workflows.

The UI action **Tạo giao diện** provisions presentation metadata for a module already registered in the C# Trusted Registry. It does not create a new business module. A genuinely new screen data contract still requires a C# definition and schema/seed migration.
