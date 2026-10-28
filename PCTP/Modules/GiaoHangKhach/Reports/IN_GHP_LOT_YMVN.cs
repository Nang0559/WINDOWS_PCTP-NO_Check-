using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using DevExpress.XtraReports.UI;

namespace PCTP.QRCODE_HVN.Report
{
    public partial class IN_GHP_LOT_YMVN : DevExpress.XtraReports.UI.XtraReport
    {
        /// <summary>
        /// true = Gear, false = Model. Caller đặt từ CustomerConfig.Delivery.CoGear
        /// (cùng contract với IN_GHP_LOT); không còn đọc trạng thái static của form legacy.
        /// </summary>
        public bool IsGear { get; set; }

        public IN_GHP_LOT_YMVN()
        {
            InitializeComponent();
           
        }
        
            private void Detail_AfterPrint(object sender, EventArgs e)
        {

        }

        private void IN_GHP_LOT_DesignerLoaded(object sender, DevExpress.XtraReports.UserDesigner.DesignerLoadedEventArgs e)
        {
            if (IsGear)
            {
                this.Model.Text = "Gear";
            }
            else
                this.Model.Text = "Model";
        }

        private void IN_GHP_LOT_BeforePrint(object sender, CancelEventArgs e)
        {
            if (IsGear)
            {
                this.Model.Text = "Gear";
            }
            else
                this.Model.Text = "Model";
        }
    }
}
