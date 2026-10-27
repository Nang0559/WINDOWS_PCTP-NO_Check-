#!/usr/bin/env python3
"""
Gate G1 - inventory & traceability.

Static analysis only (reads the repository, never opens a database):

  code file  ->  SP / SQL tables written  ->  transaction boundary  ->  tests

A row is COMPLETE only when it has all four. Every other row is reported as GAP with the
exact missing column. Nothing is skipped silently.

Usage (repository root):
    python PCTP.QC.UI/Build-TraceabilityMatrix.py [--out <dir>] [--docs <dir>] [--allow-gaps]

Exit code 0 only when there is no GAP (or --allow-gaps). Output:
    <out>/traceability-matrix.json   machine readable
    <out>/traceability-matrix.md     human readable
    <out>/gaps.csv                   one line per gap
"""
import argparse
import csv
import json
import os
import re
import subprocess
import sys
from collections import defaultdict

ROOT = os.getcwd()
TEST_DIRS = ["PCTP.QC.Tests", "PCTP.QC.SqlIntegration"]
REPO_COMPONENT_EXTS = (".cs", ".sql", ".ps1", ".py", ".csproj", ".json")
BOUNDARY_DOC = os.path.join("docs", "TRANSACTION_BOUNDARIES.md")
TEST_MATRIX = os.path.join("PCTP.QC.Tests", "TestMatrix.json")

SKIP_DIRS = {"bin", "obj", ".git", "packages", "Properties", ".vs", "node_modules", ".venv", "venv", "TestResults", "artifacts", "coverage"}

# ----------------------------------------------------------------------------- helpers
def read(path):
    for enc in ("utf-8-sig", "utf-8", "cp1258", "latin-1"):
        try:
            with open(path, encoding=enc) as f:
                return f.read()
        except UnicodeDecodeError:
            continue
    return ""


def walk(base, exts):
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if fn.lower().endswith(exts):
                yield os.path.join(dirpath, fn)


def rel(p):
    return os.path.relpath(p, ROOT).replace("\\", "/")


# C# string literals: verbatim @"..." (with "" escapes), regular "..." (with \ escapes)
_LIT = re.compile(r'\$?@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\\r\n])*"')


def literals(src):
    out = []
    for m in _LIT.finditer(src):
        s = m.group(0)
        s = s.lstrip("$@")
        s = s[1:-1] if len(s) >= 2 else s
        out.append(s.replace('""', '"'))
    return out


def strip_comments(src):
    src = re.sub(r"/\*.*?\*/", " ", src, flags=re.S)
    return re.sub(r"(?m)(?<![:\"'])//.*$", " ", src)


KEYWORDS = {"set", "top", "select", "from", "where", "into", "values", "output", "with", "the",
            "a", "an", "table", "only", "of", "to", "in", "and", "or", "as", "if", "on", "is",
            "that", "this", "for", "by", "not", "no", "when", "then", "else", "end", "case"}

_WRITE = re.compile(
    r"(?is)\b(INSERT\s+INTO|DELETE\s+FROM|DELETE|MERGE\s+INTO|MERGE|UPDATE(?:\s+TOP\s*\(\s*\w+\s*\))?)\s+"
    r"((?:\[?\w+\]?\.){0,2}\[?[#\w]+\]?)")
_SP_EXEC = re.compile(r"(?i)\bEXEC(?:UTE)?\s+((?:\[?\w+\]?\.)?\[?(?:Usp_|sp_|usp_)\w+\]?)")
_SP_LITERAL = re.compile(r"^\s*(?:dbo\.)?((?:Usp_|usp_|sp_)\w+)\s*$")
_SP_CALL = re.compile(r'ExecuteStoredProcedure\w*\s*\(\s*"((?:dbo\.)?\w+)"')
_TABLE_READ = re.compile(r"(?is)\b(?:FROM|JOIN)\s+((?:\[?\w+\]?\.){0,2}\[?\w+\]?)")


def norm_obj(name):
    name = name.replace("[", "").replace("]", "")
    if name.lower().startswith("dbo."):
        name = name[4:]
    return name


