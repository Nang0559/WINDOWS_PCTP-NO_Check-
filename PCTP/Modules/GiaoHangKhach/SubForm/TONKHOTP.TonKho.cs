using DevExpress.Data;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using PCTP.Common;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Models;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Shared.Lookup;
using PCTP.Shared.Helpers;
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
        private StockLookupControl _stockLookup;
        // Dữ liệu tồn kho gốc đã tải + tiêu chí tra cứu đang áp dụng (null = xem tất cả).
        // Mọi lưới (LOT / mã hàng / vị trí) được dựng lại từ CÙNG tập dòng đã lọc nên tổng tồn luôn khớp nhau.
        private IReadOnlyList<CurrentStockRow> _onHandRows;
        private StockLookupCriteria _onHandCriteria;
        private SimpleButton _btnRefresh, _btnExport;
        private LabelControl _lblSummary;

        private void BuildOnHandUI()
        {
            // Có khối "Lọc theo ngày" (ô tra cứu cao ~136px) khi có nguồn dữ liệu cho kiểu lọc theo phiếu.
            bool showDate = _lookupRepo != null;
            // = padding 8 + ô tra cứu (140 / 80) + hàng nút 34 + hàng tóm tắt 44 (tối đa 2 dòng).
            var top = new PanelControl { Dock = DockStyle.Top, Height = showDate ? 226 : 166 };
            var topLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(4)
            };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, showDate ? 140 : 80));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _stockLookup = new StockLookupControl { Dock = DockStyle.Fill };
            _stockLookup.SetItemSuggestions(_stockTpService.GetItemLookup());
            _stockLookup.SetLotQrPlaceholder("Nhập LOT / QR / vị trí...");
            _stockLookup.ShowDateRange = showDate;
            _stockLookup.SearchRequested += async (s, e) => await ApplyOnHandSearchAsync();
            _stockLookup.SelectionCleared += async (s, e) => await ClearOnHandSearchAsync();
            topLayout.Controls.Add(_stockLookup, 0, 0);

            var tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2, 4, 2, 0)
            };
            _cmbFilter = new ComboBoxEdit { Width = 200, Height = 27 };
            _cmbFilter.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _cmbFilter.Properties.Items.AddRange(new object[] { AllFilter, UnslottedFilter, OpenFilter, ClosedFilter, CheckFilter });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.SelectedIndexChanged += (s, e) => ApplyOnHandFilter();

            _btnRefresh = new SimpleButton { Width = 90, Height = 27, Text = "Làm mới" };
            _btnRefresh.Click += async (s, e) => await LoadOnHandAsync();
            _btnExport = new SimpleButton { Width = 100, Height = 27, Text = "Xuất Excel" };
            _btnExport.Click += (s, e) => ExportActiveOnHandView();
            // Dòng tóm tắt nằm hàng riêng, giãn theo bề rộng form và tự xuống dòng — không cố định chiều rộng
            // nên chuỗi dài (mô tả tiêu chí + số liệu) không còn bị cắt khuất.
            _lblSummary = new LabelControl { AutoSizeMode = LabelAutoSizeMode.None, Dock = DockStyle.Fill };
            _lblSummary.Appearance.Font = new Font("Tahoma", 9, FontStyle.Bold);
            _lblSummary.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Top;
            _lblSummary.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            tools.Controls.AddRange(new Control[] { _cmbFilter, _btnRefresh, _btnExport });
            topLayout.Controls.Add(tools, 0, 1);
            topLayout.Controls.Add(_lblSummary, 0, 2);
            top.Controls.Add(topLayout);

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
            Shown += async (s, e) => await LoadInitialOnHandWithWaitAsync();
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

        /// <summary>
        /// Tra cứu theo chuẩn chung của ô <see cref="StockLookupControl"/> (cùng cách hiểu với FormChonSlotNoiBo,
        /// các form xử lý hàng lỗi và báo cáo): tab mã = đúng mã đã chọn; tab LOT/QR = QR → Mã + khóa LOT, còn lại tìm theo chữ;
        /// khối "Lọc theo ngày" đi qua <see cref="StockLookupFilter"/>.
        /// </summary>
        private async Task ApplyOnHandSearchAsync()
        {
            if (_onHandRows == null) return;   // dữ liệu chưa tải xong

            StockLookupCriteria c = _stockLookup.GetCriteria();

            if (c.IsItemSearch && c.HasText && string.IsNullOrWhiteSpace(c.ItemCode))
            {
                string hit = _onHandRows
                    .Select(x => (x.ItemCode ?? string.Empty).Trim())
                    .FirstOrDefault(x => string.Equals(x, c.Text, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(hit))
                {
                    XtraMessageBox.Show(this, "Hãy chọn mã hàng trong popup đề xuất.", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _stockLookup.FocusInput();
                    return;
                }

                c.ItemCode = hit;
            }

            _onHandCriteria = c.IsEmpty ? null : c;
            await RenderOnHandAsync(true);
        }

        private async Task ClearOnHandSearchAsync()
        {
            _onHandCriteria = null;
            await RenderOnHandAsync(false);
        }

        /// <summary>Lọc các dòng tồn theo tiêu chí chung (không đụng DB; matcher ngày đã dựng sẵn).</summary>
        private static List<CurrentStockRow> FilterOnHandRows(
            IEnumerable<CurrentStockRow> rows, StockLookupCriteria c, Func<string, string, bool> dateOk)
        {
            IEnumerable<CurrentStockRow> q = rows;
            string text = (c.Text ?? string.Empty).Trim();

            if (c.HasText)
            {
                if (c.IsItemSearch)
                {
                    string code = c.EffectiveItemCode;
                    q = q.Where(x => string.Equals(
                        (x.ItemCode ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // QR (4 hoặc 6 trường) → khớp Mã hàng + khóa LOT chuẩn; còn lại tìm theo chữ.
                    QRCodeInfo qr = null;
                    if (text.IndexOf(':') >= 0)
                    {
                        try { qr = QRCodeParser.ParseQRCode(text.ToUpperInvariant()); }
                        catch (FormatException) { /* QR sai định dạng: tìm theo chữ thường. */ }
                    }

                    if (qr != null)
                    {
                        q = q.Where(x =>
                            string.Equals((x.ItemCode ?? string.Empty).Trim(), qr.ItemCode, StringComparison.OrdinalIgnoreCase)
                            && LotCodeHelper.AreLotKeysEquivalent(x.LotNo, qr.LotNo));
                    }
                    else
                    {
                        q = q.Where(x =>
                            ContainsText(x.ItemCode, text) ||
                            ContainsText(x.LotNo, text) ||
                            ContainsText(x.QrCode, text) ||
                            ContainsText(x.LocationCode, text));
                    }
                }
            }

            return q.Where(x => dateOk(x.ItemCode, x.LotNo)).ToList();
        }

        private static bool ContainsText(string value, string keyword)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Dựng lại 3 lưới từ dữ liệu đã tải + tiêu chí đang áp dụng.</summary>
        private async Task RenderOnHandAsync(bool focusResult)
        {
            IReadOnlyList<CurrentStockRow> all = _onHandRows;
            if (all == null || IsDisposed) return;

            StockLookupCriteria c = _onHandCriteria;
            IReadOnlyList<CurrentStockRow> rows = all;
            string error = null;

            if (c != null)
            {
                try
                {
                    // Kiểu lọc cần DB chỉ truy vấn MỘT lần ở đây, chạy nền để form không bị đơ.
                    Func<string, string, bool> dateOk =
                        await Task.Run(() => StockLookupFilter.CreateDateMatcher(c, _lookupRepo));
                    if (IsDisposed) return;
                    rows = FilterOnHandRows(all, c, dateOk);
                }
                catch (Exception ex)
                {
                    error = "Không lọc được theo ngày: " + ex.Message;
                    rows = new List<CurrentStockRow>();
                }
            }

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

            if (error != null)
            {
                _lblSummary.Text = error;
                return;
            }

            decimal onHand = lots.Sum(l => l.QtyOnHand), unslotted = lots.Sum(l => l.QtyUnslotted);
            string summary = string.Format(
                "{0:n0} mã hàng | {1:n0} LOT | Tổng tồn: {2:n0} | Chưa gán slot: {3:n0} ({4:n0} LOT) | Cần đối chiếu: {5:n0} LOT",
                items.Count, lots.Count, onHand, unslotted,
                lots.Count(l => l.QtyUnslotted > 0), lots.Count(l => l.Check != "OK"));

            if (c != null)
                summary = lots.Count == 0 ? "Không có tồn kho khớp: " + c.Describe() : c.Describe() + " → " + summary;

            _lblSummary.Text = summary;

            if (focusResult && c != null)
            {
                if (lots.Count == 1)
                {
                    _tabs.SelectedTabPageIndex = 0;
                    _viewLot.FocusedRowHandle = _viewLot.LocateByValue("LotNo", lots[0].LotNo);
                }
                else if (items.Count == 1)
                {
                    _tabs.SelectedTabPageIndex = 1;
                    _viewItem.FocusedRowHandle = _viewItem.LocateByValue("ItemCode", items[0].ItemCode);
                }
            }
        }

        /// <summary>
        /// Hiển thị WaitForm chuẩn của toàn hệ thống trong lần mở màn hình tồn kho.
        /// Việc tải dữ liệu vẫn chạy nền; WaitForm chỉ được đóng sau khi bind dữ liệu
        /// hoặc khi truy vấn kết thúc/lỗi.
        /// </summary>
        private async Task LoadInitialOnHandWithWaitAsync()
        {
            var waitForm = new WaitFormService(this);
            try
            {
                waitForm.Show("Đang tải dữ liệu tồn kho thành phẩm...");
                await LoadOnHandAsync();
            }
            finally
            {
                waitForm.Close();
            }
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

                // Giữ tiêu chí tra cứu đang áp dụng khi "Làm mới" — dựng lại lưới từ dữ liệu mới.
                _onHandRows = rows;
                await RenderOnHandAsync(false);
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

            // Tiêu chí của ô tra cứu đã áp lên DỮ LIỆU (RenderOnHandAsync); ở đây chỉ còn bộ lọc nhanh của combo.
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
