using DevExpress.XtraBars;
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
            InitializeDashboard();
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
        private void E_NhapTP_Click(object sender, EventArgs e) { }
        private void E_NhapTP_0QR_Click(object sender, EventArgs e) { }
        private void E_GHHVN_MP_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenGiaoHangHVN("100001"); }
        private void E_GHHVN_SP_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenGiaoHangHVN("100002"); }
        private void HTDelever_Click(object sender, EventArgs e) { WarehouseProcessNavigator.OpenGiaoHangHVN("100003"); }
        private void E_Tracuulotno_Click(object sender, EventArgs e) { }
        private void E_In_Le_Click(object sender, EventArgs e) { }
        private void E_TKTK_Click(object sender, EventArgs e) { }

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
            ComaparePart form = new ComaparePart();
            form.ShowDialog(this);
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