def analyze_cs(path):
    raw = read(path)
    src = strip_comments(raw)
    lits = literals(src)
    writes, sps, reads = set(), set(), set()
    for lit in lits:
        for m in _WRITE.finditer(lit):
            t = norm_obj(m.group(2))
            # len<=2 is a SQL alias (UPDATE D SET ...), not a table.
            if t.lower() not in KEYWORDS and not t.startswith("@") and len(t) > 2:
                writes.add(t)
        for m in _SP_EXEC.finditer(lit):
            sps.add(norm_obj(m.group(1)))
        m = _SP_LITERAL.match(lit)
        if m:
            sps.add(m.group(1))
        for m in _TABLE_READ.finditer(lit):
            t = norm_obj(m.group(1))
            if re.match(r"^[A-Za-z_#]\w*$", t) and t.lower() not in KEYWORDS:
                reads.add(t)
    for m in _SP_CALL.finditer(src):
        sps.add(norm_obj(m.group(1)))
    # Temp tables / table variables are fixtures, not business tables.
    writes = {w for w in writes if not w.startswith("#")}
    reads = {r for r in reads if not r.startswith("#")}

    begins = bool(re.search(r"\bBeginTransaction\s*\(|\b(?:_?uow|unitOfWork|_?unit|_?phieuUow|uow)\w*\.Begin\s*\(|\.BeginTransaction\b", src))
    commits = bool(re.search(r"\.Commit\s*\(|\bCOMMIT\s+TRAN", src, re.I))
    rollbacks = bool(re.search(r"\.Rollback\s*\(|SafeRollback|FailAndRollback|\bROLLBACK\s+TRAN", src, re.I))
    uow_ref = bool(re.search(r"\bIUnitOfWork\b|\bUnitOfWork\b|manageTransaction|ISqlExecutor|IDbTransaction|SqlTransaction", src))
    if begins and (commits or rollbacks):
        tx = "OWNS"
    elif begins:
        tx = "BEGINS-ONLY"
    elif uow_ref:
        tx = "PARTICIPANT"
    else:
        # A single direct DML statement is atomic at the SQL Server statement
        # boundary even without an explicit multi-statement transaction.
        # Never apply this shortcut to stored-procedure calls or multi-DML batches.
        dml_count = len(_WRITE.findall(" ".join(lits)))
        tx = "STATEMENT-ATOMIC" if dml_count == 1 and not sps else "NONE"
    # A file that names SQL objects but never executes SQL (no Execute*/CallPhieuSP/DropCreate/BulkInsert/
    # SqlCommand/adapter call) only forwards to repositories that own the transaction. NONE stays NONE
    # for any file that does execute SQL without a transaction contract.
    executes_sql = bool(re.search(
        r"ExecuteNonQuery|ExecuteScalar\w*\(|ExecuteReader|\.Execute\s*\(|ExecuteStoredProcedure|CallPhieuSP|"
        r"DropCreate|BulkInsert|\bnew\s+SqlCommand\b|SqlDataAdapter|\.Fill\s*\(", src))
    if tx == "NONE" and not executes_sql:
        tx = "DELEGATES"

    classes = re.findall(r"\b(?:class|record|struct)\s+([A-Z]\w*)", src)
    exec_nonquery = bool(re.search(r"ExecuteNonQuery|ExecuteScalar\w*\(|\.Execute\s*\(|ExecuteStoredProcedure", src))
    return {
        "classes": sorted(set(classes)),
        "writes": sorted(writes),
        "reads": sorted(reads),
        "sps": sorted(sps),
        "tx": tx,
        "hasSql": bool(lits and (writes or reads or sps)) or exec_nonquery,
        "mutates": bool(writes or sps),
    }


def sql_script_objects(path):
    txt = read(path)
    objs = set()
    for m in re.finditer(r"(?is)\b(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?(PROCEDURE|PROC|TABLE|VIEW|FUNCTION|INDEX|TRIGGER)\s+"
                         r"(?:UNIQUE\s+)?(?:NONCLUSTERED\s+|CLUSTERED\s+)?(?:\[?\w+\]?\.)?\[?(\w+)\]?", txt):
        objs.add((m.group(1).upper(), m.group(2)))
    # CREATE ... INDEX name ON table is covered by group(2)=name; add target tables too
    for m in re.finditer(r"(?is)\bON\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?\s*\(", txt):
        objs.add(("TABLE-REF", m.group(1)))
    return sorted(objs)


