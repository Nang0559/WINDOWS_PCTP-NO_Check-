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
    /// chuyền / bộ phận của IFS. IFS là chuẩn: bộ phận đang được mapping cho chuyền IFS A không nhận Work Center thuộc chuyền IFS B
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
                using (var dlg = new LinkDialog(row))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    _repository.Link(_site, dlg.WorkCenterNo, dlg.DepartmentCode, dlg.LineCode, dlg.LineName, dlg.DepartmentName);
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
            private readonly TextEdit _dept = new TextEdit();
            private readonly TextEdit _line = new TextEdit();
            private readonly TextEdit _lineName = new TextEdit();
            private readonly TextEdit _deptName = new TextEdit();

            public string WorkCenterNo { get; private set; }
            public string DepartmentCode { get; private set; }
            public string LineCode { get; private set; }
            public string LineName { get; private set; }
            public string DepartmentName { get; private set; }

            public LinkDialog(DataRow row)
            {
                WorkCenterNo = Convert.ToString(row["WorkCenterNo"]);
                Text = "Liên kết Work Center " + WorkCenterNo;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(500, 330);

                string ifsLine = Field(row, "IfsProductionLine");
                string ifsDept = Field(row, "IfsDepartmentNo");
                Controls.Add(new LabelControl { Text = "Work Center IFS:", Location = new Point(12, 18), Width = 150 });
                Controls.Add(new LabelControl { Text = WorkCenterNo + " — " + Field(row, "Description"), Location = new Point(170, 18),
                    AutoSizeMode = LabelAutoSizeMode.None, Width = 320 });
                Controls.Add(new LabelControl { Text = "Chuyền / bộ phận trong IFS:", Location = new Point(12, 44), Width = 150 });
                Controls.Add(new LabelControl { Text = (ifsLine.Length > 0 ? ifsLine : "(trống)") + " / " + (ifsDept.Length > 0 ? ifsDept : "(trống)"),
                    Location = new Point(170, 44), AutoSizeMode = LabelAutoSizeMode.None, Width = 320 });

                AddRow(2, "Mã bộ phận (đúng 4 ký tự):", _dept);
                AddRow(3, "Mã chuyền LOT (đúng 3 ký tự):", _line);
                AddRow(4, "Tên chuyền (khi tạo mới):", _lineName);
                AddRow(5, "Tên bộ phận (tùy chọn):", _deptName);
                _dept.Properties.MaxLength = 4;
                _line.Properties.MaxLength = 3;

                string linkedDept = Field(row, "LinkedDepartmentCode");
                string linkedLine = Field(row, "LinkedLineCode");
                _dept.Text = linkedDept.Length > 0 ? linkedDept : Field(row, "SuggestedDepartmentCode");
                _line.Text = linkedLine.Length > 0 ? linkedLine : Field(row, "SuggestedLineCode");

                Controls.Add(new LabelControl
                {
                    Text = "IFS là chuẩn: nếu bộ phận đang được mapping cho một chuyền IFS khác chuyền của Work Center, hệ thống từ chối.",
                    Location = new Point(12, 232), AutoSizeMode = LabelAutoSizeMode.None, Width = 476, Height = 34
                });
                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(300, 285) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(400, 285), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }

            private static string Field(DataRow row, string column)
            {
                return row.Table.Columns.Contains(column) && !row.IsNull(column) ? Convert.ToString(row[column]).Trim() : string.Empty;
            }

            private void AddRow(int index, string caption, TextEdit edit)
            {
                int y = 18 + index * 32;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 155 });
                edit.Location = new Point(170, y); edit.Width = 310;
                Controls.Add(edit);
            }

            private void Accept()
            {
                string dept = (_dept.Text ?? string.Empty).Trim();
                string line = (_line.Text ?? string.Empty).Trim();
                if (dept.Length != 4) { XtraMessageBox.Show(this, "Mã bộ phận phải đúng 4 ký tự.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (line.Length != 3) { XtraMessageBox.Show(this, "Mã chuyền LOT phải đúng 3 ký tự.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                DepartmentCode = dept; LineCode = line;
                LineName = (_lineName.Text ?? string.Empty).Trim();
                DepartmentName = (_deptName.Text ?? string.Empty).Trim();
                DialogResult = DialogResult.OK;
            }
        }
    }
}
