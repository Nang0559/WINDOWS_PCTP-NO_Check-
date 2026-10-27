#!/usr/bin/env python3
"""Generate allow-list-scoped SQL Server indexes/constraints from SQL2 metadata."""
from __future__ import annotations
import argparse, csv, json, re, sys
from collections import defaultdict
from pathlib import Path

def truth(x):
    return str(x).strip().casefold() in {"1","true","yes"}
def ident(x):
    return "[" + x.replace("]", "]]") + "]"
def qname(schema, name):
    return ident(schema) + "." + ident(name)
def allowlist(path):
    result={}
    with path.open(encoding="utf-8-sig", newline="") as f:
        for r in csv.DictReader(f):
            files=r.get("git_ddl_files",""); cls=r.get("class","").strip()
            scope=cls=="GAP-no-git-ddl" or ("SQL/QC_Schema/02_Generated_From_DB57.sql" in files and cls in {"OK-testable","GAP-ci-schema"})
            if scope and r.get("kind","").strip().lower()=="table" and r.get("on_db57_export","").strip().lower() in {"có","co","yes","true"}:
                name=r.get("object","").strip()
                if name: result[name.casefold()]=name
    if not result: raise ValueError("Coverage allow-list empty")
    return result

def sections(path, required_sets):
    lines=path.read_text(encoding="utf-8-sig",errors="replace").splitlines()
    found={}
    for i,line in enumerate(lines):
        cols=re.findall(r"\S+",line)
        for key,required in required_sets.items():
            if key in found or not required.issubset(set(cols)): continue
            starts=[m.start() for m in re.finditer(r"\S+",line)]
            sep=next((j for j in range(i+1,min(i+4,len(lines))) if lines[j].startswith("---")),None)
            if sep is None: raise ValueError("Missing separator for metadata section "+key)
            rows=[]
            for raw in lines[sep+1:]:
                if not raw.strip() or raw.startswith("--") or raw.startswith("===="): break
                rows.append({cols[k]:raw[starts[k]:(starts[k+1] if k+1<len(starts) else len(raw))].strip() if starts[k]<len(raw) else "" for k in range(len(cols))})
            if not rows: raise ValueError("Empty metadata section "+key)
            found[key]=rows
    missing=set(required_sets)-set(found)
    if missing: raise ValueError("Missing metadata sections: "+", ".join(sorted(missing)))
    return found

def module_inventory(path):
    # Module bodies are multiline in the SSMS text export. Only recognize row headers
    # at the fixed-width schema/object/type positions; never parse body lines as rows.
    lines=path.read_text(encoding="utf-8-sig",errors="replace").splitlines()
    header_i=next((i for i,line in enumerate(lines)
      if line.startswith("SchemaName") and {"ObjectName","ObjectType","ObjectTypeDescription","SqlDefinition"}.issubset(set(re.findall(r"\S+",line)))),None)
    if header_i is None: raise ValueError("Missing module-definition metadata header")
    header=lines[header_i]; starts=[m.start() for m in re.finditer(r"\S+",header)]
    names=re.findall(r"\S+",header)
    sep=next((j for j in range(header_i+1,min(header_i+4,len(lines))) if lines[j].startswith("---")),None)
    if sep is None: raise ValueError("Missing module metadata separator")
    rows=[]
    for raw in lines[sep+1:]:
        if not raw.strip() or raw.startswith("===="): continue
        # Continuation/body lines begin with whitespace and do not contain the
        # expected fixed-width object-type field.
        if len(raw)<=starts[2]: continue
        row={names[k]:raw[starts[k]:(starts[k+1] if k+1<len(starts) else len(raw))].strip() if starts[k]<len(raw) else "" for k in range(len(names))}
        if not row.get("SchemaName"): continue
        if row.get("SchemaName") and row.get("ObjectName") and row.get("ObjectType"):
            rows.append({"schema":row["SchemaName"],"name":row["ObjectName"],"type":row["ObjectType"],"description":row.get("ObjectTypeDescription",""),"definition_present":bool(row.get("SqlDefinition") and row["SqlDefinition"].upper()!="NULL")})
    return rows

