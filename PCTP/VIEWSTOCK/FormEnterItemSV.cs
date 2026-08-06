using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.XtraSplashScreen;
using PCTP.ClassSQL;
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace PCTP.VIEWSTOCK
{
    /// <summary>
    /// Form nhập kho hàng hóa: quét QR tem tổng -> (tuỳ ItemCode) kiểm tra qua FormInspection ->
    /// chọn Slot đích -> lưu.
    ///
    /// GHI CHÚ TÍCH HỢP StockService:
    ///   - Toàn bộ logic tìm slot trống, kiểm tra InspectionConfig, parse QR, lưu Lot/SlotLot,
    ///     cập nhật Slot, ghi lịch sử... đã chuyển vào StockService.
    ///   - Form không còn gọi trực tiếp CheckInfor / SlotHelper / LotNoHelper / QRCodeParser nữa.
    ///   - Đã bỏ ImportToEmptySlot / ImportToDataSlot / IsDataSlotSelected (logic gộp vào
    ///     StockService.ImportLotToSlot, tự xử lý đúng cho cả slot trống lẫn slot đã có hàng).
    ///   - _mainStockForm.AllSlots là cache hiển thị ở form cha (MainStock) — sau khi Import
    ///     thành công, form cha cần tự load lại dữ liệu (ví dụ qua StockService.GetRackRenderInfo)
    ///     trong OnSlotUpdated(), không chỉ vẽ lại từ cache cũ.
    /// </summary>
    public partial class FormEnterItemSV : DevExpress.XtraEditors.XtraForm
    {
        // ==== Controls (giữ theo đúng những gì bạn đã có; phần còn lại do Designer sinh) ====
        

        private Panel panelSlotList;
        private ListBoxControl listBoxSlots;
        private SimpleButton btnOK;
        private SimpleButton btnCancel;
        private LabelControl lblTemCode, lblItemCode, lblLotNo, lblQty, lblrackName, lblSlotNumber, lblwhName;
        private GroupControl groupInfo, groupSlotList;
        private TableLayoutPanel contentPanel, mainLayout;

        // ==== State ====
        private QRCodeInfo codeInfo;

        // ==== Dependencies ====
        private readonly StockService _stockService = new StockService();
        private readonly MainStockSV _mainStockForm;

        public FormEnterItemSV(MainStockSV mainStockForm)
        {
            InitializeComponent();
            InitializeForm();
            _mainStockForm = mainStockForm;
        }

        private void InitializeForm()
        {
            this.Text = "NHẬP KHO HÀNG HÓA - CANVAS UI";
            this.Size = new Size(600, 450);
            this.StartPosition = FormStartPosition.CenterParent;

            // Textbox đọc QRCode
            txtQRCode = new TextBox();
            txtQRCode.Dock = DockStyle.Top;
            txtQRCode.Font = new Font("Tahoma", 12);
            txtQRCode.KeyDown += TxtQRCode_KeyDown;
            this.Controls.Add(txtQRCode);
        }

        private void TxtQRCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string qrCode = txtQRCode.Text.Trim();
            txtQRCode.Clear();
            if (string.IsNullOrEmpty(qrCode)) return;

            QRCodeInfo parsed;
            try
            {
                parsed = _stockService.ParseQr(qrCode.ToUpper());
                parsed.RawQr = qrCode.ToUpper();
            }
            catch (FormatException fex)
            {
                XtraMessageBox.Show($"QR Code không hợp lệ!\n{fex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Cảnh báo nếu bắn nhầm tem thùng
            if (!parsed.IsTongPhieu)
            {
                XtraMessageBox.Show("Vui lòng bắn tem TỔNG để nhập kho!",
                    "Sai loại tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Kiểm tra có phải mã hàng cần kiểm tra không
            var config = _stockService.GetInspectionConfig(parsed.ItemCode);

            if (config != null)
            {
                // Mở form kiểm tra TRƯỚC khi cho chọn slot
                using (var formInspect = new FormInspection(parsed, config))
                {
                    var result = formInspect.ShowDialog(this);
                    if (result != DialogResult.OK)
                    {
                        XtraMessageBox.Show(
                            "Kiểm tra không đạt — Hàng không được nhập kho.",
                            "Từ chối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return; // dừng — không cho nhập
                    }
                    // PASS -> tiếp tục bình thường
                }
            }

            // Nhập kho bình thường
            ShowQRCodeInfo(parsed);
        }

        private bool ValidateQRCode(string qrCode)
        {
            return !string.IsNullOrEmpty(qrCode);
        }

        private LabelControl CreateLabel()
        {
            return new LabelControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Tahoma", 10),
                Padding = new Padding(3),
                AutoSizeMode = LabelAutoSizeMode.Vertical, // BẮT BUỘC để hiển thị nhiều dòng
                Appearance =
                {
                    TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap }
                }
            };
        }

        private void ShowQRCodeInfo(QRCodeInfo qrInfo)
        {
            // Xóa layout cũ nếu có
            if (mainLayout != null)
            {
                Controls.Remove(mainLayout);
                mainLayout.Dispose();
                mainLayout = null;
            }

            if (qrInfo == null)
            {
                XtraMessageBox.Show("Không có dữ liệu QR.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Lưu lại để dùng khi Import
            codeInfo = qrInfo;

            int top = 10;

            // =========================
            // Layout chính
            // =========================
            contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };

            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            // =========================
            // Group thông tin
            // =========================
            groupInfo = new GroupControl
            {
                Text = "Thông tin vị trí nhập",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(5)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            lblwhName = CreateLabel();
            lblrackName = CreateLabel();
            lblSlotNumber = CreateLabel();
            lblTemCode = CreateLabel();
            lblItemCode = CreateLabel();
            lblLotNo = CreateLabel();
            lblQty = CreateLabel();

            layout.Controls.Add(new LabelControl { Text = "WHName:" }, 0, 0);
            layout.Controls.Add(lblwhName, 1, 0);

            layout.Controls.Add(new LabelControl { Text = "Rack:" }, 0, 1);
            layout.Controls.Add(lblrackName, 1, 1);

            layout.Controls.Add(new LabelControl { Text = "Slot Number:" }, 0, 2);
            layout.Controls.Add(lblSlotNumber, 1, 2);

            layout.Controls.Add(new LabelControl { Text = "TemCode:" }, 0, 3);
            layout.Controls.Add(lblTemCode, 1, 3);

            layout.Controls.Add(new LabelControl { Text = "ItemCode:" }, 0, 4);
            layout.Controls.Add(lblItemCode, 1, 4);

            layout.Controls.Add(new LabelControl { Text = "LotNo:" }, 0, 5);
            layout.Controls.Add(lblLotNo, 1, 5);

            layout.Controls.Add(new LabelControl { Text = "Tồn kho:" }, 0, 6);
            layout.Controls.Add(lblQty, 1, 6);

            LoadSlotData(codeInfo);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            scrollPanel.Controls.Add(layout);
            groupInfo.Controls.Add(scrollPanel);

            // =========================
            // Danh sách Slot
            // =========================
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

            listBoxSlots.SelectedIndexChanged += (s, e) =>
            {
                if (listBoxSlots.SelectedItem == null)
                    return;

                try
                {
                    StockService.ParseSlotString(
                        listBoxSlots.SelectedItem.ToString(),
                        out string targetWh,
                        out string targetRack,
                        out int targetSlot,
                        out int capacity);

                    lblwhName.Text = targetWh;
                    lblrackName.Text = targetRack;
                    lblSlotNumber.Text = targetSlot.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            };

            // Danh sách slot trống/phù hợp cho ItemCode + số lượng cần nhập
            var emptySlots = _stockService.GetAvailableSlotsForImport(
                codeInfo.ItemCode,
                codeInfo.Quantity);

            if (emptySlots.Count == 0)
            {
                XtraMessageBox.Show(
                    "Không còn Slot trống nào phù hợp trong kho này!",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            foreach (var slot in emptySlots)
            {
                listBoxSlots.Items.Add(slot);
            }

            groupSlotList.Controls.Add(listBoxSlots);

            // =========================
            // Button
            // =========================
            btnOK = new SimpleButton
            {
                Text = "OK",
                Size = new Size(80, 30),
                Top = top,
                Left = 50
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new SimpleButton
            {
                Text = "Cancel",
                Size = new Size(80, 30),
                Top = top,
                Left = 150
            };
            btnCancel.Click += BtnCancel_Click;

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10)
            };

            bottomPanel.Controls.Add(btnCancel);
            bottomPanel.Controls.Add(btnOK);

            // =========================
            // Main Layout
            // =========================
            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            contentPanel.Controls.Add(groupInfo, 0, 0);
            contentPanel.Controls.Add(groupSlotList, 0, 1);

            mainLayout.Controls.Add(contentPanel, 0, 0);
            mainLayout.Controls.Add(bottomPanel, 0, 1);

            Controls.Add(mainLayout);
        }

        private void LoadSlotData(QRCodeInfo qrCodeInfo)
        {
            if (qrCodeInfo != null)
            {
                lblTemCode.Text = qrCodeInfo.WarehouseCode + qrCodeInfo.Unit;
                lblItemCode.Text = qrCodeInfo.ItemCode;
                lblLotNo.Text = qrCodeInfo.LotNo;
                lblQty.Text = qrCodeInfo.Quantity.ToString();
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (listBoxSlots.SelectedItem == null)
            {
                XtraMessageBox.Show("Vui lòng chọn một Slot!", "Chú ý",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedSlot = listBoxSlots.SelectedItem.ToString();

            // StockService.ImportLotToSlot tự xử lý cả 2 trường hợp:
            //  - Slot trống  -> thêm Lot mới
            //  - Slot đã có hàng cùng LotNo -> cộng dồn số lượng
            //  - Slot đã có hàng khác LotNo -> thêm Lot mới vào danh sách Lot của Slot
            // đồng thời tự kiểm tra sức chứa và ghi lịch sử nhập kho.
            var result = _stockService.ImportLotToSlot(codeInfo, selectedSlot);

            if (!result.IsOK)
            {
                XtraMessageBox.Show(result.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Báo cho form cha vẽ lại Canvas trung tâm từ CSDL mới cập nhật
            _mainStockForm?.OnSlotUpdated();
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}