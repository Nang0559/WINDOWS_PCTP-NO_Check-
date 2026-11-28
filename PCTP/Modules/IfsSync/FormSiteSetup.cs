using System;
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
    /// Khai báo Site PCTP được phép dùng (dbo.FVN_PCTPSite). Danh sách Site lấy từ IFS (đồng bộ nhóm SITE) cộng các Site đã có trong danh sách cho phép.
    /// Cho phép / bật, tạm ngưng, đổi tên (chỉ khi IFS chưa có tên). Không xóa Site. Quyền MES.SITE_SETUP: VIEW xem, EDIT thay đổi.
    /// </summary>
    public sealed class FormSiteSetup : XtraForm
    {
        private readonly ISiteSetupRepository _repository;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private readonly CheckEdit _onlyPending = new CheckEdit { Text = "Chỉ Site chưa dùng được", Width = 190 };
        private SimpleButton _enable, _disable, _rename;

        private static bool CanEdit
        {
            get { return UserSession.Current != null && UserSession.Current.HasPermission(SiteSetupRepository.FunctionCode, "EDIT"); }
        }

        public FormSiteSetup() : this(new SiteSetupRepository()) { }

        public FormSiteSetup(ISiteSetupRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            Text = "Khai báo Site PCTP (dbo.FVN_PCTPSite)";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 560);
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
            _view.RowStyle += (s, e) =>
            {
                if (e.RowHandle < 0) return;
                DataRow row = _view.GetDataRow(e.RowHandle);
                if (row != null && Convert.ToString(row["SiteStatus"]) != "OK") e.Appearance.ForeColor = Color.Firebrick;
            };

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            bar.Controls.Add(Button("TẢI LẠI", 90, (s, e) => Reload()));
            _enable = Button("Cho phép / Bật", 120, (s, e) => SetActive(true));
            _disable = Button("Tạm ngưng", 100, (s, e) => SetActive(false));
            _rename = Button("Sửa tên…", 90, (s, e) => Rename());
            string tip = CanEdit ? null : "Bạn chỉ có quyền xem; cần " + SiteSetupRepository.FunctionCode + " / EDIT.";
            foreach (SimpleButton b in new[] { _enable, _disable, _rename })
            {
                b.Enabled = CanEdit;
                if (tip != null) b.ToolTip = tip;
                bar.Controls.Add(b);
            }
            _onlyPending.CheckedChanged += (s, e) => ApplyFilter();
            bar.Controls.Add(_onlyPending);

            Controls.Add(_grid);
            Controls.Add(bar);
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

        private static string StatusText(string code)
        {
            switch (code)
            {
                case "OK": return "Đang dùng";
                case "DISABLED": return "Tạm ngưng";
                case "NOT_IN_IFS": return "Đã cho phép nhưng không còn trong IFS";
                case "NOT_ALLOW_LISTED": return "Có trong IFS, chưa cho phép";
                default: return code;
            }
        }

        private void Reload()
        {
            try
            {
                DataTable table = _repository.List();
                table.Columns.Add("StatusText", typeof(string));
                int pending = 0;
                foreach (DataRow r in table.Rows)
                {
                    string code = Convert.ToString(r["SiteStatus"]);
                    r["StatusText"] = StatusText(code);
                    if (code != "OK") pending++;
                }
                _grid.DataSource = table.DefaultView;
                Caption("SiteCode", "Site"); Caption("SiteName", "Tên Site (IFS ưu tiên)"); Caption("IfsSiteName", "Tên trong IFS");
                Caption("IsAllowListed", "Đã cho phép"); Caption("IsActive", "Đang bật"); Caption("IsPresentInIfs", "Còn trong IFS");
                Caption("StatusText", "Trạng thái"); Caption("IfsSyncedAtUtc", "Đồng bộ IFS (UTC)");
                if (_view.Columns["SiteStatus"] != null) _view.Columns["SiteStatus"].Visible = false;
                ApplyFilter();
                _view.BestFitColumns();
                SetStatus(table.Rows.Count + " Site, " + pending + " Site chưa dùng được. Site chưa có trong danh sách: đồng bộ dữ liệu IFS nhóm SITE trước.", false);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void ApplyFilter()
        {
            var view = _grid.DataSource as DataView;
            if (view == null) return;
            view.RowFilter = _onlyPending.Checked ? "SiteStatus <> 'OK'" : string.Empty;
        }

        private void Caption(string field, string caption)
        {
            var col = _view.Columns[field];
            if (col != null) col.Caption = caption;
        }

        private DataRow Focused() { return _view.GetFocusedDataRow(); }

        private void SetActive(bool active)
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn Site nào.", true); return; }
            string site = Convert.ToString(row["SiteCode"]);
            try
            {
                if (active)
                {
                    if (!Convert.ToBoolean(row["IsPresentInIfs"]))
                    { SetStatus("Site " + site + " chưa có trong IFS.FVN_Site. Đồng bộ dữ liệu IFS nhóm SITE trước.", true); return; }
                    if (Convert.ToBoolean(row["IsAllowListed"]) && Convert.ToBoolean(row["IsActive"]))
                    { SetStatus("Site " + site + " đã đang dùng.", true); return; }
                }
                else
                {
                    if (!Convert.ToBoolean(row["IsAllowListed"]) || !Convert.ToBoolean(row["IsActive"]))
                    { SetStatus("Site " + site + " chưa bật.", true); return; }
                    DataTable usage = _repository.GetUsage(site);
                    string detail = usage.Rows.Count == 0 ? string.Empty :
                        "\r\nĐang có " + usage.Rows[0]["ActiveLines"] + " chuyền, " + usage.Rows[0]["ActiveDepartments"] + " bộ phận, " +
                        usage.Rows[0]["ActiveLotCodeMaps"] + " mapping chuyền hoạt động.";
                    if (XtraMessageBox.Show(this, "Tạm ngưng Site " + site + "?\r\nUpload kế hoạch và các chức năng theo Site sẽ không còn dùng được Site này." + detail,
                            "Khai báo Site", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                }
                _repository.Save(site, null, active);
                Reload();
                SetStatus(active ? "Đã cho phép / bật Site " + site + "." : "Đã tạm ngưng Site " + site + ".", false);
            }
            catch (Exception ex) { Fail(active ? "Cho phép Site" : "Tạm ngưng Site", ex); }
        }

        private void Rename()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn Site nào.", true); return; }
            string site = Convert.ToString(row["SiteCode"]);
            try
            {
                if (!Convert.ToBoolean(row["IsAllowListed"]))
                { SetStatus("Cho phép Site " + site + " trước khi đặt tên.", true); return; }
                if (!row.IsNull("IfsSiteName") && Convert.ToString(row["IfsSiteName"]).Trim().Length > 0)
                { SetStatus("Site " + site + " đã có tên trong IFS; tên hiển thị lấy theo IFS.", true); return; }
                string current = row.IsNull("SiteName") ? string.Empty : Convert.ToString(row["SiteName"]);
                string name = XtraInputBox.Show("Tên Site " + site + " (IFS chưa có tên):", "Sửa tên Site", current);
                if (string.IsNullOrWhiteSpace(name)) return;
                _repository.Save(site, name, Convert.ToBoolean(row["IsActive"]));
                Reload();
                SetStatus("Đã lưu tên Site " + site + ".", false);
            }
            catch (Exception ex) { Fail("Sửa tên Site", ex); }
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
