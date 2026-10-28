#!/usr/bin/env python3
"""Generate reviewable SQL Server table DDL from SQL2 fixed-width or CSV column metadata.

The supplied SQL2 export has column metadata but is not a complete schema
source: keys/indexes, foreign keys, check constraints, triggers and permissions
are not reliably represented as structured records. This tool therefore emits
only columns, computed expressions, defaults and IDENTITY. It never invents
keys or silently maps alias/user-defined types.

Usage:
  python SQL/QC_Schema/tools/gen_schema_from_inventory.py \
    --inventory path/to/SQL2.txt --coverage docs/SCHEMA_COVERAGE.csv \
    --out SQL/QC_Schema/02_Generated_From_DB57.sql
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from collections import defaultdict
from pathlib import Path


def fixed_width_rows(path: Path, header_prefix: str):
    lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    header_i = next((i for i, line in enumerate(lines) if line.startswith(header_prefix)), None)
    if header_i is None:
        raise ValueError(f"SQL2 section header not found: {header_prefix}")
    header = lines[header_i]
    starts = [m.start() for m in re.finditer(r"\S+", header)]
    names = re.findall(r"\S+", header)
    separator_i = next((i for i in range(header_i + 1, min(header_i + 4, len(lines)))
                        if lines[i].startswith("---")), None)
    if separator_i is None:
        raise ValueError(f"SQL2 section separator not found after {header_prefix}")
    for line in lines[separator_i + 1:]:
        if not line.strip():
            break
        if line.startswith("--") or line.startswith("===="):
            break
        yield {names[j]: line[start:(starts[j + 1] if j + 1 < len(starts) else len(line))].strip()
               if start < len(line) else ""
               for j, start in enumerate(starts)}


def csv_rows(path: Path):
    """Read delimited inventory exports, preserving quoted SQL expressions."""
    with path.open(encoding="utf-8-sig", newline="") as stream:
        # SQL Server exports may be saved by regional Excel settings using
        # semicolons or tabs instead of commas. Detect only common delimiters;
        # never split lines manually, because SQL expressions may contain commas.
        sample = stream.read(64 * 1024)
        stream.seek(0)
        try:
            dialect = csv.Sniffer().sniff(sample, delimiters=",;\t|")
        except csv.Error:
            dialect = csv.excel
        reader = csv.DictReader(stream, dialect=dialect)
        if not reader.fieldnames:
            raise ValueError("CSV inventory has no header")
        # Trim accidental whitespace around exported header names and values.
        reader.fieldnames = [name.strip() if name else "" for name in reader.fieldnames]
        for raw in reader:
            yield {str(k).strip(): (v.strip() if isinstance(v, str) else "")
                   for k, v in raw.items() if k is not None}


def load_columns(path: Path, table_filter: set[str] | None = None):
    required = {"TableName", "type_desc", "column_id", "ColumnName", "DataType", "MaxChars",
                "precision", "scale", "is_nullable", "is_identity", "seed_value",
                "increment_value", "is_computed", "ComputedDefinition", "DefaultDefinition"}
    if path.suffix.casefold() == ".csv":
        rows = list(csv_rows(path))
        csv_required = required | {"SchemaName"}
        # Some SQL Server exports are saved with a .csv extension despite being
        # aligned fixed-width text. If the delimited header is not valid, retry
        # the documented fixed-width parser instead of misreporting missing fields.
        if not rows or not csv_required.issubset(rows[0]):
            try:
                fixed_rows = list(fixed_width_rows(path, "SchemaName"))
            except ValueError:
                fixed_rows = []
            if fixed_rows and csv_required.issubset(fixed_rows[0]):
                rows = fixed_rows
            else:
                found = sorted(rows[0]) if rows else []
                raise ValueError("CSV/fixed-width column inventory header is incomplete; missing: "
                                 + ", ".join(sorted(csv_required - set(found))))
        # This generator emits dbo-qualified objects only. Fail rather than silently
        # generating dbo tables from metadata exported from another schema.
        rows = [row for row in rows if row.get("SchemaName", "").casefold() == "dbo"]
        # A valid inventory containing only non-dbo objects should flow to render(),
        # where the allow-list check reports the missing table names.
        if not rows:
            return defaultdict(list)
    else:
        rows = list(fixed_width_rows(path, "TableName"))
    if not rows:
        raise ValueError("SQL2 column metadata contains no rows")
    required = {"TableName", "type_desc", "column_id", "ColumnName", "DataType", "MaxChars",
                "precision", "scale", "is_nullable", "is_identity", "seed_value",
                "increment_value", "is_computed", "ComputedDefinition", "DefaultDefinition"}
    if not required.issubset(rows[0]):
        raise ValueError("SQL2 column metadata header is incomplete: " + ", ".join(sorted(required)))
    tables = defaultdict(list)
    allowed_names = {name.casefold() for name in table_filter} if table_filter is not None else None
    for row in rows:
        if row.get("type_desc") != "USER_TABLE":
            continue
        if (row.get("TableName") and row.get("ColumnName") and row.get("column_id", "").isdigit()
                and (allowed_names is None or row["TableName"].casefold() in allowed_names)):
            tables[row["TableName"].casefold()].append(row)
    for columns in tables.values():
        columns.sort(key=lambda row: int(row["column_id"]))
    return tables


def load_allowlist(path: Path):
    allowed = {}
    with path.open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            generated_table_ddl = "SQL/QC_Schema/02_Generated_From_DB57.sql" in row.get("git_ddl_files", "")
            row_class = row.get("class", "").strip()
            scope_class = (row.get("in_runtime_inventory", "").strip().casefold() in {"có", "co", "yes", "true"} or
                row_class in {"GAP-no-git-ddl", "GAP-fk-dependency"} or (
                row_class in {"GAP-ci-schema", "OK-testable", "OK-fk-dependency"} and (
                    row.get("referenced_by_code", "").strip().casefold() in {"có", "co", "yes", "true"}
                    or bool(row.get("dependency_of_code", "").strip())
                    or generated_table_ddl
                )
            ))
            if (scope_class
                    and row.get("kind", "").strip().lower() == "table"
                    and row.get("on_db57_export", "").strip().lower() in {"có", "co", "yes", "true"}):
                name = row["object"].strip()
                allowed[name.casefold()] = name
    if not allowed:
        raise ValueError("coverage allow-list is empty; expected GAP-no-git-ddl table rows")
    return allowed


def sql_type(row, type_map):
    original = row["DataType"].strip()
    t = original.lower()
    maxchars = int(row["MaxChars"]) if row.get("MaxChars", "").lstrip("-").isdigit() else None
    precision = int(row["precision"]) if row.get("precision", "").isdigit() else 0
    scale = int(row["scale"]) if row.get("scale", "").isdigit() else 0
    if t in type_map:
        mapped = type_map[t].strip()
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*(?:\((?:MAX|\d+(?:\s*,\s*\d+)?)\))?", mapped, re.I):
            raise ValueError(f"invalid explicit type mapping for {original}: {mapped!r}")
        return mapped
    if t in {"varchar", "char", "varbinary", "binary"}:
        if maxchars is None:
            raise ValueError(f"missing byte length for {row['TableName']}.{row['ColumnName']}")
        return f"{t}({'MAX' if maxchars == -1 else maxchars})"
    if t in {"nvarchar", "nchar"}:
        if maxchars is None:
            raise ValueError(f"missing byte length for {row['TableName']}.{row['ColumnName']}")
        # SQL2 inventory MaxChars is sourced from SQL Server max_length (bytes).
        # nvarchar/nchar declarations use character units, so divide finite lengths by 2.
        return f"{t}({'MAX' if maxchars == -1 else maxchars // 2})"
    if t in {"decimal", "numeric"}:
        if not (1 <= precision <= 38 and 0 <= scale <= precision):
            raise ValueError(f"invalid precision/scale for {row['TableName']}.{row['ColumnName']}: {precision},{scale}")
        return f"{t}({precision},{scale})"
    builtins = {"bigint", "int", "smallint", "tinyint", "bit", "date", "datetime", "datetime2",
                "smalldatetime", "time", "datetimeoffset", "float", "real", "money", "smallmoney",
                "uniqueidentifier", "xml", "sql_variant", "text", "ntext", "image", "timestamp",
                "rowversion", "sysname"}
    if t not in builtins:
        raise ValueError(f"unsupported/alias SQL type '{original}' for {row['TableName']}.{row['ColumnName']}; add a verified --type-map entry")
    if t in {"datetime2", "time", "datetimeoffset"} and scale:
        return f"{t}({scale})"
    return t


def sql_literal(value):
    return "N'" + str(value).replace("'", "''") + "'"


def safe_expression(value, table, column, kind):
    expr = (value or "").strip()
    if not expr or expr.upper() == "NULL":
        return ""
    if "\n" in expr or "\r" in expr or ";" in expr:
        raise ValueError(f"unsafe/multiline {kind} for {table}.{column}; review manually")
    return expr


def render(inventory: Path, coverage: Path, type_map_path: Path | None = None):
    allowlist = load_allowlist(coverage)
    # Parse/store only PCTP runtime tables + verified FK dependencies, not every
    # unrelated dbo table present in the shared B7R2_FCC metadata export.
    columns_by_table = load_columns(inventory, set(allowlist))
    missing = sorted(set(allowlist) - set(columns_by_table))
    if missing:
        raise ValueError("allow-listed tables absent from SQL2 column metadata: " + ", ".join(missing))
    type_map = {}
    if type_map_path:
        type_map = {str(k).casefold(): str(v) for k, v in json.loads(type_map_path.read_text(encoding="utf-8-sig")).items()}
    lines = [
        "/*",
        " AUTO-GENERATED from SQL2 DB57 column metadata for the reviewed table allowlist.",
        " PK/unique indexes, FKs and CHECK constraints are emitted separately by 03_Generated_From_DB57_Constraints.sql.",
        " Triggers, permissions and procedure dependencies require separate review. Alias types require a verified map.",
        " Apply only to disposable PCTP_QC_CI. Never apply to DB57 or production.",
        "*/",
        "SET ANSI_NULLS ON;", "SET QUOTED_IDENTIFIER ON;", "GO", ""
    ]
    for key, name in sorted(allowlist.items(), key=lambda item: item[1].casefold()):
        columns = columns_by_table[key]
        definitions = []
        for column in columns:
            col = column["ColumnName"].replace("]", "]]")
            typ = sql_type(column, type_map)
            if column.get("is_computed", "0") == "1":
                expression = safe_expression(column.get("ComputedDefinition"), name, col, "computed expression")
                if not expression:
                    raise ValueError(f"computed column missing expression: {name}.{col}")
                definitions.append(f"        [{col}] AS {expression}")
                continue
            default = safe_expression(column.get("DefaultDefinition"), name, col, "default")
            identity = ""
            if column.get("is_identity", "0").strip().casefold() in {"1", "true", "yes"}:
                seed, increment = column.get("seed_value", "NULL"), column.get("increment_value", "NULL")
                if seed in ("", "NULL") or increment in ("", "NULL"):
                    raise ValueError(f"IDENTITY seed/increment missing: {name}.{col}")
                identity = f" IDENTITY({seed},{increment})"
            nullability = " NULL" if column.get("is_nullable", "1").strip().casefold() in {"1", "true", "yes"} else " NOT NULL"
            default_sql = f" DEFAULT {default}" if default else ""
            definitions.append(f"        [{col}] {typ}{identity}{nullability}{default_sql}")
        if not definitions:
            raise ValueError(f"table {name} has no columns")
        lines += [f"IF OBJECT_ID(N'dbo.{name}', N'U') IS NULL", "BEGIN",
                  f"    CREATE TABLE dbo.[{name}] (", ",\n".join(definitions), "    );", "END;", "GO", ""]

        # Validate pre-existing fixture tables too. CREATE IF NOT EXISTS alone can
        # hide a stale 01_Tables definition, so compare every SQL2 column contract.
        expected = []
        for ordinal, column in enumerate(columns, 1):
            col_name = column["ColumnName"]
            raw_type = column["DataType"].strip().casefold()
            mapped_type = type_map.get(raw_type, column["DataType"]).strip()
            base_type = re.match(r"[A-Za-z][A-Za-z0-9_]*", mapped_type).group(0).casefold()
            if base_type == "sysname":
                base_type = "nvarchar"
            elif base_type == "rowversion":
                base_type = "timestamp"
            max_length = int(column["MaxChars"]) if column.get("MaxChars", "").lstrip("-").isdigit() else -999999
            precision = int(column["precision"]) if column.get("precision", "").isdigit() else 0
            scale = int(column["scale"]) if column.get("scale", "").isdigit() else 0
            nullable = 1 if column.get("is_nullable", "1").strip().casefold() in {"1", "true", "yes"} else 0
            identity_flag = 1 if column.get("is_identity", "0").strip().casefold() in {"1", "true", "yes"} else 0
            computed_flag = 1 if column.get("is_computed", "0").strip().casefold() in {"1", "true", "yes"} else 0
            computed = safe_expression(column.get("ComputedDefinition"), name, col_name, "computed expression")
            default = safe_expression(column.get("DefaultDefinition"), name, col_name, "default")
            seed = column.get("seed_value", "").strip() if identity_flag else ""
            increment = column.get("increment_value", "").strip() if identity_flag else ""
            for label, value in (("identity seed", seed), ("identity increment", increment)):
                if value and not re.fullmatch(r"[-+]?(?:\d+(?:\.\d*)?|\.\d+)", value):
                    raise ValueError(f"invalid {label}: {name}.{col_name}={value!r}")
            expected.append(
                f"({ordinal}, {sql_literal(col_name)}, {sql_literal(base_type)}, {max_length}, "
                f"{precision}, {scale}, {nullable}, {identity_flag}, {computed_flag}, "
                f"{sql_literal(computed)}, {sql_literal(default)}, {sql_literal(seed)}, {sql_literal(increment)})")
        expected_values = ", ".join(expected)
        actual_columns = (
            f"SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, "
            f"CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), "
            f"ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), "
            f"ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') "
            f"FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id "
            f"LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id "
            f"LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id "
            f"WHERE c.object_id=OBJECT_ID({sql_literal('dbo.' + name)})")
        expected_columns = (
            f"SELECT v.column_id, v.column_name, v.type_name, v.max_length, v.precision, v.scale, "
            f"v.is_nullable, v.is_identity, v.is_computed, v.computed_definition, v.default_definition, "
            f"v.seed_value, v.increment_value FROM (VALUES {expected_values}) "
            f"v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,"
            f"computed_definition,default_definition,seed_value,increment_value)")
        lines += [
            f"IF OBJECT_ID(N'dbo.{name}', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.{name}', 1;",
            f"IF EXISTS ({actual_columns} EXCEPT {expected_columns}) OR EXISTS ({expected_columns} EXCEPT {actual_columns})",
            f"    THROW 52202, N'SQL2 table/column definition mismatch: dbo.{name}', 1;",
            "GO", ""
        ]
    lines += ["/* PK/index/FK/CHECK constraints are emitted from verified SQL2 metadata in 03_Generated_From_DB57_Constraints.sql. */", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--coverage", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--type-map", type=Path, help="JSON map of verified alias type name to SQL base type")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    try:
        content = render(args.inventory, args.coverage, args.type_map)
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    if args.check:
        if not args.out.exists() or args.out.read_text(encoding="utf-8") != content:
            print(f"ERROR: generated DDL is stale: {args.out}", file=sys.stderr)
            return 1
        print(f"PASS: generated DDL is current ({args.out})")
    else:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(content, encoding="utf-8", newline="\n")
        print(f"Wrote {args.out} ({len(content.splitlines())} lines)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
