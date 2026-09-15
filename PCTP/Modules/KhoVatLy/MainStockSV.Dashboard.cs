using DevExpress.XtraEditors;
using PCTP.Common;
using PCTP.Modules.KhoVatLy.UCControls;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    public partial class MainStockSV
    {
        private void BuildDashboardBar()
        {
            var pnl = new PanelControl { Dock = DockStyle.Top, Height = 40 };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 8, 0, 0) };

            _lblDashTongStockTp = MakeDashLabel("Tổng tồn STOCKTP: --");
            _lblDashTongRack = MakeDashLabel("Tổng trong Rack thật: --");
            _lblDashTongA0 = MakeDashLabel("Tổng trong kho tạm A0: --");
            _lblDashLech = MakeDashLabel("Lệch đối chiếu: --");
            _lblDashLech.Click += (s, e) => ShowDoiChieuLech();
            _lblDashLech.Cursor = Cursors.Hand;

            flow.Controls.AddRange(new Control[] { _lblDashTongStockTp, _lblDashTongRack, _lblDashTongA0, _lblDashLech });
            pnl.Controls.Add(flow);
            Controls.Add(pnl);
            pnl.BringToFront();
        }

        private LabelControl MakeDashLabel(string text) => new LabelControl
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 0, 25, 0),
            Appearance = { Font = new Font("Tahoma", 9.5F, FontStyle.Bold) }
        };

        private void BuildSlotDetailPanel()
        {
            _slotDetailPanel = new SlotDetailPanel();
            Controls.Add(_slotDetailPanel);

            // Detail panel không chiếm chỗ màn hình khi mở MainStockSV.
            // Khi click Slot, SlotDetailPanel.ShowSlot() sẽ tự mở panel;
            // VisibleChanged sẽ trigger LoadAllWarehouses() để canvas tính lại chiều rộng.
            _slotDetailPanel.Visible = false;
            _slotDetailPanel.VisibleChanged += SlotDetailPanel_VisibleChanged;
            _slotDetailPanel.BringToFront();
        }

        private void SlotDetailPanel_VisibleChanged(object sender, System.EventArgs e)
        {
            if (!isFirstShown || IsDisposed) return;

            // Dock layout cần được cập nhật trước khi tính lại kích thước Slot.
            PerformLayout();
            _ = LoadAllWarehouses();
        }

        private void RefreshDashboardBar()
        {
            int tongStockTp = _dashboardService.GetTongTonStockTp();
            int tongRack = _dashboardService.GetTongTonRackThat();
            int tongA0 = _dashboardService.GetTongTonKhoTam();
            int demLech = _dashboardService.DemLechDoiChieu();

            _lblDashTongStockTp.Text = $"📦 Tổng tồn STOCKTP: {tongStockTp:N0}";
            _lblDashTongRack.Text = $"🏭 Trong Rack thật: {tongRack:N0}";
            _lblDashTongA0.Text = $"📥 Trong kho tạm A0: {tongA0:N0}";
            _lblDashLech.Text = demLech > 0 ? $"⚠ Lệch đối chiếu: {demLech} LOT (click xem)" : "✅ Không lệch đối chiếu";
            _lblDashLech.Appearance.ForeColor = demLech > 0 ? Color.Red : Color.SeaGreen;
        }

        private void ShowDoiChieuLech()
        {
            WarehouseProcessNavigator.OpenNhapKhoTienTrinh(this, this);
        }
    }
}
