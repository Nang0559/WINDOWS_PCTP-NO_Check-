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

    def test_export_columns_come_from_structured_sql2_inventory(self):
        cols = sc.export_columns()
        self.assertIn('rack', cols)
        self.assertIn('rackrowcount', cols['rack'])
        self.assertNotIn('customerconfig', cols)

    def test_db57_absence_is_unverified_not_nonexistent(self):
        self.assertEqual('GAP-db57-unverified', sc.classify(True, None, None, False))
        self.assertEqual('GAP-no-git-ddl', sc.classify(True, {'stub': False}, None, False))

    def test_sql2_catalog_contains_dbo_tables_and_modules(self):
        objects = sc.export_objects()
        self.assertIn('b20gear', objects)
        self.assertEqual('table', objects['b20gear']['kind'])
        self.assertGreaterEqual(sum(1 for item in objects.values() if item['kind'] == 'table'), 500)
        self.assertIn('vnhaptp', objects)
        self.assertEqual('view', objects['vnhaptp']['kind'])

    def test_sql2_fk_dependency_closure_includes_workflow_status_table(self):
        objects = sc.export_objects()
        deps = sc.foreign_key_dependency_map(
            sc.METADATA, {'sys_workflowtransitions'}, objects)
        self.assertIn('sys_workflowstatuses', deps)


if __name__ == '__main__':
    unittest.main()
