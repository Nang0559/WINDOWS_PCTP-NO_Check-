#!/usr/bin/env python3
"""Generate reviewed SQL Server indexes and constraints from SQL2 metadata text.

Input is a read-only metadata export, not executable SQL. Only dbo tables
allow-listed as GAP-no-git-ddl/table/on_db57_export=có are emitted.
DEFAULT constraints are deliberately not emitted because 02_Generated_From_DB57.sql
already preserves defaults inline.
"""
from __future__ import annotations
import argparse
import csv
import re
import sys
from collections import defaultdict
from pathlib import Path

def ident(value: str) -> str:
    return "[" + value.replace("]", "]]") + "]"

def literal(value: str) -> str:
    return "N'" + value.replace("'", "''") + "'"

def parse_section(text: str, required: set[str]) -> list[dict[str, str]]:
    lines = text.splitlines()
    for header_i, header in enumerate(lines):
        names = re.findall(r"\S+", header)
        if not required.issubset(names):
            continue
        starts = [m.start() for m in re.finditer(r"\S+", header)]
        sep_i = next((i for i in range(header_i + 1, min(header_i + 4, len(lines)))
                      if lines[i].startswith("---")), None)
        if sep_i is None:
            continue
        rows = []
        for line in lines[sep_i + 1:]:
            if not line.strip():
                break
            if line.startswith("--") or line.startswith("===="):
                break
            rows.append({
                name: line[start:(starts[j + 1] if j + 1 < len(starts) else len(line))].strip()
                if start < len(line) else ""
                for j, (name, start) in enumerate(zip(names, starts))
            })
        return rows
    raise ValueError("Metadata section not found; required fields: " + ", ".join(sorted(required)))

def load_scope(path: Path) -> set[tuple[str, str]]:
    allowed = set()
    with path.open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            generated_table_ddl = "SQL/QC_Schema/02_Generated_From_DB57.sql" in row.get("git_ddl_files", "")
            scope_class = row.get("class", "").strip() == "GAP-no-git-ddl" or (
                generated_table_ddl and row.get("class", "").strip() in {"OK-testable", "GAP-ci-schema"})
            if (scope_class
                and row.get("kind", "").strip().casefold() == "table"
                and row.get("on_db57_export", "").strip().casefold() in {"có", "co", "yes", "true"}):
                allowed.add(("dbo", row.get("object", "").strip().casefold()))
    if not allowed:
        raise ValueError("Coverage allowlist selected zero tables")
    return allowed

def group_rows(rows, keys):
    groups = defaultdict(list)
    for row in rows:
        groups[tuple(row.get(k, "") for k in keys)].append(row)
    return groups

