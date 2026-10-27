#!/usr/bin/env python3
"""
Static simulation of how `sqlcmd -b -i <file>` loads the SQL files of the PCTP QC schema.

Standard library only. It never connects to a database.

  deploy files   python3 sql_batch_check.py --files A.sql B.sql ...
  full export    python3 sql_batch_check.py --export docs/SQL.text --reference SQL/QC_Schema/01_Tables.sql
  loadable copy  python3 sql_batch_check.py --export docs/SQL.text --emit-loadable out.sql [--halve-unicode-lengths]

Exit code: 0 = no ERROR in the files passed with --files, 1 = at least one ERROR, 2 = usage / unreadable input.
Defects found in --export are reported but are informational (the export is documentation, not a deploy file)
unless --strict-export is given.
"""
import argparse
import collections
import re
import sys

CODE, LINE_COMMENT, BLOCK_COMMENT, STRING, IDENT = 0, 1, 2, 3, 4
GO_LINE = re.compile(r'^[ \t]*GO(?:[ \t]+\d+)?[ \t]*(?:--.*)?$', re.IGNORECASE)
NOISE_LINES = [
    (re.compile(r'^\s*\(\d+ rows? affected\)\s*$', re.I), "SSMS result line '(N rows affected)'"),
    (re.compile(r'^\s*Msg \d+, Level \d+', re.I), "SSMS/sqlcmd error message line"),
    (re.compile(r'^\s*Command\(s\) completed successfully', re.I), "SSMS completion line"),
]
OBJECT_RE = re.compile(
    r'\bCREATE\s+(?:OR\s+ALTER\s+)?(TABLE|PROC(?:EDURE)?|FUNCTION|VIEW|TRIGGER)\s+'
    r'(?:\[?(\w+)\]?\s*\.\s*)?\[?(#{0,2}\w+)\]?', re.I)
MODULE_RE = re.compile(r'\bCREATE\s+(?:OR\s+ALTER\s+)?(PROC(?:EDURE)?|FUNCTION|VIEW|TRIGGER)\b', re.I)
REF_RE = re.compile(
    r'\b(?:FROM|JOIN|INTO|UPDATE|MERGE)\s+(?:\[?(\w+)\]?\s*\.\s*)?\[?([A-Za-z_]\w*)\]?\s*(\()?', re.I)
IGNORED_REFS = {
    'select', 'set', 'inserted', 'deleted', 'sys', 'information_schema', 'values', 'openjson', 'string_split',
    'cte', 'a', 'b', 'x', 'dual', 'only', 'exists',
}
PRODUCTION_MARKERS = re.compile(r'192\.168\.200\.14|\\BRAVO\b', re.I)
SQLCMD_VAR = re.compile(r'\$\([A-Za-z_]\w*\)')
SQLCMD_DIRECTIVE = re.compile(r'^\s*:(r|setvar|connect|on error|out|exit)\b', re.I | re.M)


class Finding:
    def __init__(self, level, code, path, line, message):
        self.level, self.code, self.path, self.line, self.message = level, code, path, line, message

    def __str__(self):
        return f"{self.level} {self.code} {self.path}:{self.line}: {self.message}"


