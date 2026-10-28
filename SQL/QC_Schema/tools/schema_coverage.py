#!/usr/bin/env python3
"""
Static schema coverage: code references vs the structured SQL2 DB57 metadata snapshot,
Git DDL, and disposable CI schema. docs/SQL.text is intentionally NOT authoritative because
its DDL was found lossy. Absence from a metadata snapshot is UNVERIFIED, not proof of absence.

  python3 SQL/QC_Schema/tools/schema_coverage.py --out docs/SCHEMA_COVERAGE

Limits (also written into the report): object names built at runtime (customer suffixes,
config-driven table names) cannot be resolved statically; CTE/alias names may appear as false
positives and are listed under 'unresolved'. A name found in a C# string is "referenced", not
"proven executed".
"""
import argparse, csv, json, os, re, subprocess, sys
from collections import defaultdict
from pathlib import Path

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
METADATA = os.path.join(ROOT, 'SQL', 'Result_Export_SQL2_Complete_DDL_Metadata.txt')
COLUMN_INVENTORY = os.path.join(ROOT, 'docs', 'SQL2_ColumnInventory.csv')
INVENTORY = os.path.join(ROOT, 'docs', 'PCTP_SQL_Runtime_Inventory_A-K.csv')

sys.path.insert(0, os.path.dirname(__file__))
from gen_schema_from_inventory import load_columns
from gen_schema_constraints import parse_section
DDL = re.compile(r'\bCREATE\s+(?:OR\s+ALTER\s+)?(TABLE|PROC(?:EDURE)?|VIEW|FUNCTION|TRIGGER)\s+'
                 r'(?:\[?dbo\]?\.)?\[?([A-Za-z_][\w]*)\]?', re.I)
KIND = {'TABLE': 'table', 'PROC': 'proc', 'PROCEDURE': 'proc', 'VIEW': 'view', 'FUNCTION': 'function', 'TRIGGER': 'trigger'}

def read(p):
    with open(p, encoding='utf-8-sig', errors='replace') as f:
        return f.read()

def fixed_width_section_rows(path, required):
    """Read SQL2 fixed-width rows; ignore continuation lines in module definitions."""
    lines = read(path).splitlines()
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
                continue
            row = {name: (line[starts[j]:(starts[j + 1] if j + 1 < len(starts) else len(line))].strip()
                          if starts[j] < len(line) else "")
                   for j, name in enumerate(names)}
            if row.get("SchemaName") and row.get("ObjectType"):
                rows.append(row)
        return rows
    raise ValueError("SQL2 metadata section not found: " + ", ".join(sorted(required)))


MODULE_KIND = {"U": "table", "V": "view", "P": "proc",
               "FN": "function", "TF": "function", "IF": "function", "TR": "trigger"}


def export_objects(metadata_path=None, column_inventory_path=None):
    """dbo tables from SQL2 column inventory + modules from SQL2 metadata."""
    metadata_path = metadata_path or METADATA
    column_inventory_path = column_inventory_path or COLUMN_INVENTORY
    out = {name: {"name": name, "kind": "table", "stub": False}
           for name in load_columns(Path(column_inventory_path))}
    rows = fixed_width_section_rows(
        metadata_path, {"SchemaName", "ObjectName", "ObjectType", "ObjectTypeDescription", "SqlDefinition"})
    for row in rows:
        if row["SchemaName"].casefold() != "dbo":
            continue
        name = row["ObjectName"].strip()
        kind = MODULE_KIND.get(row["ObjectType"].strip().upper())
        if not name or "..." in name or not kind:
            continue
        out[name.casefold()] = {"name": name, "kind": kind, "stub": False}
    return out

def git_ddl():
    """name(lower) -> sorted list of repo-relative .sql files that CREATE it."""
    out = defaultdict(set)
    for dp, dn, fn in os.walk(ROOT):
        dn[:] = [d for d in dn if d not in ('.git', 'bin', 'obj', 'node_modules')]
        for f in fn:
            if f.lower().endswith('.sql'):
                p = os.path.join(dp, f)
                rel = os.path.relpath(p, ROOT).replace(os.sep, '/')
                for m in DDL.finditer(read(p)):
                    out[m.group(2).lower()].add(rel)
    return {k: sorted(v) for k, v in out.items()}