def generate(metadata: str, allowed: set[tuple[str, str]]) -> tuple[str, str, dict[str, int]]:
    index_fields = {"SchemaName", "TableName", "IndexName", "IndexType", "IsPrimaryKey",
                    "IsUnique", "IsUniqueConstraint", "IsDisabled", "HasFilter",
                    "FilterDefinition", "KeyOrdinal", "IsIncludedColumn", "IsDescendingKey", "ColumnName"}
    fk_fields = {"ParentSchema", "ParentTable", "ParentColumn", "ReferencedSchema",
                 "ReferencedTable", "ReferencedColumn", "ForeignKeyName", "ColumnOrdinal",
                 "OnDelete", "OnUpdate", "IsDisabled", "IsNotTrusted"}
    check_fields = {"SchemaName", "TableName", "ConstraintName", "Definition",
                    "IsDisabled", "IsNotTrusted", "IsNotForReplication"}
    indexes = parse_section(metadata, index_fields)
    fks = parse_section(metadata, fk_fields)
    checks = parse_section(metadata, check_fields)

    selected_indexes = [r for r in indexes
        if r.get("SchemaName", "").casefold() == "dbo"
        and ("dbo", r.get("TableName", "").casefold()) in allowed
        and r.get("IndexName") and r.get("ColumnName")]
    selected_fks = [r for r in fks
        if r.get("ParentSchema", "").casefold() == "dbo"
        and ("dbo", r.get("ParentTable", "").casefold()) in allowed]
    selected_checks = [r for r in checks
        if r.get("SchemaName", "").casefold() == "dbo"
        and ("dbo", r.get("TableName", "").casefold()) in allowed
        and r.get("ConstraintName") and r.get("Definition") not in {"", "NULL"}]

    output = [
        "/*",
        " AUTO-GENERATED from SQL2 complete DDL metadata for the reviewed GAP-no-git-ddl allowlist.",
        " Contains PK/unique constraints, indexes, foreign keys and CHECK constraints only.",
        " DEFAULTs are already emitted inline in 02_Generated_From_DB57.sql and are not duplicated.",
        " Review required. Deploy only after 02_Generated_From_DB57.sql and only to disposable PCTP_QC_CI.",
        " Never apply this file to DB57 or production.",
        "*/", "SET ANSI_NULLS ON;", "SET QUOTED_IDENTIFIER ON;", "GO", ""
    ]
    counts = {"tables": len(allowed), "indexes": 0, "primary_keys": 0,
              "unique_constraints": 0, "foreign_keys": 0, "checks": 0}
    manual = []

    idx_groups = group_rows(selected_indexes, ["SchemaName", "TableName", "IndexName"])
    for (schema, table, name), rows in sorted(idx_groups.items(), key=lambda item: tuple(x.casefold() for x in item[0])):
        rows.sort(key=lambda r: (int(r.get("IsIncludedColumn") or 0),
                                int(r.get("KeyOrdinal") or 0),
                                r.get("ColumnName", "").casefold()))
        first = rows[0]
        key_rows = [r for r in rows if r.get("IsIncludedColumn") != "1" and int(r.get("KeyOrdinal") or 0) > 0]
        include_rows = [r for r in rows if r.get("IsIncludedColumn") == "1"]
        if not key_rows:
            manual.append(f"Index {schema}.{table}.{name}: no key columns in metadata; skipped.")
            continue
        keys = ", ".join(ident(r["ColumnName"]) + (" DESC" if r.get("IsDescendingKey") == "1" else " ASC")
                         for r in sorted(key_rows, key=lambda r: int(r["KeyOrdinal"])))
        qtable, qname = f"{ident(schema)}.{ident(table)}", ident(name)
        is_pk, is_uc = first.get("IsPrimaryKey") == "1", first.get("IsUniqueConstraint") == "1"
        unique = first.get("IsUnique") == "1"
        kind = first.get("IndexType", "NONCLUSTERED").upper()
        if is_pk or is_uc:
            constraint_kind = "PRIMARY KEY" if is_pk else "UNIQUE"
            output += [
                f"IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID({literal(schema + '.' + table)}) AND name = {literal(name)})",
                "BEGIN",
                f"    ALTER TABLE {qtable} ADD CONSTRAINT {qname} {constraint_kind} {kind} ({keys});",
                "END;"
            ]
            counts["primary_keys" if is_pk else "unique_constraints"] += 1
        else:
            unique_sql = "UNIQUE " if unique else ""
            include_sql = (" INCLUDE (" + ", ".join(ident(r["ColumnName"]) for r in include_rows) + ")") if include_rows else ""
            filter_def = first.get("FilterDefinition", "")
            where_sql = (" WHERE " + filter_def) if first.get("HasFilter") == "1" and filter_def not in {"", "NULL"} else ""
            output += [
                f"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID({literal(schema + '.' + table)}) AND name = {literal(name)})",
                "BEGIN",
                f"    CREATE {unique_sql}{kind} INDEX {qname} ON {qtable} ({keys}){include_sql}{where_sql};",
                "END;"
            ]
            counts["indexes"] += 1
        if first.get("IsDisabled") == "1":
            output.append(f"ALTER INDEX {qname} ON {qtable} DISABLE;")
        output += ["GO", ""]

    fk_groups = group_rows(selected_fks, ["ParentSchema", "ParentTable", "ForeignKeyName"])
    for (ps, pt, name), rows in sorted(fk_groups.items(), key=lambda item: tuple(x.casefold() for x in item[0])):
        rows.sort(key=lambda r: int(r.get("ColumnOrdinal") or 0))
        first = rows[0]
        rs, rt = first.get("ReferencedSchema", ""), first.get("ReferencedTable", "")
        if not rs or not rt or (rs.casefold(), rt.casefold()) not in allowed:
            manual.append(f"FK {ps}.{pt}.{name}: references {rs}.{rt}, outside the generated allowlist; intentionally omitted.")
            continue
        if any(r.get("ReferencedSchema") != rs or r.get("ReferencedTable") != rt for r in rows):
            manual.append(f"FK {ps}.{pt}.{name}: inconsistent referenced target rows; skipped.")
            continue
        parent_cols = ", ".join(ident(r["ParentColumn"]) for r in rows)
        ref_cols = ", ".join(ident(r["ReferencedColumn"]) for r in rows)
        qparent, qref, qname = f"{ident(ps)}.{ident(pt)}", f"{ident(rs)}.{ident(rt)}", ident(name)
        disabled, untrusted = first.get("IsDisabled") == "1", first.get("IsNotTrusted") == "1"
        with_clause = "WITH NOCHECK" if (disabled or untrusted) else "WITH CHECK"
        delete_action = first.get("OnDelete", "NO_ACTION").replace("_", " ")
        update_action = first.get("OnUpdate", "NO_ACTION").replace("_", " ")
        output += [
            f"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID({literal(ps + '.' + pt)}) AND name = {literal(name)})",
            "BEGIN",
            f"    ALTER TABLE {qparent} {with_clause} ADD CONSTRAINT {qname} FOREIGN KEY ({parent_cols})",
            f"        REFERENCES {qref} ({ref_cols}) ON DELETE {delete_action} ON UPDATE {update_action};",
            "END;"
        ]
        if disabled:
            output.append(f"ALTER TABLE {qparent} NOCHECK CONSTRAINT {qname};")
        elif not untrusted:
            output.append(f"ALTER TABLE {qparent} WITH CHECK CHECK CONSTRAINT {qname};")
        counts["foreign_keys"] += 1
        output += ["GO", ""]

    for row in sorted(selected_checks, key=lambda r: (r["SchemaName"].casefold(), r["TableName"].casefold(), r["ConstraintName"].casefold())):
        schema, table, name, definition = row["SchemaName"], row["TableName"], row["ConstraintName"], row["Definition"]
        qtable, qname = f"{ident(schema)}.{ident(table)}", ident(name)
        disabled, untrusted = row.get("IsDisabled") == "1", row.get("IsNotTrusted") == "1"
        nfr = " NOT FOR REPLICATION" if row.get("IsNotForReplication") == "1" else ""
        with_clause = "WITH NOCHECK" if (disabled or untrusted) else "WITH CHECK"
        output += [
            f"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID({literal(schema + '.' + table)}) AND name = {literal(name)})",
            "BEGIN",
            f"    ALTER TABLE {qtable} {with_clause} ADD CONSTRAINT {qname} CHECK{nfr} {definition};",
            "END;"
        ]
        if disabled:
            output.append(f"ALTER TABLE {qtable} NOCHECK CONSTRAINT {qname};")
        elif not untrusted:
            output.append(f"ALTER TABLE {qtable} WITH CHECK CHECK CONSTRAINT {qname};")
        counts["checks"] += 1
        output += ["GO", ""]

    report = [
        "# SQL2 metadata coverage / unresolved dependencies", "",
        "Generated from the read-only SQL2 metadata export and docs/SCHEMA_COVERAGE.csv.",
        "This report does not execute SQL. DEFAULT constraints are represented inline in 02_Generated_From_DB57.sql.", "",
        "## Generated object counts", "", "| Object | Count |", "|---|---:|",
        f"| Allowlisted tables | {counts['tables']} |",
        f"| PK constraints | {counts['primary_keys']} |",
        f"| Unique constraints | {counts['unique_constraints']} |",
        f"| Non-constraint indexes | {counts['indexes']} |",
        f"| Foreign keys emitted | {counts['foreign_keys']} |",
        f"| CHECK constraints emitted | {counts['checks']} |", "",
        "## Dependencies requiring review", ""
    ]
    report += [f"- {item}" for item in manual] if manual else ["- None detected in the exported FK metadata for this allowlist."]
    report += [
        "", "## Explicit limitations", "",
        "- SQL modules (procedures, views, functions, triggers) are not auto-deployed from this report; compare their definitions with reviewed source scripts.",
        "- FKs whose referenced table is outside the allowlist are intentionally omitted, not silently treated as restored.",
        "- This metadata export does not provide permissions, SQL Agent jobs, synonyms, linked-server dependencies, or application data.",
        "- Generated DDL is intended only for disposable PCTP_QC_CI; it must never target DB57 or production.", ""
    ]
    return "\n".join(output), "\n".join(report), counts

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--metadata", required=True, type=Path)
    parser.add_argument("--coverage", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()
    try:
        metadata = args.metadata.read_text(encoding="utf-8-sig", errors="replace")
        sql, report, counts = generate(metadata, load_scope(args.coverage))
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(sql, encoding="utf-8", newline="\n")
        args.report.write_text(report, encoding="utf-8", newline="\n")
        print(f"Wrote {args.out} and {args.report}: " + ", ".join(f"{k}={v}" for k, v in counts.items()))
        return 0
    except (OSError, ValueError, KeyError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

if __name__ == "__main__":
    raise SystemExit(main())
