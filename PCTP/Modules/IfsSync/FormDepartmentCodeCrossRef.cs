using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Security;

namespace PCTP.Modules.IfsSync
{
    /// <summary>
    /// Đối chiếu Work Center IFS (tới 5 ký tự, vd 1100E) với mã bộ phận 4 ký tự dùng trong LOT. Lưới liệt kê MỌI Work Center đã đồng bộ từ IFS
    /// (Work Center mới: đồng bộ nhóm WORK_CENTER ở menu Đồng bộ dữ liệu IFS rồi TẢI LẠI); mỗi dòng chưa có mã được sinh mã (một dòng hoặc tất cả) hoặc sửa tay.
    /// Đây là bước ① của chuỗi khai báo: các màn Liên kết Work Center và Khai báo chuyền chỉ chọn mã đã khai báo ở đây.
    /// Quyền MES.SITE_DEPARTMENT_LINE (VIEW xem, EDIT sửa, theo Site).
    /// </summary>
    public sealed class FormDepartmentCodeCrossRef : XtraForm
    {
        private readonly string _site;
        private readonly ISiteDepartmentLineRepository _repository;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private readonly CheckEdit _onlyMissing = new CheckEdit { Text = "Chỉ Work Center chưa có mã", Width = 200 };

        private static bool CanEdit
        {
            get { return UserSession.Current != null && UserSession.Current.HasPermission(SiteDepartmentLineRepository.FunctionCode, "EDIT"); }
        }

        public FormDepartmentCodeCrossRef(string siteCode) : this(siteCode, new SiteDepartmentLineRepository()) { }

        public FormDepartmentCodeCrossRef(string siteCode, ISiteDepartmentLineRepository repository)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _site = siteCode.Trim().ToUpperInvariant();
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            Text = "Work Center IFS ↔ mã bộ phận 4 ký tự (LOT) - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1150, 680);
            BuildUi();
            Load += (s, e) => Reload();
        }

        private void BuildUi()
        {
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            var reload = new SimpleButton { Text = "TẢI LẠI", Width = 90, Height = 30 };
            reload.Click += (s, e) => Reload();
            var generate = new SimpleButton { Text = "Sinh mã (dòng chọn)", Width = 150, Height = 30, Enabled = CanEdit };
            var generateAll = new SimpleButton { Text = "Sinh mã tất cả chưa có…", Width = 170, Height = 30, Enabled = CanEdit };
            var edit = new SimpleButton { Text = "Sửa…", Width = 80, Height = 30, Enabled = CanEdit };
            var delete = new SimpleButton { Text = "Xóa", Width = 80, Height = 30, Enabled = CanEdit };
            if (!CanEdit)
            {
                string tip = "Bạn chỉ có quyền xem; cần " + SiteDepartmentLineRepository.FunctionCode + " / EDIT.";
                generate.ToolTip = tip; generateAll.ToolTip = tip; edit.ToolTip = tip; delete.ToolTip = tip;
            }
            generate.ToolTip = generate.ToolTip ?? "Sinh mã bộ phận 4 ký tự cho Work Center đang chọn (chưa có mã).";
            generate.Click += (s, e) => GenerateFocused();
            generateAll.Click += (s, e) => GenerateAllMissing();
            edit.Click += (s, e) => EditRow(_view.GetFocusedDataRow(), false);
            delete.Click += (s, e) => DeleteRow(_view.GetFocusedDataRow());
            _onlyMissing.CheckedChanged += (s, e) => ApplyFilter();
            foreach (Control c in new Control[] { reload, generate, generateAll, edit, delete, _onlyMissing }) bar.Controls.Add(c);

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(_status);
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Appearance.ForeColor = error ? Color.Firebrick : Color.FromArgb(70, 85, 100);
        }

