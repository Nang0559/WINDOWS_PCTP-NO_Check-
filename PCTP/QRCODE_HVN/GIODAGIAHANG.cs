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

namespace PCTP.QRCODE_HVN
{
    public partial class GIODAGIAHANG : DevExpress.XtraEditors.XtraForm
    {
        public GIODAGIAHANG()
        {
            InitializeComponent();
        }
        public static int GIOCHOSE; 
        private void GIODAGIAHANG_Load(object sender, EventArgs e)
        {
           // dateDH.DateTime = PHIEUGIAOHANG.NGAYXUAT_DT.DateTime;
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
           string  GIOC = dateDXH.EditValue.ToString();
            GIOCHOSE = int.Parse(GIOC.Substring(0, 2));
            
        }
    }
}