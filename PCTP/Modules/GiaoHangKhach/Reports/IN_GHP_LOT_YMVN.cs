using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using DevExpress.XtraReports.UI;
using PCTP.QRCODE_HVN.YMN;

namespace PCTP.QRCODE_HVN.Report
{
    public partial class IN_GHP_LOT_YMVN : DevExpress.XtraReports.UI.XtraReport
    {
        public IN_GHP_LOT_YMVN()
        {
            InitializeComponent();
           
        }
        
            private void Detail_AfterPrint(object sender, EventArgs e)
        {

        }

        private void IN_GHP_LOT_DesignerLoaded(object sender, DevExpress.XtraReports.UserDesigner.DesignerLoadedEventArgs e)
        {
            if(GIAOHANGYMN.CHONYMVN==1)
            {
                this.Model.Text = "Gear";
            }
            else
                this.Model.Text = "Model";
        }

        private void IN_GHP_LOT_BeforePrint(object sender, CancelEventArgs e)
        {
            if (GIAOHANGYMN.CHONYMVN == 1)
            {
                this.Model.Text = "Gear";
            }
            else
                this.Model.Text = "Model";
        }
    }
}
