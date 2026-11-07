using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using PCTP.Shell.UI;
using PCTP.Shared.Helpers;
using PCTP.Shared.Lookup;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    [DesignerCategory("Code")]
    public sealed class FormBaoCaoQualityHistory : WmsTitleBarForm
    {
        private readonly IQualityHistoryQuery _query;
        private readonly IWaitFormService _waitForm;
        private readonly StockLookupControl _lookup = new StockLookupControl();
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly ComboBoxEdit _result = new ComboBoxEdit();
        private readonly GridControl _masterGrid = new GridControl();
        private readonly GridView _masterView;
        private readonly GridControl _detailGrid = new GridControl();
        private readonly GridView _detailView;
        private readonly TraceTimelineControl _timeline = new TraceTimelineControl();
        private readonly TextBox _timelineDetail = new TextBox();
        private readonly Label _status = new Label();
        private readonly SimpleButton _export = new SimpleButton();
        private readonly SimpleButton _print = new SimpleButton();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim _detailGate = new SemaphoreSlim(1, 1);
        private readonly System.Windows.Forms.Timer _detailDebounce = new System.Windows.Forms.Timer { Interval = 250 };
        private List<QualityHistoryRow> _rows = new List<QualityHistoryRow>();
        private List<QualityHistoryDetailRow> _details = new List<QualityHistoryDetailRow>();
        private List<TraceTimelineNode> _timelineNodes = new List<TraceTimelineNode>();
        private bool _busy;
        private bool _binding;
        private int _detailVersion;

        public FormBaoCaoQualityHistory(IQualityHistoryQuery query)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _waitForm = new WaitFormService(this);
            _masterView = new GridView(_masterGrid);
            _detailView = new GridView(_detailGrid);
            Text = "Báo cáo - Lịch sử kiểm tra chất lượng";
            ClientSize = new Size(1280, 780);
            MinimumSize = new Size(1050, 650);
            StartPosition = FormStartPosition.CenterParent;
            BuildUi();
            Shown += async (s, e) => await InitializeAsync();
        }

        public FormBaoCaoQualityHistory() : this(new QualityHistoryQueryService()) { }

        private void BuildUi()
        {
            // Cùng bố cục với FormBaoCaoTraceability: một root TableLayoutPanel duy nhất.
            // WmsTitleBarForm đưa control vào content host khi Load, nên các control Dock=Top/Fill
            // rời nhau có thể bị đổi thứ tự và che mất vùng tra cứu.
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
            root.Controls.Add(BuildSearchPanel(), 0, 0);
            root.Controls.Add(BuildWorkspace(), 0, 1);
            Controls.Add(root);

            _detailDebounce.Tick += async (s, e) =>
            {
                _detailDebounce.Stop();
                await LoadFocusedSessionAsync();
            };
            _masterView.FocusedRowChanged += (s, e) =>
            {
                if (_binding) return;
                _detailDebounce.Stop();
                _detailDebounce.Start();
            };
        }

        private Control BuildSearchPanel()
        {
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
            search.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var lookupLabel = CreateFieldLabel("Mã hàng / LOT / QR");
            search.Controls.Add(lookupLabel, 0, 0);
            search.SetColumnSpan(lookupLabel, 3);
            _lookup.Dock = DockStyle.Fill;
            _lookup.Margin = new Padding(3, 0, 3, 0);
            _lookup.SetLotQrPlaceholder("Nhập LOT (hoặc LOT ghép LOT1-SL1,LOT2-SL2) hoặc quét QR có chứa LOT...");
            _lookup.SearchRequested += async (s, e) => await LoadAsync();
            _lookup.SelectionCleared += (s, e) => ClearResults();
            search.Controls.Add(_lookup, 0, 1);
            search.SetColumnSpan(_lookup, 3);

            _result.Properties.Items.AddRange(new[] { "Tất cả", "PASS", "FAIL" });
            _result.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _result.SelectedIndex = 0;
            AddField(search, "Kết quả", _result, 3, 2);

            foreach (DateEdit date in new[] { _from, _to })
            {
                date.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
                date.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                date.Properties.EditFormat.FormatString = "dd/MM/yyyy";
                date.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                date.Dock = DockStyle.None;
                date.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                date.Margin = new Padding(3, 0, 3, 0);
            }
            _from.DateTime = DateTime.Today.AddDays(-30);
            _to.DateTime = DateTime.Today;
            search.Controls.Add(CreateFieldLabel("From"), 0, 2);
            search.Controls.Add(_from, 1, 2);
            search.Controls.Add(CreateFieldLabel("To"), 2, 2);
            search.Controls.Add(_to, 3, 2);

            var clearButton = new SimpleButton { Text = "Xóa điều kiện", Dock = DockStyle.Fill, Margin = new Padding(3, 2, 3, 2) };
            clearButton.Click += (s, e) => ResetConditions();
            search.Controls.Add(clearButton, 4, 2);
            search.SetColumnSpan(clearButton, 2);

            var statusBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            _status.Text = "Nhập điều kiện rồi bấm Enter / nút tra cứu.";
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Color.FromArgb(35, 35, 35);
            _status.BackColor = Color.Transparent;
            statusBar.Controls.Add(_status, 0, 0);
            _export.Text = "Xuất Excel";
            _export.Dock = DockStyle.Fill;
            _export.Margin = new Padding(3, 1, 3, 1);
            _export.Enabled = false;
            _export.Click += ExportClick;
            statusBar.Controls.Add(_export, 1, 0);
            _print.Text = "In báo cáo";
            _print.Dock = DockStyle.Fill;
            _print.Margin = new Padding(3, 1, 3, 1);
            _print.Enabled = false;
            _print.Click += PrintClick;
            statusBar.Controls.Add(_print, 2, 0);
            search.Controls.Add(statusBar, 0, 3);
            search.SetColumnSpan(statusBar, 6);
            return search;
        }

        private Control BuildWorkspace()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                Margin = Padding.Empty
            };

            // --- Danh sách phiên (trên)
            ConfigureGrid(_masterGrid, _masterView);
            _masterView.OptionsView.ShowFooter = true;
            _masterView.RowStyle += (s, e) =>
            {
                if (e.RowHandle < 0) return;
                var row = _masterView.GetRow(e.RowHandle) as QualityHistoryRow;
                if (row != null && (row.FailCount > 0 ||
                    string.Equals(row.FinalResult, "FAIL", StringComparison.OrdinalIgnoreCase)))
                    e.Appearance.BackColor = Color.FromArgb(255, 225, 225);
            };
            AddColumn(_masterView, "InspectionCode", "Mã phiên", 145);
            AddColumn(_masterView, "ItemCode", "Mã hàng", 135);
            AddColumn(_masterView, "LotNo", "LOT tổng", 190);
            AddColumn(_masterView, "ProductionDate", "Ngày SX", 95);
            AddColumn(_masterView, "DocumentNo", "Chứng từ", 120);
            AddColumn(_masterView, "TotalQuantity", "SL tổng", 75, "n0");
            AddColumn(_masterView, "TotalBox", "Số thùng", 75, "n0");
            AddColumn(_masterView, "PassCount", "Đạt", 60, "n0");
            AddColumn(_masterView, "FailCount", "Không đạt", 80, "n0");
            AddColumn(_masterView, "FinalResult", "Kết quả", 80);
            AddColumn(_masterView, "CheckedAt", "Thời gian kiểm", 145, "dd/MM/yyyy HH:mm:ss");
            var masterHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            masterHost.Controls.Add(_masterGrid);
            masterHost.Controls.Add(CreateGridCaption("DANH SÁCH PHIÊN KIỂM TRA (chọn một phiên để xem chi tiết thùng và timeline)"));
            split.Panel1.Controls.Add(masterHost);

            // --- Chi tiết thùng | Timeline | Chi tiết node (dưới) - cùng kiểu với FormBaoCaoStockHistory
            ConfigureGrid(_detailGrid, _detailView);
            _detailView.RowStyle += (s, e) =>
            {
                if (e.RowHandle < 0) return;
                var row = _detailView.GetRow(e.RowHandle) as QualityHistoryDetailRow;
                if (row != null && !row.IsMatch) e.Appearance.BackColor = Color.FromArgb(255, 225, 225);
            };
            AddColumn(_detailView, "BoxLotNo", "LOT thùng", 230);
            AddColumn(_detailView, "BoxProductionDate", "Ngày SX", 105);
            AddColumn(_detailView, "IsMatch", "Khớp", 65);
            AddColumn(_detailView, "MismatchFields", "Trường sai", 350);
            AddColumn(_detailView, "CheckedAt", "Thời gian", 145, "dd/MM/yyyy HH:mm:ss");
            var detailHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            detailHost.Controls.Add(_detailGrid);
            detailHost.Controls.Add(CreateGridCaption("CHI TIẾT THÙNG CỦA PHIÊN ĐANG CHỌN"));

            _timeline.Dock = DockStyle.Fill;
            _timeline.Clear("Chọn một phiên kiểm tra để xem timeline.");
            _timeline.SelectedNodeChanged += (s, e) =>
            {
                TraceTimelineNode node = _timeline.SelectedNode;
                _timelineDetail.Text = node == null ? string.Empty
                    : node.Title + Environment.NewLine + Environment.NewLine + node.DetailText();
            };
            _timelineDetail.Dock = DockStyle.Fill;
            _timelineDetail.Multiline = true;
            _timelineDetail.ReadOnly = true;
            _timelineDetail.ScrollBars = ScrollBars.Vertical;
            _timelineDetail.BorderStyle = BorderStyle.FixedSingle;
            _timelineDetail.Font = new Font("Consolas", 9F);
            _timelineDetail.BackColor = Color.White;
            var nodeDetailHost = new Panel { Dock = DockStyle.Fill };
            nodeDetailHost.Controls.Add(_timelineDetail);
            nodeDetailHost.Controls.Add(CreateGridCaption("CHI TIẾT NODE ĐANG CHỌN"));

            var timelineSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5
            };
            timelineSplit.Panel1.Controls.Add(_timeline);
            timelineSplit.Panel2.Controls.Add(nodeDetailHost);
            var timelineHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            timelineHost.Controls.Add(timelineSplit);
            timelineHost.Controls.Add(CreateGridCaption("TIMELINE CHẤT LƯỢNG: TỔNG HỢP PHIÊN → TỪNG THÙNG (PASS/FAIL)"));

            var bottom = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5
            };
            bottom.Panel1.Controls.Add(detailHost);
            bottom.Panel2.Controls.Add(timelineHost);
            split.Panel2.Controls.Add(bottom);

            // Đặt splitter SAU khi control có kích thước thật và luôn kẹp trong giới hạn hợp lệ.
            Action applyAll = () =>
            {
                ApplySplit(split, (int)(split.Height * 0.40), 140, 230);
                ApplySplit(bottom, (int)(bottom.Width * 0.38), 280, 420);
                ApplySplit(timelineSplit, (int)(timelineSplit.Width * 0.62), 300, 240);
            };
            split.SizeChanged += (s, e) => ApplySplit(split, (int)(split.Height * 0.40), 140, 230);
            split.HandleCreated += (s, e) => applyAll();
            bottom.SizeChanged += (s, e) => ApplySplit(bottom, (int)(bottom.Width * 0.38), 280, 420);
            bottom.HandleCreated += (s, e) => applyAll();
            timelineSplit.SizeChanged += (s, e) => ApplySplit(timelineSplit, (int)(timelineSplit.Width * 0.62), 300, 240);
            timelineSplit.HandleCreated += (s, e) => applyAll();
            Shown += (s, e) =>
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke(applyAll);
            };
            return split;
        }

        private static void ApplySplit(SplitContainer sc, int desired, int min1, int min2)
        {
            if (sc == null || sc.IsDisposed || !sc.IsHandleCreated) return;
            if (sc.Panel1Collapsed || sc.Panel2Collapsed) return;
            int total = sc.Orientation == Orientation.Horizontal ? sc.Height : sc.Width;
            int available = total - sc.SplitterWidth;
            if (available <= 0) return;
            try
            {
                sc.Panel1MinSize = 0;
                sc.Panel2MinSize = 0;
                if (available >= min1 + min2)
                {
                    sc.Panel1MinSize = min1;
                    sc.Panel2MinSize = min2;
                    desired = Math.Max(min1, Math.Min(desired, available - min2));
                }
                else desired = Math.Max(0, Math.Min(desired, available));
                if (sc.SplitterDistance != desired) sc.SplitterDistance = desired;
            }
            catch (InvalidOperationException) { }
            catch (ArgumentException) { }
        }

        private static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(35, 35, 35),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                AutoEllipsis = true,
                Margin = new Padding(3, 0, 3, 0)
            };
        }

        private static void AddField(TableLayoutPanel panel, string caption, Control editor, int column, int span)
        {
            Label label = CreateFieldLabel(caption);
            // Anchor Left|Right (không Top/Bottom) => TableLayoutPanel căn giữa theo chiều dọc, thẳng hàng với nhãn.
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
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 65, 95),
                BackColor = Color.White,
                Padding = new Padding(6, 0, 0, 0),
                Margin = Padding.Empty
            };
        }

        private static void ConfigureGrid(GridControl grid, GridView view)
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
            view.OptionsView.ShowAutoFilterRow = true;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.OptionsSelection.MultiSelect = false;
            view.OptionsCustomization.AllowFilter = true;
            view.Appearance.HeaderPanel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            view.Appearance.Row.Font = new Font("Segoe UI", 9F);
            view.Appearance.HeaderPanel.Options.UseFont = true;
            view.Appearance.Row.Options.UseFont = true;
        }

        private static void AddColumn(GridView view, string field, string caption, int width, string format = null)
        {
            var column = new GridColumn
            {
                FieldName = field, Caption = caption, Width = width, Visible = true
            };
            column.OptionsColumn.AllowEdit = false;
            column.OptionsColumn.ReadOnly = true;
            if (!string.IsNullOrWhiteSpace(format))
            {
                column.DisplayFormat.FormatType = field == "CheckedAt"
                    ? DevExpress.Utils.FormatType.DateTime : DevExpress.Utils.FormatType.Numeric;
                column.DisplayFormat.FormatString = format;
            }
            view.Columns.Add(column);
        }

        private async Task InitializeAsync()
        {
            try
            {
                IReadOnlyList<string> codes = await _waitForm.RunAsync(
                    () => _query.GetItemCodesAsync(_lifetime.Token),
                    "Đang tải danh sách mã hàng QC...");
                var items = new DataTable();
                items.Columns.Add("PART", typeof(string));
                foreach (string code in codes ?? new string[0])
                    if (!string.IsNullOrWhiteSpace(code)) items.Rows.Add(code);
                _lookup.SetItemSuggestions(items, "PART", null);
                // Danh sách mã hàng lấy từ DISTINCT InspectionLog.ItemCode của DB đang kết nối.
                // Rỗng nghĩa là DB này chưa có phiên QC nào (hoặc ItemCode toàn NULL), không phải lỗi giao diện.
                await LoadAsync();
                if (items.Rows.Count == 0 && !IsDisposed)
                    _status.Text = "Bảng InspectionLog trên DB hiện tại chưa có mã hàng nào nên ô gợi ý trống. " +
                        "Kiểm tra DB đang kết nối và dữ liệu phiên QC (xem SELECT COUNT(*), COUNT(ItemCode) FROM dbo.InspectionLog).";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể khởi tạo lịch sử chất lượng.\r\n" + ex.Message,
                    "Lịch sử chất lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadAsync()
        {
            if (_busy || IsDisposed) return;
            DateTime from = _from.DateTime.Date;
            DateTime to = _to.DateTime.Date.AddDays(1).AddTicks(-1);
            if (from > to)
            {
                XtraMessageBox.Show(this, "Khoảng ngày không hợp lệ.", "Tra cứu",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string keyword = _lookup.InputText;
            string search = keyword;
            if (!_lookup.IsItemSearch && !string.IsNullOrWhiteSpace(keyword))
            {
                // QR tem thùng/tổng chuẩn: LOT:PART:NGAYSX:QTY[:...]. Lịch sử chất lượng lưu LOT, không lưu cả payload QR.
                // LOT ghép (LOT1-SL1,LOT2-SL2) được giữ nguyên để repository tách từng LOT và tra riêng.
                string[] qrFields = keyword.Split(':');
                string lotKey = (qrFields.Length == 4 || qrFields.Length == 6)
                    ? (qrFields[0] ?? string.Empty).Trim()
                    : keyword;
                search = string.IsNullOrWhiteSpace(lotKey) ? null : "LOTQR:" + lotKey;
            }

            _busy = true;
            ToggleBusy(false);
            try
            {
                _status.Text = "Đang tra cứu...";
                IReadOnlyList<QualityHistoryRow> found = await _waitForm.RunAsync(
                    () => _query.SearchAsync(from, to, search,
                        Convert.ToString(_result.EditValue), _lifetime.Token),
                    "Đang tra cứu lịch sử kiểm tra chất lượng...");

                Interlocked.Increment(ref _detailVersion); // bỏ mọi lần tải chi tiết đang chờ của kết quả cũ
                _binding = true;
                try
                {
                    _rows = (found ?? new List<QualityHistoryRow>()).ToList();
                    _masterGrid.DataSource = _rows;
                }
                finally { _binding = false; }

                _details.Clear();
                _detailGrid.DataSource = null;
                _timelineNodes.Clear();
                _timelineDetail.Clear();
                int pass = _rows.Count(x => string.Equals(x.FinalResult, "PASS", StringComparison.OrdinalIgnoreCase));
                int fail = _rows.Count(x => string.Equals(x.FinalResult, "FAIL", StringComparison.OrdinalIgnoreCase));
                if (_rows.Count == 0)
                {
                    _timeline.Clear("Không có phiên kiểm tra phù hợp.");
                    _status.Text = string.Format(
                        "Không có phiên kiểm tra trong khoảng {0:dd/MM/yyyy} - {1:dd/MM/yyyy}. Thử nới khoảng ngày hoặc kiểm tra LOT/mã hàng.",
                        from, to);
                }
                else
                {
                    _timeline.Clear("Đang tải timeline của phiên đầu tiên...");
                    _status.Text = string.Format("Tổng phiên: {0} | PASS: {1} | FAIL: {2} | Tra cứu: {3}",
                        _rows.Count, pass, fail, string.IsNullOrWhiteSpace(keyword) ? "Tất cả" : keyword);
                }
                SetReportButtons(_rows.Count > 0);
                if (_rows.Count > 0) await LoadFocusedSessionAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _status.Text = "Lỗi tra cứu.";
                XtraMessageBox.Show(this, "Không thể tải lịch sử kiểm tra.\r\n" + ex.Message,
                    "Lịch sử chất lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _busy = false;
                ToggleBusy(true);
            }
        }

        private async Task LoadFocusedSessionAsync()
        {
            if (IsDisposed) return;
            int version = Interlocked.Increment(ref _detailVersion);
            var row = _masterView.GetFocusedRow() as QualityHistoryRow;
            if (row == null || string.IsNullOrWhiteSpace(row.InspectionCode))
            {
                _details.Clear();
                _detailGrid.DataSource = null;
                _timelineNodes.Clear();
                _timelineDetail.Clear();
                _timeline.Clear("Chưa chọn phiên kiểm tra.");
                return;
            }

            await _detailGate.WaitAsync();
            try
            {
                // Người dùng đã chọn dòng khác (hoặc tra cứu lại) trong lúc chờ: bỏ lần tải cũ.
                if (version != Volatile.Read(ref _detailVersion) || IsDisposed) return;
                IReadOnlyList<QualityHistoryDetailRow> found = await _waitForm.RunAsync(
                    () => _query.GetDetailsAsync(row.InspectionCode, _lifetime.Token),
                    "Đang tải chi tiết phiên kiểm tra...");
                if (version != Volatile.Read(ref _detailVersion) || IsDisposed) return;
                _details = (found ?? new List<QualityHistoryDetailRow>()).ToList();
                _detailGrid.DataSource = _details;
                BuildTimeline(row, _details);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (version == Volatile.Read(ref _detailVersion) && !IsDisposed)
                {
                    _timeline.Clear("Không tải được chi tiết phiên: " + ex.Message);
                    XtraMessageBox.Show(this, "Không thể tải chi tiết phiên kiểm tra.\r\n" + ex.Message,
                        "Lịch sử chất lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally { _detailGate.Release(); }
        }

        private void BuildTimeline(QualityHistoryRow session, IList<QualityHistoryDetailRow> details)
        {
            var nodes = new List<TraceTimelineNode>();
            DateTime at = session.CheckedAt ?? DateTime.MinValue;
            nodes.Add(new TraceTimelineNode
            {
                Kind = TraceNodeKind.StockOther,
                Title = "Bắt đầu / tổng hợp kiểm tra",
                EventType = "QUALITY_INSPECTION",
                EventTime = session.CheckedAt,
                SortTime = at,
                SortOrder = 0,
                LotNo = session.LotNo,
                PartNo = session.ItemCode,
                Quantity = session.TotalQuantity,
                DocumentNo = session.DocumentNo,
                SourceName = "dbo.InspectionLog",
                SourceKey = "InspectionCode=" + session.InspectionCode,
                EvidenceStatus = session.FinalResult,
                Subtitle = "Phiên " + session.InspectionCode + " | " + session.TotalBox + " thùng | Đạt " +
                    session.PassCount + " | Không đạt " + session.FailCount,
                Note = "Kết quả tổng hợp của phiên kiểm tra chất lượng."
            });
            int index = 0;
            foreach (QualityHistoryDetailRow detail in details ?? new List<QualityHistoryDetailRow>())
            {
                bool pass = detail.IsMatch;
                DateTime moment = detail.CheckedAt ?? session.CheckedAt ?? DateTime.MinValue;
                nodes.Add(new TraceTimelineNode
                {
                    Kind = pass ? TraceNodeKind.StockIn : TraceNodeKind.Missing,
                    Title = pass ? "QC PASS - thùng " + (index + 1) : "QC FAIL - thùng " + (index + 1),
                    EventType = pass ? "QC_PASS" : "QC_FAIL",
                    EventTime = detail.CheckedAt ?? session.CheckedAt,
                    SortTime = moment,
                    SortOrder = index + 1,
                    LotNo = detail.BoxLotNo,
                    PartNo = session.ItemCode,
                    Quantity = null,
                    DocumentNo = session.DocumentNo,
                    SourceName = "dbo.InspectionLog",
                    SourceKey = "InspectionCode=" + session.InspectionCode + "; BoxLotNo=" + detail.BoxLotNo,
                    EvidenceStatus = pass ? "Đạt" : "Không đạt",
                    Subtitle = "LOT thùng: " + TraceTimelineNode.ValueOrDash(detail.BoxLotNo) +
                        " | NSX: " + TraceTimelineNode.ValueOrDash(detail.BoxProductionDate) +
                        (pass ? string.Empty : " | Sai: " + TraceTimelineNode.ValueOrDash(detail.MismatchFields)),
                    Note = pass ? "Thùng khớp thông tin kiểm tra." :
                        "Các trường sai lệch: " + TraceTimelineNode.ValueOrDash(detail.MismatchFields)
                });
                index++;
            }
            _timelineNodes = nodes.OrderBy(x => x.SortTime).ThenBy(x => x.SortOrder).ToList();
            _timeline.SetNodes(_timelineNodes, "Phiên này chưa có sự kiện chi tiết.");
        }

        private void ClearResults()
        {
            if (_busy) return;
            Interlocked.Increment(ref _detailVersion);
            _binding = true;
            try { _masterGrid.DataSource = null; }
            finally { _binding = false; }
            _detailGrid.DataSource = null;
            _rows.Clear();
            _details.Clear();
            _timelineNodes.Clear();
            _timeline.Clear("Nhập điều kiện rồi tra cứu.");
            _timelineDetail.Clear();
            SetReportButtons(false);
            _status.Text = "Đã xóa điều kiện tra cứu.";
        }

        /// <summary>Nút "Xóa điều kiện": đưa mọi ô lọc về mặc định (giống FormBaoCaoTraceability) và xóa kết quả.</summary>
        private void ResetConditions()
        {
            if (_busy) return;
            _lookup.ClearInput();
            _lookup.SelectItemTab();
            _result.SelectedIndex = 0;
            _from.DateTime = DateTime.Today.AddDays(-30);
            _to.DateTime = DateTime.Today;
            ClearResults();
            _status.Text = "Sẵn sàng";
        }

        private void SetReportButtons(bool hasData)
        {
            _export.Enabled = hasData;
            _print.Enabled = hasData;
        }

        private void ToggleBusy(bool enabled)
        {
            _lookup.Enabled = enabled;
            _from.Enabled = enabled;
            _to.Enabled = enabled;
            _result.Enabled = enabled;
            // Xuất/In chỉ bật khi có dữ liệu (không bật lại mù quáng sau mỗi lần tra cứu).
            _export.Enabled = enabled && _rows.Count > 0;
            _print.Enabled = enabled && _rows.Count > 0;
        }

        private List<ReportOverviewRow> BuildOverview()
        {
            var overview = TraceReportExporter.NewOverview("Lịch sử kiểm tra chất lượng");
            overview.AddRow("Từ ngày", _from.DateTime.ToString("dd/MM/yyyy"));
            overview.AddRow("Đến ngày", _to.DateTime.ToString("dd/MM/yyyy"));
            overview.AddRow(_lookup.IsItemSearch ? "Mã hàng" : "LOT / QR", _lookup.InputText);
            overview.AddRow("Kết quả", Convert.ToString(_result.EditValue));
            overview.AddRow("Số phiên", _rows.Count.ToString());
            overview.AddRow("Số chi tiết phiên đang chọn", _details.Count.ToString());
            overview.AddRow("Số node timeline phiên đang chọn", _timelineNodes.Count.ToString());
            return overview;
        }

        private List<ReportGridSection> BuildSections()
        {
            var sections = new List<ReportGridSection>();
            if (_masterGrid.DataSource != null)
                sections.Add(new ReportGridSection("Phiên kiểm tra", "DANH SÁCH PHIÊN KIỂM TRA", _masterGrid, _masterView));
            if (_detailGrid.DataSource != null)
                sections.Add(new ReportGridSection("Chi tiết thùng", "CHI TIẾT THÙNG CỦA PHIÊN ĐANG CHỌN", _detailGrid, _detailView));
            return sections;
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_rows.Count == 0)
            {
                XtraMessageBox.Show(this, "Chưa có dữ liệu để xuất. Hãy tra cứu trước.", "Xuất Excel",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.FileName = "LichSuQuality_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    TraceReportExporter.ExportWorkbook(dialog.FileName, BuildOverview(), BuildSections(), _timelineNodes);
                    Cursor = Cursors.Default;
                    XtraMessageBox.Show(this, "Đã xuất Excel gồm Tổng quan, phiên kiểm tra, chi tiết phiên đang chọn và Timeline.", "Xuất Excel",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    XtraMessageBox.Show(this, "Không thể xuất Excel: " + ex.Message, "Xuất Excel",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PrintClick(object sender, EventArgs e)
        {
            if (_rows.Count == 0) return;
            try { TraceReportExporter.ShowPrintPreview(BuildOverview(), BuildSections(), _timelineNodes); }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể tạo bản in: " + ex.Message, "In báo cáo",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _detailDebounce.Stop();
                _detailDebounce.Dispose();
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}