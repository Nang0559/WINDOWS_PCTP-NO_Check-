import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import gen_constraints_from_metadata as gen

class ConstraintGeneratorTests(unittest.TestCase):
    def test_identifier_escaping(self):
        self.assertEqual(gen.ident("a]b"), "[a]]b]")
    def test_truth_values(self):
        self.assertTrue(gen.truth("1"))
        self.assertTrue(gen.truth("true"))
        self.assertFalse(gen.truth("0"))
    def test_metadata_sections_keep_fixed_width_columns(self):
        import tempfile
        content = "SchemaName  TableName  IndexName\n----------  ---------  ---------\ndbo         T          IX_T\n\n"
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/"m.txt"; p.write_text(content,encoding="utf-8")
            found=gen.sections(p,{"indexes":{"SchemaName","TableName","IndexName"}})
            self.assertEqual(found["indexes"][0]["TableName"],"T")
            self.assertEqual(found["indexes"][0]["IndexName"],"IX_T")

if __name__=="__main__": unittest.main()
