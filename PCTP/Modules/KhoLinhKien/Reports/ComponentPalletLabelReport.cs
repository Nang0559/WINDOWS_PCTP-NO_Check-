using DevExpress.XtraReports.UI;

namespace PCTP.Modules.KhoLinhKien.Reports
{
    /// <summary>
    /// Phieu quan ly nhap linh kien PALET (A4 ngang, 1 phieu / trang). Mo bang Report Designer cua DevExpress de chinh bo cuc.
    /// DataSource: bang tu ComponentPoLabelData.PreparePallet (cac cot PalletText, DateText, QtyText, SeqText + cot goc cua usp_FVN_ComponentPO_PalletData).
    /// </summary>
    public partial class ComponentPalletLabelReport : XtraReport
    {
        public ComponentPalletLabelReport()
        {
            InitializeComponent();
        }
    }
}