STR = re.compile(r'@\$?"(?:[^"]|"")*"|\$@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\])*"', re.S)
REF = re.compile(r'\b(?:FROM|JOIN|INTO|UPDATE|MERGE\s+INTO|DELETE\s+FROM|TRUNCATE\s+TABLE)\s+'
                 r'(?:\[?dbo\]?\.)?(\[?[A-Za-z_#@{][\w{}\.]*\]?)', re.I)
EXEC = re.compile(r'\bEXEC(?:UTE)?\s+(?:\[?dbo\]?\.)?\[?([A-Za-z_][\w]*)\]?', re.I)
SPCALL = re.compile(r'(?:ExecuteStoredProcedure(?:DataSet)?|ExecuteScalarStoredProcedure)\(\s*\$?@?"([A-Za-z_][\w\.]*)"')
SQLKW = {'and','or','on','columns','objects','tables','s','t','d','a','b','c','x','y','z','tmp','temp','select','set','values','where','dual','inserted','deleted','openjson','string_split','sys','information_schema','as','top','distinct','not','exists','case','when','then'}

def code_refs():
    """name(lower) -> {'files': set, 'ops': set}; dynamic -> set(files)."""
    refs = defaultdict(lambda: {'files': set(), 'ops': set()})
    dyn = defaultdict(set)
    for dp, dn, fn in os.walk(os.path.join(ROOT, 'PCTP')):
        dn[:] = [d for d in dn if d not in ('bin', 'obj')]
        for f in fn:
            if not f.endswith('.cs') or f.endswith('.Designer.cs'):
                continue
            p = os.path.join(dp, f)
            rel = os.path.relpath(p, ROOT).replace(os.sep, '/')
            src = read(p)
            for lit in STR.findall(src):
                body = lit
                if not re.search(r'\b(select|insert|update|delete|exec|merge|from)\b', body, re.I):
                    continue
                ctes = {c.lower() for c in re.findall(r'\bWITH\s+(\w+)\s+AS\b|,\s*(\w+)\s+AS\s*\(', body) for c in c if c}
                for m in REF.finditer(body):
                    raw = m.group(1).strip('[]')
                    if raw.startswith('#') or raw.startswith('@') or raw.lower() in SQLKW:
                        continue
                    if '{' in raw or '}' in raw:
                        dyn[rel].add(raw); continue
                    name = raw.split('.')[-1].strip('[]').lower()
                    if name in SQLKW or name in ctes or len(name) < 3 or not name.isascii():
                        continue
                    op = re.match(r'\w+(?:\s+\w+)?', m.group(0)).group(0).upper().split()[0]
                    refs[name]['files'].add(rel); refs[name]['ops'].add(op)
                for m in EXEC.finditer(body):
                    n = m.group(1).lower()
                    if n not in SQLKW and not n.startswith('sp_executesql'):
                        refs[n]['files'].add(rel); refs[n]['ops'].add('EXEC')
            for m in SPCALL.finditer(src):
                n = m.group(1).split('.')[-1].lower()
                refs[n]['files'].add(rel); refs[n]['ops'].add('EXEC')
    return refs, dyn


def export_columns(inventory_path=None):
    """dbo table columns from structured SQL2 column inventory."""
    tables = load_columns(Path(inventory_path or COLUMN_INVENTORY))
    return {name: {row["ColumnName"].casefold() for row in rows}
            for name, rows in tables.items()}

def foreign_key_dependency_map(metadata_path, seed_tables, available_tables):
    """Transitive dbo FK closure for code-referenced tables, limited to SQL2-known tables."""
    fields = {"ParentSchema", "ParentTable", "ParentColumn", "ReferencedSchema",
              "ReferencedTable", "ReferencedColumn", "ForeignKeyName", "ColumnOrdinal",
              "OnDelete", "OnUpdate", "IsDisabled", "IsNotTrusted"}
    fks = parse_section(read(metadata_path), fields)
    available = {name.casefold() for name in available_tables}
    known = {name.casefold() for name in seed_tables}
    dependencies = defaultdict(set)
    changed = True
    while changed:
        changed = False
        for row in fks:
            parent = row.get("ParentTable", "").casefold()
            target = row.get("ReferencedTable", "").casefold()
            if (row.get("ParentSchema", "").casefold() != "dbo"
                    or row.get("ReferencedSchema", "").casefold() != "dbo"
                    or parent not in known or target not in available):
                continue
            dependencies[target].add(parent)
            if target not in known:
                known.add(target)
                changed = True
    return dependencies