def lex(text):
    """Return (masked, kinds, unterminated). masked keeps newlines; non-code characters become spaces."""
    n = len(text)
    kinds = bytearray(n)
    masked = list(text)
    i = 0
    depth = 0
    state = CODE
    unterminated = None
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ''
        if state == CODE:
            if c == '-' and nxt == '-':
                state = LINE_COMMENT
                kinds[i] = kinds[i + 1] = LINE_COMMENT
                masked[i] = masked[i + 1] = ' '
                i += 2
                continue
            if c == '/' and nxt == '*':
                state, depth = BLOCK_COMMENT, 1
                kinds[i] = kinds[i + 1] = BLOCK_COMMENT
                masked[i] = masked[i + 1] = ' '
                i += 2
                continue
            if c == "'":
                state = STRING
                kinds[i] = STRING          # opening quote stays visible in `masked`; the content is blanked
            elif c == '[':
                state = IDENT
                kinds[i] = IDENT          # keep the text of [identifiers] visible for name extraction
            i += 1
            continue
        if state == LINE_COMMENT:
            if c == '\n':
                state = CODE
                kinds[i] = CODE
            else:
                kinds[i] = LINE_COMMENT
                masked[i] = ' '
            i += 1
            continue
        if state == BLOCK_COMMENT:
            kinds[i] = BLOCK_COMMENT
            if c == '/' and nxt == '*':
                depth += 1
                kinds[i + 1] = BLOCK_COMMENT
                masked[i] = masked[i + 1] = ' '
                i += 2
                continue
            if c == '*' and nxt == '/':
                depth -= 1
                kinds[i + 1] = BLOCK_COMMENT
                masked[i] = masked[i + 1] = ' '
                i += 2
                if depth == 0:
                    state = CODE
                continue
            if c != '\n':
                masked[i] = ' '
            i += 1
            continue
        if state == STRING:
            kinds[i] = STRING
            if c == "'" and nxt == "'":
                kinds[i + 1] = STRING
                masked[i] = masked[i + 1] = ' '
                i += 2
                continue
            if c == "'":
                state = CODE               # closing quote stays visible
            elif c != '\n':
                masked[i] = ' '
            i += 1
            continue
        if state == IDENT:
            kinds[i] = IDENT
            if c == ']' and nxt == ']':
                kinds[i + 1] = IDENT
                i += 2
                continue
            if c == ']':
                state = CODE
            i += 1
            continue
    if state == BLOCK_COMMENT:
        unterminated = 'block comment'
    elif state == STRING:
        unterminated = 'string literal'
    elif state == IDENT:
        unterminated = 'bracketed identifier'
    return ''.join(masked), kinds, unterminated


def split_batches(text):
    """sqlcmd-like split. Returns (batches, ambiguous_go_lines). A batch is (start_line, start_off, end_off, repeat)."""
    masked, kinds, _ = lex(text)
    batches, ambiguous = [], []
    start_off, start_line, offset = 0, 1, 0
    for lineno, raw in enumerate(text.split('\n'), 1):
        line = raw.rstrip('\r')
        line_end = offset + len(raw)
        if GO_LINE.match(line):
            if offset >= len(kinds) or kinds[offset] == CODE or (kinds[offset] == IDENT and False):
                repeat = re.search(r'\d+', line)
                batches.append((start_line, start_off, offset, int(repeat.group()) if repeat else 1))
                start_off, start_line = line_end + 1, lineno + 1
            else:
                ambiguous.append(lineno)
        offset = line_end + 1
    batches.append((start_line, start_off, len(text), 1))
    return batches, ambiguous, masked


