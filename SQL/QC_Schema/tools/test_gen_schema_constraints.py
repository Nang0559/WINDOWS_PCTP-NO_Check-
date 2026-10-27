import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import gen_schema_constraints as gen


INDEX_FIELDS = ["SchemaName", "TableName", "IndexName", "IndexType", "IsPrimaryKey",
                "IsUnique", "IsUniqueConstraint", "IsDisabled", "HasFilter", "FilterDefinition",
                "KeyOrdinal", "IsIncludedColumn", "IsDescendingKey", "ColumnName"]
FK_FIELDS = ["ParentSchema", "ParentTable", "ParentColumn", "ReferencedSchema",
             "ReferencedTable", "ReferencedColumn", "ForeignKeyName", "ColumnOrdinal",
             "OnDelete", "OnUpdate", "IsDisabled", "IsNotTrusted"]
CHECK_FIELDS = ["SchemaName", "TableName", "ConstraintName", "Definition",
                "IsDisabled", "IsNotTrusted", "IsNotForReplication"]


def fixed_section(fields, rows):
    widths = [max(len(f), max((len(str(row.get(f, ""))) for row in rows), default=0)) + 2 for f in fields]
    header = "".join(f.ljust(w) for f, w in zip(fields, widths)).rstrip()
    separator = "".join("-" * w for w in widths)
    body = ["".join(str(row.get(f, "")).ljust(w) for f, w in zip(fields, widths)).rstrip() for row in rows]
    return "\n".join([header, separator, *body, ""])


def index(table, name, col, ordinal, **kw):
    row = {"SchemaName": "dbo", "TableName": table, "IndexName": name,
           "IndexType": "NONCLUSTERED", "IsPrimaryKey": "0", "IsUnique": "0",
           "IsUniqueConstraint": "0", "IsDisabled": "0", "HasFilter": "0",
           "FilterDefinition": "NULL", "KeyOrdinal": str(ordinal),
           "IsIncludedColumn": "0", "IsDescendingKey": "0", "ColumnName": col}
    row.update(kw)
    return row


