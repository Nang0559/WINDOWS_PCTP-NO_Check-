#!/usr/bin/env python3
"""Generate reviewable SQL Server table DDL from SQL2 column metadata."""
from __future__ import annotations
import argparse,csv,json,re,sys
from collections import defaultdict
from pathlib import Path
REQ={"TableName","type_desc","column_id","ColumnName","DataType","MaxChars","precision","scale","is_nullable","is_identity","seed_value","increment_value","is_computed","ComputedDefinition","DefaultDefinition"}
def fixed_rows(path,prefix):
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
        reader=csv.DictReader(f,dialect=dialect)
        if not reader.fieldnames: raise ValueError("CSV inventory has no header")
        reader.fieldnames=[x.strip() if x else "" for x in reader.fieldnames]
        yield from ({str(k).strip():(v.strip() if isinstance(v,str) else "") for k,v in row.items() if k is not None} for row in reader)
def load_columns(path,table_filter=None):
    if path.suffix.casefold()==".csv":
        rows=list(csv_rows(path)); need=REQ|{"SchemaName"}
        if not rows or not need.issubset(rows[0]):
            try: alt=list(fixed_rows(path,"SchemaName"))
            except ValueError: alt=[]
            if alt and need.issubset(alt[0]): rows=alt
            else: raise ValueError("CSV/fixed-width column inventory header is incomplete; missing: "+", ".join(sorted(need-(set(rows[0]) if rows else set()))))
        rows=[r for r in rows if r.get("SchemaName","").casefold()=="dbo"]
        if not rows:return defaultdict(list)
    else: rows=list(fixed_rows(path,"TableName"))
    if not rows: raise ValueError("SQL2 column metadata contains no rows")
    if not REQ.issubset(rows[0]): raise ValueError("SQL2 column metadata header is incomplete: "+", ".join(sorted(REQ)))
    out=defaultdict(list); allowed={x.casefold() for x in table_filter} if table_filter is not None else None
    for row in rows:
        if row.get("type_desc")=="USER_TABLE" and row.get("TableName") and row.get("ColumnName") and row.get("column_id","").isdigit() and (allowed is None or row["TableName"].casefold() in allowed): out[row["TableName"].casefold()].append(row)
    for value in out.values(): value.sort(key=lambda r:int(r["column_id"]))
    return out
def load_allowlist(path):
    out={}
    with path.open(encoding="utf-8-sig",newline="") as f:
        for row in csv.DictReader(f):
            generated="SQL/QC_Schema/02_Generated_From_DB57.sql" in row.get("git_ddl_files",""); cls=row.get("class","").strip()
            scoped=row.get("in_runtime_inventory","").strip().casefold() in {"có","co","yes","true"} or cls in {"GAP-no-git-ddl","GAP-fk-dependency"} or (cls in {"GAP-ci-schema","OK-testable","OK-fk-dependency"} and (row.get("referenced_by_code","").strip().casefold() in {"có","co","yes","true"} or bool(row.get("dependency_of_code","").strip()) or generated))
            if scoped and row.get("kind","").strip().lower()=="table" and row.get("on_db57_export","").strip().lower() in {"có","co","yes","true"}: out[row["object"].strip().casefold()]=row["object"].strip()
    if not out: raise ValueError("coverage allow-list is empty; expected GAP-no-git-ddl table rows")
    return out
def _max_chars(row):
    raw=row.get("MaxChars",""); return int(raw) if raw.lstrip("-").isdigit() else None
