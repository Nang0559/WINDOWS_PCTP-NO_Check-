using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Columns;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Repositories;
using PCTP.Shell.UI;
using PCTP.Shared.Helpers;
using PCTP.Shared.Lookup;
using System.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PCTP.Common;

namespace PCTP.Modules.BaoCao.UI
{
    [DesignerCategory("Code")]
    public sealed class FormBaoCaoTraceability : WmsTitleBarForm
    {
        private readonly IQrTraceQuery _qrQuery;
        private readonly ILotTraceQuery _lotQuery;
        private readonly ICustomerDeliveryQuery _customerQuery;
        private readonly StockLookupControl _stockLookup = new StockLookupControl();
        private readonly TextBox _txtCustomerLabel = new TextBox();
        private readonly LookUpEdit _txtCustomer = new LookUpEdit();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly GridControl _deliveryGrid = new GridControl();
        private readonly GridView _deliveryView = new GridView();
        private readonly GridControl _qrGrid = new GridControl();
        private readonly GridView _qrView = new GridView();
        private List<DeliveryTraceRow> _deliveryRows = new List<DeliveryTraceRow>();
        private List<DeliveryTraceRow> _qrRows = new List<DeliveryTraceRow>();
        private bool _bindingQrGrid;
        private readonly IStockHistoryQuery _stockHistoryQuery;
        private readonly IWaitFormService _waitForm;
        private readonly IProductionLotTraceQuery _productionQuery;
        private readonly TraceTimelineControl _timelineView = new TraceTimelineControl();
        private readonly LotTraceTimelineBuilder _timelineBuilder;
        private readonly SemaphoreSlim _timelineGate = new SemaphoreSlim(1, 1);
        private readonly System.Windows.Forms.Timer _timelineDebounce = new System.Windows.Forms.Timer { Interval = 250 };
        private int _timelineVersion;
        private readonly Panel _detailPanel = new Panel();
        private readonly Label _detailTitle = new Label();
        private readonly TextBox _detailText = new TextBox();
        private readonly Label _timelineCaption = new Label();
        private readonly List<TraceTimelineNode> _timelineNodes = new List<TraceTimelineNode>();
        private readonly Label _status = new Label();
        private SimpleButton _exportButton;
        private List<DeliveryTraceRow> _currentRows = new List<DeliveryTraceRow>();
        private string _pendingQuickSearch;

        public FormBaoCaoTraceability() : this(new DeliveryTraceReadService(), new StockHistoryQueryService()) { }

        public FormBaoCaoTraceability(string quickSearch) : this(new DeliveryTraceReadService(), new StockHistoryQueryService())
        {
            ApplyQuickSearch(quickSearch);
        }

        public FormBaoCaoTraceability(DeliveryTraceReadService query)
            : this(query, new StockHistoryQueryService())
        {
        }

        public FormBaoCaoTraceability(DeliveryTraceReadService query, IStockHistoryQuery stockHistoryQuery)
            : this(query, stockHistoryQuery, new ProductionLotTraceRepository())
        {
        }

        public FormBaoCaoTraceability(
            DeliveryTraceReadService query,
            IStockHistoryQuery stockHistoryQuery,
            IProductionLotTraceQuery productionQuery)
        {
            if (query == null) throw new ArgumentNullException("query");
            if (stockHistoryQuery == null) throw new ArgumentNullException("stockHistoryQuery");
            if (productionQuery == null) throw new ArgumentNullException("productionQuery");
            _productionQuery = productionQuery;
            _qrQuery = query;
            _lotQuery = query;
            _customerQuery = query;
            _stockHistoryQuery = stockHistoryQuery;
            _waitForm = new WaitFormService(this);
            _timelineBuilder = new LotTraceTimelineBuilder(query, query, productionQuery, stockHistoryQuery);
            _timelineDebounce.Tick += TimelineDebounce_Tick;
            _timelineView.SelectedNodeChanged += TimelineView_SelectedNodeChanged;
            Text = "Báo cáo - Tra cứu truy xuất giao hàng";
            ClientSize = new Size(1250, 720);
            StartPosition = FormStartPosition.CenterParent;
            BuildUi();
            Shown += async (s, e) => await LoadLookupsAsync();
        }

