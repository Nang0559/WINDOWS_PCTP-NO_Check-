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
    public partial class UF_NHAPLAI_NG : Form
    {
        public UF_NHAPLAI_NG()
        {
            InitializeComponent();
        }
        string LBL;
        DataGridView GW_NG1;
        
        public UF_NHAPLAI_NG(string LOT,DataGridView gwt)
        {
            InitializeComponent();
            LOT = LBL;
            GW_NG1 = gwt;
        }
        private void UF_NHAPLAI_NG_Load(object sender, EventArgs e)
        {
            //LBL_LOTNO.Text = LBL.Text;
            GW_NG.DataSource = GW_NG1.DataSource;
            foreach (DataGridViewColumn dc in GW_NG.Columns)
            {
                if (dc.Index.Equals(3))
                {
                    dc.ReadOnly = false;
                }
                else
                {
                    dc.ReadOnly = true;
                }
            }
        }

        private void GW_NG_DoubleClick(object sender, EventArgs e)
        {
            
        }

        

        private void GW_NG_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            
        }

        private void GW_NG_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void GW_NG_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            
        }

        private void GW_NG_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (Convert.ToInt32(GW_NG.Rows[e.RowIndex].Cells[2].Value) >= Convert.ToInt32(GW_NG.Rows[e.RowIndex].Cells[3].Value))
            {
                ALLVAR.LD_NG1 = GW_NG.Rows[e.RowIndex].Cells[4].Value.ToString().Trim();
                ALLVAR.a = int.Parse(GW_NG.Rows[e.RowIndex].Cells[3].Value.ToString().Trim());
                this.Close();
               
            }
            else
            {
                var result = MessageBox.Show("Số lượng Nhận lại : " + GW_NG.Rows[e.RowIndex].Cells[4].Value + " không được lớn hơn số lượng đã trả : " + GW_NG.Rows[e.RowIndex].Cells[2].Value  + " ! ", "Thông Báo FCC",
                             MessageBoxButtons.OK,
                             MessageBoxIcon.Error);
            }
        }
    }
}