class SchemaConstraintGeneratorTests(unittest.TestCase):
    def test_composite_index_preserves_key_order_include_and_filter(self):
        rows = [
            index("Parent", "IX_Parent_A_B", "B", 2, HasFilter="1", FilterDefinition="([B]>(0))"),
            index("Parent", "IX_Parent_A_B", "A", 1, HasFilter="1", FilterDefinition="([B]>(0))"),
            index("Parent", "IX_Parent_A_B", "Payload", 0, IsIncludedColumn="1",
                  HasFilter="1", FilterDefinition="([B]>(0))"),
        ]
        metadata = fixed_section(INDEX_FIELDS, rows) + "\n" + fixed_section(FK_FIELDS, []) + "\n" + fixed_section(CHECK_FIELDS, [])
        sql, report, counts = gen.generate(metadata, {("dbo", "parent")})
        self.assertIn("([A] ASC, [B] ASC) INCLUDE ([Payload]) WHERE ([B]>(0))", sql)
        self.assertEqual(counts["indexes"], 1)

    def test_primary_key_and_unique_constraint_are_constraints_not_indexes(self):
        rows = [
            index("Parent", "PK_Parent", "Id", 1, IsPrimaryKey="1", IsUnique="1", IndexType="CLUSTERED"),
            index("Parent", "UQ_Parent_Code", "Code", 1, IsUnique="1", IsUniqueConstraint="1"),
        ]
        metadata = fixed_section(INDEX_FIELDS, rows) + "\n" + fixed_section(FK_FIELDS, []) + "\n" + fixed_section(CHECK_FIELDS, [])
        sql, _, counts = gen.generate(metadata, {("dbo", "parent")})
        self.assertIn("ADD CONSTRAINT [PK_Parent] PRIMARY KEY CLUSTERED ([Id] ASC)", sql)
        self.assertIn("ADD CONSTRAINT [UQ_Parent_Code] UNIQUE NONCLUSTERED ([Code] ASC)", sql)
        self.assertEqual(counts["primary_keys"], 1)
        self.assertEqual(counts["unique_constraints"], 1)

    def test_composite_foreign_key_and_check_are_emitted(self):
        indexes = [index("Parent", "PK_Parent", "A", 1, IsPrimaryKey="1", IsUnique="1")]
        fks = [
            {"ParentSchema":"dbo","ParentTable":"Child","ParentColumn":"A","ReferencedSchema":"dbo",
             "ReferencedTable":"Parent","ReferencedColumn":"A","ForeignKeyName":"FK_Child_Parent",
             "ColumnOrdinal":"1","OnDelete":"NO_ACTION","OnUpdate":"NO_ACTION","IsDisabled":"0","IsNotTrusted":"0"},
            {"ParentSchema":"dbo","ParentTable":"Child","ParentColumn":"B","ReferencedSchema":"dbo",
             "ReferencedTable":"Parent","ReferencedColumn":"B","ForeignKeyName":"FK_Child_Parent",
             "ColumnOrdinal":"2","OnDelete":"NO_ACTION","OnUpdate":"NO_ACTION","IsDisabled":"0","IsNotTrusted":"0"},
        ]
        checks = [{"SchemaName":"dbo","TableName":"Child","ConstraintName":"CK_Child_Qty",
                   "Definition":"([Qty]>=(0))","IsDisabled":"0","IsNotTrusted":"0","IsNotForReplication":"0"}]
        metadata = fixed_section(INDEX_FIELDS, indexes) + "\n" + fixed_section(FK_FIELDS, fks) + "\n" + fixed_section(CHECK_FIELDS, checks)
        sql, _, counts = gen.generate(metadata, {("dbo","parent"),("dbo","child")})
        self.assertIn("FOREIGN KEY ([A], [B])", sql)
        self.assertIn("REFERENCES [dbo].[Parent] ([A], [B])", sql)
        self.assertIn("CHECK ([Qty]>=(0))", sql)
        self.assertEqual(counts["foreign_keys"], 1)
        self.assertEqual(counts["checks"], 1)
        # Post-deploy checks must compare definitions and ordered columns,
        # not merely confirm that an object with the same name exists.
        self.assertIn("Index columns differ", sql)
        self.assertIn("FK columns differ", sql)
        self.assertIn("CHECK definition/properties differ", sql)
        self.assertIn("definition=N'([Qty]>=(0))'", sql)

    def test_external_fk_is_reported_and_not_generated(self):
        fk = {"ParentSchema":"dbo","ParentTable":"Child","ParentColumn":"Status","ReferencedSchema":"dbo",
              "ReferencedTable":"WorkflowStatuses","ReferencedColumn":"StatusCode","ForeignKeyName":"FK_Child_Status",
              "ColumnOrdinal":"1","OnDelete":"NO_ACTION","OnUpdate":"NO_ACTION","IsDisabled":"0","IsNotTrusted":"0"}
        metadata = fixed_section(INDEX_FIELDS, []) + "\n" + fixed_section(FK_FIELDS, [fk]) + "\n" + fixed_section(CHECK_FIELDS, [])
        sql, report, counts = gen.generate(metadata, {("dbo","child")})
        self.assertNotIn("FK_Child_Status", sql)
        self.assertIn("outside the generated allowlist", report)
        self.assertEqual(counts["foreign_keys"], 0)

    def test_scope_remains_stable_after_coverage_refresh(self):
        with tempfile.TemporaryDirectory() as tmp:
            coverage = Path(tmp) / "coverage.csv"
            coverage.write_text(
                "object,kind,on_db57_export,git_ddl_files,class\n"
                "Parent,table,có,SQL/QC_Schema/02_Generated_From_DB57.sql,OK-testable\n"
                "Unrelated,table,có,SQL/Other.sql,OK-testable\n",
                encoding="utf-8")
            self.assertEqual(gen.load_scope(coverage), {("dbo", "parent")})

    def test_full_database_baseline_is_not_in_scope(self):
        with tempfile.TemporaryDirectory() as tmp:
            coverage = Path(tmp) / "coverage.csv"
            coverage.write_text(
                "object,kind,on_db57_export,git_ddl_files,class,in_runtime_inventory\n"
                "UnrelatedDb57Table,table,có,,GAP-db57-baseline,không\n",
                encoding="utf-8")
            self.assertEqual(gen.load_scope(coverage), set())

    def test_default_constraints_are_not_duplicated(self):
        metadata = fixed_section(INDEX_FIELDS, []) + "\n" + fixed_section(FK_FIELDS, []) + "\n" + fixed_section(CHECK_FIELDS, [])
        sql, report, _ = gen.generate(metadata, {("dbo","parent")})
        self.assertNotIn(" DEFAULT ", sql)
        self.assertIn("DEFAULT constraints are represented inline", report)


if __name__ == "__main__":
    unittest.main()