        private void ApplyQuickSearch(string value)
        {
            _pendingQuickSearch = (value ?? string.Empty).Trim();
            ApplyPendingQuickSearch();
        }

        private void ApplyPendingQuickSearch()
        {
            string keyword = _pendingQuickSearch;
            if (string.IsNullOrWhiteSpace(keyword)) return;
            if (keyword.StartsWith("LOT:", StringComparison.OrdinalIgnoreCase))
            {
                _stockLookup.SelectLotQrTab();
                _stockLookup.SetInputText(keyword.Substring(4).Trim());
            }
            else if (keyword.StartsWith("QR:", StringComparison.OrdinalIgnoreCase))
            {
                _stockLookup.SelectLotQrTab();
                _stockLookup.SetInputText(keyword.Substring(3).Trim());
            }
            else if (keyword.StartsWith("PART:", StringComparison.OrdinalIgnoreCase))
            {
                _stockLookup.SelectItemTab();
                _stockLookup.SetInputText(keyword.Substring(5).Trim());
            }
            else
            {
                _stockLookup.SelectLotQrTab();
                _stockLookup.SetInputText(keyword);
            }
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                ToggleSearch(false);
                _status.Text = "Đang tải danh sách mã hàng/khách hàng...";

                IReadOnlyList<string>[] lookupRows = await _waitForm.RunAsync(
                    async () =>
                    {
                        Task<IReadOnlyList<string>> itemTask = _customerQuery.GetItemCodesAsync(CancellationToken.None);
                        Task<IReadOnlyList<string>> customerTask = _customerQuery.GetCustomersAsync(CancellationToken.None);
                        await Task.WhenAll(itemTask, customerTask);
                        return new[] { itemTask.Result, customerTask.Result };
                    },
                    "Đang tải danh sách mã hàng và khách hàng...");

                var itemRows = new DataTable();
                itemRows.Columns.Add("PART", typeof(string));
                foreach (string itemCode in lookupRows[0] ?? new string[0])
                    if (!string.IsNullOrWhiteSpace(itemCode))
                        itemRows.Rows.Add(itemCode);
                _stockLookup.SetItemSuggestions(itemRows, "PART", null);
                ApplyPendingQuickSearch();
                ConfigureLookup(_txtCustomer, lookupRows[1], "Khách hàng");
                _status.Text = "Sẵn sàng";
            }
            catch (Exception ex)
            {
                _status.Text = "Không tải được danh sách mã hàng/khách hàng.";
                MessageBox.Show(this, ex.Message, "Báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSearch(true);
            }
        }

        private static void ConfigureLookup(LookUpEdit lookup, IReadOnlyList<string> values, string nullText)
        {
            lookup.Properties.NullText = nullText;
            lookup.Properties.DataSource = values == null ? new List<string>() : values.ToList();
            lookup.Properties.DisplayMember = string.Empty;
            lookup.Properties.ValueMember = string.Empty;
            lookup.Properties.ShowHeader = false;
            lookup.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            lookup.Properties.AutoHeight = false;
        }

        private void BuildUi()
        {
            // Use one explicit root layout. WmsTitleBarForm reparents controls into its
            // content host on Load, so sibling Dock=Top/Dock=Fill controls can be
            // reordered and the fill panel can cover the search captions.
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = SystemColors.Control,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var search = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 200,
                MinimumSize = new Size(0, 200),
                ColumnCount = 6,
                RowCount = 4,
                Padding = new Padding(8),
                Margin = Padding.Empty,
                BackColor = SystemColors.Control,
                ForeColor = Color.FromArgb(35, 35, 35),
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };

