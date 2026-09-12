using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the DOCQRCODE / QR area of HVN_PGH.
    /// No QR business logic belongs here.
    /// </summary>
    public class DocQrControl : XtraUserControl
    {
        public DocQrControl()
        {
            Dock = DockStyle.Fill;
        }
    }
}
