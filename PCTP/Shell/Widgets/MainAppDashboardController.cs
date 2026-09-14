using DevExpress.XtraCharts;
using DevExpress.XtraEditors;
using PCTP.Common;
using System;
using System.Data;
using System.Drawing;
using System.Net;
using System.Windows.Forms;
using Series = DevExpress.XtraCharts.Series;
using ViewType = DevExpress.XtraCharts.ViewType;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// Owns the legacy Main_APP dashboard state and presentation.
    /// Main_APP should only host the dashboard and coordinate navigation.
    /// </summary>
    internal sealed class MainAppDashboardController
    {
        private const int PageSize = 10;

        private readonly ClassSQL.IFSPROVIDER _ifs;
        private readonly ClassSQL.SQLPROVIDER _sql;
        private readonly IWaitFormService _waitForm;
        private readonly ChartControl _hvnPlaceholder;
        private readonly ChartControl _ymvnPlaceholder;
        private readonly Label _hostLabel;
        private readonly Label _qrMachineLabel;
        private readonly Label _dateLabel;

        private DataTable _hvnData = new DataTable();
        private DataTable _ymvnData = new DataTable();
        private int _hvnPage;
        private int _ymvnPage;
        private int _hvnTotalPages = 1;
        private int _ymvnTotalPages = 1;

        private SplitContainer _hvnSplit;
        private SplitContainer _ymvnSplit;
        private ChartControl _hvnMainChart;
        private ChartControl _hvnPercentChart;
        private ChartControl _ymvnMainChart;
        private ChartControl _ymvnPercentChart;

        private SimpleButton _hvnFirst;
        private SimpleButton _hvnPrevious;
        private SimpleButton _hvnNext;
        private SimpleButton _hvnLast;
        private LabelControl _hvnPageLabel;
        private SimpleButton _ymvnFirst;
        private SimpleButton _ymvnPrevious;
        private SimpleButton _ymvnNext;
        private SimpleButton _ymvnLast;
        private LabelControl _ymvnPageLabel;

        internal MainAppDashboardController(
            IWaitFormService waitForm,
            ChartControl hvnPlaceholder,
            ChartControl ymvnPlaceholder,
            Label hostLabel,
            Label qrMachineLabel,
            Label dateLabel)
        {
            if (waitForm == null) throw new ArgumentNullException("waitForm");
            if (hvnPlaceholder == null) throw new ArgumentNullException("hvnPlaceholder");
            if (ymvnPlaceholder == null) throw new ArgumentNullException("ymvnPlaceholder");
            if (hostLabel == null) throw new ArgumentNullException("hostLabel");
            if (qrMachineLabel == null) throw new ArgumentNullException("qrMachineLabel");
            if (dateLabel == null) throw new ArgumentNullException("dateLabel");

            _waitForm = waitForm;
            _ifs = new ClassSQL.IFSPROVIDER();
            _sql = new ClassSQL.SQLPROVIDER();
            _hvnPlaceholder = hvnPlaceholder;
            _ymvnPlaceholder = ymvnPlaceholder;
            _hostLabel = hostLabel;
            _qrMachineLabel = qrMachineLabel;
            _dateLabel = dateLabel;
        }

        internal void Initialize()
        {
            InitPagerControls();
            InitDualChartLayout(_hvnPlaceholder, out _hvnSplit, out _hvnMainChart, out _hvnPercentChart);
            InitDualChartLayout(_ymvnPlaceholder, out _ymvnSplit, out _ymvnMainChart, out _ymvnPercentChart);
            Load();
        }

        internal void Refresh()
        {
            _waitForm.Run(() =>
            {
                _hvnData = new DataTable();
                _ymvnData = new DataTable();
                _hvnPage = 0;
                _ymvnPage = 0;
                LoadData();
            }, "Đang tải lại dữ liệu dashboard...");
        }

        private void Load()
        {
            _waitForm.Run(LoadData, "Đang tải dữ liệu tổng quan...");
        }

        private void LoadData()
        {
            string hostname = Dns.GetHostName();
            IPHostEntry entry = Dns.GetHostEntry(hostname);
            _hostLabel.Text = "Tên của host này là: " + entry.HostName;

            string machineSql = "select TenMay from tbl_QR_MAY_DOCQR where TT = 1";
            string qrMachine = _sql.ExecuteReader(_sql.B7R2_FCCdb, machineSql);
            Main_APP.hostname = hostname;
            Main_APP.TM_BANQR = qrMachine ?? string.Empty;
            _qrMachineLabel.Text = "Máy được phép bắn QRcode là: " + Main_APP.TM_BANQR;

            _hvnData = LoadCustomerOrders("100001", true);
            _ymvnData = LoadCustomerOrders("100002", false);

            _hvnTotalPages = CalcTotalPages(_hvnData.Rows.Count);
            _ymvnTotalPages = CalcTotalPages(_ymvnData.Rows.Count);
            _hvnPage = 0;
            _ymvnPage = 0;

            BindHvn();
            BindYmvn();

            _dateLabel.Text = "Bạn đang xem dữ liệu ngày: " + DateTime.Now
                + "  |  Tồn kho hiện tại · Tổng SL xuất trong ngày · SL đã xuất tính đến hiện tại";
        }

        private DataTable LoadCustomerOrders(string customerNo, bool requirePo)
        {
            string poCondition = requirePo ? "and CUSTOMER_PO_REL_NO is not null" : string.Empty;
            string sql = @"
select sum(BUY_QTY_DUE) as TTCS,
       0 as SLDAGIAO,
       0 as SLTONKHO,
       CUSTOMER_PART_NO
from CUSTOMER_ORDER_JOIN
where CUSTOMER_NO = '" + customerNo + @"'
  and (OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual)
       or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual))
  and to_char(WANTED_DELIVERY_DATE,'ddmm') = to_char(SYSDATE,'ddmm')
  " + poCondition + @"
