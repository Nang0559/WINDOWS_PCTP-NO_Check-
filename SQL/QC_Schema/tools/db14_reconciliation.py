#!/usr/bin/env python3
"""Read-only DB57 -> DB14 comparison. Writes SQL artifacts only; never executes DDL."""
import os,csv,json,re,argparse
from pathlib import Path
from datetime import datetime,timezone
import pyodbc

ROOT=Path(__file__).resolve().parents[3]
INV=ROOT/'docs'/'PCTP_SQL_Runtime_Inventory_A-K.csv'
TABLES_SQL="""SELECT s.name schema_name,t.name table_name,c.column_id,c.name column_name,ty.name type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,ic.seed_value,ic.increment_value,c.is_computed,cc.definition computed_definition,dc.name default_constraint_name,dc.definition default_definition,c.collation_name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.identity_columns ic ON ic.object_id=t.object_id AND ic.column_id=c.column_id LEFT JOIN sys.computed_columns cc ON cc.object_id=t.object_id AND cc.column_id=c.column_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=t.object_id AND dc.parent_column_id=c.column_id WHERE s.name='dbo' AND t.is_ms_shipped=0 ORDER BY t.name,c.column_id"""
MODULES_SQL="""SELECT o.name object_name,o.type object_type,OBJECT_DEFINITION(o.object_id) definition FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE s.name='dbo' AND o.type IN ('V','P','FN','IF','TF','TR') AND o.is_ms_shipped=0"""
KEYS_SQL="""SELECT t.name table_name,k.name constraint_name,k.type, i.type_desc,i.is_disabled, STRING_AGG(QUOTENAME(c.name)+CASE WHEN ic.is_descending_key=1 THEN ' DESC' ELSE ' ASC' END,',') WITHIN GROUP(ORDER BY ic.key_ordinal) columns_sql FROM sys.key_constraints k JOIN sys.tables t ON t.object_id=k.parent_object_id JOIN sys.indexes i ON i.object_id=t.object_id AND i.index_id=k.unique_index_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0 JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='dbo' GROUP BY t.name,k.name,k.type,i.type_desc,i.is_disabled"""
CHECKS_SQL="""SELECT t.name table_name,cc.name constraint_name,cc.definition,cc.is_disabled,cc.is_not_trusted FROM sys.check_constraints cc JOIN sys.tables t ON t.object_id=cc.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='dbo'"""
INDEXES_SQL="""SELECT t.name table_name,i.name index_name,i.type_desc,i.is_unique,i.is_disabled,i.filter_definition,STRING_AGG(CASE WHEN ic.is_included_column=0 THEN QUOTENAME(c.name)+CASE WHEN ic.is_descending_key=1 THEN ' DESC' ELSE ' ASC' END END,',') WITHIN GROUP(ORDER BY ic.key_ordinal) key_columns,STRING_AGG(CASE WHEN ic.is_included_column=1 THEN QUOTENAME(c.name) END,',') WITHIN GROUP(ORDER BY ic.index_column_id) include_columns FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id LEFT JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id LEFT JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE s.name='dbo' AND i.index_id>0 AND i.is_primary_key=0 AND i.is_unique_constraint=0 GROUP BY t.name,i.name,i.type_desc,i.is_unique,i.is_disabled,i.filter_definition"""
FKS_SQL="""SELECT pt.name parent_table,f.name fk_name,rs.name ref_schema,rt.name ref_table,STRING_AGG(QUOTENAME(pc.name),',') WITHIN GROUP(ORDER BY fkc.constraint_column_id) parent_cols,STRING_AGG(QUOTENAME(rc.name),',') WITHIN GROUP(ORDER BY fkc.constraint_column_id) ref_cols,f.delete_referential_action_desc delete_action,f.update_referential_action_desc update_action,f.is_disabled,f.is_not_trusted FROM sys.foreign_keys f JOIN sys.tables pt ON pt.object_id=f.parent_object_id JOIN sys.schemas ps ON ps.schema_id=pt.schema_id JOIN sys.tables rt ON rt.object_id=f.referenced_object_id JOIN sys.schemas rs ON rs.schema_id=rt.schema_id JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=f.object_id JOIN sys.columns pc ON pc.object_id=pt.object_id AND pc.column_id=fkc.parent_column_id JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fkc.referenced_column_id WHERE ps.name='dbo' GROUP BY pt.name,f.name,rs.name,rt.name,f.delete_referential_action_desc,f.update_referential_action_desc,f.is_disabled,f.is_not_trusted"""
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
 if c['default_definition']: s+=f" CONSTRAINT {q(c['default_constraint_name'])} DEFAULT {c['default_definition']}"
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
def resolve_driver_connection_string(connection_string, available_drivers=None):
 """Use an installed SQL Server ODBC driver without changing the stored GitHub secret."""
 available = list(available_drivers if available_drivers is not None else pyodbc.drivers())
 supported = ('ODBC Driver 18 for SQL Server', 'ODBC Driver 17 for SQL Server')
 selected = next((name for name in supported if name in available), None)
 if not selected:
  raise RuntimeError(
   'No supported SQL Server ODBC driver found. Install 64-bit ODBC Driver 17 or 18. '
   f'Drivers visible to Python: {available}'
  )

 # Rewrite only DRIVER. Preserve server, database, credentials, and encryption options.
 driver_pattern = re.compile(r'(?i)(\\bDRIVER\\s*=\\s*)(?:\\{([^}]*)\\}|([^;]*))')
 if driver_pattern.search(connection_string):
  return driver_pattern.sub(lambda m: m.group(1) + '{' + selected + '}', connection_string, count=1)

 # DSN-based strings resolve their driver through the Windows ODBC data source.
 if re.search(r'(?i)(?:^|;)\\s*DSN\\s*=', connection_string):
  return connection_string

 return 'DRIVER={' + selected + '};' + connection_string


