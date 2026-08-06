using PCTP.FuctionMain;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace PCTP.UserControls
{
    public partial class UCQr : UserControl
    {
        public UCQr()
        {
            InitializeComponent();
        }
        //QrInput QrIP = new QrInput();
        public List<VarQRInput> listQR = new List<VarQRInput>();
        private void txtQr_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(e.KeyChar == 13)
            {
                //listQR.Add( QrIP.TakeData(txtQr.Text));
            }
           
        }
    }
}
