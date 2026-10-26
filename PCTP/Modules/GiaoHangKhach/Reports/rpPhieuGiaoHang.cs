using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using DevExpress.XtraReports.UI;

namespace PCTP.QRCODE_HVN.Report
{
    public partial class rpPhieuGiaoHang : DevExpress.XtraReports.UI.XtraReport
    {
        public rpPhieuGiaoHang()
        {
            InitializeComponent();
        }
        public void SetGioHeader(string headerText)
        {
            // Tìm label header "Giờ" trong GroupHeader hoặc PageHeader
            // Tên control tùy Designer — thường là lblGio hoặc xrLabel1
            // Dùng FindControl để tìm theo tên
            var lbl = this.FindControl("lblGio", true) as XRLabel;
            if (lbl != null)
                lbl.Text = headerText;
        }
    }
}
