using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Khai báo chuyền sản xuất theo Site. Danh sách chuyền lấy từ IFS (menu Đồng bộ dữ liệu IFS, nhóm PRODUCTION_LINE);
    /// màn hình này chỉ gán mã chuyền LOT (3 ký tự) + mã bộ phận (4 ký tự) để màn Upload kế hoạch và tạo LOT/QR dùng được chuyền đó.
    /// </summary>
    public sealed class FormProductionLineSetup : XtraForm
    {
        private readonly string _site;
        private readonly ProductionLineSetupService _service;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private readonly CheckEdit _onlyUnmapped = new CheckEdit { Text = "Chỉ chuyền chưa khai báo", Width = 190 };

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
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.DoubleClick += (s, e) => EditFocused();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            bar.Controls.Add(Button("TẢI LẠI", 90, (s, e) => Reload()));
            bar.Controls.Add(Button("Khai báo / Sửa…", 130, (s, e) => EditFocused()));
            bar.Controls.Add(Button("Ngừng dùng", 100, (s, e) => DeactivateFocused()));
            _onlyUnmapped.CheckedChanged += (s, e) => Reload();
            bar.Controls.Add(_onlyUnmapped);

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

        private void Reload()
        {
            try
            {
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
                using (var dlg = new EditDialog(row))
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
            private readonly TextEdit _lot = new TextEdit();
            private readonly TextEdit _dept = new TextEdit();
            private readonly CheckEdit _active = new CheckEdit { Text = "Đang dùng" };

            public string LineCode { get; private set; }
            public string LotLineCode { get; private set; }
            public string DepartmentCode { get; private set; }
            public bool IsActive { get; private set; }

            public EditDialog(DataRow row)
            {
                LineCode = Convert.ToString(row["ProductionLineCode"]);
                Text = "Khai báo chuyền " + LineCode;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(440, 230);

                string description = row.IsNull("Description") ? string.Empty : Convert.ToString(row["Description"]);
                Controls.Add(new LabelControl { Text = "Chuyền IFS:", Location = new Point(12, 23), Width = 150 });
                Controls.Add(new LabelControl { Text = LineCode + (description.Length > 0 ? " — " + description : string.Empty),
                    Location = new Point(170, 23), AutoSizeMode = LabelAutoSizeMode.None, Width = 255 });
                AddRow(1, "Mã chuyền LOT (đúng 3 ký tự):", _lot);
                AddRow(2, "Mã bộ phận (đúng 4 ký tự):", _dept);
                _active.Location = new Point(170, 118); _active.Width = 200;
                Controls.Add(_active);

                _lot.Properties.MaxLength = 3;
                _dept.Properties.MaxLength = 4;
                _lot.Text = row.IsNull("LotLineCode") ? string.Empty : Convert.ToString(row["LotLineCode"]).Trim();
                _dept.Text = row.IsNull("DepartmentCode") ? string.Empty : Convert.ToString(row["DepartmentCode"]).Trim();
                _active.Checked = row.IsNull("MapIsActive") || Convert.ToBoolean(row["MapIsActive"]) || row.IsNull("LotLineCode");

                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(240, 180) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(340, 180), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }

            private void AddRow(int index, string caption, TextEdit edit)
            {
                int y = 23 + index * 32;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 155 });
                edit.Location = new Point(170, y); edit.Width = 250;
                Controls.Add(edit);
            }

            private void Accept()
            {
                string lot = (_lot.Text ?? string.Empty).Trim();
                string dept = (_dept.Text ?? string.Empty).Trim();
                if (_active.Checked && lot.Length != 3)
                { XtraMessageBox.Show(this, "Mã chuyền LOT phải đúng 3 ký tự.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (_active.Checked && dept.Length != 4)
                { XtraMessageBox.Show(this, "Mã bộ phận phải đúng 4 ký tự.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                LotLineCode = lot; DepartmentCode = dept; IsActive = _active.Checked;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
