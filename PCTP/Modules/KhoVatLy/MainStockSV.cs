using DevExpress.XtraEditors;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Services;
using PCTP.VIEWSTOCK.CanVas;
using PCTP.VIEWSTOCK.UCControls;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    public partial class MainStockSV : DevExpress.XtraEditors.XtraForm
    {
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
        private DevExpress.XtraEditors.LabelControl _lblDashTongStockTp, _lblDashTongRack, _lblDashTongA0, _lblDashLech;
        private SlotDetailPanel _slotDetailPanel;

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
    }
}