def sql_type(row,type_map):
    t=row["DataType"].strip().lower(); m=_max_chars(row); p=int(row["precision"]) if row.get("precision","").isdigit() else 0; s=int(row["scale"]) if row.get("scale","").isdigit() else 0
    if t in type_map:
        mapped=type_map[t].strip()
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*(?:\((?:MAX|\d+(?:\s*,\s*\d+)?)\))?",mapped,re.I): raise ValueError(f"invalid explicit type mapping for {row['DataType']}: {mapped!r}")
        return mapped
    if t in {"varchar","char","varbinary","binary"}:
        if m is None: raise ValueError(f"missing byte length for {row['TableName']}.{row['ColumnName']}")
        if m!=-1 and m<1: raise ValueError(f"invalid zero/non-positive length for {row['TableName']}.{row['ColumnName']}: {m}")
        return f"{t}({'MAX' if m==-1 else m})"
    if t in {"nvarchar","nchar"}:
        if m is None: raise ValueError(f"missing character length for {row['TableName']}.{row['ColumnName']}")
        if m==-1:return f"{t}(MAX)"
        if m<1: raise ValueError(f"invalid zero/non-positive Unicode character length for {row['TableName']}.{row['ColumnName']}: {m}")
        return f"{t}({m})"
    if t in {"decimal","numeric"}:
        if not 1<=p<=38 or not 0<=s<=p: raise ValueError(f"invalid precision/scale for {row['TableName']}.{row['ColumnName']}: {p},{s}")
        return f"{t}({p},{s})"
    builtins={"bigint","int","smallint","tinyint","bit","date","datetime","datetime2","smalldatetime","time","datetimeoffset","float","real","money","smallmoney","uniqueidentifier","xml","sql_variant","text","ntext","image","timestamp","rowversion","sysname"}
    if t not in builtins: raise ValueError(f"unsupported/alias SQL type '{row['DataType']}' for {row['TableName']}.{row['ColumnName']}; add a verified --type-map entry")
    if t in {"datetime2","time","datetimeoffset"}:
        if not 0<=s<=7: raise ValueError(f"invalid temporal scale for {row['TableName']}.{row['ColumnName']}: {s}")
        return f"{t}({s})"
    return t
def verifier_max_length(row,base):
    x=_max_chars(row)
    if x is None:return -999999
    if x==-1:return -1
    if base in {"nvarchar","nchar"}:
        if x<1: raise ValueError(f"invalid zero/non-positive Unicode character length for {row['TableName']}.{row['ColumnName']}: {x}")
        return x*2
    if x<1 and base in {"varchar","char","varbinary","binary"}: raise ValueError(f"invalid zero/non-positive length for {row['TableName']}.{row['ColumnName']}: {x}")
    return x
def lit(value): return "N'"+str(value).replace("'","''")+"'"
def expr(value,table,col,kind):
    value=(value or "").strip()
    if not value or value.upper()=="NULL":return ""
    if "\n" in value or "\r" in value or ";" in value: raise ValueError(f"unsafe/multiline {kind} for {table}.{col}; review manually")
    return value
