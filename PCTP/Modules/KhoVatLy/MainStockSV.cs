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

    public partial class MainStockSV : DevExpress.XtraEditors.XtraForm
    {
        // ── [CANVAS STATE] DỮ LIỆU TỌA ĐỘ VÀ TRẠNG THÁI KHO ĐỂ VẼ ─────────────────
        private List<RackLayoutInfo> _rackLayouts = new List<RackLayoutInfo>();
        private Slot _currentSelectedSlotData = null;
        private string _currentFilterTemCode = "";
        private const int STABLE_COLUMNS = 15;
        private const int SLOT_HEIGHT = 55;
        private const int SLOT_MARGIN = 6;
        private const int RACK_PADDING = 15;
        private const int HEADER_HEIGHT = 35;
        private DataTable _cachedPEditData = null;
        private DateTime _cacheTime = DateTime.MinValue;
        private const int CACHE_SECONDS = 30;
        private bool isFirstShown = false;
        private RackSummaryPopup _rackPopup;
        private RackRenderInfo _hoveredRackForPopup = null;
        private System.Windows.Forms.Timer _hidePopupTimer;
        private LabelControl _lblDashTongStockTp, _lblDashTongRack, _lblDashTongA0, _lblDashLech;
        private SlotDetailPanel _slotDetailPanel;

        // ── [SERVICE — DUY NHẤT] Form chỉ biết Service, không biết Repository nào ──
        private readonly ISlotService _slotService;
        private readonly IWarehouseService _warehouseService;
        private readonly IRackService _rackService;
        private readonly IStockExportService _exportService;
        private readonly IPrintService _printService;
        private readonly IWarehouseDashboardService _dashboardService;
        private readonly IInspectionConfigService _inspectionConfigService;
        private readonly IInspectionLogRepository _inspectionLogRepo;
        private readonly IStockTpLookupService _stockTpLookupService;

        private List<RackRenderInfo> LoadRackRenderInfosSync() => _rackService.GetRackRenderInfos();

        public MainStockSV()
        {
            InitializeComponent();

            var module = MainStockModuleFactory.Build();
            _slotService = module.SlotService;
            _warehouseService = module.WarehouseService;
            _rackService = module.RackService;
            _exportService = module.ExportService;
            _printService = module.PrintService;
            _dashboardService = module.DashboardService;
            _inspectionConfigService = module.InspectionConfigService;
            _inspectionLogRepo = module.InspectionLogRepo;
            _stockTpLookupService = module.StockTpLookupService;

            BuildDashboardBar();
            BuildSlotDetailPanel();

            PEditInput.TextChanged += PEditInput_TextChanged;
            PEditInput.Closed += PEditInput_Closed;
            PEditInput.KeyDown += PEditInput_KeyDown;
            PEditInput.MouseClick += PEditInput_MouseClick;
            StockChangedNotifier.StockChanged += OnExternalStockChanged;

            if (PEditInput.Properties.View != null)
                PEditInput.Properties.View.KeyDown += GridView_KeyDown;
        }

;

            // ✅ Không còn raw SQL — toàn bộ đi qua IWarehouseDashboardService

            // ✅ Đã dùng đúng FormEnterItemSV(this) và ExportFormSV với đủ 3 service

            private string TruncateToWidth(Graphics g, string text, Font font, float maxWidth)
            {
                if (g.MeasureString(text, font).Width <= maxWidth)
                    return text;

                const string ellipsis = "...";
                int lo = 0, hi = text.Length;
                string result = ellipsis;

                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    string candidate = text.Substring(0, mid) + ellipsis;
                    if (g.MeasureString(candidate, font).Width <= maxWidth)
                    {
                        result = candidate;
                        lo = mid;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }
                return result;
            }

            private void pnlMain_MouseClick(object sender, MouseEventArgs e)
            {
                Point logicalLocation = new Point(e.X - pnlMain.AutoScrollPosition.X, e.Y - pnlMain.AutoScrollPosition.Y);

                foreach (var rack in _rackLayouts)
                {
                    if (rack.HeaderBounds.Contains(logicalLocation) && e.Button == MouseButtons.Right)
                    {
                        ShowCanvasRackContextMenu(e.Location, rack.RackData);
                        return;
                    }

                    if (rack.Bounds.Contains(logicalLocation))
                    {
                        foreach (var slotLayout in rack.Slots)
                        {
                            if (slotLayout.Bounds.Contains(logicalLocation))
                            {
                                if (e.Button == MouseButtons.Left)
                                {
                                    _currentSelectedSlotData = slotLayout.SlotData;
                                    pnlMain.Invalidate();
                                    _slotDetailPanel.ShowSlot(slotLayout.SlotData, _stockTpLookupService);
                                }
                                else if (e.Button == MouseButtons.Right)
                                {
                                    ShowSlotContextMenu(e.Location, slotLayout.SlotData);
                                }
                                return;
                            }
                        }
                    }
                }

                _currentSelectedSlotData = null;
                pnlMain.Invalidate();
                _slotDetailPanel.ShowSlot(null, _stockTpLookupService);
            }

            // ✅ Đã bỏ CheckInfor + sqlProvider.SaveWarehouseToDatabase — đi qua IWarehouseService

        }
    
}
