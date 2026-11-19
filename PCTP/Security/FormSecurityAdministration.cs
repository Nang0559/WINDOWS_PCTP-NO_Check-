using System.Linq;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Security
{
    public sealed class FormSecurityAdministration : Form
    {
        private readonly SecurityAdministrationRepository _repository = new SecurityAdministrationRepository();
        private readonly UserSession _session;
        private readonly TabControl _tabs = new TabControl();
        private readonly DataGridView _users = Grid();
        private readonly DataGridView _roles = Grid();
        private readonly DataGridView _permissions = Grid();
        private readonly DataGridView _scopes = Grid();
        private readonly DataGridView _audit = Grid();
        private readonly DataGridView _userOverrides = Grid();
        private readonly DataGridView _userScopes = Grid();

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

        private static DataGridView Grid()
        {
            return new DataGridView {
                Dock = DockStyle.Fill, ReadOnly = false, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                RowHeadersVisible = false
            };
        }

        private void BuildUi()
        {
            _users.ReadOnly = true;
            _roles.ReadOnly = true;
            _scopes.ReadOnly = true;
            _audit.ReadOnly = true;
            _tabs.Dock = DockStyle.Fill;
            var userTab = new TabPage("Tài khoản");
            var roleTab = new TabPage("Vai trò và quyền");
            var auditTab = new TabPage("Nhật ký bảo mật");
            var accountAccessTab = new TabPage("Quyền riêng tài khoản");
            _tabs.TabPages.Add(userTab); _tabs.TabPages.Add(roleTab);
            _tabs.TabPages.Add(accountAccessTab); _tabs.TabPages.Add(auditTab);
            Controls.Add(_tabs);

            var userPanel = new Panel { Dock = DockStyle.Fill };
            var userButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
            AddButton(userButtons, "Tạo tài khoản", CreateUser);
            AddButton(userButtons, "Gán vai trò", AssignRoles);
            AddButton(userButtons, "Khóa / mở tài khoản", ToggleUser);
            AddButton(userButtons, "Đặt lại mật khẩu", ResetPassword);
            userPanel.Controls.Add(_users); userPanel.Controls.Add(userButtons); userTab.Controls.Add(userPanel);

            var rolePanel = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 190 };
            var roleTop = new Panel { Dock = DockStyle.Fill };
            var roleButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            AddButton(roleButtons, "Tạo vai trò", CreateRole);
            AddButton(roleButtons, "Làm mới", (s,e) => LoadRoles());
            roleTop.Controls.Add(_roles); roleTop.Controls.Add(roleButtons);
            var roleDetails = new TabControl { Dock = DockStyle.Fill };
            var permissionTab = new TabPage("Quyền theo chức năng");
            var scopeTab = new TabPage("Phạm vi dữ liệu");
            var scopePanel = new Panel { Dock = DockStyle.Fill };
            var scopeButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            AddButton(scopeButtons, "Thêm phạm vi", AddScope);
            AddButton(scopeButtons, "Làm mới", (s,e) => LoadScopes());
            scopePanel.Controls.Add(_scopes); scopePanel.Controls.Add(scopeButtons);
            permissionTab.Controls.Add(_permissions); scopeTab.Controls.Add(scopePanel);
            roleDetails.TabPages.Add(permissionTab); roleDetails.TabPages.Add(scopeTab);
            rolePanel.Panel1.Controls.Add(roleTop); rolePanel.Panel2.Controls.Add(roleDetails);
            roleTab.Controls.Add(rolePanel);

            var accountAccessPanel = new Panel { Dock = DockStyle.Fill };
            var accountAccessTabs = new TabControl { Dock = DockStyle.Fill };
            var accountPermissionTab = new TabPage("Quyền chức năng riêng");
            var accountScopeTab = new TabPage("Phạm vi dữ liệu riêng");
            var accountPermissionPanel = new Panel { Dock = DockStyle.Fill };
            var accountPermissionButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38 };
            AddButton(accountPermissionButtons, "Làm mới", (s,e) => LoadUserOverrides());
            accountPermissionPanel.Controls.Add(_userOverrides);
            accountPermissionPanel.Controls.Add(accountPermissionButtons);
            var accountScopePanel = new Panel { Dock = DockStyle.Fill };
            var accountScopeButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38 };
            AddButton(accountScopeButtons, "Thêm phạm vi", AddUserScope);
            AddButton(accountScopeButtons, "Xóa phạm vi", DeleteUserScope);
            AddButton(accountScopeButtons, "Làm mới", (s,e) => LoadUserScopes());
            accountScopePanel.Controls.Add(_userScopes);
            accountScopePanel.Controls.Add(accountScopeButtons);
            accountPermissionTab.Controls.Add(accountPermissionPanel);
            accountScopeTab.Controls.Add(accountScopePanel);
            accountAccessTabs.TabPages.Add(accountPermissionTab);
            accountAccessTabs.TabPages.Add(accountScopeTab);
            accountAccessPanel.Controls.Add(accountAccessTabs);
            var accountAccessNotice = new Label {
                Dock = DockStyle.Top, Height = 26,
                Text = "Quyền riêng và phạm vi riêng được nạp lại khi tài khoản đăng nhập phiên mới.",
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            };
            accountAccessPanel.Controls.Add(accountAccessNotice);
            accountAccessTab.Controls.Add(accountAccessPanel);

            var auditButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            AddButton(auditButtons, "Làm mới nhật ký", (s,e) => LoadAudit());
            auditTab.Controls.Add(_audit); auditTab.Controls.Add(auditButtons);

            _roles.SelectionChanged += (s,e) => { LoadPermissions(); LoadScopes(); };
            _users.SelectionChanged += (s,e) => { LoadUserOverrides(); LoadUserScopes(); };
            _permissions.CurrentCellDirtyStateChanged += (s,e) => {
                if (_permissions.IsCurrentCellDirty) _permissions.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _permissions.CellValueChanged += PermissionChanged;
            _userOverrides.CurrentCellDirtyStateChanged += (s,e) => {
                if (_userOverrides.IsCurrentCellDirty) _userOverrides.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _userOverrides.CellValueChanged += UserOverrideChanged;
        }

        private static void AddButton(Control parent, string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(4) };
            button.Click += handler; parent.Controls.Add(button);
        }

        private void RefreshAll()
        {
            bool userView = _session.HasPermission("ADMIN.USER_MANAGEMENT", "VIEW");
            bool roleView = _session.HasPermission("ADMIN.ROLE_MANAGEMENT", "VIEW");
            bool auditView = _session.HasPermission("ADMIN.AUDIT", "VIEW");
            _tabs.TabPages[0].Enabled = userView;
            _tabs.TabPages[1].Enabled = roleView;
            _tabs.TabPages[2].Enabled = userView;
            _tabs.TabPages[3].Enabled = auditView;
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
            if (_roles.Rows.Count > 0) _roles.Rows[0].Selected = true;
            LoadPermissions(); LoadScopes();
        }
        private int? SelectedRoleId()
        {
            if (_roles.CurrentRow == null) return null;
            return Convert.ToInt32(_roles.CurrentRow.Cells["RoleId"].Value);
        }
        private long? SelectedUserId()
        {
            if (_users.CurrentRow == null) return null;
            return Convert.ToInt64(_users.CurrentRow.Cells["UserId"].Value);
        }
        private void LoadPermissions()
        {
            var roleId = SelectedRoleId();
            if (!roleId.HasValue) { _permissions.DataSource = null; return; }
            _permissions.DataSource = _repository.ListPermissions(roleId.Value);
            _permissions.ReadOnly = !_session.HasPermission("ADMIN.ROLE_MANAGEMENT", "ADMIN");
            if (_permissions.Columns["IsAllowed"] != null)
                _permissions.Columns["IsAllowed"].ReadOnly = false;
            foreach (DataGridViewColumn column in _permissions.Columns)
                if (column.Name != "IsAllowed") column.ReadOnly = true;
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
            if (!userId.HasValue) { _userOverrides.DataSource = null; return; }
            var table = _repository.ListUserOverrides(userId.Value);
            if (!table.Columns.Contains("OverrideChoice")) table.Columns.Add("OverrideChoice", typeof(string));
            foreach (DataRow row in table.Rows)
                row["OverrideChoice"] = row["Effect"] == DBNull.Value ? "INHERIT" : Convert.ToString(row["Effect"]);
            _userOverrides.DataSource = table;
            if (_userOverrides.Columns["Effect"] != null) _userOverrides.Columns["Effect"].Visible = false;
            if (_userOverrides.Columns["IsAllowed"] != null) _userOverrides.Columns["IsAllowed"].Visible = false;
            if (_userOverrides.Columns["OverrideChoice"] != null)
            {
                var column = new DataGridViewComboBoxColumn {
                    Name = "OverrideChoice", HeaderText = "Quyền riêng (kế thừa / cho phép / từ chối)",
                    DataPropertyName = "OverrideChoice", DataSource = new[] { "INHERIT", "ALLOW", "DENY" },
                    FlatStyle = FlatStyle.Flat, Width = 220
                };
                int index = _userOverrides.Columns["OverrideChoice"].Index;
                _userOverrides.Columns.Remove("OverrideChoice");
                _userOverrides.Columns.Insert(index, column);
            }
            foreach (DataGridViewColumn column in _userOverrides.Columns)
                column.ReadOnly = column.Name != "OverrideChoice" ||
                    !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN");
        }

        private void LoadUserScopes()
        {
            var userId = SelectedUserId();
            _userScopes.DataSource = userId.HasValue ? _repository.ListUserScopes(userId.Value) : null;
            _userScopes.ReadOnly = true;
        }

        private void UserOverrideChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _userOverrides.Columns[e.ColumnIndex].Name != "OverrideChoice") return;
            var userId = SelectedUserId();
            if (!userId.HasValue) return;
            var row = _userOverrides.Rows[e.RowIndex];
            try
            {
                string choice = Convert.ToString(row.Cells["OverrideChoice"].Value);
                string effect = choice == "INHERIT" ? null : choice;
                _repository.SaveUserOverride(userId.Value,
                    Convert.ToString(row.Cells["FunctionCode"].Value),
                    Convert.ToString(row.Cells["ActionCode"].Value), effect, _session.UserId);
                LoadUserOverrides();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadUserOverrides();
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
            if (!userId.HasValue || _userScopes.CurrentRow == null ||
                !_session.HasPermission("ADMIN.USER_MANAGEMENT", "ADMIN")) return;
            if (MessageBox.Show(this, "Xóa phạm vi dữ liệu riêng đang chọn?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                long scopeId = Convert.ToInt64(_userScopes.CurrentRow.Cells["UserDataScopeId"].Value);
                _repository.DeleteUserScope(scopeId, _session.UserId);
                LoadUserScopes();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }



        private void PermissionChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _permissions.Columns[e.ColumnIndex].Name != "IsAllowed") return;
            var roleId = SelectedRoleId();
            if (!roleId.HasValue) return;
            try
            {
                var row = _permissions.Rows[e.RowIndex];
                _repository.SaveRolePermission(roleId.Value,
                    Convert.ToString(row.Cells["FunctionCode"].Value),
                    Convert.ToString(row.Cells["ActionCode"].Value),
                    Convert.ToBoolean(row.Cells["IsAllowed"].Value), _session.UserId);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); LoadPermissions(); }
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
            bool active = !Convert.ToBoolean(_users.CurrentRow.Cells["IsActive"].Value);
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

        private sealed class UserRolesDialog : Form
        {
            public DataTable Roles { get; private set; }
            public UserRolesDialog(DataTable roles)
            {
                Roles=roles; Text="Gán vai trò"; Width=430; Height=500; StartPosition=FormStartPosition.CenterParent;
                var grid=FormSecurityAdministration.Grid(); grid.DataSource=Roles; Controls.Add(grid);
                if(grid.Columns["IsAssigned"]!=null) grid.Columns["IsAssigned"].ReadOnly=false;
                foreach(DataGridViewColumn c in grid.Columns) if(c.Name!="IsAssigned") c.ReadOnly=true;
                grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
                var ok=new Button{Text="Lưu",Dock=DockStyle.Bottom,Height=36,DialogResult=DialogResult.OK};
                Controls.Add(ok);AcceptButton=ok;
            }
        }

        private sealed class RoleScopeDialog : Form
        {
            private readonly ComboBox _type=new ComboBox();
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