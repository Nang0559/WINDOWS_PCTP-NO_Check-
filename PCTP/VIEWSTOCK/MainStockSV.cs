using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraVerticalGrid;
using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.CanVas;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.ViewForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        // SQL Provider kết nối database
        private SQLPROVIDER sqlProvider = new SQLPROVIDER();

        // ── [CANVAS STATE] DỮ LIỆU TỌA ĐỘ VÀ TRẠNG THÁI KHHO ĐỂ VẼ ─────────────────
        private List<RackLayoutInfo> _rackLayouts = new List<RackLayoutInfo>();
        private Slot _currentSelectedSlotData = null;
        private string _currentFilterTemCode = "";

        // Cấu hình kích thước vẽ Canvas (Cải tiến lấp đầy màn hình động)
        private const int STABLE_COLUMNS = 15; // 🌟 Bạn muốn 1 dòng có bao nhiêu Slot? Hãy sửa số này (ví dụ: 12, 15, 20)
        private const int SLOT_HEIGHT = 55;    // Tăng nhẹ chiều cao cho cân đối với chiều rộng mới
        private const int SLOT_MARGIN = 6;
        private const int RACK_PADDING = 15;
        private const int HEADER_HEIGHT = 35;

        // ── [CACHE] TỐI ƯU DỮ LIỆU CHO LOOKUPEDIT ──────────────────────────────────
        private DataTable _cachedPEditData = null;
        private DateTime _cacheTime = DateTime.MinValue;
        private const int CACHE_SECONDS = 30;
        private bool isFirstShown = false;
        private RackSummaryPopup _rackPopup;
        private RackRenderInfo _hoveredRackForPopup = null;
        private System.Windows.Forms.Timer _hidePopupTimer;
        public MainStockSV()
        {
            InitializeComponent();

            // Đăng ký sự kiện cho ô nhập liệu PEditInput (GridLookUpEdit)
            PEditInput.TextChanged += PEditInput_TextChanged;
            PEditInput.Closed += PEditInput_Closed;
            PEditInput.KeyDown += PEditInput_KeyDown;
            PEditInput.MouseClick += PEditInput_MouseClick;

            if (PEditInput.Properties.View != null)
            {
                PEditInput.Properties.View.KeyDown += GridView_KeyDown;
            }
        }

        // ── [HÀM MỚI 1] SỰ KIỆN LOAD FORM CHÍNH HỆ THỐNG ──────────────────────────
        private void MainStock_Load(object sender, EventArgs e)
        {
            // Thực hiện các cấu hình khởi tạo form nếu cần trước khi hiển thị (Shown)
            // Ví dụ: Đặt tiêu đề động, cấu hình quyền hạn, v.v.
        }

        // ── [HÀM MỚI 2] XỬ LÝ ĐIỀU HƯỚNG THÔNG MINH KHI CLICK VÀO SLOT CANVAS ──────
        private void OnSlotClicked(Slot slot)
        {
            if (slot == null) return;

            try
            {
                if (slot.IsOccupied)
                {
                    // Nếu ô đang chứa hàng -> Tự động mở Form Xuất Kho
                    var exportForm = new ExportFormSV(slot, slot.RackName, slot.whname, this);
                    exportForm.ShowDialog(this);
                }
                else
                {
                    // Nếu ô đang trống -> Tự động mở Form Nhập Kho
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
            // Bật DoubleBuffered trực tiếp cho pnlMain để chống nhấp nháy 100% khi vẽ lại
            SetDoubleBuffered(pnlMain);

            // 🌟 THÊM CÁC DÒNG NÀY: Bật tính năng cuộn chuột cho Panel đồ họa
            pnlMain.AutoScroll = true;
            pnlMain.VerticalScroll.Enabled = true;
            pnlMain.VerticalScroll.Visible = true;
            pnlMain.Scroll += (s, e) => { pnlMain.Invalidate(); };

            // Đăng ký các sự kiện tương tác đồ họa trên Panel nền
            pnlMain.Paint += PnlMain_Paint;
            pnlMain.MouseClick += pnlMain_MouseClick;
            pnlMain.MouseMove += PnlMain_MouseMove;
            // ← THÊM
            _rackPopup = new RackSummaryPopup();
            // ← THÊM: khi chuột vào popup -> huỷ lịch ẩn
            _rackPopup.MouseEnter += (s, e) => _hidePopupTimer.Stop();
            // ← THÊM: khi chuột rời popup -> lên lịch ẩn
            _rackPopup.MouseLeave += (s, e) => ScheduleHidePopup();

            // ← THÊM: khi chuột rời hẳn pnlMain -> lên lịch ẩn (không ẩn ngay, cho kịp chạy sang popup)
            pnlMain.MouseLeave += (s, e) => ScheduleHidePopup();

            // ← THÊM: timer trễ ẩn, giống AutoPopDelay của ToolTip
            _hidePopupTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _hidePopupTimer.Tick += (s, e) =>
            {
                _hidePopupTimer.Stop();
                _rackPopup.Hide();
                _hoveredRackForPopup = null;
            };
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

            // Hiển thị loading của DevExpress
            DevExpress.XtraSplashScreen.SplashScreenManager.ShowForm(this, typeof(WaitFormExp), true, true, false);
            DevExpress.XtraSplashScreen.SplashScreenManager.Default.SetWaitFormCaption("Đang tải cấu trúc kho...");

            // 1. Kích hoạt cài đặt đồ họa Canvas
            InitCanvasSettings();

            // 2. Nạp dữ liệu gợi ý ban đầu
            InitPEditInput();

            // 3. Tải và dựng bản đồ kho Canvas
            await LoadAllWarehouses();

            this.WindowState = FormWindowState.Maximized;
            DevExpress.XtraSplashScreen.SplashScreenManager.CloseForm();
        }
       
        // ── [TÍNH TOÁN & VẼ CANVAS] VÙNG XỬ LÝ ĐỒ HỌA CHÍNH ───────────────────────
        public async Task LoadAllWarehouses()
        {
            // 1. Đọc dữ liệu từ SQL (Chạy trên Background Thread tránh đơ UI)
            var rackInfos = await Task.Run(() => LoadRackRenderInfosSync());

            // 2. Tính toán ma trận tọa độ hiển thị ảo (Thuật toán tự động giãn kích thước lấp đầy)
            int currentY = RACK_PADDING;
            var newLayouts = new List<RackLayoutInfo>();

            // Chiều rộng khả dụng thực tế của panel trừ đi lề và trừ đi độ rộng thanh cuộn dọc (khoảng 25px)
            int availableWidth = pnlMain.ClientSize.Width - (RACK_PADDING * 2) - 15;
            if (availableWidth < 300) availableWidth = 300; // Khóa giới hạn tối thiểu tránh lỗi chia cho 0

            // 🌟 CẢI TIẾN QUAN TRỌNG: Tính toán độ rộng của MỖI Ô SLOT một cách ĐỘNG để lấp đầy màn hình
            // Công thức: (Tổng chiều rộng - Tổng các khoảng cách Margin giữa các ô) / Số lượng cột cấu hình
            int totalMarginsWidth = SLOT_MARGIN * (STABLE_COLUMNS + 1);
            int dynamicSlotWidth = (availableWidth - totalMarginsWidth) / STABLE_COLUMNS;
            if (dynamicSlotWidth < 50) dynamicSlotWidth = 50; // Giới hạn chiều rộng tối thiểu của 1 slot để tránh chữ bị đè

            foreach (var rackInfo in rackInfos)
            {
                var rackLayout = new RackLayoutInfo
                {
                    RackData = rackInfo,
                    Slots = new List<SlotLayoutInfo>()
                };

                int currentSlotX = RACK_PADDING + SLOT_MARGIN;
                int currentSlotY = currentY + HEADER_HEIGHT + SLOT_MARGIN;
                int maxRackWidth = availableWidth;

                int count = 0;
                foreach (var slotInfo in rackInfo.Slots)
                {
                    // Tự động xuống dòng dựa trên số cột cố định STABLE_COLUMNS đã cấu hình
                    if (count > 0 && count % STABLE_COLUMNS == 0)
                    {
                        currentSlotX = RACK_PADDING + SLOT_MARGIN;
                        currentSlotY += SLOT_HEIGHT + SLOT_MARGIN;
                    }

                    // 🌟 Áp dụng dynamicSlotWidth (Chiều rộng co giãn động) vào ô vẽ
                    var slotBounds = new Rectangle(currentSlotX, currentSlotY, dynamicSlotWidth, SLOT_HEIGHT);
                    rackLayout.Slots.Add(new SlotLayoutInfo
                    {
                        SlotData = slotInfo.Slot,
                        Bounds = slotBounds
                    });

                    currentSlotX += dynamicSlotWidth + SLOT_MARGIN;
                    count++;
                }

                // Tính toán bao quanh Rectangle cho cả Rack Container (Giờ đây luôn kéo dài hết lề phải màn hình)
                int rackHeight = (currentSlotY + SLOT_HEIGHT + SLOT_MARGIN) - currentY;
                rackLayout.Bounds = new Rectangle(RACK_PADDING, currentY, maxRackWidth, rackHeight);
                rackLayout.HeaderBounds = new Rectangle(RACK_PADDING, currentY, maxRackWidth, HEADER_HEIGHT);

                newLayouts.Add(rackLayout);

                // Đẩy vị trí Y của Rack tiếp theo xuống dưới (cộng thêm khoảng đệm)
                currentY += rackHeight + RACK_PADDING;
            }

            // Gán lưới tọa độ mới vào biến toàn cục
            _rackLayouts = newLayouts;

            // Cập nhật vùng cuộn ảo cho Panel dựa trên tổng chiều cao currentY đã tính toán
            pnlMain.AutoScrollMinSize = new Size(pnlMain.ClientSize.Width - 30, currentY + RACK_PADDING);

            // Ra lệnh vẽ lại toàn bộ bề mặt đồ họa
            pnlMain.Invalidate();
        }

        private void PnlMain_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 🌟 KHẮC PHỤC LAG 1: Đồng bộ hóa ma trận đồ họa theo vị trí thanh cuộn hiện tại
            Point scrollOffset = pnlMain.AutoScrollPosition;
            g.TranslateTransform(scrollOffset.X, scrollOffset.Y);

            // Khởi tạo các Font chữ hệ thống để vẽ trực tiếp giống cấu trúc SlotControl cũ
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
                    // 🌟 KHẮC PHỤC LAG 2: Chuyển đổi vùng kiểm tra ClipRectangle sang tọa độ ảo đã cuộn
                    Rectangle virtualClip = e.ClipRectangle;
                    virtualClip.Offset(-scrollOffset.X, -scrollOffset.Y);
                    if (!virtualClip.IntersectsWith(rack.Bounds)) continue;

                    // 1. Vẽ khung viền bao quanh toàn bộ Rack
                    g.FillRectangle(Brushes.White, rack.Bounds);
                    g.DrawRectangle(Pens.DarkGray, rack.Bounds);

                    // 2. Vẽ Thanh Tiêu Đề (Header) của Rack
                    g.FillRectangle(Brushes.LightGray, rack.HeaderBounds);
                    g.DrawRectangle(Pens.DimGray, rack.HeaderBounds);

                    // Vẽ chuỗi thông tin Tên Kho | Tên Rack
                    string titleText = $"WH: {rack.RackData.WarehouseName} | Rack: {rack.RackData.RackName}";
                    g.DrawString(titleText, headerFont, Brushes.Black, rack.HeaderBounds.X + 8, rack.HeaderBounds.Y + 8);

                    //string itemSummaryDisplay = string.Join(" | ", rack.RackData.ItemSummary.Select(kvp => $"[{kvp.Key}: vị trí {kvp.Value.Item1}, SL {kvp.Value.Item2}]"));
                    //string statusText = $"Trống: {rack.RackData.EmptySlotCount}/{rack.RackData.SlotCount} " + (string.IsNullOrEmpty(itemSummaryDisplay) ? "" : $" - {itemSummaryDisplay}");

                    //SizeF statusSize = g.MeasureString(statusText, summaryFont);
                    //g.DrawString(statusText, summaryFont, Brushes.DarkBlue, rack.HeaderBounds.Right - statusSize.Width - 10, rack.HeaderBounds.Y + 9);
                    // "Trống: x/y" cố định bên phải
                    string trongText = $"Trống: {rack.RackData.EmptySlotCount}/{rack.RackData.SlotCount}";
                    SizeF trongSize = g.MeasureString(trongText, summaryFont);
                    float trongX = rack.HeaderBounds.Right - trongSize.Width - 10;
                    g.DrawString(trongText, summaryFont, Brushes.DarkBlue, trongX, rack.HeaderBounds.Y + 9);

                    // Vùng còn lại giữa title và "Trống" -> rút gọn theo chiều rộng thật, ellipsis nếu tràn
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

                        // Lưu bounds ở toạ độ "ảo" (đồng nhất với hệ toạ độ dùng trong MouseMove)
                        rack.SummaryTextBounds = Rectangle.Round(summaryRect);
                    }
                    else
                    {
                        rack.SummaryTextBounds = Rectangle.Empty;
                    }

                    // 3. Vẽ các ô vuông biểu thị Slot
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

                        // Định vị màu sắc cho ô dựa trên trạng thái (Chuẩn theo SlotControl cũ)
                        Brush slotBackground = Brushes.White;
                        Pen slotBorder = Pens.Black;

                        if (slot == _currentSelectedSlotData)
                        {
                            slotBackground = Brushes.LightCyan;
                            slotBorder = new Pen(Color.DeepSkyBlue, 2);
                        }
                        else if (slot.IsOccupied)
                        {
                            slotBackground = Brushes.Orange; // Đổi sang màu Cam (giống thuộc tính UpdateSlot cũ của bạn)
                        }
                        else
                        {
                            slotBackground = Brushes.LightGray; // Đổi sang màu Xám nhạt (giống trạng thái trống cũ)
                        }

                        g.FillRectangle(slotBackground, slotLayout.Bounds);
                        g.DrawRectangle(slotBorder, slotLayout.Bounds);

                        // 🌟 THAY ĐỔI: Vẽ lại chuỗi tiêu đề trên đỉnh ô (Ví dụ: 1-100:Tem01 hoặc Slot 1 - Đã sử dụng)
                        string headerSlotStr = slot.IsOccupied
                            ? $"{slot.SlotNumber}-{slot.Capacity}:{slot.TemCode}"
                            : $"Slot {slot.SlotNumber} - Trống";

                        // Thu gọn chuỗi tiêu đề nếu quá rộng so với kích thước ô vẽ
                        if (headerSlotStr.Length > 12) headerSlotStr = headerSlotStr.Substring(0, 10) + "..";
                        g.DrawString(headerSlotStr, slotFont, Brushes.Black, slotLayout.Bounds.X + 2, slotLayout.Bounds.Y + 3);

                        // 🌟 BỔ SUNG: Hiển thị thông tin ItemCode, LotNo, Quantity chi tiết vào thân ô nếu đang có hàng
                        if (slot.IsOccupied)
                        {
                            // Vẽ Mã hàng (ItemCode) màu đỏ đậm
                            string itemCodeStr = !string.IsNullOrEmpty(slot.ItemCode)
                                ? (slot.ItemCode.Length > 8 ? slot.ItemCode.Substring(0, 7) + ".." : slot.ItemCode)
                                : "";
                            g.DrawString(itemCodeStr, boldSlotFont, Brushes.DarkRed, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 16);

                            // Vẽ Số Lot (LotNo)
                            string lotNoStr = !string.IsNullOrEmpty(slot.LotNo)
                                ? (slot.LotNo.Length > 8 ? slot.LotNo.Substring(0, 7) + ".." : slot.LotNo)
                                : "";
                            g.DrawString(lotNoStr, slotFont, Brushes.DimGray, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 27);

                            // Vẽ Số lượng (Quantity) nằm góc dưới
                            string qtyStr = $"SL: {slot.Quantity}";
                            g.DrawString(qtyStr, boldSlotFont, Brushes.Blue, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 38);
                        }
                    }
                }
            }

            // Khôi phục lại trạng thái ma trận mặc định
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
            // 🌟 KHẮC PHỤC LAG VÀ LỆCH TỌA ĐỘ: Đổi tọa độ chuột thực tế sang tọa độ ảo theo thanh cuộn
            Point logicalLocation = new Point(e.X - pnlMain.AutoScrollPosition.X, e.Y - pnlMain.AutoScrollPosition.Y);

            foreach (var rack in _rackLayouts)
            {
                // Kiểm tra dựa trên tọa độ ảo mới tính toán
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

                                // Gọi sang hàm xử lý điều hướng thông minh tự động mở form nhập/xuất của bạn
                                OnSlotClicked(slotLayout.SlotData);
                            }
                            else if (e.Button == MouseButtons.Right)
                            {
                                ShowExportFormFromCanvas(slotLayout.SlotData);
                            }
                            return;
                        }
                    }
                }
            }
        }

        //private void PnlMain_MouseMove(object sender, MouseEventArgs e)
        //{
        //    bool isHoveringSlot = false;

        //    // 🌟 KHẮC PHỤC LỆCH TỌA ĐỘ KHI CUỘN CHUỘT
        //    Point logicalLocation = new Point(e.X - pnlMain.AutoScrollPosition.X, e.Y - pnlMain.AutoScrollPosition.Y);

        //    foreach (var rack in _rackLayouts)
        //    {
        //        if (rack.Bounds.Contains(logicalLocation))
        //        {
        //            foreach (var slotLayout in rack.Slots)
        //            {
        //                if (slotLayout.Bounds.Contains(logicalLocation))
        //                {
        //                    isHoveringSlot = true;
        //                    break;
        //                }
        //            }
        //        }
        //        if (isHoveringSlot) break;
        //    }

        //    pnlMain.Cursor = isHoveringSlot ? Cursors.Hand : Cursors.Default;
        //}
        private void PnlMain_MouseMove(object sender, MouseEventArgs e)
        {
            bool isHoveringSlot = false;
            Point logicalLocation = new Point(e.X - pnlMain.AutoScrollPosition.X, e.Y - pnlMain.AutoScrollPosition.Y);

            RackLayoutInfo hoveredSummaryRack = null;

            foreach (var rack in _rackLayouts)
            {
                if (rack.SummaryTextBounds != Rectangle.Empty &&
                    rack.SummaryTextBounds.Contains(logicalLocation))
                {
                    hoveredSummaryRack = rack;
                }

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

            pnlMain.Cursor = isHoveringSlot || hoveredSummaryRack != null
                ? Cursors.Hand
                : Cursors.Default;

            if (hoveredSummaryRack != null)
            {
                _hidePopupTimer.Stop(); // đang hover đúng chỗ -> huỷ lịch ẩn

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
                // ← THÊM: rời khỏi vùng tóm tắt -> lên lịch ẩn (không ẩn ngay để không giật
                // khi chuột đi ngang qua nhanh)
                if (_hoveredRackForPopup != null)
                    ScheduleHidePopup();
            }
        }
        // ── [SỰ KIỆN CHUỘT PHẢI & XỬ LÝ LOGIC NGHIỆP VỤ] ──────────────────────────

        private void ShowExportFormFromCanvas(Slot slotData)
        {
            try
            {
                // ✅ FIX: dùng đúng ExportFormSV (bản Canvas mới) thay vì ExportForm (bản cũ
                // không tương thích chữ ký MainStockSV và không đồng bộ lại Canvas sau khi xuất).
                var exportForm = new ExportFormSV(slotData, slotData.RackName, slotData.whname, this);
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

            itemDelete.Click += (s, e) =>
            {
                DeleteRackFromCanvas(rackInfo);
            };

            menu.Items.Add(itemDelete);
            menu.Show(pnlMain, mouseLocation); // Bung menu ngay vị trí chuột trên Canvas
        }

        private void DeleteRackFromCanvas(RackRenderInfo rackInfo)
        {
            var confirm = MessageBox.Show($"Xóa Rack [{rackInfo.RackName}] - Kho [{rackInfo.WarehouseName}]?\nThao tác không thể hoàn tác.", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                sqlProvider.ExecuteNonQuery(sqlProvider.B7R2_FCCdbb, "DELETE FROM Slot WHERE RackId = @RackId", new[] { new SqlParameter("@RackId", rackInfo.RackId) });
                sqlProvider.ExecuteNonQuery(sqlProvider.B7R2_FCCdbb, "DELETE FROM Rack WHERE RackId = @RackId", new[] { new SqlParameter("@RackId", rackInfo.RackId) });

                // Tính toán tải lại toàn bộ bản đồ Canvas mới ngay sau khi xóa
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
            // Reset và nạp lại nguồn dữ liệu LookUp để đồng bộ dữ liệu mới nhất
            InitPEditInput(forceRefresh: true);

            // Ép Canvas đọc lại Database và tính toán vẽ lại giao diện mới
            await LoadAllWarehouses();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            _currentFilterTemCode = "";
            _currentSelectedSlotData = null;
            pnlMain.Invalidate(); // Vẽ lại trạng thái ban đầu sạch sẽ
        }

        private void FilterSlotByTemCode(string temCode)
        {
            _currentFilterTemCode = temCode;
            pnlMain.Invalidate(); // Ép hệ thống gọi sự kiện Paint vẽ lại bản đồ theo bộ lọc mới ngay lập tức
        }

        // ── [SQL CONNECTIONS] ĐỌC DỮ LIỆU ĐỒNG BỘ TỪ CƠ SỞ DỮ LIỆU ─────────────────

        private List<RackRenderInfo> LoadRackRenderInfosSync()
        {
            // ✅ KIẾN TRÚC MỚI: Slot không còn lưu LotNo/TemCode trực tiếp.
            // LEFT JOIN thêm SlotLot để lấy toàn bộ Lot của từng Slot (1 Slot - N Lot).
            // Lưu ý: query này trả 1 dòng / (Slot x Lot) — cần gộp lại theo SlotId ở tầng C#.
            string query = @"
            SELECT 
                w.Name      AS WarehouseName,
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
                sl.ItemCode    AS LotItemCode,
                sl.Quantity    AS LotQuantity,
                sl.TemCode     AS LotTemCode,
                sl.QrData,
                sl.ImportDate  AS LotImportDate,
                sl.NgaySX,
                sl.SoPhieuTong,
                sl.MaPhieu
            FROM Warehouse w
            INNER JOIN Rack r    ON r.WarehouseId = w.WarehouseId
            LEFT  JOIN Slot s    ON s.RackId      = r.RackId
            LEFT  JOIN SlotLot sl ON sl.SlotId    = s.SlotId
            ORDER BY w.Name, r.RackName, s.SlotNumber, sl.LotNo";

            DataTable dt = sqlProvider.LoadData1(sqlProvider.B7R2_FCCdbb, query);
            var rackDict = new Dictionary<string, RackRenderInfo>();
            var slotDict = new Dictionary<int, Slot>(); // SlotId -> Slot, để gộp nhiều dòng SlotLot vào đúng 1 Slot

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

                if (row["SlotNumber"] is DBNull) continue; // rack chưa có slot nào

                int slotId = Convert.ToInt32(row["SlotId"]);

                if (!slotDict.TryGetValue(slotId, out var slot))
                {
                    slot = new Slot
                    {
                        // ✅ SlotId luôn phải gán đúng — StockService.GetSlotLots/ExportFromSlot/
                        // ClearSlot/SaveHistory đều thao tác theo SlotId này.
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

                    rackInfo.Slots.Add(new SlotRenderInfo
                    {
                        Slot = slot,
                        RackName = rackName,
                        WarehouseName = whName
                    });
                }

                // Slot trống (LEFT JOIN SlotLot không có dòng nào) -> LotNo sẽ là DBNull, bỏ qua
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

                // Tổng hợp ItemSummary sau khi Lots đã gộp xong (tránh cộng trùng do nhiều dòng SlotLot)
                foreach (var sr in info.Slots)
                {
                    var slot = sr.Slot;
                    if (!string.IsNullOrEmpty(slot.ItemCode) && slot.Quantity > 0)
                    {
                        if (info.ItemSummary.TryGetValue(slot.ItemCode, out var s))
                            info.ItemSummary[slot.ItemCode] = (s.Item1 + 1, s.Item2 + slot.Quantity);
                        else
                            info.ItemSummary[slot.ItemCode] = (1, slot.Quantity);
                    }
                }
            }

            return rackDict.Values.ToList();
        }

        // ── [UI CONTROLS LOGIC] GỢI Ý VÀ QUÉT MÃ QR CỦA PEDITINPUT ─────────────────

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
                        {
                            FilterSlotByTemCode(temCode);
                        }
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
                    {
                        FilterSlotByTemCode(row["TemCode"].ToString());
                    }
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

        // ── [NÚT NHẤN KHÁC TRÊN FORM] ───────────────────────────────────────────

        private void btnRegisterRack_Click(object sender, EventArgs e)
        {
            using (var form = new FormRegisterRack())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    string whName = form.whName;
                    string rackName = form.RackName;
                    int slotCount = form.SlotCount;
                    int slotCapacity = form.SlotCapacity;

                    var checkInfor = new CheckInfor();

                    if (checkInfor.IsWarehouseExists(whName))
                    {
                        var slots = new List<Slot>();
                        for (int i = 1; i <= slotCount; i++)
                        {
                            slots.Add(new Slot { SlotNumber = i, Capacity = slotCapacity, IsOccupied = false });
                        }
                        var rack = new Rack { Name = rackName, Slots = slots };
                        checkInfor.AddRackToExistingWarehouse(whName, rack);
                    }
                    else
                    {
                        var slots = new List<Slot>();
                        for (int i = 1; i <= slotCount; i++)
                        {
                            slots.Add(new Slot { SlotNumber = i, Capacity = slotCapacity, IsOccupied = false });
                        }
                        var rack = new Rack { Name = rackName, Slots = slots };
                        var warehouse = new Warehouse { Name = whName, Racks = new List<Rack> { rack } };
                        sqlProvider.SaveWarehouseToDatabase(warehouse);
                    }

                    _ = LoadAllWarehouses(); // Làm mới bản đồ Canvas sau khi đăng ký Rack thành công
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
                    {
                        // Gom tất cả SlotData từ danh sách SlotRenderInfo
                        list.AddRange(rack.RackData.Slots.Select(s => s.Slot));
                    }
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

        // ── [TỐI ƯU HỆ THỐNG] KÍCH HOẠT DOUBLE BUFFERED QUA REFLECTION ───────────
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
            // Nếu Form chưa nạp xong dữ liệu ở lần đầu tiên (hoặc đang minimized) thì bỏ qua
            if (!isFirstShown || this.WindowState == FormWindowState.Minimized) return;

            // Ép hệ thống Canvas tính toán lại tọa độ và độ rộng các Slot theo kích thước màn hình mới
            _ = LoadAllWarehouses();
        }

        private void btnDKMa_Click(object sender, EventArgs e)
        {
            using (var form = new FormInspectionConfig())
            {
                form.ShowDialog(this);
            }
        }

        private void btnHisCheck_Click(object sender, EventArgs e)
        {
            using (var form = new FormInspectionHistory())
            {
                form.ShowDialog(this);
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _rackPopup?.Close();
            _rackPopup?.Dispose();
            base.OnFormClosed(e);
        }
    }
}