def analyse_file(path, text, findings, created, referenced):
    batches, ambiguous, masked = split_batches(text)
    _, kinds_all, unterminated = lex(text)
    for lineno in ambiguous:
        findings.append(Finding('WARN', 'W001', path, lineno,
                                "a 'GO' line sits inside a comment or string; sqlcmd and SSMS may treat it differently"))
    if unterminated:
        findings.append(Finding('ERROR', 'E005', path, 1, f"unterminated {unterminated}"))
    for m in SQLCMD_VAR.finditer(text):
        findings.append(Finding('ERROR', 'E008', path, text.count('\n', 0, m.start()) + 1,
                                "sqlcmd variable " + m.group() + " would be substituted before execution"))
    for m in SQLCMD_DIRECTIVE.finditer(text):
        findings.append(Finding('ERROR', 'E008', path, text.count('\n', 0, m.start()) + 1,
                                "sqlcmd directive is not allowed in deploy files"))
    no_comments = ''.join(' ' if kinds_all[i] in (LINE_COMMENT, BLOCK_COMMENT) and ch != '\n' else ch
                          for i, ch in enumerate(text))
    for m in PRODUCTION_MARKERS.finditer(no_comments):
        findings.append(Finding('ERROR', 'E009', path, text.count('\n', 0, m.start()) + 1,
                                "production server marker '" + m.group() + "' in a CI deploy file"))
    lines = text.split('\n')
    offsets = []
    off = 0
    for raw in lines:
        offsets.append(off)
        off += len(raw) + 1
    first_nonblank = next((i for i, l in enumerate(lines) if l.strip()), None)
    if first_nonblank is not None and lines[first_nonblank].strip().lower() == 'text':
        findings.append(Finding('ERROR', 'E002', path, first_nonblank + 1, "stray SSMS header line 'Text'"))
    for idx, raw in enumerate(lines):
        if offsets[idx] < len(text) and kinds_all[offsets[idx]] == CODE:
            for rx, why in NOISE_LINES:
                if rx.match(raw):
                    findings.append(Finding('ERROR', 'E002', path, idx + 1, why))
    for start_line, a, b, _ in batches:
        code = masked[a:b]
        if not code.strip():
            continue
        if re.search(r'\\[nrt]', code):
            findings.append(Finding('ERROR', 'E001', path, start_line,
                                    "literal backslash escape (\\n, \\r or \\t) in SQL code; the text was exported as an escaped string"))
        if re.search(r',\s*\)', code):
            findings.append(Finding('ERROR', 'E006', path, start_line, "trailing comma before ')'"))
        if code.count('(') != code.count(')'):
            findings.append(Finding('ERROR', 'E005', path, start_line,
                                    f"unbalanced parentheses ({code.count('(')} open, {code.count(')')} close)"))
        use = re.search(r'^\s*USE\s+\[?(\w+)\]?', code, re.I | re.M)
        if use:
            findings.append(Finding('ERROR', 'E004', path, start_line,
                                    f"USE {use.group(1)} would leave the disposable CI database"))
        module = MODULE_RE.search(code)
        if module:
            first = re.match(r'\s*([A-Za-z_]+)', code)
            if not first or first.group(1).upper() not in ('CREATE', 'ALTER'):
                findings.append(Finding('ERROR', 'E003', path, start_line,
                                        "CREATE " + module.group(1).upper() + " must be the first statement in its batch (found '"
                                        + (first.group(1) if first else '?') + "' first)"))
        for m in OBJECT_RE.finditer(code):
            kind = m.group(1).upper()
            name = m.group(3)
            if name.startswith('#'):
                continue
            created.setdefault(name.lower(), []).append((path, start_line, kind))
        local_names = {m.group(1).lower() for m in re.finditer(r'\bDECLARE\s+(\w+)\s+CURSOR\b', code, re.I)}
        local_names |= {m.group(1).lower() for m in re.finditer(r'(?:\bWITH|,)\s*(\w+)\s*(?:\([^)]*\))?\s*AS\s*\(', code, re.I)}
        if module or re.search(r'\bCREATE\s+TABLE\b', code, re.I) is None:
            for m in REF_RE.finditer(code):
                schema, name, is_call = m.group(1), m.group(2), m.group(3)
                if is_call or name.lower() in IGNORED_REFS or name.lower() in local_names \
                        or (schema and schema.lower() in ('sys', 'information_schema')):
                    continue
                referenced.setdefault(name.lower(), set()).add(path)


# ------------------------------------------------------------------ export handling

def read_text(path):
    with open(path, 'rb') as handle:
        raw = handle.read()
    for enc in ('utf-8-sig', 'utf-16'):
        try:
            return raw.decode(enc)
        except UnicodeDecodeError:
            continue
    raise SystemExit(f"cannot decode {path}")


def parse_columns(body):
    cols = collections.OrderedDict()
    body = re.sub(r'\bCONSTRAINT\s+\[\w+\]', ' ', body, flags=re.I)   # constraint names are not columns
    for m in re.finditer(r'\[(\w+)\]\s*(\[?[A-Za-z0-9_]+\]?(?:\s*\(\s*(?:max|\d+(?:\s*,\s*\d+)?)\s*\))?)([^,\n]*)', body, re.I):
        name = m.group(1).lower()
        if name in cols:
            continue
        typ = re.sub(r'[\[\]\s]', '', m.group(2)).lower()
        cols[name] = (typ, 'not null' if 'NOT NULL' in m.group(3).upper() else 'null')
    return cols


