using System;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Security;

namespace PCTP.Modules.IfsSync
{
    /// <summary>
    /// Map Kho PCTP (dbo.FVN_Warehouse) ↔ Kho IFS (MES.FVN_IfsWarehouse) của một Site, cùng kiểu với Site / Work Center / Chuyền:
    /// IFS là nguồn danh mục, Kho PCTP là danh mục vận hành. Rack / Slot thuộc Kho nên kế thừa Site và Kho IFS từ Kho; Slot chỉ gán được
    /// Location thuộc đúng Kho IFS đã map (xem migration 085). Có thể map Kho sẵn có, hoặc tạo Kho PCTP từ Kho IFS (xem trước rồi mới tạo).
    /// Quyền WAREHOUSE.WAREHOUSE_IFS_MAP (VIEW xem, EDIT map / tạo, theo Site).
    /// </summary>
    public sealed class FormWarehouseIfsMap : XtraForm
    {
        private readonly string _site;
        private readonly IIfsMapCenterRepository _repository;
        private readonly GridControl _grid = new GridControl { Dock = DockStyle.Fill };
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl { Dock = DockStyle.Bottom, Height = 26 };

        private static bool CanEdit
        {
            get { return UserSession.Current != null && UserSession.Current.HasPermission(IfsMapCenterRepository.WarehouseMapFunctionCode, "EDIT"); }
        }

        public FormWarehouseIfsMap(string siteCode) : this(siteCode, new IfsMapCenterRepository()) { }

