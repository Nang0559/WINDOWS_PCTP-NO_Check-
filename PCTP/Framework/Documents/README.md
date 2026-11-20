# PCTP Document Runtime

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

## Status

Done: contracts, registry, validator, resolver, dispatcher, SQL metadata/data/command bindings,
DevExpress list/editor/detail, COMPONENT_DEMAND definition, migration 050 (views + seed), contract tests
(`PCTP.QC.Tests/DocumentRuntimeTests.cs`).

Not done / known limits:

- Read views are filtered by SITE in the application only (`SqlDocumentDataProvider`); department-level
  data scope is not applied to reads yet.
- Header editing and save are out of scope for v1: all header editors are read-only.
- `ISSUE_COMPONENT` needs a module-supplied `IDocumentCommandInputProvider` (LOT/slot picker). Until one is
  written the button stays disabled and `FormYeuCauVaXuatLinhKien` remains the entry point.
- No `IDocumentWorkflowGate` implementation yet (needed before registering a command that requires a transition).
- `LayoutJson`, `LookupCode` and `RequiredStatusCode` are stored but not interpreted.
- No menu entry: existing forms stay the operational entry points until acceptance tests pass on DB57.
- Migration 050 replaces `UQ_FVN_DocumentField_Code` with a per-region unique key so key/site columns can appear in both regions.
