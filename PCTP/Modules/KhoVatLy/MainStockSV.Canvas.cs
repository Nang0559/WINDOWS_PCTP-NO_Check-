
using DevExpress.XtraEditors;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.CanVas;
using PCTP.Modules.KhoVatLy.UCControls;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    public partial class MainStockSV
    {

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
                    var exportForm = new ExportFormSV(slot, slot.RackName, slot.whname, this, _slotService, _exportService, _printService, _waitForm);
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
                if (_rackPopup.Visible && _rackPopup.Bounds.Contains(Cursor.Position)) return;
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
                        string fullSummary = string.Join(" | ", rack.RackData.ItemSummary.Select(kvp => $"[{kvp.Key}: vị trí {kvp.Value.Item1}, SL {kvp.Value.Item2}]"));
                        string displayText = TruncateToWidth(g, fullSummary, summaryFont, summaryMaxWidth);
                        var summaryRect = new RectangleF(summaryStartX, rack.HeaderBounds.Y + 9, summaryMaxWidth, 18);
                        g.DrawString(displayText, summaryFont, Brushes.DimGray, summaryRect);
                        rack.SummaryTextBounds = Rectangle.Round(summaryRect);
                    }
                    else rack.SummaryTextBounds = Rectangle.Empty;

                    foreach (var slotLayout in rack.Slots)
                    {
                        Slot slot = slotLayout.SlotData;
                        if (inputTemCodes.Count > 0)
                        {
                            if (string.IsNullOrWhiteSpace(slot.TemCode)) continue;
                            var slotTemCodes = slot.TemCode.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(code => code.Split('-')[0].Trim());
                            if (!slotTemCodes.Any(code => inputTemCodes.Contains(code))) continue;
                        }

                        Brush slotBackground = Brushes.White;
                        Pen slotBorder = Pens.Black;
                        if (slot == _currentSelectedSlotData)
                        {
                            slotBackground = Brushes.LightCyan;
                            slotBorder = new Pen(Color.DeepSkyBlue, 2);
                        }
                        else if (slot.IsOccupied) slotBackground = Brushes.Orange;
                        else slotBackground = Brushes.LightGray;

                        g.FillRectangle(slotBackground, slotLayout.Bounds);
                        g.DrawRectangle(slotBorder, slotLayout.Bounds);

                        string headerSlotStr = slot.IsOccupied ? $"{slot.SlotNumber}-{slot.Capacity}:{slot.TemCode}" : $"Slot {slot.SlotNumber} - Trống";
                        if (headerSlotStr.Length > 12) headerSlotStr = headerSlotStr.Substring(0, 10) + "..";
                        g.DrawString(headerSlotStr, slotFont, Brushes.Black, slotLayout.Bounds.X + 2, slotLayout.Bounds.Y + 3);

                        if (slot.IsOccupied)
                        {
                            var distinctItemCodes = slot.Lots.Select(l => l.QRInfo?.ItemCode).Where(ic => !string.IsNullOrEmpty(ic)).Distinct().ToList();
                            string itemCodeStr;
                            if (distinctItemCodes.Count == 0) itemCodeStr = "";
                            else if (distinctItemCodes.Count == 1) itemCodeStr = distinctItemCodes[0].Length > 8 ? distinctItemCodes[0].Substring(0, 7) + ".." : distinctItemCodes[0];
                            else itemCodeStr = $"{distinctItemCodes.Count} mã hàng";
                            g.DrawString(itemCodeStr, boldSlotFont, Brushes.DarkRed, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 16);

                            string lotNoStr = !string.IsNullOrEmpty(slot.LotNo) ? (slot.LotNo.Length > 8 ? slot.LotNo.Substring(0, 7) + ".." : slot.LotNo) : "";
                            g.DrawString(lotNoStr, slotFont, Brushes.DimGray, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 27);
                            g.DrawString($"SL: {slot.Quantity}", boldSlotFont, Brushes.Blue, slotLayout.Bounds.X + 4, slotLayout.Bounds.Y + 38);
                        }
                    }
                }
            }
            g.ResetTransform();
        }

        private string TruncateToWidth(Graphics g, string text, Font font, float maxWidth)
        {
            if (g.MeasureString(text, font).Width <= maxWidth) return text;
            const string ellipsis = "...";
            int lo = 0, hi = text.Length;
            string result = ellipsis;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                string candidate = text.Substring(0, mid) + ellipsis;
                if (g.MeasureString(candidate, font).Width <= maxWidth) { result = candidate; lo = mid; }
                else hi = mid - 1;
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
                if (rack.SummaryTextBounds != Rectangle.Empty && rack.SummaryTextBounds.Contains(logicalLocation)) hoveredSummaryRack = rack;
                if (rack.Bounds.Contains(logicalLocation))
                    foreach (var slotLayout in rack.Slots)
                        if (slotLayout.Bounds.Contains(logicalLocation)) { isHoveringSlot = true; break; }
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
                    _rackPopup.ShowSummary($"Rack: {hoveredSummaryRack.RackData.RackName}", hoveredSummaryRack.RackData.ItemSummary, screenPoint);
                }
            }
            else if (_hoveredRackForPopup != null) ScheduleHidePopup();
        }

        private void ShowExportFormFromCanvas(Slot slotData)
        {
            try
            {
                var exportForm = new ExportFormSV(slotData, slotData.RackName, slotData.whname, this, _slotService, _exportService, _printService,_waitForm);
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

        private void DeleteRackFromCanvas(RackRenderInfo rackInfo)
        {
            var confirm = MessageBox.Show($"Xóa Rack [{rackInfo.RackName}] - Kho [{rackInfo.WarehouseName}]?\nThao tác không thể hoàn tác.", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            try
            {
                _rackService.Delete(rackInfo.RackId);
                _ = LoadAllWarehouses();
                MessageBox.Show("Xóa Rack thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa Rack:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SetDoubleBuffered(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }
    }
}
