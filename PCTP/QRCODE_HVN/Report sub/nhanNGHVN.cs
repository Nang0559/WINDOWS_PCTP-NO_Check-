using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using DevExpress.XtraReports.UI;

namespace PCTP.QRCODE_HVN.Report_sub
{
    public partial class nhanNGHVN : DevExpress.XtraReports.UI.XtraReport
    {
        public nhanNGHVN()
        {
            InitializeComponent();

            //Name = ReportNames.SubreportsName;
            //DisplayName = ReportNames.Subreports;
        }
       
        void Detail_BeforePrint(object sender, CancelEventArgs e)
        {
            if (xrSubreport1.ReportSource != null)
            {
                xrSubreport1.ApplyParameterBindings();
                xrSubreport1.ReportSource.ApplyFiltering();
                e.Cancel = xrSubreport1.ReportSource.RowCount == 0;
            }
        }
        void MasterReport_BeforePrint(object sender, CancelEventArgs e)
        {
            if (xrSubreport1.ReportSource != null)
                xrSubreport1.ReportSource.FillDataSource();
        }
    }
}