def export_objects(text):
    """Yield (name, type_code, sql_or_None_when_stub) for every '-- OBJECT:' block of the DB57 export."""
    for m in re.finditer(r'-- OBJECT: (\S+) \((\w+)\)\n-+\n(.*?)\nGO\n', text, re.S):
        body = m.group(3)
        stub = '[Notice]' in body and not re.search(r'^\s*CREATE\b', body, re.M | re.I)
        yield m.group(1), m.group(2), None if stub else body


def check_export(path, reference, strict, emit, halve, out):
    text = read_text(path)
    findings = []
    created, referenced = {}, {}
    batches, ambiguous, _ = split_batches(text)
    objs = list(export_objects(text))
    stubs = [(n, t) for n, t, s in objs if s is None]
    out.append(f"EXPORT {path}: {len(batches)} batches, {len(objs)} objects, {len(stubs)} stubs (object missing on the exported DB)")
    analyse_file(path, text, findings, created, referenced)
    by_code = collections.Counter()
    for f in findings:
        by_code[f.code] += 1
    for code, count in sorted(by_code.items()):
        sample = next(f for f in findings if f.code == code)
        out.append(f"  {code}: {count} occurrence(s); first {sample.path}:{sample.line}: {sample.message}")
    if stubs:
        out.append("  stubs: " + ', '.join(f"{n}({t})" for n, t in stubs))
    if reference:
        ratio = compare_with_reference(text, read_text(reference), out)
    if emit:
        emit_loadable(text, emit, halve, out)
    return findings if strict else []


def compare_with_reference(export_text, reference_text, out):
    exp = {}
    for name, typ, body in export_objects(export_text):
        if typ == 'U' and body:
            sql = body.replace('\\n', '\n')
            exp[name.lower()] = parse_columns(sql[sql.index('('):])
    ref = {}
    for part in re.split(r'(?=CREATE TABLE \[dbo\]\.)', reference_text):
        m = re.match(r'CREATE TABLE \[dbo\]\.\[(\w+)\]\s*\(', part)
        if m:
            end = re.search(r'\n(?:GO|CREATE TABLE)', part)
            ref[m.group(1).lower()] = parse_columns(part[m.end() - 1:end.start() if end else None])
    doubled = other = same = 0
    only_export, only_ref, examples = [], [], []
    for table in sorted(set(exp) & set(ref)):
        a, b = exp[table], ref[table]
        for col in sorted(set(a) & set(b)):
            ta, tb = a[col][0], b[col][0]
            if ta == tb:
                same += 1
                continue
            ma, mb = re.fullmatch(r'(n(?:var)?char)\((\d+)\)', ta), re.fullmatch(r'(n(?:var)?char)\((\d+)\)', tb)
            if ma and mb and ma.group(1) == mb.group(1) and int(ma.group(2)) == 2 * int(mb.group(2)):
                doubled += 1
            else:
                other += 1
                if len(examples) < 6:
                    examples.append(f"{table}.{col}: export {ta} vs reference {tb}")
        for col in sorted(set(a) - set(b)):
            only_export.append(f"{table}.{col}")
        for col in sorted(set(b) - set(a)):
            only_ref.append(f"{table}.{col}")
    out.append(f"  REFERENCE compare ({len(set(exp) & set(ref))} common tables): identical types={same}, "
               f"export length exactly 2x reference={doubled}, other type differences={other}")
    if doubled:
        out.append("  WARN W010: NVARCHAR/NCHAR lengths in the export are exactly twice the reference (bytes exported as characters); "
                   "do not deploy them as DDL without --halve-unicode-lengths and owner confirmation")
    for e in examples:
        out.append("    differs: " + e)
    if only_export:
        out.append("  columns only in export: " + ', '.join(only_export[:12]) + (' ...' if len(only_export) > 12 else ''))
    if only_ref:
        out.append("  columns only in reference: " + ', '.join(only_ref[:12]))
    return doubled


