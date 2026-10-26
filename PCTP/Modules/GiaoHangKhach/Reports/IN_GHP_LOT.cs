using System;
using System.ComponentModel;


namespace PCTP.QRCODE_HVN.Report
{
    public partial class IN_GHP_LOT : DevExpress.XtraReports.UI.XtraReport
    {
        /// <summary>
        /// true = Gear, false = Model.
        /// Giá trị lấy từ CustomerConfig.Delivery.CoGear.
        /// </summary>
        public bool IsGear { get; set; }

        public IN_GHP_LOT()
        {
            InitializeComponent();
        }

        private void Detail_AfterPrint(object sender, EventArgs e)
        {
        }

        private void IN_GHP_LOT_DesignerLoaded(
            object sender,
            DevExpress.XtraReports.UserDesigner.DesignerLoadedEventArgs e)
        {
            UpdateModelCaption();
        }

        private void IN_GHP_LOT_BeforePrint(
            object sender,
            CancelEventArgs e)
        {
            UpdateModelCaption();
        }

        private void UpdateModelCaption()
        {
            Model.Text = IsGear ? "Gear" : "Model";
        }
    }
}
