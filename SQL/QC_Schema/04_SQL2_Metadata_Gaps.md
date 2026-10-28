# SQL2 metadata coverage / unresolved dependencies

Generated from the read-only SQL2 metadata export and docs/SCHEMA_COVERAGE.csv.
This report does not execute SQL. DEFAULT constraints are represented inline in 02_Generated_From_DB57.sql.

## Generated object counts

| Object | Count |
|---|---:|
| Allowlisted tables | 61 |
| PK constraints | 40 |
| Unique constraints | 10 |
| Non-constraint indexes | 28 |
| Foreign keys emitted | 16 |
| CHECK constraints emitted | 21 |

## Dependencies requiring review

- None detected in the exported FK metadata for this allowlist.

## Explicit limitations

- SQL modules (procedures, views, functions, triggers) are not auto-deployed from this report; compare their definitions with reviewed source scripts.
- FKs whose referenced table is outside the generated scope are reported; strict CI mode fails rather than silently accepting them.
- SQL2 FK metadata does not export IsNotForReplication; FK replication behavior is therefore not asserted.
- SQL2 index metadata does not export fill factor, pad index, ignore-duplicate-key, or row/page-lock options; those options are not asserted.
- This metadata export does not provide permissions, SQL Agent jobs, synonyms, linked-server dependencies, or application data.
- Generated DDL is intended only for disposable PCTP_QC_CI; it must never target DB57 or production.