# ----------------------------------------------------------------------------- inventory
def is_interface_file(fn):
    return bool(re.match(r"^I[A-Z]", fn))


def inventory():
    rows = []
    for path in sorted(walk(".", (".cs",))):
        fn = os.path.basename(path)
        if fn.endswith((".Designer.cs", ".g.cs")):
            continue
        p = rel(path)
        low = p.lower()
        # Scan the whole repository, not only PCTP/Modules.
        if any(part.lower().endswith((".tests", ".test")) or part.lower() in {"test", "tests"}
               for part in p.split("/")):
            continue
        in_repo_dir = "/repositories/" in low or "/repository/" in low
        in_service_dir = "/services/" in low or "/service/" in low
        name_match = bool(re.search(r"(Repository|Repo|Service)\w*\.cs$", fn))
        if is_interface_file(fn):
            continue
        info = analyze_cs(path)
        if not (in_repo_dir or in_service_dir or name_match or info["hasSql"]):
            continue
        kind = ("Repository" if re.search(r"Repo", fn) or in_repo_dir else
                "Service" if re.search(r"Service", fn) or in_service_dir else "SQL caller")
        parts = p.split("/")
        module = parts[parts.index("Modules") + 1] if "Modules" in parts else parts[0]
        rows.append({"id": None, "module": module, "kind": kind, "file": p, **info})
    return rows


def load_test_sources():
    tests = {}
    roots = set(TEST_DIRS)
    for dirpath, dirnames, _ in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        base = os.path.basename(dirpath).lower()
        if base.endswith((".tests", ".test")) or base in {"tests", "test"}:
            roots.add(os.path.relpath(dirpath, ROOT))
    for d in sorted(roots):
        if not os.path.isdir(d):
            continue
        for path in walk(d, (".cs", ".py", ".ps1", ".sql")):
            tests[rel(path)] = read(path)
    return tests


def link_tests(rows, tests):
    for r in rows:
        names = [c for c in r["classes"]] or [os.path.splitext(os.path.basename(r["file"]))[0]]
        stem = os.path.splitext(os.path.basename(r["file"]))[0]
        if stem not in names:
            names.append(stem)
        # Tests commonly reference the contract (IFooService) rather than the concrete class.
        names += ["I" + n for n in list(names) if not n.startswith("I")]
        hit_unit, hit_sql = set(), set()
        for tp, txt in tests.items():
            for n in names:
                if len(n) < 6:
                    continue
                if re.search(r"\b" + re.escape(n) + r"\b", txt):
                    (hit_sql if tp.startswith("PCTP.QC.SqlIntegration") else hit_unit).add(tp)
                    break
        r["testsUnit"] = sorted(hit_unit)
        r["testsSql"] = sorted(hit_sql)


def link_matrix(rows):
    if not os.path.exists(TEST_MATRIX):
        return {}, []
    data = json.loads(read(TEST_MATRIX))
    mods = data.get("modules", [])
    by_class = defaultdict(list)
    for m in mods:
        text = (m.get("module", "") + " " + m.get("scenario", "") + " " + m.get("testEvidence", ""))
        for r in rows:
            stem = os.path.splitext(os.path.basename(r["file"]))[0]
            if len(stem) >= 6 and re.search(r"\b" + re.escape(stem) + r"\b", text):
                by_class[r["file"]].append(m["id"])
    for r in rows:
        r["matrixIds"] = sorted(set(by_class.get(r["file"], [])))
    return {m["id"]: m for m in mods}, mods


def boundary_documented(rows):
    doc = read(BOUNDARY_DOC) if os.path.exists(BOUNDARY_DOC) else ""
    for r in rows:
        stem = os.path.splitext(os.path.basename(r["file"]))[0]
        r["txDocumented"] = bool(re.search(r"\b" + re.escape(stem) + r"\b", doc))


