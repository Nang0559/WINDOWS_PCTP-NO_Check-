using DevExpress.XtraEditors;
using PCTP.FuctionMain;
using PCTP.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.TEST
{
    public partial class FrmTEST : DevExpress.XtraEditors.XtraForm
    {
        public FrmTEST()
        {
            InitializeComponent();


        }
        
        public List<VarQRInput> listQR = new List<VarQRInput>();
        private void txtQrinput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                VarQRInput vrQR = new VarQRInput(txtQrinput.Text);
                listQR.Add(vrQR);
                gridControl1.DataSource = listQR.ToList();
                //dataGridView1.DataBindings();
            }
        }
    }
}