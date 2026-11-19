# PCTP Document Runtime

## Architecture boundary

The Document Runtime separates trusted executable bindings from editable
presentation metadata.

- `DocumentRegistry` is populated only by application startup code.
- A registered definition is the allow-list for module, header/detail read
  views, workflow code and command bindings.
- SQL document metadata may change captions, field order, dimensions,
  visibility, read-only presentation and approved editor codes.
- SQL metadata must never supply SQL text, arbitrary object names,
  procedure names, C# type names, expressions or executable callbacks.
- A configured definition must match its trusted C# definition exactly;
  mismatch is a fail-closed configuration error.
- Hiding a command in DevExpress is not authorization. Every command must
  still pass the existing SEC/RBAC and data-scope checks, then the shared
  workflow transition service and the SQL business guard.

## Current implementation

`DocumentFormDefinition.cs` currently provides:

- immutable document and command contracts;
- a duplicate-rejecting, case-insensitive application registry;
- identifier validation for schema-qualified SQL object names;
- command membership checks;
- `DocumentMetadataGuard.DemandMatchesRegistry` for fail-closed
  comparison of SQL metadata with the application allow-list.

Migration 039 adds SQL presentation metadata tables. It deliberately does
not create a second workflow engine or seed unverified MES view/procedure
names.

## Required next integration steps

1. Register verified module definitions during application startup.
2. Implement a metadata repository using parameterized SQL and read-only
   access for normal runtime users.
3. Load metadata and call `DocumentMetadataGuard` before building any UI.
4. Implement the DevExpress list/editor renderer from approved field/layout
   metadata; custom QR/LOT editors remain registered C# controls.
5. Implement command dispatch by resolving a command code only through the
   trusted definition. Reuse existing AuthorizationGuard/SEC and
   IWorkflowTransitionService; do not create a parallel permission or
   workflow engine.
6. Route every mutation to a fixed stored procedure binding, with server-side
   authorization, data scope, state, concurrency and audit checks.
7. Add integration tests for unknown modules, mismatched views, unknown
   commands, unauthorized scope, invalid transition and stale rowversion.

Until those steps are completed, existing forms remain the operational
entry points. Do not switch production forms to metadata-driven execution
merely because migration 039 has been applied.
