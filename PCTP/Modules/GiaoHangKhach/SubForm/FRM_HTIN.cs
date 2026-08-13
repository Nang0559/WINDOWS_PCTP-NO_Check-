using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH
{
    public partial class FRM_HTIN : DevExpress.XtraEditors.XtraForm
    {
        public int HinhThucIn { get; private set; } = 1;
        public FRM_HTIN()
        {
            InitializeComponent();
        }

        private void CMD_INPHIEUGIAO_Click(object sender, EventArgs e)
        {
            if (checkBKGX.Checked && !checkBNM.Checked)
                HinhThucIn = 3;
            else if (!checkBKGX.Checked && checkBNM.Checked)
                HinhThucIn = 2;
            else
                HinhThucIn = 1;

            this.Close();
        }
    }
}