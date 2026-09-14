using DevExpress.XtraEditors;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// WMS worklist based only on existing transactional/query sources.
    /// This control intentionally does not own business logic or invent KPI data.
    /// </summary>
    public sealed class WmsWorklistBar : XtraUserControl
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly INhapKhoDashboardRepository _nhapKhoRepo;
        private readonly Action _openNhapKho;

        private LabelControl _lblChoDinhHuong;
        private LabelControl _lblChoQCCuoi;
        private LabelControl _lblDaDuyetChuaTra;
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
            Height = 44;
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

            _lblDaDuyetChuaTra = MakeItem("🔄 Đã duyệt chờ trả SX: --");
            _lblDaDuyetChuaTra.Click += delegate { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); };

            _lblChoNhap = MakeItem("📥 Phiếu chờ nhập: --");
            _lblChoNhap.Click += delegate { _openNhapKho(); };

            _lblLechA0 = MakeItem("⚠ Lệch đối chiếu A0: --");
            _lblLechA0.Click += delegate { _openNhapKho(); };

            flow.Controls.Add(title);
            flow.Controls.Add(_lblChoDinhHuong);
            flow.Controls.Add(_lblChoQCCuoi);
            flow.Controls.Add(_lblDaDuyetChuaTra);
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
                int daDuyetChuaTra = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaQCXacNhanCuoi);
                int choNhap = _nhapKhoRepo.DemPhieuChoNhap();
                int lechA0 = _nhapKhoRepo.DemLechDoiChieu();

                SetItem(_lblChoDinhHuong, "🟡 QC chờ định hướng: " + choDinhHuong, choDinhHuong);
                SetItem(_lblChoQCCuoi, "🔴 QC chờ duyệt cuối: " + choQCCuoi, choQCCuoi);
                SetItem(_lblDaDuyetChuaTra, "🔄 Đã duyệt chờ trả SX: " + daDuyetChuaTra, daDuyetChuaTra);
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
