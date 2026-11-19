using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Security
{
    public enum PermissionMatrixMode
    {
        /// <summary>Quyền của vai trò: mỗi ô là hộp chọn Cho phép / Không.</summary>
        Role,
        /// <summary>Quyền riêng của tài khoản: mỗi ô là Kế thừa / Cho phép / Từ chối.</summary>
        UserOverride
    }

    /// <summary>
    /// Ma trận phân quyền (hàng = chức năng gom theo phân hệ, cột = thao tác) dùng DevExpress GridView.
    /// Nguồn dữ liệu là bảng "dài" từ SEC.usp_Admin_ListFunctionCatalog (Role)
    /// hoặc SEC.usp_Admin_ListUserFunctionOverrides (UserOverride); control tự pivot.
    /// Mỗi lần người dùng đổi một ô sẽ gọi <see cref="SaveHandler"/>; trả về false thì nạp lại ma trận.
    /// </summary>
    public sealed class PermissionMatrixControl : XtraUserControl
    {
        public const string Inherit = "INHERIT";
        public const string Allow = "ALLOW";
        public const string Deny = "DENY";

        private const string ActionPrefix = "A_";
        private const string RolePrefix = "R_";
        private const string AllField = "ALL_ACTIONS";

        private readonly PermissionMatrixMode _mode;
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _view;
        private readonly Dictionary<string, string> _fieldToAction =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _actionCodes = new List<string>();

        private DataTable _matrix;
        private bool _suppress;
        private bool _readOnly;

        /// <summary>(FunctionCode, ActionCode, giá trị mới) -> đã lưu thành công?
        /// Role: giá trị là bool. UserOverride: giá trị là "INHERIT" | "ALLOW" | "DENY".</summary>
        public Func<string, string, object, bool> SaveHandler { get; set; }

        /// <summary>Phát sinh khi lưu lỗi để form nạp lại dữ liệu từ DB.</summary>
        public event EventHandler ReloadRequested;

        public PermissionMatrixControl(PermissionMatrixMode mode)
        {
            _mode = mode;
            Dock = DockStyle.Fill;

            _grid.Dock = DockStyle.Fill;
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            Controls.Add(_grid);

            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsView.ShowIndicator = false;
            _view.OptionsView.EnableAppearanceEvenRow = false;
            _view.OptionsFind.AlwaysVisible = true;                 // ô tìm chức năng
            _view.OptionsFind.FindNullPrompt = "Tìm chức năng / phân hệ...";
            _view.OptionsBehavior.AutoExpandAllGroups = true;
            _view.OptionsBehavior.Editable = true;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.OptionsCustomization.AllowColumnMoving = false;
            _view.OptionsCustomization.AllowGroup = false;
            _view.HorzScrollVisibility = ScrollVisibility.Auto;
            _view.Appearance.HeaderPanel.Options.UseTextOptions = true;
            _view.Appearance.HeaderPanel.TextOptions.HAlignment = HorzAlignment.Center;
            _view.Appearance.HeaderPanel.TextOptions.WordWrap = WordWrap.Wrap;
            _view.ColumnPanelRowHeight = 36;

            _view.CellValueChanged += OnCellValueChanged;
            _view.RowCellStyle += OnRowCellStyle;
            _view.CustomColumnDisplayText += OnCustomColumnDisplayText;
        }

        public bool ReadOnly
        {
            get { return _readOnly; }
            set { _readOnly = value; _view.OptionsBehavior.Editable = !value; }
        }

        // ───────────────────────── Nạp dữ liệu ─────────────────────────

        public void SetData(DataTable source)
        {
            _suppress = true;
            try
            {
                _grid.DataSource = null;
                _view.Columns.Clear();
                _fieldToAction.Clear();
                _actionCodes.Clear();
                _matrix = null;
                if (source == null || source.Rows.Count == 0) return;

                _matrix = Pivot(source);
                _grid.DataSource = _matrix;
                ConfigureColumns(source);
                _view.ExpandAllGroups();
            }
            finally { _suppress = false; }
        }

        private DataTable Pivot(DataTable source)
        {
            var actionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in source.Rows)
            {
                string code = Convert.ToString(r["ActionCode"]);
                if (actionNames.ContainsKey(code)) continue;
                actionNames[code] = Convert.ToString(r["ActionName"]);
                _actionCodes.Add(code);
            }

            var table = new DataTable("PermissionMatrix");
            table.Columns.Add("FunctionCode", typeof(string));
            table.Columns.Add("ModuleCode", typeof(string));
            table.Columns.Add("FunctionName", typeof(string));
            foreach (string code in _actionCodes)
            {
                string field = ActionPrefix + code;
                _fieldToAction[field] = code;
                table.Columns.Add(field, _mode == PermissionMatrixMode.Role ? typeof(bool) : typeof(string));
                if (_mode == PermissionMatrixMode.UserOverride)
                    table.Columns.Add(RolePrefix + code, typeof(bool));
            }
            if (_mode == PermissionMatrixMode.Role) table.Columns.Add(AllField, typeof(bool));

            var rows = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in source.Rows)
            {
                string fn = Convert.ToString(r["FunctionCode"]);
                DataRow row;
                if (!rows.TryGetValue(fn, out row))
                {
                    row = table.NewRow();
                    row["FunctionCode"] = fn;
                    row["ModuleCode"] = Convert.ToString(r["ModuleCode"]);
                    row["FunctionName"] = Convert.ToString(r["FunctionName"]);
                    foreach (string code in _actionCodes)
                    {
                        row[ActionPrefix + code] = _mode == PermissionMatrixMode.Role ? (object)false : Inherit;
                        if (_mode == PermissionMatrixMode.UserOverride) row[RolePrefix + code] = false;
                    }
                    table.Rows.Add(row);
                    rows[fn] = row;
                }

                string action = Convert.ToString(r["ActionCode"]);
                if (_mode == PermissionMatrixMode.Role)
                {
                    row[ActionPrefix + action] = r["IsAllowed"] != DBNull.Value && Convert.ToBoolean(r["IsAllowed"]);
                }
                else
                {
                    row[ActionPrefix + action] = r["Effect"] == DBNull.Value ? Inherit : Convert.ToString(r["Effect"]);
                    row[RolePrefix + action] = r["RoleIsAllowed"] != DBNull.Value && Convert.ToBoolean(r["RoleIsAllowed"]);
                }
            }

            if (_mode == PermissionMatrixMode.Role)
                foreach (DataRow row in table.Rows) row[AllField] = AllChecked(row);

            table.AcceptChanges();
            return table;
        }

        private bool AllChecked(DataRow row)
        {
            foreach (string code in _actionCodes)
                if (!Convert.ToBoolean(row[ActionPrefix + code])) return false;
            return _actionCodes.Count > 0;
        }

        private void ConfigureColumns(DataTable source)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in source.Rows)
                names[Convert.ToString(r["ActionCode"])] = Convert.ToString(r["ActionName"]);

            var check = new RepositoryItemCheckEdit
            {
                CheckStyle = CheckStyles.Standard,
                GlyphAlignment = HorzAlignment.Center,
                ValueChecked = true,
                ValueUnchecked = false
            };
            check.EditValueChanged += delegate { _view.PostEditor(); };   // lưu ngay khi bấm

            var combo = new RepositoryItemImageComboBox { TextEditStyle = TextEditStyles.DisableTextEditor };
            combo.Items.Add(new ImageComboBoxItem("Kế thừa vai trò", Inherit, -1));
            combo.Items.Add(new ImageComboBoxItem("✔ Cho phép", Allow, -1));
            combo.Items.Add(new ImageComboBoxItem("✘ Từ chối", Deny, -1));
            combo.EditValueChanged += delegate { _view.PostEditor(); };

            _grid.RepositoryItems.Add(check);
            _grid.RepositoryItems.Add(combo);

            _view.Columns["ModuleCode"].Caption = "Phân hệ";
            _view.Columns["ModuleCode"].GroupIndex = 0;
            _view.Columns["ModuleCode"].OptionsColumn.AllowEdit = false;

            GridColumn fn = _view.Columns["FunctionCode"];
            fn.Caption = "Mã chức năng";
            fn.Width = 210;
            fn.Fixed = FixedStyle.Left;
            fn.OptionsColumn.AllowEdit = false;

            GridColumn name = _view.Columns["FunctionName"];
            name.Caption = "Chức năng";
            name.Width = 260;
            name.Fixed = FixedStyle.Left;
            name.OptionsColumn.AllowEdit = false;

            if (_mode == PermissionMatrixMode.Role)
            {
                GridColumn all = _view.Columns[AllField];
                all.Caption = "Tất cả";
                all.Width = 64;
                all.ColumnEdit = check;
                all.AppearanceCell.TextOptions.HAlignment = HorzAlignment.Center;
            }

            foreach (string code in _actionCodes)
            {
                GridColumn col = _view.Columns[ActionPrefix + code];
                string caption;
                col.Caption = names.TryGetValue(code, out caption) && !string.IsNullOrWhiteSpace(caption) ? caption : code;
                col.Width = _mode == PermissionMatrixMode.Role ? 78 : 120;
                col.ColumnEdit = _mode == PermissionMatrixMode.Role ? (RepositoryItem)check : combo;
                col.AppearanceCell.TextOptions.HAlignment = HorzAlignment.Center;
                col.ToolTip = code;

                if (_mode == PermissionMatrixMode.UserOverride)
                {
                    GridColumn role = _view.Columns[RolePrefix + code];
                    role.Visible = false;
                    role.OptionsColumn.ShowInCustomizationForm = false;
                }
            }
        }

        // ───────────────────────── Sự kiện ─────────────────────────

        private void OnCellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (_suppress || _matrix == null || SaveHandler == null) return;

            string field = e.Column.FieldName;
            DataRow row = _view.GetDataRow(e.RowHandle);
            if (row == null) return;
            string function = Convert.ToString(row["FunctionCode"]);

            if (_mode == PermissionMatrixMode.Role && field == AllField)
            {
                SaveWholeRow(row, function, Convert.ToBoolean(e.Value));
                return;
            }

            string action;
            if (!_fieldToAction.TryGetValue(field, out action)) return;

            bool ok = SaveHandler(function, action, e.Value);
            if (!ok) { RequestReload(); return; }

            if (_mode == PermissionMatrixMode.Role) SyncAllFlag(row);
        }

        private void SaveWholeRow(DataRow row, string function, bool value)
        {
            _suppress = true;
            try
            {
                foreach (string code in _actionCodes)
                {
                    string field = ActionPrefix + code;
                    if (Convert.ToBoolean(row[field]) == value) continue;
                    if (!SaveHandler(function, code, value)) { RequestReload(); return; }
                    row[field] = value;
                }
            }
            finally { _suppress = false; }
        }

        private void SyncAllFlag(DataRow row)
        {
            _suppress = true;
            try { row[AllField] = AllChecked(row); }
            finally { _suppress = false; }
        }

        private void RequestReload()
        {
            var handler = ReloadRequested;
            if (handler == null) return;
            // Không nạp lại ngay trong CellValueChanged để tránh đổi nguồn khi view đang xử lý.
            BeginInvoke(new Action(() => handler(this, EventArgs.Empty)));
        }

        private static readonly Color AllowBack = Color.FromArgb(198, 239, 206);
        private static readonly Color DenyBack = Color.FromArgb(255, 199, 206);
        private static readonly Color InheritAllowBack = Color.FromArgb(232, 245, 233);
        private static readonly Color InheritDenyBack = Color.FromArgb(246, 246, 246);

        private void OnRowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.Column == null || !_fieldToAction.ContainsKey(e.Column.FieldName)) return;
            object value = _view.GetRowCellValue(e.RowHandle, e.Column);

            if (_mode == PermissionMatrixMode.Role)
            {
                if (value is bool && (bool)value) e.Appearance.BackColor = AllowBack;
                return;
            }

            string effect = Convert.ToString(value);
            if (effect == Allow) e.Appearance.BackColor = AllowBack;
            else if (effect == Deny) e.Appearance.BackColor = DenyBack;
            else
            {
                GridColumn role = _view.Columns[RolePrefix + _fieldToAction[e.Column.FieldName]];
                bool roleAllowed = role != null && Convert.ToBoolean(_view.GetRowCellValue(e.RowHandle, role));
                e.Appearance.BackColor = roleAllowed ? InheritAllowBack : InheritDenyBack;
            }
        }

        private void OnCustomColumnDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            if (_mode != PermissionMatrixMode.UserOverride || _matrix == null || e.Column == null) return;
            string action;
            if (!_fieldToAction.TryGetValue(e.Column.FieldName, out action)) return;

            string effect = Convert.ToString(e.Value);
            if (effect == Allow) e.DisplayText = "✔ Cho phép";
            else if (effect == Deny) e.DisplayText = "✘ Từ chối";
            else
            {
                // Kế thừa: hiển thị luôn kết quả kế thừa từ vai trò (↳ ✔ / ↳ ✘).
                bool roleAllowed = false;
                if (e.ListSourceRowIndex >= 0 && e.ListSourceRowIndex < _matrix.DefaultView.Count)
                    roleAllowed = Convert.ToBoolean(_matrix.DefaultView[e.ListSourceRowIndex][RolePrefix + action]);
                e.DisplayText = roleAllowed ? "↳ ✔ (vai trò)" : "↳ ✘ (vai trò)";
            }
        }
    }
}
