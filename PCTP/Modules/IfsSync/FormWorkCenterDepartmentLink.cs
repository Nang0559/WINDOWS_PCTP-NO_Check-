using System;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Security;

namespace PCTP.Modules.IfsSync
{
    /// <summary>
    /// Liên kết Work Center IFS với bộ phận PCTP của một Site. Danh sách Work Center lấy từ IFS (đồng bộ nhóm WORK_CENTER) kèm gợi ý
    /// chuyền / bộ phận của IFS. Mã bộ phận (4 ký tự) và mã chuyền LOT (3 ký tự) KHÔNG nhập hay tự sinh ở đây: lấy từ các mã đã khai báo ở màn
    /// Work Center ↔ mã bộ phận và màn Mã chuyền 5 ↔ 3 (thiếu thì mở đúng màn đó từ hộp thoại). IFS là chuẩn: bộ phận đang được mapping cho chuyền IFS A không nhận Work Center thuộc chuyền IFS B
    /// (SQL chặn); phần dưới màn hình liệt kê mọi bất nhất dữ liệu gốc của Site. Quyền MES.SITE_DEPARTMENT_LINE (VIEW + EDIT, theo Site).
    /// </summary>
    public sealed class FormWorkCenterDepartmentLink : XtraForm
    {
        private readonly string _site;
        private readonly ISiteDepartmentLineRepository _repository;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly MemoEdit _issues = new MemoEdit { Dock = DockStyle.Bottom, Height = 150 };
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private readonly CheckEdit _onlyUnlinked = new CheckEdit { Text = "Chỉ Work Center chưa liên kết", Width = 210 };
        private SimpleButton _link, _unlink;

        private static bool CanEdit
        {
            get { return UserSession.Current != null && UserSession.Current.HasPermission(SiteDepartmentLineRepository.FunctionCode, "EDIT"); }
        }

        public FormWorkCenterDepartmentLink(string siteCode) : this(siteCode, new SiteDepartmentLineRepository()) { }

        public FormWorkCenterDepartmentLink(string siteCode, ISiteDepartmentLineRepository repository)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _site = siteCode.Trim().ToUpperInvariant();
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            Text = "Liên kết Work Center IFS ↔ Bộ phận - Site " + _site;
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
            _view.DoubleClick += (s, e) => LinkFocused();
            _issues.Properties.ReadOnly = true;
            _issues.Properties.ScrollBars = ScrollBars.Vertical;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            bar.Controls.Add(Button("TẢI LẠI", 90, (s, e) => Reload()));
            _link = Button("Liên kết / Sửa…", 130, (s, e) => LinkFocused());
            _unlink = Button("Gỡ liên kết", 100, (s, e) => UnlinkFocused());
            string tip = CanEdit ? null : "Bạn chỉ có quyền xem; cần " + SiteDepartmentLineRepository.FunctionCode + " / EDIT.";
            foreach (SimpleButton b in new[] { _link, _unlink })
            {
                b.Enabled = CanEdit;
                if (tip != null) b.ToolTip = tip;
                bar.Controls.Add(b);
            }
            _onlyUnlinked.CheckedChanged += (s, e) => ApplyFilter();
            bar.Controls.Add(_onlyUnlinked);

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(_issues);
            Controls.Add(_status);
        }

