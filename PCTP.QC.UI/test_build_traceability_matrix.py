import importlib.util
import os
import tempfile
import unittest

MODULE_PATH = os.path.join(os.path.dirname(__file__), "Build-TraceabilityMatrix.py")
SPEC = importlib.util.spec_from_file_location("traceability_matrix", MODULE_PATH)
TRACE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(TRACE)


class TransactionClassificationTests(unittest.TestCase):
    def analyze(self, source):
        with tempfile.NamedTemporaryFile("w", suffix=".cs", encoding="utf-8", delete=False) as handle:
            handle.write(source)
            path = handle.name
        try:
            return TRACE.analyze_cs(path)
        finally:
            os.unlink(path)

    def test_single_dml_statement_is_statement_atomic(self):
        result = self.analyze('class Legacy { void Save() { ExecuteNonQuery("UPDATE T SET A = 1"); } }')
        self.assertEqual("STATEMENT-ATOMIC", result["tx"])

    def test_multiple_dml_statements_require_transaction_owner(self):
        result = self.analyze('class Legacy { void Save() { ExecuteNonQuery("UPDATE T SET A = 1; DELETE FROM T WHERE A = 2"); } }')
        self.assertEqual("NONE", result["tx"])

    def test_stored_procedure_call_is_not_misclassified_as_single_statement(self):
        result = self.analyze('class Legacy { void Save() { ExecuteStoredProcedure("Usp_UpdateStock"); } }')
        self.assertEqual("NONE", result["tx"])

    def test_explicit_begin_commit_is_owner(self):
        result = self.analyze('class Legacy { void Save() { uow.Begin(); ExecuteNonQuery("UPDATE T SET A = 1"); uow.Commit(); } }')
        self.assertEqual("OWNS", result["tx"])


if __name__ == "__main__":
    unittest.main()
