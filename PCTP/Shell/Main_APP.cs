using DevExpress.XtraBars;
using DevExpress.Utils.Svg;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.UserDesigner;
using PCTP.Acess_Image;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Modules.KhoVatLy;
using PCTP.QRCODE_HVN.ComaprePart;
using PCTP.Shared.Helpers;
using PCTP.Shell.Composition;
using PCTP.Shell.Help;
using PCTP.Shell.Services;
using PCTP.Shell.Widgets;
using PCTP.VIEWSTOCK;
using System;
using System.Drawing.Design;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PCTP
{
    public partial class Main_APP : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        public static string hostname = string.Empty;
        public static string TM_BANQR = string.Empty;

        private readonly IWaitFormService _waitForm;
        private readonly QrMachineSwitchService _qrMachineSwitch;
        private readonly WmsHelpService _wmsHelp;
        private MainAppDashboardController _dashboard;
        private WarehouseDashboardBar _dashboardBar;
        private WmsWorklistBar _worklistBar;
        private MainAppDashboardFactory _dashboardFactory;
        private WmsControlCenterBar _controlCenter;
        private ShellSystemInfoRibbon _systemInfo;

        public Main_APP()
        {
            InitializeComponent();
            _waitForm = new WaitFormService(this);
            _qrMachineSwitch = new QrMachineSwitchService();
            _wmsHelp = new WmsHelpService();
            EnsureShellNavigation();
            accordionControl.SelectedElement = NHAccordionControlElement;
        }

        private void EnsureShellNavigation()
        {
            if (accordionControl.Elements.Count == 0)
                accordionControl.Elements.Add(mainAccordionGroup);

            accordionControl.Dock = DockStyle.Left;
            ribbonControl.Dock = DockStyle.Top;
            ribbonStatusBar.Dock = DockStyle.Bottom;

            // Main_APP is only the Shell.  Keep the HangLoi workflow entry points
            // together and let WarehouseProcessNavigator own the composition.
            NormalizeHangLoiNavigation();

            // Menu chuẩn: Nhập Kho → Kho → Giao Hàng → Xử lý hàng lỗi → Tra cứu & Báo cáo → Công cụ.
            BuildStandardMenus();
            ApplyFluentMenuIcons();
        }

        // Fluent UI-inspired outline SVG icons; generated in-memory to avoid external image dependencies.
        private static SvgImage CreateFluentIcon(string pathData)
        {
            string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"20\" viewBox=\"0 0 24 24\">" +
                         "<path d=\"" + pathData + "\" fill=\"none\" stroke=\"#64748B\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg)))
                return SvgImage.FromStream(stream);
        }

        private static void SetFluentIcon(AccordionControlElement element, string pathData)
        {
            if (element != null)
                element.ImageOptions.SvgImage = CreateFluentIcon(pathData);
        }

        private void ApplyFluentMenuIcons()
        {
            // Nhập kho & QC
            SetFluentIcon(NHAccordionControlElement, "M3 7.5 12 3l9 4.5-9 4.5-9-4.5Z M3 12l9 4.5 9-4.5 M3 16.5 12 21l9-4.5");
            SetFluentIcon(E_NhapTP, "M4 7.5 12 3l8 4.5v9L12 21l-8-4.5v-9Z M4 7.5l8 4.5 8-4.5 M12 12v9");
            SetFluentIcon(E_NhapTP_0QR, "M4 4h16v16H4z M8 8h3v3H8z M14 8h2 M14 12h2 M8 14h8");
            SetFluentIcon(accordionControlElement_QCDinhHuong, "M12 3 20 6v5c0 5-3.4 8.2-8 10-4.6-1.8-8-5-8-10V6l8-3Z M8.5 12l2.2 2.2 4.8-5");
            SetFluentIcon(accordionControlElement_QCXacNhanCuoi, "M12 3 20 6v5c0 5-3.4 8.2-8 10-4.6-1.8-8-5-8-10V6l8-3Z M8.5 12l2.2 2.2 4.8-5");
            // Xử lý hàng lỗi
            SetFluentIcon(E_Trahang, "M12 3 21 19H3L12 3Z M12 9v4 M12 16.5h.01");
            SetFluentIcon(trahangngPD, "M7 3h8l4 4v14H7z M15 3v5h4 M10 12h6 M10 16h6");
            SetFluentIcon(trahangndHVN, "M4 5h16 M4 12h16 M4 19h16 M8 3v4 M8 10v4 M8 17v4");
            // Công cụ
            SetFluentIcon(accordionControlElement20, "M14 6a5 5 0 0 0-6.5 6.5L3 17l4 4 4.5-4.5A5 5 0 0 0 18 10l-3 3-4-4 3-3Z");
            SetFluentIcon(accordionControlElement21, "M12 7v5l3 2 M12 3a9 9 0 1 0 9 9 9 9 0 0 0-9-9Z");
            SetFluentIcon(accordionControlElement19, "M4 4h16v16H4z M8 8h8 M8 12h8 M8 16h5");
            SetFluentIcon(accordionControlElement24, "M5 5h5v5H5z M14 14h5v5h-5z M10 7h4a3 3 0 0 1 3 3v4 M14 17h-4a3 3 0 0 1-3-3v-4");
            SetFluentIcon(accordionControlElement29, "M4 5h16v14H4z M8 9h3 M8 13h8 M14 8l2 2-2 2");
            SetFluentIcon(acrImageControl, "M4 5h16v14H4z M4 16l5-5 4 4 3-3 4 4 M9 9h.01");
            SetFluentIcon(cmdRackControl, "M3 21V8l9-5 9 5v13 M3 8h18 M8 21v-7h8v7 M8 10h.01 M16 10h.01");
            SetFluentIcon(accordionControlElement38, "M5 4h14v16H5z M8 8h8 M8 12h8 M8 16h5");
            SetFluentIcon(accordionControlElement30, "M12 3 21 19H3L12 3Z M12 9v4 M12 16.5h.01");
            // Giao hàng
            SetFluentIcon(accordionControlElement22, "M3 7h11v10H3z M14 10h4l3 3v4h-7z M7 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z M18 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z");
            SetFluentIcon(E_GHHVN_MP, "M3 7h11v10H3z M14 10h4l3 3v4h-7z M7 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z M18 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z");
            SetFluentIcon(E_GHHVN_SP, "M3 7h11v10H3z M14 10h4l3 3v4h-7z M7 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z M18 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z");
            SetFluentIcon(HTDelever, "M3 7h11v10H3z M14 10h4l3 3v4h-7z M7 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z M18 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z");
            SetFluentIcon(accordionControlElement36, "M3 21V8l9-5 9 5v13 M3 8h18 M8 21v-7h8v7");
            SetFluentIcon(accordionControlElement37, "M4 5h16 M4 12h16 M4 19h16 M8 3v4 M8 10v4 M8 17v4");
            // Tra cứu & báo cáo
            SetFluentIcon(accordionControlElement28, "M10 3a7 7 0 1 0 0 14 7 7 0 0 0 0-14Z M15 15l6 6");
            SetFluentIcon(E_Tracuulotno, "M10 3a7 7 0 1 0 0 14 7 7 0 0 0 0-14Z M15 15l6 6 M7 10h6");
            SetFluentIcon(E_TKTK, "M4 20V10 M10 20V4 M16 20v-7 M22 20H2");
        }

        private void NormalizeHangLoiNavigation()
        {
            // The designer historically placed the QC entries under "Nhập Kho & QC".
            // They belong to the HangLoi workflow and must live under one business area.
            NHAccordionControlElement.Elements.Remove(accordionControlElement_QCDinhHuong);
            NHAccordionControlElement.Elements.Remove(accordionControlElement_QCXacNhanCuoi);

            if (!E_Trahang.Elements.Contains(accordionControlElement_QCDinhHuong))
                E_Trahang.Elements.Add(accordionControlElement_QCDinhHuong);
            if (!E_Trahang.Elements.Contains(accordionControlElement_QCXacNhanCuoi))
                E_Trahang.Elements.Add(accordionControlElement_QCXacNhanCuoi);

            // E_Trahang is a group, not an action. The old designer handler opened
            // the workflow when the group itself was clicked, which is ambiguous.
            E_Trahang.Click -= E_Trahang_Click;

            // Both legacy child entries now route through the single workflow screen.
            // The workflow screen itself determines the active step from the ticket state.
            trahangngPD.Click -= trahangngPD_Click;
            trahangngPD.Click += trahangngPD_Click;
            trahangndHVN.Click -= trahangndHVN_Click;
            trahangndHVN.Click += trahangndHVN_Click;

            // Remove duplicate legacy entry points.  Keep the controls in the
            // designer for backward compatibility, but do not expose them twice.
            accordionControlElement30.Visible = false;
            accordionControlElement37.Visible = false;
        }

        private void Main_APP_Load(object sender, EventArgs e)
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle("Caramel");
            InitializeShellComposition();
            _systemInfo = new ShellSystemInfoRibbon(ribbonControl, ribbonPage);
            InitializeDashboard();
            RefreshSystemInfo();
            InstallQcUiBridgeIfRequested(); // test-only; no-op unless PCTP_QC_UI_BRIDGE=1
        }

        private void InitializeShellComposition()
        {
            _dashboardFactory = new MainAppDashboardFactory();
            _dashboardBar = _dashboardFactory.Create(OpenNhapKhoFromDashboard);
            _worklistBar = _dashboardFactory.CreateWorklist(OpenNhapKhoFromDashboard);
            _controlCenter = new WmsControlCenterBar(_wmsHelp);
            _controlCenter.RefreshRequested += ControlCenter_RefreshRequested;

            Controls.Add(_dashboardBar);
            Controls.Add(_worklistBar);
            Controls.Add(_controlCenter);

            _dashboardBar.BringToFront();
            _worklistBar.BringToFront();
            _controlCenter.BringToFront();
        }

        private void ControlCenter_RefreshRequested(object sender, EventArgs e)
        {
            RefreshDashboard();
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
            // Mở thẳng màn hình tiến trình nhập kho — không dựng/hiện bản đồ kho (MainStockSV).
            WarehouseProcessNavigator.OpenNhapKhoTienTrinh(this);
        }

        // Máy đang chạy / máy được bắn QR / DB đang thao tác — hiển thị trên Ribbon.
        private void RefreshSystemInfo()
        {
            if (_systemInfo != null)
                _systemInfo.Update(hostname, TM_BANQR);
        }

        public void RefreshDashboard()
        {
            if (_dashboard != null)
                _dashboard.Refresh();
            RefreshSystemInfo();
            if (_dashboardBar != null)
                _dashboardBar.Refresh();
            if (_worklistBar != null)
                _worklistBar.RefreshWorklist();
        }

        private void accordionControl_SelectedElementChanged(object sender, SelectedElementChangedEventArgs e) { }

        private void barButtonNavigation_ItemClick(object sender, ItemClickEventArgs e)
        {
            accordionControl.SelectedElement = E_NhapTP;
        }

        private void E_Trahang_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); }
        private void trahangngPD_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); }
        private void trahangndHVN_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); }
        private void E_NhapTP_Click(object sender, EventArgs e) { OpenNhapThanhPhamQr(); }
        private void E_NhapTP_0QR_Click(object sender, EventArgs e) { }
        private void E_GHHVN_MP_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenGiaoHangHVN("100001"); }
        private void E_GHHVN_SP_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenGiaoHangHVN("100002"); }
        private void HTDelever_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenGiaoHangHVN("100003"); }
        private void E_Tracuulotno_Click(object sender, EventArgs e) { OpenTraCuuLot(); }
        private void E_In_Le_Click(object sender, EventArgs e) { }
        private void E_TKTK_Click(object sender, EventArgs e) { OpenBaoCaoTongHop(); }

        private void accordionControlElement21_Click(object sender, EventArgs e)
        {
            UF_CHANGETIME form = new UF_CHANGETIME();
            form.ShowDialog(this);
        }

        private void accordionControlElement23_Click(object sender, EventArgs e) { }
        private void accordionControlElement27_Click(object sender, EventArgs e) { }

        private void accordionControlElement24_Click(object sender, EventArgs e)
        {
            if (string.Equals(hostname, TM_BANQR, StringComparison.OrdinalIgnoreCase)) return;
            DialogResult result = XtraMessageBox.Show(
                string.Format("Bạn có muốn chuyển máy bắn QRcode từ: {0} sang máy: {1}?", TM_BANQR, hostname),
                "Cảnh Báo!", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
            _qrMachineSwitch.Switch(TM_BANQR, hostname);
            Application.Restart();
        }

        private void accordionControlElement29_Click(object sender, EventArgs e)
        {
            using (ComaparePart form = new ComaparePart())
            {
                form.ShowDialog(this);
            }
        }

        private void InGhepLot_Click(object sender, EventArgs e) { }

        private void btHelp_ItemClick(object sender, ItemClickEventArgs e)
        {
            _wmsHelp.Show(this, WmsHelpContext.Resolve(this));
        }

        private void acrImageControl_Click(object sender, EventArgs e)
        {
            FrmImageControl form = new FrmImageControl();
            form.Show(this);
        }

        private void cmdRackControl_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenBanDoKho(this); }

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
                    IToolboxService toolbox = (IToolboxService)e.DesignerHost.GetService(typeof(IToolboxService));
                    if (toolbox != null) toolbox.AddToolboxItem(new ToolboxItem(typeof(XRZipCode)));
                },
                "Đang khởi tạo Report Designer...");
        }

        private int _tickerStep = 10;

        private void timer1_Tick_1(object sender, EventArgs e)
        {
            tgxem.Left += _tickerStep;
            if (tgxem.Left >= 100) timer1.Enabled = false;
        }

        private void accordionControlElement36_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenBanDoKho(this); }
        private void accordionControlElement30_Click_1(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); }

        private void accordionControlElement38_Click(object sender, EventArgs e)
        {
            FormItemFifoConfig form = new FormItemFifoConfig();
            form.Show(this);
        }

        private void accordionControlElement37_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); }
        private void accordionControlElement_QCDinhHuong_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQCDinhHuong(this); }
        private void accordionControlElement_QCXacNhanCuoi_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenQCXacNhanCuoi(this); }
    }

    public static class HTMLHelpClass
    {
        private static string _helpNamespace;
        public static string HelpNamespace { get { return _helpNamespace; } set { _helpNamespace = value; } }
        public static string GetLocalHelpFileName(string fileName)
        {
            string exeName = Application.ExecutablePath;
            string directory = System.IO.Path.GetDirectoryName(exeName);
            return System.IO.Path.Combine(directory ?? string.Empty, fileName);
        }
    }
}