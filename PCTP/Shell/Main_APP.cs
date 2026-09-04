using DevExpress.XtraBars;
using DevExpress.XtraBars.Docking2010.Views;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraCharts;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.UserDesigner;
using DevExpress.XtraSplashScreen;
using PCTP.Acess_Image;
using PCTP.Common;
using PCTP.FuctionPrint;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.QRCODE_HVN;
using PCTP.QRCODE_HVN.ComaprePart;
using PCTP.QRCODE_HVN.Report;
using PCTP.QRCODE_HVN.Report;
using PCTP.QRCODE_HVN.YMN;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Shell.Widgets;
using PCTP.VIEWSTOCK;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.ViewForm;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Series = DevExpress.XtraCharts.Series;
using ViewType = DevExpress.XtraCharts.ViewType;

namespace PCTP
{
  
    public partial class Main_APP : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        // =====================================================================
        // PHÂN TRANG: cấu hình số dòng mỗi trang
        // =====================================================================
        private const int PAGE_SIZE = 10;

        // Lưu toàn bộ data gốc (load 1 lần duy nhất)
        private DataTable _fullTableHVN = new DataTable();
        private DataTable _fullTableYMVN = new DataTable();

        // Trang hiện tại (0-based)
        private int _pageHVN = 0;
        private int _pageYMVN = 0;

        // Tổng số trang
        private int _totalPagesHVN = 1;
        private int _totalPagesYMVN = 1;

        // =====================================================================
        // CONTROLS phân trang — khai báo ở đây, khởi tạo trong InitPagingControls()
        // =====================================================================
        // -- HVN --
        private SimpleButton btnHVN_Prev;
        private SimpleButton btnHVN_Next;
        private SimpleButton btnHVN_First;
        private SimpleButton btnHVN_Last;
        private LabelControl lblHVN_Page;
        private Panel pnlHVN_Pager;

        // -- YMVN --
        private SimpleButton btnYMVN_Prev;
        private SimpleButton btnYMVN_Next;
        private SimpleButton btnYMVN_First;
        private SimpleButton btnYMVN_Last;
        private LabelControl lblYMVN_Page;
        private Panel pnlYMVN_Pager;

        // =====================================================================
        // 2-CHART LAYOUT: mỗi tab = SplitContainer chia đôi dọc
        //   Trên (70%): Bar chart  — Tồn kho vs Tổng đơn hàng (scale độc lập)
        //   Dưới (30%): Bar chart  — % Đã giao (0–100%)
        // =====================================================================
        // HVN
        private DevExpress.XtraCharts.ChartControl chartHVN_Main;   // tồn kho & đơn hàng
        private DevExpress.XtraCharts.ChartControl chartHVN_Pct;    // % đã giao
        private SplitContainer splitHVN;

        // YMVN
        private DevExpress.XtraCharts.ChartControl chartYMVN_Main;
        private DevExpress.XtraCharts.ChartControl chartYMVN_Pct;
        private SplitContainer splitYMVN;

        // =====================================================================
        ClassSQL.IFSPROVIDER IFS = new ClassSQL.IFSPROVIDER();
        ClassSQL.SQLPROVIDER SQL = new ClassSQL.SQLPROVIDER();

        //wait form 
        private readonly IWaitFormService _waitForm;

        public static string hostname = "";
        public static string TM_BANQR = "";
        // đang mở module nào, luôn hiển thị ngay khi vào app.
        private LabelControl _lblAppChoQC, _lblAppChoDinhHuong, _lblAppDaDuyetChuaTra, _lblAppLechA0;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly INhapKhoDashboardRepository _dashRepo;
        private WarehouseDashboardBar _dashBar;
        // =====================================================================
        public Main_APP()
        {
            InitializeComponent();
            accordionControl.SelectedElement = NHAccordionControlElement;
        }

        private void Main_APP_Load(object sender, EventArgs e)
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle("Caramel");

            // 1. Khởi tạo pager controls
            InitPagingControls();

