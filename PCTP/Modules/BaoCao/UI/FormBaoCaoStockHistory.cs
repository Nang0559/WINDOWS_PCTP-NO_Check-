using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using DevExpress.Data;
using DevExpress.XtraGrid.Columns;
using PCTP.Shell.UI;
using PCTP.Common;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using PCTP.Shared.Lookup;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
        private readonly ILotTraceQuery _deliveryTraceQuery;
        private readonly IWaitFormService _waitForm;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private DateEdit _dateFrom;
        private DateEdit _dateTo;
        private StockLookupControl _stockLookup;
        private SimpleButton _btnCurrentStock;
        private SimpleButton _btnExport;
        private SimpleButton _btnPrint;
        private SimpleButton _btnTraceDelivery;
        private GridControl _deliveryGrid;
        private GridView _deliveryView;
        private readonly List<DeliveryTraceRow> _deliveryRows = new List<DeliveryTraceRow>();
        private TraceTimelineControl _timelineView;
        private TextBox _detailText;
        private LotTraceTimelineBuilder _timelineBuilder;
        private readonly SemaphoreSlim _timelineGate = new SemaphoreSlim(1, 1);
        private System.Windows.Forms.Timer _timelineDebounce;
        private int _timelineVersion;
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
            var deliveryService = new DeliveryTraceReadService();
            _deliveryTraceQuery = deliveryService;
            _timelineBuilder = new LotTraceTimelineBuilder(
                deliveryService, deliveryService,
                new PCTP.Modules.BaoCao.Infrastructure.Repositories.ProductionLotTraceRepository(),
                historyQuery);
            _waitForm = new WaitFormService(this);
            InitializeComponent();
            Shown += async (s, e) => await LoadItemCodesAsync();
            FormClosed += (s, e) => _lifetime.Cancel();
        }

        private void InitializeComponent()
        {
            Text = "Báo cáo - Lịch sử kho";
            ClientSize = new System.Drawing.Size(1184, 710);
            StartPosition = FormStartPosition.CenterScreen;

            var top = new PanelControl { Dock = DockStyle.Top, Height = 150 };
            var historyGroup = new GroupControl { Text = "Tra cứu lịch sử nhập xuất", Dock = DockStyle.Fill };
            var currentGroup = new GroupControl { Text = "Kho hiện tại", Dock = DockStyle.Left, Width = 190 };
            var actionPanel = new PanelControl { Dock = DockStyle.Right, Width = 205 };

            _dateFrom = new DateEdit { Left = 12, Top = 28, Width = 145 };
            _dateFrom.Properties.NullText = "Từ ngày";
            _dateTo = new DateEdit { Left = 164, Top = 28, Width = 145 };
            _dateTo.Properties.NullText = "Đến ngày";
            // Mặc định 30 ngày gần nhất (trước đây để trống/1 ngày nên tra theo mã hàng thường ra 0 dòng).
            _dateFrom.EditValue = DateTime.Today.AddDays(-30);
            _dateTo.EditValue = DateTime.Today;

            _stockLookup = new StockLookupControl
            {
                Left = 12,
                Top = 62,
                Width = 560,
                Height = 78,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _stockLookup.SetLotQrPlaceholder("Quét QR / nhập LOT / số phiếu để tra cứu...");
            _stockLookup.SearchRequested += async (s, e) => await SearchAsync();

            _btnCurrentStock = new SimpleButton { Left = 10, Top = 29, Width = 165, Text = "Thống kê kho hiện tại" };
            _btnCurrentStock.Click += async (s, e) => await LoadCurrentStockAsync();
            _btnExport = new SimpleButton { Left = 8, Top = 22, Width = 90, Text = "Xuất Excel" };
            _btnExport.Click += ExportClick;
            _btnPrint = new SimpleButton { Left = 105, Top = 22, Width = 90, Text = "In báo cáo" };
            _btnPrint.Click += PrintClick;
            _btnTraceDelivery = new SimpleButton { Left = 8, Top = 62, Width = 187, Text = "Truy vết giao hàng →" };
            _btnTraceDelivery.Click += async (s, e) => await TraceDeliveryAsync();

            historyGroup.Controls.AddRange(new Control[] { _dateFrom, _dateTo, _stockLookup });
            currentGroup.Controls.Add(_btnCurrentStock);
            actionPanel.Controls.AddRange(new Control[] { _btnExport, _btnPrint, _btnTraceDelivery });
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

            _deliveryGrid = new GridControl { Dock = DockStyle.Fill };
            _deliveryView = new GridView(_deliveryGrid);
            _deliveryGrid.MainView = _deliveryView;
            _deliveryGrid.ViewCollection.Add(_deliveryView);
            _deliveryView.OptionsBehavior.Editable = false;
            _deliveryView.OptionsView.ColumnAutoWidth = false;
            _deliveryView.OptionsView.ShowAutoFilterRow = true;
            _deliveryView.OptionsView.ShowFooter = true;

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260,
                Panel1MinSize = 120,
                Panel2MinSize = 220
            };
            split.Panel1.Controls.Add(_grid);

            var deliveryPanel = new PanelControl { Dock = DockStyle.Fill };
            var deliveryCaption = new LabelControl
            {
                Text = "TRUY VẾT XUÔI: TỪ LOT SẢN XUẤT / LỊCH SỬ KHO → PHIẾU GIAO HÀNG VÀ QR",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(8, 5, 0, 0)
            };
            deliveryPanel.Controls.Add(_deliveryGrid);
            deliveryPanel.Controls.Add(deliveryCaption);

            _timelineView = new TraceTimelineControl { Dock = DockStyle.Fill };
            _timelineView.SelectedNodeChanged += (s2, e2) =>
            {
                TraceTimelineNode node = _timelineView.SelectedNode;
                _detailText.Text = node == null ? string.Empty : node.Title + System.Environment.NewLine +
                    System.Environment.NewLine + node.DetailText();
            };
            _timelineView.Clear("Chọn một dòng LOT trong lưới trên (hoặc bấm \"Truy vết giao hàng →\") để xem timeline.");
            _detailText = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 130,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new System.Drawing.Font("Consolas", 9F),
                BackColor = System.Drawing.Color.White
            };
            var timelinePanel = new PanelControl { Dock = DockStyle.Fill };
            var timelineCaption = new LabelControl
            {
                Text = "TIMELINE LOT: SẢN XUẤT → KHO → GIAO HÀNG (theo từng LOT)",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(8, 5, 0, 0)
            };
            timelinePanel.Controls.Add(_timelineView);
            timelinePanel.Controls.Add(_detailText);
            timelinePanel.Controls.Add(timelineCaption);

            var bottom = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5
            };
            bottom.Panel1.Controls.Add(deliveryPanel);
            bottom.Panel2.Controls.Add(timelinePanel);
            bottom.HandleCreated += (s2, e2) => { try { bottom.SplitterDistance = Math.Max(200, bottom.Width * 45 / 100); } catch (Exception) { } };
            split.Panel2.Controls.Add(bottom);

            _timelineDebounce = new System.Windows.Forms.Timer { Interval = 250 };
            _timelineDebounce.Tick += async (s2, e2) =>
            {
                _timelineDebounce.Stop();
                await BuildTimelineForFocusedRowAsync();
            };
            _view.FocusedRowChanged += (s2, e2) => { _timelineDebounce.Stop(); _timelineDebounce.Start(); };
            Controls.Add(split);
            Controls.Add(top);
        }

        private async Task LoadItemCodesAsync()
        {
            try
            {
                ToggleBusy(false);
                var codes = await _waitForm.RunAsync(
                    () => Task.Run(() => _historyQuery.GetItemCodesAsync(_lifetime.Token), _lifetime.Token),
                    "Đang tải danh sách mã hàng...");
                var lookupRows = new DataTable();
                lookupRows.Columns.Add("PART", typeof(string));
                foreach (string code in codes ?? Array.Empty<string>())
                    if (!string.IsNullOrWhiteSpace(code))
                        lookupRows.Rows.Add(code);
                _stockLookup.SetItemSuggestions(lookupRows, "PART", null);
                ToggleBusy(true);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ToggleBusy(true);
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
                    PartNo = _stockLookup.IsItemSearch && !string.IsNullOrWhiteSpace(_stockLookup.InputText)
                        ? _stockLookup.InputText
                        : null
                };
                string search = _stockLookup.IsItemSearch ? string.Empty : _stockLookup.InputText;
                if (!string.IsNullOrEmpty(search))
                {
                    if (search.IndexOf(':') >= 0)
                    {
                        var qr = QRCodeParser.ParseQRCode(search.ToUpperInvariant());
                        criteria.LotNo = qr.LotNo;
                        criteria.QrCode = search;
                        if (string.IsNullOrWhiteSpace(criteria.PartNo)) criteria.PartNo = qr.ItemCode;
                    }
                    else if (LotCodeHelper.ClassifyLotKey(search) != LotKeyKind.Unknown) criteria.LotNo = search;
                    else criteria.QrCode = search;
                }
                var rows = await _waitForm.RunAsync(
                    () => Task.Run(() => _historyQuery.SearchAsync(criteria, _lifetime.Token), _lifetime.Token),
                    "Đang truy vấn lịch sử nhập xuất kho...");
                Bind(rows, ConfigureHistoryColumns);
                _deliveryRows.Clear();
                _deliveryGrid.DataSource = null;
                _timelineVersion++;
                _timelineView.Clear(rows == null || rows.Count == 0
                    ? "Không có giao dịch kho phù hợp. Kiểm tra khoảng ngày (Từ ngày/Đến ngày) và mã hàng/LOT."
                    : "Chọn một dòng LOT trong lưới trên để xem timeline.");
                _detailText.Text = string.Empty;
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
                var rows = await _waitForm.RunAsync(
                    () => Task.Run(() => _currentStockQuery.GetAsync(_lifetime.Token), _lifetime.Token),
                    "Đang tải dữ liệu sản xuất và tồn kho...");
                string search = _stockLookup.InputText;
                if (!string.IsNullOrEmpty(search))
                {
                    if (_stockLookup.IsItemSearch)
                    {
                        rows = rows.Where(x => ContainsSearch(x.ItemCode, search)).ToList();
                    }
                    else
                    {
                    QRCodeInfo qr = null;
                    if (search.IndexOf(':') >= 0) { try { qr = QRCodeParser.ParseQRCode(search.ToUpperInvariant()); } catch (FormatException) { } }
                    rows = qr != null
                        ? rows.Where(x => string.Equals(x.ItemCode, qr.ItemCode, StringComparison.OrdinalIgnoreCase) && LotCodeHelper.AreLotKeysEquivalent(x.LotNo, qr.LotNo)).ToList()
                        : LotCodeHelper.ClassifyLotKey(search) != LotKeyKind.Unknown
                            ? rows.Where(x => LotCodeHelper.AreLotKeysEquivalent(x.LotNo, search)).ToList()
                            : rows.Where(x => ContainsSearch(x.ItemCode, search) || ContainsSearch(x.LotNo, search) || ContainsSearch(x.QrCode, search) || ContainsSearch(x.DocumentNo, search)).ToList();
                    }
                }
                Bind(rows, ConfigureCurrentStockColumns);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể tải tồn kho hiện tại", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task TraceDeliveryAsync()
        {
            try
            {
                string lot = null;
                string part = null;
                int handle = _view.FocusedRowHandle;

                if (handle >= 0)
                {
                    CurrentStockRow stock = _view.GetRow(handle) as CurrentStockRow;
                    ItemHistoryRow history = _view.GetRow(handle) as ItemHistoryRow;
                    if (stock != null) { lot = stock.LotNo; part = stock.ItemCode; }
                    else if (history != null) { lot = history.LotNo; part = history.ItemCode; }
                }

                if (string.IsNullOrWhiteSpace(lot))
                {
                    string search = _stockLookup.InputText;
                    if (!_stockLookup.IsItemSearch && !string.IsNullOrWhiteSpace(search))
                    {
                        if (search.IndexOf(':') >= 0)
                        {
                            QRCodeInfo qr = QRCodeParser.ParseQRCode(search.ToUpperInvariant());
                            lot = qr.LotNo;
                            if (string.IsNullOrWhiteSpace(part)) part = qr.ItemCode;
                        }
                        else if (LotCodeHelper.ClassifyLotKey(search) != LotKeyKind.Unknown)
                            lot = search;
                    }
                }

                if (string.IsNullOrWhiteSpace(lot))
                {
                    // Chưa chọn dòng: dùng LOT đầu tiên đang có trong lưới (nếu có) thay vì bắt người dùng làm lại.
                    var first = _grid.DataSource as System.Collections.IEnumerable;
                    if (first != null)
                    {
                        foreach (object o in first)
                        {
                            var h = o as ItemHistoryRow; var c = o as CurrentStockRow;
                            string l = h != null ? h.LotNo : c != null ? c.LotNo : null;
                            if (!string.IsNullOrWhiteSpace(l)) { lot = l; part = h != null ? h.ItemCode : c.ItemCode; break; }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(lot))
                {
                    XtraMessageBox.Show(this,
                        "Hãy tra cứu lịch sử kho trước, rồi chọn một dòng có LOT (hoặc nhập LOT/QR ở ô tra cứu).",
                        "Truy vết giao hàng", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                IReadOnlyList<DeliveryTraceRow> rows = await _waitForm.RunAsync(
                    () => Task.Run(() => _deliveryTraceQuery.SearchAsync(
                        lot, part, null, ToDate(_dateFrom.EditValue), ToDate(_dateTo.EditValue), _lifetime.Token), _lifetime.Token),
                    "Đang truy vết LOT từ sản xuất/kho sang phiếu giao hàng và dữ liệu đọc QR...");

                _deliveryRows.Clear();
                if (rows != null) _deliveryRows.AddRange(rows);
                BindDeliveryRows();
                _ = BuildTimelineForLotAsync(lot, part);
                if (_deliveryRows.Count == 0)
                {
                    XtraMessageBox.Show(this,
                        "Không tìm thấy phiếu giao hàng/QR tương ứng với LOT " + lot +
                        ". Kiểm tra khoảng ngày giao, mã hàng và dữ liệu LOT trên LUUPHIEUGIAOHANG/LOTFCC.",
                        "Truy vết giao hàng", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, ex.Message, "Không thể truy vết giao hàng",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task BuildTimelineForFocusedRowAsync()
        {
            int handle = _view.FocusedRowHandle;
            if (handle < 0) return;
            string lot = null, part = null;
            var history = _view.GetRow(handle) as ItemHistoryRow;
            var stock = _view.GetRow(handle) as CurrentStockRow;
            if (history != null) { lot = history.LotNo; part = history.ItemCode; }
            else if (stock != null) { lot = stock.LotNo; part = stock.ItemCode; }
            if (string.IsNullOrWhiteSpace(lot)) return;
            await BuildTimelineForLotAsync(lot, part);
        }

        /// <summary>
        /// Timeline xuôi cho 1 LOT: sản xuất → lịch sử kho → các phiếu giao chứa LOT (đã tách LOT ghép).
        /// Không giới hạn ngày giao để không bỏ sót phiếu giao sau khoảng ngày đang lọc.
        /// </summary>
        private async Task BuildTimelineForLotAsync(string lot, string part)
        {
            int version = ++_timelineVersion;
            await _timelineGate.WaitAsync();
            try
            {
                if (version != _timelineVersion || IsDisposed) return;
                TraceTimelineResult result = await Task.Run(
                    () => _timelineBuilder.BuildForLotAsync(lot, part, null, null, _lifetime.Token), _lifetime.Token);
                if (version != _timelineVersion || IsDisposed) return;
                _timelineView.SetNodes(result.Nodes, result.Summary ?? "Không có sự kiện cho LOT này.");
                if (result.Nodes.Count == 0) _detailText.Text = result.Summary ?? string.Empty;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (version == _timelineVersion && !IsDisposed)
                    _timelineView.Clear("Không dựng được timeline: " + ex.Message);
            }
            finally { _timelineGate.Release(); }
        }

        private void BindDeliveryRows()
        {
            _deliveryView.BeginUpdate();
            try
            {
                _deliveryView.Columns.Clear();
                _deliveryGrid.DataSource = _deliveryRows.ToList();
                _deliveryView.PopulateColumns();
                ConfigureDeliveryColumns();
            }
            finally { _deliveryView.EndUpdate(); }
        }

        private void ConfigureDeliveryColumns()
        {
            int index = 0;
            SetDeliveryColumn("DeliveryDate", "Ngày giao", 115, index++, "dd/MM/yyyy");
            SetDeliveryColumn("DeliveryTime", "Giờ giao", 85, index++);
            SetDeliveryColumn("Factory", "Nhà máy", 180, index++);
            SetDeliveryColumn("DocumentNo", "Chứng từ / PO", 130, index++);
            SetDeliveryColumn("CustomerCode", "Mã KH", 75, index++);
            SetDeliveryColumn("CustomerName", "Khách hàng", 160, index++);
            SetDeliveryColumn("PartNo", "Mã hàng", 145, index++);
            SetDeliveryColumn("PartName", "Tên hàng", 180, index++);
            SetDeliveryColumn("LotNoRaw", "LOT giao (có thể ghép)", 260, index++);
            SetDeliveryColumn("Quantity", "SL giao", 85, index++, "n0");
            SetDeliveryColumn("Unit", "ĐVT", 55, index++);
            SetDeliveryColumn("QRCode", "QR FCC", 230, index++);
            SetDeliveryColumn("QrLotNo", "LOT QR FCC", 190, index++);
            SetDeliveryColumn("CustomerLabelData", "LOT tem KH", 180, index++);
            SetDeliveryColumn("QrScanDate", "Ngày đọc QR", 115, index++, "dd/MM/yyyy");
            SetDeliveryColumn("QrScanTime", "Giờ đọc QR", 90, index++);
            SetDeliveryColumn("QrStatus", "Trạng thái QR", 110, index++);
            SetDeliveryColumn("Status", "Trạng thái giao", 110, index++);
        }

        private void SetDeliveryColumn(string field, string caption, int width, int visibleIndex, string format = null)
        {
            GridColumn column = _deliveryView.Columns[field];
            if (column == null) return;
            column.Caption = caption;
            column.Width = width;
            column.Visible = true;
            column.VisibleIndex = visibleIndex;
            column.OptionsColumn.AllowEdit = false;
            if (!string.IsNullOrWhiteSpace(format))
            {
                column.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Custom;
                column.DisplayFormat.FormatString = format;
            }
        }

        private void ToggleBusy(bool enabled)
        {
            if (_stockLookup != null) _stockLookup.Enabled = enabled;
            if (_dateFrom != null) _dateFrom.Enabled = enabled;
            if (_dateTo != null) _dateTo.Enabled = enabled;
            if (_btnCurrentStock != null) _btnCurrentStock.Enabled = enabled;
            if (_btnTraceDelivery != null) _btnTraceDelivery.Enabled = enabled;
            if (_btnExport != null) _btnExport.Enabled = enabled;
            if (_btnPrint != null) _btnPrint.Enabled = enabled;
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
            Col("LastReceivedDate", "Ngày nhập mới nhất", 125, "dd/MM/yyyy HH:mm");
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
            Col("ReceivedDate", "Ngày nhập LOT", 120, "dd/MM/yyyy HH:mm");
            Col("SlotImportDate", "Ngày nhập slot", 125, "dd/MM/yyyy HH:mm");
            Col("IssuedDate", "Ngày xuất gần nhất", 115, "dd/MM/yyyy");
            Col("DocumentNo", "Số phiếu", 120);
            Col("Warning", "Cảnh báo", 300);
            HideOthers("LastReceivedDate", "ItemCode", "ItemName", "Model", "LotNo", "QrCode", "LocationCode", "Quantity", "QtyProduced",
                "QtyReceived", "QtyIssued", "QtyOnHandLot", "Status", "ProductionDate", "ShiftNo", "ReceivedDate",
                "SlotImportDate", "IssuedDate", "DocumentNo", "Warning");
            var item = _view.Columns["ItemCode"];
            if (item != null) { item.SummaryItem.SummaryType = SummaryItemType.Count; item.SummaryItem.DisplayFormat = "{0:n0} dòng"; }

            // Ngày nhập mới nhất lên trên (dòng chưa có ngày nhập xuống cuối), cùng ngày thì gom theo LOT.
            _view.SortInfo.Clear();
            var newest = _view.Columns["LastReceivedDate"];
            var lot = _view.Columns["LotNo"];
            if (newest != null) _view.SortInfo.Add(new GridColumnSortInfo(newest, DevExpress.Data.ColumnSortOrder.Descending));
            if (lot != null) _view.SortInfo.Add(new GridColumnSortInfo(lot, DevExpress.Data.ColumnSortOrder.Ascending));
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

        private async void ExportClick(object sender, EventArgs e)
        {
            if (_grid.DataSource == null) return;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.FileName = (_showingCurrentStock ? "KhoHienTai_" : "LichSuNhapXuat_") +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string basePath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(dialog.FileName),
                        System.IO.Path.GetFileNameWithoutExtension(dialog.FileName));
                    string sourcePath = basePath + "_SanXuatTonKho.xlsx";
                    string deliveryPath = basePath + "_GiaoHangQR.xlsx";

                    await _waitForm.RunAsync(() => Task.Run(() =>
                    {
                        _grid.ExportToXlsx(sourcePath);
                        if (_deliveryGrid.DataSource != null)
                            _deliveryGrid.ExportToXlsx(deliveryPath);
                    }), "Đang xuất dữ liệu sản xuất/kho và giao hàng/QR...");

                    string message = "Đã xuất dữ liệu bằng DevExpress Export:\r\n" + sourcePath;
                    if (_deliveryGrid.DataSource != null) message += "\r\n" + deliveryPath;
                    XtraMessageBox.Show(this, message, "Xuất Excel",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, "Không thể xuất Excel: " + ex.Message, "Xuất Excel",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

        private static bool ContainsSearch(string value, string search)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DateTime? ToDate(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            return Convert.ToDateTime(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lifetime.Cancel();
                if (_timelineDebounce != null) { _timelineDebounce.Stop(); _timelineDebounce.Dispose(); }
                _lifetime.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
