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
using PCTP.QRCODE_HVN.YMN;

namespace PCTP.YMN
{
    public partial class YMVN_CHONGIAO : DevExpress.XtraEditors.XtraForm
    {
        public YMVN_CHONGIAO()
        {
            InitializeComponent();
        }
        public static string MP_SP;
        private void simpleButton1_Click(object sender, EventArgs e)
        {
            
            MP_SP = YMVN_MP_SP.Properties.Items[YMVN_MP_SP.SelectedIndex].AccessibleName;
            this.Close();
            GIAOHANGYMN F_Giao_YMVN = new GIAOHANGYMN();
            F_Giao_YMVN.Show();
        }
    }
}