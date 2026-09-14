using DevExpress.XtraBars;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.UserDesigner;
using PCTP.Acess_Image;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.QRCODE_HVN.ComaprePart;
using PCTP.Shell.Composition;
using PCTP.Shell.Widgets;
using PCTP.VIEWSTOCK;
using System;
using System.Drawing.Design;
using System.Windows.Forms;

namespace PCTP
{
    /// <summary>
    /// Application shell.
    ///
    /// Responsibilities are intentionally limited to:
    /// - hosting the Designer-generated navigation UI;
    /// - wiring navigation commands to module navigators;
    /// - hosting the read-only dashboard controller;
    /// - keeping legacy Designer event handlers alive during migration.
    ///
    /// Reporting/query logic must live in Modules.BaoCao and must not be added here.
    /// Database/repository composition is delegated to Shell.Composition.
    /// </summary>
    public partial class Main_APP : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        public static string hostname = string.Empty;
        public static string TM_BANQR = string.Empty;

        private readonly IWaitFormService _waitForm;
        private MainAppDashboardController _dashboard;
        private WarehouseDashboardBar _dashboardBar;
        private MainAppDashboardFactory _dashboardFactory;

        public Main_APP()
        {
            InitializeComponent();
            _waitForm = new WaitFormService(this);
            accordionControl.SelectedElement = NHAccordionControlElement;
        }

