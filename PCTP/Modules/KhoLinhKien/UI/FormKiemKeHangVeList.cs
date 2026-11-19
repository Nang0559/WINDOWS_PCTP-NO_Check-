using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.Infrastructure.Security;
using PCTP.Modules.NhapKho.ComponentReceiving;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.KhoLinhKien.UI
{
    /// <summary>
    /// Màn hình tổng hợp các yêu cầu "Kiểm kê hàng về" (PO linh kiện đã phát hành).
    /// Nhấp đúp / Enter / nút "Nhập liệu" trên một yêu cầu để mở màn hình đọc QR (FormKiemKeHangVeScan).
    /// </summary>
    public sealed class FormKiemKeHangVeList : XtraForm
    {
        private sealed class Preset
        {
            public string Caption;
            public Func<DataRow, bool> Match;
            public override string ToString() { return Caption; }
        }

        private readonly IComponentReceivingQueryService _query;
        private readonly ListBoxControl _presets = new ListBoxControl();
        private readonly GridControl _gridRequests = new GridControl();
        private readonly GridView _viewRequests = new GridView();
        private readonly GridControl _gridLots = new GridControl();
        private readonly GridView _viewLots = new GridView();
        private readonly GridControl _gridLabels = new GridControl();
        private readonly GridView _viewLabels = new GridView();
        private readonly LabelControl _status = new LabelControl();
        private readonly SimpleButton _btnOpen = new SimpleButton();

        private DataTable _all;
        private long _detailForId = -1;

        public FormKiemKeHangVeList()
            : this(new ComponentReceivingQueryService(new ComponentReceivingQueryRepository()))
        {
        }

        public FormKiemKeHangVeList(IComponentReceivingQueryService query)
        {
            AuthorizationGuard.Demand("WAREHOUSE.COMPONENT_RECEIPT", "VIEW");
            _query = query ?? throw new ArgumentNullException(nameof(query));
            Text = "Kiểm kê hàng về";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1500, 860);
            MinimumSize = new Size(1000, 600);
            BuildLayout();
            Load += (s, e) => LoadRequests();
        }

        // ───────────────────────────── Layout ─────────────────────────────

        private void BuildLayout()
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(6, 4, 0, 0) };
            _btnOpen.Text = "Nhập liệu (quét QR)";
            _btnOpen.Width = 160; _btnOpen.Height = 30;
            _btnOpen.Click += (s, e) => OpenSelected();
            var btnRefresh = new SimpleButton { Text = "Làm mới", Width = 100, Height = 30 };
            btnRefresh.Click += (s, e) => LoadRequests();
            top.Controls.Add(_btnOpen);
            top.Controls.Add(btnRefresh);

            _status.Dock = DockStyle.Bottom;
            _status.AutoSizeMode = LabelAutoSizeMode.None;
            _status.Height = 26;
            _status.Padding = new Padding(8, 4, 0, 0);

            // Bên trái: bộ lọc nhanh (chưa xong / đã xong / theo ngày-tháng-năm)
            _presets.Dock = DockStyle.Fill;
            _presets.Appearance.Font = new Font("Tahoma", 10F);
            _presets.ItemHeight = 28;
            var today = DateTime.Today;
            _presets.Items.Add(new Preset { Caption = "Kiểm kê hàng về (đang xử lý)", Match = r => IsOpen(r) });
            _presets.Items.Add(new Preset { Caption = "Dữ liệu đã kết thúc", Match = r => !IsOpen(r) });
            _presets.Items.Add(new Preset { Caption = "Chứng từ trong ngày", Match = r => OrderDate(r) == today });
            _presets.Items.Add(new Preset { Caption = "Chứng từ trong tháng", Match = r => OrderDate(r).Year == today.Year && OrderDate(r).Month == today.Month });
            _presets.Items.Add(new Preset { Caption = "Chứng từ trong năm", Match = r => OrderDate(r).Year == today.Year });
            _presets.Items.Add(new Preset { Caption = "Tất cả", Match = r => true });
            _presets.SelectedIndex = 0;
            _presets.SelectedIndexChanged += (s, e) => ApplyPreset();

            // Lưới yêu cầu (nhóm theo trạng thái)
            ConfigureView(_gridRequests, _viewRequests);
            _viewRequests.OptionsView.ShowGroupPanel = false;
            _viewRequests.OptionsBehavior.AutoExpandAllGroups = true;
            _viewRequests.DoubleClick += (s, e) => OpenSelected();
            _viewRequests.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; OpenSelected(); } };
            _viewRequests.FocusedRowChanged += (s, e) => LoadDetail();

            // Chi tiết bên dưới: 2 tab như màn hình cũ
            ConfigureView(_gridLots, _viewLots);
            ConfigureView(_gridLabels, _viewLabels);
            var tabs = new XtraTabControl { Dock = DockStyle.Fill };
            var tabLots = new XtraTabPage { Text = "Chi tiết kiểm kê" };
            var tabLabels = new XtraTabPage { Text = "Danh sách tem" };
            tabLots.Controls.Add(_gridLots);
            tabLabels.Controls.Add(_gridLabels);
            tabs.TabPages.AddRange(new[] { tabLots, tabLabels });

            var split = new SplitContainerControl
            {
                Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 420
            };
            split.Panel1.Controls.Add(_gridRequests);
            split.Panel2.Controls.Add(tabs);

            var outer = new SplitContainerControl
            {
                Dock = DockStyle.Fill, Horizontal = true, SplitterPosition = 230,
                FixedPanel = SplitFixedPanel.Panel1
            };
            outer.Panel1.Controls.Add(_presets);
            outer.Panel2.Controls.Add(split);

            Controls.Add(outer);
            Controls.Add(top);
            Controls.Add(_status);
        }

        private static void ConfigureView(GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.EnableAppearanceEvenRow = true;
            view.OptionsFind.AlwaysVisible = false;
            view.Appearance.HeaderPanel.Options.UseTextOptions = true;
            view.Appearance.HeaderPanel.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            view.ColumnPanelRowHeight = 36;
        }

        // ───────────────────────────── Dữ liệu ─────────────────────────────

        private static DateTime OrderDate(DataRow r) { return Convert.ToDateTime(r["OrderDate"]).Date; }
        private static bool IsOpen(DataRow r)
        {
            string s = Convert.ToString(r["Status"]);
            return s == "RELEASED" || s == "PARTIALLY_RECEIVED";
        }

        private void LoadRequests()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _status.Text = "Đang tải yêu cầu kiểm kê hàng về...";
                _all = _query.ListRequests();
                ApplyPreset();
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được danh sách: " + ex.Message;
                XtraMessageBox.Show(this, ex.Message, "Kiểm kê hàng về", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void ApplyPreset()
        {
            if (_all == null) return;
            var preset = _presets.SelectedItem as Preset;
            DataTable shown = _all.Clone();
            foreach (DataRow r in _all.Rows)
                if (preset == null || preset.Match(r)) shown.ImportRow(r);

            long keep = SelectedRequestId();
            _detailForId = -1;
            _gridRequests.DataSource = shown;
            FormatRequestColumns();
            _status.Text = shown.Rows.Count + " yêu cầu — nhấp đúp để mở màn hình đọc QR.";

            if (keep > 0)
                for (int i = 0; i < _viewRequests.DataRowCount; i++)
                    if (Convert.ToInt64(_viewRequests.GetRowCellValue(i, "PurchaseOrderId")) == keep)
                    { _viewRequests.FocusedRowHandle = i; break; }
            LoadDetail();
        }

        private void FormatRequestColumns()
        {
            Cfg(_viewRequests, "PurchaseOrderId", visible: false);
            Cfg(_viewRequests, "Status", visible: false);
            Cfg(_viewRequests, "SupplierCode", visible: false);
            Cfg(_viewRequests, "StatusText", "Trạng thái", 130);
            Cfg(_viewRequests, "PurchaseOrderNo", "Số", 120);
            Cfg(_viewRequests, "OrderDate", "Ngày", 100, "dd/MM/yyyy");
            Cfg(_viewRequests, "SiteCode", "Site", 70);
            Cfg(_viewRequests, "SupplierName", "Nhà cung cấp", 300);
            Cfg(_viewRequests, "ReleasedBy", "Người lập", 140);
            Cfg(_viewRequests, "LineCount", "Số dòng", 70, "#,##0");
            Cfg(_viewRequests, "OrderQty", "SL đặt", 90, "#,##0.###");
            Cfg(_viewRequests, "ReceivedQty", "SL đã nhận", 90, "#,##0.###");
            Cfg(_viewRequests, "LastUpdateUtc", "Cập nhật gần đây nhất", 150, "dd/MM/yyyy HH:mm");
            if (_viewRequests.Columns["StatusText"] != null)
            {
                _viewRequests.Columns["StatusText"].GroupIndex = 0;
                _viewRequests.Columns["StatusText"].SortOrder = DevExpress.Data.ColumnSortOrder.Ascending;
            }
            _viewRequests.ExpandAllGroups();
        }

        private long SelectedRequestId()
        {
            if (_viewRequests.FocusedRowHandle < 0) return 0;
            object v = _viewRequests.GetFocusedRowCellValue("PurchaseOrderId");
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        private void LoadDetail()
        {
            long id = SelectedRequestId();
            if (id <= 0) { _gridLots.DataSource = null; _gridLabels.DataSource = null; _detailForId = -1; return; }
            if (id == _detailForId) return;
            _detailForId = id;
            try
            {
                DataTable lots = _query.ListLots(id);
                _gridLots.DataSource = lots;
                _gridLabels.DataSource = lots;
                FormatLotColumns(_viewLots);
                FormatLabelColumns(_viewLabels);
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được chi tiết: " + ex.Message;
            }
        }

        internal static void FormatLotColumns(GridView v)
        {
            foreach (GridColumn c in v.Columns) c.Visible = false;
            Cfg(v, "LotNo", "Số lot", 170, null, 0);
            Cfg(v, "PurchaseOrderNo", "Số PO", 80, null, 1);
            Cfg(v, "OrderDate", "Ngày PO", 90, "dd/MM/yyyy", 2);
            Cfg(v, "SupplierName", "Nhà cung cấp", 250, null, 3);
            Cfg(v, "SupplierCode", "Mã maker", 80, null, 4);
            Cfg(v, "PartNo", "Mã vật tư", 150, null, 5);
            Cfg(v, "PartDescription", "Tên phụ tùng", 230, null, 6);
            Cfg(v, "UnitCode", "Đvt", 50, null, 7);
            Cfg(v, "NeedByDate", "Ngày y.cầu", 90, "dd/MM/yyyy", 8);
            Cfg(v, "OrderQty", "SL đặt", 80, "#,##0.###", 9);
            Cfg(v, "IssuedQty", "SL theo tem", 90, "#,##0.###", 10);
            Cfg(v, "ReceivedQty", "SL thực tế", 90, "#,##0.###", 11);
        }

        private static void FormatLabelColumns(GridView v)
        {
            foreach (GridColumn c in v.Columns) c.Visible = false;
            Cfg(v, "LotNo", "Số lot", 190, null, 0);
            Cfg(v, "QrPayload", "Nội dung QR", 260, null, 1);
            Cfg(v, "PartNo", "Mã vật tư", 150, null, 2);
            Cfg(v, "IssuedQty", "SL trên tem", 90, "#,##0.###", 3);
            Cfg(v, "LotStatus", "Trạng thái tem", 130, null, 4);
            Cfg(v, "LabelPrintCount", "Số tem đã in", 90, "#,##0", 5);
        }

        private static void Cfg(GridView v, string field, string caption = null, int width = 100,
            string format = null, int order = -1, bool visible = true)
        {
            GridColumn c = v.Columns[field];
            if (c == null) return;
            c.Visible = visible;
            if (!visible) return;
            if (caption != null) c.Caption = caption;
            c.Width = width;
            if (format != null)
            {
                c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
                c.DisplayFormat.FormatString = format;
            }
            if (order >= 0) c.VisibleIndex = order;
        }

        // ───────────────────────────── Mở màn hình quét QR ─────────────────────────────

        private void OpenSelected()
        {
            if (_viewRequests.FocusedRowHandle < 0 || _viewRequests.IsGroupRow(_viewRequests.FocusedRowHandle))
            {
                _status.Text = "Chọn một yêu cầu để nhập liệu.";
                return;
            }
            var row = _viewRequests.GetFocusedDataRow();
            if (row == null) return;
            try
            {
                using (var scan = new FormKiemKeHangVeScan(
                    Convert.ToInt64(row["PurchaseOrderId"]),
                    Convert.ToString(row["SiteCode"]),
                    Convert.ToString(row["PurchaseOrderNo"]),
                    Convert.ToDateTime(row["OrderDate"]),
                    Convert.ToString(row["ReleasedBy"])))
                {
                    scan.ShowDialog(this);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Kiểm kê hàng về", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _detailForId = -1;
            LoadRequests();   // số lượng/trạng thái có thể đã đổi sau khi quét
        }
    }
}
