using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using System.Linq;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Security
{
    public sealed class FormSecurityAdministration : XtraForm
    {
        private readonly SecurityAdministrationRepository _repository = new SecurityAdministrationRepository();
        private readonly UserSession _session;
        private readonly XtraTabControl _tabs = new XtraTabControl();
        private readonly GridControl _users = Grid();
        private readonly GridControl _roles = Grid();
        private readonly GridControl _scopes = Grid();
        private readonly GridControl _audit = Grid();
        private readonly GridControl _userScopes = Grid();
        private readonly PermissionMatrixControl _rolePermissions = new PermissionMatrixControl(PermissionMatrixMode.Role);
        private readonly PermissionMatrixControl _userPermissions = new PermissionMatrixControl(PermissionMatrixMode.UserOverride);
        private readonly LabelControl _selectedUserLabel = new LabelControl();
        private GridView _usersView, _rolesView, _scopesView, _auditView, _userScopesView;

        public FormSecurityAdministration()
        {
            _session = UserSession.Current ?? throw new UnauthorizedAccessException("Chưa đăng nhập.");
            Text = "PCTP - Quản trị tài khoản và phân quyền";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 780);
            MinimumSize = new Size(1000, 600);
            BuildUi();
            Load += (s, e) => RefreshAll();
        }

        /// <summary>Lưới DevExpress chỉ-đọc dùng chung (danh sách tài khoản, vai trò, phạm vi, nhật ký).</summary>
        private static GridControl Grid()
        {
            var grid = new GridControl { Dock = DockStyle.Fill };
            var view = new GridView(grid);
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsFind.AlwaysVisible = true;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            grid.DataSourceChanged += (s, e) =>
            {
                // Chờ view nạp cột xong rồi mới BestFit.
                if (grid.IsHandleCreated)
                    grid.BeginInvoke(new Action(() => { if (view.Columns.Count > 0) view.BestFitColumns(); }));
            };
            return grid;
        }

        private static GridView ViewOf(GridControl grid) { return (GridView)grid.MainView; }

        private static object FocusedValue(GridView view, string field)
        {
            if (view == null || view.FocusedRowHandle < 0 || view.Columns[field] == null) return null;
            return view.GetFocusedRowCellValue(field);
        }

        private void BuildUi()
        {
            _usersView = ViewOf(_users); _rolesView = ViewOf(_roles); _scopesView = ViewOf(_scopes);
            _auditView = ViewOf(_audit); _userScopesView = ViewOf(_userScopes);

            _tabs.Dock = DockStyle.Fill;
            var userTab = new XtraTabPage { Text = "Tài khoản" };
            var roleTab = new XtraTabPage { Text = "Vai trò và quyền" };
            var accountAccessTab = new XtraTabPage { Text = "Quyền riêng tài khoản" };
            var auditTab = new XtraTabPage { Text = "Nhật ký bảo mật" };
            _tabs.TabPages.AddRange(new[] { userTab, roleTab, accountAccessTab, auditTab });
            Controls.Add(_tabs);

            // ── Tab 1: tài khoản
            var userButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
            AddButton(userButtons, "Tạo tài khoản", CreateUser);
            AddButton(userButtons, "Gán vai trò", AssignRoles);
            AddButton(userButtons, "Khóa / mở tài khoản", ToggleUser);
            AddButton(userButtons, "Đặt lại mật khẩu", ResetPassword);
            userTab.Controls.Add(_users);
            userTab.Controls.Add(userButtons);

            // ── Tab 2: vai trò — ma trận quyền (chức năng x thao tác) + phạm vi dữ liệu
            var rolePanel = new SplitContainerControl
            {
                Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 190,
                FixedPanel = SplitFixedPanel.Panel1
            };
            var roleButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            AddButton(roleButtons, "Tạo vai trò", CreateRole);
            AddButton(roleButtons, "Làm mới", (s, e) => LoadRoles());
            rolePanel.Panel1.Controls.Add(_roles);
            rolePanel.Panel1.Controls.Add(roleButtons);

            var roleDetails = new XtraTabControl { Dock = DockStyle.Fill };
            var permissionTab = new XtraTabPage { Text = "Ma trận quyền theo chức năng" };
            var scopeTab = new XtraTabPage { Text = "Phạm vi dữ liệu" };
            var scopeButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            AddButton(scopeButtons, "Thêm phạm vi", AddScope);
            AddButton(scopeButtons, "Làm mới", (s, e) => LoadScopes());
            permissionTab.Controls.Add(_rolePermissions);
            scopeTab.Controls.Add(_scopes);
            scopeTab.Controls.Add(scopeButtons);
            roleDetails.TabPages.AddRange(new[] { permissionTab, scopeTab });
            rolePanel.Panel2.Controls.Add(roleDetails);
            roleTab.Controls.Add(rolePanel);

            // ── Tab 3: quyền riêng tài khoản — ma trận Kế thừa / Cho phép / Từ chối
            var accountAccessTabs = new XtraTabControl { Dock = DockStyle.Fill };
            var accountPermissionTab = new XtraTabPage { Text = "Ma trận quyền chức năng riêng" };
            var accountScopeTab = new XtraTabPage { Text = "Phạm vi dữ liệu riêng" };
            var accountPermissionButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38 };
            AddButton(accountPermissionButtons, "Làm mới", (s, e) => LoadUserOverrides());
            accountPermissionTab.Controls.Add(_userPermissions);
            accountPermissionTab.Controls.Add(accountPermissionButtons);
            var accountScopeButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38 };
            AddButton(accountScopeButtons, "Thêm phạm vi", AddUserScope);
            AddButton(accountScopeButtons, "Xóa phạm vi", DeleteUserScope);
            AddButton(accountScopeButtons, "Làm mới", (s, e) => LoadUserScopes());
            accountScopeTab.Controls.Add(_userScopes);
            accountScopeTab.Controls.Add(accountScopeButtons);
            accountAccessTabs.TabPages.AddRange(new[] { accountPermissionTab, accountScopeTab });

            _selectedUserLabel.Dock = DockStyle.Top;
            _selectedUserLabel.AutoSizeMode = LabelAutoSizeMode.None;
            _selectedUserLabel.Height = 30;
            _selectedUserLabel.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _selectedUserLabel.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _selectedUserLabel.Padding = new Padding(8, 0, 0, 0);
            var accountAccessNotice = new LabelControl
            {
                Dock = DockStyle.Top, AutoSizeMode = LabelAutoSizeMode.None, Height = 26,
                Text = "Chọn tài khoản ở tab \"Tài khoản\". Quyền riêng và phạm vi riêng được nạp lại khi tài khoản đăng nhập phiên mới.",
                Padding = new Padding(8, 0, 0, 0)
            };
            accountAccessTab.Controls.Add(accountAccessTabs);
            accountAccessTab.Controls.Add(accountAccessNotice);
            accountAccessTab.Controls.Add(_selectedUserLabel);

            // ── Tab 4: nhật ký
            var auditButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            AddButton(auditButtons, "Làm mới nhật ký", (s, e) => LoadAudit());
            auditTab.Controls.Add(_audit);
            auditTab.Controls.Add(auditButtons);

            _rolesView.FocusedRowChanged += (s, e) => { LoadPermissions(); LoadScopes(); };
            _usersView.FocusedRowChanged += (s, e) => { LoadUserOverrides(); LoadUserScopes(); };

            _rolePermissions.SaveHandler = SaveRolePermissionCell;
            _rolePermissions.ReloadRequested += (s, e) => LoadPermissions();
            _userPermissions.SaveHandler = SaveUserOverrideCell;
            _userPermissions.ReloadRequested += (s, e) => LoadUserOverrides();
        }

        private static void AddButton(Control parent, string text, EventHandler handler)
        {
            var button = new SimpleButton { Text = text, Width = Math.Max(110, text.Length * 9), Height = 30, Margin = new Padding(4) };
            button.Click += handler; parent.Controls.Add(button);
        }

        private void RefreshAll()
        {
            bool userView = _session.HasPermission("ADMIN.USER_MANAGEMENT", "VIEW");
            bool roleView = _session.HasPermission("ADMIN.ROLE_MANAGEMENT", "VIEW");
            bool auditView = _session.HasPermission("ADMIN.AUDIT", "VIEW");
            _tabs.TabPages[0].PageEnabled = userView;
            _tabs.TabPages[1].PageEnabled = roleView;
            _tabs.TabPages[2].PageEnabled = userView;
            _tabs.TabPages[3].PageEnabled = auditView;
            if (!userView) _tabs.TabPages[0].Text += " (không có quyền)";
            if (!roleView) _tabs.TabPages[1].Text += " (không có quyền)";
            if (!userView) _tabs.TabPages[2].Text += " (không có quyền)";
            if (!auditView) _tabs.TabPages[3].Text += " (không có quyền)";
            if (userView) { LoadUsers(); LoadUserOverrides(); LoadUserScopes(); }
            if (roleView) LoadRoles();
            if (auditView) LoadAudit();
        }

        private void LoadUsers() { _users.DataSource = _repository.ListUsers(); }
        private void LoadRoles()
        {
            _roles.DataSource = _repository.ListRoles();
            if (_rolesView.RowCount > 0) _rolesView.FocusedRowHandle = 0;
            LoadPermissions(); LoadScopes();
        }
        private int? SelectedRoleId()
        {
            object v = FocusedValue(_rolesView, "RoleId");
            return v == null ? (int?)null : Convert.ToInt32(v);
        }
        private long? SelectedUserId()
        {
            object v = FocusedValue(_usersView, "UserId");
            return v == null ? (long?)null : Convert.ToInt64(v);
        }
        private void LoadPermissions()
        {
            var roleId = SelectedRoleId();
            if (!roleId.HasValue) { _rolePermissions.SetData(null); return; }
            _rolePermissions.ReadOnly = !_session.HasPermission("ADMIN.ROLE_MANAGEMENT", "ADMIN");
            _rolePermissions.SetData(_repository.ListPermissions(roleId.Value));
        }
        private void LoadScopes()
        {
            var roleId = SelectedRoleId();
            _scopes.DataSource = roleId.HasValue ? _repository.ListRoleScopes(roleId.Value) : null;
        }
        private void LoadAudit() { _audit.DataSource = _repository.ListAudit(); }

        private void LoadUserOverrides()
        {
            var userId = SelectedUserId();
            UpdateSelectedUserLabel();
            if (!userId.HasValue) { _userPermissions.SetData(null); return; }
            _userPermissions.ReadOnly = !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN");
            _userPermissions.SetData(_repository.ListUserOverrides(userId.Value));
        }

        private void UpdateSelectedUserLabel()
        {
            object name = FocusedValue(_usersView, "UserName");
            object display = FocusedValue(_usersView, "DisplayName");
            _selectedUserLabel.Text = name == null
                ? "Chưa chọn tài khoản."
                : "Tài khoản đang chỉnh: " + name + (display == null ? string.Empty : "  —  " + display);
        }

        private void LoadUserScopes()
        {
            var userId = SelectedUserId();
            _userScopes.DataSource = userId.HasValue ? _repository.ListUserScopes(userId.Value) : null;
        }

        /// <summary>Lưu một ô của ma trận vai trò. Lỗi -> báo người dùng, ma trận tự nạp lại.</summary>
        private bool SaveRolePermissionCell(string functionCode, string actionCode, object value)
        {
            var roleId = SelectedRoleId();
            if (!roleId.HasValue) return false;
            try
            {
                _repository.SaveRolePermission(roleId.Value, functionCode, actionCode,
                    Convert.ToBoolean(value), _session.UserId);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>Lưu một ô của ma trận quyền riêng (INHERIT -> xóa override).</summary>
        private bool SaveUserOverrideCell(string functionCode, string actionCode, object value)
        {
            var userId = SelectedUserId();
            if (!userId.HasValue) return false;
            try
            {
                string choice = Convert.ToString(value);
                string effect = choice == PermissionMatrixControl.Inherit ? null : choice;
                _repository.SaveUserOverride(userId.Value, functionCode, actionCode, effect, _session.UserId);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void AddUserScope(object sender, EventArgs e)
        {
            var userId = SelectedUserId();
            if (!userId.HasValue || !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            using (var dialog = new RoleScopeDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _repository.SaveUserScope(userId.Value, dialog.FunctionCode, dialog.ScopeType,
                        dialog.ScopeValue, dialog.SiteCode, dialog.IsAllowed, _session.UserId);
                    LoadUserScopes();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void DeleteUserScope(object sender, EventArgs e)
        {
            var userId = SelectedUserId();
            if (!userId.HasValue || _userScopesView.FocusedRowHandle < 0 ||
                !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            if (MessageBox.Show(this, "Xóa phạm vi dữ liệu riêng đang chọn?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                long scopeId = Convert.ToInt64(_userScopesView.GetFocusedRowCellValue("UserDataScopeId"));
                _repository.DeleteUserScope(scopeId, _session.UserId);
                LoadUserScopes();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }



        private void CreateUser(object sender, EventArgs e)
        {
            if (!_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            using (var dialog = new UserCreateDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { _repository.CreateUser(dialog.UserName, dialog.DisplayName, dialog.EmployeeCode,
                    dialog.Email, dialog.Password, _session.UserId); LoadUsers(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void AssignRoles(object sender, EventArgs e)
        {
            var userId = SelectedUserId();
            if (!userId.HasValue) return;
            if (!_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            using (var dialog = new UserRolesDialog(_repository.ListUserRoles(userId.Value)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    // Chỉ gửi những vai trò người dùng thực sự đổi (so với giá trị gốc khi mở hộp thoại),
                    // gán thêm trước rồi mới gỡ để không rơi vào trạng thái thiếu quyền giữa chừng.
                    var changed = new System.Collections.Generic.List<DataRow>();
                    foreach (DataRow row in dialog.Roles.Rows)
                    {
                        if (row.RowState != DataRowState.Modified) continue;
                        if (Convert.ToBoolean(row["IsAssigned", DataRowVersion.Original]) ==
                            Convert.ToBoolean(row["IsAssigned", DataRowVersion.Current])) continue;
                        changed.Add(row);
                    }
                    foreach (DataRow row in changed.OrderByDescending(r => Convert.ToBoolean(r["IsAssigned"])))
                        _repository.AssignUserRole(userId.Value, Convert.ToInt32(row["RoleId"]),
                            Convert.ToBoolean(row["IsAssigned"]), _session.UserId);
                    LoadUsers();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void ToggleUser(object sender, EventArgs e)
        {
            var userId = SelectedUserId();
            if (!userId.HasValue || !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            bool active = !Convert.ToBoolean(_usersView.GetFocusedRowCellValue("IsActive"));
            try { _repository.SetUserActive(userId.Value, active, _session.UserId); LoadUsers(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ResetPassword(object sender, EventArgs e)
        {
            var userId = SelectedUserId();
            if (!userId.HasValue || !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            string password = Prompt("Đặt mật khẩu mới (tối thiểu 12 ký tự):", "Đặt lại mật khẩu");
            if (password == null) return;
            try { _repository.ResetPassword(userId.Value, password, _session.UserId); LoadUsers(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void CreateRole(object sender, EventArgs e)
        {
            if (!_session.HasPermission("ADMIN.ROLE_MANAGEMENT", "ADMIN")) return;
            string code = Prompt("Mã vai trò (A-Z, 0-9, _):", "Tạo vai trò");
            if (string.IsNullOrWhiteSpace(code)) return;
            string name = Prompt("Tên vai trò:", "Tạo vai trò");
            if (string.IsNullOrWhiteSpace(name)) return;
            try { _repository.CreateRole(code.Trim().ToUpperInvariant(), name.Trim(), null, _session.UserId); LoadRoles(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void AddScope(object sender, EventArgs e)
        {
            var roleId = SelectedRoleId();
            if (!roleId.HasValue || !_session.HasPermission("ADMIN.ROLE_MANAGEMENT", "ADMIN")) return;
            using (var dialog = new RoleScopeDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { _repository.SaveRoleScope(roleId.Value, dialog.FunctionCode, dialog.ScopeType,
                    dialog.ScopeValue, dialog.SiteCode, dialog.IsAllowed, _session.UserId); LoadScopes(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private static string Prompt(string message, string title)
        {
            using (var form = new Form { Text = title, Width = 440, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                var label = new Label { Text = message, Left = 12, Top = 12, Width = 400 };
                var input = new TextBox { Left = 12, Top = 40, Width = 400 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 250, Top = 75, Width = 75 };
                var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Left = 335, Top = 75, Width = 75 };
                form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? input.Text : null;
            }
        }

        private sealed class UserCreateDialog : Form
        {
            private readonly TextBox[] _fields = { new TextBox(), new TextBox(), new TextBox(), new TextBox(), new TextBox() };
            private readonly string[] _labels = { "Tên đăng nhập", "Họ tên", "Mã nhân viên (nếu có)", "Email", "Mật khẩu ban đầu" };
            public string UserName => _fields[0].Text.Trim();
            public string DisplayName => _fields[1].Text.Trim();
            public string EmployeeCode => Empty(_fields[2].Text);
            public string Email => Empty(_fields[3].Text);
            public string Password => _fields[4].Text;
            public UserCreateDialog()
            {
                Text = "Tạo tài khoản"; Width = 520; Height = 350; StartPosition = FormStartPosition.CenterParent;
                for (int i=0;i<_fields.Length;i++)
                {
                    Controls.Add(new Label { Text=_labels[i], Left=15, Top=18+i*48, Width=180 });
                    _fields[i].SetBounds(200,15+i*48,285,26); Controls.Add(_fields[i]);
                }
                _fields[4].UseSystemPasswordChar = true;
                var ok=new Button { Text="Tạo",Left=310,Top=265,Width=80,DialogResult=DialogResult.OK };
                var cancel=new Button { Text="Hủy",Left=400,Top=265,Width=80,DialogResult=DialogResult.Cancel };
                Controls.Add(ok);Controls.Add(cancel);AcceptButton=ok;CancelButton=cancel;
            }
            private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed class UserRolesDialog : XtraForm
        {
            public DataTable Roles { get; private set; }
            public UserRolesDialog(DataTable roles)
            {
                Roles = roles; Text = "Gán vai trò"; Width = 430; Height = 500; StartPosition = FormStartPosition.CenterParent;
                var grid = FormSecurityAdministration.Grid();
                var view = FormSecurityAdministration.ViewOf(grid);
                grid.DataSource = Roles;
                Controls.Add(grid);

                var check = new RepositoryItemCheckEdit();
                check.EditValueChanged += (s, e) => view.PostEditor();
                grid.RepositoryItems.Add(check);
                view.OptionsBehavior.Editable = true;
                view.OptionsFind.AlwaysVisible = false;
                foreach (DevExpress.XtraGrid.Columns.GridColumn c in view.Columns)
                    c.OptionsColumn.AllowEdit = c.FieldName == "IsAssigned";
                if (view.Columns["IsAssigned"] != null)
                {
                    view.Columns["IsAssigned"].Caption = "Đã gán";
                    view.Columns["IsAssigned"].ColumnEdit = check;
                }

                var ok = new SimpleButton { Text = "Lưu", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK };
                // Chốt ô đang sửa trước khi form đóng để DataRow có RowState đúng.
                ok.Click += (s, e) => { view.CloseEditor(); view.UpdateCurrentRow(); };
                Controls.Add(ok); AcceptButton = ok;
            }
        }

        private sealed class RoleScopeDialog : Form
        {
            private readonly System.Windows.Forms.ComboBox _type = new System.Windows.Forms.ComboBox();
            private readonly TextBox _value=new TextBox();
            private readonly TextBox _function=new TextBox();
            private readonly TextBox _site=new TextBox();
            private readonly CheckBox _allowed=new CheckBox { Text="Cho phép",Checked=true };
            public string ScopeType => Convert.ToString(_type.SelectedItem);
            public string ScopeValue => _value.Text.Trim();
            public string FunctionCode => string.IsNullOrWhiteSpace(_function.Text)?null:_function.Text.Trim();
            public string SiteCode => string.IsNullOrWhiteSpace(_site.Text)?null:_site.Text.Trim();
            public bool IsAllowed => _allowed.Checked;
            public RoleScopeDialog()
            {
                Text="Thêm phạm vi dữ liệu";Width=520;Height=330;StartPosition=FormStartPosition.CenterParent;
                _type.DropDownStyle=ComboBoxStyle.DropDownList;_type.Items.AddRange(new object[]{"ALL","SITE","DEPARTMENT","WAREHOUSE","LINE","PROCESS","OWN"});_type.SelectedIndex=1;
                Add("Loại phạm vi",_type,20);Add("Giá trị (* với ALL)",_value,70);Add("FunctionCode (trống = tất cả)",_function,120);Add("SiteCode (trống = mọi site)",_site,170);
                _allowed.SetBounds(200,215,180,25);Controls.Add(_allowed);
                var ok=new Button{Text="Lưu",Left=300,Top=250,Width=80,DialogResult=DialogResult.OK};
                var cancel=new Button{Text="Hủy",Left=390,Top=250,Width=80,DialogResult=DialogResult.Cancel};
                Controls.Add(ok);Controls.Add(cancel);AcceptButton=ok;CancelButton=cancel;
            }
            private void Add(string label,Control control,int top)
            {
                Controls.Add(new Label{Text=label,Left=15,Top=top+4,Width=180});
                control.SetBounds(200,top,285,26);Controls.Add(control);
            }
        }
    }
}