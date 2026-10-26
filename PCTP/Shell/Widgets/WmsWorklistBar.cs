using DevExpress.XtraEditors;
using PCTP.Common;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Repository;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// WMS worklist for actionable workflow states.
    /// MainApp only exposes navigation; business validation remains in module services.
    /// </summary>
    public sealed class WmsWorklistBar : XtraUserControl
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly INhapKhoDashboardRepository _nhapKhoRepo;
        private readonly Action _openNhapKho;

        private LabelControl _lblChoDinhHuong;
        private LabelControl _lblChoQCCuoi;
        private LabelControl _lblChoRework;
        private LabelControl _lblChoQCRework;
        private LabelControl _lblChoGiaoBu;
        private LabelControl _lblChoNhapLai;
        private LabelControl _lblChoNhap;
        private LabelControl _lblLechA0;

        public WmsWorklistBar(
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            INhapKhoDashboardRepository nhapKhoRepo,
            Action openNhapKho)
        {
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException("phieuXuLyRepo");
            _nhapKhoRepo = nhapKhoRepo ?? throw new ArgumentNullException("nhapKhoRepo");
            _openNhapKho = openNhapKho ?? throw new ArgumentNullException("openNhapKho");

            Dock = DockStyle.Top;
            Height = 64;
            BuildUi();
        }

        private void BuildUi()
        {
            PanelControl panel = new PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                Padding = new Padding(12, 5, 12, 3)
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            LabelControl title = new LabelControl
            {
                Text = "VIỆC CẦN XỬ LÝ",
                AutoSize = true,
                Margin = new Padding(0, 7, 18, 0),
                Appearance = { Font = new Font("Tahoma", 9.5F, FontStyle.Bold) }
            };

            _lblChoDinhHuong = MakeItem("🟡 QC chờ định hướng: --");
            _lblChoDinhHuong.Click += delegate { WarehouseProcessNavigator.OpenQCDinhHuong(this); };

            _lblChoQCCuoi = MakeItem("🔴 QC chờ duyệt cuối: --");
            _lblChoQCCuoi.Click += delegate { WarehouseProcessNavigator.OpenQCXacNhanCuoi(this); };

            _lblChoRework = MakeItem("🛠 Chờ xuất Rework: --");
            _lblChoRework.Click += delegate { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); };

            _lblChoQCRework = MakeItem("🔎 Chờ QC Rework: --");
            _lblChoQCRework.Click += delegate { WarehouseProcessNavigator.OpenQCXacNhanCuoi(this); };

            _lblChoGiaoBu = MakeItem("🚚 Chờ giao bù: --");
            _lblChoGiaoBu.Click += delegate { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); };

            _lblChoNhapLai = MakeItem("📦 Chờ nhập lại kho: --");
            _lblChoNhapLai.Click += delegate { _openNhapKho(); };

            _lblChoNhap = MakeItem("📥 Phiếu chờ nhập: --");
            _lblChoNhap.Click += delegate { _openNhapKho(); };

            _lblLechA0 = MakeItem("⚠ Lệch đối chiếu A0: --");
            _lblLechA0.Click += delegate { _openNhapKho(); };

            flow.Controls.Add(title);
            flow.Controls.Add(_lblChoDinhHuong);
            flow.Controls.Add(_lblChoQCCuoi);
            flow.Controls.Add(_lblChoRework);
            flow.Controls.Add(_lblChoQCRework);
            flow.Controls.Add(_lblChoGiaoBu);
            flow.Controls.Add(_lblChoNhapLai);
            flow.Controls.Add(_lblChoNhap);
            flow.Controls.Add(_lblLechA0);

            panel.Controls.Add(flow);
            Controls.Add(panel);
            RefreshWorklist();
        }

        private LabelControl MakeItem(string text)
        {
            return new LabelControl
            {
                Text = text,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 7, 22, 0),
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Bold) }
            };
        }

        public void RefreshWorklist()
        {
            if (IsDisposed)
                return;

            try
            {
                int choDinhHuong = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaTaoPhieuBatThuong);
                int choQCCuoi = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaGiaoSanXuat);
                int choRework = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaDinhHuong);
                int choQCRework = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaGiaoSanXuat);
                int choGiaoBu = _phieuXuLyRepo.CountByStatus(QTChungStatus.ChoGiaoBu);
                int choNhapLai = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaQCXacNhanCuoi);
                int choNhap = _nhapKhoRepo.DemPhieuChoNhap();
                int lechA0 = _nhapKhoRepo.DemLechDoiChieu();

                SetItem(_lblChoDinhHuong, "🟡 QC chờ định hướng: " + choDinhHuong, choDinhHuong);
                SetItem(_lblChoQCCuoi, "🔴 QC chờ duyệt cuối: " + choQCCuoi, choQCCuoi);
                SetItem(_lblChoRework, "🛠 Chờ xuất Rework: " + choRework, choRework);
                SetItem(_lblChoQCRework, "🔎 Chờ QC Rework: " + choQCRework, choQCRework);
                SetItem(_lblChoGiaoBu, "🚚 Chờ giao bù: " + choGiaoBu, choGiaoBu);
                SetItem(_lblChoNhapLai, "📦 Chờ nhập lại kho: " + choNhapLai, choNhapLai);
                SetItem(_lblChoNhap, "📥 Phiếu chờ nhập: " + choNhap, choNhap);
                SetItem(_lblLechA0, lechA0 > 0 ? "⚠ Lệch đối chiếu A0: " + lechA0 : "✅ Không lệch đối chiếu A0", lechA0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[WmsWorklistBar] RefreshWorklist lỗi: " + ex.Message);
            }
        }

        private static void SetItem(LabelControl label, string text, int count)
        {
            label.Text = text;
            label.Appearance.ForeColor = count > 0 ? Color.DarkOrange : Color.SeaGreen;
        }
    }
}