using DevExpress.XtraEditors;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
    public partial class WarehouseDashboardBar : XtraUserControl
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;   // ← thay cho IPhieuLoiRepository
        private readonly INhapKhoDashboardRepository _dashRepo;
        private LabelControl _lblChoDinhHuong, _lblChoQC, _lblDaDuyetChuaTra, _lblLechA0;

        public WarehouseDashboardBar(
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            INhapKhoDashboardRepository dashRepo)
        {
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
            _dashRepo = dashRepo ?? throw new ArgumentNullException(nameof(dashRepo));
            BuildUI();
            Refresh_();
        }

        public void BuildUI()
        {
            var pnl = new PanelControl { Dock = DockStyle.Top, Height = 42 };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(12, 8, 0, 0)
            };

            // ── Mốc 3a: chờ QC định hướng ban đầu ────────────────────────────────
            _lblChoDinhHuong = MakeAppDashLabel("QC chờ định hướng: --");
            _lblChoDinhHuong.Click += (s, e) => WarehouseProcessNavigator.OpenQCDinhHuong(this);
            _lblChoDinhHuong.Cursor = Cursors.Hand;

            // ── Mốc 3b: chờ QC xác nhận lần cuối ─────────────────────────────────
            _lblChoQC = MakeAppDashLabel("QC chờ duyệt cuối: --");
            _lblChoQC.Click += (s, e) => WarehouseProcessNavigator.OpenQCXacNhanCuoi(this);
            _lblChoQC.Cursor = Cursors.Hand;

            // ── Mốc 4: đã duyệt, chờ trả về SX ───────────────────────────────────
            _lblDaDuyetChuaTra = MakeAppDashLabel("Đã duyệt chờ trả SX: --");
            // WarehouseDashboardBar.cs
            _lblDaDuyetChuaTra.Click += (s, e) => WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this);
            _lblDaDuyetChuaTra.Cursor = Cursors.Hand;

            // ── Đối chiếu A0 ──────────────────────────────────────────────────────
            _lblLechA0 = MakeAppDashLabel("Lệch đối chiếu A0: --");
            _lblLechA0.Click += (s, e) => WarehouseProcessNavigator.OpenNhapKhoTienTrinh(this);
            _lblLechA0.Cursor = Cursors.Hand;

            flow.Controls.AddRange(new Control[]
            {
        _lblChoDinhHuong, _lblChoQC, _lblDaDuyetChuaTra, _lblLechA0
            });
            pnl.Controls.Add(flow);
            Controls.Add(pnl);
            pnl.BringToFront();

            Refresh_();
        }
        private void Refresh_()
        {
            try
            {
                int choDinhHuong = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaTaoPhieuBatThuong);
                int choQCCuoi = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaGiaoSanXuat);
                int daDuyetChuaTra = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaQCXacNhanCuoi);
                int lech = _dashRepo.DemLechDoiChieu();

                _lblChoDinhHuong.Text = $"🟡 QC chờ định hướng: {choDinhHuong}";
                _lblChoDinhHuong.Appearance.ForeColor = choDinhHuong > 0 ? Color.DarkOrange : Color.SeaGreen;

                _lblChoQC.Text = $"🔴 QC chờ duyệt cuối: {choQCCuoi}";
                _lblChoQC.Appearance.ForeColor = choQCCuoi > 0 ? Color.Crimson : Color.SeaGreen;

                _lblDaDuyetChuaTra.Text = $"🔄 Đã duyệt chờ trả SX: {daDuyetChuaTra}";
                _lblDaDuyetChuaTra.Appearance.ForeColor = daDuyetChuaTra > 0 ? Color.DarkOrange : Color.SeaGreen;

                _lblLechA0.Text = lech > 0 ? $"⚠ Lệch đối chiếu A0: {lech}" : "✅ Không lệch đối chiếu A0";
                _lblLechA0.Appearance.ForeColor = lech > 0 ? Color.Red : Color.SeaGreen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WarehouseDashboardBar] Refresh_ lỗi: {ex.Message}");
            }
        }

        private LabelControl MakeAppDashLabel(string text) => new LabelControl
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 0, 30, 0),
            Appearance = { Font = new Font("Tahoma", 9.5F, FontStyle.Bold) }
        };
    }
}
