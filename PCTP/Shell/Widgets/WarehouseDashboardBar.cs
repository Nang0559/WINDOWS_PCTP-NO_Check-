using DevExpress.XtraEditors;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// Real-time warehouse KPI bar. It only consumes existing read repositories
    /// and delegates navigation to the Shell process navigator.
    /// </summary>
    public sealed class WarehouseDashboardBar : XtraUserControl
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly INhapKhoDashboardRepository _dashRepo;
        private readonly Action _openNhapKho;

        private LabelControl _lblChoDinhHuong;
        private LabelControl _lblChoQC;
        private LabelControl _lblDaDuyetChuaTra;
        private LabelControl _lblLechA0;

        public WarehouseDashboardBar(
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            INhapKhoDashboardRepository dashRepo,
            Action openNhapKho)
        {
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException("phieuXuLyRepo");
            _dashRepo = dashRepo ?? throw new ArgumentNullException("dashRepo");
            _openNhapKho = openNhapKho ?? throw new ArgumentNullException("openNhapKho");

            Dock = DockStyle.Top;
            Height = 42;
            BuildUI();
        }

        private void BuildUI()
        {
            PanelControl panel = new PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 8, 0, 0),
                Margin = Padding.Empty
            };

            _lblChoDinhHuong = MakeLabel("🟡 QC chờ định hướng: --");
            _lblChoDinhHuong.Click += delegate { WarehouseProcessNavigator.OpenQCDinhHuong(this); };

            _lblChoQC = MakeLabel("🔴 QC chờ duyệt cuối: --");
            _lblChoQC.Click += delegate { WarehouseProcessNavigator.OpenQCXacNhanCuoi(this); };

            _lblDaDuyetChuaTra = MakeLabel("🔄 Đã duyệt chờ trả SX: --");
            _lblDaDuyetChuaTra.Click += delegate { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); };

            _lblLechA0 = MakeLabel("⚠ Lệch đối chiếu A0: --");
            _lblLechA0.Click += delegate { _openNhapKho(); };

            flow.Controls.Add(_lblChoDinhHuong);
            flow.Controls.Add(_lblChoQC);
            flow.Controls.Add(_lblDaDuyetChuaTra);
            flow.Controls.Add(_lblLechA0);
            panel.Controls.Add(flow);
            Controls.Add(panel);

            Refresh();
        }

        /// <summary>
        /// Refreshes all dashboard KPIs from the injected read repositories.
        /// Kept public so Main_APP and the Control Center can refresh the widget.
        /// </summary>
        public void Refresh()
        {
            if (IsDisposed)
                return;

            try
            {
                int choDinhHuong = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaTaoPhieuBatThuong);
                int choQCCuoi = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaGiaoSanXuat);
                int daDuyetChuaTra = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaQCXacNhanCuoi);
                int lech = _dashRepo.DemLechDoiChieu();

                SetItem(_lblChoDinhHuong, "🟡 QC chờ định hướng: " + choDinhHuong, choDinhHuong > 0 ? Color.DarkOrange : Color.SeaGreen);
                SetItem(_lblChoQC, "🔴 QC chờ duyệt cuối: " + choQCCuoi, choQCCuoi > 0 ? Color.Crimson : Color.SeaGreen);
                SetItem(_lblDaDuyetChuaTra, "🔄 Đã duyệt chờ trả SX: " + daDuyetChuaTra, daDuyetChuaTra > 0 ? Color.DarkOrange : Color.SeaGreen);
                SetItem(_lblLechA0, lech > 0 ? "⚠ Lệch đối chiếu A0: " + lech : "✅ Không lệch đối chiếu A0", lech > 0 ? Color.Red : Color.SeaGreen);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[WarehouseDashboardBar] Refresh lỗi: " + ex.Message);
            }
        }

        private static LabelControl MakeLabel(string text)
        {
            return new LabelControl
            {
                Text = text,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 30, 0),
                Appearance = { Font = new Font("Tahoma", 9.5F, FontStyle.Bold) }
            };
        }

        private static void SetItem(LabelControl label, string text, Color color)
        {
            label.Text = text;
            label.Appearance.ForeColor = color;
        }
    }
}
