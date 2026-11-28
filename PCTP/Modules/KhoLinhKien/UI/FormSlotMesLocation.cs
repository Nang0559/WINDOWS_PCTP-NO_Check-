using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Map Slot kho ↔ MES Location (IFS) và vùng PART / WIP của một Site. Màn Kiểm kê hàng về, tra cứu tồn và Part → WIP chỉ thấy Slot đã có
    /// cả vùng lẫn MES Location đang hoạt động; trước đây không có màn nào để khai báo hai thông tin này.
    /// Slot / Rack / Kho tạo ở màn quản lý kho; MES Location lấy từ IFS (đồng bộ nhóm INVENTORY_LOCATION) nên chỉ CHỌN trong danh sách, không nhập tay.
    /// Chọn một hoặc nhiều Slot rồi bấm "Gán vùng + Location…". Quyền WAREHOUSE.SLOT_MES_LOCATION (VIEW xem, EDIT gán / gỡ, theo Site).
    /// </summary>
    public sealed class FormSlotMesLocation : XtraForm
    {
        private readonly string _site;
        private readonly ISlotMesLocationRepository _repository;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };
        private readonly CheckEdit _onlyPending = new CheckEdit { Text = "Chỉ Slot chưa sẵn sàng", Width = 180 };

        private static bool CanEdit
        {
            get { return UserSession.Current != null && UserSession.Current.HasPermission(SlotMesLocationRepository.FunctionCode, "EDIT"); }
        }

        public FormSlotMesLocation(string siteCode) : this(siteCode, new SlotMesLocationRepository()) { }

        public FormSlotMesLocation(string siteCode, ISlotMesLocationRepository repository)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            AuthorizationGuard.Demand(SlotMesLocationRepository.FunctionCode, "VIEW");
            _site = siteCode.Trim().ToUpperInvariant();
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            Text = "Map Slot kho ↔ MES Location (IFS) - Site " + _site;
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
            _view.OptionsSelection.MultiSelect = true;
            _view.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
            _view.RowCellStyle += RowCellStyle;
            _view.DoubleClick += (s, e) => AssignSelected();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            bar.Controls.Add(Button("TẢI LẠI", 90, (s, e) => Reload()));
            var assign = Button("Gán vùng + Location…", 170, (s, e) => AssignSelected());
            var clear = Button("Gỡ map", 90, (s, e) => ClearSelected());
            string tip = CanEdit ? null : "Bạn chỉ có quyền xem; cần " + SlotMesLocationRepository.FunctionCode + " / EDIT.";
            foreach (SimpleButton b in new[] { assign, clear })
            {
                b.Enabled = CanEdit;
                if (tip != null) b.ToolTip = tip;
                bar.Controls.Add(b);
            }
            assign.ToolTip = assign.ToolTip ?? "Chọn một hoặc nhiều Slot (Ctrl / Shift) rồi gán cùng một vùng và MES Location.";
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

        private static string Cell(DataRow row, string column)
        {
            return row != null && row.Table.Columns.Contains(column) && !row.IsNull(column) ? Convert.ToString(row[column]).Trim() : string.Empty;
        }

        private void Reload()
        {
            try
            {
                DataTable table = _repository.GetStatus(_site);
                table.Columns.Add("StatusText", typeof(string));
                int ready = 0;
                foreach (DataRow r in table.Rows)
                {
                    string state = Cell(r, "MapStatus");
                    if (state == "READY") ready++;
                    r["StatusText"] = state == "READY" ? "Sẵn sàng"
                        : state == "NO_AREA" ? "Chưa gán vùng"
                        : state == "NO_LOCATION" ? "Chưa gán Location"
                        : "Location đã ngừng trong IFS";
                }
                _grid.DataSource = table.DefaultView;
                Caption("WarehouseName", "Kho"); Caption("RackName", "Rack"); Caption("SlotNumber", "Slot");
                Caption("AreaCode", "Vùng"); Caption("LocationNo", "MES Location (IFS)"); Caption("LocationWarehouseCode", "Kho IFS");
                Caption("LocationGroupCode", "Nhóm Location"); Caption("LocationDescription", "Mô tả Location"); Caption("StatusText", "Trạng thái");
                foreach (string hidden in new[] { "SiteCode", "SlotId", "MapStatus" })
                    if (_view.Columns[hidden] != null) _view.Columns[hidden].Visible = false;
                ApplyFilter();
                _view.BestFitColumns();
                SetStatus(table.Rows.Count == 0
                    ? "Site " + _site + " chưa có Slot đang hoạt động. Tạo Kho / Rack / Slot ở màn quản lý kho trước."
                    : table.Rows.Count + " Slot, " + ready + " sẵn sàng, " + (table.Rows.Count - ready)
                      + " chưa sẵn sàng. Location chưa có trong danh sách: đồng bộ nhóm INVENTORY_LOCATION ở menu Đồng bộ dữ liệu IFS.",
                    table.Rows.Count == 0);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void ApplyFilter()
        {
            var view = _grid.DataSource as DataView;
            if (view == null) return;
            view.RowFilter = _onlyPending.Checked ? "MapStatus <> 'READY'" : string.Empty;
        }

        private void Caption(string field, string caption)
        {
            if (_view.Columns[field] != null) _view.Columns[field].Caption = caption;
        }

        private void RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.RowHandle < 0 || e.Column.FieldName != "StatusText") return;
            string state = Convert.ToString(_view.GetRowCellValue(e.RowHandle, "MapStatus"));
            e.Appearance.ForeColor = state == "READY" ? Color.FromArgb(24, 110, 66) : Color.FromArgb(160, 90, 0);
            e.Appearance.Options.UseForeColor = true;
        }

        private List<int> SelectedSlotIds(out DataRow focused)
        {
            var ids = new List<int>();
            focused = _view.GetFocusedDataRow();
            foreach (int handle in _view.GetSelectedRows())
            {
                DataRow row = _view.GetDataRow(handle);
                if (row != null && !row.IsNull("SlotId")) ids.Add(Convert.ToInt32(row["SlotId"]));
            }
            if (ids.Count == 0 && focused != null && !focused.IsNull("SlotId")) ids.Add(Convert.ToInt32(focused["SlotId"]));
            return ids;
        }

        private void AssignSelected()
        {
            if (!CanEdit) return;
            DataRow focused;
            List<int> ids = SelectedSlotIds(out focused);
            if (ids.Count == 0) { SetStatus("Chưa chọn Slot nào.", true); return; }
            try
            {
                DataTable locations = _repository.GetLocations(_site);
                using (var dlg = new AssignDialog(_site, ids.Count, locations, Cell(focused, "AreaCode"), Cell(focused, "LocationNo")))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    var failures = new List<string>();
                    int done = _repository.Assign(_site, ids, dlg.AreaCode, dlg.LocationNo, failures);
                    Reload();
                    Report("Gán vùng + Location", "Đã gán " + dlg.AreaCode + " / " + dlg.LocationNo + " cho " + done + " Slot.", failures);
                }
            }
            catch (Exception ex) { Fail("Gán vùng + Location", ex); }
        }

        private void ClearSelected()
        {
            if (!CanEdit) return;
            DataRow focused;
            List<int> ids = SelectedSlotIds(out focused);
            if (ids.Count == 0) { SetStatus("Chưa chọn Slot nào.", true); return; }
            if (XtraMessageBox.Show(this, "Gỡ vùng và MES Location của " + ids.Count + " Slot?\r\nSlot sẽ không còn hiện ở màn nhập kho / tra cứu tồn cho đến khi gán lại.",
                    "Gỡ map", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                var failures = new List<string>();
                int done = _repository.Clear(_site, ids, failures);
                Reload();
                Report("Gỡ map", "Đã gỡ map " + done + " Slot.", failures);
            }
            catch (Exception ex) { Fail("Gỡ map", ex); }
        }

        private void Report(string caption, string success, List<string> failures)
        {
            if (failures.Count == 0) { SetStatus(success, false); return; }
            SetStatus(success + " Lỗi " + failures.Count + " Slot.", true);
            var sb = new StringBuilder();
            foreach (string f in failures) sb.AppendLine(f);
            XtraMessageBox.Show(this, sb.ToString(), caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Fail(string caption, Exception ex)
        {
            string message = ex.GetBaseException().Message;
            SetStatus(caption + " thất bại: " + message, true);
            XtraMessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class AssignDialog : XtraForm
        {
            private readonly ComboBoxEdit _area = new ComboBoxEdit();
            private readonly LookUpEdit _location = new LookUpEdit();

            public string AreaCode { get; private set; }
            public string LocationNo { get; private set; }

            public AssignDialog(string site, int slotCount, DataTable locations, string currentArea, string currentLocation)
            {
                Text = "Gán vùng + MES Location - " + slotCount + " Slot";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(560, 230);

                _area.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
                _area.Properties.Items.AddRange(new object[] { "PART", "WIP" });
                _area.SelectedIndex = string.Equals(currentArea, "WIP", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

                _location.Properties.DataSource = locations;
                _location.Properties.DisplayMember = "LocationNo";
                _location.Properties.ValueMember = "LocationNo";
                _location.Properties.NullText = "Chọn MES Location (IFS)";
                _location.Properties.Columns.Add(new LookUpColumnInfo("LocationNo", "Location", 150));
                _location.Properties.Columns.Add(new LookUpColumnInfo("WarehouseCode", "Kho IFS", 90));
                _location.Properties.Columns.Add(new LookUpColumnInfo("Description", "Mô tả", 240));
                _location.Properties.SearchMode = SearchMode.AutoComplete;
                _location.Properties.TextEditStyle = TextEditStyles.Standard;
                if (currentLocation.Length > 0 && locations.Select("LocationNo = '" + currentLocation.Replace("'", "''") + "'").Length > 0)
                    _location.EditValue = currentLocation;

                AddRow(0, "Vùng kho:", _area);
                AddRow(1, "MES Location (IFS):", _location);

                var note = new LabelControl
                {
                    Text = locations.Rows.Count == 0
                        ? "Site " + site + " chưa có MES Location nào từ IFS. Đồng bộ dữ liệu IFS nhóm INVENTORY_LOCATION rồi mở lại màn này."
                        : "PART: Slot nhận linh kiện từ PO (màn Kiểm kê hàng về). WIP: Slot nhận linh kiện chuyển sang sản xuất. Mỗi Slot có một vùng và một Location chính.",
                    AutoSizeMode = LabelAutoSizeMode.None, Location = new Point(12, 96), Size = new Size(536, 70)
                };
                if (locations.Rows.Count == 0) note.Appearance.ForeColor = Color.Firebrick;
                Controls.Add(note);

                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(360, 186), Enabled = locations.Rows.Count > 0 };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(460, 186), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }

            private void AddRow(int index, string caption, BaseEdit edit)
            {
                int y = 20 + index * 36;
                Controls.Add(new LabelControl { Text = caption, Location = new Point(12, y + 3), AutoSizeMode = LabelAutoSizeMode.None, Width = 150 });
                edit.Location = new Point(170, y); edit.Width = 378;
                Controls.Add(edit);
            }

            private void Accept()
            {
                if (_location.EditValue == null || _location.EditValue == DBNull.Value || Convert.ToString(_location.EditValue).Length == 0)
                { XtraMessageBox.Show(this, "Chọn MES Location từ danh sách.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                AreaCode = Convert.ToString(_area.SelectedItem);
                LocationNo = Convert.ToString(_location.EditValue);
                DialogResult = DialogResult.OK;
            }
        }
    }
}