COLDEF = re.compile(
    r'^\s*\[?([A-Za-z_][\w]*)\]?\s+(?:N?VARCHAR|N?CHAR|INT|BIGINT|SMALLINT|TINYINT|BIT|DATETIME2?|SMALLDATETIME|DATE|TIME|DECIMAL|NUMERIC|FLOAT|REAL|MONEY|UNIQUEIDENTIFIER|VARBINARY|XML)\b',
    re.I | re.M
)


def git_columns():
    """table(lower) -> set(columns) from CREATE TABLE bodies and ALTER TABLE ... ADD in repo .sql files."""
    cols = defaultdict(set)
    for dp, dn, fn in os.walk(ROOT):
        dn[:] = [d for d in dn if d not in ('.git', 'bin', 'obj')]
        for f in fn:
            if not f.lower().endswith('.sql'): continue
            txt = read(os.path.join(dp, f))
            for m in re.finditer(r'CREATE\s+TABLE\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?\s*\((.*?)\n\s*\)\s*;?', txt, re.S | re.I):
                for c in COLDEF.findall(m.group(2)): cols[m.group(1).lower()].add(c.lower())
            for m in re.finditer(r'ALTER\s+TABLE\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?\s+ADD\s+(?:COLUMN\s+)?\[?(\w+)\]?\s+\w+', txt, re.I):
                cols[m.group(1).lower()].add(m.group(2).lower())
    return cols

INS = re.compile(r'INSERT\s+INTO\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?\s*\(([^)]*)\)', re.I)
UPD = re.compile(r'UPDATE\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?(?:\s+(?:AS\s+)?\w+)?\s+SET\s+(.*?)(?=\bWHERE\b|\bFROM\b|;|$)', re.I | re.S)
ASG = re.compile(r'(?:^|,)\s*(?:\w+\.)?\[?([A-Za-z_]\w*)\]?\s*=', re.S)

def code_column_writes():
    """(table, column) -> set(files) for INSERT column lists and UPDATE SET assignments in C# SQL literals."""
    out = defaultdict(set)
    for dp, dn, fn in os.walk(os.path.join(ROOT, 'PCTP')):
        dn[:] = [d for d in dn if d not in ('bin', 'obj')]
        for f in fn:
            if not f.endswith('.cs') or f.endswith('.Designer.cs'): continue
            p = os.path.join(dp, f); rel = os.path.relpath(p, ROOT).replace(os.sep, '/')
            for lit in STR.findall(read(p)):
                if '{' in lit: lit = re.sub(r'\{[^{}]*\}', '', lit)
                for m in INS.finditer(lit):
                    for c in m.group(2).split(','):
                        c = c.strip().strip('[]').split('.')[-1].strip('[]')
                        if re.fullmatch(r'[A-Za-z_]\w*', c): out[(m.group(1).lower(), c.lower())].add(rel)
                for m in UPD.finditer(lit):
                    for c in ASG.findall(m.group(2)):
                        out[(m.group(1).lower(), c.lower())].add(rel)
    return out

def column_report(exp_cols, git_cols):
    rows = []
    for (table, column), files in sorted(code_column_writes().items()):
        if table not in exp_cols:
            rows.append({
                'table': table, 'column_written_by_code': column,
                'compared_with': 'Không xác minh: bảng ngoài SQL2 dbo column inventory',
                'column_in_git_ddl': 'có' if column in git_cols.get(table, set()) else 'không',
                'code_files': '; '.join(sorted(os.path.basename(x) for x in files))[:240],
                'meaning': 'Chưa có bằng chứng schema DB57 cho bảng này; cần xác minh schema/owner hoặc bổ sung đúng metadata export.'
            })
            continue
        if column in exp_cols[table]:
            continue
        in_git = column in git_cols.get(table, set())
        rows.append({
            'table': table, 'column_written_by_code': column,
            'compared_with': 'SQL2 DB57 column inventory',
            'column_in_git_ddl': 'có' if in_git else 'không',
            'code_files': '; '.join(sorted(os.path.basename(x) for x in files))[:240],
            'meaning': ('Code ghi cột không có trong SQL2 snapshot; Git có script thêm cột, cần đối chiếu trạng thái deploy'
                        if in_git else
                        'Code ghi cột không có trong SQL2 snapshot và không có Git DDL; cần xác minh DB57 trước khi sửa code/schema')
        })
    return rows

def status(name, exp, gitd, ci_names):
    e = exp.get(name)
    on57 = 'có' if e else 'chưa xác minh trong SQL2 metadata'
    g = gitd.get(name)
    return on57, (', '.join(g) if g else 'không'), ('có' if name in ci_names else 'không')


