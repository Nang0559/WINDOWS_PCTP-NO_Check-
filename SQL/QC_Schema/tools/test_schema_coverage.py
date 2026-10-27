import os, sys, unittest
sys.path.insert(0, os.path.dirname(__file__))
import schema_coverage as sc


class ParsingTests(unittest.TestCase):
    def test_insert_columns(self):
        m = sc.INS.search('INSERT INTO Rack (WarehouseId, RackName, RowCount) OUTPUT INSERTED.RackId VALUES (@a,@b,@c)')
        self.assertEqual('Rack', m.group(1))
        self.assertEqual(['WarehouseId', 'RackName', 'RowCount'], [c.strip() for c in m.group(2).split(',')])

    def test_update_assignments_ignore_where_and_alias(self):
        m = sc.UPD.search('UPDATE dbo.STOCKTP SET SLCONLAI = SLCONLAI - @sl, t.STATUS = 1 WHERE LOT = @lot')
        self.assertEqual('STOCKTP', m.group(1))
        self.assertEqual(['SLCONLAI', 'STATUS'], sc.ASG.findall(m.group(2)))

    def test_ddl_regex_matches_alter_and_create_proc(self):
        self.assertEqual('Usp_X', sc.DDL.search('CREATE OR ALTER PROCEDURE dbo.Usp_X AS').group(2))

    def test_export_columns_found_for_real_tables_only(self):
        cols = sc.export_columns()
        self.assertIn('rack', cols)
        self.assertIn('rackrowcount', cols['rack'])
        self.assertNotIn('customerconfig', cols)  # stub on DB57


if __name__ == '__main__':
    unittest.main()
