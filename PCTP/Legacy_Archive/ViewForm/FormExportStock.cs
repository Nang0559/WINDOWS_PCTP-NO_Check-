using DevExpress.XtraEditors;
using PCTP.ClassSQL;
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
  
    public partial class FormExportStock : XtraForm
    {
        private CommonSlotFormUI commonUI;
        private readonly Slot _slotToExport;
        private readonly string _rackName;
        private readonly string _whName;
        private readonly Action _refreshMainView;
        private CommonSlotFormUI _commonUI;

        public FormExportStock(Slot slotToExport, string rackName, string whName, Action refreshCallback)
        {
            InitializeComponent();
            _slotToExport = slotToExport;
            _rackName = rackName;
            _whName = whName;
            _refreshMainView = refreshCallback;
            InitializeForm();
        }

        private void InitializeForm()
        {
            this.Text = "Export Item";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;

            //commonUI = new CommonSlotFormUI();
            //Control layout = commonUI.BuildLayout(
            //    includeExportQty: true, // true vì là xuất kho
            //    btn1Handler: BtnExport_Click,
            //    //btn2Handler: BtnPrint_Click,
            //    //cancelHandler: BtnCancel_Click
            //);

            //layout.Dock = DockStyle.Fill;
            //this.Controls.Add(layout);
        }

        // Hàm callback từ CommonSlotFormUI khi nhấn OK
        //public void OnSlotConfirmed(string selectedSlot, QRCodeInfo qrInfo, int exportQty)
        //{
        //    var slotHelper = new SlotHelper();
        //    var provider = new SQLPROVIDER();

        //    int slotIdOld = slotHelper.GetSlotID(_whName, _rackName, _slotToExport.SlotNumber);

        //    if (selectedSlot != null && selectedSlot != _slotToExport.SlotNumber.ToString())
        //    {
        //        // Xuất một phần và chuyển sang vị trí khác
        //        slotHelper.ClearSlot(slotIdOld);

        //        int remainingQty = _slotToExport.Quantity - exportQty;
        //        bool result = slotHelper.UpdateSlotInfo(
        //            selectedSlot,
        //            qrInfo.WarehouseCode,
        //            qrInfo.ItemCode,
        //            qrInfo.LotNo,
        //            qrInfo.ImportDate,
        //            remainingQty
        //        );

        //        if (result)
        //        {
        //            XtraMessageBox.Show("Xuất thành công sang vị trí mới!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        }
        //        else
        //        {
        //            XtraMessageBox.Show("Xuất thất bại!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //            return;
        //        }
        //    }
        //    else
        //    {
        //        // Xuất tại chỗ
        //        _slotToExport.Quantity -= exportQty;
        //        if (_slotToExport.Quantity == 0)
        //        {
        //            _slotToExport.IsOccupied = false;
        //        }

        //        provider.UpdateSlotAfterExport(_slotToExport, slotIdOld);
        //        XtraMessageBox.Show("Xuất thành công tại vị trí hiện tại!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //    }

        //    _refreshMainView?.Invoke();
        //    this.DialogResult = DialogResult.OK;
        //    this.Close();
        //}
    }
}