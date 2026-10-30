using DevExpress.Data;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    /// <summary>
    /// Chế độ XEM TỒN KHO (lotFilter rỗng): chỉ đọc, 3 góc nhìn từ cùng 1 nguồn
    /// (sp_GetCurrentStockStatus) nên khớp với báo cáo "Kho hiện tại".
    ///   1. Theo LOT          — STOCKTP + phần đã xếp slot / chưa gán slot + cột đối chiếu
    ///   2. Theo mã hàng      — cộng dồn các LOT
    ///   3. Chi tiết vị trí   — từng Kho/Rack/Slot + QR
    /// Điều chỉnh tồn (chế độ 1 LOT) vẫn mở riêng qua ShowForLot(owner, lot), không nằm trong màn xem này.
    /// </summary>
    public partial class TONKHOTP
    {
        private const string AllFilter = "Tất cả";
        private const string UnslottedFilter = "Chỉ phần chưa gán slot";
        private const string OpenFilter = "Chỉ LOT đang mở";
        private const string ClosedFilter = "Chỉ LOT đã kết thúc";
        private const string CheckFilter = "Chỉ dòng cần đối chiếu";

        private CancellationTokenSource _onHandCts;
        private XtraTabControl _tabs;
        private GridControl _gridLot, _gridItem, _gridDetail;
        private GridView _viewLot, _viewItem, _viewDetail;
        private ComboBoxEdit _cmbFilter;
        private SimpleButton _btnRefresh, _btnExport;
        private LabelControl _lblSummary;

        private void BuildOnHandUI()
        {
            var top = new PanelControl { Dock = DockStyle.Top, Height = 46 };
            _cmbFilter = new ComboBoxEdit { Left = 10, Top = 11, Width = 200 };
            _cmbFilter.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _cmbFilter.Properties.Items.AddRange(new object[] { AllFilter, UnslottedFilter, OpenFilter, ClosedFilter, CheckFilter });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.SelectedIndexChanged += (s, e) => ApplyOnHandFilter();

            _btnRefresh = new SimpleButton { Left = 220, Top = 9, Width = 90, Height = 27, Text = "Làm mới" };
            _btnRefresh.Click += async (s, e) => await LoadOnHandAsync();
            _btnExport = new SimpleButton { Left = 316, Top = 9, Width = 100, Height = 27, Text = "Xuất Excel" };
            _btnExport.Click += (s, e) => ExportActiveOnHandView();
            _lblSummary = new LabelControl { Left = 430, Top = 14, AutoSizeMode = LabelAutoSizeMode.None, Width = 800, Height = 20 };
            _lblSummary.Appearance.Font = new Font("Tahoma", 9, FontStyle.Bold);
            top.Controls.AddRange(new Control[] { _cmbFilter, _btnRefresh, _btnExport, _lblSummary });

            _tabs = new XtraTabControl { Dock = DockStyle.Fill };
            var tabLot = new XtraTabPage { Text = "Theo LOT" };
            var tabItem = new XtraTabPage { Text = "Theo mã hàng" };
            var tabDetail = new XtraTabPage { Text = "Chi tiết vị trí & QR" };
            CreateOnHandGrid(out _gridLot, out _viewLot); tabLot.Controls.Add(_gridLot);
            CreateOnHandGrid(out _gridItem, out _viewItem); tabItem.Controls.Add(_gridItem);
            CreateOnHandGrid(out _gridDetail, out _viewDetail); tabDetail.Controls.Add(_gridDetail);
            _tabs.TabPages.AddRange(new[] { tabLot, tabItem, tabDetail });

            Controls.Add(_tabs);   // Fill trước, Top sau (thứ tự Dock)
            Controls.Add(top);

            _viewLot.RowStyle += OnHandRowStyle;
            _viewDetail.RowStyle += OnHandRowStyle;
            _viewLot.CustomColumnDisplayText += OnHandDisplayText;
            _viewDetail.CustomColumnDisplayText += OnHandDisplayText;
            _viewItem.RowStyle += OnHandRowStyle;

            FormClosed += (s, e) => { if (_onHandCts != null) { _onHandCts.Cancel(); _onHandCts.Dispose(); _onHandCts = null; } };
            Shown += async (s, e) => await LoadOnHandAsync();
        }

        private static void CreateOnHandGrid(out GridControl grid, out GridView view)
        {
            grid = new GridControl { Dock = DockStyle.Fill };
            view = new GridView(grid);
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowFooter = true;
            view.OptionsView.ShowAutoFilterRow = true;
            view.OptionsFind.AlwaysVisible = true;                       // ô tìm nhanh: mã hàng / LOT / QR / vị trí
            view.OptionsFind.FindNullPrompt = "Tìm mã hàng / LOT / QR / vị trí...";
        }

        private async Task LoadOnHandAsync()
        {
            if (_onHandCts != null) _onHandCts.Cancel();
            _onHandCts = new CancellationTokenSource();
            var token = _onHandCts.Token;

            _btnRefresh.Enabled = false;
            UseWaitCursor = true;
            try
            {
                var query = _currentStockQuery;
                // Truy vấn chạy nền để form không bị đơ.
                var rows = await Task.Run(() => query.GetAsync(token), token);
                if (token.IsCancellationRequested || IsDisposed) return;

                var lots = StockOnHandAggregator.BuildLotRows(rows);
                var items = StockOnHandAggregator.BuildItemRows(lots);
                var detail = rows.OrderByDescending(r => r.SlotImportDate.HasValue || r.ReceivedDate.HasValue)
                                 .ThenByDescending(r => r.SlotImportDate ?? r.ReceivedDate)
                                 .ThenBy(r => r.LotNo, StringComparer.OrdinalIgnoreCase)
                                 .ToList();

                BindOnHand(_viewLot, _gridLot, lots, ConfigureLotColumns);
                BindOnHand(_viewItem, _gridItem, items, ConfigureItemColumns);
                BindOnHand(_viewDetail, _gridDetail, detail, ConfigureDetailColumns);
                ApplyOnHandFilter();

                decimal onHand = lots.Sum(l => l.QtyOnHand), unslotted = lots.Sum(l => l.QtyUnslotted);
                _lblSummary.Text = string.Format(
                    "{0:n0} mã hàng | {1:n0} LOT | Tổng tồn: {2:n0} | Chưa gán slot: {3:n0} ({4:n0} LOT) | Cần đối chiếu: {5:n0} LOT",
                    items.Count, lots.Count, onHand, unslotted,
                    lots.Count(l => l.QtyUnslotted > 0), lots.Count(l => l.Check != "OK"));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể tải tồn kho", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed) { UseWaitCursor = false; _btnRefresh.Enabled = true; }
            }
        }

        private static void BindOnHand(GridView view, GridControl grid, object data, Action<GridView> configure)
        {
            view.BeginUpdate();
            try
            {
                view.Columns.Clear();
                grid.DataSource = data;
                view.PopulateColumns();
                configure(view);
            }
            finally { view.EndUpdate(); }
        }

        // ── Cột ─────────────────────────────────────────────────────────
        private static void Col(GridView v, ref int idx, string field, string caption, int width,
                                string format = null, bool sum = false, bool right = false)
        {
            var c = v.Columns[field];
            if (c == null) return;
            c.Caption = caption; c.Width = width; c.Visible = true; c.VisibleIndex = idx++;
            if (!string.IsNullOrEmpty(format))
            {
                c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
                c.DisplayFormat.FormatString = format;
            }
            if (right) c.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Far;
            if (sum) { c.SummaryItem.SummaryType = SummaryItemType.Sum; c.SummaryItem.DisplayFormat = "Σ {0:n0}"; }
        }

        private static void ShowOnly(GridView v, params string[] keep)
        {
            foreach (GridColumn c in v.Columns)
                if (Array.IndexOf(keep, c.FieldName) < 0) c.Visible = false;
        }

        private static void SortNewestFirst(GridView v, string dateField, string tieField)
        {
            v.SortInfo.Clear();
            var d = v.Columns[dateField];
            var t = v.Columns[tieField];
            if (d != null) v.SortInfo.Add(new GridColumnSortInfo(d, ColumnSortOrder.Descending));
            if (t != null) v.SortInfo.Add(new GridColumnSortInfo(t, ColumnSortOrder.Ascending));
        }

        private void ConfigureLotColumns(GridView v)
        {
            int i = 0;
            Col(v, ref i, "LastReceivedDate", "Ngày nhập mới nhất", 125, "dd/MM/yyyy HH:mm");
            Col(v, ref i, "ItemCode", "Mã hàng", 140);
            Col(v, ref i, "ItemName", "Tên sản phẩm", 190);
            Col(v, ref i, "Model", "Model", 70);
            Col(v, ref i, "LotNo", "LOT", 190);
            Col(v, ref i, "QtyOnHand", "Tồn LOT", 85, "n0", true, true);
            Col(v, ref i, "QtySlotted", "Đã xếp slot", 90, "n0", true, true);
            Col(v, ref i, "QtyUnslotted", "Chưa gán slot", 95, "n0", true, true);
            Col(v, ref i, "SlotCount", "Số slot", 60, "n0", false, true);
            Col(v, ref i, "QtyProduced", "SL sản xuất", 90, "n0", false, true);
            Col(v, ref i, "QtyReceived", "SL đã nhập", 90, "n0", false, true);
            Col(v, ref i, "QtyIssued", "SL đã xuất", 90, "n0", false, true);
            Col(v, ref i, "Status", "Trạng thái", 90);
            Col(v, ref i, "ProductionDate", "Ngày SX", 85, "dd/MM/yyyy");
            Col(v, ref i, "ShiftNo", "Ca SX", 50);
            Col(v, ref i, "IssuedDate", "Ngày xuất gần nhất", 115, "dd/MM/yyyy");
            Col(v, ref i, "Check", "Đối chiếu", 320);
            ShowOnly(v, "LastReceivedDate", "ItemCode", "ItemName", "Model", "LotNo", "QtyOnHand", "QtySlotted", "QtyUnslotted",
                "SlotCount", "QtyProduced", "QtyReceived", "QtyIssued", "Status", "ProductionDate", "ShiftNo", "IssuedDate", "Check");
            var lot = v.Columns["LotNo"];
            if (lot != null) { lot.SummaryItem.SummaryType = SummaryItemType.Count; lot.SummaryItem.DisplayFormat = "{0:n0} LOT"; }
            SortNewestFirst(v, "LastReceivedDate", "LotNo");
        }

        private void ConfigureItemColumns(GridView v)
        {
            int i = 0;
            Col(v, ref i, "LastReceivedDate", "Ngày nhập mới nhất", 125, "dd/MM/yyyy HH:mm");
            Col(v, ref i, "ItemCode", "Mã hàng", 150);
            Col(v, ref i, "ItemName", "Tên sản phẩm", 220);
            Col(v, ref i, "LotCount", "Số LOT", 70, "n0", true, true);
            Col(v, ref i, "OpenLotCount", "LOT đang mở", 90, "n0", true, true);
            Col(v, ref i, "QtyOnHand", "Tổng tồn", 95, "n0", true, true);
            Col(v, ref i, "QtySlotted", "Đã xếp slot", 95, "n0", true, true);
            Col(v, ref i, "QtyUnslotted", "Chưa gán slot", 100, "n0", true, true);
            Col(v, ref i, "LotsToCheck", "LOT cần đối chiếu", 120, "n0", true, true);
            ShowOnly(v, "LastReceivedDate", "ItemCode", "ItemName", "LotCount", "OpenLotCount", "QtyOnHand", "QtySlotted",
                "QtyUnslotted", "LotsToCheck");
            SortNewestFirst(v, "LastReceivedDate", "ItemCode");
        }

        private void ConfigureDetailColumns(GridView v)
        {
            int i = 0;
            Col(v, ref i, "SlotImportDate", "Ngày nhập slot", 125, "dd/MM/yyyy HH:mm");
            Col(v, ref i, "ItemCode", "Mã hàng", 140);
            Col(v, ref i, "ItemName", "Tên sản phẩm", 190);
            Col(v, ref i, "LotNo", "LOT", 190);
            Col(v, ref i, "LocationCode", "Vị trí (Kho-Rack-Slot)", 200);
            Col(v, ref i, "Quantity", "SL tại vị trí", 95, "n0", true, true);
            Col(v, ref i, "QrCode", "QR Code", 320);
            Col(v, ref i, "DocumentNo", "Số phiếu", 120);
            Col(v, ref i, "Status", "Trạng thái LOT", 100);
            ShowOnly(v, "SlotImportDate", "ItemCode", "ItemName", "LotNo", "LocationCode", "Quantity", "QrCode", "DocumentNo", "Status");
            // Không đặt sort theo cột: danh sách đã được xếp sẵn (ngày nhập slot / ngày nhập LOT mới nhất lên trên),
            // dòng "Chưa gán slot" không có ngày nhập slot nên vẫn xếp đúng theo ngày nhập LOT.
            v.SortInfo.Clear();
        }

        // ── Hiển thị / màu / lọc ────────────────────────────────────────
        private static void OnHandDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
        {
            var text = e.Value as string;
            if (text == null) return;
            if (e.Column.FieldName == "LocationCode" && StockOnHandAggregator.IsUnslotted(text)) e.DisplayText = "Chưa gán slot";
            else if (e.Column.FieldName == "Status")
                e.DisplayText = StockOnHandAggregator.IsClosed(text) ? "Đã kết thúc" : string.Equals(text.Trim(), "Dang mo", StringComparison.OrdinalIgnoreCase) ? "Đang mở" : text;
        }

        private void OnHandRowStyle(object sender, RowStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            var view = (GridView)sender;
            var row = view.GetRow(e.RowHandle);

            var lot = row as StockOnHandLotRow;
            var det = row as CurrentStockRow;
            var item = row as StockOnHandItemRow;

            if ((lot != null && lot.LotNoFormatError) || (det != null && det.LotNoFormatError))
                e.Appearance.BackColor = Color.FromArgb(255, 225, 225);                 // LOT sai định dạng
            else if ((lot != null && lot.QtyUnslotted > 0) || (det != null && StockOnHandAggregator.IsUnslotted(det.LocationCode))
                     || (item != null && item.QtyUnslotted > 0))
                e.Appearance.BackColor = Color.FromArgb(255, 247, 200);                 // còn phần chưa gán slot
            else if (lot != null && lot.Check != "OK")
                e.Appearance.BackColor = Color.FromArgb(255, 235, 205);                 // lệch nhập-xuất-tồn / slot > tồn
        }

        private void ApplyOnHandFilter()
        {
            string choice = Convert.ToString(_cmbFilter.EditValue);
            string lotExpr = null, itemExpr = null, detailExpr = null;

            switch (choice)
            {
                case UnslottedFilter:
                    lotExpr = "[QtyUnslotted] > 0"; itemExpr = "[QtyUnslotted] > 0";
                    detailExpr = "[LocationCode] = '" + StockOnHandAggregator.UnslottedLocation + "'"; break;
                case OpenFilter:
                    lotExpr = "[Status] <> 'Da ket thuc'"; itemExpr = "[OpenLotCount] > 0"; detailExpr = "[Status] <> 'Da ket thuc'"; break;
                case ClosedFilter:
                    lotExpr = "[Status] = 'Da ket thuc'"; itemExpr = "[LotCount] > [OpenLotCount]"; detailExpr = "[Status] = 'Da ket thuc'"; break;
                case CheckFilter:
                    lotExpr = "[Check] <> 'OK'"; itemExpr = "[LotsToCheck] > 0";
                    detailExpr = "[LotNoFormatError] = True Or [LocationCode] = '" + StockOnHandAggregator.UnslottedLocation + "'"; break;
            }

            _viewLot.ActiveFilterString = lotExpr ?? string.Empty;
            _viewItem.ActiveFilterString = itemExpr ?? string.Empty;
            _viewDetail.ActiveFilterString = detailExpr ?? string.Empty;
        }

        private void ExportActiveOnHandView()
        {
            var view = _tabs.SelectedTabPageIndex == 1 ? _viewItem : _tabs.SelectedTabPageIndex == 2 ? _viewDetail : _viewLot;
            if (view.RowCount == 0) return;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel File (*.xlsx)|*.xlsx";
                dialog.FileName = "TonKhoThanhPham_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                view.ExportToXlsx(dialog.FileName);
                XtraMessageBox.Show(this, "Xuất Excel thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
