#!/usr/bin/env python3
"""Read-only DB57 -> DB14 comparison. Writes SQL artifacts only; never executes DDL."""
import os,csv,json,re,argparse
from pathlib import Path
from datetime import datetime,timezone
import pyodbc

ROOT=Path(__file__).resolve().parents[2]
INV=ROOT/'docs'/'PCTP_SQL_Runtime_Inventory_A-K.csv'
TABLES_SQL="""SELECT s.name schema_name,t.name table_name,c.column_id,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,ic.seed_value,ic.increment_value,c.is_computed,cc.definition computed_definition,dc.definition default_definition,c.collation_name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.identity_columns ic ON ic.object_id=t.object_id AND ic.column_id=c.column_id LEFT JOIN sys.computed_columns cc ON cc.object_id=t.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=t.object_id AND dc.parent_column_id=c.column_id WHERE s.name='dbo' AND t.is_ms_shipped=0 ORDER BY t.name,c.column_id"""
MODULES_SQL="""SELECT o.name object_name,o.type object_type,OBJECT_DEFINITION(o.object_id) definition FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE s.name='dbo' AND o.type IN ('V','P','FN','IF','TF','TR') AND o.is_ms_shipped=0"""
def rows(c,sql):
 x=c.cursor();x.execute(sql);names=[d[0] for d in x.description];return [dict(zip(names,r)) for r in x.fetchall()]
def q(x): return '['+str(x).replace(']',']]')+']'
def typ(c):
 t=c['type_name'].lower()
 if t in ('varchar','char','varbinary','binary'): return f"{t}({'max' if c['max_length']==-1 else c['max_length']})"
 if t in ('nvarchar','nchar'): return f"{t}({'max' if c['max_length']==-1 else c['max_length']//2})"
 if t in ('decimal','numeric'): return f"{t}({c['precision']},{c['scale']})"
 if t in ('datetime2','datetimeoffset','time'): return f"{t}({c['scale']})"
 return t
def colddl(c):
 if c['is_computed']: return f"{q(c['column_name'])} AS {c['computed_definition']}"
 s=f"{q(c['column_name'])} {typ(c)}"
 if c['collation_name'] and c['type_name'].lower() in ('varchar','char','nvarchar','nchar'): s+=f" COLLATE {c['collation_name']}"
 if c['is_identity']: s+=f" IDENTITY({int(c['seed_value'])},{int(c['increment_value'])})"
 s+=' NULL' if c['is_nullable'] else ' NOT NULL'
 if c['default_definition']: s+=f" DEFAULT {c['default_definition']}"
 return s
def invnames(path):
 out=set(); excluded={}
 with path.open(encoding='utf-8-sig',newline='') as f:
  for r in csv.DictReader(f):
   n=(r.get('Tên đối tượng') or '').strip(); k=(r.get('Loại đối tượng') or '').lower(); rt=(r.get('Runtime hay test-only') or '').lower(); st=(r.get('Trạng thái xác minh') or '').lower()
   if not n or any(x in k+' '+rt+' '+st for x in ('external','oracle','built-in','test-only','legacy-only','retired','không phải bảng sql','không phải bảng')): continue
   n=n.removeprefix('dbo.')
   if re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*',n): out.add(n)
   else: excluded[n]='Dynamic/non-SQL object; manual resolution required'
 return sorted(out,key=str.casefold),excluded
def connect(env):
 s=os.environ.get(env)
 if not s: raise RuntimeError('Missing secret '+env)
 return pyodbc.connect(s,timeout=20,readonly=True)