def sql_scripts(rows, tests):
    out = []
    code_text = {r["file"]: r for r in rows}
    all_sp = {s.lower() for r in rows for s in r["sps"]}
    all_tables = {t.lower() for r in rows for t in (r["writes"] + r["reads"])}
    test_blob = "\n".join(tests.values()).lower()
    # SQL is distributed across SQL/, module Database/ folders and tooling.
    for path in sorted(walk(".", (".sql",))):
        objs = sql_script_objects(path)
        names = [n for k, n in objs if k != "TABLE-REF"]
        used_in_code = sorted({n for n in names if n.lower() in all_sp or n.lower() in all_tables})
        used_in_tests = sorted({n for n in names if n.lower() in test_blob})
        out.append({"file": rel(path), "objects": [f"{k} {n}" for k, n in objs if k != "TABLE-REF"],
                    "referencedByCode": used_in_code, "referencedByTests": used_in_tests})
    return out


# ----------------------------------------------------------------------------- gaps
def evaluate(rows, scripts):
    gaps = []
    for i, r in enumerate(rows, 1):
        r["id"] = f"G1-{i:03d}"
        miss = []
        if not r["file"]:
            miss.append("code-file")
        if r["mutates"] and not (r["sps"] or r["writes"]):
            miss.append("sp-or-sql")
        if r["mutates"]:
            if r["tx"] in ("NONE", "BEGINS-ONLY"):
                miss.append("transaction-boundary(" + r["tx"] + ")")
            elif not r["txDocumented"]:
                miss.append("transaction-boundary(not-documented)")
        if not (r["testsUnit"] or r["testsSql"]):
            miss.append("test")
        elif r["mutates"] and not r["testsSql"]:
            miss.append("sql-test(only-fakes)")
        r["severity"] = "WRITE" if r["mutates"] else "READ"
        r["gaps"] = miss
        r["status"] = "COMPLETE" if not miss else "GAP"
        for m in miss:
            gaps.append({"id": r["id"], "kind": "code", "severity": r["severity"], "file": r["file"], "missing": m})
    for s in scripts:
        miss = []
        if not s["referencedByCode"]:
            miss.append("no-code-reference")
        if not s["referencedByTests"]:
            miss.append("no-test-reference")
        s["gaps"] = miss
        s["status"] = "COMPLETE" if not miss else "GAP"
        for m in miss:
            gaps.append({"id": "SQL", "kind": "sql-script", "severity": "WRITE", "file": s["file"], "missing": m})
    return gaps


def matrix_rows_gaps(mods, gaps):
    for m in mods:
        if m["status"] != "implemented":
            gaps.append({"id": m["id"], "kind": "test-matrix", "severity": "WRITE",
                         "file": TEST_MATRIX.replace("\\", "/"), "missing": f"status={m['status']}"})


