import csv
import importlib.util
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).with_name("db14_reconciliation.py")
spec = importlib.util.spec_from_file_location("db14_reconciliation", MODULE_PATH)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

class ReconciliationGeneratorTests(unittest.TestCase):
    def test_sql_type_preserves_unicode_length(self):
        self.assertEqual(mod.typ({"type_name":"nvarchar","max_length":100}), "nvarchar(50)")

    def test_sql_type_preserves_decimal_precision(self):
        self.assertEqual(mod.typ({"type_name":"decimal","precision":18,"scale":4}), "decimal(18,4)")

    def test_column_definition_preserves_identity_and_named_default(self):
        col = {
            "column_name":"RowId","type_name":"bigint","max_length":8,"precision":19,"scale":0,
            "is_computed":False,"collation_name":None,"is_identity":True,
            "seed_value":1,"increment_value":1,"is_nullable":False,
            "default_definition":"((0))","default_constraint_name":"DF_Test_RowId"
        }
        self.assertEqual(
            mod.colddl(col),
            "[RowId] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [DF_Test_RowId] DEFAULT ((0))"
        )

    def test_metadata_queries_do_not_select_raw_sql_variant_columns(self):
        # sys.identity_columns.seed_value/increment_value are sql_variant (ODBC type -150),
        # which pyodbc cannot fetch ("ODBC SQL type -150 is not yet supported").
        self.assertNotIn(",ic.seed_value,", mod.TABLES_SQL)
        self.assertIn("CONVERT(bigint,ic.seed_value) seed_value", mod.TABLES_SQL)
        self.assertIn("CONVERT(bigint,ic.increment_value) increment_value", mod.TABLES_SQL)

    def test_numeric_odbc_boolean_options_become_yes_no_for_driver_17(self):
        actual = mod.normalize_odbc_boolean_options(
            "DRIVER={ODBC Driver 17 for SQL Server};SERVER=db;DATABASE=PCTP;Encrypt=0;TrustServerCertificate = 1;UID=u;PWD=p"
        )
        self.assertEqual(
            actual,
            "DRIVER={ODBC Driver 17 for SQL Server};SERVER=db;DATABASE=PCTP;Encrypt=no;TrustServerCertificate=yes;UID=u;PWD=p"
        )

    def test_odbc_boolean_normalization_leaves_other_values_alone(self):
        s = "SERVER=db;Encrypt=yes;PWD=pass0;ApplicationIntent=ReadOnly;TrustServerCertificate=strict"
        self.assertEqual(mod.normalize_odbc_boolean_options(s), s)

    def test_ado_net_keywords_are_mapped_to_odbc(self):
        ado = "Data Source=192.168.200.14;Initial Catalog=B7R2;User ID=pctp_ro;Password=p;w=1;Integrated Security=False;Connect Timeout=30;Persist Security Info=True;Encrypt=0"
        actual = mod.normalize_connection_string_keywords(ado)
        self.assertEqual(
            actual,
            "SERVER=192.168.200.14;DATABASE=B7R2;UID=pctp_ro;PWD=p;w=1;Trusted_Connection=no;Encrypt=0"
        )

    def test_ado_integrated_security_sspi_becomes_trusted_connection(self):
        actual = mod.normalize_connection_string_keywords("Server=db;Database=x;Integrated Security=SSPI")
        self.assertEqual(actual, "SERVER=db;DATABASE=x;Trusted_Connection=yes")

    def test_quoted_and_braced_passwords_with_semicolons_survive(self):
        for pwd_part in ('Password="a;b}c"', "Password='a;b}c'", "PWD={a;b}}c}"):
            actual = mod.normalize_connection_string_keywords("Server=db;User ID=u;" + pwd_part)
            self.assertEqual(actual, "SERVER=db;UID=u;PWD={a;b}}c}", pwd_part)

    def test_driver_resolution_still_works_after_keyword_normalization(self):
        norm = mod.normalize_connection_string_keywords("Driver={ODBC Driver 18 for SQL Server};Server=db;Uid=u;Pwd=p")
        actual = mod.resolve_driver_connection_string(norm, ["ODBC Driver 17 for SQL Server"])
        self.assertEqual(actual, "DRIVER={ODBC Driver 17 for SQL Server};SERVER=db;UID=u;PWD=p")

    def test_diagnostic_never_prints_credentials_or_hosts(self):
        text = mod.describe_connection_string("Server=secret-host;User ID=;Password=hunter2;Encrypt=0")
        self.assertNotIn("hunter2", text)
        self.assertNotIn("secret-host", text)
        self.assertIn("UID=<EMPTY>", text)
        self.assertIn("PWD=<set>", text)

    def test_metadata_queries_work_on_sql_server_before_2017(self):
        # STRING_AGG is SQL Server 2017+; the live DB57/DB14 reported "'STRING_AGG' is not a recognized built-in function name".
        for name in ("TABLES_SQL", "MODULES_SQL", "KEYS_SQL", "CHECKS_SQL", "INDEXES_SQL", "FKS_SQL"):
            sql = getattr(mod, name)
            self.assertNotIn("STRING_AGG", sql.upper(), name)
        for name in ("KEYS_SQL", "INDEXES_SQL", "FKS_SQL"):
            sql = getattr(mod, name)
            self.assertIn("FOR XML PATH('')", sql, name)
            self.assertEqual(sql.count("("), sql.count(")"), name + " has unbalanced parentheses")

    def test_aggregated_column_lists_are_ordered_and_scoped(self):
        self.assertIn("ORDER BY ic.key_ordinal FOR XML PATH('')", mod.KEYS_SQL)
        self.assertIn("ic.is_included_column=1 ORDER BY ic.index_column_id", mod.INDEXES_SQL)
        self.assertIn("ORDER BY fkc.constraint_column_id FOR XML PATH('')", mod.FKS_SQL)
        self.assertNotIn("GROUP BY", mod.KEYS_SQL + mod.INDEXES_SQL + mod.FKS_SQL)

    def test_to_alter_converts_first_real_create_and_ignores_comments(self):
        d = "-- create view old\n/* CREATE PROCEDURE nope */\nCREATE   PROC dbo.P AS SELECT 1"
        self.assertEqual(mod.to_alter(d), "-- create view old\n/* CREATE PROCEDURE nope */\nALTER   PROC dbo.P AS SELECT 1")
        self.assertIsNone(mod.to_alter("SELECT 1"))

    def test_normdef_ignores_line_endings_and_trailing_spaces(self):
        self.assertEqual(mod.normdef("A  \r\nB\r\n"), mod.normdef("A\nB"))

    def test_modules_are_ordered_functions_views_procs_triggers(self):
        self.assertEqual([mod.mod_rank(t) for t in ("FN", "V ", "P ", "TR")], [0, 1, 2, 3])

    def test_coverage_scope_keeps_procs_views_functions_even_when_class_is_info(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "cov.csv"
            path.write_text(
                "object,kind,class\nb20dept,table,OK-testable\nUsp_X,proc,info\nvNhapTP,view,info\nSplit,function,info\n"
                "DUAL/dual,,info\nCustomerConfig,,info\nbad name,table,info\n", encoding="utf-8")
            self.assertEqual(mod.coveragenames(path), {"b20dept", "Usp_X", "vNhapTP", "Split"})

    def test_main_generates_sql_modules_and_difference_csv(self):
        import sys, os
        def col(t, i, n, ty="nvarchar", ml=100, nul=True, ident=False, default=None):
            return dict(schema_name="dbo", table_name=t, column_id=i, column_name=n, type_name=ty, max_length=ml,
                        precision=0, scale=0, is_nullable=nul, is_identity=ident, seed_value=1 if ident else None,
                        increment_value=1 if ident else None, is_computed=False, computed_definition=None,
                        default_constraint_name="DF_" + n if default else None, default_definition=default, collation_name=None)
        mdl = lambda n, ty, d, parent=None: dict(object_name=n, object_type=ty, parent_name=parent, definition=d)
        d57 = {
            "T": [col("Dept", 1, "Id", "bigint", 8, False, True), col("Dept", 2, "Name"), col("Dept", 3, "Req", "int", 4, False),
                  col("Dept", 4, "Flag", "bit", 1, False, default="((0))"), col("Gear", 1, "Id", "int", 4, False)],
            "M": [mdl("vDept", "V ", "CREATE VIEW dbo.vDept AS SELECT Id FROM dbo.Dept"),
                  mdl("Usp_Same", "P ", "CREATE PROC dbo.Usp_Same AS SELECT 1"),
                  mdl("Usp_Diff", "P ", "CREATE PROC dbo.Usp_Diff AS SELECT 2"),
                  mdl("TR_Dept", "TR", "CREATE TRIGGER dbo.TR_Dept ON dbo.Dept AFTER INSERT AS SELECT 1", "Dept"),
                  mdl("Usp_UsesOutside", "P ", "CREATE PROC dbo.Usp_UsesOutside AS SELECT * FROM dbo.Outside")],
            "K": [], "C": [], "I": [], "F": [],
            "D": [dict(referencing_name="Usp_UsesOutside", referenced_server_name=None, referenced_database_name=None, referenced_schema_name="dbo", referenced_entity_name="Outside")],
        }
        d14 = {"T": [col("Dept", 1, "Id", "bigint", 8, False, True), col("Dept", 2, "Name", "nvarchar", 40)],
               "M": [mdl("Usp_Same", "P ", "CREATE PROC dbo.Usp_Same AS SELECT 1  \r\n"), mdl("Usp_Diff", "P ", "CREATE PROC dbo.Usp_Diff AS SELECT 1")],
               "K": [], "C": [], "I": [], "F": [], "D": []}
        d57["T"].append(col("Outside", 1, "X"))
        class Cur:
            def __init__(s, srv): s.srv = srv
            def execute(s, q): pass
            def fetchone(s): return (s.srv, "DB")
        class Conn:
            def __init__(s, k): s.k = k
            def cursor(s): return Cur(s.k)
            def close(s): pass
        keys = {mod.TABLES_SQL: "T", mod.MODULES_SQL: "M", mod.KEYS_SQL: "K", mod.CHECKS_SQL: "C", mod.INDEXES_SQL: "I", mod.FKS_SQL: "F", mod.DEPS_SQL: "D"}
        saved = (mod.connect, mod.rows, sys.argv)
        try:
            mod.connect = lambda env: Conn("S57" if "TARGET" in env else "S14")
            mod.rows = lambda c, sql: (d57 if c.k == "S57" else d14)[keys[sql]]
            with tempfile.TemporaryDirectory() as tmp:
                inv = Path(tmp) / "inv.csv"
                headers = ["T\xean \u0111\u1ed1i t\u01b0\u1ee3ng","Lo\u1ea1i \u0111\u1ed1i t\u01b0\u1ee3ng","Database/schema","Repository/caller","SQL thao t\xe1c","Kh\xe1ch h\xe0ng","\u0110\u01b0\u1ee3c t\u1ea1o b\u1edfi script n\xe0o","C\xf3 tr\xean DB57?","C\xf3 trong schema Git?","Runtime hay test-only","Tr\u1ea1ng th\xe1i x\xe1c minh"]
                with inv.open("w", encoding="utf-8-sig", newline="") as f:
                    w = csv.writer(f); w.writerow(headers)
                    for n in ("dept", "Gear"): w.writerow([n, "B\u1ea3ng", "dbo", "", "SELECT", "", "", "", "", "Runtime", "ok"])
                cov = Path(tmp) / "cov.csv"
                cov.write_text("object,kind,class\nvDept,view,info\nUsp_Same,proc,info\nUsp_Diff,proc,info\nUsp_UsesOutside,proc,info\nMissingProc,proc,info\n", encoding="utf-8")
                out = Path(tmp) / "out"
                sys.argv = ["x", "--out", str(out), "--inventory", str(inv), "--coverage", str(cov)]
                mod.main()
                migration = (out / "DB14_Migration.sql").read_text(encoding="utf-8-sig")
                updates = (out / "DB14_Module_Updates.sql").read_text(encoding="utf-8-sig")
                manual = (out / "DB14_Manual_Review.sql").read_text(encoding="utf-8-sig")
                diffs = list(csv.DictReader((out / "DB14_Differences.csv").open(encoding="utf-8-sig", newline="")))
        finally:
            mod.connect, mod.rows, sys.argv = saved
        self.assertIn("USE [DB];", migration)
        self.assertIn("ADD [Req] int NULL;", migration)                      # NOT NULL without default is added as NULL
        self.assertIn("ADD [Flag] bit NOT NULL CONSTRAINT [DF_Flag] DEFAULT ((0));", migration)
        self.assertIn("CREATE TABLE [dbo].[Gear]", migration)
        self.assertIn("CREATE VIEW dbo.vDept", migration)
        self.assertIn("CREATE TRIGGER dbo.TR_Dept", migration)              # trigger on an in-scope table
        self.assertIn("CREATE PROC dbo.Usp_UsesOutside", migration)
        self.assertLess(migration.index("CREATE VIEW dbo.vDept"), migration.index("CREATE PROC dbo.Usp_UsesOutside"))
        self.assertNotIn("Usp_Same", migration + updates)                    # identical apart from whitespace
        self.assertIn("ALTER PROC dbo.Usp_Diff AS SELECT 2", updates)
        self.assertIn("ALTER COLUMN [Name] nvarchar(50) NULL", manual)       # type difference is only suggested, commented out
        by = {(r["status"], r["object"]) for r in diffs}
        self.assertIn(("GENERATED", "Gear"), by)
        self.assertIn(("NOT_IN_DB57", "MissingProc"), by)
        self.assertIn(("MANUAL", "Usp_UsesOutside"), by)                     # depends on dbo.Outside which DB14 lacks
        self.assertIn(("REVIEW", "Dept"), by)

    def test_connection_string_maps_driver_18_secret_to_installed_driver_17(self):
        secret = "DRIVER={ODBC Driver 18 for SQL Server};SERVER=db;DATABASE=PCTP;Encrypt=yes;TrustServerCertificate=yes"
        actual = mod.resolve_driver_connection_string(secret, ["ODBC Driver 17 for SQL Server"])
        self.assertEqual(
            actual,
            "DRIVER={ODBC Driver 17 for SQL Server};SERVER=db;DATABASE=PCTP;Encrypt=yes;TrustServerCertificate=yes"
        )

    def test_connection_string_prefers_driver_18_when_both_are_installed(self):
        secret = "Driver=ODBC Driver 17 for SQL Server;Server=db;Database=PCTP"
        actual = mod.resolve_driver_connection_string(
            secret, ["ODBC Driver 17 for SQL Server", "ODBC Driver 18 for SQL Server"]
        )
        self.assertTrue(actual.startswith("Driver={ODBC Driver 18 for SQL Server};"))

    def test_connection_string_adds_driver_when_none_is_specified(self):
        actual = mod.resolve_driver_connection_string(
            "SERVER=db;DATABASE=PCTP", ["ODBC Driver 17 for SQL Server"]
        )
        self.assertEqual(actual, "DRIVER={ODBC Driver 17 for SQL Server};SERVER=db;DATABASE=PCTP")

    def test_connection_string_preserves_dsn_and_rejects_missing_driver(self):
        dsn = "DSN=PCTP-QC;UID=user"
        self.assertEqual(
            mod.resolve_driver_connection_string(dsn, ["ODBC Driver 17 for SQL Server"]), dsn
        )
        with self.assertRaisesRegex(RuntimeError, "No supported SQL Server ODBC driver"):
            mod.resolve_driver_connection_string("SERVER=db;DATABASE=PCTP", ["SQL Server"])

    def test_inventory_keeps_sql_runtime_and_excludes_legacy_external(self):
        headers = ["Tên đối tượng","Loại đối tượng","Database/schema","Repository/caller",
                   "SQL thao tác","Khách hàng","Được tạo bởi script nào","Có trên DB57?",
                   "Có trong schema Git?","Runtime hay test-only","Trạng thái xác minh"]
        rows = [
            ["STOCKTP","Bảng","dbo","Repo","SELECT","","","","","Runtime","Caller đã thấy"],
            ["OLD_TABLE","Bảng","dbo","Legacy","SELECT","","","","","Legacy-only","retired"],
            ["IFS_TABLE","Đối tượng nguồn IFS","IFS","Repo","SELECT","","","","","Runtime external","External"],
            ["TMP_{host}","Tên bảng động","dbo","Repo","SELECT","","","","","Runtime động","Chưa xác minh"],
        ]
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/"inventory.csv"
            with path.open("w",encoding="utf-8-sig",newline="") as f:
                writer=csv.writer(f);writer.writerow(headers);writer.writerows(rows)
            names,excluded=mod.invnames(path)
        self.assertEqual(names,["STOCKTP"])
        self.assertIn("TMP_{host}",excluded)

if __name__ == "__main__":
    unittest.main()
