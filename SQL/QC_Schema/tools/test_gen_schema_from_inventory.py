import csv
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import gen_schema_from_inventory as gen


WIDTHS = [128, 60, 11, 128, 128, 11, 10, 6, 12, 12, 128, 128, 12, 128, 128]
HEADERS = ["TableName", "type_desc", "column_id", "ColumnName", "DataType", "MaxChars",
           "precision", "scale", "is_nullable", "is_identity", "seed_value",
           "increment_value", "is_computed", "ComputedDefinition", "DefaultDefinition"]


def fixed_row(values):
    return "".join(str(value).ljust(width) for value, width in zip(values, WIDTHS)).rstrip()


class Sql2DdlGeneratorTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.inventory = self.root / "SQL2.txt"
        self.coverage = self.root / "coverage.csv"
        self.inventory.write_text(
            fixed_row(HEADERS) + "\n" +
            "-" * sum(WIDTHS) + "\n" +
            fixed_row(["FVN_Test", "USER_TABLE", 1, "Id", "int", 4, 10, 0, 0, 1, 1, 1, 0, "NULL", "NULL"]) + "\n" +
            fixed_row(["FVN_Test", "USER_TABLE", 2, "Code", "nvarchar", 40, 0, 0, 0, 0, "NULL", "NULL", 0, "NULL", "('x')"]) + "\n" +
            fixed_row(["FVN_Test", "USER_TABLE", 3, "Calculated", "int", 4, 10, 0, 1, 0, "NULL", "NULL", 1, "([Id] + (1))", "NULL"]) + "\n",
            encoding="utf-8")
        with self.coverage.open("w", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=["object", "kind", "on_db57_export", "class"])
            writer.writeheader()
            writer.writerow({"object": "FVN_Test", "kind": "table", "on_db57_export": "có", "class": "GAP-no-git-ddl"})

    def tearDown(self):
        self.tmp.cleanup()

    def test_preserves_identity_nvarchar_byte_length_default_and_computed(self):
        sql = gen.render(self.inventory, self.coverage)
        self.assertIn("[Id] int IDENTITY(1,1) NOT NULL", sql)
        self.assertIn("[Code] nvarchar(20) NOT NULL DEFAULT ('x')", sql)
        self.assertIn("[Calculated] AS ([Id] + (1))", sql)
        self.assertIn("PK/index/FK/CHECK DDL intentionally omitted", sql)

    def test_alias_type_fails_closed_without_verified_mapping(self):
        source = self.inventory.read_text(encoding="utf-8")
        source = source.replace("nvarchar", "LongCodeType").replace("40".ljust(11), "40".ljust(11), 1)
        self.inventory.write_text(source, encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "verified --type-map"):
            gen.render(self.inventory, self.coverage)

    def test_allowlist_rejects_table_missing_from_metadata(self):
        self.coverage.write_text(
            "object,kind,on_db57_export,class\nMissingTable,table,có,GAP-no-git-ddl\n",
            encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "absent from SQL2"):
            gen.render(self.inventory, self.coverage)


if __name__ == "__main__":
    unittest.main()
