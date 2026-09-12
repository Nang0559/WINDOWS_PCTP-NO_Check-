using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the YMVN / MilkRun area of HVN_PGH.
    /// Business rules remain in PhieuYmvnService / YmvnPresenter.
    /// </summary>
    public class YmvnControl : XtraUserControl
    {
        public YmvnControl()
        {
            Dock = DockStyle.Fill;
        }
    }
}