def emit_loadable(text, target, halve, out):
    parts = ["-- Generated by sql_batch_check.py from the DB57 export. NOT a deployment artifact for DB57/DB14.\n"]
    skipped = []
    for name, typ, body in export_objects(text):
        if body is None:
            skipped.append(name)
            continue
        sql = body.replace('\\n', '\n')
        if typ == 'U':
            sql = re.sub(r',\s*\n?\s*\)\s*;?\s*$', '\n)', sql.strip())
            if halve:
                sql = re.sub(r'\b(NVARCHAR|NCHAR)\s*\(\s*(\d+)\s*\)',
                             lambda m: f"{m.group(1)}({max(1, int(m.group(2)) // 2)})", sql, flags=re.I)
        parts.append(f"-- OBJECT: {name} ({typ})\n{'-' * 82}\n{sql.strip()}\nGO\n")
    with open(target, 'w', encoding='utf-8', newline='\n') as handle:
        handle.write('\n'.join(parts))
    check = []
    created, referenced = {}, {}
    with open(target, encoding='utf-8') as handle:
        generated = handle.read()
    analyse_file(target, generated, check, created, referenced)
    errors = [f for f in check if f.level == 'ERROR']
    out.append(f"  EMIT {target}: {len(parts) - 1} objects written, {len(skipped)} stubs skipped, "
               f"{len(errors)} loader error(s) in the generated file" + (" (unicode lengths halved)" if halve else ""))
    for f in errors[:10]:
        out.append("    " + str(f))


# ------------------------------------------------------------------ main

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--files', nargs='*', default=[], help='deploy files in load order (checked strictly)')
    ap.add_argument('--export', help='full DB57 export (docs/SQL.text); informational')
    ap.add_argument('--reference', help='reference DDL (SQL/QC_Schema/01_Tables.sql) to compare export column types')
    ap.add_argument('--strict-export', action='store_true', help='fail on defects found in --export')
    ap.add_argument('--emit-loadable', help='write a normalised loadable copy of the export')
    ap.add_argument('--halve-unicode-lengths', action='store_true')
    args = ap.parse_args(argv)
    if not args.files and not args.export:
        ap.print_usage(sys.stderr)
        return 2
    out, findings = [], []
    created, referenced = {}, {}
    try:
        for path in args.files:
            text = read_text(path)
            batches, _, _ = split_batches(text)
            out.append(f"FILE {path}: {len([b for b in batches])} batch(es)")
            analyse_file(path, text, findings, created, referenced)
        if args.export:
            findings += check_export(args.export, args.reference, args.strict_export,
                                     args.emit_loadable, args.halve_unicode_lengths, out)
    except OSError as ex:
        print(f"ERROR cannot read input: {ex}", file=sys.stderr)
        return 2
    if args.files:
        known = set(created)
        unresolved = sorted(n for n in referenced if n not in known)
        if unresolved:
            out.append("INFO tables/views referenced by the loaded files but not created by them (must come from the base schema or be dynamic): "
                       + ', '.join(unresolved[:40]) + (' ...' if len(unresolved) > 40 else ''))
        duplicates = {n: v for n, v in created.items() if len({p for p, _, _ in v}) > 1}
        for name, places in sorted(duplicates.items()):
            findings.append(Finding('WARN', 'W011', places[0][0], places[0][1],
                                    f"object '{name}' is created in more than one file: " + ', '.join(sorted({p for p, _, _ in places}))))
    for f in findings:
        out.append(str(f))
    errors = [f for f in findings if f.level == 'ERROR']
    out.append(f"RESULT: {'FAIL' if errors else 'PASS'} ({len(errors)} error(s), {len(findings) - len(errors)} warning(s))")
    print('\n'.join(out))
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
