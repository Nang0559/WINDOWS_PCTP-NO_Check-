using DevExpress.XtraReports.UI;

namespace PCTP.Modules.KhoLinhKien.Reports
{
    /// <summary>
    /// Tem thung linh kien (A4 ngang, 4 cot x 4 hang = 16 tem/trang). Mo bang Report Designer cua DevExpress de chinh bo cuc.
    /// DataSource: bang tu ComponentPoLabelData.PrepareCarton (cac cot DateText, SeqText, TimeText, QtyText + cot goc cua usp_FVN_ComponentPO_LabelData).
    /// </summary>
    public partial class ComponentCartonLabelReport : XtraReport
    {
        public ComponentCartonLabelReport()
        {
            InitializeComponent();
        }
    }
}