def render(metadata, coverage):
    allowed=allowlist(coverage)
    data=sections(metadata,{
      "indexes":{"TableName","IndexName","IsPrimaryKey","IsUnique","IsUniqueConstraint","IsDisabled","HasFilter","FilterDefinition","KeyOrdinal","IsIncludedColumn","IsDescendingKey","ColumnName","IndexType"},
      "fks":{"ParentSchema","ParentTable","ParentColumn","ReferencedSchema","ReferencedTable","ReferencedColumn","ForeignKeyName","ColumnOrdinal","OnDelete","OnUpdate","IsDisabled","IsNotTrusted"},
      "checks":{"SchemaName","TableName","ConstraintName","Definition","IsDisabled","IsNotTrusted"},
      "defaults":{"SchemaName","TableName","ColumnName","ConstraintName","Definition"},
    })
    out=["/* AUTO-GENERATED from SQL2 metadata. Review before use.",
         "   Apply only to disposable PCTP_QC_CI, after 02_Generated_From_DB57.sql.",
         "   Modules, data, permissions and external-table DDL are not generated. */",
         "SET ANSI_NULLS ON;","SET QUOTED_IDENTIFIER ON;","GO",""]
    report={"allowlisted_tables":sorted(allowed.values()),"indexes":[],"checks":[],"foreign_keys_generated":[],"foreign_keys_external_or_skipped":[],"defaults_inventory":[],"modules_inventory":[],"warnings":[
      "DEFAULTs are inventory-only; column DDL already emits defaults. Compare both sources.",
      "Module bodies are inventory-only; SSMS text exports can truncate/fold multiline definitions. Inventory does not validate executable definitions.",
      "Preserve both unique constraints and same-column unique indexes when both exist in source; review intentionality."
    ]}
    grouped=defaultdict(list)
    for r in data["indexes"]:
        if r.get("SchemaName","").casefold()=="dbo" and r.get("TableName","").casefold() in allowed:
            grouped[(r["TableName"],r["IndexName"])].append(r)
    for (table,name),rows in sorted(grouped.items(),key=lambda x:(x[0][0].casefold(),x[0][1].casefold())):
        if any(truth(r.get("IsDisabled")) for r in rows): raise ValueError(f"Disabled index requires manual review: dbo.{table}.{name}")
        if any(truth(r.get("HasFilter")) for r in rows): raise ValueError(f"Filtered index requires manual review: dbo.{table}.{name}")
        rows.sort(key=lambda r:(int(r.get("KeyOrdinal") or 0),int(truth(r.get("IsIncludedColumn")))))
        keys=[r for r in rows if not truth(r.get("IsIncludedColumn")) and int(r.get("KeyOrdinal") or 0)>0]
        includes=[r for r in rows if truth(r.get("IsIncludedColumn"))]
        if not keys: raise ValueError(f"Index has no key columns: dbo.{table}.{name}")
        typ=rows[0].get("IndexType","").upper()
        if typ not in {"CLUSTERED","NONCLUSTERED"}: raise ValueError(f"Unsupported index type {typ}: dbo.{table}.{name}")
        key_sql=", ".join(ident(r["ColumnName"])+(" DESC" if truth(r.get("IsDescendingKey")) else " ASC") for r in keys)
        ispk=truth(rows[0].get("IsPrimaryKey")); isuc=truth(rows[0].get("IsUniqueConstraint")); unique=truth(rows[0].get("IsUnique"))
        escaped=name.replace("'","''")
        if ispk or isuc:
            kind="PRIMARY KEY" if ispk else "UNIQUE"
            sql=f"IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{escaped}')\nALTER TABLE dbo.{ident(table)} ADD CONSTRAINT {ident(name)} {kind} {typ} ({key_sql});"
        else:
            inc=(" INCLUDE ("+", ".join(ident(r["ColumnName"]) for r in includes)+")") if includes else ""
            sql=f"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{escaped}')\nCREATE {'UNIQUE ' if unique else ''}{typ} INDEX {ident(name)} ON dbo.{ident(table)} ({key_sql}){inc};"
        out += [sql,"GO",""]
        report["indexes"].append({"table":table,"name":name,"kind":"PK" if ispk else "UNIQUE_CONSTRAINT" if isuc else "UNIQUE_INDEX" if unique else "INDEX","keys":[{"column":r["ColumnName"],"ordinal":int(r["KeyOrdinal"]),"descending":truth(r.get("IsDescendingKey"))} for r in keys],"included":[r["ColumnName"] for r in includes]})
    for r in data["checks"]:
        table=r.get("TableName","")
        if r.get("SchemaName","").casefold()!="dbo" or table.casefold() not in allowed: continue
        name,definition=r.get("ConstraintName",""),r.get("Definition","")
        if truth(r.get("IsDisabled")) or truth(r.get("IsNotTrusted")): raise ValueError(f"Disabled/untrusted CHECK requires manual review: dbo.{table}.{name}")
        if not definition or definition.upper()=="NULL" or "\n" in definition or ";" in definition: raise ValueError(f"Unsafe CHECK definition: dbo.{table}.{name}")
        out += [f"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.{table}') AND name=N'{name.replace(chr(39),chr(39)*2)}')\nALTER TABLE dbo.{ident(table)} WITH CHECK ADD CONSTRAINT {ident(name)} CHECK {definition};","GO",""]
        report["checks"].append({"table":table,"name":name,"definition":definition})
    fk_groups=defaultdict(list)
    for r in data["fks"]:
        if r.get("ParentSchema","").casefold()=="dbo" and r.get("ParentTable","").casefold() in allowed:
            fk_groups[(r["ParentSchema"],r["ParentTable"],r["ForeignKeyName"],r["ReferencedSchema"],r["ReferencedTable"])].append(r)
    for (ps,pt,name,rs,rt),rows in sorted(fk_groups.items()):
        rows.sort(key=lambda r:int(r.get("ColumnOrdinal") or 0))
        if rs.casefold()!="dbo" or rt.casefold() not in allowed:
            report["foreign_keys_external_or_skipped"].append({"table":pt,"name":name,"references":rs+"."+rt,"reason":"referenced table outside allow-list"})
            continue
        if any(truth(r.get("IsDisabled")) or truth(r.get("IsNotTrusted")) for r in rows): raise ValueError(f"Disabled/untrusted FK requires manual review: dbo.{pt}.{name}")
        action_map={"NO_ACTION":"NO ACTION","CASCADE":"CASCADE","SET_NULL":"SET NULL","SET_DEFAULT":"SET DEFAULT"}
        delete=rows[0].get("OnDelete","NO_ACTION").upper(); update=rows[0].get("OnUpdate","NO_ACTION").upper()
        if delete not in action_map or update not in action_map: raise ValueError(f"Unsupported FK action: dbo.{pt}.{name}")
        child=", ".join(ident(r["ParentColumn"]) for r in rows); parent=", ".join(ident(r["ReferencedColumn"]) for r in rows)
        clauses=("" if delete=="NO_ACTION" else " ON DELETE "+action_map[delete])+("" if update=="NO_ACTION" else " ON UPDATE "+action_map[update])
        escaped=name.replace("'","''")
        out += [f"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.{pt}') AND name=N'{escaped}')\nALTER TABLE dbo.{ident(pt)} WITH CHECK ADD CONSTRAINT {ident(name)} FOREIGN KEY ({child}) REFERENCES {qname(rs,rt)} ({parent}){clauses};","GO",""]
        report["foreign_keys_generated"].append({"table":pt,"name":name,"references":rs+"."+rt,"columns":[r["ParentColumn"] for r in rows],"referenced_columns":[r["ReferencedColumn"] for r in rows]})
    for r in data["defaults"]:
        if r.get("SchemaName","").casefold()=="dbo" and r.get("TableName","").casefold() in allowed:
            report["defaults_inventory"].append({"table":r["TableName"],"column":r["ColumnName"],"name":r.get("ConstraintName",""),"definition":r.get("Definition","")})
    report["modules_inventory"] = module_inventory(metadata)
    return "\n".join(out),report

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--metadata",type=Path,required=True)
    p.add_argument("--coverage",type=Path,required=True)
    p.add_argument("--out",type=Path,required=True)
    p.add_argument("--report",type=Path,required=True)
    p.add_argument("--check",action="store_true")
    a=p.parse_args()
    try: sql,report=render(a.metadata,a.coverage)
    except (OSError,ValueError,KeyError) as e:
        print("ERROR: "+str(e),file=sys.stderr); return 2
    report_text=json.dumps(report,ensure_ascii=False,indent=2)+"\n"
    if a.check:
        if not a.out.exists() or a.out.read_text(encoding="utf-8")!=sql or not a.report.exists() or a.report.read_text(encoding="utf-8")!=report_text:
            print("ERROR: generated DDL/report is stale or missing",file=sys.stderr); return 1
        print(f"PASS: current; {len(report['indexes'])} indexes/keys, {len(report['checks'])} CHECKs, {len(report['foreign_keys_generated'])} internal FKs")
        return 0
    a.out.parent.mkdir(parents=True,exist_ok=True); a.report.parent.mkdir(parents=True,exist_ok=True)
    a.out.write_text(sql,encoding="utf-8",newline="\n"); a.report.write_text(report_text,encoding="utf-8",newline="\n")
    print(f"Wrote {a.out} and {a.report}")
    return 0
if __name__=="__main__": raise SystemExit(main())
