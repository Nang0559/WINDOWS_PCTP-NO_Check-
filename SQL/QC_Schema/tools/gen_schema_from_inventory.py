#!/usr/bin/env python3
"""Generate reviewable SQL Server table DDL from SQL2 column metadata."""
from __future__ import annotations
import argparse, csv, json, re, sys
from collections import defaultdict
from pathlib import Path

REQ={"TableName","type_desc","column_id","ColumnName","DataType","MaxChars","precision","scale","is_nullable","is_identity","seed_value","increment_value","is_computed","ComputedDefinition","DefaultDefinition"}

def fixed_rows(path, prefix):
    lines=path.read_text(encoding="utf-8-sig",errors="replace").splitlines(); hi=next((i for i,x in enumerate(lines) if x.startswith(prefix)),None)
    if hi is None: raise ValueError(f"SQL2 section header not found: {prefix}")
    starts=[m.start() for m in re.finditer(r"\S+",lines[hi])]; names=re.findall(r"\S+",lines[hi]); si=next((i for i in range(hi+1,min(hi+4,len(lines))) if lines[i].startswith("---")),None)
    if si is None: raise ValueError(f"SQL2 section separator not found after {prefix}")
    for line in lines[si+1:]:
        if not line.strip() or line.startswith("--") or line.startswith("===="): break
        yield {names[j]:line[s:(starts[j+1] if j+1<len(starts) else len(line))].strip() if s<len(line) else "" for j,s in enumerate(starts)}

def csv_rows(path):
    with path.open(encoding="utf-8-sig",newline="") as f:
        sample=f.read(65536); f.seek(0)
        try: dialect=csv.Sniffer().sniff(sample,delimiters=",;\t|")
        except csv.Error: dialect=csv.excel
        r=csv.DictReader(f,dialect=dialect)
        if not r.fieldnames: raise ValueError("CSV inventory has no header")
        r.fieldnames=[x.strip() if x else "" for x in r.fieldnames]
        yield from ({str(k).strip():(v.strip() if isinstance(v,str) else "") for k,v in x.items() if k is not None} for x in r)

def load_columns(path, table_filter=None):
    if path.suffix.casefold()==".csv":
        rows=list(csv_rows(path)); need=REQ|{"SchemaName"}
        if not rows or not need.issubset(rows[0]):
            try: alt=list(fixed_rows(path,"SchemaName"))
            except ValueError: alt=[]
            if alt and need.issubset(alt[0]): rows=alt
            else: raise ValueError("CSV/fixed-width column inventory header is incomplete; missing: "+", ".join(sorted(need-(set(rows[0]) if rows else set()))))
        rows=[r for r in rows if r.get("SchemaName","").casefold()=="dbo"]
        if not rows: return defaultdict(list)
    else: rows=list(fixed_rows(path,"TableName"))
    if not rows: raise ValueError("SQL2 column metadata contains no rows")
    if not REQ.issubset(rows[0]): raise ValueError("SQL2 column metadata header is incomplete: "+", ".join(sorted(REQ)))
    out=defaultdict(list); allowed={x.casefold() for x in table_filter} if table_filter is not None else None
    for r in rows:
        if r.get("type_desc")!="USER_TABLE": continue
        if r.get("TableName") and r.get("ColumnName") and r.get("column_id","").isdigit() and (allowed is None or r["TableName"].casefold() in allowed): out[r["TableName"].casefold()].append(r)
    for v in out.values(): v.sort(key=lambda r:int(r["column_id"]))
    return out

def load_allowlist(path):
    out={}
    with path.open(encoding="utf-8-sig",newline="") as f:
        for r in csv.DictReader(f):
            generated="SQL/QC_Schema/02_Generated_From_DB57.sql" in r.get("git_ddl_files",""); cls=r.get("class","").strip()
            scoped=r.get("in_runtime_inventory","").strip().casefold() in {"có","co","yes","true"} or cls in {"GAP-no-git-ddl","GAP-fk-dependency"} or (cls in {"GAP-ci-schema","OK-testable","OK-fk-dependency"} and (r.get("referenced_by_code","").strip().casefold() in {"có","co","yes","true"} or bool(r.get("dependency_of_code","").strip()) or generated))
            if scoped and r.get("kind","").strip().lower()=="table" and r.get("on_db57_export","").strip().lower() in {"có","co","yes","true"}: out[r["object"].strip().casefold()]=r["object"].strip()
    if not out: raise ValueError("coverage allow-list is empty; expected GAP-no-git-ddl table rows")
    return out