            // 2. Khởi tạo layout 2 chart cho mỗi tab
            InitDualChartLayout(CharHVN, out splitHVN, out chartHVN_Main, out chartHVN_Pct);
            InitDualChartLayout(CharYMVN, out splitYMVN, out chartYMVN_Main, out chartYMVN_Pct);
            BuildAppDashboardBar();
            // 3. Load toàn bộ data
            _waitForm.Run(() => LoadAllData(), "Đang tải dữ liệu tổng quan...");
        }
        // ★ THÊM: thanh dashboard tổng — chỉ đọc số liệu quy trình, không thao tác.
        // Đặt Dock=Top, add SAU cùng để nó nổi trên cùng của form (theo đúng thứ tự
        // Dock=Top: control add sau nằm trên control add trước).
        private void BuildAppDashboardBar()
        {
            var provider = new ClassSQL.SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);

            var phieuXuLyRepo = new PhieuXuLyBatThuongRepository(sql, uow);
            var dashRepo = new NhapKhoDashboardRepository(sql,uow);

            _dashBar = new WarehouseDashboardBar(phieuXuLyRepo, dashRepo) { Dock = DockStyle.Top };
            Controls.Add(_dashBar);
            _dashBar.BringToFront();
        }

        // ★ THÊM: gọi lại khi LoadAllData/RefreshDashboard chạy, để số liệu
        // luôn khớp với thời điểm dữ liệu chart được refresh.

        // =====================================================================
        // KHỞI TẠO PAGER CONTROLS
        // =====================================================================
        private SimpleButton MakePagerBtn(string text, int w = 70)
        {
            return new SimpleButton
            {
                Text = text,
                Width = w,
                Height = 26,
                Appearance = { Font = new Font("Tahoma", 8.25f) }
            };
        }

        private LabelControl MakePagerLbl()
        {
            return new LabelControl
            {
                AutoSizeMode = LabelAutoSizeMode.None,
                Width = 200,
                Height = 26,
                Appearance =
                {
                    TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Center },
                    Font        = new Font("Tahoma", 8.25f)
                }
            };
        }

        private Panel MakePagerPanel(Control anchorControl)
        {
            var pnl = new Panel
            {
                Height = 32,
                Dock = DockStyle.Bottom,
                BackColor = Color.WhiteSmoke
            };
            anchorControl.Parent.Controls.Add(pnl);
            anchorControl.Parent.Controls.SetChildIndex(pnl, 0);
            return pnl;
        }

        private void InitPagingControls()
        {
            // -------- HVN Pager --------
            pnlHVN_Pager = MakePagerPanel(CharHVN);
            btnHVN_First = MakePagerBtn("|◄", 40);
            btnHVN_Prev = MakePagerBtn("◄ Trước", 80);
            lblHVN_Page = MakePagerLbl();
            btnHVN_Next = MakePagerBtn("Sau ►", 80);
            btnHVN_Last = MakePagerBtn("►|", 40);

            int x = 6;
            foreach (Control c in new Control[] { btnHVN_First, btnHVN_Prev, lblHVN_Page, btnHVN_Next, btnHVN_Last })
            {
                c.Location = new Point(x, 3);
                pnlHVN_Pager.Controls.Add(c);
                x += c.Width + 4;
            }

            btnHVN_First.Click += (s, e) => { _pageHVN = 0; BindChartHVN(); };
            btnHVN_Prev.Click += (s, e) => { if (_pageHVN > 0) _pageHVN--; BindChartHVN(); };
            btnHVN_Next.Click += (s, e) => { if (_pageHVN < _totalPagesHVN - 1) _pageHVN++; BindChartHVN(); };
            btnHVN_Last.Click += (s, e) => { _pageHVN = _totalPagesHVN - 1; BindChartHVN(); };

            // -------- YMVN Pager --------
            pnlYMVN_Pager = MakePagerPanel(CharYMVN);
            btnYMVN_First = MakePagerBtn("|◄", 40);
            btnYMVN_Prev = MakePagerBtn("◄ Trước", 80);
            lblYMVN_Page = MakePagerLbl();
            btnYMVN_Next = MakePagerBtn("Sau ►", 80);
            btnYMVN_Last = MakePagerBtn("►|", 40);

            x = 6;
            foreach (Control c in new Control[] { btnYMVN_First, btnYMVN_Prev, lblYMVN_Page, btnYMVN_Next, btnYMVN_Last })
            {
                c.Location = new Point(x, 3);
                pnlYMVN_Pager.Controls.Add(c);
                x += c.Width + 4;
            }

            btnYMVN_First.Click += (s, e) => { _pageYMVN = 0; BindChartYMVN(); };
            btnYMVN_Prev.Click += (s, e) => { if (_pageYMVN > 0) _pageYMVN--; BindChartYMVN(); };
            btnYMVN_Next.Click += (s, e) => { if (_pageYMVN < _totalPagesYMVN - 1) _pageYMVN++; BindChartYMVN(); };
            btnYMVN_Last.Click += (s, e) => { _pageYMVN = _totalPagesYMVN - 1; BindChartYMVN(); };
        }

        // =====================================================================
        // LOAD DỮ LIỆU — chỉ gọi 1 lần, lưu vào _fullTableHVN / _fullTableYMVN
        // =====================================================================
        private void LoadAllData()
        {
            // --- Hostname ---
            IPHostEntry ip = new IPHostEntry();
            hostname = System.Net.Dns.GetHostName();
            ip = System.Net.Dns.GetHostByName(hostname);
            lblHostName.Text = "Tên của host này là: " + ip.HostName;

            string sqlqr = "select TenMay from tbl_QR_MAY_DOCQR where TT = 1";
            TM_BANQR = SQL.ExecuteReader(SQL.B7R2_FCCdb, sqlqr);
            lblMayBan.Text = "Máy được phép bắn QRcode là: " + TM_BANQR;

            // -------- HVN --------
            string sql = @"
                select sum(BUY_QTY_DUE) as TTCS,
                       0               as SLDAGIAO,
                       0               as SLTONKHO,
                       CUSTOMER_PART_NO
                from   CUSTOMER_ORDER_JOIN
                where  CUSTOMER_NO = '100001'
                  and  (   OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released')           from dual)
                        or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )
                  and  to_char(WANTED_DELIVERY_DATE,'ddmm') = to_char(SYSDATE,'ddmm')
                  and  CUSTOMER_PO_REL_NO is not null
                group by CUSTOMER_PART_NO
                order by CUSTOMER_PART_NO";

            _fullTableHVN = IFS.ExecuteQuery(sql);
            EnrichTable(_fullTableHVN);

            // -------- YMVN --------
            sql = @"
                select sum(BUY_QTY_DUE) as TTCS,
                       0               as SLDAGIAO,
                       0               as SLTONKHO,
                       CUSTOMER_PART_NO
                from   CUSTOMER_ORDER_JOIN
                where  CUSTOMER_NO = '100002'
                  and  (   OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released')           from dual)
                        or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )
                  and  to_char(WANTED_DELIVERY_DATE,'ddmm') = to_char(SYSDATE,'ddmm')
                group by CUSTOMER_PART_NO
                order by CUSTOMER_PART_NO";

            _fullTableYMVN = IFS.ExecuteQuery(sql);
            EnrichTable(_fullTableYMVN);

            // Tính tổng trang
            _totalPagesHVN = CalcTotalPages(_fullTableHVN.Rows.Count);
            _totalPagesYMVN = CalcTotalPages(_fullTableYMVN.Rows.Count);

            _pageHVN = 0;
            _pageYMVN = 0;

            // Bind chart trang đầu
            BindChartHVN();
            BindChartYMVN();

            // Cập nhật label thông tin ngày
            tgxem.Text = "Bạn đang xem dữ liệu ngày: " + DateTime.Now
                       + "  |  Tồn kho hiện tại · Tổng SL xuất trong ngày · SL đã xuất tính đến hiện tại";
        }

        // =====================================================================
        // ENRICH: bổ sung SLTONKHO và SLDAGIAO cho từng dòng
        // =====================================================================
        private void EnrichTable(DataTable tbl)
        {
            string today = DateTime.Now.ToString("MM/dd/yyyy");

            foreach (DataRow row in tbl.Rows)
            {
                string mh = row["CUSTOMER_PART_NO"].ToString();

                // Tồn kho
                string sqlTon = $"select sum(slconlai) from stocktp where PART = '{mh}'";
                string kqTon = SQL.ExecuteReader(SQL.B7R2_FCCdb, sqlTon);
                row["SLTONKHO"] = string.IsNullOrEmpty(kqTon) ? 0 : Convert.ToDecimal(kqTon);

                // Đã giao
                string sqlGiao = $@"select sum(SOLUONG) from luuphieugiaohang
                                    where MAHANG = '{mh}'
                                      and CONVERT(VARCHAR(10),NGAYGIAO,101) = '{today}'";
                string kqGiao = SQL.ExecuteReader(SQL.B7R2_FCCdb, sqlGiao);
                row["SLDAGIAO"] = string.IsNullOrEmpty(kqGiao) ? 0 : Convert.ToDecimal(kqGiao);
            }
        }

        // =====================================================================
        // PAGING HELPERS
        // =====================================================================
        private int CalcTotalPages(int rowCount)
            => rowCount == 0 ? 1 : (int)Math.Ceiling((double)rowCount / PAGE_SIZE);

        // Lấy DataTable của trang hiện tại từ bảng gốc
        private DataTable GetPage(DataTable source, int page)
        {
            int start = page * PAGE_SIZE;
            int count = Math.Min(PAGE_SIZE, source.Rows.Count - start);

            DataTable paged = source.Clone();   // giữ nguyên cấu trúc cột
            for (int i = start; i < start + count; i++)
                paged.ImportRow(source.Rows[i]);

            return paged;
        }

        private void UpdatePageLabel(LabelControl lbl, int page, int total, int rowCount)
        {
            int from = page * PAGE_SIZE + 1;
            int to = Math.Min((page + 1) * PAGE_SIZE, rowCount);
            lbl.Text = $"Trang {page + 1} / {total}   ({from}–{to} của {rowCount} mã)";
        }

        // =====================================================================
        // LAYOUT: thay CharHVN/CharYMVN gốc bằng SplitContainer chứa 2 chart
        // =====================================================================
        private void InitDualChartLayout(
            DevExpress.XtraCharts.ChartControl placeholder,
            out SplitContainer split,
            out DevExpress.XtraCharts.ChartControl chartMain,
            out DevExpress.XtraCharts.ChartControl chartPct)
        {
            var parent = placeholder.Parent;
            var bounds = placeholder.Bounds;
            var dock = placeholder.Dock;
            parent.Controls.Remove(placeholder);

            // SplitContainer chia dọc: trên 70% = main, dưới 30% = % giao
            split = new SplitContainer
            {
                Orientation = Orientation.Horizontal,
                Dock = dock,
                Bounds = bounds,
                SplitterDistance = (int)(bounds.Height * 0.70),
                Panel1MinSize = 120,
                Panel2MinSize = 80,
                BackColor = Color.White,
            };
            split.SplitterWidth = 4;
            parent.Controls.Add(split);

            // ---- Chart trên: Tồn kho & Tổng đơn hàng ----
            chartMain = new DevExpress.XtraCharts.ChartControl { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(chartMain);
            ConfigureMainChart(chartMain);

            // ---- Chart dưới: % Đã giao ----
            chartPct = new DevExpress.XtraCharts.ChartControl { Dock = DockStyle.Fill };
            split.Panel2.Controls.Add(chartPct);
            ConfigurePctChart(chartPct);
        }

        // =====================================================================
        // CHART TRÊN: 2 bar series — Tồn kho (trục phụ) & Tổng đơn hàng (trục chính)
        //             Dùng SecondaryAxisY để 2 series có scale độc lập
        // =====================================================================
        private void ConfigureMainChart(DevExpress.XtraCharts.ChartControl chart)
        {
            chart.Series.Clear();
            chart.BackColor = Color.White;
            chart.BorderOptions.Visible = false;
            chart.Legend.Visibility = DevExpress.Utils.DefaultBoolean.True;
            chart.Legend.AlignmentHorizontal = LegendAlignmentHorizontal.Right;
            chart.Legend.AlignmentVertical = LegendAlignmentVertical.Top;
            chart.Legend.Font = new Font("Tahoma", 7.5f);

            // -- Series 1: Tổng đơn hàng (trục Y chính) --
            var sDon = new Series("Tổng Đơn Hàng", ViewType.Bar);
            sDon.ArgumentDataMember = "CUSTOMER_PART_NO";
            sDon.ValueDataMembers.AddRange(new[] { "TTCS" });
            sDon.Label.Visible = false;
            sDon.ToolTipPointPattern = "Đơn hàng: {V:#,##0}";
            var vDon = (BarSeriesView)sDon.View;
            vDon.Color = Color.FromArgb(226, 75, 74);
            vDon.BarWidth = 0.35;
            vDon.Border.Visible = false;
            chart.Series.Add(sDon);

            // -- Series 2: Tồn kho (trục Y phụ — scale riêng) --
            var sTon = new Series("Tồn Kho", ViewType.Bar);
            sTon.ArgumentDataMember = "CUSTOMER_PART_NO";
            sTon.ValueDataMembers.AddRange(new[] { "SLTONKHO" });
            sTon.Label.Visible = false;
            sTon.ToolTipPointPattern = "Tồn kho: {V:#,##0}";
            var vTon = (BarSeriesView)sTon.View;
            vTon.Color = Color.FromArgb(24, 95, 165);
            vTon.BarWidth = 0.35;
            vTon.Border.Visible = false;
            chart.Series.Add(sTon);

            // Diagram
            var diag = (XYDiagram)chart.Diagram;
            diag.Rotated = true;
            diag.AxisX.QualitativeScaleOptions.AutoGrid = false;
            diag.AxisX.Label.Font = new Font("Tahoma", 7.5f);
            diag.AxisX.Label.TextColor = Color.FromArgb(50, 50, 50);
            diag.AxisX.Tickmarks.Visible = false;
            diag.AxisX.GridLines.Visible = false;

            // Trục Y chính = Đơn hàng
            diag.AxisY.Title.Text = "Đơn hàng";
            diag.AxisY.Title.Visible = false;
            diag.AxisY.Label.Font = new Font("Tahoma", 7f);
            diag.AxisY.Label.TextColor = Color.FromArgb(226, 75, 74);
            diag.AxisY.GridLines.Color = Color.FromArgb(230, 230, 230);
            diag.AxisY.GridLines.Visible = true;
            diag.AxisY.Label.NumericOptions.Format = DevExpress.XtraCharts.NumericFormat.Number;
            diag.AxisY.Label.NumericOptions.Precision = 0;

            // Trục Y phụ = Tồn kho (scale độc lập)
            var axTon = new SecondaryAxisY("axTonKho");
            axTon.Label.Font = new Font("Tahoma", 7f);
            axTon.Label.TextColor = Color.FromArgb(24, 95, 165);
            axTon.GridLines.Visible = false;
            axTon.Label.NumericOptions.Format = DevExpress.XtraCharts.NumericFormat.Number;
            axTon.Label.NumericOptions.Precision = 0;
            diag.SecondaryAxesY.Add(axTon);

            // Gán series Tồn kho vào trục phụ
            ((BarSeriesView)sTon.View).AxisY = axTon;

            // Crosshair
            chart.CrosshairEnabled = DevExpress.Utils.DefaultBoolean.True;
            chart.CrosshairOptions.ShowArgumentLabels = true;
            chart.CrosshairOptions.ShowValueLabels = true;
        }

        // =====================================================================
        // CHART DƯỚI: % Đã giao — bar nằm ngang, scale 0–100%
        //             Màu đổi theo ngưỡng: <50% đỏ, 50–80% cam, >80% xanh
        // =====================================================================
        private void ConfigurePctChart(DevExpress.XtraCharts.ChartControl chart)
        {
            chart.Series.Clear();
            chart.BackColor = Color.White;
            chart.BorderOptions.Visible = false;
            chart.Legend.Visibility = DevExpress.Utils.DefaultBoolean.False;

            var sPct = new Series("% Đã Giao", ViewType.Bar);
            sPct.ArgumentDataMember = "CUSTOMER_PART_NO";
            sPct.ValueDataMembers.AddRange(new[] { "PCT_GIAO" });   // cột tính sẵn
            sPct.Label.Visible = true;
            sPct.Label.TextPattern = "{V:F0}%";
            sPct.Label.Font = new Font("Tahoma", 7f);
            sPct.ToolTipPointPattern = "Đã giao: {V:F1}%";

            var vPct = (BarSeriesView)sPct.View;
            vPct.Color = Color.FromArgb(99, 153, 34);
            vPct.BarWidth = 0.55;
            vPct.Border.Visible = false;
            chart.Series.Add(sPct);

            var diag = (XYDiagram)chart.Diagram;
            diag.Rotated = true;
            diag.AxisX.QualitativeScaleOptions.AutoGrid = false;
            diag.AxisX.Label.Font = new Font("Tahoma", 7.5f);
            diag.AxisX.Label.TextColor = Color.FromArgb(50, 50, 50);
            diag.AxisX.Tickmarks.Visible = false;
            diag.AxisX.GridLines.Visible = false;

            // Trục Y cố định 0–100%
            diag.AxisY.WholeRange.Auto = false;
            diag.AxisY.WholeRange.MinValue = 0;
            diag.AxisY.WholeRange.MaxValue = 100;
            diag.AxisY.VisualRange.Auto = false;
            diag.AxisY.VisualRange.MinValue = 0;
            diag.AxisY.VisualRange.MaxValue = 100;
            diag.AxisY.Label.Font = new Font("Tahoma", 7f);
            diag.AxisY.Label.TextColor = Color.DimGray;
            diag.AxisY.Label.NumericOptions.Format = DevExpress.XtraCharts.NumericFormat.Percent;
            diag.AxisY.Label.NumericOptions.Precision = 0;
            diag.AxisY.GridLines.Color = Color.FromArgb(230, 230, 230);
            diag.AxisY.GridLines.Visible = true;

            // Vùng xanh nhạt ≥80% — ngưỡng an toàn
            var strip = new Strip();
            strip.Color = Color.FromArgb(15, 99, 153, 34);
            strip.MinLimit.AxisValue = 80;
            strip.MaxLimit.AxisValue = 100;
            diag.AxisY.Strips.Add(strip);

            // Đường đứt mốc 80%
            var line80 = new ConstantLine();
            line80.Name = "80%";
            line80.AxisValue = 80;
            line80.Color = Color.FromArgb(99, 153, 34);
            line80.LineStyle.DashStyle = DevExpress.XtraCharts.DashStyle.Dash;
            line80.ShowInLegend = false;
            line80.LegendText = "Mốc 80%";
            diag.AxisY.ConstantLines.Add(line80);

            chart.CrosshairEnabled = DevExpress.Utils.DefaultBoolean.True;
        }

        // =====================================================================
        // TÍNH CỘT PCT_GIAO trước khi bind
        // =====================================================================
        private DataTable AddPctColumn(DataTable src)
        {
            var tbl = src.Copy();
            if (!tbl.Columns.Contains("PCT_GIAO"))
                tbl.Columns.Add("PCT_GIAO", typeof(double));

            foreach (DataRow r in tbl.Rows)
            {
                double don = Convert.ToDouble(r["TTCS"]);
                double giao = Convert.ToDouble(r["SLDAGIAO"]);
                r["PCT_GIAO"] = don > 0 ? Math.Min(Math.Round(giao / don * 100, 1), 100) : 0;
            }
            return tbl;
        }

        // =====================================================================
        // BIND CHART HVN
        // =====================================================================
        private void BindChartHVN()
        {
            var page = AddPctColumn(GetPage(_fullTableHVN, _pageHVN));
            chartHVN_Main.DataSource = page;
            chartHVN_Pct.DataSource = page;
            chartHVN_Main.RefreshData();
            chartHVN_Pct.RefreshData();

            UpdatePageLabel(lblHVN_Page, _pageHVN, _totalPagesHVN, _fullTableHVN.Rows.Count);
            btnHVN_First.Enabled = btnHVN_Prev.Enabled = (_pageHVN > 0);
            btnHVN_Next.Enabled = btnHVN_Last.Enabled = (_pageHVN < _totalPagesHVN - 1);
        }

        // =====================================================================
        // BIND CHART YMVN
        // =====================================================================
        private void BindChartYMVN()
        {
            var page = AddPctColumn(GetPage(_fullTableYMVN, _pageYMVN));
            chartYMVN_Main.DataSource = page;
            chartYMVN_Pct.DataSource = page;
            chartYMVN_Main.RefreshData();
            chartYMVN_Pct.RefreshData();

            UpdatePageLabel(lblYMVN_Page, _pageYMVN, _totalPagesYMVN, _fullTableYMVN.Rows.Count);
            btnYMVN_First.Enabled = btnYMVN_Prev.Enabled = (_pageYMVN > 0);
            btnYMVN_Next.Enabled = btnYMVN_Last.Enabled = (_pageYMVN < _totalPagesYMVN - 1);
        }

        // =====================================================================
        // Nút refresh thủ công (tuỳ chọn — gắn vào ribbon hoặc accordion)
        // =====================================================================
        public void RefreshDashboard()
        {
            _waitForm.Run(() =>
            {
                _fullTableHVN = new DataTable();
                _fullTableYMVN = new DataTable();
                _pageHVN = 0;
                _pageYMVN = 0;
                LoadAllData();
                _dashBar.Refresh();
            }, "Đang tải lại dữ liệu dashboard...");
        }

        // =====================================================================
        // Phần còn lại giữ nguyên từ code gốc của bạn
        // =====================================================================

        // -- 4 handler bị thiếu do Designer.cs vẫn tham chiếu --
        void accordionControl_SelectedElementChanged(object sender, SelectedElementChangedEventArgs e)
        {
            // Giữ nguyên logic gốc — hiện chưa dùng userControl nào
        }

        void barButtonNavigation_ItemClick(object sender, ItemClickEventArgs e)
        {
            accordionControl.SelectedElement = E_NhapTP;
        }

        private void E_Trahang_Click(object sender, EventArgs e)
        => WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);

        private void E_NhapTP_Click(object sender, EventArgs e)
        {
            //NHAP_TP UF_NHAPTP = new NHAP_TP();
            //UF_NHAPTP.Show();
        }

        private void E_NhapTP_0QR_Click(object sender, EventArgs e)
        {
            //NHAPKHOKHONGQRCODE UF_NHAPSP = new NHAPKHOKHONGQRCODE();
            //UF_NHAPSP.Show();
        }
        // ★ SỬA: đi qua Navigator thay vì new HVN_PGH trực tiếp
        private void E_GHHVN_MP_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenGiaoHangHVN("100001");

        private void E_GHYMVN_MP_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenGiaoHangYMVN("MP");

        private void E_GHYMVN_SP_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenGiaoHangYMVN("SP");

        

        private void E_Tracuulotno_Click(object sender, EventArgs e)
        {
            //TruyTimLOTNO UF_TruyTim_LOT = new TruyTimLOTNO();
            //UF_TruyTim_LOT.Show();
        }

        private void E_In_Le_Click(object sender, EventArgs e)
        {
            //UF_TACHLOT f_TACHLOT = new UF_TACHLOT();
            //f_TACHLOT.Show();
        }

        private void E_TKTK_Click(object sender, EventArgs e)
        {
            //TONKHOTP UF_TKTP = new TONKHOTP();
            //UF_TKTP.Show();
        }

        private void accordionControlElement21_Click(object sender, EventArgs e)
        {
            UF_CHANGETIME uF_CHANGETIME = new UF_CHANGETIME();
            uF_CHANGETIME.ShowDialog();
        }

        private void accordionControlElement23_Click(object sender, EventArgs e)
        {
            //FRM_LOTNO_UPDATE_INFOR rM_LOTNO_UPDATE_INFOR = new FRM_LOTNO_UPDATE_INFOR();
            //rM_LOTNO_UPDATE_INFOR.Show();
        }

        

        private void accordionControlElement27_Click(object sender, EventArgs e)
        {
           
        }

        private void accordionControlElement24_Click(object sender, EventArgs e)
        {
            if (hostname != TM_BANQR)
            {
                DialogResult rs = XtraMessageBox.Show(
                    $"Bạn có muốn chuyển máy bắn QRcode từ: {TM_BANQR} sang máy: {hostname}?",
                    "Cảnh Báo!",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (rs == DialogResult.Yes)
                {
                    string sqlChange = $"update tbl_QR_MAY_DOCQR set LichSu = '{TM_BANQR} --> {hostname} : {DateTime.Now}', TT = 0 where TT = 1";
                    SQL.LoadData1(SQL.B7R2_FCCdb, sqlChange);
                    string sqlAdd = $"insert into tbl_QR_MAY_DOCQR(TenMay,LichSu,TT) values ('{hostname}','KO',1)";
                    SQL.LoadData1(SQL.B7R2_FCCdb, sqlAdd);
                    System.Windows.Forms.Application.Restart();
                }
            }
        }

        private void accordionControlElement29_Click(object sender, EventArgs e)
        {
            ComaparePart frm_compare = new ComaparePart();
            frm_compare.ShowDialog();
        }

        private void InGhepLot_Click(object sender, EventArgs e)
        {
            //UF_GHEPLOT fgeplot = new UF_GHEPLOT();
            //fgeplot.ShowDialog();
        }

        private void btHelp_ItemClick(object sender, ItemClickEventArgs e)
        {
            Help.ShowHelp(this, helpProvider1.HelpNamespace);
        }

        private void acrImageControl_Click(object sender, EventArgs e)
        {
            FrmImageControl frm = new FrmImageControl();
            frm.Show();
        }

        private void cmdRackControl_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenBanDoKho(this);

        private void barButtonItem1_ItemClick(object sender, ItemClickEventArgs e)
        {
            //PCTP.REQUEST_LK.RequestLK FRM_RQLK = new PCTP.REQUEST_LK.RequestLK();
            //FRM_RQLK.Show();
        }

        private void barButtonItem2_ItemClick(object sender, ItemClickEventArgs e)
        {
            //PGH_XK Frm_GHXK = new PGH_XK();
            //Frm_GHXK.Show();
        }

        private void accordionControlElement19_Click(object sender, EventArgs e)
        {
            QRCODE_HVN.Report.GHEPLOT report = new QRCODE_HVN.Report.GHEPLOT();
            ReportDesignTool designTool = new ReportDesignTool(report);
            report.DesignerLoaded += report_DesignerLoaded;
            designTool.ShowRibbonDesignerDialog();
        }

        void report_DesignerLoaded(object sender, DesignerLoadedEventArgs e)
        {
            
            splashScreenManager1.ShowWaitForm();
            IToolboxService toolboxService =
                (IToolboxService)e.DesignerHost.GetService(typeof(IToolboxService));
            toolboxService.AddToolboxItem(new ToolboxItem(typeof(XRZipCode)));
            splashScreenManager1.CloseWaitForm();
        }

        // Ticker label (giữ nguyên animation gốc)
        private int tm = 10;
        private void timer1_Tick_1(object sender, EventArgs e)
        {
            tgxem.Left += tm;
            if (tgxem.Left >= 100)
                timer1.Enabled = false;
        }

        private void accordionControlElement30_Click(object sender, EventArgs e)
        {
           
        }

        private void E_GHHVN_SP_Click(object sender, EventArgs e)
           => WarehouseProcessNavigator.OpenGiaoHangHVN("100002");

        private void accordionControlElement36_Click(object sender, EventArgs e)
         => WarehouseProcessNavigator.OpenBanDoKho(this);

        private void HTDelever_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenGiaoHangHVN("100003");

        private void accordionControlElement30_Click_1(object sender, EventArgs e)
     => WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);

        private void accordionControlElement37_Click(object sender, EventArgs e)
    => WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);
        // Designer, cần thêm 1 AccordionControlElement mới trỏ tới handler này.
        // ── SỬA: nút cũ trỏ tới OpenQCDuyet (không tồn tại) → tách theo đúng mốc ─────
        private void accordionControlElement_QCDinhHuong_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenQCDinhHuong(this);

        private void accordionControlElement_QCXacNhanCuoi_Click(object sender, EventArgs e)
            => WarehouseProcessNavigator.OpenQCXacNhanCuoi(this);
    }

    // =========================================================================
    // HTMLHelpClass giữ nguyên
    // =========================================================================
    public class HTMLHelpClass
    {
        private static string HelpNamespaceValue;
        public static string HelpNamespace
        {
            get => HelpNamespaceValue;
            set => HelpNamespaceValue = value;
        }

        public static string GetLocalHelpFileName(string FileName)
        {
            string ExeName = Application.ExecutablePath;
            string DirName = System.IO.Path.GetDirectoryName(ExeName);
            return DirName + @"\" + FileName;
        }
    }

}