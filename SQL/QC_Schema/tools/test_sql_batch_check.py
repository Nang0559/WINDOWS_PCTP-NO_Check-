import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import sql_batch_check as checker  # noqa: E402


def run(text):
    findings, created, referenced = [], {}, {}
    checker.analyse_file('t.sql', text, findings, created, referenced)
    return findings, created


def codes(text):
    return sorted({f.code for f in run(text)[0]})


class BatchSplitTests(unittest.TestCase):
    def test_go_with_count_and_comment_splits(self):
        batches, ambiguous, _ = checker.split_batches("SELECT 1;\nGO 3 -- repeat\nSELECT 2;\n")
        self.assertEqual(2, len(batches))
        self.assertEqual(3, batches[0][3])
        self.assertEqual([], ambiguous)

    def test_go_inside_block_comment_or_string_is_ambiguous_not_a_separator(self):
        batches, ambiguous, _ = checker.split_batches("/*\nGO\n*/\nSELECT 'a\nGO\nb';\n")
        self.assertEqual(1, len(batches))
        self.assertEqual([2, 5], ambiguous)

    def test_word_go_inside_a_statement_is_not_a_separator(self):
        batches, _, _ = checker.split_batches("SELECT 1 AS go;\nSELECT 2 -- GO\n")
        self.assertEqual(1, len(batches))


class DefectTests(unittest.TestCase):
    def test_literal_backslash_n_in_code_is_flagged_but_not_in_strings_or_comments(self):
        self.assertIn('E001', codes("CREATE TABLE t (\\n a INT);"))
        self.assertNotIn('E001', codes("SELECT N'line\\nbreak'; -- \\n in a comment\n"))

    def test_trailing_comma_ignores_empty_string_arguments(self):
        self.assertIn('E006', codes("CREATE TABLE t (a INT,\n);"))
        self.assertNotIn('E006', codes("SELECT ISNULL(a,'') FROM t;"))

    def test_module_must_be_first_statement_in_batch(self):
        self.assertIn('E003', codes("SET NOCOUNT ON;\nCREATE PROC dbo.p AS SELECT 1;\n"))
        self.assertNotIn('E003', codes("-- header\nCREATE OR ALTER PROC dbo.p AS SELECT 1;\n"))
        self.assertNotIn('E003', codes("SET NOCOUNT ON;\nGO\nCREATE PROC dbo.p AS SELECT 1;\n"))

    def test_use_and_sqlcmd_variables_and_production_markers(self):
        self.assertIn('E004', codes("USE B7R2_FCC;\nSELECT 1;\n"))
        self.assertIn('E008', codes("SELECT $(db);\n"))
        self.assertIn('E009', codes("SELECT N'Server=192.168.200.14';\n"))
        self.assertNotIn('E009', codes("-- never 192.168.200.14\nIF @@SERVERNAME LIKE N'%BRAVO%' THROW 51002, N'x', 1;\n"))

    def test_ssms_noise_and_stray_header(self):
        self.assertIn('E002', codes("(68 rows affected)\nSELECT 1;\n"))
        self.assertIn('E002', codes("Text\nCREATE PROC dbo.p AS SELECT 1;\n"))

    def test_unterminated_string_or_comment(self):
        self.assertIn('E005', codes("SELECT 'abc;\n"))
        self.assertIn('E005', codes("/* never closed\nSELECT 1;\n"))


class ExportTests(unittest.TestCase):
    EXPORT = (
        "(2 rows affected)\n"
        "-- OBJECT: T1 (U)\n" + "-" * 82 + "\n"
        "CREATE TABLE [dbo].[T1] (\\n    [A] NVARCHAR(100) NULL,\\n    [B] INT NULL,\\n);\\n\nGO\n \n"
        "-- OBJECT: Missing (U)\n" + "-" * 82 + "\n"
        "-- [Notice]: Table [Missing] does not exist in current database.\nGO\n"
    )

    def test_normalised_copy_loads_cleanly_and_halves_unicode_lengths(self):
        with tempfile.TemporaryDirectory() as d:
            src, dst = os.path.join(d, 'export.txt'), os.path.join(d, 'out.sql')
            with open(src, 'w', encoding='utf-8') as handle:
                handle.write(self.EXPORT)
            self.assertEqual(0, checker.main(['--export', src, '--emit-loadable', dst, '--halve-unicode-lengths']))
            with open(dst, encoding='utf-8') as handle:
                out = handle.read()
            self.assertIn('NVARCHAR(50)', out)
            self.assertNotIn('\\n', out)
            self.assertNotIn('Missing', out)
            findings, _ = run(out)
            self.assertEqual([], [f for f in findings if f.level == 'ERROR'])

    def test_raw_export_is_reported_as_defective(self):
        findings, _ = run(self.EXPORT)
        self.assertTrue({'E001', 'E002'} <= {f.code for f in findings})


if __name__ == '__main__':
    unittest.main()
