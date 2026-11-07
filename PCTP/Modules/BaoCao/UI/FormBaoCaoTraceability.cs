using DevExpress.XtraEditors;
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
        private readonly DataGridView _grid = new DataGridView();
        private readonly IStockHistoryQuery _stockHistoryQuery;
        private readonly IProductionLotTraceQuery _productionQuery;
        private readonly FlowLayoutPanel _timeline = new FlowLayoutPanel();
        private readonly Panel _detailPanel = new Panel();
        private readonly Label _detailTitle = new Label();
        private readonly TextBox _detailText = new TextBox();
        private readonly Label _timelineCaption = new Label();
        private readonly List<TraceNode> _timelineNodes = new List<TraceNode>();
        private readonly Label _status = new Label();
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

                Task<IReadOnlyList<string>> itemTask = _customerQuery.GetItemCodesAsync(CancellationToken.None);
                Task<IReadOnlyList<string>> customerTask = _customerQuery.GetCustomersAsync(CancellationToken.None);
                await Task.WhenAll(itemTask, customerTask);

                var itemRows = new DataTable();
                itemRows.Columns.Add("PART", typeof(string));
                foreach (string itemCode in itemTask.Result ?? new string[0])
                    if (!string.IsNullOrWhiteSpace(itemCode))
                        itemRows.Rows.Add(itemCode);
                _stockLookup.SetItemSuggestions(itemRows, "PART", null);
                ApplyPendingQuickSearch();
                ConfigureLookup(_txtCustomer, customerTask.Result, "Khách hàng");
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var search = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 174,
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
            _from.Margin = new Padding(3, 0, 3, 0);
            search.Controls.Add(_from, 1, 2);

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

            _status.Text = "Đang chuẩn bị...";
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.FromArgb(35, 35, 35);
            _status.BackColor = Color.Transparent;
            search.Controls.Add(_status, 0, 3);
            search.SetColumnSpan(_status, 6);

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
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            ConfigureGrid(_grid);
            _grid.CellDoubleClick += DeliveryGrid_CellDoubleClick;
            left.Controls.Add(_grid, 0, 0);

            var timelineHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(6) };
            _timelineCaption.Text = "TIMELINE GIAO DỊCH";
            _timelineCaption.Dock = DockStyle.Top;
            _timelineCaption.Height = 28;
            _timelineCaption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _timelineCaption.ForeColor = Color.FromArgb(35, 65, 95);
            _timeline.Dock = DockStyle.Fill;
            _timeline.FlowDirection = FlowDirection.TopDown;
            _timeline.WrapContents = false;
            _timeline.AutoScroll = true;
            _timeline.BackColor = Color.White;
            timelineHost.Controls.Add(_timeline);
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
            ShowEmptyTimeline("Chọn một dòng giao hàng để dựng timeline theo LOT.");

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

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.ForeColor = Color.FromArgb(35, 35, 35);
            grid.GridColor = Color.FromArgb(220, 220, 220);
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersVisible = true;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 230, 216);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(35, 35, 35);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(35, 35, 35);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
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
                    if (LotCodeHelper.ClassifyLotKey(stockSearch) != LotKeyKind.Unknown)
                        lot = stockSearch;
                    else
                        qr = stockSearch;
                }
                string customerLabel = _txtCustomerLabel.Text.Trim();
                string customer = GetLookupText(_txtCustomer);

                IReadOnlyList<DeliveryTraceRow> rows;
                if (!string.IsNullOrWhiteSpace(lot))
                    rows = await _lotQuery.SearchAsync(lot, part, customer, from, to, CancellationToken.None);
                else if (!string.IsNullOrWhiteSpace(customer) && string.IsNullOrWhiteSpace(qr) && string.IsNullOrWhiteSpace(customerLabel))
                    rows = await _customerQuery.SearchAsync(customer, part, from, to, CancellationToken.None);
                else
                    rows = await _qrQuery.SearchAsync(qr, customerLabel, part, customer, from, to, CancellationToken.None);

                _currentRows = rows == null ? new List<DeliveryTraceRow>() : rows.ToList();
                _grid.DataSource = new BindingList<DeliveryTraceRow>(_currentRows);
                ClearTimeline("Chọn một dòng giao hàng để dựng timeline theo LOT.");
                _status.Text = "Tìm thấy " + _currentRows.Count + " dòng." +
                    (_currentRows.Count == 0 ? " Kiểm tra khoảng ngày (theo NGAYGIAO) và ô Khách hàng." : string.Empty);
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

        private async void DeliveryGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentRows.Count) return;

            DeliveryTraceRow row = _currentRows[e.RowIndex];
            try
            {
                _status.Text = "Đang dựng timeline theo bằng chứng LOT...";
                _timeline.Enabled = false;
                IReadOnlyList<DeliveryLotTraceRow> lots = await _qrQuery.GetLotsAsync(
                    row.DeliveryKey, row.QRCode, CancellationToken.None);

                var nodes = new List<TraceNode>();
                nodes.Add(TraceNode.ForDelivery(row));

                if (lots != null)
                {
                    foreach (DeliveryLotTraceRow lot in lots)
                    {
                        if (lot == null || string.IsNullOrWhiteSpace(lot.LotNo)) continue;

                        nodes.Add(TraceNode.ForDeliveryLot(row, lot));

                        IReadOnlyList<ProductionLotTraceRow> production = await _productionQuery.FindByLotAsync(
                            lot.LotNo, row.PartNo, CancellationToken.None);
                        if (production != null && production.Count > 0)
                        {
                            foreach (ProductionLotTraceRow prod in production)
                                nodes.Add(TraceNode.ForProduction(row, lot, prod));
                        }
                        else
                        {
                            nodes.Add(TraceNode.MissingProduction(row, lot));
                        }

                        var criteria = new HistorySearchCriteria
                        {
                            LotNo = lot.LotNo,
                            PartNo = row.PartNo,
                            From = null,
                            To = null
                        };
                        IReadOnlyList<ItemHistoryRow> history = await _stockHistoryQuery.SearchAsync(
                            criteria, CancellationToken.None);

                        int matchedHistoryCount = 0;
                        if (history != null)
                        {
                            foreach (ItemHistoryRow item in history)
                            {
                                if (item == null) continue;
                                // A stock-history row is included only when its LOT matches
                                // the allocation being traced. The query's LIKE is narrowed
                                // again here to avoid prefix/suffix cross-matches.
                                if (!LotCodeHelper.AreLotKeysEquivalent(item.LotNo, lot.LotNo))
                                    continue;
                                nodes.Add(TraceNode.ForStock(item, lot.LotNo));
                                matchedHistoryCount++;
                            }
                        }

                        if (matchedHistoryCount == 0)
                            nodes.Add(TraceNode.MissingStockHistory(row, lot));
                    }
                }

                _timelineNodes.Clear();
                _timelineNodes.AddRange(nodes
                    .OrderBy(x => x.EventTime.HasValue ? x.EventTime.Value : DateTime.MinValue)
                    .ThenBy(x => x.SortOrder)
                    .ToList());
                RenderTimeline();
                _status.Text = "Timeline có " + _timelineNodes.Count +
                    " node. " + (lots == null ? 0 : lots.Count) + " phân bổ LOT; " +
                    "chỉ gồm chứng từ giao hàng và lịch sử kho truy vấn được.";
            }
            catch (Exception ex)
            {
                ClearTimeline("Không dựng được timeline do lỗi truy vấn.");
                _status.Text = "Lỗi dựng timeline.";
                XtraMessageBox.Show(this, ex.Message, "Báo cáo truy xuất", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _timeline.Enabled = true;
            }
        }

        private void RenderTimeline()
        {
            _timeline.SuspendLayout();
            try
            {
                _timeline.Controls.Clear();
                if (_timelineNodes.Count == 0)
                {
                    ShowEmptyTimeline("Không có sự kiện lịch sử phù hợp với LOT đã chọn.");
                    return;
                }

                for (int i = 0; i < _timelineNodes.Count; i++)
                {
                    TraceNode node = _timelineNodes[i];
                    var button = new Button
                    {
                        Width = Math.Max(300, _timeline.ClientSize.Width - 34),
                        Height = 76,
                        TextAlign = ContentAlignment.MiddleLeft,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.White,
                        ForeColor = Color.FromArgb(35, 35, 35),
                        Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                        Tag = node,
                        Margin = new Padding(4, 3, 4, 3),
                        Text = (i + 1).ToString("00") + "  " + node.Title + Environment.NewLine +
                            (node.EventTime.HasValue ? node.EventTime.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Không có thời gian") +
                            "   |   LOT: " + (node.LotNo ?? "-") + Environment.NewLine +
                            node.Subtitle
                    };
                    button.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 225);
                    button.Click += TimelineNode_Click;
                    _timeline.Controls.Add(button);
                }
                if (_timelineNodes.Count > 0)
                    ShowNodeDetail(_timelineNodes[0]);
            }
            finally { _timeline.ResumeLayout(); }
        }

        private void TimelineNode_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            var node = button == null ? null : button.Tag as TraceNode;
            if (node != null) ShowNodeDetail(node);
        }

        private void ShowNodeDetail(TraceNode node)
        {
            _detailTitle.Text = node.Title;
            _detailText.Text =
                "Trạng thái bằng chứng : " + node.EvidenceStatus + Environment.NewLine +
                "Loại sự kiện         : " + node.EventType + Environment.NewLine +
                "Thời gian             : " + (node.EventTime.HasValue ? node.EventTime.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Không có dữ liệu") + Environment.NewLine +
                "LOT                   : " + ValueOrDash(node.LotNo) + Environment.NewLine +
                "Mã hàng               : " + ValueOrDash(node.PartNo) + Environment.NewLine +
                "Số lượng              : " + (node.Quantity.HasValue ? node.Quantity.Value.ToString("N0") : "-") + Environment.NewLine +
                "Vị trí từ              : " + ValueOrDash(node.FromLocation) + Environment.NewLine +
                "Vị trí đến             : " + ValueOrDash(node.ToLocation) + Environment.NewLine +
                "QR                    : " + ValueOrDash(node.QrCode) + Environment.NewLine +
                "Tem khách hàng        : " + ValueOrDash(node.CustomerLabel) + Environment.NewLine +
                "Khách hàng            : " + ValueOrDash(node.CustomerName) + Environment.NewLine +
                "Chứng từ              : " + ValueOrDash(node.DocumentNo) + Environment.NewLine +
                "Người thực hiện       : " + ValueOrDash(node.UserName) + Environment.NewLine +
                "Nguồn dữ liệu         : " + ValueOrDash(node.SourceName) + Environment.NewLine +
                "Khóa tra cứu/đối chiếu : " + ValueOrDash(node.SourceKey) + Environment.NewLine +
                Environment.NewLine + node.Note;
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

        private void ShowEmptyTimeline(string message)
        {
            _timeline.Controls.Clear();
            _detailTitle.Text = "Chi tiết sự kiện";
            _detailText.Text = message;
        }

        private void ClearTimeline(string message)
        {
            _timelineNodes.Clear();
            ShowEmptyTimeline(message);
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private sealed class TraceNode
        {
            public string Title;
            public string EventType;
            public DateTime? EventTime;
            public string LotNo;
            public string PartNo;
            public decimal? Quantity;
            public string FromLocation;
            public string ToLocation;
            public string QrCode;
            public string CustomerLabel;
            public string CustomerName;
            public string DocumentNo;
            public string UserName;
            public string SourceName;
            public string SourceKey;
            public string EvidenceStatus;
            public string Subtitle;
            public string Note;
            public int SortOrder;

            public static TraceNode ForDelivery(DeliveryTraceRow row)
            {
                return new TraceNode
                {
                    Title = "Giao hàng đã lưu",
                    EventType = "DELIVERY",
                    EventTime = row.DeliveryDate,
                    LotNo = row.LotNoRaw,
                    PartNo = row.PartNo,
                    Quantity = row.Quantity,
                    QrCode = row.QRCode,
                    CustomerLabel = row.CustomerLabelData,
                    CustomerName = row.CustomerName,
                    DocumentNo = row.DocumentNo,
                    SourceName = "dbo.LUUPHIEUGIAOHANG + dbo.LUUDOCQRCODE",
                    SourceKey = row.DeliveryKey,
                    EvidenceStatus = "Có phiếu giao; liên kết QR cần đối soát",
                    Subtitle = "Phiếu: " + ValueOrDash(row.DocumentNo) + " | KH: " + ValueOrDash(row.CustomerName),
                    Note = "Bản ghi phiếu giao được lưu trong LUUPHIEUGIAOHANG. QR/tem được lấy từ LUUDOCQRCODE qua điều kiện nhà máy, mã hàng, ngày và giờ giao; đây chưa phải khóa ngoại duy nhất nên cần đối soát nếu có nhiều bản ghi trùng điều kiện. Ngày giao lấy từ NGAYGIAO; không suy diễn giờ nếu nguồn không cung cấp.",
                    SortOrder = 30
                };
            }

            public static TraceNode ForDeliveryLot(DeliveryTraceRow row, DeliveryLotTraceRow lot)
            {
                return new TraceNode
                {
                    Title = "Phân bổ LOT trên giao hàng",
                    EventType = "DELIVERY_LOT",
                    EventTime = row.DeliveryDate,
                    LotNo = lot.LotNo,
                    PartNo = row.PartNo,
                    Quantity = lot.Quantity,
                    QrCode = lot.QRCode,
                    CustomerLabel = row.CustomerLabelData,
                    CustomerName = row.CustomerName,
                    DocumentNo = row.DocumentNo,
                    SourceName = "LOT composite trong dữ liệu giao hàng",
                    SourceKey = row.DeliveryKey + " / " + lot.LotNo,
                    EvidenceStatus = "Có bằng chứng phân bổ LOT",
                    Subtitle = "SL: " + lot.Quantity.ToString("N0") + " | QR: " + ValueOrDash(lot.QRCode),
                    Note = "LOT và số lượng được parse từ trường LOT composite của bản ghi giao hàng.",
                    SortOrder = 31
                };
            }

            public static TraceNode ForProduction(DeliveryTraceRow delivery, DeliveryLotTraceRow lot, ProductionLotTraceRow prod)
            {
                return new TraceNode
                {
                    Title = "Thông tin sản xuất",
                    EventType = "PRODUCTION",
                    EventTime = prod.ProductionDate,
                    LotNo = prod.Lot,
                    PartNo = prod.PartNo,
                    Quantity = prod.ProducedQty,
                    QrCode = lot.QRCode,
                    CustomerName = delivery.CustomerName,
                    DocumentNo = delivery.DocumentNo,
                    SourceName = "dbo.STOCKTP (cột sản xuất)",
                    SourceKey = "LOT=" + prod.Lot,
                    EvidenceStatus = "Có bản ghi sản xuất cho LOT",
                    Subtitle = "Ca: " + (prod.Shift.HasValue ? prod.Shift.Value.ToString() : "-") +
                        " | Line: " + ValueOrDash(prod.LineCode) +
                        " | SL SX: " + (prod.ProducedQty.HasValue ? prod.ProducedQty.Value.ToString("N0") : "-") +
                        " | Nhập TP: " + (prod.ReceivedDate.HasValue ? prod.ReceivedDate.Value.ToString("dd/MM/yyyy") : "-") +
                        " (" + (prod.ReceivedQty.HasValue ? prod.ReceivedQty.Value.ToString("N0") : "-") + ")",
                    Note = "Dữ kiện sản xuất lấy từ STOCKTP theo khóa LOT (13/20 ký tự). Chỉ dùng ngày/ca/SL sản xuất, ngày/SL nhập TP, line và bộ phận; không dùng tồn hiện tại làm bằng chứng lịch sử. Model: " +
                        ValueOrDash(prod.Model) + " | Bộ phận: " + ValueOrDash(prod.DeptCode),
                    SortOrder = 5
                };
            }

            public static TraceNode MissingProduction(DeliveryTraceRow delivery, DeliveryLotTraceRow lot)
            {
                return new TraceNode
                {
                    Title = "Chưa tìm thấy thông tin sản xuất",
                    EventType = "MISSING_PRODUCTION",
                    EventTime = delivery.DeliveryDate,
                    LotNo = lot.LotNo,
                    PartNo = delivery.PartNo,
                    Quantity = lot.Quantity,
                    QrCode = lot.QRCode,
                    CustomerName = delivery.CustomerName,
                    DocumentNo = delivery.DocumentNo,
                    SourceName = "dbo.STOCKTP (cột sản xuất)",
                    SourceKey = "LOT=" + lot.LotNo,
                    EvidenceStatus = "Thiếu bằng chứng sản xuất",
                    Subtitle = "Không có bản ghi STOCKTP khớp LOT này (khóa 13/20 ký tự)",
                    Note = "Đây là cảnh báo thiếu bằng chứng, không phải một sự kiện sản xuất.",
                    SortOrder = 19
                };
            }

            public static TraceNode MissingStockHistory(DeliveryTraceRow delivery, DeliveryLotTraceRow lot)
            {
                return new TraceNode
                {
                    Title = "Chưa tìm thấy lịch sử kho",
                    EventType = "MISSING_STOCK_HISTORY",
                    EventTime = delivery.DeliveryDate,
                    LotNo = lot.LotNo,
                    PartNo = delivery.PartNo,
                    Quantity = lot.Quantity,
                    QrCode = lot.QRCode,
                    CustomerLabel = delivery.CustomerLabelData,
                    CustomerName = delivery.CustomerName,
                    DocumentNo = delivery.DocumentNo,
                    SourceName = "dbo.StockHistory (sp_GetStockHistory)",
                    SourceKey = "LOT=" + lot.LotNo,
                    EvidenceStatus = "Thiếu bằng chứng lịch sử",
                    Subtitle = "Chưa truy xuất được bản ghi nhập/xuất kho cho LOT này",
                    Note = "Đây là cảnh báo thiếu bằng chứng, không phải một giao dịch kho. Không suy diễn từ STOCKTP hoặc tồn kho hiện tại.",
                    SortOrder = 20
                };
            }

            public static TraceNode ForStock(ItemHistoryRow row, string requestedLot)
            {
                return new TraceNode
                {
                    Title = "Lịch sử kho: " + ValueOrDash(row.ActionType),
                    EventType = row.ActionType,
                    EventTime = row.EventTime,
                    LotNo = row.LotNo,
                    PartNo = row.ItemCode,
                    Quantity = row.Quantity,
                    FromLocation = row.FromLocation,
                    ToLocation = row.ToLocation,
                    QrCode = row.QrCode,
                    DocumentNo = row.DocumentNo,
                    UserName = row.UserName,
                    SourceName = "dbo.StockHistory (sp_GetStockHistory)",
                    SourceKey = "LOT=" + requestedLot + " | Phiếu=" + ValueOrDash(row.DocumentNo) +
                        " | " + (row.EventTime.HasValue ? row.EventTime.Value.ToString("O") : "no-time"),
                    EvidenceStatus = "Có bản ghi lịch sử kho",
                    Subtitle = "SL: " + row.Quantity.ToString("N0") + " | " +
                        ValueOrDash(row.FromLocation) + " → " + ValueOrDash(row.ToLocation),
                    Note = "Sự kiện lấy từ read model lịch sử kho. Không coi tồn kho hiện tại là bằng chứng lịch sử.",
                    SortOrder = 10
                };
            }
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
            _grid.DataSource = null;
            ClearTimeline("Đã xóa điều kiện tra cứu.");
            _currentRows.Clear();
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
                _grid.CellDoubleClick -= DeliveryGrid_CellDoubleClick;
            base.Dispose(disposing);
        }
    }
}
