using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.TraHangMP
{
    public partial class frm_TraMP : Form
    {
        public frm_TraMP()
        {
            InitializeComponent();
        }

        private void txt_Qrcode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                

                string QRFCC = txt_Qrcode.Text.Trim().ToUpper();
                string[] arrQRFCC = QRFCC.Split(':');

                if (arrQRFCC.Length == 4)
                {
                    fcc.Text = arrQRFCC[0] + ":" + arrQRFCC[3];
                }
                else
                {
                    //if()
                    hvn.Text = arrQRFCC[0];
                }

            }
        }
    }
}
