> **Historical artifact — regenerate before use.** This report predates the PCTP-only runtime inventory scope and may include unrelated B7R2_FCC objects. CI regenerates it from the scoped coverage CSV and current SQL2 metadata.

# SQL2 metadata coverage / unresolved dependencies

Generated from the read-only SQL2 metadata export and docs/SCHEMA_COVERAGE.csv.
This report does not execute SQL. DEFAULT constraints are represented inline in 02_Generated_From_DB57.sql.

## Generated object counts

| Object | Count |
|---|---:|
| Allowlisted tables | 19 |
| PK constraints | 14 |
| Unique constraints | 4 |
| Non-constraint indexes | 5 |
| Foreign keys emitted | 2 |
| CHECK constraints emitted | 2 |

## Dependencies requiring review

- FK dbo.sys_WorkflowTransitions.FK_Transition_From: references dbo.sys_WorkflowStatuses, outside the generated allowlist; intentionally omitted.
- FK dbo.sys_WorkflowTransitions.FK_Transition_To: references dbo.sys_WorkflowStatuses, outside the generated allowlist; intentionally omitted.

## Explicit limitations

- SQL modules (procedures, views, functions, triggers) are not auto-deployed from this report; compare their definitions with reviewed source scripts.
- FKs whose referenced table is outside the allowlist are intentionally omitted, not silently treated as restored.
- This metadata export does not provide permissions, SQL Agent jobs, synonyms, linked-server dependencies, or application data.
- Generated DDL is intended only for disposable PCTP_QC_CI; it must never target DB57 or production.
