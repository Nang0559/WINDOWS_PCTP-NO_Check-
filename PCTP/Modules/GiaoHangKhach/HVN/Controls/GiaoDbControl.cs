using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the special-delivery (GiaoDB) area of HVN_PGH.
    /// Business orchestration remains in PhieuGiaoDbService / GiaoDbPresenter.
    /// </summary>
    public class GiaoDbControl : XtraUserControl
    {
        public GiaoDbControl()
        {
            Dock = DockStyle.Fill;
        }
    }
}