group by CUSTOMER_PART_NO
order by CUSTOMER_PART_NO";

            DataTable table = _ifs.ExecuteQuery(sql);
            EnrichTable(table);
            return table;
        }

        private void EnrichTable(DataTable table)
        {
            string today = DateTime.Now.ToString("MM/dd/yyyy");
            foreach (DataRow row in table.Rows)
            {
                string partNo = Convert.ToString(row["CUSTOMER_PART_NO"]);
                if (string.IsNullOrWhiteSpace(partNo))
                    continue;

                string safePartNo = partNo.Replace("'", "''");
                string stockSql = "select sum(slconlai) from stocktp where PART = '" + safePartNo + "'";
                string deliverySql = @"select sum(SOLUONG) from luuphieugiaohang
where MAHANG = '" + safePartNo + @"'
  and CONVERT(VARCHAR(10),NGAYGIAO,101) = '" + today + "'";

                string stock = _sql.ExecuteReader(_sql.B7R2_FCCdb, stockSql);
                string delivered = _sql.ExecuteReader(_sql.B7R2_FCCdb, deliverySql);
                row["SLTONKHO"] = string.IsNullOrEmpty(stock) ? 0 : Convert.ToDecimal(stock);
                row["SLDAGIAO"] = string.IsNullOrEmpty(delivered) ? 0 : Convert.ToDecimal(delivered);
            }
        }

        private void InitPagerControls()
        {
            Panel hvnPanel = CreatePagerPanel(_hvnPlaceholder);
            _hvnFirst = CreateButton("|◄", 40);
            _hvnPrevious = CreateButton("◄ Trước", 80);
            _hvnPageLabel = CreatePageLabel();
            _hvnNext = CreateButton("Sau ►", 80);
            _hvnLast = CreateButton("►|", 40);
            AddPagerControls(hvnPanel, _hvnFirst, _hvnPrevious, _hvnPageLabel, _hvnNext, _hvnLast);
            _hvnFirst.Click += delegate { _hvnPage = 0; BindHvn(); };
            _hvnPrevious.Click += delegate { if (_hvnPage > 0) _hvnPage--; BindHvn(); };
            _hvnNext.Click += delegate { if (_hvnPage < _hvnTotalPages - 1) _hvnPage++; BindHvn(); };
            _hvnLast.Click += delegate { _hvnPage = _hvnTotalPages - 1; BindHvn(); };

            Panel ymvnPanel = CreatePagerPanel(_ymvnPlaceholder);
            _ymvnFirst = CreateButton("|◄", 40);
            _ymvnPrevious = CreateButton("◄ Trước", 80);
            _ymvnPageLabel = CreatePageLabel();
            _ymvnNext = CreateButton("Sau ►", 80);
            _ymvnLast = CreateButton("►|", 40);
            AddPagerControls(ymvnPanel, _ymvnFirst, _ymvnPrevious, _ymvnPageLabel, _ymvnNext, _ymvnLast);
            _ymvnFirst.Click += delegate { _ymvnPage = 0; BindYmvn(); };
            _ymvnPrevious.Click += delegate { if (_ymvnPage > 0) _ymvnPage--; BindYmvn(); };
            _ymvnNext.Click += delegate { if (_ymvnPage < _ymvnTotalPages - 1) _ymvnPage++; BindYmvn(); };
            _ymvnLast.Click += delegate { _ymvnPage = _ymvnTotalPages - 1; BindYmvn(); };
        }

        private static Panel CreatePagerPanel(Control anchor)
        {
            Panel panel = new Panel
            {
                Height = 32,
                Dock = DockStyle.Bottom,
                BackColor = Color.WhiteSmoke
            };
            anchor.Parent.Controls.Add(panel);
            anchor.Parent.Controls.SetChildIndex(panel, 0);
            return panel;
        }

        private static SimpleButton CreateButton(string text, int width)
        {
            return new SimpleButton
            {
                Text = text,
                Width = width,
                Height = 26,
                Appearance = { Font = new Font("Tahoma", 8.25f) }
            };
        }

        private static LabelControl CreatePageLabel()
        {
            return new LabelControl
            {
                AutoSizeMode = LabelAutoSizeMode.None,
                Width = 200,
                Height = 26,
                Appearance =
                {
                    TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Center },
                    Font = new Font("Tahoma", 8.25f)
                }
            };
        }

        private static void AddPagerControls(Panel panel, params Control[] controls)
        {
            int x = 6;
            foreach (Control control in controls)
            {
                control.Location = new Point(x, 3);
                panel.Controls.Add(control);
                x += control.Width + 4;
            }
        }

        private void InitDualChartLayout(ChartControl placeholder, out SplitContainer split, out ChartControl mainChart, out ChartControl percentChart)
        {
            Control parent = placeholder.Parent;
            DockStyle dock = placeholder.Dock;
            Rectangle bounds = placeholder.Bounds;
            parent.Controls.Remove(placeholder);

            split = new SplitContainer
            {
                Orientation = Orientation.Horizontal,
                Dock = dock,
                Bounds = bounds,
                Panel1MinSize = 120,
                Panel2MinSize = 80,
                SplitterWidth = 4
            };
            parent.Controls.Add(split);

            mainChart = new ChartControl { Dock = DockStyle.Fill };
            percentChart = new ChartControl { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(mainChart);
            split.Panel2.Controls.Add(percentChart);

            ConfigureMainChart(mainChart);
            ConfigurePercentChart(percentChart);
        }

        private static void ConfigureMainChart(ChartControl chart)
        {
            chart.Series.Clear();
            chart.BackColor = Color.White;
            chart.BorderOptions.Visibility = DevExpress.Utils.DefaultBoolean.False;

            Series orders = new Series("Tổng Đơn Hàng", ViewType.Bar);
            orders.ArgumentDataMember = "CUSTOMER_PART_NO";
            orders.ValueDataMembers.AddRange(new[] { "TTCS" });
            orders.LabelsVisibility = DevExpress.Utils.DefaultBoolean.False;
            orders.ToolTipPointPattern = "Đơn hàng: {V:#,##0}";
            ((BarSeriesView)orders.View).BarWidth = 0.35;
            chart.Series.Add(orders);

            Series stock = new Series("Tồn Kho", ViewType.Bar);
            stock.ArgumentDataMember = "CUSTOMER_PART_NO";
            stock.ValueDataMembers.AddRange(new[] { "SLTONKHO" });
            stock.LabelsVisibility = DevExpress.Utils.DefaultBoolean.False;
            stock.ToolTipPointPattern = "Tồn kho: {V:#,##0}";
            ((BarSeriesView)stock.View).BarWidth = 0.35;
            chart.Series.Add(stock);

            XYDiagram diagram = (XYDiagram)chart.Diagram;
            diagram.Rotated = true;
            diagram.AxisX.QualitativeScaleOptions.AutoGrid = false;
            diagram.AxisX.Tickmarks.Visible = false;
            diagram.AxisX.GridLines.Visible = false;
            diagram.AxisX.Label.Font = new Font("Tahoma", 7.5f);
            diagram.AxisY.Label.TextPattern = "{V:n0}";
            diagram.AxisY.GridLines.Visible = true;

            SecondaryAxisY secondary = new SecondaryAxisY("axTonKho");
            secondary.Label.TextPattern = "{V:n0}";
            secondary.GridLines.Visible = false;
            diagram.SecondaryAxesY.Add(secondary);
            ((BarSeriesView)stock.View).AxisY = secondary;

            chart.CrosshairEnabled = DevExpress.Utils.DefaultBoolean.True;
            chart.CrosshairOptions.ShowArgumentLabels = true;
            chart.CrosshairOptions.ShowValueLabels = true;
        }

        private static void ConfigurePercentChart(ChartControl chart)
        {
            chart.Series.Clear();
            chart.BackColor = Color.White;
            chart.BorderOptions.Visibility = DevExpress.Utils.DefaultBoolean.False;
            chart.Legend.Visibility = DevExpress.Utils.DefaultBoolean.False;

            Series percent = new Series("% Đã Giao", ViewType.Bar);
            percent.ArgumentDataMember = "CUSTOMER_PART_NO";
            percent.ValueDataMembers.AddRange(new[] { "PCT_GIAO" });
            percent.LabelsVisibility = DevExpress.Utils.DefaultBoolean.True;
            percent.Label.TextPattern = "{V:F0}%";
            percent.ToolTipPointPattern = "Đã giao: {V:F1}%";
            ((BarSeriesView)percent.View).BarWidth = 0.55;
            chart.Series.Add(percent);

            XYDiagram diagram = (XYDiagram)chart.Diagram;
            diagram.Rotated = true;
            diagram.AxisX.QualitativeScaleOptions.AutoGrid = false;
            diagram.AxisX.Tickmarks.Visible = false;
            diagram.AxisX.GridLines.Visible = false;
            diagram.AxisY.WholeRange.Auto = false;
            diagram.AxisY.WholeRange.MinValue = 0;
            diagram.AxisY.WholeRange.MaxValue = 100;
            diagram.AxisY.VisualRange.Auto = false;
            diagram.AxisY.VisualRange.MinValue = 0;
            diagram.AxisY.VisualRange.MaxValue = 100;
            diagram.AxisY.Label.TextPattern = "{V:n0}%";
            diagram.AxisY.GridLines.Visible = true;
        }

        private void BindHvn()
        {
            Bind(_hvnData, _hvnPage, _hvnMainChart, _hvnPercentChart, _hvnPageLabel, _hvnFirst, _hvnPrevious, _hvnNext, _hvnLast, _hvnTotalPages);
        }

        private void BindYmvn()
        {
            Bind(_ymvnData, _ymvnPage, _ymvnMainChart, _ymvnPercentChart, _ymvnPageLabel, _ymvnFirst, _ymvnPrevious, _ymvnNext, _ymvnLast, _ymvnTotalPages);
        }

        private static void Bind(DataTable source, int page, ChartControl mainChart, ChartControl percentChart, LabelControl label, SimpleButton first, SimpleButton previous, SimpleButton next, SimpleButton last, int totalPages)
        {
            DataTable pageTable = GetPage(source, page);
            if (!pageTable.Columns.Contains("PCT_GIAO"))
                pageTable.Columns.Add("PCT_GIAO", typeof(double));

            foreach (DataRow row in pageTable.Rows)
            {
                double order = Convert.ToDouble(row["TTCS"]);
                double delivered = Convert.ToDouble(row["SLDAGIAO"]);
                row["PCT_GIAO"] = order > 0 ? Math.Min(Math.Round(delivered / order * 100, 1), 100) : 0;
            }

            mainChart.DataSource = pageTable;
            percentChart.DataSource = pageTable;
            mainChart.RefreshData();
            percentChart.RefreshData();

            int rowCount = source.Rows.Count;
            int from = rowCount == 0 ? 0 : page * PageSize + 1;
            int to = Math.Min((page + 1) * PageSize, rowCount);
            label.Text = string.Format("Trang {0} / {1}   ({2}–{3} của {4} mã)", page + 1, totalPages, from, to, rowCount);
            first.Enabled = previous.Enabled = page > 0;
            next.Enabled = last.Enabled = page < totalPages - 1;
        }

        private static DataTable GetPage(DataTable source, int page)
        {
            int start = Math.Max(0, page * PageSize);
            int count = Math.Min(PageSize, Math.Max(0, source.Rows.Count - start));
            DataTable result = source.Clone();
            for (int i = start; i < start + count; i++)
                result.ImportRow(source.Rows[i]);
            return result;
        }

        private static int CalcTotalPages(int rowCount)
        {
            return rowCount == 0 ? 1 : (int)Math.Ceiling((double)rowCount / PageSize);
        }
    }
}