        private void Main_APP_Load(object sender, EventArgs e)
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle("Caramel");
            InitializeShellComposition();
            InitializeDashboard();
        }

        private void InitializeShellComposition()
        {
            _dashboardFactory = new MainAppDashboardFactory();
            _dashboardBar = _dashboardFactory.Create(OpenNhapKhoFromDashboard);

            Controls.Add(_dashboardBar);
            _dashboardBar.BringToFront();
        }

        private void InitializeDashboard()
        {
            _dashboard = new MainAppDashboardController(
                _waitForm,
                CharHVN,
                CharYMVN,
                lblHostName,
                lblMayBan,
                tgxem);

            _dashboard.Initialize();
        }

        private void OpenNhapKhoFromDashboard()
        {
            MainStockSV mainStock = new MainStockSV();
            mainStock.Show();
            WarehouseProcessNavigator.OpenNhapKhoTienTrinh(this, mainStock);
        }

        public void RefreshDashboard()
        {
            if (_dashboard != null)
                _dashboard.Refresh();

            if (_dashboardBar != null)
                _dashboardBar.Refresh();
        }

        // -----------------------------------------------------------------
        // Designer compatibility / navigation handlers
        // -----------------------------------------------------------------

        private void accordionControl_SelectedElementChanged(object sender, SelectedElementChangedEventArgs e)
        {
            // Navigation is handled by the individual element click handlers.
        }

        private void barButtonNavigation_ItemClick(object sender, ItemClickEventArgs e)
        {
            accordionControl.SelectedElement = E_NhapTP;
        }

        private void E_Trahang_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);
        }

        private void E_NhapTP_Click(object sender, EventArgs e)
        {
            // Kept for Designer compatibility. The receiving flow is opened by its module entry point.
        }

        private void E_NhapTP_0QR_Click(object sender, EventArgs e)
        {
            // Kept for Designer compatibility. The non-QR receiving flow is currently disabled.
        }

        private void E_GHHVN_MP_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenGiaoHangHVN("100001");
        }

        private void E_GHHVN_SP_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenGiaoHangHVN("100002");
        }

        private void E_GHYMVN_MP_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenGiaoHangYMVN("MP");
        }

        private void E_GHYMVN_SP_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenGiaoHangYMVN("SP");
        }

        private void HTDelever_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenGiaoHangHVN("100003");
        }

        private void E_Tracuulotno_Click(object sender, EventArgs e)
        {
            // Legacy LOT lookup is now part of BaoCao and must not be reopened here.
        }

        private void E_In_Le_Click(object sender, EventArgs e)
        {
            // Legacy split-LOT screen intentionally disabled during migration.
        }

        private void E_TKTK_Click(object sender, EventArgs e)
        {
            // Legacy stock lookup is now part of BaoCao.
        }

        private void accordionControlElement21_Click(object sender, EventArgs e)
        {
            UF_CHANGETIME form = new UF_CHANGETIME();
            form.ShowDialog(this);
        }

        private void accordionControlElement23_Click(object sender, EventArgs e)
        {
            // Legacy LOT information editor intentionally disabled during migration.
        }

        private void accordionControlElement27_Click(object sender, EventArgs e)
        {
            // Reserved Designer handler.
        }

        private void accordionControlElement24_Click(object sender, EventArgs e)
        {
            if (string.Equals(hostname, TM_BANQR, StringComparison.OrdinalIgnoreCase))
                return;

            DialogResult result = XtraMessageBox.Show(
                string.Format(
                    "Bạn có muốn chuyển máy bắn QRcode từ: {0} sang máy: {1}?",
                    TM_BANQR,
                    hostname),
                "Cảnh Báo!",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            string history = string.Format("{0} --> {1} : {2}", TM_BANQR, hostname, DateTime.Now);
            string safeHistory = history.Replace("'", "''");
            string safeHostname = hostname.Replace("'", "''");

            string updateSql =
                "update tbl_QR_MAY_DOCQR set LichSu = '" + safeHistory + "', TT = 0 where TT = 1";
            string insertSql =
                "insert into tbl_QR_MAY_DOCQR(TenMay,LichSu,TT) values ('" + safeHostname + "','KO',1)";

            ClassSQL.SQLPROVIDER sql = new ClassSQL.SQLPROVIDER();
            sql.LoadData1(sql.B7R2_FCCdb, updateSql);
            sql.LoadData1(sql.B7R2_FCCdb, insertSql);
            Application.Restart();
        }

        private void accordionControlElement29_Click(object sender, EventArgs e)
        {
            ComaparePart form = new ComaparePart();
            form.ShowDialog(this);
        }

        private void InGhepLot_Click(object sender, EventArgs e)
        {
            // Legacy LOT merge screen intentionally disabled during migration.
        }

        private void btHelp_ItemClick(object sender, ItemClickEventArgs e)
        {
            Help.ShowHelp(this, helpProvider1.HelpNamespace);
        }

        private void acrImageControl_Click(object sender, EventArgs e)
        {
            FrmImageControl form = new FrmImageControl();
            form.Show(this);
        }

        private void cmdRackControl_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenBanDoKho(this);
        }

        private void barButtonItem1_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Legacy request screen intentionally disabled during migration.
        }

        private void barButtonItem2_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Legacy export-delivery screen intentionally disabled during migration.
        }

        private void accordionControlElement19_Click(object sender, EventArgs e)
        {
            QRCODE_HVN.Report.GHEPLOT report = new QRCODE_HVN.Report.GHEPLOT();
            ReportDesignTool designTool = new ReportDesignTool(report);
            report.DesignerLoaded += report_DesignerLoaded;
            designTool.ShowRibbonDesignerDialog();
        }

        private void report_DesignerLoaded(object sender, DesignerLoadedEventArgs e)
        {
            _waitForm.Run(
                () =>
                {
                    IToolboxService toolbox =
                        (IToolboxService)e.DesignerHost.GetService(typeof(IToolboxService));
                    if (toolbox != null)
                        toolbox.AddToolboxItem(new ToolboxItem(typeof(XRZipCode)));
                },
                "Đang khởi tạo Report Designer...");
        }

        private int _tickerStep = 10;

        private void timer1_Tick_1(object sender, EventArgs e)
        {
            tgxem.Left += _tickerStep;
            if (tgxem.Left >= 100)
                timer1.Enabled = false;
        }

        private void accordionControlElement30_Click(object sender, EventArgs e)
        {
            // Reserved Designer handler.
        }

        private void accordionControlElement36_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenBanDoKho(this);
        }

        private void accordionControlElement30_Click_1(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);
        }

        private void accordionControlElement38_Click(object sender, EventArgs e)
        {
            FormItemFifoConfig form = new FormItemFifoConfig();
            form.Show(this);
        }

        private void accordionControlElement37_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);
        }

        private void accordionControlElement_QCDinhHuong_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenQCDinhHuong(this);
        }

        private void accordionControlElement_QCXacNhanCuoi_Click(object sender, EventArgs e)
        {
            WarehouseProcessNavigator.OpenQCXacNhanCuoi(this);
        }
    }

    /// <summary>
    /// Backward-compatible holder for the application's local help path.
    /// </summary>
    public static class HTMLHelpClass
    {
        private static string _helpNamespace;

        public static string HelpNamespace
        {
            get { return _helpNamespace; }
            set { _helpNamespace = value; }
        }

        public static string GetLocalHelpFileName(string fileName)
        {
            string exeName = Application.ExecutablePath;
            string directory = System.IO.Path.GetDirectoryName(exeName);
            return System.IO.Path.Combine(directory ?? string.Empty, fileName);
        }
    }
}
