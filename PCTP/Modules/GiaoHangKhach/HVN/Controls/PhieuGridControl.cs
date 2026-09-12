using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the normal delivery/order area of HVN_PGH.
    /// Phase 9 initially introduces the boundary only; existing controls remain owned by HVN_PGH
    /// until each migration step is verified.
    /// </summary>
    public class PhieuGridControl : XtraUserControl
    {
        public PhieuGridControl()
        {
            Dock = DockStyle.Fill;
        }
    }
}