        private void Reload()
        {
            try
            {
                DataTable table = _repository.GetWorkCenterCodeStatus(_site);
                table.Columns.Add("SuggestedCode", typeof(string));
                table.Columns.Add("CodeText", typeof(string));
                int missing = 0;
                foreach (DataRow r in table.Rows)
                {
                    bool has = Cell(r, "CodeStatus") == "HAS_CODE";
                    r["CodeText"] = has ? "Đã có mã" : "Chưa có mã";
                    if (!has)
                    {
                        missing++;
                        string pad = SiteDepartmentLineRepository.DepartmentCodeFromWorkCenter(Cell(r, "WorkCenterNo"));
                        r["SuggestedCode"] = pad ?? "(chọn khi sinh mã)";
                    }
                }
                _grid.DataSource = table.DefaultView;
                Caption("WorkCenterNo", "Work Center IFS"); Caption("IfsWorkCenterDescription", "Mô tả Work Center IFS");
                Caption("IsPresentInIfs", "Còn trong IFS"); Caption("IfsProductionLine", "Chuyền IFS");
                Caption("CodeText", "Trạng thái mã"); Caption("DepartmentCode", "Mã bộ phận (LOT)"); Caption("SuggestedCode", "Gợi ý mã");
                Caption("Description", "Mô tả"); Caption("LotDepartmentName", "Tên bộ phận LOT"); Caption("LotLineCode", "Chuyền LOT");
                Caption("LotDepartmentStatus", "Bộ phận trong PCTP"); Caption("IsLegacySeed", "Mã có sẵn");
                Caption("CreatedAtUtc", "Tạo lúc (UTC)"); Caption("CreatedBy", "Người tạo");
                Caption("UpdatedAtUtc", "Sửa lúc (UTC)"); Caption("UpdatedBy", "Người sửa");
                foreach (string hidden in new[] { "SiteCode", "CodeStatus" })
                    if (_view.Columns[hidden] != null) _view.Columns[hidden].Visible = false;
                ApplyFilter();
                _view.BestFitColumns();
                SetStatus(table.Rows.Count == 0
                    ? "Site " + _site + " chưa có Work Center IFS. Đồng bộ dữ liệu IFS nhóm WORK_CENTER (và SITE) rồi TẢI LẠI."
                    : table.Rows.Count + " Work Center IFS, " + missing + " chưa có mã bộ phận. Work Center mới của IFS: đồng bộ nhóm WORK_CENTER rồi TẢI LẠI. "
                      + "Bộ phận trong PCTP = NOT_DEFINED khi mã 4 ký tự chưa có trong dbo.FVN_ProductionDepartment (sẽ tạo khi Liên kết Work Center).",
                    table.Rows.Count == 0);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void ApplyFilter()
        {
            var view = _grid.DataSource as DataView;
            if (view == null) return;
            view.RowFilter = _onlyMissing.Checked ? "CodeStatus = 'NO_CODE'" : string.Empty;
        }

        private void GenerateFocused()
        {
            if (!CanEdit) return;
            DataRow row = _view.GetFocusedDataRow();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            string wc = Cell(row, "WorkCenterNo");
            try
            {
                if (Cell(row, "CodeStatus") == "HAS_CODE") { SetStatus("Work Center " + wc + " đã có mã " + Cell(row, "DepartmentCode") + ". Dùng Sửa… để đổi.", true); return; }
                DataTable t = _repository.GenerateDepartmentCode(_site, wc, null, null, false);
                string code = t.Rows.Count > 0 ? Convert.ToString(t.Rows[0]["DepartmentCode"]).Trim() : string.Empty;
                Reload();
                SetStatus("Đã sinh mã " + code + " cho Work Center " + wc + ".", false);
            }
            catch (Exception ex) { Fail("Sinh mã bộ phận", ex); }
        }

        private void GenerateAllMissing()
        {
            if (!CanEdit) return;
            try
            {
                var targets = new List<string>();
                var table = (_grid.DataSource as DataView)?.Table;
                if (table != null)
                    foreach (DataRow r in table.Rows)
                        if (Cell(r, "CodeStatus") == "NO_CODE" && r["IsPresentInIfs"] != DBNull.Value && Convert.ToBoolean(r["IsPresentInIfs"]))
                            targets.Add(Cell(r, "WorkCenterNo"));
                if (targets.Count == 0) { SetStatus("Mọi Work Center đã có mã bộ phận.", false); return; }
                if (XtraMessageBox.Show(this, "Sinh mã bộ phận 4 ký tự cho " + targets.Count + " Work Center chưa có mã?\r\n"
                        + "Work Center không sinh được mã (ví dụ ký tự lạ) sẽ được liệt kê để nhập tay bằng Sửa….",
                        "Sinh mã bộ phận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                int ok = 0;
                var failed = new List<string>();
                foreach (string wc in targets)
                {
                    try { _repository.GenerateDepartmentCode(_site, wc, null, null, false); ok++; }
                    catch (InvalidOperationException ex) { failed.Add(wc + ": " + ex.Message); }
                }
                Reload();
                if (failed.Count == 0) SetStatus("Đã sinh mã cho " + ok + " Work Center.", false);
                else
                {
                    SetStatus("Đã sinh " + ok + ", còn " + failed.Count + " Work Center cần nhập tay (Sửa…).", true);
                    XtraMessageBox.Show(this, string.Join("\r\n", failed), "Chưa sinh được mã", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) { Fail("Sinh mã bộ phận", ex); }
        }

        private void Caption(string field, string caption)
        {
            var col = _view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        private static string Cell(DataRow r, string col)
        {
            return r == null || !r.Table.Columns.Contains(col) || r.IsNull(col) ? string.Empty : Convert.ToString(r[col]).Trim();
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void EditRow(DataRow row, bool generateFirst)
        {
            if (!CanEdit) return;
            try
            {
                DataTable candidates = row == null ? _repository.GetCandidates(_site) : null;
                using (var dlg = new EditDialog(_site, _repository, candidates, row, generateFirst))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                }
                Reload();
                SetStatus("Đã lưu cặp Work Center / mã bộ phận.", false);
            }
            catch (Exception ex) { Fail("Lưu cặp mã bộ phận", ex); }
        }

        private void DeleteRow(DataRow row)
        {
            if (!CanEdit) return;
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            string wc = Cell(row, "WorkCenterNo");
            try
            {
                if (Cell(row, "CodeStatus") != "HAS_CODE") { SetStatus("Work Center " + wc + " chưa có mã để xóa.", true); return; }
                if (XtraMessageBox.Show(this, "Xóa dòng đối chiếu " + wc + " → " + Cell(row, "DepartmentCode") + "?\r\nChỉ xóa được khi mã bộ phận chưa được định nghĩa trong PCTP.",
                        "Mã bộ phận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _repository.DeleteDepartmentCodeCrossRef(_site, wc);
                Reload();
                SetStatus("Đã xóa dòng " + wc + ".", false);
            }
            catch (Exception ex) { Fail("Xóa cặp mã bộ phận", ex); }
        }

        private sealed class EditDialog : XtraForm
        {
            private readonly string _site;
            private readonly ISiteDepartmentLineRepository _repository;
            private readonly bool _isNew;
            private readonly List<string> _workCenters = new List<string>();
            private readonly ComboBoxEdit _wc = new ComboBoxEdit();
            private readonly TextEdit _dept = new TextEdit();
            private readonly TextEdit _description = new TextEdit();

            public EditDialog(string site, ISiteDepartmentLineRepository repository, DataTable candidates, DataRow row, bool generateFirst)
            {
                _site = site;
                _repository = repository;
                _isNew = row == null;
                base.Text = (_isNew ? "Thêm" : "Sửa") + " cặp Work Center / bộ phận - Site " + site;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(520, 230);

                _wc.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                if (_isNew)
                {
                    if (candidates != null)
                        foreach (DataRow c in candidates.Rows)
                        {
                            string no = Cell(c, "WorkCenterNo");
                            if (no.Length == 0) continue;
                            string desc = Cell(c, "Description");
                            _workCenters.Add(no);
                            _wc.Properties.Items.Add(desc.Length > 0 ? no + " — " + desc : no);
                        }
                    _wc.SelectedIndexChanged += (s, e) => { _dept.Text = string.Empty; if (generateFirst) Generate(true); };
                }
                else
                {
                    string no = Cell(row, "WorkCenterNo");
                    _workCenters.Add(no);
                    _wc.Properties.Items.Add(no);
                    _wc.SelectedIndex = 0;
                    _wc.Enabled = false;
                    _dept.Text = Cell(row, "DepartmentCode");
                    _description.Text = Cell(row, "Description");
                }
                _dept.Properties.CharacterCasing = CharacterCasing.Upper; _dept.Properties.MaxLength = 4;
                _description.Properties.MaxLength = 250;

                AddRow(0, "Work Center IFS:", _wc);
                AddRow(1, "Mã bộ phận (đúng 4):", _dept);
                AddRow(2, "Mô tả (trống = lấy từ IFS):", _description);

                var suggest = new SimpleButton { Text = "Sinh gợi ý mã", Width = 110, Location = new Point(170, 18 + 3 * 36) };
                suggest.Click += (s, e) => Generate(true);
                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(310, 186) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(410, 186), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Save();
                Controls.Add(suggest); Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }

            private string SelectedWorkCenter
            {
                get { return _wc.SelectedIndex >= 0 && _wc.SelectedIndex < _workCenters.Count ? _workCenters[_wc.SelectedIndex] : null; }
            }

            private void AddRow(int index, string caption, BaseEdit edit)
            {
                int y = 18 + index * 36;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 155 });
                edit.Location = new Point(170, y); edit.Width = 330;
                Controls.Add(edit);
            }

            private void Generate(bool preview)
            {
                try
                {
                    string wc = SelectedWorkCenter;
                    if (wc == null) throw new InvalidOperationException("Chọn Work Center IFS.");
                    DataTable t = _repository.GenerateDepartmentCode(_site, wc, null, _description.Text, preview);
                    if (t.Rows.Count == 0) throw new InvalidOperationException("Không nhận được mã từ SQL.");
                    _dept.Text = Convert.ToString(t.Rows[0]["DepartmentCode"]).Trim();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, ex.GetBaseException().Message, base.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            private void Save()
            {
                try
                {
                    string wc = SelectedWorkCenter;
                    if (wc == null) throw new InvalidOperationException("Chọn Work Center IFS.");
                    _repository.SaveDepartmentCodeCrossRef(_site, wc, _dept.Text, _description.Text);
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, ex.GetBaseException().Message, base.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            private static string Cell(DataRow r, string col)
            {
                return r == null || !r.Table.Columns.Contains(col) || r.IsNull(col) ? string.Empty : Convert.ToString(r[col]).Trim();
            }
        }
    }
}