        public FormWarehouseIfsMap(string siteCode, IIfsMapCenterRepository repository)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            AuthorizationGuard.Demand(IfsMapCenterRepository.WarehouseMapFunctionCode, "VIEW");
            _site = siteCode.Trim().ToUpperInvariant();
            _repository = repository ?? throw new ArgumentNullException("repository");
            _view = new GridView(_grid);
            Text = "Map Kho PCTP ↔ Kho IFS - Site " + _site;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 620);
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
            _view.RowCellStyle += (s, e) =>
            {
                var row = _view.GetDataRow(e.RowHandle);
                string state = row == null ? string.Empty : Convert.ToString(row["MapStatus"]);
                if (state == "NO_IFS_MAP" || state == "IFS_INACTIVE") e.Appearance.BackColor = Color.FromArgb(255, 244, 214);
                else if (state == "IFS_ONLY") e.Appearance.BackColor = Color.FromArgb(232, 240, 255);
            };
            _view.DoubleClick += (s, e) => MapSelected();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 6, 6, 0) };
            bar.Controls.Add(Button("TẢI LẠI", 90, (s, e) => Reload()));
            var map = Button("Map Kho ↔ Kho IFS…", 170, (s, e) => MapSelected());
            var unmap = Button("Gỡ map", 90, (s, e) => UnmapSelected());
            var create = Button("Tạo Kho từ Kho IFS…", 170, (s, e) => CreateFromIfs());
            string tip = CanEdit ? null : "Bạn chỉ có quyền xem; cần " + IfsMapCenterRepository.WarehouseMapFunctionCode + " / EDIT.";
            foreach (SimpleButton b in new[] { map, unmap, create })
            {
                b.Enabled = CanEdit;
                if (tip != null) b.ToolTip = tip;
                bar.Controls.Add(b);
            }
            map.ToolTip = map.ToolTip ?? "Chọn một Kho PCTP rồi chọn Kho IFS tương ứng.";
            create.ToolTip = create.ToolTip ?? "Tạo Kho PCTP cho Kho IFS chưa có Kho (xem trước danh sách trước khi tạo).";

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
                DataTable table = _repository.GetWarehouseMap(_site);
                table.Columns.Add("StatusText", typeof(string));
                int mapped = 0;
                foreach (DataRow r in table.Rows)
                {
                    string state = Cell(r, "MapStatus");
                    if (state == "MAPPED") mapped++;
                    r["StatusText"] = state == "MAPPED" ? "Đã map"
                        : state == "NO_IFS_MAP" ? "Kho PCTP chưa map IFS"
                        : state == "IFS_ONLY" ? "Kho IFS chưa có Kho PCTP"
                        : "Kho IFS đã ngừng";
                }
                _grid.DataSource = table.DefaultView;
                Caption("WarehouseName", "Kho PCTP"); Caption("IfsWarehouseCode", "Kho IFS"); Caption("IfsDescription", "Mô tả Kho IFS");
                Caption("RackCount", "Số Rack"); Caption("SlotCount", "Số Slot"); Caption("IfsLocationCount", "Số Location IFS");
                Caption("StatusText", "Trạng thái");
                foreach (string hidden in new[] { "SiteCode", "WarehouseId", "MapStatus" })
                    if (_view.Columns[hidden] != null) _view.Columns[hidden].Visible = false;
                _view.BestFitColumns();
                SetStatus(table.Rows.Count == 0
                    ? "Site " + _site + " chưa có Kho PCTP và chưa đồng bộ Kho IFS. Đồng bộ nhóm WAREHOUSE (và INVENTORY_LOCATION) ở Trung tâm đồng bộ IFS."
                    : table.Rows.Count + " dòng, " + mapped + " Kho đã map. Dòng vàng: Kho PCTP chưa map; dòng xanh: Kho IFS chưa có Kho PCTP.",
                    table.Rows.Count == 0);
            }
            catch (Exception ex) { SetStatus("Không tải được: " + ex.GetBaseException().Message, true); }
        }

        private void Caption(string field, string caption)
        {
            if (_view.Columns[field] != null) _view.Columns[field].Caption = caption;
        }

        private DataRow Current()
        {
            return _view.FocusedRowHandle < 0 ? null : _view.GetDataRow(_view.FocusedRowHandle);
        }

        private void MapSelected()
        {
            if (!CanEdit) return;
            DataRow row = Current();
            if (row == null) return;
            string state = Cell(row, "MapStatus");
            if (state == "IFS_ONLY") { CreateFromIfs(Cell(row, "IfsWarehouseCode")); return; }
            int warehouseId = Convert.ToInt32(row["WarehouseId"]);
            string ifsCode = PickIfsWarehouse(Cell(row, "WarehouseName"), Cell(row, "IfsWarehouseCode"));
            if (ifsCode == null) return;
            try
            {
                _repository.MapWarehouse(_site, warehouseId, ifsCode);
                Reload();
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void UnmapSelected()
        {
            if (!CanEdit) return;
            DataRow row = Current();
            if (row == null || Cell(row, "WarehouseId").Length == 0 || Cell(row, "IfsWarehouseCode").Length == 0) return;
            if (XtraMessageBox.Show(this, "Gỡ map Kho \"" + Cell(row, "WarehouseName") + "\" khỏi Kho IFS " + Cell(row, "IfsWarehouseCode") + "?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                _repository.MapWarehouse(_site, Convert.ToInt32(row["WarehouseId"]), null);
                Reload();
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private void CreateFromIfs(string onlyIfsCode = null)
        {
            if (!CanEdit) return;
            try
            {
                DataTable preview = _repository.CreateWarehousesFromIfs(_site, onlyIfsCode, true);
                if (preview.Rows.Count == 0)
                {
                    SetStatus("Không có Kho IFS nào cần tạo Kho PCTP.", false);
                    return;
                }
                var sb = new StringBuilder();
                sb.AppendLine("Sẽ tạo " + preview.Rows.Count + " Kho PCTP cho Site " + _site + ":");
                foreach (DataRow r in preview.Rows)
                    sb.AppendLine("  " + Cell(r, "IfsWarehouseCode") + "  →  " + Cell(r, "NewWarehouseName"));
                sb.AppendLine().AppendLine("Rack / Slot không bị thay đổi. Tiếp tục?");
                if (XtraMessageBox.Show(this, sb.ToString(), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                DataTable created = _repository.CreateWarehousesFromIfs(_site, onlyIfsCode, false);
                Reload();
                SetStatus("Đã tạo " + created.Rows.Count + " Kho PCTP từ Kho IFS.", false);
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        }

        private string PickIfsWarehouse(string warehouseName, string current)
        {
            DataTable list = _repository.GetIfsWarehouses(_site);
            if (list.Rows.Count == 0)
            {
                SetStatus("Site " + _site + " chưa có Kho IFS. Đồng bộ nhóm WAREHOUSE từ IFS trước.", true);
                return null;
            }
            list.Columns.Add("Display", typeof(string));
            foreach (DataRow r in list.Rows)
                r["Display"] = Cell(r, "WarehouseCode") + (Cell(r, "Description").Length > 0 ? " - " + Cell(r, "Description") : string.Empty);
            using (var dialog = new XtraForm())
            {
                dialog.Text = "Chọn Kho IFS";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(440, 140);
                var label = new LabelControl { Text = "Kho IFS tương ứng với Kho PCTP \"" + warehouseName + "\":", Left = 14, Top = 14 };
                var combo = new LookUpEdit { Left = 14, Top = 42, Width = 410 };
                combo.Properties.DataSource = list;
                combo.Properties.ValueMember = "WarehouseCode";
                combo.Properties.DisplayMember = "Display";
                combo.Properties.Columns.Add(new LookUpColumnInfo("Display", "Kho IFS"));
                combo.Properties.ShowHeader = false;
                combo.Properties.NullText = "";
                if (current.Length > 0) combo.EditValue = current;
                var ok = new SimpleButton { Text = "MAP", Left = 238, Top = 92, Width = 90, Height = 30, DialogResult = DialogResult.OK };
                var cancel = new SimpleButton { Text = "HỦY", Left = 334, Top = 92, Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { label, combo, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK || combo.EditValue == null) return null;
                return Convert.ToString(combo.EditValue);
            }
        }
    }
}
