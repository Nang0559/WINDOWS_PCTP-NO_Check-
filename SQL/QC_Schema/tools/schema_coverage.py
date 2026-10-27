#!/usr/bin/env python3
"""
Static schema coverage: which SQL objects the PCTP code uses, which exist on DB57 (per the
exported docs/SQL.text), which are defined in Git, and which are present in the disposable CI
schema. No database access. Output is evidence for "what SQL to fix / where in code".

  python3 SQL/QC_Schema/tools/schema_coverage.py --out docs/SCHEMA_COVERAGE

Limits (also written into the report): object names built at runtime (customer suffixes,
config-driven table names) cannot be resolved statically; CTE/alias names may appear as false
positives and are listed under 'unresolved'. A name found in a C# string is "referenced", not
"proven executed".
"""
import argparse, csv, json, os, re, subprocess, sys
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
EXPORT = os.path.join(ROOT, 'docs', 'SQL.text')
INVENTORY = os.path.join(ROOT, 'docs', 'PCTP_SQL_Runtime_Inventory_A-K.csv')
CI_BASE_FILES = [
    'SQL/QC_Schema/01_Tables.sql', 'SQL/QC_Schema/05_Split.sql',
    'SQL/Usp_Qrcode_Update_Stock2405.sql', 'SQL/Usp_Qrcode_Update_Stock_SP.sql',
    'SQL/sp_GetCurrentStockStatus.sql',
]
DDL = re.compile(r'\bCREATE\s+(?:OR\s+ALTER\s+)?(TABLE|PROC(?:EDURE)?|VIEW|FUNCTION)\s+'
                 r'(?:\[?dbo\]?\.)?\[?([A-Za-z_][\w]*)\]?', re.I)
KIND = {'TABLE': 'table', 'PROC': 'proc', 'PROCEDURE': 'proc', 'VIEW': 'view', 'FUNCTION': 'function'}

def read(p):
    with open(p, encoding='utf-8-sig', errors='replace') as f:
        return f.read()

def export_objects():
    """name(lower) -> dict(kind, stub). Export lists '-- OBJECT: name (T)'; stub when notice follows."""
    t = read(EXPORT)
    out = {}
    for m in re.finditer(r'-- OBJECT: (\S+) \((\w+)\)\n-+\n(-- \[Notice\][^\n]*)?', t):
        k = {'U': 'table', 'V': 'view', 'P': 'proc', 'FN': 'function', 'TF': 'function', 'IF': 'function'}.get(m.group(2), m.group(2))
        out[m.group(1).lower()] = {'name': m.group(1), 'kind': k, 'stub': bool(m.group(3))}
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


COLDEF = re.compile(r'^\s*\[?([A-Za-z_][\w]*)\]?\s+(?:N?VARCHAR|N?CHAR|INT|BIGINT|SMALLINT|TINYINT|BIT|DATETIME2?|SMALLDATETIME|DATE|TIME|DECIMAL|NUMERIC|FLOAT|REAL|MONEY|UNIQUEIDENTIFIER|VARBINARY|XML)\b', re.I | re.M)

def export_columns():
    """table(lower) -> set(lower column names) from real (non-stub) export tables."""
    t = read(EXPORT).replace('\\n', '\n')
    cols = {}
    for m in re.finditer(r'-- OBJECT: (\S+) \(U\)\n-+\n(.*?)\nGO', t, re.S):
        if m.group(2).lstrip().startswith('-- [Notice]'):
            continue
        cols[m.group(1).lower()] = {c.lower() for c in COLDEF.findall(m.group(2))}
    return cols

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
    for (t, c), files in sorted(code_column_writes().items()):
        if t in exp_cols:
            src, known = 'DB57 export', exp_cols[t]
        elif t in git_cols:
            src, known = 'Git DDL (không có trong file xuất)', git_cols[t]
        else:
            continue
        if c in known: continue
        in_git = c in git_cols.get(t, set())
        rows.append({'table': t, 'column_written_by_code': c, 'compared_with': src,
                     'column_in_git_ddl': 'có' if in_git else 'không',
                     'code_files': '; '.join(sorted(os.path.basename(x) for x in files))[:240],
                     'meaning': ('Code ghi cột này; DB57 (theo file xuất) chưa có; Git có script thêm cột => chạy script trên DB57'
                                 if src == 'DB57 export' and in_git else
                                 'Code ghi cột không có trong schema đối chiếu => sửa SQL (thêm cột) hoặc sửa code'
                                 ) })
    return rows

