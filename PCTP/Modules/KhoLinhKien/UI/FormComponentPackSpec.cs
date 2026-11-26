using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Khai báo quy cách đóng gói linh kiện theo Site: thêm / sửa / vô hiệu từng dòng, upload Excel hàng loạt
    /// (cột như B20ItemQuyCach) và tải file Excel mẫu. Quyền và Site được service kiểm lại, SQL kiểm lại mã vật tư.
    /// </summary>
    public sealed class FormComponentPackSpec : XtraForm
    {
        private readonly string _site;
        private readonly ComponentPackSpecService _service;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };

        public FormComponentPackSpec(string siteCode)
            : this(siteCode, new ComponentPackSpecService(new ComponentPackSpecRepository())) { }

        public FormComponentPackSpec(string siteCode, ComponentPackSpecService service)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            _site = siteCode.Trim().ToUpperInvariant();
            _service = service ?? throw new ArgumentNullException("service");
            _view = new GridView(_grid);
            Text = "Quy cách đóng gói linh kiện - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 600);
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
            bar.Controls.Add(Button("Thêm…", 90, (s, e) => Edit(null)));
            bar.Controls.Add(Button("Sửa…", 90, (s, e) => EditFocused()));
            bar.Controls.Add(Button("Vô hiệu", 90, (s, e) => DeactivateFocused()));
            bar.Controls.Add(Button("Upload Excel…", 130, (s, e) => UploadExcel()));
            bar.Controls.Add(Button("File Excel mẫu…", 130, (s, e) => SaveTemplate()));

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
                _grid.DataSource = table;
                string[] headers = { "Id", "Site", "Nhà cung cấp (trống = mọi NCC)", "Mã vật tư", "SL / tem", "SL đóng tối đa", "Đang dùng", "Người tạo", "Cập nhật (UTC)", "Người sửa" };
                for (int i = 0; i < _view.Columns.Count && i < headers.Length; i++) _view.Columns[i].Caption = headers[i];
                _view.BestFitColumns();
                SetStatus(table.Rows.Count + " quy cách, Site " + _site + ".", false);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private DataRow Focused() { return _view.GetFocusedDataRow(); }

        private void EditFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            Edit(row);
        }

        private static ComponentPackSpecRow ToSpec(DataRow row)
        {
            decimal? max = row.IsNull("MaxCloseQty") ? (decimal?)null : Convert.ToDecimal(row["MaxCloseQty"]);
            return new ComponentPackSpecRow
            {
                SiteCode = Convert.ToString(row["SiteCode"]),
                SupplierCode = Convert.ToString(row["SupplierCode"]),
                PartNo = Convert.ToString(row["PartNo"]),
                PackQty = Convert.ToDecimal(row["PackQty"]),
                MaxCloseQty = max,
                IsActive = Convert.ToBoolean(row["IsActive"])
            };
        }

        private void Edit(DataRow row)
        {
            try
            {
                ComponentPackSpecRow spec = row == null ? new ComponentPackSpecRow { SiteCode = _site } : ToSpec(row);
                using (var dlg = new EditDialog(spec, row != null))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    _service.Save(new[] { dlg.Result });
                }
                Reload();
                SetStatus("Đã lưu quy cách.", false);
            }
            catch (Exception ex) { Fail("Lưu quy cách", ex); }
        }

        private void DeactivateFocused()
        {
            DataRow row = Focused();
            if (row == null) { SetStatus("Chưa chọn dòng nào.", true); return; }
            try
            {
                ComponentPackSpecRow spec = ToSpec(row);
                if (!spec.IsActive) { SetStatus("Quy cách này đã vô hiệu.", true); return; }
                if (XtraMessageBox.Show(this, "Vô hiệu quy cách của " + spec.PartNo + "?\r\nPO phát hành sau đó sẽ không có quy cách cho mã này.",
                        "Quy cách đóng gói", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                spec.IsActive = false;
                _service.Save(new[] { spec });
                Reload();
                SetStatus("Đã vô hiệu quy cách " + spec.PartNo + ".", false);
            }
            catch (Exception ex) { Fail("Vô hiệu quy cách", ex); }
        }

        private void UploadExcel()
        {
            try
            {
                string path;
                using (var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", Title = "Chọn file quy cách đóng gói" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    path = dlg.FileName;
                }
                var rows = ComponentPackSpecExcel.Read(path, _site);
                string message = "File " + Path.GetFileName(path) + ": " + rows.Count + " dòng quy cách.\r\n" +
                                 "Dòng đã có (cùng Site / nhà cung cấp / mã vật tư) sẽ được cập nhật. Toàn bộ file chạy trong một transaction.\r\n\r\nTiếp tục?";
                if (XtraMessageBox.Show(this, message, "Upload quy cách đóng gói", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                int n = _service.Save(rows);
                Reload();
                SetStatus("Đã upload " + n + " dòng quy cách.", false);
            }
            catch (Exception ex) { Fail("Upload quy cách", ex); }
        }

        private void SaveTemplate()
        {
            try
            {
                using (var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "QuyCachDongGoi_mau.xlsx" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    ComponentPackSpecExcel.WriteTemplate(dlg.FileName, _site);
                    SetStatus("Đã tạo file mẫu: " + dlg.FileName, false);
                }
            }
            catch (Exception ex) { Fail("File Excel mẫu", ex); }
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class EditDialog : XtraForm
        {
            private readonly TextEdit _supplier = new TextEdit();
            private readonly TextEdit _part = new TextEdit();
            private readonly TextEdit _qty = new TextEdit();
            private readonly TextEdit _max = new TextEdit();
            private readonly CheckEdit _active = new CheckEdit { Text = "Đang dùng" };
            private readonly string _site;

            public ComponentPackSpecRow Result { get; private set; }

            public EditDialog(ComponentPackSpecRow spec, bool existing)
            {
                _site = spec.SiteCode;
                Text = existing ? "Sửa quy cách đóng gói" : "Thêm quy cách đóng gói";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(420, 230);

                AddRow(0, "Nhà cung cấp (trống = mọi NCC):", _supplier);
                AddRow(1, "Mã vật tư (PartNo):", _part);
                AddRow(2, "Số lượng mỗi tem:", _qty);
                AddRow(3, "SL đóng tối đa (tùy chọn):", _max);
                _active.Location = new Point(190, 134); _active.Width = 200;
                Controls.Add(_active);

                _supplier.Text = spec.SupplierCode;
                _part.Text = spec.PartNo;
                _qty.Text = spec.PackQty > 0 ? spec.PackQty.ToString("0.###", CultureInfo.CurrentCulture) : string.Empty;
                _max.Text = spec.MaxCloseQty.HasValue ? spec.MaxCloseQty.Value.ToString("0.###", CultureInfo.CurrentCulture) : string.Empty;
                _active.Checked = spec.IsActive;
                if (existing) { _supplier.Properties.ReadOnly = true; _part.Properties.ReadOnly = true; }   // khóa khóa chính

                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(210, 185) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(310, 185), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }

            private void AddRow(int index, string caption, TextEdit edit)
            {
                int y = 20 + index * 28;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 175 });
                edit.Location = new Point(190, y); edit.Width = 210;
                Controls.Add(edit);
            }

            private void Accept()
            {
                decimal qty;
                if (!ComponentPackSpecExcel.TryParseDecimal(_qty.Text, out qty) || qty <= 0)
                { XtraMessageBox.Show(this, "Số lượng mỗi tem phải là số lớn hơn 0.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                decimal? max = null;
                if (!string.IsNullOrWhiteSpace(_max.Text))
                {
                    decimal m;
                    if (!ComponentPackSpecExcel.TryParseDecimal(_max.Text, out m) || m <= 0)
                    { XtraMessageBox.Show(this, "SL đóng tối đa phải là số lớn hơn 0 hoặc để trống.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    max = m;
                }
                if (string.IsNullOrWhiteSpace(_part.Text))
                { XtraMessageBox.Show(this, "Chưa nhập mã vật tư.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                Result = new ComponentPackSpecRow
                {
                    SiteCode = _site,
                    SupplierCode = (_supplier.Text ?? string.Empty).Trim(),
                    PartNo = _part.Text.Trim(),
                    PackQty = qty,
                    MaxCloseQty = max,
                    IsActive = _active.Checked
                };
                DialogResult = DialogResult.OK;
            }
        }
    }
}
