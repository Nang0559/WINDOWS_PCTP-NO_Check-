using DevExpress.Utils.Extensions;
using DevExpress.XtraCharts.Native;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using DevExpress.XtraSplashScreen;
using PCTP.ClassSQL;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.QRCODE_HVN.Report;
using PCTP.Shared.Services;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
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
    // <summary>
    /// Form xuất kho: chọn số lượng xuất từ 1 Slot, hoặc để nguyên phần dư tại chỗ,
    /// hoặc chuyển phần dư sang Slot khác; kèm chức năng in phiếu xuất (preview, không lưu DB).
    ///
    /// GHI CHÚ TÍCH HỢP StockService:
    ///   - LoadEmptySlots / LoadSlotData: dùng StockService.GetAvailableSlotsForImport,
    ///     StockService.GetSlotLots, StockService.CreatePrintData.
    ///   - ExportToSameSlot -> StockService.ExportFromSlot + StockService.SyncSlotFromSplitResult
    ///     (đồng bộ lại object `slot` hiển thị trên form theo đúng dữ liệu vừa lưu DB).
    ///   - ExportToOtherSlot -> StockService.ExportAndMoveRemaining (xuất + gộp phần dư vào slot
    ///     đích + xoá slot nguồn trong 1 lần gọi); slot nguồn sau đó được xoá sạch trong bộ nhớ
    ///     bằng StockService.ClearSlotTemporarily (không đụng DB, DB đã được ExportAndMoveRemaining
    ///     xử lý).
    ///   - BtnPrint_Click -> StockService.BuildExportPreview (chỉ tính toán preview, KHÔNG lưu DB,
    ///     giữ đúng hành vi gốc) + StockService.GetProductNameByCode.
    ///   - Đã sửa lỗi nhỏ trong code gốc: biến `ProductName` (viết hoa, không tồn tại) khi build
    ///     PXuatINModel — nay dùng đúng biến `productName` lấy từ StockService.
    /// </summary>
    public partial class ExportFormSV : DevExpress.XtraEditors.XtraForm
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
        private MainStockSV _mainStockForm;


        private readonly ISlotService _slotService;
        private readonly IStockExportService _exportService;
        private readonly IPrintService _printService;
        
        private readonly string _phieuGiaoId;
        public ExportFormSV(Slot slot, string rackname, string whName, MainStockSV mainStockForm,
            ISlotService slotService, IStockExportService exportService, IPrintService printService,
            string phieuGiaoId = null)
        {
            this.whname = whName;
            this.rackName = rackname;
            this.slot = slot;
            this._phieuGiaoId = phieuGiaoId;
            _slotService = slotService;
            _exportService = exportService;
            _printService = printService;
            this.Text = "XUẤT KHO - FVN";
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
            btnExport = new SimpleButton { Text = "Pick hàng (Chờ giao)", Width = 130, Margin = new Padding(5) };
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
            string itemCode = lblItemCode.Text;
            int exportQty = Convert.ToInt32(spinExportQty.Value);

            var slots = _slotService.GetEmptySlots(itemCode, exportQty);

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

            // Load Lot mới nhất
            slot.Lots = _slotService.GetLots(slot.SlotId);

            var printData = _printService.CreatePrintData(slot.Lots);

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

                _mainStockForm?.OnSlotUpdated();

                SplashScreenManager.CloseForm();

                XtraMessageBox.Show(
                    "Đã pick hàng khỏi kệ — hàng đang ở trạng thái CHỜ GIAO.\n" +
                    "Nếu phát hiện lỗi trước khi xe rời kho, dùng màn hình \"Huỷ chờ giao\" để trả về sản xuất.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
            var result = _exportService.PickToChoGiao(new StockExportRequest
            {
                SlotId = slot.SlotId,
                Quantity = qty,
                ItemCode = slot.ItemCode,
                PhieuGiaoId = _phieuGiaoId
            });

            if (!result.IsOK) { MessageBox.Show(result.Message); return false; }

            // Đồng bộ object hiển thị — đọc lại từ DB thay vì tự sync tay
            slot.Lots = _slotService.GetLots(slot.SlotId);
            slot.Quantity = LotNoHelper.GetTotalQuantity(slot.Lots);
            slot.IsOccupied = slot.Quantity > 0;
            return true;
        }

        private bool ExportToOtherSlot(int qty)
        {
            if (listBoxSlots.SelectedItem == null) { MessageBox.Show("Vui lòng chọn vị trí."); return false; }

            var pickResult = _exportService.PickToChoGiao(new StockExportRequest
            {
                SlotId = slot.SlotId,
                Quantity = qty,
                ItemCode = slot.ItemCode,
                PhieuGiaoId = _phieuGiaoId
            });

            if (!pickResult.IsOK) { MessageBox.Show(pickResult.Message); return false; }

            if (pickResult.RemainingQuantity > 0)
            {
                int toSlotId = _slotService.GetSlotIdFromString(listBoxSlots.SelectedItem.ToString());
                _slotService.MoveLot(slot.SlotId, toSlotId, pickResult.RemainingLotNo);
            }

            _slotService.ClearSlotTemporarily(slot);
            return true;
        }

        private void BtnPrint_Click(object sender, EventArgs e)
        {
            int slXuat = Convert.ToInt32(spinExportQty.Value);
            if (slXuat <= 0) { XtraMessageBox.Show("Vui lòng nhập số lượng xuất."); return; }

            try
            {
                var dataSource = _printService.BuildExportPreview(
                    slot.SlotId, slot.SlotNumber, slXuat, slot.ItemCode, nguoiThucHien: "");

                var report = new RpInNhapKho { DataSource = dataSource };
                new ReportPrintTool(report).ShowPreviewDialog();
            }
            catch (InvalidOperationException ex)
            {
                XtraMessageBox.Show(ex.Message);
            }
        }

    }
}