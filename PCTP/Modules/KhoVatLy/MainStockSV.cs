using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraVerticalGrid;
using PCTP.ClassSQL;
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

namespace PCTP.VIEWSTOCK
{
  
        public partial class MainStockSV : DevExpress.XtraEditors.XtraForm
        {
            // ⚠️ TODO (xem ghi chú cuối câu trả lời): sqlProvider vẫn còn dùng trực tiếp ở
            // LoadRackRenderInfosSync(), InitPEditInput(), DeleteRackFromCanvas() — 3 chỗ
            // cần SlotRepository.cs / RackRepository.cs để chuyển hẳn qua ISlotService/IRackService.
            private SQLPROVIDER sqlProvider = new SQLPROVIDER();

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
            private readonly IStockExportService _exportService;
            private readonly IPrintService _printService;
            private readonly IWarehouseDashboardService _dashboardService;

        private readonly IInspectionConfigService _inspectionConfigService;
        private readonly IInspectionLogRepository _inspectionLogRepo;

        // ⚠️ TODO: _stockTpRepoForDetail vẫn là Repository truyền thẳng vào SlotDetailPanel.
        // Cần SlotDetailPanel.cs để đổi chữ ký ShowSlot(...) sang nhận 1 Service thay vì
        // IStockTpRepository. Giữ tạm để không phá vỡ control đang chạy.
        private readonly IStockTpRepository _stockTpRepoForDetail;

            public MainStockSV()
            {
                InitializeComponent();

                // ── Dựng dependency graph DUY NHẤT tại đây — dùng chung 1 UnitOfWork cho
                //    toàn bộ Service của Form (giống pattern NhapTpModuleFactory) ──
                var dbExecutor = new PhieuSqlExecutor(new SQLPROVIDER());
                var uow = new UnitOfWork(dbExecutor.Sql);

                var slotRepo = new SlotRepository(dbExecutor, uow);
                var warehouseRepo = new WarehouseRepository(dbExecutor, uow);
                var rackRepo = new RackRepository(dbExecutor, uow);
                var historyRepo = new StockHistoryRepository(dbExecutor, uow);
                var stockExportRepo = new StockExportRepository(dbExecutor, uow);
                var hangChoGiaoRepo = new HangChoGiaoRepository(dbExecutor, uow);
                var exportHistoryRepo = new StockExportHistoryRepository(dbExecutor, uow, historyRepo);
                var dashRepo = new NhapKhoDashboardRepository(dbExecutor, uow);
                var inspectionConfigRepo = new InspectionConfigRepository(dbExecutor, uow);
                var inspectionLogRepo = new InspectionLogRepository(dbExecutor, uow);
            _slotService = new SlotService(slotRepo);
                _warehouseService = new WarehouseService(warehouseRepo, rackRepo, uow);
                var exportValidationService = new StockExportValidationService(stockExportRepo, exportHistoryRepo);
                _exportService = new StockExportService(
                    uow, _slotService, stockExportRepo, historyRepo, hangChoGiaoRepo, exportValidationService);
                _printService = new PrintService(_slotService, _warehouseService);
                _dashboardService = new WarehouseDashboardService(dashRepo);
            _inspectionConfigService = new InspectionConfigService(inspectionConfigRepo);
            _inspectionLogRepo = inspectionLogRepo;

         
            _stockTpRepoForDetail = new StockTpRepository(dbExecutor, uow);

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
                _slotDetailPanel.BringToFront();
            }

            // ✅ Không còn raw SQL — toàn bộ đi qua IWarehouseDashboardService
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
                using (var f = new FormNhapKhoTienTrinh(this))
                    f.ShowDialog(this);
            }

