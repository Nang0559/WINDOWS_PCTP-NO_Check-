using DevExpress.XtraEditors;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{

    public partial class FormEnterStock : DevExpress.XtraEditors.XtraForm
    {
        private CommonSlotFormUI commonUI;
        private MainStock _mainStockForm;

        public FormEnterStock(MainStock mainStockForm)
        {
            InitializeComponent();
            _mainStockForm = mainStockForm;
            InitializeForm();
        }

        private void InitializeForm()
        {
            this.Text = "Enter Item";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            // TextBox QRCode
            var txtQRCode = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Tahoma", 12)
            };
            txtQRCode.KeyDown += TxtQRCode_KeyDown;
            this.Controls.Add(txtQRCode);
            commonUI = new CommonSlotFormUI();
            Control layout = commonUI.BuildLayout(
                includeExportQty: false,
                btn1Handler: BtnEnter_Click,
                btn2Handler: BtnCheck_Click,
                cancelHandler: BtnCancel_Click
            );

            layout.Dock = DockStyle.Fill;
            this.Controls.Add(layout);
        }
        private void ShowQRCodeInfo(string qrCode)
        {
            var parts = qrCode.Split(':');

            if (parts.Length < 6)
            {
                XtraMessageBox.Show("QR code không đúng định dạng!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Gán thông tin vào UI chung
            commonUI.LblLotNo.Text = parts[0];
            commonUI.LblItemCode.Text = parts[1];
            //commonUI.LblSlotNumber.Text = parts[2];
            //commonUI.LblTemCode.Text = parts[3];
            //commonUI.LblItemCode.Text = parts[4];
            commonUI.LblTemCode.Text = parts[4];
            commonUI.LblQty.Text = parts[3];

            // Giả lập danh sách vị trí trống
            commonUI.ListBoxSlots.Items.Clear();
            commonUI.ListBoxSlots.Items.AddRange(new[] { "A01", "A02", "B03" });
        }

        private void TxtQRCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var txtBox = sender as TextBox;
                var qrCode = txtBox.Text.Trim();

                if (!string.IsNullOrEmpty(qrCode))
                {
                    ShowQRCodeInfo(qrCode); // Gọi hàm hiển thị thông tin QR
                }

                e.Handled = true;
                e.SuppressKeyPress = true; // Ngăn tiếng "ding" khi nhấn Enter
            }
        }

        private void BtnEnter_Click(object sender, EventArgs e)
        {
            if (commonUI.ListBoxSlots.SelectedItem == null)
            {
                XtraMessageBox.Show("Vui lòng chọn một vị trí để nhập kho.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedSlot = commonUI.ListBoxSlots.SelectedItem.ToString();

            QRCodeInfo qrInfo = new QRCodeInfo
            {
                WarehouseCode = commonUI.LblWhName.Text,
                ItemCode = commonUI.LblItemCode.Text,
                LotNo = commonUI.LblLotNo.Text,
                NgaySX = DateTime.Now.ToString("dd/MM/yyyy"),
                //ImportDate = DateTime.Now,
                Quantity = int.TryParse(commonUI.LblQty.Text, out int qty) ? qty : 0
            };

            OnSlotConfirmed(selectedSlot, qrInfo);
        }

        private void BtnCheck_Click(object sender, EventArgs e)
        {
            // Tùy bạn xử lý phần kiểm tra trước khi nhập kho
            MessageBox.Show("Chức năng kiểm tra chưa được triển khai.", "Thông báo");
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        public void OnSlotConfirmed(string selectedSlot, QRCodeInfo qrInfo)
        {
            var slotHelper = new SlotHelper();

            int slotId = slotHelper.GetSlotIDFromString(selectedSlot);

            if (slotId <= 0)
            {
                XtraMessageBox.Show("Không tìm thấy Slot.");
                return;
            }

            // Load Lot hiện tại
            List<LotInfo> currentLots =
                slotHelper.GetSlotLots(slotId);

            // Lot mới
            LotInfo newLot =
                LotNoHelper.CreateLot(qrInfo);

            // Merge
            List<LotInfo> mergedLots =
                LotNoHelper.MergeLotInfos(
                    currentLots,
                    new List<LotInfo> { newLot });

            // Lưu SlotLot
            slotHelper.SaveSlotLots(
                slotId,
                mergedLots,
                true);

            // Update Header
            slotHelper.UpdateSlotInfo(
                slotId,
                qrInfo.ItemCode,
                qrInfo.ImportDate ?? DateTime.Now,
                LotNoHelper.GetTotalQuantity(mergedLots));

            MessageBox.Show(
                "Cập nhật thành công!",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            _mainStockForm.LoadAllWarehouses();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
 }