def sql_type(r, type_map):
    t=r["DataType"].strip().lower(); m=int(r["MaxChars"]) if r.get("MaxChars","").lstrip("-").isdigit() else None; p=int(r["precision"]) if r.get("precision","").isdigit() else 0; s=int(r["scale"]) if r.get("scale","").isdigit() else 0
    if t in type_map:
        x=type_map[t].strip()
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*(?:\((?:MAX|\d+(?:\s*,\s*\d+)?)\))?",x,re.I): raise ValueError(f"invalid explicit type mapping for {r['DataType']}: {x!r}")
        return x
    if t in {"varchar","char","varbinary","binary"}: return f"{t}({'MAX' if m==-1 else m})"
    if t in {"nvarchar","nchar"}:
        if m is None: raise ValueError(f"missing byte length for {r['TableName']}.{r['ColumnName']}")
        if m!=-1: m-=m%2
        return f"{t}({'MAX' if m==-1 else m//2})"
    if t in {"decimal","numeric"}:
        if not 1<=p<=38 or not 0<=s<=p: raise ValueError(f"invalid precision/scale for {r['TableName']}.{r['ColumnName']}: {p},{s}")
        return f"{t}({p},{s})"
    builtins={"bigint","int","smallint","tinyint","bit","date","datetime","datetime2","smalldatetime","time","datetimeoffset","float","real","money","smallmoney","uniqueidentifier","xml","sql_variant","text","ntext","image","timestamp","rowversion","sysname"}
    if t not in builtins: raise ValueError(f"unsupported/alias SQL type '{r['DataType']}' for {r['TableName']}.{r['ColumnName']}; add a verified --type-map entry")
    if t in {"datetime2","time","datetimeoffset"}: return f"{t}({s})"
    return t

def verifier_max_length(r, base):
    x=int(r["MaxChars"]) if r.get("MaxChars","").lstrip("-").isdigit() else -999999
    return -1 if x==-1 else (x-x%2 if base in {"nvarchar","nchar"} else x)

def lit(x): return "N'"+str(x).replace("'","''")+"'"
def expr(x,table,col,kind):
    x=(x or "").strip()
    if not x or x.upper()=="NULL": return ""
    if "\n" in x or "\r" in x or ";" in x: raise ValueError(f"unsafe/multiline {kind} for {table}.{col}; review manually")
    return x