def status(name, exp, gitd, ci_names):
    e = exp.get(name)
    on57 = 'có' if e and not e['stub'] else ('stub (không tồn tại lúc xuất)' if e else 'không có trong file xuất')
    g = gitd.get(name)
    return on57, (', '.join(g) if g else 'không'), ('có' if name in ci_names else 'không')

def classify(refd, e, g, ci):
    if not refd: return 'info'
    stub = e and e['stub']; on57 = e and not stub
    if on57 and ci: return 'OK-testable'
    if on57 and g and not ci: return 'GAP-ci-schema'      # DB57 + Git DDL, but CI base schema lacks it
    if on57 and not g: return 'GAP-no-git-ddl'            # exists on 57, no DDL in Git
    if (stub or not e) and g: return 'GAP-not-deployed-57'  # Git DDL but DB57 lacks it
    return 'GAP-undefined'                                  # code uses it; neither 57 nor Git defines it

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default='docs/SCHEMA_COVERAGE', help='output prefix (writes <out>.csv and <out>_columns.csv)')
    ap.add_argument('--check', action='store_true', help='exit 1 on a column mismatch not listed in known_column_gaps.txt')
    a = ap.parse_args()
    exp, gitd = export_objects(), git_ddl()
    refs, dyn = code_refs()
    ci_names = set()
    for f in CI_BASE_FILES:
        for m in DDL.finditer(read(os.path.join(ROOT, f))):
            ci_names.add(m.group(2).lower())
    inv = {}
    with open(INVENTORY, encoding='utf-8-sig', newline='') as fh:
        for r in csv.DictReader(fh):
            inv[r['Tên đối tượng'].split('.')[-1].lower()] = r
    names = sorted(set(refs) | set(exp) | set(inv) | set(gitd))
    rows = []
    for n in names:
        e = exp.get(n); g = gitd.get(n); ci = n in ci_names; r = refs.get(n)
        in_inv = n in inv
        if not (r or e or in_inv):
            continue  # defined only in Git and never referenced anywhere else: not part of coverage
        on57, gitf, cis = status(n, exp, gitd, ci_names)
        cls = classify(bool(r), e, g, ci)
        rows.append({
            'object': (e['name'] if e else (inv[n]['Tên đối tượng'] if in_inv else n)),
            'kind': (e['kind'] if e else ''),
            'referenced_by_code': 'có' if r else 'không thấy literal SQL',
            'code_files': '; '.join(sorted(os.path.basename(x) for x in r['files']))[:300] if r else '',
            'code_ops': '/'.join(sorted(r['ops'])) if r else '',
            'on_db57_export': on57, 'git_ddl_files': gitf, 'in_ci_base_schema': cis,
            'in_runtime_inventory': 'có' if in_inv else 'KHÔNG (thiếu trong CSV)',
            'class': cls,
        })
    order = {'GAP-undefined': 0, 'GAP-no-git-ddl': 1, 'GAP-not-deployed-57': 2, 'GAP-ci-schema': 3, 'OK-testable': 4, 'info': 5}
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
    print('COVERAGE', dict(cnt), 'dynamic-ref files:', len(dyn), '| column mismatches:', len(crow))
    if a.check:
        known = {}
        kp = os.path.join(os.path.dirname(__file__), 'known_column_gaps.txt')
        for line in read(kp).splitlines():
            line = line.strip()
            if line and not line.startswith('#'):
                known[line.split('|')[0].strip().lower()] = line
        seen = {f"{r['table']}.{r['column_written_by_code']}" for r in crow}
        new = sorted(seen - set(known)); stale = sorted(set(known) - seen)
        for n in new: print('NEW column mismatch (fix SQL or code):', n)
        for n in stale: print('WARN known gap no longer present, remove from known_column_gaps.txt:', n)
        print('CHECK', 'FAIL' if new else 'PASS', f'({len(seen)} mismatch, {len(known)} known)')
        return 1 if new else 0
    return 0

if __name__ == '__main__':
    sys.exit(main())
