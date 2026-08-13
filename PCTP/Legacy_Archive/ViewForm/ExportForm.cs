using DevExpress.Utils.Extensions;
using DevExpress.XtraCharts.Native;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using DevExpress.XtraSplashScreen;
using PCTP.ClassSQL;
using PCTP.QRCODE_HVN.Report;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.RpIn;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace PCTP.VIEWSTOCK
{
    public partial class ExportForm : DevExpress.XtraEditors.XtraForm
    {
        private Slot slot;
        private string rackName;
        private string whname;
        private int currentQtyInSlot;
        private GroupControl groupInfo, groupSlotList;
        private LabelControl lblTemCode, lblItemCode, lblLotNo, lblQty, lblrackName, lblSlotNumber, lblwhName;
        private SpinEdit spinExportQty;
        private SimpleButton btnExport, btnPrint, btnCancel;
        private Panel panelSlotList;
        private ListBoxControl listBoxSlots;
        private string selectedSlotText;
        private TableLayoutPanel contentPanel;
        private MainStock _mainStockForm;
        SQLPROVIDER provider = new SQLPROVIDER();
        public ExportForm(Slot slot, string rackname, string whName, MainStock mainStockForm)
        {
            this.whname = whName;
            this.rackName = rackname;
            this.slot = slot;
            this.Text = "XUẤT KHO - CANVAS GRAPHICS";
            this.Size = new Size(450, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeControls();
            _mainStockForm = mainStockForm;
            LoadSlotData();
        }

        private void InitializeControls()
        {
            contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); // groupInfo
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); // groupSlotList

            // ==== groupInfo ====
            groupInfo = new GroupControl
            {
                Text = "Thông tin Slot",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Padding = new Padding(5)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            lblwhName = CreateLabel(); lblrackName = CreateLabel(); lblSlotNumber = CreateLabel();
            lblTemCode = CreateLabel(); lblItemCode = CreateLabel(); lblLotNo = CreateLabel(); lblQty = CreateLabel();

            spinExportQty = new SpinEdit
            {
                Dock = DockStyle.Fill,
                Properties = { MinValue = 1, MaxValue = 99999, IsFloatValue = false },
                Value = 1
            };
            spinExportQty.EditValueChanged += SpinExportQty_EditValueChanged;

            layout.Controls.Add(new LabelControl { Text = "WHName:" }, 0, 0); layout.Controls.Add(lblwhName, 1, 0);
            layout.Controls.Add(new LabelControl { Text = "Rack:" }, 0, 1); layout.Controls.Add(lblrackName, 1, 1);
            layout.Controls.Add(new LabelControl { Text = "Slot Number:" }, 0, 2); layout.Controls.Add(lblSlotNumber, 1, 2);
            layout.Controls.Add(new LabelControl { Text = "TemCode:" }, 0, 3); layout.Controls.Add(lblTemCode, 1, 3);
            layout.Controls.Add(new LabelControl { Text = "ItemCode:" }, 0, 4); layout.Controls.Add(lblItemCode, 1, 4);
            layout.Controls.Add(new LabelControl { Text = "LotNo:" }, 0, 5); layout.Controls.Add(lblLotNo, 1, 5);
            layout.Controls.Add(new LabelControl { Text = "Tồn kho:" }, 0, 6); layout.Controls.Add(lblQty, 1, 6);
            layout.Controls.Add(new LabelControl { Text = "SL xuất:" }, 0, 7); layout.Controls.Add(spinExportQty, 1, 7);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            scrollPanel.Controls.Add(layout);
            groupInfo.Controls.Add(scrollPanel);

            // ==== groupSlotList ====
            groupSlotList = new GroupControl
            {
                Text = "Danh sách vị trí trống phù hợp",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            listBoxSlots = new ListBoxControl
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                SelectionMode = SelectionMode.One
            };

            groupSlotList.Controls.Add(listBoxSlots);

            // ==== Bottom Panel ====
            btnExport = new SimpleButton { Text = "Xuất kho", Width = 100, Margin = new Padding(5) };
            btnExport.Click += BtnExport_Click;
            btnPrint = new SimpleButton { Text = "In phiếu", Width = 100, Margin = new Padding(5) };
            btnPrint.Click += BtnPrint_Click;
            btnCancel = new SimpleButton { Text = "Hủy", Width = 100, Margin = new Padding(5) };
            btnCancel.Click += (s, e) => { this.Close(); };

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10)
            };
            bottomPanel.Controls.Add(btnCancel);
            bottomPanel.Controls.Add(btnExport);
            bottomPanel.Controls.Add(btnPrint);

            // ==== Tổng Layout ====
            contentPanel.Controls.Add(groupInfo, 0, 0);
            contentPanel.Controls.Add(groupSlotList, 0, 1);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            mainLayout.Controls.Add(contentPanel, 0, 0);
            mainLayout.Controls.Add(bottomPanel, 0, 1);

            this.Controls.Add(mainLayout);

            UpdateLayoutProportion(listBoxSlots.Items.Count > 0);
        }

        private void UpdateLayoutProportion(bool hasEmptySlots)
        {
            if (hasEmptySlots)
            {
                contentPanel.RowStyles[0].Height = 60f;
                contentPanel.RowStyles[1].Height = 40f;
                groupSlotList.Visible = true;
            }
            else
            {
                contentPanel.RowStyles[0].Height = 100f;
                contentPanel.RowStyles[1].Height = 0f;
                groupSlotList.Visible = false;
            }
            contentPanel.ResumeLayout();
        }

        private void LoadEmptySlots()
        {
            string warehouseCode = lblwhName.Text;
            string itemCode = lblItemCode.Text;
            int exportQty = Convert.ToInt32(spinExportQty.Value);
            var checkInfor = new CheckInfor();
            var slots = checkInfor.GetEmptySlots(warehouseCode, itemCode, exportQty);

            listBoxSlots.Items.Clear();
            foreach (var s in slots)
            {
                listBoxSlots.Items.Add(s);
            }

            UpdateLayoutProportion(slots.Count > 0);
        }

        private void SpinExportQty_EditValueChanged(object sender, EventArgs e)
        {
            int exportQty = Convert.ToInt32(spinExportQty.Value);
            if (exportQty < currentQtyInSlot)
            {
                LoadEmptySlots();
            }
            else
            {
                UpdateLayoutProportion(false);
            }
        }

        private LabelControl CreateLabel()
        {
            return new LabelControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 10),
                Padding = new Padding(3),
                AutoSizeMode = LabelAutoSizeMode.Vertical,
                Appearance = { TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap } }
            };
        }

        private void LoadSlotData()
        {
            if (slot == null)
                return;

            var slotHelper = new SlotHelper();

            // Load Lot mới nhất
            slot.Lots = slotHelper.GetSlotLots(slot.SlotId);

            var printData = LotNoHelper.CreatePrintData(
                slot.Lots);

            lblItemCode.Text = printData.ItemCode;

            lblQty.Text = printData.Quantity.ToString();

            lblLotNo.Text = printData.LotNo;

            lblTemCode.Text = printData.TemCode;

            currentQtyInSlot = printData.Quantity;
            spinExportQty.Properties.MaxValue = printData.Quantity;

            lblwhName.Text = whname;
            lblrackName.Text = rackName;
            lblSlotNumber.Text = slot.SlotNumber.ToString();
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(spinExportQty.Text, out int soLuongXuat) || soLuongXuat <= 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng hợp lệ.");
                return;
            }

            if (soLuongXuat > slot.Quantity)
            {
                MessageBox.Show("Số lượng vượt quá tồn kho hiện tại.");
                return;
            }

            bool exportSuccess;

            if (groupSlotList.Visible && listBoxSlots.SelectedItem != null)
            {
                string selectedSlot = listBoxSlots.SelectedItem.ToString();
                if (IsSameSlotSelected(selectedSlot))
                {
                    exportSuccess = ExportToSameSlot(soLuongXuat);
                }
                else
                {
                    exportSuccess = ExportToOtherSlot(soLuongXuat);
                }
            }
            else
            {
                exportSuccess = ExportToSameSlot(soLuongXuat);
            }

            if (exportSuccess)
            {
                SplashScreenManager.ShowForm(this, typeof(WaitFormExp), true, true, false);
                SplashScreenManager.Default.SetWaitFormCaption("Đang cập nhật thông tin kho...");

                // Đồng bộ trực tiếp dữ liệu ảo lên form nền Canvas và ép vẽ lại giao diện
                _mainStockForm?.OnSlotUpdated();

                SplashScreenManager.CloseForm();
                this.Close();
            }
        }

        private bool IsSameSlotSelected(string selectedSlot)
        {
            string pattern = @"WH\s*:\s*(.*?)\s*-\s*Rack\s*:\s*(.*?)\s*-\s*Slot\s*:\s*(\d+)";
            var match = Regex.Match(selectedSlot, pattern);
            if (match.Success)
            {
                string wh = match.Groups[1].Value.Trim();
                string rack = match.Groups[2].Value.Trim();
                int slotNumber = int.Parse(match.Groups[3].Value.Trim());

                return wh == whname && rack == rackName && slotNumber == slot.SlotNumber;
            }
            return false;
        }

        private bool ExportToSameSlot(int qty)
        {
            var slotHelper = new SlotHelper();

            int slotId = slot.SlotId;

            // Load dữ liệu mới nhất
            var currentLots = slotHelper.GetSlotLots(slotId);

            // Tách Lot
            var result = LotNoHelper.SubtractLots(currentLots, qty);

            var remainLots = result.RemainingLots;
            var exportLots = result.ExportLots;

            // Lưu lại SlotLot và tự cập nhật bảng Slot
            slotHelper.SaveSlotLots(
                slotId,
                remainLots,
                true);

            // Đồng bộ object đang hiển thị
            slot.Lots = remainLots;
            slot.Quantity = remainLots.Sum(x => x.Quantity);
            slot.IsOccupied = slot.Quantity > 0;

            if (remainLots.Any())
            {
                slot.ItemCode = remainLots.First().QRInfo?.ItemCode;
                slot.ImportDate = remainLots.Max(x => x.QRInfo?.ImportDate);
            }
            else
            {
                slot.ItemCode = null;
                slot.ImportDate = null;
            }

            // Lưu lịch sử xuất
            foreach (var lot in exportLots)
            {
                SlotHelper.SaveHistory(
                    "Export",
                    lot.QRInfo?.ItemCode,
                    lot,
                    slotId);
            }

            return true;
        }

        private bool ExportToOtherSlot(int qty)
        {
            if (listBoxSlots.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn vị trí để chuyển phần hàng còn lại.");
                return false;
            }

            string itemCode = slot.ItemCode;
            string selectedSlotText = listBoxSlots.SelectedItem.ToString();

            SlotHelper.ParseSlotString(
                selectedSlotText,
                out string whDest,
                out string rackDest,
                out int slotNumber,
                out int capacity);

            var slotHelper = new SlotHelper();

            int slotIdOld = slot.SlotId;
            int slotIdDest = slotHelper.GetSlotID(
                whDest,
                rackDest,
                slotNumber);

            //--------------------------------------------------
            // Load dữ liệu mới nhất
            //--------------------------------------------------

            List<LotInfo> sourceLots =
                slotHelper.GetSlotLots(slotIdOld);

            List<LotInfo> destLots =
                slotHelper.GetSlotLots(slotIdDest);

            //--------------------------------------------------
            // Tách Lot
            //--------------------------------------------------

            LotSplitResult split =
                LotNoHelper.SubtractLots(
                    sourceLots,
                    qty);

            List<LotInfo> exportLots = split.ExportLots;
            List<LotInfo> remainLots = split.RemainingLots;

            //--------------------------------------------------
            // Ghép phần còn lại vào Slot mới
            //--------------------------------------------------

            List<LotInfo> mergedLots =
                LotNoHelper.MergeLotInfos(
                    destLots,
                    remainLots);

            int finalQty =
                LotNoHelper.GetTotalQuantity(mergedLots);

            if (capacity > 0 && finalQty > capacity)
            {
                MessageBox.Show(
                    $"Không thể chuyển. Tổng số lượng ({finalQty}) vượt quá sức chứa ({capacity}).");

                return false;
            }

            //--------------------------------------------------
            // Lưu Slot đích
            //--------------------------------------------------

            slotHelper.SaveSlotLots(
                slotIdDest,
                mergedLots,
                true);

            //--------------------------------------------------
            // Xóa Slot nguồn
            //--------------------------------------------------

            slotHelper.ClearSlot(slotIdOld);

            //--------------------------------------------------
            // Đồng bộ object trên màn hình
            //--------------------------------------------------

            slot.Lots.Clear();
            slot.Quantity = 0;
            slot.ItemCode = null;
            slot.ImportDate = null;
            slot.IsOccupied = false;

            //--------------------------------------------------
            // History Export
            //--------------------------------------------------

            foreach (var lot in exportLots)
            {
                SlotHelper.SaveHistory(
                    "Export",
                    itemCode,
                    lot,
                    slotIdOld,
                    null);
            }

            //--------------------------------------------------
            // History Move
            //--------------------------------------------------

            foreach (var lot in remainLots)
            {
                SlotHelper.SaveHistory(
                    "Move",
                    itemCode,
                    lot,
                    slotIdOld,
                    slotIdDest);
            }

            return true;
        }

        private void BtnPrint_Click(object sender, EventArgs e)
        {
            int slXuat = Convert.ToInt32(spinExportQty.Value);

            if (slXuat <= 0)
            {
                XtraMessageBox.Show("Vui lòng nhập số lượng xuất.");
                return;
            }

            var slotHelper = new SlotHelper();

            // Luôn lấy dữ liệu mới nhất
            slot.Lots = slotHelper.GetSlotLots(slot.SlotId);

            int tongSoLuong = LotNoHelper.GetTotalQuantity(slot.Lots);

            if (slXuat > tongSoLuong)
            {
                XtraMessageBox.Show("Số lượng xuất lớn hơn tồn kho.");
                return;
            }

            //--------------------------------------------------------
            // Tách Lot
            //--------------------------------------------------------

            LotSplitResult split =
                LotNoHelper.SubtractLots(
                    slot.Lots,
                    slXuat);

            string productName =
                provider.GetProductNameByCode(slot.ItemCode);

            var exportPrint =
                LotNoHelper.CreatePrintData(
                    split.ExportLots);

            var remainPrint =
                LotNoHelper.CreatePrintData(
                    split.RemainingLots);

            //--------------------------------------------------------
            // Datasource
            //--------------------------------------------------------

            List<PXuatINModel> dataSource = new List<PXuatINModel>();

        

            //--------------------------------------------------------
            // Phiếu xuất
            //--------------------------------------------------------

            dataSource.Add(new PXuatINModel
            {
                LoaiPhieu = "PHIẾU XUẤT",

                Ca = "",

                SoThuTuXe = slot.SlotNumber.ToString(),

                TenSanPham = ProductName,

                MaSanPham = slot.ItemCode,

                LotNo = exportPrint.LotNo,

                SoLuong = exportPrint.Quantity,

                CheckTem = exportPrint.TemCode,

                NguoiThucHien = "",

                QrData = exportPrint.QrData,

                Ngay = DateTime.Now.ToString("dd/MM"),

                Gio = DateTime.Now.ToString("HH:mm"),

                SoLuongXuat = exportPrint.Quantity,

                NguoiXuat = "",

                SoLuongTon = remainPrint.Quantity
            });

            //--------------------------------------------------------
            // Phiếu nhập lại
            //--------------------------------------------------------

            if (remainPrint.Quantity > 0)
            {
                dataSource.Add(new PXuatINModel
                {
                    LoaiPhieu = "PHIẾU NHẬP LẠI KHO",

                    Ca = "",

                    SoThuTuXe = slot.SlotNumber.ToString(),

                    TenSanPham = ProductName,

                    MaSanPham = slot.ItemCode,

                    LotNo = remainPrint.LotNo,

                    SoLuong = remainPrint.Quantity,

                    CheckTem = remainPrint.TemCode,

                    NguoiThucHien = "",

                    QrData = remainPrint.QrData,

                    Ngay = DateTime.Now.ToString("dd/MM"),

                    Gio = DateTime.Now.ToString("HH:mm"),

                    SoLuongXuat = exportPrint.Quantity,

                    NguoiXuat = "",

                    SoLuongTon = remainPrint.Quantity
                });
            }

            //--------------------------------------------------------
            // Preview
            //--------------------------------------------------------

            RpInNhapKho report = new RpInNhapKho();
            report.DataSource = dataSource;

            new ReportPrintTool(report)
                .ShowPreviewDialog();
        }
    }
}