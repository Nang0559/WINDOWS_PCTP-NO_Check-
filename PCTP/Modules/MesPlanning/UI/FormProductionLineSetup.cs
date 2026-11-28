using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Security;
using PCTP.Shared.MesControls;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Khai báo chuyền sản xuất theo Site. Danh sách chuyền lấy từ IFS (menu Đồng bộ dữ liệu IFS, nhóm PRODUCTION_LINE);
    /// màn hình này gán mã chuyền LOT (3 ký tự) + mã bộ phận (4 ký tự) cho chuyền IFS để màn Upload kế hoạch và tạo LOT/QR dùng được chuyền đó.
    /// Hai mã này KHÔNG nhập tay: chỉ chọn trong danh sách đã khai báo ở các màn trước (thứ tự ① Work Center ↔ mã bộ phận,
    /// ② Mã chuyền 5 ↔ 3, ③ Liên kết Work Center, ④ màn này). Thiếu mã nào thì mở đúng màn đó ngay trong hộp thoại Khai báo / Sửa.
    /// </summary>
    public sealed class FormProductionLineSetup : XtraForm
    {
        private string _site;
        private const string Function = "MES.PRODUCTION_LINE_SETUP";
        private readonly FvnSiteLookupEdit _siteLookup = new FvnSiteLookupEdit();
        private FvnConsoleScreenFrame _frame;
        private readonly ProductionLineSetupService _service;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _flow = new LabelControl
        {
            Dock = DockStyle.Top, Height = 28, AutoSizeMode = LabelAutoSizeMode.None,
            Text = "Thứ tự khai báo: ① Work Center ↔ mã bộ phận → ② Mã chuyền 5 ↔ 3 → ③ Liên kết Work Center → ④ Khai báo chuyền (màn này: chỉ chọn từ danh sách)."
        };
        private readonly CheckEdit _onlyUnmapped = new CheckEdit { Text = "Chỉ chuyền chưa khai báo", Width = 190 };
        private SimpleButton _editButton;
        private SimpleButton _deactivateButton;
        private bool CanConfigure { get { return UserSession.Current != null && UserSession.Current.HasPermission("MES.PRODUCTION_LINE_SETUP", "CREATE"); } }

        public FormProductionLineSetup(string siteCode)
            : this(siteCode, new ProductionLineSetupService(new ProductionLineSetupRepository())) { }

        public FormProductionLineSetup(string siteCode, ProductionLineSetupService service)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _site = siteCode.Trim().ToUpperInvariant();
            _service = service ?? throw new ArgumentNullException("service");
            _view = new GridView(_grid);
            Text = "Khai báo chuyền sản xuất - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 600);
            BuildUi();
            Load += (s, e) => Reload();
        }

        private void BuildUi()
        {
            FvnConsoleScreen.StyleGrid(_grid, _view);
            _view.DoubleClick += (s, e) => EditFocused();

            // Site chuẩn: dbo.vFVN_Site ∩ phạm vi MES.PRODUCTION_LINE_SETUP; Site truyền từ menu được chọn sẵn.
            _siteLookup.Width = 150;
            _siteLookup.LoadForFunction(Function, false, _site);
            if (!string.IsNullOrEmpty(_siteLookup.SiteCode)) _site = _siteLookup.SiteCode.ToUpperInvariant();
            _siteLookup.SiteChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(_siteLookup.SiteCode)) return;
                _site = _siteLookup.SiteCode.ToUpperInvariant();
                Text = "Khai báo chuyền sản xuất - Site " + _site;
                Reload();
            };

            var reload = new SimpleButton();
            FvnConsoleScreen.StyleButton(reload, "TẢI LẠI", 100, (s, e) => Reload());
            _editButton = new SimpleButton();
            FvnConsoleScreen.StyleButton(_editButton, "KHAI BÁO / SỬA…", 150, (s, e) => EditFocused(), CanConfigure);
            _deactivateButton = new SimpleButton();
            FvnConsoleScreen.StyleButton(_deactivateButton, "NGỪNG DÙNG", 120, (s, e) => DeactivateFocused(), CanConfigure);
            _editButton.ToolTip = CanConfigure ? "Khai báo / sửa mapping IFS → LOT" : "Bạn chỉ có quyền xem; cần MES.PRODUCTION_LINE_SETUP / CREATE.";
            _deactivateButton.ToolTip = _editButton.ToolTip;
            _onlyUnmapped.CheckedChanged += (s, e) => Reload();
            _onlyUnmapped.Margin = new Padding(8, 4, 0, 0);

            var filter = FvnConsoleScreen.CreateFilterCard(
                FvnConsoleScreen.Caption("Site"), _siteLookup, reload, _editButton, _deactivateButton, _onlyUnmapped);

            _flow.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            var gridCard = new FvnCardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            gridCard.Controls.Add(_grid);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = FvnConsoleTheme.Background };
            body.Controls.Add(gridCard);
            body.Controls.Add(_flow);

            _frame = FvnConsoleScreen.Compose(this,
                "KHAI BÁO CHUYỀN SẢN XUẤT THEO SITE",
                "Gán chuyền IFS sang mã chuyền LOT (3 ký tự) + mã bộ phận (4 ký tự) | Site từ dbo.vFVN_Site",
                filter, FvnConsoleScreen.FilterCardHeight, body);
            if (_siteLookup.SiteCount == 0)
                SetStatus(string.IsNullOrEmpty(_siteLookup.LastError)
                    ? "Không tìm thấy Site trong phạm vi được cấp cho tài khoản." : _siteLookup.LastError, true);
        }

        private void SetStatus(string text, bool error)
        {
            if (_frame != null) _frame.SetStatus(text, error);
        }

        private void Reload()
        {
            try
            {
                _frame.SetRuntime("Site " + _site, true);
                DataTable table = _service.List(_site);
                int lines = table.Rows.Count;
                int unmapped = 0;
                foreach (DataRow r in table.Rows)
                    if (!Convert.ToBoolean(r["IsMapped"]) || !Convert.ToBoolean(r["MapIsActive"])) unmapped++;
                if (_onlyUnmapped.Checked)
                    table.DefaultView.RowFilter = "IsMapped = 0 OR MapIsActive = 0";
                _grid.DataSource = table.DefaultView;
                SetCaption("SiteCode", "Site"); SetCaption("SiteName", "Tên Site (IFS)");
                SetCaption("SiteExistsInIfs", "Site còn trong IFS"); SetCaption("SiteIsActive", "Site MES đang dùng");
                SetCaption("ProductionLineCode", "Chuyền IFS"); SetCaption("Description", "Mô tả (IFS)");
                SetCaption("CalendarId", "Calendar"); SetCaption("TaktTime", "Takt time"); SetCaption("DailyOperatingMinutes", "Phút vận hành / ngày");
                SetCaption("ExistsInIfs", "Còn trong IFS"); SetCaption("IsKnownSite", "Site đã khai báo");
                SetCaption("LotLineCode", "Chuyền LOT (3 ký tự)"); SetCaption("DepartmentCode", "Bộ phận (4 ký tự)");
                SetCaption("IsMapped", "Đã khai báo"); SetCaption("MapIsActive", "Đang dùng"); SetCaption("SyncedAtUtc", "Đồng bộ IFS (UTC)");
                _view.BestFitColumns();
                bool siteInIfs = lines > 0 && !table.Rows[0].IsNull("SiteExistsInIfs") && Convert.ToBoolean(table.Rows[0]["SiteExistsInIfs"]);
                bool siteActive = lines > 0 && !table.Rows[0].IsNull("SiteIsActive") && Convert.ToBoolean(table.Rows[0]["SiteIsActive"]);
                string status = lines == 0
                    ? "Site " + _site + " chưa có chuyền IFS. Kiểm tra IFS.FVN_Site / IFS.FVN_ProductionLine và đồng bộ SITE + PRODUCTION_LINE."
                    : !siteInIfs ? "Site " + _site + " chưa được đồng bộ hoặc không còn trong IFS.FVN_Site."
                    : !siteActive ? "Site " + _site + " chưa được khai báo hoạt động trong dbo.FVN_PCTPSite."
                    : lines + " chuyền IFS của Site " + _site + ", " + unmapped + " chuyền chưa mapping LOT hoặc đã ngừng.";
                SetStatus(status, lines == 0 || !siteInIfs || !siteActive);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void SetCaption(string field, string caption)
        {
            var col = _view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        private DataRow Focused()
        {
            var rv = _view.GetFocusedRow() as DataRowView;
            return rv == null ? null : rv.Row;
        }

        private void EditFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            try
            {
                using (var dlg = new EditDialog(_site, _service, row))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    _service.Save(_site, dlg.LineCode, dlg.LotLineCode, dlg.DepartmentCode, dlg.IsActive);
                }
                Reload();
                SetStatus("Đã lưu khai báo chuyền.", false);
            }
            catch (Exception ex) { Fail("Lưu khai báo chuyền", ex); }
        }

        private void DeactivateFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            try
            {
                if (row.IsNull("LotLineCode") || !Convert.ToBoolean(row["MapIsActive"])) { SetStatus("Chuyền này chưa khai báo hoặc đã ngừng.", true); return; }
                string line = Convert.ToString(row["ProductionLineCode"]);
                if (XtraMessageBox.Show(this, "Ngừng dùng chuyền " + line + "?\r\nMàn Upload kế hoạch sẽ không còn hiện chuyền này.",
                        "Khai báo chuyền", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _service.Save(_site, line, Convert.ToString(row["LotLineCode"]), Convert.ToString(row["DepartmentCode"]), false);
                Reload();
                SetStatus("Đã ngừng dùng chuyền " + line + ".", false);
            }
            catch (Exception ex) { Fail("Ngừng dùng chuyền", ex); }
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class EditDialog : XtraForm
        {
            private const string UpstreamFunction = "MES.SITE_DEPARTMENT_LINE";
            private readonly string _site;
            private readonly string _ifsLine;
            private readonly ProductionLineSetupService _service;
            private readonly string _currentLot;
            private readonly string _currentDept;
            private readonly List<string> _lotCodes = new List<string>();
            private readonly List<string> _deptCodes = new List<string>();
            private readonly ComboBoxEdit _lot = new ComboBoxEdit();
            private readonly ComboBoxEdit _dept = new ComboBoxEdit();
            private readonly CheckEdit _active = new CheckEdit { Text = "Đang dùng" };
            private readonly LabelControl _hint = new LabelControl();
            private bool _loading;

            public string LineCode { get; private set; }
            public string LotLineCode { get; private set; }
            public string DepartmentCode { get; private set; }
            public bool IsActive { get; private set; }

            public EditDialog(string site, ProductionLineSetupService service, DataRow row)
            {
                _site = site;
                _service = service;
                LineCode = Convert.ToString(row["ProductionLineCode"]);
                _ifsLine = LineCode;
                Text = "Khai báo chuyền " + LineCode;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(580, 340);

                string description = row.IsNull("Description") ? string.Empty : Convert.ToString(row["Description"]);
                Controls.Add(new LabelControl { Text = "Chuyền IFS:", Location = new Point(12, 23), Width = 190 });
                Controls.Add(new LabelControl { Text = LineCode + (description.Length > 0 ? " — " + description : string.Empty),
                    Location = new Point(210, 23), AutoSizeMode = LabelAutoSizeMode.None, Width = 360 });

                _lot.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                _dept.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                AddRow(1, "Mã chuyền LOT (chọn từ danh sách):", _lot);
                AddRow(2, "Mã bộ phận (chọn từ danh sách):", _dept);
                _active.Location = new Point(210, 118); _active.Width = 200;
                Controls.Add(_active);

                _currentLot = row.IsNull("LotLineCode") ? null : Convert.ToString(row["LotLineCode"]).Trim();
                _currentDept = row.IsNull("DepartmentCode") ? null : Convert.ToString(row["DepartmentCode"]).Trim();
                _active.Checked = row.IsNull("MapIsActive") || Convert.ToBoolean(row["MapIsActive"]) || row.IsNull("LotLineCode");

                _hint.AutoSizeMode = LabelAutoSizeMode.None;
                _hint.Appearance.ForeColor = Color.Firebrick;
                _hint.Location = new Point(12, 150); _hint.Size = new Size(556, 62);
                Controls.Add(_hint);

                Controls.Add(UpstreamButton("① Mã bộ phận (Work Center)…", 12, () => new PCTP.Modules.IfsSync.FormDepartmentCodeCrossRef(_site)));
                Controls.Add(UpstreamButton("② Mã chuyền 5 ↔ 3…", 202, () => new PCTP.Modules.IfsSync.FormLineCodeCrossRef(_site)));
                Controls.Add(UpstreamButton("③ Liên kết Work Center…", 392, () => new PCTP.Modules.IfsSync.FormWorkCenterDepartmentLink(_site)));

                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(380, 296) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(480, 296), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;

                _lot.SelectedIndexChanged += (s, e) => { if (!_loading) LoadDepartments(null); };
                LoadChoices();
            }

            private SimpleButton UpstreamButton(string caption, int x, Func<XtraForm> create)
            {
                var b = new SimpleButton { Text = caption, Width = 178, Height = 28, Location = new Point(x, 232) };
                b.ToolTip = "Mở màn hình khai báo mã tương ứng; đóng lại để cập nhật danh sách chọn.";
                b.Click += (s, e) => OpenUpstream(create);
                return b;
            }

            private void OpenUpstream(Func<XtraForm> create)
            {
                if (UserSession.Current == null || !UserSession.Current.HasPermission(UpstreamFunction, "VIEW"))
                {
                    XtraMessageBox.Show(this, "Cần quyền " + UpstreamFunction + " / VIEW để mở màn hình khai báo mã.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (XtraForm form = create()) form.ShowDialog(this);
                LoadChoices();
            }

            private string SelectedLot
            {
                get { return _lot.SelectedIndex >= 0 && _lot.SelectedIndex < _lotCodes.Count ? _lotCodes[_lot.SelectedIndex] : null; }
            }

            private string SelectedDept
            {
                get { return _dept.SelectedIndex >= 0 && _dept.SelectedIndex < _deptCodes.Count ? _deptCodes[_dept.SelectedIndex] : null; }
            }

            private static string Field(DataRow r, string column)
            {
                return r.Table.Columns.Contains(column) && !r.IsNull(column) ? Convert.ToString(r[column]).Trim() : string.Empty;
            }

            private void LoadChoices()
            {
                string wantLot = SelectedLot ?? _currentLot;
                _loading = true;
                try
                {
                    _lotCodes.Clear();
                    _lot.Properties.Items.Clear();
                    DataTable lots = _service.ListLotLineChoices(_site, _ifsLine);
                    foreach (DataRow r in lots.Rows)
                    {
                        string code = Field(r, "LineCode"), name = Field(r, "LineName");
                        _lotCodes.Add(code);
                        _lot.Properties.Items.Add(name.Length > 0 ? code + " — " + name : code);
                    }
                    int index = wantLot == null ? -1 : _lotCodes.FindIndex(c => string.Equals(c, wantLot, StringComparison.OrdinalIgnoreCase));
                    if (index < 0 && _lotCodes.Count == 1) index = 0;
                    _lot.SelectedIndex = index;
                }
                finally { _loading = false; }
                LoadDepartments(SelectedDept ?? _currentDept);
            }

            private void LoadDepartments(string wantDept)
            {
                _deptCodes.Clear();
                _dept.Properties.Items.Clear();
                string lot = SelectedLot;
                if (lot != null)
                {
                    DataTable depts = _service.ListDepartmentChoices(_site, _ifsLine, lot);
                    foreach (DataRow r in depts.Rows)
                    {
                        string code = Field(r, "DepartmentCode"), name = Field(r, "DepartmentName"), wc = Field(r, "WorkCenterNo");
                        _deptCodes.Add(code);
                        _dept.Properties.Items.Add(code + (name.Length > 0 ? " — " + name : string.Empty) + (wc.Length > 0 ? "  (WC " + wc + ")" : string.Empty));
                    }
                }
                int index = wantDept == null ? -1 : _deptCodes.FindIndex(c => string.Equals(c, wantDept, StringComparison.OrdinalIgnoreCase));
                if (index < 0 && _deptCodes.Count == 1) index = 0;
                _dept.SelectedIndex = index;
                UpdateHint();
            }

            private void UpdateHint()
            {
                if (_lotCodes.Count == 0)
                    _hint.Text = "Chuyền IFS " + _ifsLine + " chưa có mã chuyền LOT để chọn. Khai báo mã ở ② Mã chuyền 5 ↔ 3, rồi ③ Liên kết Work Center vào chuyền này "
                               + "(mã bộ phận khai báo ở ①). Đóng màn đó để danh sách tự cập nhật.";
                else if (SelectedLot == null)
                    _hint.Text = "Chọn mã chuyền LOT trong danh sách.";
                else if (_deptCodes.Count == 0)
                    _hint.Text = "Chuyền LOT " + SelectedLot + " chưa có bộ phận nào đã liên kết Work Center. Khai báo mã bộ phận ở ①, rồi liên kết Work Center ở ③.";
                else
                    _hint.Text = string.Empty;
            }

            private void AddRow(int index, string caption, BaseEdit edit)
            {
                int y = 23 + index * 32;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 195 });
                edit.Location = new Point(210, y); edit.Width = 358;
                Controls.Add(edit);
            }

            private void Accept()
            {
                string lot = SelectedLot;
                string dept = SelectedDept;
                if (_active.Checked && lot == null)
                { XtraMessageBox.Show(this, "Chọn mã chuyền LOT từ danh sách.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_active.Checked && dept == null)
                { XtraMessageBox.Show(this, "Chọn mã bộ phận từ danh sách (bộ phận phải đã liên kết Work Center thuộc chuyền LOT này).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                LotLineCode = lot ?? _currentLot ?? string.Empty;
                DepartmentCode = dept ?? _currentDept ?? string.Empty;
                IsActive = _active.Checked;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
