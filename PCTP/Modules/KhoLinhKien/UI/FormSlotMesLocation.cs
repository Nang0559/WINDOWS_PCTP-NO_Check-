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
    /// Vùng PART / WIP của Slot kho MES (Kho › Rack › Slot) trong một Site. IFS chỉ có Kho + Location, KHÔNG có Rack / Slot, nên Location IFS
    /// không gán ở Slot: nó chọn ở cấp Kho (màn Map Kho PCTP ↔ Kho IFS, FVN_Warehouse.IfsLocationNo) và mọi Slot của Kho kế thừa (migration 086).
    /// Màn Kiểm kê hàng về, tra cứu tồn và Part → WIP chỉ thấy Slot đã có vùng lẫn Location của Kho.
    /// Chọn một hoặc nhiều Slot rồi bấm "Gán vùng…". Quyền WAREHOUSE.SLOT_MES_LOCATION (VIEW xem, EDIT gán / gỡ vùng, theo Site).
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
            Text = "Map Slot kho MES ↔ Vùng PART / WIP - Site " + _site;
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
            var assign = Button("Gán vùng…", 110, (s, e) => AssignSelected());
            var clear = Button("Gỡ vùng", 90, (s, e) => ClearSelected());
            var warehouseMap = Button("Map Kho ↔ Location IFS…", 190, (s, e) => OpenWarehouseMap());
            warehouseMap.ToolTip = "Location IFS chọn cho từng Kho (IFS không có Rack / Slot); Slot của Kho kế thừa Location này.";
            string tip = CanEdit ? null : "Bạn chỉ có quyền xem; cần " + SlotMesLocationRepository.FunctionCode + " / EDIT.";
            foreach (SimpleButton b in new[] { assign, clear })
            {
                b.Enabled = CanEdit;
                if (tip != null) b.ToolTip = tip;
                bar.Controls.Add(b);
            }
            assign.ToolTip = assign.ToolTip ?? "Chọn một hoặc nhiều Slot (Ctrl / Shift) rồi gán cùng một vùng PART / WIP.";
            bar.Controls.Add(warehouseMap);
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
                    if (Cell(r, "MapStatus") == "READY") ready++;
                    r["StatusText"] = StatusText(r);
                }
                _grid.DataSource = table.DefaultView;
                Caption("WarehouseName", "Kho MES"); Caption("RackName", "Rack (MES)"); Caption("SlotNumber", "Slot (MES)");
                Caption("AreaCode", "Vùng"); Caption("WarehouseIfsCode", "Kho IFS (theo Kho)"); Caption("LocationNo", "Location IFS (theo Kho)");
                Caption("LocationDescription", "Mô tả Location"); Caption("StatusText", "Trạng thái");
                foreach (string hidden in new[] { "SiteCode", "SlotId", "MapStatus", "LocationWarehouseCode", "LocationGroupCode", "WarehouseLocationNo", "WarehouseMapStatus" })
                    if (_view.Columns[hidden] != null) _view.Columns[hidden].Visible = false;
                ApplyFilter();
                _view.BestFitColumns();
                SetStatus(table.Rows.Count == 0
                    ? "Site " + _site + " chưa có Slot đang hoạt động. Tạo Kho / Rack / Slot ở màn quản lý kho trước."
                    : table.Rows.Count + " Slot, " + ready + " sẵn sàng, " + (table.Rows.Count - ready)
                      + " chưa sẵn sàng. Kho / Location IFS chọn ở cấp Kho (nút \"Map Kho ↔ Location IFS…\"); Slot chỉ gán vùng.",
                    table.Rows.Count == 0);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        /// <summary>Gộp lý do chưa sẵn sàng: thiếu vùng (việc của Slot) và thiếu Kho IFS / Location (việc của Kho, không phải của Slot).</summary>
        private static string StatusText(DataRow r)
        {
            string map = Cell(r, "MapStatus");
            string wh = Cell(r, "WarehouseMapStatus");
            if (map == "READY")
                return wh == "NO_WH_LOCATION" ? "Sẵn sàng (Location đang gán riêng Slot - chọn Location cho Kho)"
                     : wh == "WAREHOUSE_MISMATCH" ? "Sẵn sàng nhưng Location khác Kho IFS của Kho"
                     : "Sẵn sàng";
            var reasons = new List<string>();
            if (Cell(r, "AreaCode").Length == 0) reasons.Add("Chưa gán vùng");
            if (wh == "NO_IFS_MAP") reasons.Add("Kho chưa map Kho IFS");
            else if (wh == "NO_WH_LOCATION") reasons.Add("Kho chưa chọn Location IFS");
            else if (wh == "OUT_OF_SYNC") reasons.Add("Location của Kho chưa đồng bộ xuống Slot");
            else if (wh == "WAREHOUSE_MISMATCH") reasons.Add("Location khác Kho IFS của Kho");
            if (map == "LOCATION_INACTIVE") reasons.Add("Location đã ngừng trong IFS");
            return reasons.Count == 0 ? "Chưa sẵn sàng" : string.Join("; ", reasons);
        }

        private void OpenWarehouseMap()
        {
            try
            {
                using (var form = new PCTP.Modules.IfsSync.FormWarehouseIfsMap(_site)) form.ShowDialog(this);
                Reload();
            }
            catch (Exception ex) { Fail("Map Kho ↔ Location IFS", ex); }
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
                using (var dlg = new AssignDialog(ids.Count, Cell(focused, "AreaCode")))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    var failures = new List<string>();
                    int done = _repository.Assign(_site, ids, dlg.AreaCode, failures);
                    Reload();
                    Report("Gán vùng", "Đã gán vùng " + dlg.AreaCode + " cho " + done + " Slot.", failures);
                }
            }
            catch (Exception ex) { Fail("Gán vùng", ex); }
        }

        private void ClearSelected()
        {
            if (!CanEdit) return;
            DataRow focused;
            List<int> ids = SelectedSlotIds(out focused);
            if (ids.Count == 0) { SetStatus("Chưa chọn Slot nào.", true); return; }
            if (XtraMessageBox.Show(this, "Gỡ vùng của " + ids.Count + " Slot?\r\nLocation IFS vẫn thuộc Kho. Slot sẽ không còn hiện ở màn nhập kho / tra cứu tồn cho đến khi gán lại vùng.",
                    "Gỡ vùng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                var failures = new List<string>();
                int done = _repository.Clear(_site, ids, failures);
                Reload();
                Report("Gỡ vùng", "Đã gỡ vùng " + done + " Slot.", failures);
            }
            catch (Exception ex) { Fail("Gỡ vùng", ex); }
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

            public string AreaCode { get; private set; }

            public AssignDialog(int slotCount, string currentArea)
            {
                Text = "Gán vùng kho - " + slotCount + " Slot";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false;
                ClientSize = new Size(560, 190);

                _area.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
                _area.Properties.Items.AddRange(new object[] { "PART", "WIP" });
                _area.SelectedIndex = string.Equals(currentArea, "WIP", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

                Controls.Add(new LabelControl { Text = "Vùng kho:", Location = new Point(12, 23), AutoSizeMode = LabelAutoSizeMode.None, Width = 150 });
                _area.Location = new Point(170, 20); _area.Width = 378;
                Controls.Add(_area);

                Controls.Add(new LabelControl
                {
                    Text = "PART: Slot nhận linh kiện từ PO (màn Kiểm kê hàng về). WIP: Slot nhận linh kiện chuyển sang sản xuất. "
                         + "Location IFS không chọn ở Slot (IFS không có Rack / Slot): Slot dùng Location của Kho, chọn ở \"Map Kho ↔ Location IFS…\".",
                    AutoSizeMode = LabelAutoSizeMode.None, Location = new Point(12, 60), Size = new Size(536, 70)
                });

                var ok = new SimpleButton { Text = "Lưu", Width = 90, Location = new Point(360, 146) };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Location = new Point(460, 146), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => { AreaCode = Convert.ToString(_area.SelectedItem); DialogResult = DialogResult.OK; };
                Controls.Add(ok); Controls.Add(cancel);
                CancelButton = cancel;
            }
        }
    }
}
