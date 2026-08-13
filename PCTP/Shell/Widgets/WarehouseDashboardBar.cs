using DevExpress.XtraEditors;
using PCTP.Common;
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
        private readonly IPhieuLoiRepository _phieuLoiRepo;
        private readonly INhapKhoDashboardRepository _dashRepo;
        private LabelControl _lblChoDinhHuong, _lblChoQC, _lblDaDuyetChuaTra, _lblLechA0;

        public WarehouseDashboardBar(IPhieuLoiRepository phieuLoiRepo,
                                      INhapKhoDashboardRepository dashRepo)
        {
            _phieuLoiRepo = phieuLoiRepo;
            _dashRepo = dashRepo;
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
            _lblDaDuyetChuaTra.Click += (s, e) => WarehouseProcessNavigator.OpenTraHangNG(this);
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
                // ── SHELL chỉ ĐỌC số liệu qua Repository (Tầng 3) — không tự viết SQL ──
                int choDinhHuong = _phieuLoiRepo.DemChoBanHanhPhieuBatThuong(); // mốc 2→3a
                int choQCCuoi = _phieuLoiRepo.DemChoQC();                      // mốc 3b
                int daDuyetChuaTra = _phieuLoiRepo.DemSanSangTra();             // mốc 4
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
                System.Diagnostics.Debug.WriteLine($"[Main_APP] RefreshAppDashboardBar lỗi: {ex.Message}");
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
