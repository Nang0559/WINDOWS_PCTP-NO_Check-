using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using DevExpress.Data;
using DevExpress.XtraGrid.Columns;
using PCTP.Shell.UI;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    [DesignerCategory("Code")]
    public sealed class FormBaoCaoStockHistory : WmsTitleBarForm
    {
        private readonly IStockHistoryQuery _historyQuery;
        private readonly ICurrentStockQuery _currentStockQuery;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private DateEdit _dateFrom;
        private DateEdit _dateTo;
        private LookUpEdit _lookupItemCode;
        private SimpleButton _btnSearch;
        private SimpleButton _btnCurrentStock;
        private SimpleButton _btnExport;
        private SimpleButton _btnPrint;
        private GridControl _grid;
        private GridView _view;
        private bool _showingCurrentStock;

        public FormBaoCaoStockHistory()
            : this(new StockHistoryQueryService(), new CurrentStockQueryService()) { }

        public FormBaoCaoStockHistory(IStockHistoryQuery historyQuery, ICurrentStockQuery currentStockQuery)
        {
            if (historyQuery == null) throw new ArgumentNullException("historyQuery");
            if (currentStockQuery == null) throw new ArgumentNullException("currentStockQuery");
            _historyQuery = historyQuery;
            _currentStockQuery = currentStockQuery;
            InitializeComponent();
            Shown += async (s, e) => await LoadItemCodesAsync();
            FormClosed += (s, e) => _lifetime.Cancel();
        }

        private void InitializeComponent()
        {
            Text = "Báo cáo - Lịch sử kho";
            ClientSize = new System.Drawing.Size(1184, 672);
            StartPosition = FormStartPosition.CenterScreen;

            var top = new PanelControl { Dock = DockStyle.Top, Height = 72 };
            var historyGroup = new GroupControl { Text = "Tra cứu lịch sử nhập xuất", Dock = DockStyle.Fill };
            var currentGroup = new GroupControl { Text = "Kho hiện tại", Dock = DockStyle.Left, Width = 190 };
            var actionPanel = new PanelControl { Dock = DockStyle.Right, Width = 205 };

            _dateFrom = new DateEdit { Left = 12, Top = 32, Width = 145 };
            _dateFrom.Properties.NullText = "Từ ngày";
            _dateTo = new DateEdit { Left = 164, Top = 32, Width = 145 };
            _dateTo.Properties.NullText = "Đến ngày";
            _lookupItemCode = new LookUpEdit { Left = 316, Top = 32, Width = 205 };
            _lookupItemCode.Properties.NullText = "Mã hàng";
            _lookupItemCode.Properties.DisplayMember = "ItemCode";
            _lookupItemCode.Properties.ValueMember = "ItemCode";
            _btnSearch = new SimpleButton { Left = 526, Top = 29, Width = 90, Text = "Tìm kiếm" };
            _btnSearch.Click += async (s, e) => await SearchAsync();

            _btnCurrentStock = new SimpleButton { Left = 10, Top = 29, Width = 165, Text = "Thống kê kho hiện tại" };
            _btnCurrentStock.Click += async (s, e) => await LoadCurrentStockAsync();
            _btnExport = new SimpleButton { Left = 8, Top = 22, Width = 90, Text = "Xuất Excel" };
            _btnExport.Click += ExportClick;
            _btnPrint = new SimpleButton { Left = 105, Top = 22, Width = 90, Text = "In báo cáo" };
            _btnPrint.Click += PrintClick;

            historyGroup.Controls.AddRange(new Control[] { _dateFrom, _dateTo, _lookupItemCode, _btnSearch });
            currentGroup.Controls.Add(_btnCurrentStock);
            actionPanel.Controls.AddRange(new Control[] { _btnExport, _btnPrint });
            top.Controls.Add(historyGroup);
            top.Controls.Add(currentGroup);
            top.Controls.Add(actionPanel);

            _grid = new GridControl { Dock = DockStyle.Fill };
            _view = new GridView(_grid);
            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsView.ColumnAutoWidth = false;      // nhiều cột -> cuộn ngang thay vì ép hẹp
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ShowFooter = true;
            _view.RowStyle += ViewRowStyle;
            Controls.Add(_grid);
            Controls.Add(top);
        }

        private async Task LoadItemCodesAsync()
        {
            try
            {
                var codes = await _historyQuery.GetItemCodesAsync(_lifetime.Token);
                _lookupItemCode.Properties.DataSource = codes;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể tải mã hàng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SearchAsync()
        {
            try
            {
                _showingCurrentStock = false;
                var criteria = new HistorySearchCriteria
                {
                    From = ToDate(_dateFrom.EditValue),
                    To = ToDate(_dateTo.EditValue),
                    PartNo = _lookupItemCode.EditValue == null ? null : _lookupItemCode.EditValue.ToString()
                };
                var rows = await _historyQuery.SearchAsync(criteria, _lifetime.Token);
                Bind(rows, ConfigureHistoryColumns);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể tra cứu lịch sử kho", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadCurrentStockAsync()
        {
            try
            {
                _showingCurrentStock = true;
                var rows = await _currentStockQuery.GetAsync(_lifetime.Token);
                Bind(rows, ConfigureCurrentStockColumns);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể tải tồn kho hiện tại", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Đổi bộ dữ liệu: xoá cột cũ để 2 chế độ (lịch sử / kho hiện tại) không dính cột của nhau.</summary>
        private void Bind(object rows, Action configure)
        {
            _view.BeginUpdate();
            try
            {
                _view.Columns.Clear();
                _view.GroupSummary.Clear();
                _grid.DataSource = rows;
                _view.PopulateColumns();
                configure();
            }
            finally { _view.EndUpdate(); }
        }

        private void Col(string field, string caption, int width, string format = null, bool sum = false, bool right = false)
        {
            var c = _view.Columns[field];
            if (c == null) return;
            c.Caption = caption;
            c.Width = width;
            c.Visible = true;
            c.VisibleIndex = _colIndex++;
            c.OptionsColumn.AllowEdit = false;
            if (!string.IsNullOrEmpty(format))
            {
                c.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
                c.DisplayFormat.FormatString = format;
            }
            if (right) c.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Far;
            if (sum)
            {
                c.SummaryItem.SummaryType = SummaryItemType.Sum;
                c.SummaryItem.DisplayFormat = "Σ {0:n0}";
            }
        }

        private int _colIndex;

        private void HideOthers(params string[] keep)
        {
            foreach (GridColumn c in _view.Columns)
                if (Array.IndexOf(keep, c.FieldName) < 0) c.Visible = false;
        }

        private void ConfigureCurrentStockColumns()
        {
            _colIndex = 0;
            // LotNo / QrCode là chuỗi -> không bị định dạng số; dạng "2.61E+19" nếu có là do dữ liệu gốc (xem cột Cảnh báo).
            Col("ItemCode", "Mã hàng", 140);
            Col("ItemName", "Tên sản phẩm", 190);
            Col("Model", "Model", 70);
            Col("LotNo", "LOT No", 190);
            Col("QrCode", "QR Code", 300);
            Col("LocationCode", "Vị trí (Kho-Rack-Slot)", 190);
            Col("Quantity", "SL tại vị trí", 90, "n0", true, true);
            Col("QtyProduced", "SL sản xuất", 90, "n0", false, true);
            Col("QtyReceived", "SL đã nhập (LOT)", 105, "n0", false, true);
            Col("QtyIssued", "SL đã xuất (LOT)", 105, "n0", false, true);
            Col("QtyOnHandLot", "Tồn LOT", 80, "n0", false, true);
            Col("Status", "Trạng thái LOT", 100);
            Col("ProductionDate", "Ngày SX", 85, "dd/MM/yyyy");
            Col("ShiftNo", "Ca SX", 50);
            Col("ReceivedDate", "Ngày nhập LOT", 100, "dd/MM/yyyy");
            Col("SlotImportDate", "Ngày nhập slot", 125, "dd/MM/yyyy HH:mm");
            Col("IssuedDate", "Ngày xuất gần nhất", 115, "dd/MM/yyyy");
            Col("DocumentNo", "Số phiếu", 120);
            Col("Warning", "Cảnh báo", 300);
            HideOthers("ItemCode", "ItemName", "Model", "LotNo", "QrCode", "LocationCode", "Quantity", "QtyProduced",
                "QtyReceived", "QtyIssued", "QtyOnHandLot", "Status", "ProductionDate", "ShiftNo", "ReceivedDate",
                "SlotImportDate", "IssuedDate", "DocumentNo", "Warning");
            var item = _view.Columns["ItemCode"];
            if (item != null) { item.SummaryItem.SummaryType = SummaryItemType.Count; item.SummaryItem.DisplayFormat = "{0:n0} dòng"; }
        }

        private void ConfigureHistoryColumns()
        {
            _colIndex = 0;
            Col("EventTime", "Thời gian", 125, "dd/MM/yyyy HH:mm:ss");
            Col("ActionType", "Loại giao dịch", 130);
            Col("Direction", "Nhập/Xuất", 75);
            Col("ItemCode", "Mã hàng", 140);
            Col("LotNo", "LOT No", 190);
            Col("QrCode", "QR Code", 300);
            Col("QuantityIn", "SL nhập", 80, "n0;-n0;", true, true);   // 0 hiển thị trống
            Col("QuantityOut", "SL xuất", 80, "n0;-n0;", true, true);
            Col("Quantity", "SL giao dịch", 90, "n0", false, true);
            Col("FromLocation", "Từ vị trí", 190);
            Col("ToLocation", "Đến vị trí", 190);
            Col("DocumentNo", "Số phiếu", 130);
            Col("UserName", "Người thực hiện", 120);
            HideOthers("EventTime", "ActionType", "Direction", "ItemCode", "LotNo", "QrCode", "QuantityIn", "QuantityOut",
                "Quantity", "FromLocation", "ToLocation", "DocumentNo", "UserName");
            var ev = _view.Columns["EventTime"];
            if (ev != null) { ev.SummaryItem.SummaryType = SummaryItemType.Count; ev.SummaryItem.DisplayFormat = "{0:n0} giao dịch"; }
            _view.SortInfo.Clear();
            if (ev != null) ev.SortOrder = DevExpress.Data.ColumnSortOrder.Descending;
        }

        /// <summary>Tô đỏ nhạt dòng có LOT sai định dạng (2.61E+19) để người dùng biết đó là lỗi dữ liệu gốc.</summary>
        private void ViewRowStyle(object sender, RowStyleEventArgs e)
        {
            if (e.RowHandle < 0) return;
            var cur = _view.GetRow(e.RowHandle) as CurrentStockRow;
            var his = _view.GetRow(e.RowHandle) as ItemHistoryRow;
            if ((cur != null && cur.LotNoFormatError) || (his != null && his.LotNoFormatError))
            {
                e.Appearance.BackColor = System.Drawing.Color.FromArgb(255, 225, 225);
            }
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_grid.DataSource == null) return;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel File (*.xlsx)|*.xlsx";
                dialog.FileName = (_showingCurrentStock ? "KhoHienTai_" : "LichSuNhapXuat_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _view.ExportToXlsx(dialog.FileName);
                XtraMessageBox.Show(this, "Xuất Excel thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void PrintClick(object sender, EventArgs e)
        {
            if (_grid.DataSource == null) return;
            _view.OptionsPrint.AutoWidth = false;
            _view.OptionsPrint.PrintHeader = true;
            _view.OptionsPrint.PrintFooter = true;
            _grid.ShowPrintPreview();
        }

        private static DateTime? ToDate(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            return Convert.ToDateTime(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _lifetime.Dispose();
            base.Dispose(disposing);
        }
    }
}