def render_md(commit, rows, scripts, gaps, mods):
    n_gap = sum(1 for r in rows if r["status"] == "GAP")
    L = []
    L.append("# G1 - Ma trận truy vết (code -> SP/SQL -> transaction -> test)")
    L.append("")
    L.append(f"- Commit: `{commit}`  |  sinh bởi `PCTP.QC.UI/Build-TraceabilityMatrix.py` (phân tích tĩnh, không mở DB)")
    L.append(f"- Dòng code: {len(rows)} (COMPLETE {len(rows)-n_gap}, **GAP {n_gap}**)  |  SQL script: {len(scripts)}  |  dòng TestMatrix không `implemented`: "
             f"{sum(1 for m in mods if m['status']!='implemented')}")
    L.append(f"- Tổng số gap: **{len(gaps)}**. Gate G1 chỉ PASS khi bằng 0.")
    L.append("")
    L.append("Giới hạn: static inventory bằng regex, không phải compiler hay SQL parser. SQL ghép động, tên bảng lấy từ biến, reflection, DI runtime và delegate có thể không được nhận diện. Test chỉ nhắc tên class không chứng minh đã gọi production method; cần review call-site và fixture thực tế trước khi dùng làm bằng chứng release.")
    L.append("")
    L.append("## Repository / Service")
    L.append("")
    L.append("| ID | Module | Loại | File nguồn | SP | Bảng ghi | Transaction | Test unit | Test SQL | Trạng thái |")
    L.append("|---|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        tu = ", ".join(os.path.basename(x) for x in r["testsUnit"]) or "—"
        ts = ", ".join(os.path.basename(x) for x in r["testsSql"]) or "—"
        st = "OK" if r["status"] == "COMPLETE" else "**GAP**: " + "; ".join(r["gaps"])
        L.append(f"| {r['id']} | {r['module']} | {r['kind']} | `{r['file']}` | {', '.join(r['sps']) or '—'} | "
                 f"{', '.join(r['writes']) or '—'} | {r['tx']}{' (doc)' if r['txDocumented'] else ''} | {tu} | {ts} | {st} |")
    L.append("")
    L.append("## SQL scripts (toàn repository: SQL/, Modules/*/Database/, tooling)")
    L.append("")
    L.append("| File | Đối tượng | Code tham chiếu | Test tham chiếu | Trạng thái |")
    L.append("|---|---|---|---|---|")
    for s in scripts:
        st = "OK" if s["status"] == "COMPLETE" else "**GAP**: " + "; ".join(s["gaps"])
        L.append(f"| `{s['file']}` | {', '.join(s['objects']) or '—'} | {', '.join(s['referencedByCode']) or '—'} | "
                 f"{', '.join(s['referencedByTests']) or '—'} | {st} |")
    L.append("")
    L.append("## TestMatrix.json chưa `implemented`")
    L.append("")
    L.append("| ID | Module | Trạng thái | Kịch bản |")
    L.append("|---|---|---|---|")
    for m in mods:
        if m["status"] != "implemented":
            L.append(f"| {m['id']} | {m['module']} | {m['status']} | {m['scenario']} |")
    L.append("")
    return "\n".join(L)


def git_commit():
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    except Exception:
        return "unknown"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=None, help="output directory (default artifacts/gates/<short>/G1)")
    ap.add_argument("--docs", default=None, help="also write docs/TRACEABILITY_MATRIX.md/.json here")
    ap.add_argument("--allow-gaps", action="store_true")
    a = ap.parse_args()

    commit = git_commit()
    short = commit[:7]
    out = a.out or os.path.join("artifacts", "gates", short, "G1")
    os.makedirs(out, exist_ok=True)

    rows = inventory()
    tests = load_test_sources()
    link_tests(rows, tests)
    _, mods = link_matrix(rows)
    boundary_documented(rows)
    scripts = sql_scripts(rows, tests)
    gaps = evaluate(rows, scripts)
    matrix_rows_gaps(mods, gaps)

    status = "PASS" if not gaps else "FAIL"
    payload = {"gate": "G1", "status": status, "commit": commit,
               "counts": {"codeRows": len(rows), "codeGaps": sum(1 for r in rows if r["status"] == "GAP"),
                          "sqlScripts": len(scripts), "gaps": len(gaps)},
               "rows": rows, "sqlScripts": scripts, "gaps": gaps}
    md = render_md(commit, rows, scripts, gaps, mods)

    with open(os.path.join(out, "traceability-matrix.json"), "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
    with open(os.path.join(out, "traceability-matrix.md"), "w", encoding="utf-8") as f:
        f.write(md)
    with open(os.path.join(out, "gaps.csv"), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["id", "kind", "severity", "file", "missing"])
        w.writeheader()
        w.writerows(gaps)
    with open(os.path.join(out, "gate-summary.json"), "w", encoding="utf-8") as f:
        json.dump({"Gate": "G1", "Status": status, "Commit": commit, "Counts": payload["counts"]}, f, indent=2)
    if a.docs:
        os.makedirs(a.docs, exist_ok=True)
        with open(os.path.join(a.docs, "TRACEABILITY_MATRIX.md"), "w", encoding="utf-8") as f:
            f.write(md)

    c = payload["counts"]
    print(f"G1 {status}: {c['codeRows']} code rows, {c['codeGaps']} with gaps, {c['sqlScripts']} SQL scripts, {c['gaps']} gaps total")
    print(f"Output: {out}")
    return 0 if (not gaps or a.allow_gaps) else 1


if __name__ == "__main__":
    sys.exit(main())