def render(inventory,coverage,type_map_path=None):
    allow=load_allowlist(coverage); cols=load_columns(inventory,set(allow)); missing=sorted(set(allow)-set(cols))
    if missing: raise ValueError("allow-listed tables absent from SQL2 column metadata: "+", ".join(missing))
    type_map={str(k).casefold():str(v) for k,v in json.loads(type_map_path.read_text(encoding="utf-8-sig")).items()} if type_map_path else {}
    lines=["/*"," AUTO-GENERATED from SQL2 DB57 column metadata for the reviewed table allowlist."," PK/unique indexes, FKs and CHECK constraints are emitted separately by 03_Generated_From_DB57_Constraints.sql."," Triggers, permissions and procedure dependencies require separate review. Alias types require a verified map."," Apply only to disposable PCTP_QC_CI. Never apply to DB57 or production.","*/","SET ANSI_NULLS ON;","SET QUOTED_IDENTIFIER ON;","GO",""]
    for key,name in sorted(allow.items(),key=lambda x:x[1].casefold()):
        rows=cols[key]; defs=[]
        for row in rows:
            col=row["ColumnName"].replace("]","]]" ); typ=sql_type(row,type_map)
            if row.get("is_computed","0")=="1":
                definition=expr(row.get("ComputedDefinition"),name,col,"computed expression")
                if not definition: raise ValueError(f"computed column missing expression: {name}.{col}")
                defs.append(f"        [{col}] AS {definition}"); continue
            default_value=expr(row.get("DefaultDefinition"),name,col,"default"); identity=""
            if row.get("is_identity","0").strip().casefold() in {"1","true","yes"}:
                seed=row.get("seed_value","NULL"); increment=row.get("increment_value","NULL")
                if seed in ("","NULL") or increment in ("","NULL"): raise ValueError(f"IDENTITY seed/increment missing: {name}.{col}")
                identity=f" IDENTITY({seed},{increment})"
            nullable=" NULL" if row.get("is_nullable","1").strip().casefold() in {"1","true","yes"} else " NOT NULL"; default=f" DEFAULT {default_value}" if default_value else ""
            defs.append(f"        [{col}] {typ}{identity}{nullable}{default}")
        lines += [f"IF OBJECT_ID(N'dbo.{name}', N'U') IS NULL","BEGIN",f"    CREATE TABLE dbo.[{name}] (",",\n".join(defs),"    );","END;","GO",""]
        expected=[]
        for i,row in enumerate(rows,1):
            mapped=type_map.get(row["DataType"].strip().casefold(),row["DataType"]).strip(); match=re.match(r"[A-Za-z][A-Za-z0-9_]*",mapped)
            if not match: raise ValueError(f"invalid SQL type for {name}.{row['ColumnName']}: {mapped!r}")
            base={"sysname":"nvarchar","rowversion":"timestamp"}.get(match.group(0).casefold(),match.group(0).casefold()); p=int(row["precision"]) if row.get("precision","").isdigit() else 0; s=int(row["scale"]) if row.get("scale","").isdigit() else 0
            nullable=1 if row.get("is_nullable","1").strip().casefold() in {"1","true","yes"} else 0; identity=1 if row.get("is_identity","0").strip().casefold() in {"1","true","yes"} else 0; computed=1 if row.get("is_computed","0").strip().casefold() in {"1","true","yes"} else 0
            computed_definition=expr(row.get("ComputedDefinition"),name,row["ColumnName"],"computed expression"); default_definition=expr(row.get("DefaultDefinition"),name,row["ColumnName"],"default"); seed=row.get("seed_value","").strip() if identity else ""; increment=row.get("increment_value","").strip() if identity else ""
            for label,value in (("identity seed",seed),("identity increment",increment)):
                if value and not re.fullmatch(r"[-+]?(?:\d+(?:\.\d*)?|\.\d+)",value): raise ValueError(f"invalid {label}: {name}.{row['ColumnName']}={value!r}")
            expected.append(f"({i}, {lit(row['ColumnName'])}, {lit(base)}, {verifier_max_length(row,base)}, {p}, {s}, {nullable}, {identity}, {computed}, {lit(computed_definition)}, {lit(default_definition)}, {lit(seed)}, {lit(increment)})")
        values=", ".join(expected); actual=("SELECT ROW_NUMBER() OVER (ORDER BY c.column_id), c.name, TYPE_NAME(c.system_type_id), c.max_length, c.precision, c.scale, CONVERT(int,c.is_nullable), CONVERT(int,c.is_identity), CONVERT(int,c.is_computed), ISNULL(cc.definition,N''), ISNULL(dc.definition,N''), ISNULL(CONVERT(nvarchar(128),ic.seed_value),N''), ISNULL(CONVERT(nvarchar(128),ic.increment_value),N'') FROM sys.columns c LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id LEFT JOIN sys.identity_columns ic ON ic.object_id=c.object_id AND ic.column_id=c.column_id f"WHERE c.object_id=OBJECT_ID({lit('dbo.'+name)})")
        expected_query=("SELECT v.column_id,v.column_name,v.type_name,v.max_length,v.precision,v.scale,v.is_nullable,v.is_identity,v.is_computed,v.computed_definition,v.default_definition,v.seed_value,v.increment_value FROM (VALUES "+values+") v(column_id,column_name,type_name,max_length,precision,scale,is_nullable,is_identity,is_computed,computed_definition,default_definition,seed_value,increment_value)")
        lines += [f"IF OBJECT_ID(N'dbo.{name}', N'U') IS NULL THROW 52201, N'Missing SQL2 table dbo.{name}', 1;",f"IF EXISTS ({actual} EXCEPT {expected_query}) OR EXISTS ({expected_query} EXCEPT {actual})",f"    THROW 52202, N'SQL2 table/column definition mismatch: dbo.{name}', 1;","GO",""]
    return "\n".join(lines+["/* PK/index/FK/CHECK constraints are emitted from verified SQL2 metadata in 03_Generated_From_DB57_Constraints.sql. */",""])
def main():
    parser=argparse.ArgumentParser(); parser.add_argument("--inventory",type=Path,required=True); parser.add_argument("--coverage",type=Path,required=True); parser.add_argument("--out",type=Path,required=True); parser.add_argument("--type-map",type=Path); parser.add_argument("--check",action="store_true"); args=parser.parse_args()
    try: content=render(args.inventory,args.coverage,args.type_map)
    except (OSError,ValueError,KeyError,json.JSONDecodeError) as exc: print(f"ERROR: {exc}",file=sys.stderr); return 2
    if args.check:
        if not args.out.exists() or args.out.read_text(encoding="utf-8")!=content: print(f"ERROR: generated DDL is stale: {args.out}",file=sys.stderr); return 1
        print(f"PASS: generated DDL is current ({args.out})")
    else:
        args.out.parent.mkdir(parents=True,exist_ok=True); args.out.write_text(content,encoding="utf-8",newline="\n"); print(f"Wrote {args.out} ({len(content.splitlines())} lines)")
    return 0
if __name__=="__main__": raise SystemExit(main())
