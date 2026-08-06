using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP
{
    public partial class UpdateStockPOP : DevExpress.XtraEditors.XtraForm
    {
        private List<DataRow> selectedRows;
        public UpdateStockPOP(List<DataRow> selectedRows)
        {
            InitializeComponent();
            this.selectedRows = selectedRows;
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            // Cập nhật dữ liệu từ các TextBox vào tất cả các DataRow đã chọn
            foreach (var row in selectedRows)
            {
                row["SLCONLAI"] = txtSL.Text;
                //row["Column2"] = textBox2.Text;
                // Thêm các cập nhật khác tương ứng với các cột dữ liệu
            }

            // Đóng form sau khi cập nhật
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}