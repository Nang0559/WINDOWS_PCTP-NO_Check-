import os, sys, unittest
from unittest.mock import patch
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
        self.assertIn('vnhaptp', objects)
        self.assertEqual('view', objects['vnhaptp']['kind'])

    def test_runtime_scope_uses_pctp_inventory_not_all_db57_tables(self):
        inventory = {
            "stocktp": {"Loại đối tượng": "Bảng", "Runtime hay test-only": "Runtime",
                        "Database/schema": "Chưa xác định", "Trạng thái xác minh": "Caller đã thấy"},
            "tmp_delivery": {"Loại đối tượng": "Bảng (tên cấu hình)", "Runtime hay test-only": "Runtime theo cấu hình",
                             "Database/schema": "Chưa xác định", "Trạng thái xác minh": "Tên cấu hình"},
            "ifs_source": {"Loại đối tượng": "Bảng/nguồn IFS cấu hình", "Runtime hay test-only": "Runtime theo cấu hình",
                           "Database/schema": "IFS connection", "Trạng thái xác minh": "Không mặc định là DB57"},
            "not_confirmed": {"Loại đối tượng": "Bảng", "Runtime hay test-only": "Chưa xác định runtime",
                              "Database/schema": "Chưa xác định", "Trạng thái xác minh": "Cần xác minh"},
            "ui_type": {"Loại đối tượng": "Lớp C#", "Runtime hay test-only": "Runtime",
                        "Database/schema": "Không áp dụng", "Trạng thái xác minh": "Không phải bảng SQL"},
        }
        self.assertEqual({"stocktp", "tmp_delivery"}, sc.runtime_table_candidates(inventory))

    def test_sql2_fk_dependency_closure_includes_workflow_status_table(self):
        # Keep this algorithm test independent of the optional local SQL2 export.
        objects = {'sys_workflowtransitions': {'kind': 'table'},
                   'sys_workflowstatuses': {'kind': 'table'}}
        fk_rows = [{
            'ParentSchema': 'dbo', 'ParentTable': 'sys_workflowtransitions',
            'ParentColumn': 'StatusId', 'ReferencedSchema': 'dbo',
            'ReferencedTable': 'sys_workflowstatuses', 'ReferencedColumn': 'Id',
            'ForeignKeyName': 'FK_Transitions_Status', 'ColumnOrdinal': '1',
            'OnDelete': 'NO_ACTION', 'OnUpdate': 'NO_ACTION',
            'IsDisabled': '0', 'IsNotTrusted': '0',
        }]
        with patch.object(sc, 'parse_section', return_value=fk_rows):
            deps = sc.foreign_key_dependency_map(
                sc.METADATA, {'sys_workflowtransitions'}, objects)
        self.assertIn('sys_workflowstatuses', deps)

    def test_verified_fk_dependency_is_not_reported_as_a_schema_gap(self):
        classification = sc.fk_dependency_classification(
            {'fvn_pxlb_reworkmovement'}, None,
            {'kind': 'table', 'name': 'FVN_PXLB_ReworkPlan'}, 'có', True)
        self.assertEqual('OK-fk-dependency', classification)

    def test_fk_dependency_missing_from_db57_or_ci_remains_a_gap(self):
        dependency = {'fvn_pxlb_reworkmovement'}
        table = {'kind': 'table', 'name': 'FVN_PXLB_ReworkPlan'}
        self.assertEqual('GAP-fk-dependency',
                         sc.fk_dependency_classification(dependency, None, table, 'không', True))
        self.assertEqual('GAP-fk-dependency',
                         sc.fk_dependency_classification(dependency, None, table, 'có', False))

    def test_fk_dependency_with_direct_code_reference_uses_normal_classification(self):
        self.assertIsNone(sc.fk_dependency_classification(
            {'fvn_pxlb_reworkmovement'}, {'files': set()}, {'kind': 'table'}, 'có', True))


if __name__ == '__main__':
    unittest.main()
