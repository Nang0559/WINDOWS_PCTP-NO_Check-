import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import gen_schema_from_inventory as gen


class InvalidSqlLengthTests(unittest.TestCase):
    def test_rejects_zero_unicode_length_before_sql_deploy(self):
        row = {
            "TableName": "B20Dept",
            "ColumnName": "BrokenCode",
            "DataType": "nvarchar",
            "MaxChars": "0",
            "precision": "0",
            "scale": "0",
        }
        with self.assertRaisesRegex(ValueError, "zero/non-positive Unicode character length"):
            gen.sql_type(row, {})

    def test_rejects_zero_non_unicode_length_before_sql_deploy(self):
        row = {
            "TableName": "FVN_Test",
            "ColumnName": "BrokenCode",
            "DataType": "varchar",
            "MaxChars": "0",
            "precision": "0",
            "scale": "0",
        }
        with self.assertRaisesRegex(ValueError, "zero/non-positive length"):
            gen.sql_type(row, {})

    def test_temporal_scale_zero_remains_valid(self):
        for data_type in ("datetime2", "time", "datetimeoffset"):
            row = {
                "TableName": "FVN_Test",
                "ColumnName": "OccurredAt",
                "DataType": data_type,
                "MaxChars": "8",
                "precision": "19",
                "scale": "0",
            }
            self.assertEqual(f"{data_type}(0)", gen.sql_type(row, {}))


if __name__ == "__main__":
    unittest.main()
