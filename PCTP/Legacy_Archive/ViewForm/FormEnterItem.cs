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
    public partial class FormEnterItem : DevExpress.XtraEditors.XtraForm
    {
        private Panel panelSlotList;
        private ListBoxControl listBoxSlots;
        private SimpleButton btnOK;
        private SimpleButton btnCancel;
        private LabelControl lblTemCode, lblItemCode, lblLotNo, lblQty, lblrackName, lblSlotNumber, lblwhName;
        private GroupControl groupInfo, groupSlotList;
        private TableLayoutPanel contentPanel, mainLayout;
        private Slot slot;
        private QRCodeInfo codeInfo;
        private string rackName;
        private string whname;
        
        private SQLPROVIDER sqlpr = new SQLPROVIDER();
        private MainStock _mainStockForm;

        public FormEnterItem(MainStock mainStockForm)
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
                parsed = QRCodeParser.ParseQRCode(qrCode.ToUpper());
                parsed.RawQr = qrCode.ToUpper();
            }
            catch (FormatException fex)
            {
                XtraMessageBox.Show($"QR Code không hợp lệ!\n{fex.Message}",
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // ✅ Cảnh báo nếu bắn nhầm tem thùng
            if (!parsed.IsTongPhieu)
            {
                XtraMessageBox.Show("Vui lòng bắn tem TỔNG để nhập kho!",
                    "Sai loại tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Kiểm tra có phải mã hàng cần kiểm tra không
            var checkInfor = new CheckInfor();
            var config = checkInfor.GetInspectionConfig(parsed.ItemCode);

            if (config != null)
            {
                // ✅ Mở form kiểm tra TRƯỚC khi cho chọn slot
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
                    // PASS → tiếp tục bình thường
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
                    SlotHelper.ParseSlotString(
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

            var checkInfor = new CheckInfor();

            var emptySlots = checkInfor.GetEmptySlots(
                codeInfo.WarehouseCode + codeInfo.Unit,
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

            bool importSuccess;
            string selectedSlot = listBoxSlots.SelectedItem.ToString();

            if (IsDataSlotSelected(selectedSlot))
                importSuccess = ImportToDataSlot(selectedSlot, codeInfo);
            else
                importSuccess = ImportToEmptySlot(selectedSlot, codeInfo);

            if (importSuccess)
            {
                // Gọi cơ chế vẽ lại màn hình Canvas trung tâm từ CSDL mới cập nhật
                _mainStockForm?.OnSlotUpdated();
                this.Close();
            }
        }

        private bool ImportToEmptySlot(string selectedSlot, QRCodeInfo qrCodeInfo)
        {
            var slotHelper = new SlotHelper();

            SlotHelper.ParseSlotString(
                selectedSlot,
                out string whDest,
                out string rackDest,
                out int slotNumber,
                out int capacity);

            int slotId = slotHelper.GetSlotID(
                whDest,
                rackDest,
                slotNumber);

            if (slotId <= 0)
            {
                MessageBox.Show("Không tìm thấy Slot.");
                return false;
            }

            DateTime importDate = DateTime.Now;

            // Tạo Lot
            LotInfo lot = LotNoHelper.CreateLot(qrCodeInfo);

            // Chỉ cập nhật Slot
            bool result = slotHelper.UpdateSlotInfo(
                selectedSlot,
                qrCodeInfo.ItemCode,
                importDate,
                lot.Quantity);

            if (!result)
                return false;

            // Lưu SlotLot
            slotHelper.SaveSlotLots(
                slotId,
                new List<LotInfo>
                {
            lot
                });

            // Lưu lịch sử
            SlotHelper.SaveHistory(
                "Import",
                qrCodeInfo.ItemCode,
                lot,
                slotId);

            return true;
        }

        private bool ImportToDataSlot(string selectedSlot, QRCodeInfo qrCodeInfo)
        {
            var slotHelper = new SlotHelper();

            SlotHelper.ParseSlotString(
                selectedSlot,
                out string whDest,
                out string rackDest,
                out int slotNumber,
                out int capacity);

            int slotId = slotHelper.GetSlotID(
                whDest,
                rackDest,
                slotNumber);

            var localSlot = _mainStockForm.AllSlots.FirstOrDefault(s =>
                s.RackName == rackDest &&
                s.whname == whDest &&
                s.SlotNumber == slotNumber);

            if (localSlot == null)
            {
                MessageBox.Show(
                    $"Không tìm thấy dữ liệu ô chứa.\nWH:{whDest} Rack:{rackDest} Slot:{slotNumber}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }

            // Lot mới
            LotInfo importLot = LotNoHelper.CreateLot(qrCodeInfo);

            // Merge với các Lot đang có
            var existingLots =
                slotHelper.GetSlotLots(slotId);

            var mergedLots =
                LotNoHelper.MergeLotInfos(
                    existingLots,
                    new List<LotInfo> { importLot });

            int finalQty = mergedLots.Sum(x => x.Quantity);

            if (capacity > 0 && finalQty > capacity)
            {
                MessageBox.Show(
                    $"Tổng số lượng ({finalQty}) vượt quá sức chứa ({capacity}).");

                return false;
            }

            DateTime importDate = DateTime.Now;

            SlotHelper.BackupSlot(localSlot, out var backup);

            // Chỉ cập nhật Slot
            bool result = slotHelper.UpdateSlotInfo(
                selectedSlot,
                qrCodeInfo.ItemCode,
                importDate,
                finalQty);

            if (!result)
                return false;

            // Ghi lại toàn bộ SlotLot
            slotHelper.SaveSlotLots(
                slotId,
                mergedLots);

            // Lưu lịch sử
            SlotHelper.SaveHistory(
                "Import",
                qrCodeInfo.ItemCode,
                importLot,
                slotId);

            return true;
        }

        private bool IsDataSlotSelected(string selectedSlot)
        {
            string pattern = @"WH\s*:\s*(.*?)\s*-\s*Rack\s*:\s*(.*?)\s*-\s*Slot\s*:\s*(\d+)\s*-\s*Capacity\s*:\s*(\d+)\s*-\s*TemCode\s*:\s*(\S+)";
            var match = Regex.Match(selectedSlot, pattern);
            if (match.Success)
            {
                string temcode = match.Groups[5].Value.Trim();
                string wh = match.Groups[1].Value.Trim();
                return wh != "" && temcode != "";
            }
            return false;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}