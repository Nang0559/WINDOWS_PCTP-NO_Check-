using DevExpress.XtraEditors;
using PCTP.DAL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PCTP.Models;
using DevExpress.XtraBars.Docking2010;

namespace PCTP.QRCODE_HVN.PGH.FuctionQR
{
    public partial class UF_QRCodeERR : DevExpress.XtraEditors.XtraForm
    {
        public UF_QRCodeERR()
        {
            InitializeComponent();
            DanhSachQr = DAL.sp_loadQRCode();
            GT_QR.DataSource = DanhSachQr;
            SetBT();
            UIP_BT.Images = imageCollection1.Images[0];
            dateEdit1.DateTime = DateTime.Now;
        }
        List<QrcodeModels> _DanhSachQr;
        QrcodeDAL DAL = new QrcodeDAL();
        QrcodeModels _QrSelect;
        public List<QrcodeModels> DanhSachQr
        {
            get
            {
                return _DanhSachQr;
            }

            set
            {
                _DanhSachQr = value;

            }
        }
        private void SetBT()
        {
            //if (DanhSachQr.Count == 0)
                //UIP_BT.Hide();
          
            
        }

        private void UIP_BT_Click(object sender, EventArgs e)
        {
            //string tag = ((UIP_BT)e.Button).Caption.ToString();
            //switch (tag)
            //{
            //if (UIP_BT. == "CUT")
            //    XtraMessageBox.Show("Click CUT");
            //if (UIP_BT.Tag == "PASTE")
            //    XtraMessageBox.Show("Click Paste");
        }

        private void UIP_BT_ButtonClick(object sender, DevExpress.XtraBars.Docking2010.ButtonEventArgs e)
        {
            string tag = ((WindowsUIButton)e.Button).Caption.ToString();
            switch (tag)
            {
                case "CUT":
                    XtraMessageBox.Show("Click CUT");
                    break;
                case "PASTE":
                    XtraMessageBox.Show("Click Paste");
                    break;
            }
        }
    }
    
}