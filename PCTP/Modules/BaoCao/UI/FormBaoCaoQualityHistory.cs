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
        private readonly LabelControl _summary = new LabelControl();
        private readonly SimpleButton _export = new SimpleButton();
        private readonly SimpleButton _print = new SimpleButton();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private List<QualityHistoryRow> _rows = new List<QualityHistoryRow>();
        private List<QualityHistoryDetailRow> _details = new List<QualityHistoryDetailRow>();
        private List<TraceTimelineNode> _timelineNodes = new List<TraceTimelineNode>();
        private bool _loading;

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
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(6)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var filter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false,
                AutoScroll = true, Padding = new Padding(4)
            };
            _from.Width = 105;
            _to.Width = 105;
            foreach (DateEdit date in new[] { _from, _to })
            {
                date.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
                date.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            }
            _from.DateTime = DateTime.Today.AddDays(-7);
            _to.DateTime = DateTime.Today;
            filter.Controls.Add(new LabelControl { Text = "Từ ngày:", Padding = new Padding(0, 7, 4, 0) });
            filter.Controls.Add(_from);
            filter.Controls.Add(new LabelControl { Text = "Đến ngày:", Padding = new Padding(10, 7, 4, 0) });
            filter.Controls.Add(_to);

            _lookup.Width = 390;
            _lookup.Height = 78;
            _lookup.SetLotQrPlaceholder("Nhập LOT hoặc quét QR có chứa LOT...");
            _lookup.SearchRequested += async (s, e) => await LoadAsync();
            _lookup.SelectionCleared += (s, e) => ClearResults();
            filter.Controls.Add(_lookup);

            filter.Controls.Add(new LabelControl { Text = "Kết quả:", Padding = new Padding(8, 7, 4, 0) });
            _result.Width = 90;
            _result.Properties.Items.AddRange(new[] { "Tất cả", "PASS", "FAIL" });
            _result.SelectedIndex = 0;
            filter.Controls.Add(_result);

            _export.Text = "Xuất Excel";
            _export.Width = 95;
            _export.Click += ExportClick;
            filter.Controls.Add(_export);
            _print.Text = "In";
            _print.Width = 55;
            _print.Click += PrintClick;
            filter.Controls.Add(_print);
            root.Controls.Add(filter, 0, 0);

            _summary.Dock = DockStyle.Fill;
            _summary.Padding = new Padding(4, 5, 0, 0);
            root.Controls.Add(_summary, 0, 1);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                SplitterWidth = 7, Panel1MinSize = 150, Panel2MinSize = 230
            };
            var masterGroup = new GroupControl { Text = "Danh sách phiên kiểm tra", Dock = DockStyle.Fill };
            _masterGrid.Dock = DockStyle.Fill;
            _masterGrid.MainView = _masterView;
            _masterGrid.ViewCollection.Add(_masterView);
            ConfigureView(_masterView);
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
            _masterView.FocusedRowChanged += async (s, e) => await LoadFocusedSessionAsync();
            masterGroup.Controls.Add(_masterGrid);
            split.Panel1.Controls.Add(masterGroup);

            var lowerTabs = new TabControl { Dock = DockStyle.Fill };
            var detailPage = new TabPage("Chi tiết thùng");
            var timelinePage = new TabPage("Timeline chất lượng");
            _detailGrid.Dock = DockStyle.Fill;
            _detailGrid.MainView = _detailView;
            _detailGrid.ViewCollection.Add(_detailView);
            ConfigureView(_detailView);
            AddColumn(_detailView, "BoxLotNo", "LOT thùng", 230);
            AddColumn(_detailView, "BoxProductionDate", "Ngày SX", 105);
            AddColumn(_detailView, "IsMatch", "Khớp", 65);
            AddColumn(_detailView, "MismatchFields", "Trường sai", 350);
            AddColumn(_detailView, "CheckedAt", "Thời gian", 145, "dd/MM/yyyy HH:mm:ss");
            detailPage.Controls.Add(_detailGrid);

            var timelineLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            timelineLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            timelineLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            _timeline.Dock = DockStyle.Fill;
            _timeline.SelectedNodeChanged += (s, e) =>
            {
                TraceTimelineNode node = _timeline.SelectedNode;
                _timelineDetail.Text = node == null ? string.Empty : node.DetailText();
            };
            _timelineDetail.Dock = DockStyle.Fill;
            _timelineDetail.Multiline = true;
            _timelineDetail.ReadOnly = true;
            _timelineDetail.ScrollBars = ScrollBars.Vertical;
            _timelineDetail.Font = new Font("Consolas", 9F);
            timelineLayout.Controls.Add(_timeline, 0, 0);
            timelineLayout.Controls.Add(_timelineDetail, 0, 1);
            timelinePage.Controls.Add(timelineLayout);
            lowerTabs.TabPages.Add(detailPage);
            lowerTabs.TabPages.Add(timelinePage);
            split.Panel2.Controls.Add(lowerTabs);
            root.Controls.Add(split, 0, 2);
            Controls.Add(root);
            Shown += (s, e) =>
            {
                try { split.SplitterDistance = Math.Max(split.Panel1MinSize, (int)(split.Height * 0.43)); }
                catch (InvalidOperationException) { }
            };
        }

        private static void ConfigureView(GridView view)
        {
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsView.EnableAppearanceEvenRow = true;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
        }

        private static void AddColumn(GridView view, string field, string caption, int width, string format = null)
        {
            var column = new GridColumn
            {
                FieldName = field, Caption = caption, Width = width, Visible = true
            };
            column.OptionsColumn.AllowEdit = false;
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
                await LoadAsync();
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
            if (_loading || IsDisposed) return;
            DateTime from = _from.DateTime.Date;
            DateTime to = _to.DateTime.Date.AddDays(1).AddTicks(-1);
            if (from > to)
            {
                XtraMessageBox.Show(this, "Khoảng ngày không hợp lệ.", "Tra cứu",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string keyword = _lookup.InputText;
            string search = _lookup.IsItemSearch ? keyword : (string.IsNullOrWhiteSpace(keyword) ? null : "LOTQR:" + keyword);
            _loading = true;
            ToggleBusy(false);
            try
            {
                IReadOnlyList<QualityHistoryRow> found = await _waitForm.RunAsync(
                    () => _query.SearchAsync(from, to, search,
                        Convert.ToString(_result.EditValue), _lifetime.Token),
                    "Đang tra cứu lịch sử kiểm tra chất lượng...");
                _rows = (found ?? new List<QualityHistoryRow>()).ToList();
                _masterGrid.DataSource = _rows;
                _detailGrid.DataSource = null;
                _details.Clear();
                _timelineNodes.Clear();
                _timeline.SetNodes(null, "Chọn một phiên kiểm tra để xem timeline.");
                _timelineDetail.Clear();
                int pass = _rows.Count(x => string.Equals(x.FinalResult, "PASS", StringComparison.OrdinalIgnoreCase));
                int fail = _rows.Count(x => string.Equals(x.FinalResult, "FAIL", StringComparison.OrdinalIgnoreCase));
                _summary.Text = string.Format("Tổng phiên: {0} | PASS: {1} | FAIL: {2} | Tra cứu: {3}",
                    _rows.Count, pass, fail, string.IsNullOrWhiteSpace(keyword) ? "Tất cả" : keyword);
                if (_rows.Count > 0)
                {
                    // Binding the master grid raises FocusedRowChanged while loading;
                    // release the guard before explicitly loading the selected session.
                    _loading = false;
                    await LoadFocusedSessionAsync();
                    _loading = true;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể tải lịch sử kiểm tra.\r\n" + ex.Message,
                    "Lịch sử chất lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _loading = false;
                ToggleBusy(true);
            }
        }

        private async Task LoadFocusedSessionAsync()
        {
            if (_loading || IsDisposed) return;
            var row = _masterView.GetFocusedRow() as QualityHistoryRow;
            if (row == null || string.IsNullOrWhiteSpace(row.InspectionCode))
            {
                _detailGrid.DataSource = null;
                _timeline.SetNodes(null, "Chưa chọn phiên kiểm tra.");
                return;
            }
            try
            {
                IReadOnlyList<QualityHistoryDetailRow> found = await _waitForm.RunAsync(
                    () => _query.GetDetailsAsync(row.InspectionCode, _lifetime.Token),
                    "Đang tải chi tiết phiên kiểm tra...");
                _details = (found ?? new List<QualityHistoryDetailRow>()).ToList();
                _detailGrid.DataSource = _details;
                BuildTimeline(row, _details);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể tải chi tiết phiên kiểm tra.\r\n" + ex.Message,
                    "Lịch sử chất lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            if (_loading) return;
            _masterGrid.DataSource = null;
            _detailGrid.DataSource = null;
            _rows.Clear();
            _details.Clear();
            _timelineNodes.Clear();
            _timeline.SetNodes(null, "Nhập điều kiện rồi tra cứu.");
            _timelineDetail.Clear();
            _summary.Text = "Đã xóa điều kiện tra cứu.";
        }

        private void ToggleBusy(bool enabled)
        {
            _lookup.Enabled = enabled;
            _from.Enabled = enabled;
            _to.Enabled = enabled;
            _result.Enabled = enabled;
            _export.Enabled = enabled;
            _print.Enabled = enabled;
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
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}