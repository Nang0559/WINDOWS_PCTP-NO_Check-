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

    def test_load_columns_can_filter_to_pctp_runtime_scope(self):
        tables = gen.load_columns(self.inventory, {"fvn_test", "unrelated"})
        self.assertEqual({"fvn_test"}, set(tables))
        self.assertEqual(["Id", "Code", "Calculated"],
                         [row["ColumnName"] for row in tables["fvn_test"]])

    def test_preserves_identity_unicode_byte_length_default_and_computed(self):
        sql = gen.render(self.inventory, self.coverage)
        self.assertIn("[Id] int IDENTITY(1,1) NOT NULL", sql)
        self.assertIn("[Code] nvarchar(20) NOT NULL DEFAULT ('x')", sql)
        self.assertIn("N'Code', N'nvarchar', 40", sql)
        self.assertIn("[Calculated] AS ([Id] + (1))", sql)
        self.assertIn("SQL2 table/column definition mismatch", sql)
        self.assertIn("sys.identity_columns", sql)
        self.assertIn("ROW_NUMBER() OVER (ORDER BY c.column_id)", sql)
        self.assertIn("PK/unique indexes, FKs and CHECK constraints are emitted separately", sql)

    def test_native_unicode_lengths_are_bytes_but_alias_lengths_use_verified_map(self):
        nvarchar_row = {"TableName": "FVN_Test", "ColumnName": "KETQUA",
                        "DataType": "nvarchar", "MaxChars": "10"}
        nchar_row = {"TableName": "FVN_Test", "ColumnName": "Gear",
                     "DataType": "nchar", "MaxChars": "2"}
        alias_row = {"TableName": "FVN_Test", "ColumnName": "Code",
                     "DataType": "CodeType", "MaxChars": "32"}
        self.assertEqual("nvarchar(5)", gen.sql_type(nvarchar_row, {}))
        self.assertEqual("nchar(1)", gen.sql_type(nchar_row, {}))
        self.assertEqual("nvarchar(16)", gen.sql_type(alias_row, {"codetype": "nvarchar(16)"}))

    def test_alias_type_fails_closed_without_verified_mapping(self):
        source = self.inventory.read_text(encoding="utf-8")
        source = source.replace("nvarchar".ljust(128), "LongCodeType".ljust(128)).replace("40".ljust(11), "40".ljust(11), 1)
        self.inventory.write_text(source, encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "verified --type-map"):
            gen.render(self.inventory, self.coverage)

    def test_reads_csv_column_inventory_and_preserves_quoted_comma_expressions(self):
        csv_inventory = self.root / "SQL2_ColumnInventory.csv"
        fields = ["SchemaName", "TableName", "type_desc", "column_id", "ColumnName",
                  "DataType", "MaxChars", "precision", "scale", "is_nullable",
                  "is_identity", "seed_value", "increment_value", "is_computed",
                  "ComputedDefinition", "DefaultDefinition"]
        rows = [
            {"SchemaName": "dbo", "TableName": "FVN_Test", "type_desc": "USER_TABLE",
             "column_id": "1", "ColumnName": "Id", "DataType": "int", "MaxChars": "4",
             "precision": "10", "scale": "0", "is_nullable": "0", "is_identity": "1",
             "seed_value": "1", "increment_value": "1", "is_computed": "0",
             "ComputedDefinition": "", "DefaultDefinition": ""},
            {"SchemaName": "dbo", "TableName": "FVN_Test", "type_desc": "USER_TABLE",
             "column_id": "2", "ColumnName": "Amount", "DataType": "numeric",
             "MaxChars": "9", "precision": "15", "scale": "4", "is_nullable": "1",
             "is_identity": "0", "seed_value": "", "increment_value": "",
             "is_computed": "1", "ComputedDefinition": "(CONVERT(numeric(15,4), [Id] * 1.0))",
             "DefaultDefinition": ""},
        ]
        with csv_inventory.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=fields)
            writer.writeheader()
            writer.writerows(rows)
        sql = gen.render(csv_inventory, self.coverage)
        self.assertIn("[Id] int IDENTITY(1,1) NOT NULL", sql)
        self.assertIn("[Amount] AS (CONVERT(numeric(15,4), [Id] * 1.0))", sql)

    def test_reads_semicolon_delimited_inventory_export(self):
        csv_inventory = self.root / "SQL2_ColumnInventory.csv"
        fields = ["SchemaName", "TableName", "type_desc", "column_id", "ColumnName",
                  "DataType", "MaxChars", "precision", "scale", "is_nullable",
                  "is_identity", "seed_value", "increment_value", "is_computed",
                  "ComputedDefinition", "DefaultDefinition"]
        row = {"SchemaName": "dbo", "TableName": "FVN_Test", "type_desc": "USER_TABLE",
               "column_id": "1", "ColumnName": "Id", "DataType": "int", "MaxChars": "4",
               "precision": "10", "scale": "0", "is_nullable": "0", "is_identity": "0",
               "seed_value": "", "increment_value": "", "is_computed": "0",
               "ComputedDefinition": "", "DefaultDefinition": ""}
        with csv_inventory.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=fields, delimiter=";")
            writer.writeheader()
            writer.writerow(row)
        sql = gen.render(csv_inventory, self.coverage)
        self.assertIn("[Id] int NOT NULL", sql)

    def test_reads_fixed_width_inventory_saved_with_csv_extension(self):
        inventory = self.root / "SQL2_ColumnInventory.csv"
        fields = ["SchemaName", "TableName", "type_desc", "column_id", "ColumnName",
                  "DataType", "MaxChars", "precision", "scale", "is_nullable",
                  "is_identity", "seed_value", "increment_value", "is_computed",
                  "ComputedDefinition", "DefaultDefinition"]
        widths = [16, 16, 16, 12, 16, 16, 12, 12, 8, 12, 12, 12, 16, 12, 28, 28]
        header = "".join(name.ljust(width) for name, width in zip(fields, widths)).rstrip()
        values = ["dbo", "FVN_Test", "USER_TABLE", "1", "Id", "int", "4", "10", "0",
                  "0", "0", "NULL", "NULL", "0", "NULL", "NULL"]
        row = "".join(str(value).ljust(width) for value, width in zip(values, widths)).rstrip()
        inventory.write_text(header + "\n" + "-" * sum(widths) + "\n" + row + "\n",
                             encoding="utf-8")
        sql = gen.render(inventory, self.coverage)
        self.assertIn("[Id] int NOT NULL", sql)

    def test_csv_inventory_rejects_non_dbo_only_table(self):
        csv_inventory = self.root / "SQL2_ColumnInventory.csv"
        fields = ["SchemaName", "TableName", "type_desc", "column_id", "ColumnName",
                  "DataType", "MaxChars", "precision", "scale", "is_nullable",
                  "is_identity", "seed_value", "increment_value", "is_computed",
                  "ComputedDefinition", "DefaultDefinition"]
        with csv_inventory.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=fields)
            writer.writeheader()
            writer.writerow({"SchemaName": "other", "TableName": "FVN_Test",
                             "type_desc": "USER_TABLE", "column_id": "1", "ColumnName": "Id",
                             "DataType": "int", "MaxChars": "4", "precision": "10", "scale": "0",
                             "is_nullable": "0", "is_identity": "0", "is_computed": "0"})
        with self.assertRaisesRegex(ValueError, "absent from SQL2"):
            gen.render(csv_inventory, self.coverage)

    def test_allowlist_remains_scoped_after_coverage_refresh(self):
        self.coverage.write_text(
            "object,kind,on_db57_export,git_ddl_files,class\n"
            "FVN_Test,table,có,SQL/QC_Schema/02_Generated_From_DB57.sql,OK-testable\n",
            encoding="utf-8")
        sql = gen.render(self.inventory, self.coverage)
        self.assertIn("CREATE TABLE dbo.[FVN_Test]", sql)
        self.assertNotIn("CREATE TABLE dbo.[OtherTable]", sql)

    def test_full_database_baseline_class_is_not_included(self):
        self.coverage.write_text(
            "object,kind,on_db57_export,git_ddl_files,class,in_runtime_inventory\n"
            "UnrelatedDb57Table,table,có,,GAP-db57-baseline,không\n",
            encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "allow-list is empty"):
            gen.render(self.inventory, self.coverage)

    def test_explicit_pctp_runtime_inventory_is_included(self):
        self.coverage.write_text(
            "object,kind,on_db57_export,git_ddl_files,class,in_runtime_inventory\n"
            "FVN_Test,table,có,,OK-testable,có\n",
            encoding="utf-8")
        sql = gen.render(self.inventory, self.coverage)
        self.assertIn("CREATE TABLE dbo.[FVN_Test]", sql)

    def test_allowlist_rejects_table_missing_from_metadata(self):
        self.coverage.write_text(
            "object,kind,on_db57_export,class\nMissingTable,table,có,GAP-no-git-ddl\n",
            encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "absent from SQL2"):
            gen.render(self.inventory, self.coverage)


if __name__ == "__main__":
    unittest.main()