        private static SimpleButton Button(string text, int width, EventHandler click)
        {
            var b = new SimpleButton { Text = text, Width = width, Height = 30 };
            b.Click += click;
            return b;
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
                DataTable table = _repository.GetCandidates(_site);
                table.Columns.Add("LinkText", typeof(string));
                int unlinked = 0;
                foreach (DataRow r in table.Rows)
                {
                    bool linked = Convert.ToString(r["LinkStatus"]) == "LINKED";
                    r["LinkText"] = linked ? "Đã liên kết" : "Chưa liên kết";
                    if (!linked) unlinked++;
                }
                _grid.DataSource = table.DefaultView;
                Caption("WorkCenterNo", "Work Center"); Caption("Description", "Mô tả (IFS)"); Caption("WorkCenterCode", "Mã WC");
                Caption("IsPresentInIfs", "Còn trong IFS"); Caption("IfsProductionLine", "Chuyền IFS"); Caption("IfsDepartmentNo", "Bộ phận IFS");
                Caption("SuggestedLineCode", "Gợi ý chuyền LOT"); Caption("SuggestedDepartmentCode", "Gợi ý bộ phận");
                Caption("LinkedDepartmentCode", "Bộ phận đã liên kết"); Caption("LinkedLineCode", "Chuyền LOT đã liên kết");
                Caption("LinkText", "Trạng thái"); Caption("SiteIsAllowListed", "Site được phép");
                if (_view.Columns["LinkStatus"] != null) _view.Columns["LinkStatus"].Visible = false;
                ApplyFilter();
                _view.BestFitColumns();

                _issues.Text = BuildIssueText(_repository.GetConsistency(_site));
                bool siteAllowed = table.Rows.Count > 0 && Convert.ToBoolean(table.Rows[0]["SiteIsAllowListed"]);
                string status = table.Rows.Count == 0
                    ? "Site " + _site + " chưa có Work Center IFS. Đồng bộ dữ liệu IFS nhóm WORK_CENTER (và SITE)."
                    : !siteAllowed ? "Site " + _site + " chưa được cho phép trong dbo.FVN_PCTPSite (menu Khai báo Site)."
                    : table.Rows.Count + " Work Center, " + unlinked + " chưa liên kết.";
                SetStatus(status, table.Rows.Count == 0 || !siteAllowed);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private static string BuildIssueText(DataTable issues)
        {
            if (issues == null || issues.Rows.Count == 0) return "Dữ liệu gốc của Site khớp nhau (MES.vw_FVN_MasterDataConsistency không có dòng nào).";
            var sb = new StringBuilder("Bất nhất dữ liệu gốc của Site (" + issues.Rows.Count + "):\r\n");
            foreach (DataRow r in issues.Rows)
                sb.AppendLine("[" + r["Severity"] + "] " + r["IssueCode"] + " — " + r["ObjectKey"] + ": " + r["Detail"]);
            return sb.ToString();
        }

        private void ApplyFilter()
        {
            var view = _grid.DataSource as DataView;
            if (view == null) return;
            view.RowFilter = _onlyUnlinked.Checked ? "LinkStatus <> 'LINKED'" : string.Empty;
        }

        private void Caption(string field, string caption)
        {
            var col = _view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        private DataRow Focused() { return _view.GetFocusedDataRow(); }

        private void LinkFocused()
        {
            if (!CanEdit) return;
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn Work Center nào.", true); return; }
            try
            {
                DataTable ifsLines = _repository.GetIfsProductionLines(_site);
                using (var dlg = new LinkDialog(row, _site, _repository, ifsLines))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    // Mã chuyền LOT để trống: SQL lấy mã đã khai báo ở màn Mã chuyền 5 ↔ 3; tên bộ phận / chuyền lấy từ IFS.
                    _repository.Link(_site, dlg.WorkCenterNo, dlg.DepartmentCode, null, null, null, dlg.IfsProductionLine);
                }
                Reload();
                SetStatus("Đã liên kết Work Center.", false);
            }
            catch (Exception ex) { Fail("Liên kết Work Center", ex); }
        }

        private void UnlinkFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn Work Center nào.", true); return; }
            try
            {
                if (Convert.ToString(row["LinkStatus"]) != "LINKED" || row.IsNull("LinkedDepartmentCode"))
                { SetStatus("Work Center này chưa liên kết.", true); return; }
                string dept = Convert.ToString(row["LinkedDepartmentCode"]).Trim();
                string wc = Convert.ToString(row["WorkCenterNo"]);
                if (XtraMessageBox.Show(this, "Gỡ liên kết Work Center " + wc + " khỏi bộ phận " + dept + "?\r\nChỉ gỡ liên kết IFS; bộ phận, chuyền và mã LOT giữ nguyên.",
                        "Liên kết Work Center", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _repository.Unlink(_site, dept);
                Reload();
                SetStatus("Đã gỡ liên kết Work Center " + wc + ".", false);
            }
            catch (Exception ex) { Fail("Gỡ liên kết", ex); }
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class LinkDialog : XtraForm
        {
            private readonly string _site;
            private readonly ISiteDepartmentLineRepository _repository;
            private readonly System.Collections.Generic.List<string> _lineCodes = new System.Collections.Generic.List<string>();
            private readonly ComboBoxEdit _ifsLine = new ComboBoxEdit();
            private readonly TextEdit _dept = new TextEdit();
            private readonly TextEdit _line = new TextEdit();
            private readonly LabelControl _hint = new LabelControl();

            public string WorkCenterNo { get; private set; }
            public string DepartmentCode { get; private set; }
            public string IfsProductionLine { get; private set; }

            public LinkDialog(DataRow row, string site, ISiteDepartmentLineRepository repository, DataTable ifsLines)
            {
                _site = site;
                _repository = repository;
                WorkCenterNo = Convert.ToString(row["WorkCenterNo"]);
                Text = "Liên kết Work Center " + WorkCenterNo;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(520, 350);

                string ifsLine = Field(row, "IfsProductionLine");
                string linkedLine = Field(row, "LinkedLineCode");
                Controls.Add(new LabelControl { Text = "Work Center IFS:", Location = new Point(12, 18), Width = 190 });
                Controls.Add(new LabelControl { Text = WorkCenterNo + " — " + Field(row, "Description"), Location = new Point(210, 18),
                    AutoSizeMode = LabelAutoSizeMode.None, Width = 300 });

                // Chuyền IFS: IFS là chuẩn nên khóa lại khi Work Center đã có chuyền trong IFS; nếu không thì chọn từ danh sách IFS.
                _ifsLine.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                string selected = ifsLine;
                if (ifsLines != null)
                    foreach (DataRow l in ifsLines.Rows)
                    {
                        string code = Convert.ToString(l["ProductionLineCode"]).Trim();
                        string desc = l.IsNull("Description") ? string.Empty : Convert.ToString(l["Description"]).Trim();
                        _lineCodes.Add(code);
                        _ifsLine.Properties.Items.Add(desc.Length > 0 ? code + " — " + desc : code);
                        if (selected.Length == 0 && linkedLine.Length > 0 && !l.IsNull("LotLineCode")
                            && string.Equals(Convert.ToString(l["LotLineCode"]).Trim(), linkedLine, StringComparison.OrdinalIgnoreCase))
                            selected = code;
                    }
                int index = selected.Length > 0 ? _lineCodes.FindIndex(c => string.Equals(c, selected, StringComparison.OrdinalIgnoreCase)) : -1;
                _ifsLine.SelectedIndex = index;
                _ifsLine.Enabled = ifsLine.Length == 0;
                _ifsLine.SelectedIndexChanged += (s, e) => RefreshCodes();

                // Mã bộ phận và mã chuyền LOT chỉ ĐỌC từ các mã đã khai báo; không nhập tay, không tự sinh ở bước liên kết.
                _dept.Properties.ReadOnly = true;
                _line.Properties.ReadOnly = true;

                AddRow(1, "Chuyền IFS (chọn từ danh sách IFS):", _ifsLine);
                AddRow(2, "Mã bộ phận (đã khai báo ở ①):", _dept);
                AddRow(3, "Mã chuyền LOT (đã khai báo ở ②):", _line);

                _hint.AutoSizeMode = LabelAutoSizeMode.None;
                _hint.Appearance.ForeColor = Color.Firebrick;
                _hint.Location = new Point(12, 146); _hint.Size = new Size(496, 48);
                Controls.Add(_hint);

                Controls.Add(UpstreamButton("① Mã bộ phận (Work Center)…", 12, () => new FormDepartmentCodeCrossRef(_site)));
                Controls.Add(UpstreamButton("② Mã chuyền 5 ↔ 3…", 202, () => new FormLineCodeCrossRef(_site)));

                Controls.Add(new LabelControl
                {
                    Text = "Tên bộ phận lấy từ mô tả Work Center, tên chuyền lấy từ IFS; mã 3 / 4 ký tự do FVN định nghĩa ở màn ① ②. "
                         + "IFS là chuẩn: nếu bộ phận đang được mapping cho một chuyền IFS khác chuyền của Work Center, hệ thống từ chối.",
                    Location = new Point(12, 236), AutoSizeMode = LabelAutoSizeMode.None, Width = 496, Height = 52
                });
                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(320, 306) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(420, 306), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
                RefreshCodes();
            }

            private SimpleButton UpstreamButton(string caption, int x, Func<XtraForm> create)
            {
                var b = new SimpleButton { Text = caption, Width = 178, Height = 28, Location = new Point(x, 198) };
                b.ToolTip = "Mở màn hình khai báo mã tương ứng; đóng lại để cập nhật mã hiển thị.";
                b.Click += (s, e) => OpenUpstream(create);
                return b;
            }

            private void OpenUpstream(Func<XtraForm> create)
            {
                if (UserSession.Current == null || !UserSession.Current.HasPermission(SiteDepartmentLineRepository.FunctionCode, "VIEW"))
                {
                    XtraMessageBox.Show(this, "Cần quyền " + SiteDepartmentLineRepository.FunctionCode + " / VIEW để mở màn hình khai báo mã.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (XtraForm form = create()) form.ShowDialog(this);
                RefreshCodes();
            }

            private string SelectedIfsLine
            {
                get { return _ifsLine.SelectedIndex >= 0 && _ifsLine.SelectedIndex < _lineCodes.Count ? _lineCodes[_ifsLine.SelectedIndex] : null; }
            }

            private void RefreshCodes()
            {
                try
                {
                    _dept.Text = _repository.GetDeclaredDepartmentCode(_site, WorkCenterNo) ?? string.Empty;
                    string ifs = SelectedIfsLine;
                    _line.Text = ifs == null ? string.Empty : (_repository.GetDeclaredLotLineCode(_site, ifs) ?? string.Empty);
                    if (_dept.Text.Length == 0)
                        _hint.Text = "Work Center " + WorkCenterNo + " chưa có mã bộ phận 4 ký tự. Khai báo ở ① (màn Work Center IFS ↔ mã bộ phận 4 ký tự).";
                    else if (ifs == null)
                        _hint.Text = "Chọn chuyền IFS trong danh sách.";
                    else if (_line.Text.Length == 0)
                        _hint.Text = "Chuyền IFS " + ifs + " chưa có mã chuyền LOT 3 ký tự. Khai báo ở ② (màn Mã chuyền 5 ký tự ↔ 3 ký tự).";
                    else
                        _hint.Text = string.Empty;
                }
                catch (Exception ex)
                {
                    _hint.Text = "Không tải được mã đã khai báo: " + ex.GetBaseException().Message;
                }
            }

            private static string Field(DataRow row, string column)
            {
                return row.Table.Columns.Contains(column) && !row.IsNull(column) ? Convert.ToString(row[column]).Trim() : string.Empty;
            }

            private void AddRow(int index, string caption, BaseEdit edit)
            {
                int y = 18 + index * 32;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 195 });
                edit.Location = new Point(210, y); edit.Width = 298;
                Controls.Add(edit);
            }

            private void Accept()
            {
                string ifs = SelectedIfsLine;
                if (ifs == null)
                { XtraMessageBox.Show(this, "Chọn chuyền IFS trong danh sách.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_dept.Text.Trim().Length != 4)
                { XtraMessageBox.Show(this, "Work Center chưa có mã bộ phận 4 ký tự. Khai báo ở màn Work Center IFS ↔ mã bộ phận (nút ①).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_line.Text.Trim().Length != 3)
                { XtraMessageBox.Show(this, "Chuyền IFS chưa có mã chuyền LOT 3 ký tự. Khai báo ở màn Mã chuyền 5 ký tự ↔ 3 ký tự (nút ②).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                DepartmentCode = _dept.Text.Trim();
                IfsProductionLine = ifs;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