def classify(refd, e, g, ci):
    if not refd: return 'info'
    # Absence from this snapshot is not proof of non-existence on DB57.
    if e and ci: return 'OK-testable'
    if e and g and not ci: return 'GAP-ci-schema'
    if e and not g: return 'GAP-no-git-ddl'
    return 'GAP-db57-unverified'

def runtime_table_candidates(inventory_rows):
    """PCTP SQL Server table candidates from the curated runtime inventory.

    The SQL2 snapshot proves whether a candidate is a dbo table on DB57.
    IFS/Oracle, external sources, C# types and non-table entries are excluded.
    """
    candidates = set()
    for name, row in inventory_rows.items():
        kind = (row.get("Loại đối tượng") or "").casefold()
        runtime = (row.get("Runtime hay test-only") or "").casefold()
        database = (row.get("Database/schema") or "").casefold()
        status = (row.get("Trạng thái xác minh") or "").casefold()
        if "bảng" not in kind:
            continue
        if "ifs" in kind or "ifs" in database or "oracle" in database or "external" in runtime:
            continue
        if "test-only" in runtime or "test only" in runtime:
            continue
        if runtime.startswith("chưa xác định") or runtime.startswith("cần trace"):
            continue
        if "không mặc định db57" in status or "không phải bảng sql" in status:
            continue
        # Do not resolve host-name templates as literal table names.
        if "{" in name or "}" in name:
            continue
        candidates.add(name.casefold())
    return candidates


def fk_dependency_classification(dependency_parents, referenced_by_code, export_object, on_db57, in_ci):
    """Distinguish a verified indirect FK dependency from a genuine schema gap."""
    if not dependency_parents or referenced_by_code:
        return None
    if export_object and export_object.get("kind") == "table" and on_db57 == 'có' and in_ci:
        return 'OK-fk-dependency'
    return 'GAP-fk-dependency'


