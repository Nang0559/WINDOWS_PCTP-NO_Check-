import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import gen_schema_from_inventory as gen

HEADER = ("SchemaName,TableName,type_desc,column_id,ColumnName,DataType,MaxChars,precision,scale,"
          "is_nullable,is_identity,seed_value,increment_value,is_computed,ComputedDefinition,DefaultDefinition")


class AliasVerifierLengthTests(unittest.TestCase):
    def test_mapped_max_length_follows_mapped_base_type(self):
        self.assertEqual(32, gen.mapped_max_length("nvarchar(16)"))
        self.assertEqual(48, gen.mapped_max_length("NVARCHAR(24)"))
        self.assertEqual(16, gen.mapped_max_length("varchar(16)"))
        self.assertEqual(-1, gen.mapped_max_length("nvarchar(MAX)"))
        self.assertIsNone(gen.mapped_max_length("numeric(15,4)"))
        self.assertIsNone(gen.mapped_max_length("smalldatetime"))

    def test_alias_column_ddl_and_verifier_describe_the_same_length(self):
        # Native nvarchar MaxChars are characters (x2 bytes); alias MaxChars are
        # bytes, and the verified map says CodeType == nvarchar(16) == 32 bytes.
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            inv, cov, tmap = root / "inv.csv", root / "cov.csv", root / "map.json"
            inv.write_text(
                HEADER + "\n"
                "dbo,FVN_Test,USER_TABLE,1,Code,CodeType,32,0,0,0,0,,,0,,\n"
                "dbo,FVN_Test,USER_TABLE,2,Name,nvarchar,64,0,0,0,0,,,0,,\n"
                "dbo,FVN_Test,USER_TABLE,3,Qty,QuantityType,9,15,4,0,0,,,0,,\n",
                encoding="utf-8")
            cov.write_text("object,kind,on_db57_export,class\nFVN_Test,table,có,GAP-no-git-ddl\n", encoding="utf-8")
            tmap.write_text(json.dumps({"codetype": "nvarchar(16)", "quantitytype": "numeric(15,4)"}), encoding="utf-8")
            sql = gen.render(inv, cov, tmap)
        self.assertIn("[Code] nvarchar(16) NOT NULL", sql)
        self.assertIn("(1, N'Code', N'nvarchar', 32, 0, 0,", sql)
        self.assertIn("[Name] nvarchar(64) NOT NULL", sql)
        self.assertIn("(2, N'Name', N'nvarchar', 128, 0, 0,", sql)
        self.assertIn("(3, N'Qty', N'numeric', 9, 15, 4,", sql)


if __name__ == "__main__":
    unittest.main()
