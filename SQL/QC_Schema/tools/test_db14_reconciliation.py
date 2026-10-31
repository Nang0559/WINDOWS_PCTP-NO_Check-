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
