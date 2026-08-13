using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.Fuction;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq.SqlClient;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK
{
    public partial class FormRegisterRack : DevExpress.XtraEditors.XtraForm
    {
        private SQLPROVIDER sql = new SQLPROVIDER();
         
        
        CheckInfor checkInfor = new CheckInfor();
        public string whName => txtWarehouseName.Text.Trim();
        public string RackName => txtRackName.Text.Trim();
        
        public int RowCount => (int)spinRowCount.Value;
        public int ColumnCount => (int)spinColumnCount.Value;
        public int SlotCount => RowCount * ColumnCount;
        public int SlotCapacity => (int)spinCapacity.Value;
        public FormRegisterRack()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtWarehouseName.Text)) // ✅ thêm check tên kho
            {
                XtraMessageBox.Show("Vui lòng nhập tên Kho!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtRackName.Text))
            {
                XtraMessageBox.Show("Vui lòng nhập tên Rack!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (RowCount <= 0 || ColumnCount <= 0)
            {
                XtraMessageBox.Show("Số hàng và cột phải lớn hơn 0!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (spinCapacity.Value <= 0) // ✅ bắt buộc nhập capacity
            {
                XtraMessageBox.Show("Sức chứa mỗi Slot phải lớn hơn 0!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                spinCapacity.Focus();
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void txtWarehouseName_Leave(object sender, EventArgs e)
        {
            string name = txtWarehouseName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            if (checkInfor.IsWarehouseExists(whName))
                checkInfor.LoadWarehouseData(whName, cmbRack);
            else
                MessageBox.Show("Warehouse không tồn tại!");
        }
    }
}