def external_inventory_names(inventory_rows):
    """Names explicitly belonging to IFS/Oracle/external sources, not DB57."""
    excluded = set()
    for name, row in inventory_rows.items():
        kind = (row.get("Loại đối tượng") or "").casefold()
        database = (row.get("Database/schema") or "").casefold()
        runtime = (row.get("Runtime hay test-only") or "").casefold()
        status = (row.get("Trạng thái xác minh") or "").casefold()
        if "ifs" in kind or "ifs" in database or "oracle" in database or "external" in runtime:
            excluded.add(name.casefold())
        if "không mặc định db57" in status:
            excluded.add(name.casefold())
    return excluded


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--inventory', type=str, default=INVENTORY, help='runtime inventory CSV (default: PCTP_SQL_Runtime_Inventory_A-K.csv)')
    ap.add_argument('--out', default='docs/SCHEMA_COVERAGE', help='output prefix (writes <out>.csv and <out>_columns.csv)')
    ap.add_argument('--check', action='store_true', help='exit 1 on a column mismatch not listed in known_column_gaps.txt')
    a = ap.parse_args()
    exp, gitd = export_objects(), git_ddl()
    refs, dyn = code_refs()
    inv = {}
    with open(a.inventory, encoding='utf-8-sig', newline='') as fh:
        for row in csv.DictReader(fh):
            inv[row['Tên đối tượng'].split('.')[-1].casefold()] = row

    # The PCTP runtime inventory, plus literal SQL references, is the seed scope.
    # SQL2 metadata is used only to resolve those names and their FK dependencies;
    # unrelated B7R2 dbo tables are never pulled into the PCTP CI baseline.
    available_tables = {name for name, value in exp.items() if value["kind"] == "table"}
    inventory_candidates = runtime_table_candidates(inv)
    external_names = external_inventory_names(inv)
    # The CSV is the scope authority. Literal code references are a completeness
    # audit; they do not silently expand the generated schema.
    seed_tables = inventory_candidates & available_tables
    fk_dependencies = foreign_key_dependency_map(METADATA, seed_tables, available_tables)
    # This is the exact set that the current CI run will generate from SQL2,
    # not a scan of stale checked-in 02/03 artifacts from an older wider scope.
    ci_names = set(seed_tables) | set(fk_dependencies)
    names = sorted(set(refs) | set(inv) | set(fk_dependencies))
    rows = []
    for n in names:
        e = exp.get(n); g = gitd.get(n); ci = n in ci_names; r = refs.get(n)
        in_inv = n in inv
        dependency_parents = fk_dependencies.get(n, set())
        if not (r or in_inv or dependency_parents):
            continue  # SQL2 catalog is lookup evidence, not a request to clone all of B7R2
        on57, gitf, cis = status(n, exp, gitd, ci_names)
        in_runtime_scope = n in inventory_candidates
        dependency_class = fk_dependency_classification(dependency_parents, r, e, on57, ci)
        if dependency_class:
            cls = dependency_class
        elif r and not in_runtime_scope and n not in external_names and (not e or e['kind'] == 'table'):
            cls = 'GAP-not-in-runtime-inventory'
        else:
            cls = classify(bool(in_runtime_scope or dependency_parents), e, g, ci)
        rows.append({
            'object': (e['name'] if e else (inv[n]['Tên đối tượng'] if in_inv else n)),
            'kind': (e['kind'] if e else ''),
            'referenced_by_code': 'có' if r else 'không thấy literal SQL',
            'dependency_of_code': '; '.join(sorted(dependency_parents)),
            'code_files': '; '.join(sorted(os.path.basename(x) for x in r['files']))[:300] if r else '',
            'code_ops': '/'.join(sorted(r['ops'])) if r else ('FK-DEPENDENCY' if dependency_parents else ''),
            'on_db57_export': on57, 'git_ddl_files': gitf, 'in_ci_base_schema': cis,
            'in_runtime_inventory': 'có' if in_runtime_scope else ('cần xác minh' if in_inv else 'không'),
            'class': cls,
        })
    order = {'GAP-db57-unverified': 0, 'GAP-not-in-runtime-inventory': 1, 'GAP-fk-dependency': 2, 'GAP-no-git-ddl': 3, 'GAP-ci-schema': 4, 'OK-fk-dependency': 5, 'OK-testable': 6, 'info': 7}
    rows.sort(key=lambda x: (order[x['class']], x['object'].lower()))
    out = os.path.join(ROOT, a.out) if not os.path.isabs(a.out) else a.out
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out + '.csv', 'w', encoding='utf-8-sig', newline='') as fh:
        w = csv.DictWriter(fh, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
    cnt = defaultdict(int)
    for r in rows: cnt[r['class']] += 1
    crow = column_report(export_columns(), git_columns())
    with open(out + '_columns.csv', 'w', encoding='utf-8-sig', newline='') as fh:
        w = csv.DictWriter(fh, fieldnames=['table','column_written_by_code','compared_with','column_in_git_ddl','code_files','meaning'])
        w.writeheader(); w.writerows(crow)
    print('COVERAGE (SQL2 structured snapshot; absence=unverified)', dict(cnt), 'dynamic-ref files:', len(dyn), '| column mismatches/unverified:', len(crow))
    if a.check:
        known = {}
        kp = os.path.join(os.path.dirname(__file__), 'known_column_gaps.txt')
        for line in read(kp).splitlines():
            line = line.strip()
            if line and not line.startswith('#'):
                known[line.split('|')[0].strip().lower()] = line
        seen = {f"{r['table']}.{r['column_written_by_code']}" for r in crow}
        verified = {f"{r['table']}.{r['column_written_by_code']}" for r in crow
                    if r['compared_with'] == 'SQL2 DB57 column inventory'}
        unverified = [r for r in crow if r['compared_with'].startswith('Không xác minh:')]
        new = sorted(verified - set(known)); stale = sorted(set(known) - verified)
        for n in new: print('NEW SQL2 column mismatch (review SQL/code):', n)
        for n in stale: print('WARN known SQL2 gap no longer present:', n)
        scope_gaps = [r for r in rows if r['class'] in {'GAP-db57-unverified', 'GAP-not-in-runtime-inventory'}
                      and (r['referenced_by_code'] == 'có' or r['in_runtime_inventory'] == 'có')]
        for row in scope_gaps:
            print('SCHEMA SCOPE GAP:', row['object'], row['class'])
        print('UNVERIFIED table writes (not a mismatch verdict):', len(unverified))
        failed = bool(new or scope_gaps)
        print('CHECK', 'FAIL' if failed else 'PASS', f'({len(verified)} verified mismatches, {len(known)} known, {len(unverified)} unverified, {len(scope_gaps)} scope gaps)')
        return 1 if failed else 0
    return 0

if __name__ == '__main__':
    sys.exit(main())