            for (int i = 0; i < 6; i++)
                search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6667f));

            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            search.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var stockLabel = new Label
            {
                Text = "Mã hàng / LOT / QR",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Margin = new Padding(3, 0, 3, 0)
            };
            search.Controls.Add(stockLabel, 0, 0);
            search.SetColumnSpan(stockLabel, 3);
            _stockLookup.Dock = DockStyle.Fill;
            _stockLookup.Margin = new Padding(3, 0, 3, 0);
            search.Controls.Add(_stockLookup, 0, 1);
            search.SetColumnSpan(_stockLookup, 3);
            AddField(search, "Customer label", _txtCustomerLabel, 3);
            AddFieldSpan(search, "Khách hàng", _txtCustomer, 4, 2);

            _from.Format = DateTimePickerFormat.Short;
            _to.Format = DateTimePickerFormat.Short;
            _from.Value = DateTime.Today.AddDays(-30);
            _to.Value = DateTime.Today;

            search.Controls.Add(new Label
            {
                Text = "From",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Margin = new Padding(3, 0, 3, 0)
            }, 0, 2);
            _from.Dock = DockStyle.None;
            _from.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _from.Margin = new Padding(3, 0, 3, 0);            search.Controls.Add(_from, 1, 2);

            search.Controls.Add(new Label
            {
                Text = "To",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Margin = new Padding(3, 0, 3, 0)
            }, 2, 2);
            _to.Dock = DockStyle.None;
            _to.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _to.Margin = new Padding(3, 0, 3, 0);
            search.Controls.Add(_to, 3, 2);

            // Search is triggered only by the shared control's own button / Enter key.
            // Keep one clear action beside the date/customer filters.
            _stockLookup.SearchRequested += SearchButton_Click;

            var clearButton = new SimpleButton { Text = "Xóa điều kiện", Dock = DockStyle.Fill, Margin = new Padding(3, 2, 3, 2) };
            clearButton.Click += ClearButton_Click;
            search.Controls.Add(clearButton, 4, 2);
            search.SetColumnSpan(clearButton, 2);

            var statusBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            _status.Text = "Đang chuẩn bị...";
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.FromArgb(35, 35, 35);
            _status.BackColor = Color.Transparent;
            statusBar.Controls.Add(_status, 0, 0);
            var exportButton = new SimpleButton
            {
                Text = "Xuất Excel",
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 1, 3, 1),
                Enabled = false
            };
            exportButton.Click += ExportExcel_Click;
            statusBar.Controls.Add(exportButton, 1, 0);
            _exportButton = exportButton;
            search.Controls.Add(statusBar, 0, 3);
            search.SetColumnSpan(statusBar, 6);

            var workspace = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Margin = Padding.Empty,
                // Set minimum sizes and SplitterDistance only after the control
                // has its real layout width. Its default width is too small.
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            workspace.SizeChanged += (s, e) => AdjustWorkspaceSplitter(workspace);
            workspace.HandleCreated += (s, e) => AdjustWorkspaceSplitter(workspace);

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(6),
                BackColor = SystemColors.Control
            };
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 62));

            var deliveryQrHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            deliveryQrHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            deliveryQrHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            deliveryQrHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            deliveryQrHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var deliveryCaption = CreateGridCaption("THÔNG TIN ĐƠN HÀNG (LUUPHIEUGIAOHANG)");
            var qrCaption = CreateGridCaption("THÔNG TIN ĐỌC QR");
            deliveryQrHost.Controls.Add(deliveryCaption, 0, 0);
            deliveryQrHost.Controls.Add(qrCaption, 1, 0);

            ConfigureDevExpressGrid(_deliveryGrid, _deliveryView);
            ConfigureDevExpressGrid(_qrGrid, _qrView);
            ConfigureDeliveryColumns();
            ConfigureQrColumns();
            _deliveryView.FocusedRowChanged += DeliveryView_FocusedRowChanged;
            _deliveryView.DoubleClick += DeliveryGrid_DoubleClick;
            _qrView.DoubleClick += DeliveryGrid_DoubleClick;

            deliveryQrHost.Controls.Add(_deliveryGrid, 0, 1);
            deliveryQrHost.Controls.Add(_qrGrid, 1, 1);
            left.Controls.Add(deliveryQrHost, 0, 0);

            var timelineHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(6) };
            _timelineCaption.Text = "TIMELINE TRUY VẾT (chọn dòng giao hàng để dựng theo toàn bộ LOT ghép)";
            _timelineCaption.Dock = DockStyle.Top;
            _timelineCaption.Height = 28;
            _timelineCaption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _timelineCaption.ForeColor = Color.FromArgb(35, 65, 95);
            _timelineView.Dock = DockStyle.Fill;
            timelineHost.Controls.Add(_timelineView);
            timelineHost.Controls.Add(_timelineCaption);
            left.Controls.Add(timelineHost, 0, 1);

            _detailPanel.Dock = DockStyle.Fill;
            _detailPanel.BackColor = Color.White;
            _detailPanel.Padding = new Padding(12);
            _detailTitle.Dock = DockStyle.Top;
            _detailTitle.Height = 42;
            _detailTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _detailTitle.Text = "Chi tiết sự kiện";
            _detailText.Dock = DockStyle.Fill;
            _detailText.Multiline = true;
            _detailText.ReadOnly = true;
            _detailText.ScrollBars = ScrollBars.Vertical;
            _detailText.BorderStyle = BorderStyle.None;
            _detailText.Font = new Font("Consolas", 9F);
            _detailText.BackColor = Color.White;
            _detailPanel.Controls.Add(_detailText);
            _detailPanel.Controls.Add(_detailTitle);

            workspace.Panel1.Controls.Add(left);
            workspace.Panel2.Controls.Add(_detailPanel);
            ClearTimeline("Chọn một dòng giao hàng để dựng timeline theo LOT.");

            root.Controls.Add(search, 0, 0);
            root.Controls.Add(workspace, 0, 1);
            Controls.Add(root);
        }

        private static void AddField(TableLayoutPanel panel, string caption, Control editor, int column)
        {
            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                AutoEllipsis = true,
                Margin = new Padding(3, 0, 3, 0)
            };

            // Anchor Left|Right (no Top/Bottom) => TableLayoutPanel centers the editor
            // vertically in its cell, so it lines up with the caption/label of the same row.
            editor.Dock = DockStyle.None;
            editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            editor.Margin = new Padding(3, 0, 3, 0);
            panel.Controls.Add(label, column, 0);
            panel.Controls.Add(editor, column, 1);
        }

        private static void AddFieldSpan(TableLayoutPanel panel, string caption, Control editor, int column, int span)
        {
            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                AutoEllipsis = true,
                Margin = new Padding(3, 0, 3, 0)
            };
            editor.Dock = DockStyle.None;
            editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            editor.Margin = new Padding(3, 0, 3, 0);
            panel.Controls.Add(label, column, 0);
            panel.SetColumnSpan(label, span);
            panel.Controls.Add(editor, column, 1);
            panel.SetColumnSpan(editor, span);
        }

        private static Label CreateGridCaption(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 65, 95),
                BackColor = Color.White,
                Padding = new Padding(6, 0, 0, 0),
                Margin = Padding.Empty
            };
        }

        private static void ConfigureDevExpressGrid(GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            grid.UseEmbeddedNavigator = false;
            view.OptionsBehavior.Editable = false;
            view.OptionsBehavior.ReadOnly = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowIndicator = true;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.EnableAppearanceEvenRow = true;
            view.OptionsView.EnableAppearanceOddRow = true;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.FocusRectStyle = DrawFocusRectStyle.RowFocus;
            view.OptionsSelection.MultiSelect = false;
            view.OptionsCustomization.AllowFilter = true;
            view.OptionsView.ShowAutoFilterRow = true;
            view.Appearance.HeaderPanel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            view.Appearance.Row.Font = new Font("Segoe UI", 9F);
            view.Appearance.HeaderPanel.Options.UseFont = true;
            view.Appearance.Row.Options.UseFont = true;
        }

        private void ConfigureDeliveryColumns()
        {
            AddGridColumn(_deliveryView, "DeliveryStt", "STT", 55);
            AddGridColumn(_deliveryView, "Door", "Cửa", 65);
            AddGridColumn(_deliveryView, "Conveyor", "Truyền", 70);
            AddGridColumn(_deliveryView, "LotNoRaw", "LOT giao (có thể ghép)", 260);
            AddGridColumn(_deliveryView, "Quantity", "Số lượng", 85);
            AddGridColumn(_deliveryView, "Unit", "ĐVT", 55);
            AddGridColumn(_deliveryView, "DeliveryDate", "Ngày giao", 100);
            AddGridColumn(_deliveryView, "DeliveryTime", "Giờ giao", 85);
            AddGridColumn(_deliveryView, "DocumentNo", "Chứng từ", 110);
            AddGridColumn(_deliveryView, "CustomerName", "Khách hàng", 150);
            AddGridColumn(_deliveryView, "DeliveryKey", "DeliveryKey", 220).Visible = false;
        }

        private void ConfigureQrColumns()        {
            AddGridColumn(_qrView, "QrScanOrder", "Dòng", 55);
            AddGridColumn(_qrView, "QRCode", "QR FCC", 210);
            AddGridColumn(_qrView, "CustomerLabelData", "LOT tem KH", 160);
            AddGridColumn(_qrView, "QrLotNo", "LOT QR", 160);
            AddGridColumn(_qrView, "QrScanDate", "Ngày đọc", 100);
            AddGridColumn(_qrView, "QrScanTime", "Giờ đọc", 85);
            AddGridColumn(_qrView, "QrStatus", "Kết quả", 85);
        }

        private static GridColumn AddGridColumn(GridView view, string field, string caption, int width)
        {
            GridColumn column = view.Columns.AddVisible(field, caption);
            column.Width = width;
            column.OptionsColumn.AllowEdit = false;
            column.OptionsColumn.ReadOnly = true;
            return column;
        }

        private void BindDeliveryRows()
        {
            _deliveryRows = _currentRows
                .Where(x => x != null)
                .GroupBy(x => x.DeliveryKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            _deliveryGrid.DataSource = new BindingList<DeliveryTraceRow>(_deliveryRows);
            if (_deliveryRows.Count > 0)
            {
                _deliveryView.FocusedRowHandle = 0;
                BindQrRows(_deliveryRows[0].DeliveryKey);
            }
            else
            {
                BindQrRows(null);
            }
        }

        private void BindQrRows(string deliveryKey)
        {
            _bindingQrGrid = true;
            try
            {
                _qrRows = string.IsNullOrWhiteSpace(deliveryKey)
                    ? new List<DeliveryTraceRow>()
                    : _currentRows
                        .Where(x => x != null && string.Equals(x.DeliveryKey, deliveryKey, StringComparison.OrdinalIgnoreCase))
                        .Where(x => !string.IsNullOrWhiteSpace(x.QRCode))
                        .OrderBy(x => x.QrScanOrder ?? int.MaxValue)
                        .ThenBy(x => x.QrScanDate ?? DateTime.MinValue)
                        .ThenBy(x => x.QrScanTime ?? string.Empty)
                        .ToList();

                _qrGrid.DataSource = new BindingList<DeliveryTraceRow>(_qrRows);
                if (_qrRows.Count > 0) _qrView.FocusedRowHandle = 0;
            }
            finally { _bindingQrGrid = false; }
        }

        private DeliveryTraceRow GetFocusedDelivery()
        {
            return _deliveryView.GetFocusedRow() as DeliveryTraceRow;
        }

        private DeliveryTraceRow GetFocusedQr()
        {
            return _qrView.GetFocusedRow() as DeliveryTraceRow;
        }

        private void DeliveryView_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            if (_bindingQrGrid) return;
            DeliveryTraceRow row = GetFocusedDelivery();
            BindQrRows(row == null ? null : row.DeliveryKey);
            _timelineDebounce.Stop();
            if (row != null) _timelineDebounce.Start();
        }

        private void TimelineDebounce_Tick(object sender, EventArgs e)
        {
            _timelineDebounce.Stop();
            DeliveryTraceRow row = GetFocusedDelivery();
            if (row != null) _ = BuildTimelineAsync(row);
        }

        private void TimelineView_SelectedNodeChanged(object sender, EventArgs e)
        {
            TraceTimelineNode node = _timelineView.SelectedNode;
            if (node != null) ShowNodeDetail(node);
        }

        private void DeliveryGrid_DoubleClick(object sender, EventArgs e)
        {
            DeliveryTraceRow row = sender == _qrView ? GetFocusedQr() : GetFocusedDelivery();
            if (row == null) row = GetFocusedDelivery();
            if (row != null) _ = BuildTimelineAsync(row);
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            try
            {
                ToggleSearch(false);
                _status.Text = "Đang tra cứu...";

                DateTime? from = _from.Value.Date;
                DateTime? to = _to.Value.Date;
                string stockSearch = _stockLookup.InputText;
                string qr = string.Empty;
                string lot = string.Empty;
                string part = string.Empty;
                if (_stockLookup.IsItemSearch)
                    part = stockSearch;
                else if (!string.IsNullOrWhiteSpace(stockSearch))
                {
                    // Chuỗi QR có dấu ':'; còn lại nếu chứa LOT hợp lệ (LOT đơn / LOT in đủ / LOT ghép) thì tra theo LOT.
                    if (stockSearch.IndexOf(':') < 0 && IsLotSearch(stockSearch))
                        lot = stockSearch;
                    else
                        qr = stockSearch;
                }
                string customerLabel = _txtCustomerLabel.Text.Trim();
                string customer = GetLookupText(_txtCustomer);

                IReadOnlyList<DeliveryTraceRow> rows = await _waitForm.RunAsync(
                    async () =>
                    {
                        if (!string.IsNullOrWhiteSpace(lot))
                            return await _lotQuery.SearchAsync(lot, part, customer, from, to, CancellationToken.None);
                        if (!string.IsNullOrWhiteSpace(customer) && string.IsNullOrWhiteSpace(qr) && string.IsNullOrWhiteSpace(customerLabel))
                            return await _customerQuery.SearchAsync(customer, part, from, to, CancellationToken.None);
                        return await _qrQuery.SearchAsync(qr, customerLabel, part, customer, from, to, CancellationToken.None);
                    },
                    "Đang tra cứu giao hàng, LOT và dữ liệu đọc QR...");

                _currentRows = rows == null ? new List<DeliveryTraceRow>() : rows.ToList();
                BindDeliveryRows();
                ClearTimeline("Chọn một dòng giao hàng để xem QR và dựng timeline theo toàn bộ LOT ghép.");
                _status.Text = "Tìm thấy " + _deliveryRows.Count + " dòng giao hàng / " + _currentRows.Count + " dòng QR." +
                    (_deliveryRows.Count == 0 ? " Kiểm tra khoảng ngày (theo NGAYGIAO) và ô Khách hàng." : string.Empty);
                if (_exportButton != null) _exportButton.Enabled = _currentRows.Count > 0;
            }
            catch (Exception ex)
            {
                _status.Text = "Lỗi tra cứu.";
                XtraMessageBox.Show(this, ex.Message, "Báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSearch(true);
            }
        }

        private static bool IsLotSearch(string value)
        {
            return LotCodeHelper.ParseCompositeLotTolerant(value, null).Any(x => x.IsValid);
        }

        private static string GetLookupText(LookUpEdit lookup)
        {
            // Chưa chọn gì (EditValue null) = không lọc. Không đọc lookup.Text vì khi rỗng nó có thể
            // trả về chính NullText ("Khách hàng"); chuỗi đó bị coi là tên khách lạ => điều kiện 1 = 0
            // => luôn "Tìm thấy 0 dòng".
            object value = lookup.EditValue;
            if (value == null || value == DBNull.Value)
                return string.Empty;
            string text = Convert.ToString(value).Trim();
            string nullText = (lookup.Properties.NullText ?? string.Empty).Trim();
            if (text.Length == 0 || string.Equals(text, nullText, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return text;
        }

        private async Task BuildTimelineAsync(DeliveryTraceRow row)
        {
            if (row == null) return;
            int version = ++_timelineVersion;
            _status.Text = "Đang dựng timeline theo toàn bộ LOT ghép...";
            await _timelineGate.WaitAsync();
            try
            {
                if (version != _timelineVersion || IsDisposed) return; // đã chọn dòng khác
                TraceTimelineResult result = await Task.Run(
                    () => _timelineBuilder.BuildForDeliveryAsync(row, CancellationToken.None));
                if (version != _timelineVersion || IsDisposed) return;

                _timelineNodes.Clear();
                _timelineNodes.AddRange(result.Nodes);
                _timelineView.SetNodes(_timelineNodes, "Không có sự kiện phù hợp với LOT đã chọn.");
                _status.Text = result.Summary;
            }
            catch (Exception ex)
            {
                if (version == _timelineVersion && !IsDisposed)
                {
                    ClearTimeline("Không dựng được timeline do lỗi truy vấn.");
                    _status.Text = "Lỗi dựng timeline: " + ex.Message;
                }
            }
            finally { _timelineGate.Release(); }
        }

        private void ShowNodeDetail(TraceTimelineNode node)
        {
            _detailTitle.Text = node.Title;
            _detailText.Text = node.DetailText();
        }

        private static void AdjustWorkspaceSplitter(SplitContainer workspace)
        {
            if (workspace == null || workspace.IsDisposed || !workspace.IsHandleCreated)
                return;

            int available = workspace.ClientSize.Width - workspace.SplitterWidth;
            if (available <= 0) return;

            // Reset constraints first: a resize can make the previous distance
            // incompatible with the new width/minimums.
            if (workspace.Panel1MinSize != 0) workspace.Panel1MinSize = 0;
            if (workspace.Panel2MinSize != 0) workspace.Panel2MinSize = 0;

            int desiredDistance = 590;
            int distance = Math.Max(0, Math.Min(desiredDistance, available));
            if (workspace.SplitterDistance != distance)
                workspace.SplitterDistance = distance;

            // Comfortable minimum widths are enabled only when both panes fit.
            if (available >= 714)
            {
                workspace.Panel1MinSize = 350;
                workspace.Panel2MinSize = 360;
                int maxDistance = available - 360;
                distance = Math.Max(350, Math.Min(desiredDistance, maxDistance));
                if (workspace.SplitterDistance != distance)
                    workspace.SplitterDistance = distance;
            }
        }

        private void ClearTimeline(string message)
        {
            _timelineVersion++;
            _timelineNodes.Clear();
            _timelineView.Clear(message);
            _detailTitle.Text = "Chi tiết sự kiện";
            _detailText.Text = message;
        }

        private async void ExportExcel_Click(object sender, EventArgs e)
        {
            if ((_deliveryRows == null || _deliveryRows.Count == 0) &&
                (_currentRows == null || _currentRows.Count == 0) &&
                (_timelineNodes == null || _timelineNodes.Count == 0))
            {
                XtraMessageBox.Show(this, "Chưa có dữ liệu tra cứu để xuất.", "Xuất Excel",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.FileName = "TruyXuatGiaoHang_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string basePath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(dialog.FileName),
                        System.IO.Path.GetFileNameWithoutExtension(dialog.FileName));
                    string deliveryPath = basePath + "_GiaoHang.xlsx";
                    string qrPath = basePath + "_DocQR.xlsx";
                    string timelinePath = basePath + "_Timeline.csv";

                    await _waitForm.RunAsync(() => Task.Run(() =>
                    {
                        if (_deliveryGrid.DataSource != null)
                            _deliveryGrid.ExportToXlsx(deliveryPath);
                        if (_qrGrid.DataSource != null)
                            _qrGrid.ExportToXlsx(qrPath);
                        WriteTimelineCsv(timelinePath, _timelineNodes);
                    }), "Đang xuất dữ liệu giao hàng, QR và Timeline...");

                    XtraMessageBox.Show(this,
                        "Đã xuất dữ liệu bằng bộ Export DevExpress hiện có:\r\n" +
                        deliveryPath + "\r\n" + qrPath + "\r\n" + timelinePath +
                        "\r\nTimeline ở định dạng CSV để mở trực tiếp bằng Excel.",
                        "Xuất Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, "Không thể xuất dữ liệu: " + ex.Message, "Xuất Excel",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void WriteTimelineCsv(string filePath, IList<TraceTimelineNode> rows)
        {
            var lines = new List<string>();
            lines.Add(string.Join(",", new[]
            {
                "STT", "Su kien", "Loai su kien", "Thoi gian", "LOT", "Ma hang", "So luong",
                "Vi tri tu", "Vi tri den", "QR", "Customer label", "Khach hang", "Chung tu",
                "Nguoi thuc hien", "Nguon du lieu", "Khoa nguon", "Trang thai bang chung", "Mo ta", "Ghi chu"
            }.Select(CsvEscape)));

            int sequence = 1;
            foreach (TraceTimelineNode x in rows ?? new List<TraceTimelineNode>())
            {
                lines.Add(string.Join(",", new[]
                {
                    (sequence++).ToString(), x.Title, x.EventType,
                    x.EventTime.HasValue ? x.EventTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    x.LotNo, x.PartNo, x.Quantity == null ? string.Empty : Convert.ToString(x.Quantity),
                    x.FromLocation, x.ToLocation, x.QrCode, x.CustomerLabel, x.CustomerName,
                    x.DocumentNo, x.UserName, x.SourceName, x.SourceKey, x.EvidenceStatus, x.Subtitle, x.Note
                }.Select(CsvEscape)));
            }
            System.IO.File.WriteAllLines(filePath, lines, new System.Text.UTF8Encoding(true));
        }

        private static string CsvEscape(string value)
        {
            string text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            _stockLookup.ClearInput();
            _stockLookup.SelectItemTab();
            _txtCustomerLabel.Clear();
            _txtCustomer.EditValue = null;
            _txtCustomer.Text = string.Empty;
            _from.Value = DateTime.Today.AddDays(-30);
            _to.Value = DateTime.Today;
            _deliveryGrid.DataSource = null;
            _qrGrid.DataSource = null;
            _deliveryRows.Clear();
            _qrRows.Clear();
            ClearTimeline("Đã xóa điều kiện tra cứu.");
            _currentRows.Clear();
            if (_exportButton != null) _exportButton.Enabled = false;
            _status.Text = "Sẵn sàng";
        }

        private void ToggleSearch(bool enabled)
        {
            _stockLookup.Enabled = enabled;
            _txtCustomerLabel.Enabled = enabled;
            _txtCustomer.Enabled = enabled;
            _from.Enabled = enabled;
            _to.Enabled = enabled;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _deliveryView.FocusedRowChanged -= DeliveryView_FocusedRowChanged;
                _deliveryView.DoubleClick -= DeliveryGrid_DoubleClick;
                _qrView.DoubleClick -= DeliveryGrid_DoubleClick;
                _timelineDebounce.Stop();
                _timelineDebounce.Tick -= TimelineDebounce_Tick;
                _timelineDebounce.Dispose();
                _timelineView.SelectedNodeChanged -= TimelineView_SelectedNodeChanged;
                _timelineGate.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}