def connect(env):
 s=os.environ.get(env)
 if not s: raise RuntimeError('Missing secret '+env)
 return pyodbc.connect(resolve_driver_connection_string(s),timeout=20,readonly=True)
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
  skeys,dkeys=rows(d57,KEYS_SQL),rows(d14,KEYS_SQL);schecks,dchecks=rows(d57,CHECKS_SQL),rows(d14,CHECKS_SQL)
  sidx,didx=rows(d57,INDEXES_SQL),rows(d14,INDEXES_SQL);sfks,dfks=rows(d57,FKS_SQL),rows(d14,FKS_SQL)
  group=lambda rr:{k:sorted([x for x in rr if x['table_name'].casefold()==k],key=lambda x:x['column_id']) for k in {x['table_name'].casefold() for x in rr}}
  st,dt=group(src),group(dst);smod={x['object_name'].casefold():x for x in sm};dmod={x['object_name'].casefold():x for x in dm}
  sql=['/* DB57 authoritative source -> DB14 target. Generated for manual review only. */',f"/* DB57 {i57['server']} / {i57['database']} */",f"/* DB14 {i14['server']} / {i14['database']} */"]
  changes=[];blockers=[];unverified=[];module_sql=[]
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
      if not c['is_nullable']:
       blockers.append({'object':n,'column':c['column_name'],'reason':'NOT NULL column would initialize or constrain existing DB14 rows; manual migration required.'});continue
      sql.append(f"IF COL_LENGTH(N'dbo.{n}',N'{c['column_name']}') IS NULL ALTER TABLE [dbo].{q(n)} ADD {colddl(c)};")
      changes.append({'object':n,'change':'ADD COLUMN','column':c['column_name']})
     else:
      old=existing[ck]
      if typ(c).casefold()!=typ(old).casefold() or bool(c['is_nullable'])!=bool(old['is_nullable']) or bool(c['is_identity'])!=bool(old['is_identity']) or (c['collation_name'] or '').casefold()!=(old['collation_name'] or '').casefold() or (c['default_definition'] or '').casefold()!=(old['default_definition'] or '').casefold() or (c['computed_definition'] or '').casefold()!=(old['computed_definition'] or '').casefold():
       blockers.append({'object':n,'column':c['column_name'],'reason':'Existing type/nullability/identity differs; no ALTER generated.','db57':typ(c),'db14':typ(old)})
   if k in smod:
    s=smod[k]
    if k not in dmod:
     if not s['definition']: blockers.append({'object':n,'reason':'DB57 module definition unavailable; VIEW DEFINITION permission required.'})
     else: module_sql.append(s['definition'].rstrip().rstrip(';')+';');changes.append({'object':n,'change':'CREATE MODULE','type':s['object_type']})
    elif not s['definition'] or not dmod[k]['definition'] or (s['definition'] or '').strip()!=(dmod[k]['definition'] or '').strip():
     blockers.append({'object':n,'reason':'Existing module definition differs; manual review required.'})
  scoped={x.casefold() for x in names}
  target_tables={x['table_name'].casefold() for x in dst}
  target_tables.update(x.casefold() for x in names if x.casefold() in st)
  # Add missing primary/unique keys. SQL Server validates existing DB14 rows; a duplicate can stop the script.
  dkeymap={x['constraint_name'].casefold():x for x in dkeys}
  for x in skeys:
   t=x['table_name'];n=x['constraint_name']
   if t.casefold() not in scoped: continue
   old=dkeymap.get(n.casefold())
   if old:
    if old['type']!=x['type'] or (old['columns_sql'] or '').casefold()!=(x['columns_sql'] or '').casefold() or bool(old['is_disabled'])!=bool(x['is_disabled']):
     blockers.append({'object':t,'constraint':n,'reason':'Existing PK/UNIQUE definition differs; no destructive replacement generated.'})
    continue
   if x['is_disabled']:
    unverified.append({'object':t,'constraint':n,'reason':'DB57 key index is disabled; not generated.'});continue
   mode='PRIMARY KEY' if x['type']=='PK' else 'UNIQUE'
   sql.append(f"IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name=N'{n.replace(chr(39),chr(39)*2)}') ALTER TABLE [dbo].{q(t)} ADD CONSTRAINT {q(n)} {mode} {x['type_desc']} ({x['columns_sql']});")
   changes.append({'object':t,'change':'ADD '+mode,'constraint':n})
   blockers.append({'object':t,'constraint':n,'reason':'SQL Server will validate existing DB14 rows for duplicate/null key values; inspect data before manual execution.'})
  dcheckmap={x['constraint_name'].casefold():x for x in dchecks}
  for x in schecks:
   t=x['table_name'];n=x['constraint_name']
   if t.casefold() not in scoped: continue
   old=dcheckmap.get(n.casefold())
   if x['is_disabled']:
    unverified.append({'object':t,'constraint':n,'reason':'DB57 CHECK is disabled; not generated.'});continue
   if old:
    if (old['definition'] or '').casefold()!=(x['definition'] or '').casefold() or bool(old['is_disabled'])!=bool(x['is_disabled']) or bool(old['is_not_trusted'])!=bool(x['is_not_trusted']): blockers.append({'object':t,'constraint':n,'reason':'Existing CHECK definition/trust/disabled state differs.'})
    continue
   sql.append(f"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'{n.replace(chr(39),chr(39)*2)}') ALTER TABLE [dbo].{q(t)} WITH NOCHECK ADD CONSTRAINT {q(n)} CHECK {x['definition']};")
   changes.append({'object':t,'change':'ADD CHECK WITH NOCHECK','constraint':n})
   blockers.append({'object':t,'constraint':n,'reason':'CHECK is deliberately untrusted to preserve historical rows; validate manually after reviewing DB14 data.'})
  dfkmap={x['fk_name'].casefold():x for x in dfks}
  for x in sfks:
   t=x['parent_table'];n=x['fk_name']
   if t.casefold() not in scoped: continue
   old=dfkmap.get(n.casefold())
   if x['is_disabled']:
    unverified.append({'object':t,'constraint':n,'reason':'DB57 FK is disabled; not generated.'});continue
   if old:
    sig=('ref_schema','ref_table','parent_cols','ref_cols','delete_action','update_action')
    if any((old[k] or '').casefold()!=(x[k] or '').casefold() for k in sig) or bool(old['is_disabled'])!=bool(x['is_disabled']) or bool(old['is_not_trusted'])!=bool(x['is_not_trusted']): blockers.append({'object':t,'constraint':n,'reason':'Existing FK definition/trust/disabled state differs.'})
    continue
   if x['ref_schema'].casefold()!='dbo' or x['ref_table'].casefold() not in scoped or x['ref_table'].casefold() not in target_tables:
    blockers.append({'object':t,'constraint':n,'reason':'Referenced table is outside the approved runtime inventory or is not present/planned in DB14; FK omitted for manual dependency review.'});continue
   da='' if x['delete_action']=='NO_ACTION' else ' ON DELETE '+x['delete_action'].replace('_',' ')
   ua='' if x['update_action']=='NO_ACTION' else ' ON UPDATE '+x['update_action'].replace('_',' ')
   sql.append(f"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'{n.replace(chr(39),chr(39)*2)}') ALTER TABLE [dbo].{q(t)} WITH NOCHECK ADD CONSTRAINT {q(n)} FOREIGN KEY ({x['parent_cols']}) REFERENCES [dbo].{q(x['ref_table'])} ({x['ref_cols']}){da}{ua};")
   changes.append({'object':t,'change':'ADD FK WITH NOCHECK','constraint':n})
   blockers.append({'object':t,'constraint':n,'reason':'FK is deliberately untrusted to avoid validating existing DB14 rows; validate manually.'})
  didxmap={x['index_name'].casefold():x for x in didx}
  for x in sidx:
   t=x['table_name'];n=x['index_name']
   if t.casefold() not in scoped: continue
   old=didxmap.get(n.casefold())
   if old:
    sig=('type_desc','key_columns','include_columns','filter_definition')
    if any((old[k] or '').casefold()!=(x[k] or '').casefold() for k in sig) or bool(old['is_unique'])!=bool(x['is_unique']):
     blockers.append({'object':t,'index':n,'reason':'Existing index definition differs; no DROP/RECREATE generated.'})
    continue
   if x['is_disabled'] or not x['key_columns'] or x['type_desc'] not in ('CLUSTERED','NONCLUSTERED'): unverified.append({'object':t,'index':n,'reason':'Disabled, non-rowstore, or unscriptable index; manual scripting required.'});continue
   unique='UNIQUE ' if x['is_unique'] else ''
   include=f" INCLUDE ({x['include_columns']})" if x['include_columns'] else ''
   where=f" WHERE {x['filter_definition']}" if x['filter_definition'] else ''
   sql.append(f"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.{t}') AND name=N'{n.replace(chr(39),chr(39)*2)}') CREATE {unique}{x['type_desc']} INDEX {q(n)} ON [dbo].{q(t)} ({x['key_columns']}){include}{where};")
   changes.append({'object':t,'change':'CREATE INDEX','index':n})
  # Create views/procedures/functions/triggers after their in-scope tables and constraints.
  sql.extend(module_sql)
  # GO is required between module definitions and other statements. Every statement is idempotent; no transaction spans batches.

  report={'generated_utc':datetime.now(timezone.utc).isoformat(),'DB57_source':i57,'DB14_target':i14,'candidate_count':len(names),'changes':changes,'blockers':blockers,'unverified':unverified,'excluded':excluded}
  (a.out/'DB14_Migration.sql').write_text('\nGO\n'.join(sql)+'\n',encoding='utf-8-sig')
  (a.out/'DB14_Reconciliation_Report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
  summary=['# DB14 reconciliation',f"- DB57 source: {i57}",f"- DB14 target: {i14}",f"- Candidate objects: {len(names)}",f"- Proposed changes: {len(changes)}",f"- Blockers: {len(blockers)}",f"- Unverified: {len(unverified)}",'','## Safety','- Read-only SQL connections only; CI never applies the generated script.','- No DROP, TRUNCATE, DELETE, UPDATE, INSERT, or business-data copy.','- Existing incompatible columns and module drift are blockers, not auto-repaired.','- Configuration rows are not copied because there is no approved key/value mapping in the inventory.','', '## Blockers']+[f"- {x}" for x in blockers]+['','## Unverified']+[f"- {x}" for x in unverified]
  (a.out/'README.md').write_text('\n'.join(summary)+'\n',encoding='utf-8')
  print(json.dumps({'changes':len(changes),'blockers':len(blockers),'unverified':len(unverified)}))
 finally: d14.close();d57.close()
if __name__=='__main__': main()
