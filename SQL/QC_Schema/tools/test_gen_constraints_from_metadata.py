import csv
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import gen_constraints_from_metadata as gen

def fixed_section(headers, rows, widths=None):
    widths = widths or [max(len(h), 18) for h in headers]
    starts=[]; pos=0
    for width in widths:
        starts.append(pos); pos += width + 1
    header="".join(h.ljust(widths[i])+" " for i,h in enumerate(headers)).rstrip()
    sep="".join("-"*widths[i]+" " for i in range(len(headers))).rstrip()
    rendered=[]
    for row in rows:
        rendered.append("".join(str(row.get(h,"")).ljust(widths[i])+" " for i,h in enumerate(headers)).rstrip())
    return "\n".join([header,sep,*rendered,""])

class ConstraintGeneratorTests(unittest.TestCase):
    def fixture(self, root, disabled_index=False):
        metadata=root/"metadata.txt"
        index_headers=["SchemaName","TableName","IndexName","IndexType","IsPrimaryKey","IsUnique","IsUniqueConstraint","IsDisabled","HasFilter","FilterDefinition","KeyOrdinal","IsIncludedColumn","IsDescendingKey","ColumnName"]
        iw=[16,16,34,18,14,10,20,12,10,40,12,18,18,20]
        def ix(ord_,col,desc="0",included="0"):
            return {"SchemaName":"dbo","TableName":"A","IndexName":"IX_A_AB","IndexType":"NONCLUSTERED","IsPrimaryKey":"0","IsUnique":"0","IsUniqueConstraint":"0","IsDisabled":"1" if disabled_index else "0","HasFilter":"0","FilterDefinition":"NULL","KeyOrdinal":str(ord_),"IsIncludedColumn":included,"IsDescendingKey":desc,"ColumnName":col}
        indexes=fixed_section(index_headers,[ix(1,"A1"),ix(2,"A2","1"),ix(0,"Extra","0","1")],iw)
        fk_headers=["ParentSchema","ParentTable","ParentColumn","ReferencedSchema","ReferencedTable","ReferencedColumn","ForeignKeyName","ColumnOrdinal","OnDelete","OnUpdate","IsDisabled","IsNotTrusted"]
        fw=[18,18,20,20,20,20,30,14,18,18,12,14]
        fks=fixed_section(fk_headers,[
          dict(ParentSchema="dbo",ParentTable="A",ParentColumn="BId",ReferencedSchema="dbo",ReferencedTable="B",ReferencedColumn="Id",ForeignKeyName="FK_A_B",ColumnOrdinal="1",OnDelete="NO_ACTION",OnUpdate="NO_ACTION",IsDisabled="0",IsNotTrusted="0"),
          dict(ParentSchema="dbo",ParentTable="A",ParentColumn="ExternalId",ReferencedSchema="dbo",ReferencedTable="Outside",ReferencedColumn="Id",ForeignKeyName="FK_A_Outside",ColumnOrdinal="1",OnDelete="NO_ACTION",OnUpdate="NO_ACTION",IsDisabled="0",IsNotTrusted="0"),
        ],fw)
        check_headers=["SchemaName","TableName","ConstraintName","Definition","IsDisabled","IsNotTrusted","IsNotForReplication"]
        cw=[18,18,38,70,12,14,22]
        checks=fixed_section(check_headers,[dict(SchemaName="dbo",TableName="A",ConstraintName="CK_A_Positive",Definition="([Qty] >= (0))",IsDisabled="0",IsNotTrusted="0",IsNotForReplication="0")],cw)
        default_headers=["SchemaName","TableName","ColumnName","ConstraintName","Definition"]
        dw=[18,18,22,34,50]
        defaults=fixed_section(default_headers,[dict(SchemaName="dbo",TableName="A",ColumnName="Qty",ConstraintName="DF_A_Qty",Definition="((0))")],dw)
        module_headers=["SchemaName","ObjectName","ObjectType","ObjectTypeDescription","SqlDefinition"]
        mw=[18,36,18,36,80]
        modules=fixed_section(module_headers,[dict(SchemaName="dbo",ObjectName="usp_A",ObjectType="P",ObjectTypeDescription="SQL_STORED_PROCEDURE",SqlDefinition="CREATE PROCEDURE dbo.usp_A AS SELECT 1")],mw)
        metadata.write_text(indexes+fks+checks+defaults+modules,encoding="utf-8")
        coverage=root/"coverage.csv"
        with coverage.open("w",encoding="utf-8",newline="") as stream:
            writer=csv.DictWriter(stream,fieldnames=["object","kind","on_db57_export","git_ddl_files","class"])
            writer.writeheader()
            writer.writerow({"object":"A","kind":"table","on_db57_export":"yes","git_ddl_files":"","class":"GAP-no-git-ddl"})
            writer.writerow({"object":"B","kind":"table","on_db57_export":"yes","git_ddl_files":"","class":"GAP-no-git-ddl"})
        return metadata,coverage

    def test_composite_index_order_desc_include_and_fk_scope(self):
        with tempfile.TemporaryDirectory() as d:
            metadata,coverage=self.fixture(Path(d))
            sql,report=gen.render(metadata,coverage)
            self.assertIn("([A1] ASC, [A2] DESC) INCLUDE ([Extra])",sql)
            self.assertIn("FOREIGN KEY ([BId]) REFERENCES [dbo].[B] ([Id])",sql)
            self.assertNotIn("FOREIGN KEY ([ExternalId])",sql)
            self.assertEqual(report["foreign_keys_external_or_skipped"][0]["name"],"FK_A_Outside")
            self.assertEqual(report["defaults_inventory"][0]["name"],"DF_A_Qty")
            self.assertEqual(report["modules_inventory"][0]["name"],"usp_A")
            self.assertIn("CK_A_Positive",sql)

    def test_disabled_index_fails_closed(self):
        with tempfile.TemporaryDirectory() as d:
            metadata,coverage=self.fixture(Path(d),disabled_index=True)
            with self.assertRaisesRegex(ValueError,"Disabled index"):
                gen.render(metadata,coverage)

    def test_identifier_escaping_and_truth_values(self):
        self.assertEqual(gen.ident("a]b"),"[a]]b]")
        self.assertTrue(gen.truth("1"))
        self.assertTrue(gen.truth("true"))
        self.assertFalse(gen.truth("0"))

if __name__=="__main__": unittest.main()
