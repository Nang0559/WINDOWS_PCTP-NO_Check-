using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraVerticalGrid;
using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Services;
using PCTP.VIEWSTOCK.CanVas;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.UCControls;
using PCTP.VIEWSTOCK.ViewForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq.SqlClient;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
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
                    }
        private void BuildSlotDetailPanel()
                    {
                        _slotDetailPanel = new SlotDetailPanel();
                        Controls.Add(_slotDetailPanel);
                        _slotDetailPanel.BringToFront();
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
                    WarehouseProcessNavigator.OpenNhapKhoTienTrinh(
                        this,
                        this);
                }
    }
}