def render(inventory,coverage,type_map_path=None):
    allow=load_allowlist(coverage); cols=load_columns(inventory,set(allow)); missing=sorted(set(allow)-set(cols))
    if missing: raise ValueError("allow-listed tables absent from SQL2 column metadata: "+", ".join(missing))
    tm={str(k).casefold():str(v) for k,v in json.loads(type_map_path.read_text(encoding="utf-8-sig")).items()} if type_map_path else {}
    lines=["/*"," AUTO-GENERATED from SQL2 DB57 column metadata for the reviewed table allowlist."," PK/unique indexes, FKs and CHECK constraints are emitted separately by 03_Generated_From_DB57_Constraints.sql."," Triggers, permissions and procedure dependencies require separate review. Alias types require a verified map."," Apply only to disposable PCTP_QC_CI. Never apply to DB57 or production.","*/","SET ANSI_NULLS ON;","SET QUOTED_IDENTIFIER ON;","GO",""]
    for key,name in sorted(allow.items(),key=lambda x:x[1].casefold()):
        rows=cols[key]; defs=[]
        for r in rows:
            c=r["ColumnName"].replace("]","]]" ); typ=sql_type(r,tm)
            if r.get("is_computed","0")=="1":
                e=expr(r.get("ComputedDefinition"),name,c,"computed expression")
                if not e: raise ValueError(f"computed column missing expression: {name}.{c}")
                defs.append(f"        [{c}] AS {e}"); continue
            d=expr(r.get("DefaultDefinition"),name,c,"default"); ident=""
            if r.get("is_identity","0").strip().casefold() in {"1","true","yes"}:
                seed,inc=r.get("seed_value","NULL"),r.get("increment_value","NULL")
                if seed in ("","NULL") or inc in ("","NULL"): raise ValueError(f"IDENTITY seed/increment missing: {name}.{c}")
                ident=f" IDENTITY({seed},{inc})"
            null=" NULL" if r.get("is_nullable","1").strip().casefold() in {"1","true","yes"} else " NOT NULL"; default=f" DEFAULT {d}" if d else ""
            defs.append(f"        [{c}] {typ}{ident}{null}{default}")
        lines += [f"IF OBJECT_ID(N'dbo.{name}', N'U') IS NULL","BEGIN",f"    CREATE TABLE dbo.[{name}] (",",\n".join(defs),"    );","END;","GO",""]
        expected=[]
        for i,r in enumerate(rows,1):
            mapped=tm.get(r["DataType"].strip().casefold(),r["DataType"]).strip(); base=re.match(r"[A-Za-z][A-Za-z0-9_]*",mapped).group(0).casefold(); base={"sysname":"nvarchar","rowversion":"timestamp"}.get(base,base)
            p=int(r["precision"]) if r.get("precision","").isdigit() else 0; s=int(r["scale"]) if r.get("scale","").isdigit() else 0
            nullable=1 if r.get("is_nullable","1").strip().casefold() in {"1","true","yes"} else 0; identity=1 if r.get("is_identity","0").strip().casefold() in {"1","true","yes"} else 0; computed=1 if r.get("is_computed","0").strip().casefold() in {"1","true","yes"} else 0
            cd=expr(r.get("ComputedDefinition"),name,r["ColumnName"],"computed expression"); dd=expr(r.get("DefaultDefinition"),name,r["ColumnName"],"default"); seed=r.get("seed_value","").strip() if identity else ""; inc=r.get("increment_value","").strip() if identity else ""
            for label,v in (("identity seed",seed),("identity increment",inc)):
                if v and not re.fullmatch(r"[-+]?(?:\d+(?:\.\d*)?|\.\d+)",v): raise ValueError(f"invalid {label}: {name}.{r['ColumnName']}={v!r}")
            expected.append(f"({i}, {lit(r['ColumnName'])}, {lit(base)}, {verifier_max_length(r,base)}, {p}, {s}, {nullable}, {identity}, {computed}, {lit(cd)}, {lit(dd)}, {lit(seed)}, {lit(inc)})")
        vals=", ".join(expected); actual=(f"SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID({lit('dbo.'+name)})")
        exp=(f"SELECT v.column_id,v.column_name,v.type_name,v.max_length,v.precision,v.scale,v.is_nullable,v.is_identity,v.is_computed,v.computed_definition,v.default_definition,v.seed_value,v.increment_value FROM (VALUES {vals}) v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)")
        lines += [f"IF OBJECT_ID(N'dbo.{name}', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.{name}', 1;",f"IF EXISTS ({actual} EXCEPT {exp}) OR EXISTS ({exp} EXCEPT {actual})",f"    THROW 52202, N'SQL2 table/column definition mismatch: dbo.{name}', 1;","GO",""]
    return "\n".join(lines+["/* PK/index/FK/CHECK constraints are emitted from verified SQL2 metadata in 03_Generated_From_DB57_Constraints.sql. */",""])

def main():
    p=argparse.ArgumentParser(); p.add_argument("--inventory",type=Path,required=True); p.add_argument("--coverage",type=Path,required=True); p.add_argument("--out",type=Path,required=True); p.add_argument("--type-map",type=Path); p.add_argument("--check",action="store_true"); a=p.parse_args()
    try: content=render(a.inventory,a.coverage,a.type_map)
    except (OSError,ValueError,KeyError,json.JSONDecodeError) as e: print(f"ERROR: {e}",file=sys.stderr); return 2
    if a.check:
        if not a.out.exists() or a.out.read_text(encoding="utf-8")!=content: print(f"ERROR: generated DDL is stale: {a.out}",file=sys.stderr); return 1
        print(f"PASS: generated DDL is current ({a.out})")
    else: a.out.parent.mkdir(parents=True,exist_ok=True); a.out.write_text(content,encoding="utf-8",newline="\n"); print(f"Wrote {a.out} ({len(content.splitlines())} lines)")
    return 0
if __name__=="__main__": raise SystemExit(main())