def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,default=Path('artifacts/db14-reconciliation'));p.add_argument('--inventory',type=Path,default=INV);a=p.parse_args();a.out.mkdir(parents=True,exist_ok=True)
 # Fixed direction: SOURCE secret is DB14 (target); TARGET secret is DB57 (source).
 d14=connect('PCTP_QC_SOURCE_SQL_CONNECTION');d57=connect('PCTP_QC_TARGET_SQL_CONNECTION')
 try:
  def ident(c):
   x=c.cursor();x.execute("SELECT CONVERT(nvarchar(256),SERVERPROPERTY('ServerName')),DB_NAME()");r=x.fetchone();return {'server':r[0],'database':r[1]}
  i14,i57=ident(d14),ident(d57)
  if (i14['server'].casefold(),i14['database'].casefold())==(i57['server'].casefold(),i57['database'].casefold()): raise RuntimeError('Safety stop: both secrets resolve to the same SQL Server/database.')
  names,excluded=invnames(a.inventory);src=rows(d57,TABLES_SQL);dst=rows(d14,TABLES_SQL);sm=rows(d57,MODULES_SQL);dm=rows(d14,MODULES_SQL)
  group=lambda rr:{k:sorted([x for x in rr if x['table_name'].casefold()==k],key=lambda x:x['column_id']) for k in {x['table_name'].casefold() for x in rr}}
  st,dt=group(src),group(dst);smod={x['object_name'].casefold():x for x in sm};dmod={x['object_name'].casefold():x for x in dm}
  sql=['/* DB57 authoritative source -> DB14 target. Generated for manual review only. */',f"/* DB57 {i57['server']} / {i57['database']} */",f"/* DB14 {i14['server']} / {i14['database']} */",'SET XACT_ABORT ON;','BEGIN TRY','BEGIN TRANSACTION;']
  changes=[];blockers=[];unverified=[]
  for n in names:
   k=n.casefold()
   if k not in st and k not in smod: unverified.append({'object':n,'reason':'Not present in DB57 metadata; no SQL emitted.'});continue
   if k in st and k not in dt:
    sql.append(f"IF OBJECT_ID(N'dbo.{n}',N'U') IS NULL BEGIN CREATE TABLE [dbo].{q(n)} ("+', '.join(colddl(c) for c in st[k])+'); END;')
    changes.append({'object':n,'change':'CREATE TABLE','columns':len(st[k])})
   elif k in st and k in dt:
    existing={x['column_name'].casefold():x for x in dt[k]}
    for c in st[k]:
     ck=c['column_name'].casefold()
     if ck not in existing:
      if not c['is_nullable'] and not c['default_definition'] and not c['is_computed']:
       blockers.append({'object':n,'column':c['column_name'],'reason':'NOT NULL column has no default; unsafe for existing DB14 rows.'});continue
      sql.append(f"IF COL_LENGTH(N'dbo.{n}',N'{c['column_name']}') IS NULL ALTER TABLE [dbo].{q(n)} ADD {colddl(c)};")
      changes.append({'object':n,'change':'ADD COLUMN','column':c['column_name']})
     else:
      old=existing[ck]
      if typ(c).casefold()!=typ(old).casefold() or bool(c['is_nullable'])!=bool(old['is_nullable']) or bool(c['is_identity'])!=bool(old['is_identity']):
       blockers.append({'object':n,'column':c['column_name'],'reason':'Existing type/nullability/identity differs; no ALTER generated.','db57':typ(c),'db14':typ(old)})
   if k in smod:
    s=smod[k]
    if k not in dmod:
     if not s['definition']: blockers.append({'object':n,'reason':'DB57 module definition unavailable; VIEW DEFINITION permission required.'})
     else: sql.append(s['definition'].rstrip().rstrip(';')+';');changes.append({'object':n,'change':'CREATE MODULE','type':s['object_type']})
    elif (s['definition'] or '').strip()!=(dmod[k]['definition'] or '').strip():
     blockers.append({'object':n,'reason':'Existing module definition differs; manual review required.'})
  sql+=['COMMIT TRANSACTION;','END TRY','BEGIN CATCH','IF XACT_STATE()<>0 ROLLBACK TRANSACTION;','THROW;','END CATCH;']
  report={'generated_utc':datetime.now(timezone.utc).isoformat(),'DB57_source':i57,'DB14_target':i14,'candidate_count':len(names),'changes':changes,'blockers':blockers,'unverified':unverified,'excluded':excluded}
  (a.out/'DB14_Migration.sql').write_text('\nGO\n'.join(sql)+'\n',encoding='utf-8-sig')
  (a.out/'DB14_Reconciliation_Report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
  summary=['# DB14 reconciliation',f"- DB57 source: {i57}",f"- DB14 target: {i14}",f"- Candidate objects: {len(names)}",f"- Proposed changes: {len(changes)}",f"- Blockers: {len(blockers)}",f"- Unverified: {len(unverified)}",'','## Safety','- Read-only SQL connections only; CI never applies the generated script.','- No DROP, TRUNCATE, DELETE, UPDATE, INSERT, or business-data copy.','- Existing incompatible columns and module drift are blockers, not auto-repaired.','- Configuration rows are not copied because there is no approved key/value mapping in the inventory.','', '## Blockers']+[f"- {x}" for x in blockers]+['','## Unverified']+[f"- {x}" for x in unverified]
  (a.out/'README.md').write_text('\n'.join(summary)+'\n',encoding='utf-8')
  print(json.dumps({'changes':len(changes),'blockers':len(blockers),'unverified':len(unverified)}))
 finally: d14.close();d57.close()
if __name__=='__main__': main()