            private void OnExternalStockChanged()
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(OnExternalStockChanged));
                    return;
                }
                if (this.IsDisposed || !isFirstShown) return;
                OnSlotUpdated();
            }

            private void MainStock_Load(object sender, EventArgs e) { }

            // ✅ Đã dùng đúng FormEnterItemSV(this) và ExportFormSV với đủ 3 service
            private void OnSlotClicked(Slot slot)
            {
                if (slot == null) return;

                if (BulkImportConfig.IsBulkSlot(slot))
                {
                    var view = new FormBulkSlotView(slot, _slotService);
                    view.ShowDialog(this);
                    return;
                }

                try
                {
                    if (slot.IsOccupied)
                    {
                        var exportForm = new ExportFormSV(
                            slot, slot.RackName, slot.whname, this,
                            _slotService, _exportService, _printService);
                        exportForm.ShowDialog(this);
                    }
                    else
                    {
                        var enterForm = new FormEnterItemSV(this);
                        enterForm.ShowDialog(this);
                    }
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show($"Lỗi thực hiện điều hướng ô chứa:\n{ex.Message}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private void InitCanvasSettings()
            {
                SetDoubleBuffered(pnlMain);
                pnlMain.AutoScroll = true;
                pnlMain.VerticalScroll.Enabled = true;
                pnlMain.VerticalScroll.Visible = true;
                pnlMain.Scroll += (s, e) => { pnlMain.Invalidate(); };

                pnlMain.Paint += PnlMain_Paint;
                pnlMain.MouseClick += pnlMain_MouseClick;
                pnlMain.MouseMove += PnlMain_MouseMove;

                _rackPopup = new RackSummaryPopup();
                _rackPopup.MouseEnter += (s, e) => _hidePopupTimer.Stop();
                _rackPopup.MouseLeave += (s, e) => ScheduleHidePopup();
                pnlMain.MouseLeave += (s, e) => ScheduleHidePopup();

                _hidePopupTimer = new System.Windows.Forms.Timer { Interval = 250 };
                _hidePopupTimer.Tick += (s, e) =>
                {
                    _hidePopupTimer.Stop();
                    if (_rackPopup.Visible && _rackPopup.Bounds.Contains(Cursor.Position))
                        return;
                    _rackPopup.Hide();
                    _hoveredRackForPopup = null;
                };

                pnlMain.MouseDoubleClick += pnlMain_MouseDoubleClick;
            }

            private void ScheduleHidePopup()
            {
                _hidePopupTimer.Stop();
                _hidePopupTimer.Start();
            }

            private async void MainStock_Shown(object sender, EventArgs e)
            {
                if (isFirstShown) return;
                isFirstShown = true;

                SplashScreenManager.ShowForm(this, typeof(WaitFormExp), true, true, false);
                SplashScreenManager.Default.SetWaitFormCaption("Đang tải cấu trúc kho...");

                InitCanvasSettings();
                InitPEditInput();
                await LoadAllWarehouses();

                this.WindowState = FormWindowState.Maximized;
                SplashScreenManager.CloseForm();
            }

            public async Task LoadAllWarehouses()
            {
                var rackInfos = await Task.Run(() => LoadRackRenderInfosSync());

                int currentY = RACK_PADDING;
                var newLayouts = new List<RackLayoutInfo>();

                int availableWidth = pnlMain.ClientSize.Width - (RACK_PADDING * 2) - 15;
                if (availableWidth < 300) availableWidth = 300;

                int totalMarginsWidth = SLOT_MARGIN * (STABLE_COLUMNS + 1);
                int dynamicSlotWidth = (availableWidth - totalMarginsWidth) / STABLE_COLUMNS;
                if (dynamicSlotWidth < 50) dynamicSlotWidth = 50;

                foreach (var rackInfo in rackInfos)
                {
                    var rackLayout = new RackLayoutInfo { RackData = rackInfo, Slots = new List<SlotLayoutInfo>() };

                    int currentSlotX = RACK_PADDING + SLOT_MARGIN;
                    int currentSlotY = currentY + HEADER_HEIGHT + SLOT_MARGIN;
                    int maxRackWidth = availableWidth;
                    int count = 0;

                    foreach (var slotInfo in rackInfo.Slots)
                    {
                        if (count > 0 && count % STABLE_COLUMNS == 0)
                        {
                            currentSlotX = RACK_PADDING + SLOT_MARGIN;
                            currentSlotY += SLOT_HEIGHT + SLOT_MARGIN;
                        }

                        var slotBounds = new Rectangle(currentSlotX, currentSlotY, dynamicSlotWidth, SLOT_HEIGHT);
                        rackLayout.Slots.Add(new SlotLayoutInfo { SlotData = slotInfo.Slot, Bounds = slotBounds });

                        currentSlotX += dynamicSlotWidth + SLOT_MARGIN;
                        count++;
                    }

                    int rackHeight = (currentSlotY + SLOT_HEIGHT + SLOT_MARGIN) - currentY;
                    rackLayout.Bounds = new Rectangle(RACK_PADDING, currentY, maxRackWidth, rackHeight);
                    rackLayout.HeaderBounds = new Rectangle(RACK_PADDING, currentY, maxRackWidth, HEADER_HEIGHT);
                    newLayouts.Add(rackLayout);

                    currentY += rackHeight + RACK_PADDING;
                }

                _rackLayouts = newLayouts;
                pnlMain.AutoScrollMinSize = new Size(pnlMain.ClientSize.Width - 30, currentY + RACK_PADDING);
                pnlMain.Invalidate();
                RefreshDashboardBar();
            }

            private void PnlMain_Paint(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                Point scrollOffset = pnlMain.AutoScrollPosition;
                g.TranslateTransform(scrollOffset.X, scrollOffset.Y);

                using (Font headerFont = new Font("Tahoma", 10, FontStyle.Bold))
                using (Font slotFont = new Font("Tahoma", 8, FontStyle.Regular))
                using (Font boldSlotFont = new Font("Tahoma", 8, FontStyle.Bold))
                using (Font summaryFont = new Font("Tahoma", 9, FontStyle.Regular))
                {
                    var inputTemCodes = string.IsNullOrWhiteSpace(_currentFilterTemCode) ? new HashSet<string>() :
                        _currentFilterTemCode.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(code => code.Split('-')[0].Trim())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var rack in _rackLayouts)
                    {
                        Rectangle virtualClip = e.ClipRectangle;
                        virtualClip.Offset(-scrollOffset.X, -scrollOffset.Y);
                        if (!virtualClip.IntersectsWith(rack.Bounds)) continue;

                        g.FillRectangle(Brushes.White, rack.Bounds);
                        g.DrawRectangle(Pens.DarkGray, rack.Bounds);

                        g.FillRectangle(Brushes.LightGray, rack.HeaderBounds);
                        g.DrawRectangle(Pens.DimGray, rack.HeaderBounds);

                        string titleText = $"WH: {rack.RackData.WarehouseName} | Rack: {rack.RackData.RackName}";
                        g.DrawString(titleText, headerFont, Brushes.Black, rack.HeaderBounds.X + 8, rack.HeaderBounds.Y + 8);

                        string trongText = $"Trống: {rack.RackData.EmptySlotCount}/{rack.RackData.SlotCount}";
                        SizeF trongSize = g.MeasureString(trongText, summaryFont);
                        float trongX = rack.HeaderBounds.Right - trongSize.Width - 10;
                        g.DrawString(trongText, summaryFont, Brushes.DarkBlue, trongX, rack.HeaderBounds.Y + 9);

                        SizeF titleSize = g.MeasureString(titleText, headerFont);
                        float summaryStartX = rack.HeaderBounds.X + 8 + titleSize.Width + 20;
                        float summaryMaxWidth = trongX - summaryStartX - 10;

                        if (rack.RackData.ItemSummary.Count > 0 && summaryMaxWidth > 30)
                        {
                            string fullSummary = string.Join(" | ", rack.RackData.ItemSummary
                                .Select(kvp => $"[{kvp.Key}: vị trí {kvp.Value.Item1}, SL {kvp.Value.Item2}]"));
                            string displayText = TruncateToWidth(g, fullSummary, summaryFont, summaryMaxWidth);
                            var summaryRect = new RectangleF(summaryStartX, rack.HeaderBounds.Y + 9, summaryMaxWidth, 18);
                            g.DrawString(displayText, summaryFont, Brushes.DimGray, summaryRect);
                            rack.SummaryTextBounds = Rectangle.Round(summaryRect);
                        }
                        else
                        {
                            rack.SummaryTextBounds = Rectangle.Empty;
                        }

                        foreach (var slotLayout in rack.Slots)
                        {
                            Slot slot = slotLayout.SlotData;

                            if (inputTemCodes.Count > 0)
                            {
                                if (string.IsNullOrWhiteSpace(slot.TemCode)) continue;
                                var slotTemCodes = slot.TemCode.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(code => code.Split('-')[0].Trim());
                                if (!slotTemCodes.Any(code => inputTemCodes.Contains(code))) continue;
                            }

                            Brush slotBackground = Brushes.White;
                            Pen slotBorder = Pens.Black;

                            if (slot == _currentSelectedSlotData)
                            {
                                slotBackground = Brushes.LightCyan;
                                slotBorder = new Pen(Color.DeepSkyBlue, 2);
                            }
                            else if (slot.IsOccupied)
                            {
                                slotBackground = Brushes.Orange;
                            }
                            else
                            {
                                slotBackground = Brushes.LightGray;
                            }

                            g.FillRectangle(slotBackground, slotLayout.Bounds);
                            g.DrawRectangle(slotBorder, slotLayout.Bounds);

                            string headerSlotStr = slot.IsOccupied
                                ? $"{slot.SlotNumber}-{slot.Capacity}:{slot.TemCode}"
                                : $"Slot {slot.SlotNumber} - Trống";
                            if (headerSlotStr.Length > 12) headerSlotStr = headerSlotStr.Substring(0, 10) + "..";
                            g.DrawString(headerSlotStr, slotFont, Brushes.Black, slotLayout.Bounds.X + 2, slotLayout.Bounds.Y + 3);

                            if (slot.IsOccupied)
                            {
                                var distinctItemCodes = slot.Lots
                                    .Select(l => l.QRInfo?.ItemCode)
                                    .Where(ic => !string.IsNullOrEmpty(ic))
                                    .Distinct()
                                    .ToList();

                                string itemCodeStr;
                                if (distinctItemCodes.Count == 0)
                                    itemCodeStr = "";
                                else if (distinctItemCodes.Count == 1)
                                    itemCodeStr = distinctItemCodes[0].Length > 8
                                        ? distinctItemCodes[0].Substring(0, 7) + ".."
                                        : distinctItemCodes[0];
                                else
                                    itemCodeStr = $"{distinctItemCodes.Count} mã hàng";

                                g.DrawString(itemCodeStr, boldSlotFont, Brushes.DarkRed, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 16);

                                string lotNoStr = !string.IsNullOrEmpty(slot.LotNo)
                                    ? (slot.LotNo.Length > 8 ? slot.LotNo.Substring(0, 7) + ".." : slot.LotNo)
                                    : "";
                                g.DrawString(lotNoStr, slotFont, Brushes.DimGray, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 27);

                                string qtyStr = $"SL: {slot.Quantity}";
                                g.DrawString(qtyStr, boldSlotFont, Brushes.Blue, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 38);
                            }
                        }
                    }
                }

                g.ResetTransform();
            }

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
                                    _slotDetailPanel.ShowSlot(slotLayout.SlotData, _stockTpRepoForDetail);
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
                _slotDetailPanel.ShowSlot(null, _stockTpRepoForDetail);
            }

            private void pnlMain_MouseDoubleClick(object sender, MouseEventArgs e)
            {
                Point logicalLocation = new Point(e.X - pnlMain.AutoScrollPosition.X, e.Y - pnlMain.AutoScrollPosition.Y);
                foreach (var rack in _rackLayouts)
                    if (rack.Bounds.Contains(logicalLocation))
                        foreach (var slotLayout in rack.Slots)
                            if (slotLayout.Bounds.Contains(logicalLocation))
                            {
                                OnSlotClicked(slotLayout.SlotData);
                                return;
                            }
            }

            private void ShowSlotContextMenu(Point mouseLocation, Slot slot)
            {
                var menu = new ContextMenuStrip();

                if (slot.IsOccupied)
                {
                    var itemExport = new ToolStripMenuItem("📤 Xuất kho");
                    itemExport.Click += (s, e) => OnSlotClicked(slot);
                    menu.Items.Add(itemExport);
                }
                else
                {
                    var itemImport = new ToolStripMenuItem("📥 Nhập kho vào đây");
                    itemImport.Click += (s, e) => OnSlotClicked(slot);
                    menu.Items.Add(itemImport);
                }

                var itemHistory = new ToolStripMenuItem("📜 Xem lịch sử Slot");
                itemHistory.Click += (s, e) => { /* mở FormStockHistory lọc theo SlotId nếu cần */ };
                menu.Items.Add(itemHistory);

                menu.Show(pnlMain, mouseLocation);
            }

            private void PnlMain_MouseMove(object sender, MouseEventArgs e)
            {
                bool isHoveringSlot = false;
                Point logicalLocation = new Point(e.X - pnlMain.AutoScrollPosition.X, e.Y - pnlMain.AutoScrollPosition.Y);
                RackLayoutInfo hoveredSummaryRack = null;

                foreach (var rack in _rackLayouts)
                {
                    if (rack.SummaryTextBounds != Rectangle.Empty && rack.SummaryTextBounds.Contains(logicalLocation))
                        hoveredSummaryRack = rack;

                    if (rack.Bounds.Contains(logicalLocation))
                    {
                        foreach (var slotLayout in rack.Slots)
                        {
                            if (slotLayout.Bounds.Contains(logicalLocation))
                            {
                                isHoveringSlot = true;
                                break;
                            }
                        }
                    }

                    if (isHoveringSlot) break;
                }

                pnlMain.Cursor = isHoveringSlot || hoveredSummaryRack != null ? Cursors.Hand : Cursors.Default;

                if (hoveredSummaryRack != null)
                {
                    _hidePopupTimer.Stop();
                    if (_hoveredRackForPopup != hoveredSummaryRack.RackData)
                    {
                        _hoveredRackForPopup = hoveredSummaryRack.RackData;
                        Point screenPoint = pnlMain.PointToScreen(new Point(e.X + 15, e.Y + 15));
                        _rackPopup.ShowSummary(
                            $"Rack: {hoveredSummaryRack.RackData.RackName}",
                            hoveredSummaryRack.RackData.ItemSummary,
                            screenPoint);
                    }
                }
                else
                {
                    if (_hoveredRackForPopup != null)
                        ScheduleHidePopup();
                }
            }

            private void ShowExportFormFromCanvas(Slot slotData)
            {
                try
                {
                    var exportForm = new ExportFormSV(
                        slotData, slotData.RackName, slotData.whname, this,
                        _slotService, _exportService, _printService);
                    exportForm.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi mở Form xuất kho:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private void ShowCanvasRackContextMenu(Point mouseLocation, RackRenderInfo rackInfo)
            {
                var menu = new ContextMenuStrip();

                var itemInfo = new ToolStripMenuItem($"📦 {rackInfo.WarehouseName} | Rack: {rackInfo.RackName} ({rackInfo.EmptySlotCount}/{rackInfo.SlotCount} trống)")
                {
                    Enabled = false,
                    Font = new Font("Tahoma", 9, FontStyle.Bold)
                };
                menu.Items.Add(itemInfo);
                menu.Items.Add(new ToolStripSeparator());

                bool isEmpty = rackInfo.EmptySlotCount == rackInfo.SlotCount;
                var itemDelete = new ToolStripMenuItem("🗑 Xóa Rack này")
                {
                    Enabled = isEmpty,
                    ForeColor = isEmpty ? Color.Red : Color.Gray,
                    Font = new Font("Tahoma", 9, FontStyle.Bold)
                };

                if (!isEmpty)
                {
                    var itemWhy = new ToolStripMenuItem($" ⚠ Rack còn {rackInfo.SlotCount - rackInfo.EmptySlotCount} ô chứa hàng")
                    {
                        Enabled = false,
                        Font = new Font("Tahoma", 8, FontStyle.Italic),
                        ForeColor = Color.DarkOrange
                    };
                    menu.Items.Add(itemWhy);
                }

                itemDelete.Click += (s, e) => DeleteRackFromCanvas(rackInfo);
                menu.Items.Add(itemDelete);
                menu.Show(pnlMain, mouseLocation);
            }

            // ⚠️ TODO: chuyển sang IRackService.Delete(rackId) khi có RackService.cs
            // (interface đã tài liệu hoá method Delete(rackId), nhưng chưa xác nhận được
            // constructor RackService thật để dựng field an toàn — tránh đoán sai).
            private void DeleteRackFromCanvas(RackRenderInfo rackInfo)
            {
                var confirm = MessageBox.Show($"Xóa Rack [{rackInfo.RackName}] - Kho [{rackInfo.WarehouseName}]?\nThao tác không thể hoàn tác.", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                try
                {
                    sqlProvider.ExecuteNonQuery(sqlProvider.B7R2_FCCdbb, "DELETE FROM Slot WHERE RackId = @RackId", new[] { new SqlParameter("@RackId", rackInfo.RackId) });
                    sqlProvider.ExecuteNonQuery(sqlProvider.B7R2_FCCdbb, "DELETE FROM Rack WHERE RackId = @RackId", new[] { new SqlParameter("@RackId", rackInfo.RackId) });

                    _ = LoadAllWarehouses();
                    MessageBox.Show("Xóa Rack thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xóa Rack:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            public async void OnSlotUpdated()
            {
                InitPEditInput(forceRefresh: true);
                await LoadAllWarehouses();
            }

            private void btnReset_Click(object sender, EventArgs e)
            {
                _currentFilterTemCode = "";
                _currentSelectedSlotData = null;
                pnlMain.Invalidate();
            }

            private void FilterSlotByTemCode(string temCode)
            {
                _currentFilterTemCode = temCode;
                pnlMain.Invalidate();
            }

            // ⚠️ TODO: chuyển toàn bộ query này vào 1 method mới trên ISlotRepository/IRackService
            // (vd. GetCanvasRenderInfos()), rồi expose qua ISlotService hoặc IRackService.
            // Cần SlotRepository.cs/RackRepository.cs thật để biết field Db/Uow nội bộ, tránh đoán sai.
            private List<RackRenderInfo> LoadRackRenderInfosSync()
            {
                string query = @"
                SELECT
                    w.Name AS WarehouseName,
                    r.RackName,
                    r.RackId,
                    s.SlotId,
                    s.SlotNumber,
                    s.ItemCode,
                    s.Quantity,
                    s.Capacity,
                    s.ImportDate,
                    s.IsOccupied,
                    sl.LotNo,
                    sl.ItemCode AS LotItemCode,
                    sl.Quantity AS LotQuantity,
                    sl.TemCode AS LotTemCode,
                    sl.QrData,
                    sl.ImportDate AS LotImportDate,
                    sl.NgaySX,
                    sl.SoPhieuTong,
                    sl.MaPhieu
                FROM Warehouse w
                INNER JOIN Rack r ON r.WarehouseId = w.WarehouseId
                LEFT JOIN Slot s ON s.RackId = r.RackId
                LEFT JOIN SlotLot sl ON sl.SlotId = s.SlotId
                ORDER BY w.Name, r.RackName, s.SlotNumber, sl.LotNo";

                DataTable dt = sqlProvider.LoadData1(sqlProvider.B7R2_FCCdbb, query);

                var rackDict = new Dictionary<string, RackRenderInfo>();
                var slotDict = new Dictionary<int, Slot>();

                foreach (DataRow row in dt.Rows)
                {
                    string whName = row["WarehouseName"].ToString();
                    string rackName = row["RackName"].ToString();
                    string key = $"{whName}_{rackName}";

                    if (!rackDict.TryGetValue(key, out var rackInfo))
                    {
                        rackInfo = new RackRenderInfo
                        {
                            WarehouseName = whName,
                            RackName = rackName,
                            RackId = Convert.ToInt32(row["RackId"]),
                            Slots = new List<SlotRenderInfo>(),
                            ItemSummary = new Dictionary<string, (int, int)>()
                        };
                        rackDict[key] = rackInfo;
                    }

                    if (row["SlotNumber"] is DBNull) continue;

                    int slotId = Convert.ToInt32(row["SlotId"]);

                    if (!slotDict.TryGetValue(slotId, out var slot))
                    {
                        slot = new Slot
                        {
                            SlotId = slotId,
                            whname = whName,
                            RackName = rackName,
                            Rackid = Convert.ToInt32(row["RackId"]),
                            SlotNumber = Convert.ToInt32(row["SlotNumber"]),
                            ItemCode = row["ItemCode"]?.ToString(),
                            Quantity = row["Quantity"] is DBNull ? 0 : Convert.ToInt32(row["Quantity"]),
                            Capacity = row["Capacity"] is DBNull ? 0 : Convert.ToInt32(row["Capacity"]),
                            ImportDate = row["ImportDate"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(row["ImportDate"]),
                            IsOccupied = row["IsOccupied"] is DBNull ? false : Convert.ToBoolean(row["IsOccupied"]),
                            Lots = new List<LotInfo>()
                        };
                        slotDict[slotId] = slot;

                        rackInfo.Slots.Add(new SlotRenderInfo { Slot = slot, RackName = rackName, WarehouseName = whName });
                    }

                    if (row["LotNo"] != DBNull.Value)
                    {
                        slot.Lots.Add(new LotInfo
                        {
                            LotNo = row["LotNo"].ToString(),
                            Quantity = row["LotQuantity"] is DBNull ? 0 : Convert.ToInt32(row["LotQuantity"]),
                            TemCode = row["LotTemCode"]?.ToString(),
                            RawQr = row["QrData"]?.ToString(),
                            QRInfo = new QRCodeInfo
                            {
                                ItemCode = row["LotItemCode"]?.ToString(),
                                NgaySX = row["NgaySX"]?.ToString(),
                                SoPhieuTong = row["SoPhieuTong"]?.ToString(),
                                MaPhieu = row["MaPhieu"]?.ToString(),
                                RawQr = row["QrData"]?.ToString()
                            }
                        });
                    }
                }

                foreach (var info in rackDict.Values)
                {
                    info.SlotCount = info.Slots.Count;
                    info.EmptySlotCount = info.Slots.Count(s => !s.Slot.IsOccupied);

                    foreach (var sr in info.Slots)
                    {
                        var slot = sr.Slot;
                        foreach (var lot in slot.Lots)
                        {
                            string itemCode = lot.QRInfo?.ItemCode;
                            if (string.IsNullOrEmpty(itemCode) || lot.Quantity <= 0) continue;

                            if (info.ItemSummary.TryGetValue(itemCode, out var s))
                                info.ItemSummary[itemCode] = (s.Item1 + 1, s.Item2 + lot.Quantity);
                            else
                                info.ItemSummary[itemCode] = (1, lot.Quantity);
                        }
                    }
                }

                return rackDict.Values.ToList();
            }

            // ⚠️ TODO: chuyển sang ISlotService.GetOccupiedSlotsForLookup() khi có SlotRepository.cs
            private void InitPEditInput(bool forceRefresh = false)
            {
                if (!forceRefresh && _cachedPEditData != null && (DateTime.Now - _cacheTime).TotalSeconds < CACHE_SECONDS)
                {
                    BindPEditInput(_cachedPEditData);
                    return;
                }

                string query = @"
                SELECT s.SlotNumber, w.Name WhName, r.RackName,
                       s.ItemCode, s.TemCode, s.LotNo, s.ImportDate
                FROM   Slot s
                JOIN   Rack r      ON s.RackId      = r.RackId
                JOIN   Warehouse w ON r.WarehouseId = w.WarehouseId
                WHERE  s.IsOccupied = 1";

                _cachedPEditData = sqlProvider.LoadData1(sqlProvider.B7R2_FCCdbb, query);
                _cacheTime = DateTime.Now;
                BindPEditInput(_cachedPEditData);
            }

            private void BindPEditInput(DataTable dt)
            {
                PEditInput.Properties.DataSource = null;
                PEditInput.Properties.DataSource = dt;
                PEditInput.Properties.DisplayMember = "ItemCode";
                PEditInput.Properties.ValueMember = "ItemCode";

                GridView view = PEditInput.Properties.View as GridView;
                if (view == null) return;

                view.Columns.Clear();
                view.Columns.AddVisible("WhName", "Kho");
                view.Columns.AddVisible("RackName", "Rack");
                view.Columns.AddVisible("SlotNumber", "Vị trí");
                view.Columns.AddVisible("LotNo", "Lot No");
                view.Columns.AddVisible("TemCode", "TemCode");
                view.Columns.AddVisible("ItemCode", "Mã Item");
                view.Columns.AddVisible("ImportDate", "Ngày nhập");
                view.Columns["ImportDate"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                view.Columns["ImportDate"].DisplayFormat.FormatString = "yyyy-MM-dd";
            }

            private void PEditInput_TextChanged(object sender, EventArgs e)
            {
                GridView view = PEditInput.Properties.View as GridView;
                if (view == null) return;

                string keyword = PEditInput.Text.ToUpper().Trim();

                if (keyword.Contains(":"))
                {
                    view.ActiveFilterString = "";
                    PEditInput.ClosePopup();
                    return;
                }

                if (keyword.Length >= 1)
                {
                    view.ActiveFilterString = $"[ItemCode] LIKE '{keyword}%'";
                    PEditInput.ShowPopup();
                }
                else
                {
                    view.ActiveFilterString = "";
                    PEditInput.ClosePopup();
                }
            }

            private void PEditInput_Closed(object sender, ClosedEventArgs e)
            {
                if (e.CloseMode == PopupCloseMode.Normal)
                {
                    GridView view = PEditInput.Properties.View as GridView;
                    if (view != null && view.FocusedRowHandle >= 0)
                    {
                        DataRow row = view.GetDataRow(view.FocusedRowHandle);
                        if (row != null)
                        {
                            string temCode = row["TemCode"].ToString();
                            if (!string.IsNullOrEmpty(temCode))
                                FilterSlotByTemCode(temCode);
                        }
                    }
                }
            }

            private void PEditInput_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    string input = PEditInput.Text.Trim();
                    if (input.Contains(":"))
                    {
                        var parts = input.Split(':');
                        if (parts.Length >= 5)
                        {
                            string temcode = parts.Last() + parts[4] + "-" + parts[3];
                            FilterSlotByTemCode(temcode);
                            e.Handled = true;
                        }
                        else
                        {
                            MessageBox.Show("Mã QR không đúng định dạng cấu trúc.");
                        }
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    PEditInput.Text = "";
                    PEditInput.EditValue = null;
                }
            }

            private void GridView_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (PEditInput.Properties.View != null && PEditInput.Properties.View.FocusedRowHandle >= 0)
                    {
                        DataRow row = PEditInput.Properties.View.GetFocusedDataRow();
                        if (row != null)
                            FilterSlotByTemCode(row["TemCode"].ToString());
                    }
                    PEditInput.ClosePopup();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }

            private void PEditInput_MouseClick(object sender, MouseEventArgs e)
            {
                InitPEditInput();
            }

            // ✅ Đã bỏ CheckInfor + sqlProvider.SaveWarehouseToDatabase — đi qua IWarehouseService
            private void btnRegisterRack_Click(object sender, EventArgs e)
            {
                using (var form = new FormRegisterRack(_warehouseService))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;

                    string whName = form.WhName;
                    string rackName = form.RackName;
                int rowCount = form.RowCount;
                int columnCount = form.ColumnCount;
                int slotCapacity = form.SlotCapacity;

                    try
                    {
                        // Giả định layout 1 hàng x slotCount cột — giữ đúng hành vi cũ (danh sách
                        // Slot phẳng đánh số 1..slotCount, không phân hàng/cột thật).
                        _warehouseService.RegisterWarehouseAndRack(
                            whName, rackName, rowCount: rowCount, columnCount: columnCount, slotCapacity: slotCapacity);

                        _ = LoadAllWarehouses();
                    }
                    catch (Exception ex)
                    {
                        XtraMessageBox.Show($"Lỗi đăng ký Rack:\n{ex.Message}", "Lỗi",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            public List<Slot> AllSlots
            {
                get
                {
                    var list = new List<Slot>();
                    foreach (var rack in _rackLayouts)
                    {
                        if (rack.RackData?.Slots != null)
                            list.AddRange(rack.RackData.Slots.Select(s => s.Slot));
                    }
                    return list;
                }
            }

            private void btnEnterItem_Click(object sender, EventArgs e)
            {
                using (var form = new FormEnterItemSV(this))
                {
                    form.ShowDialog(this);
                }
            }

            private void simpleButton1_Click(object sender, EventArgs e)
            {
                FormStockHistory shs = new FormStockHistory();
                shs.Show();
            }

            private static void SetDoubleBuffered(Control control)
            {
                typeof(Control).InvokeMember("DoubleBuffered",
                    System.Reflection.BindingFlags.SetProperty |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic,
                    null, control, new object[] { true });
            }

            private void MainStock_Resize(object sender, EventArgs e)
            {
                if (!isFirstShown || this.WindowState == FormWindowState.Minimized) return;
                _ = LoadAllWarehouses();
            }

            private void btnDKMa_Click(object sender, EventArgs e)
            {
                using (var form = new FormInspectionConfig(_inspectionConfigService, _warehouseService))
                {
                    form.ShowDialog(this);
                }
            }

            private void btnHisCheck_Click(object sender, EventArgs e)
            {
                using (var form = new FormInspectionHistory(_inspectionLogRepo, _warehouseService))
                {
                    form.ShowDialog(this);
                }
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                _rackPopup?.Close();
                _rackPopup?.Dispose();
                StockChangedNotifier.StockChanged -= OnExternalStockChanged;
                base.OnFormClosed(e);
            }
        }
    
}