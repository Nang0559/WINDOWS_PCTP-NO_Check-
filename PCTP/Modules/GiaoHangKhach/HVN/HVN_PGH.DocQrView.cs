using System;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        /// <summary>
        /// Enables/disables the QR scanner input according to the current DocQR state.
        /// Kept in a separate partial class so the form's generated UI code remains untouched.
        /// </summary>
        public void SetDocQrScanInputEnabled(bool enabled)
        {
            if (_docQrInputControl == null)
                return;

            if (_docQrInputControl.InvokeRequired)
            {
                _docQrInputControl.BeginInvoke(new Action<bool>(SetDocQrScanInputEnabled), enabled);
                return;
            }

            _docQrInputControl.Enabled = enabled;
            if (enabled)
                _docQrInputControl.FocusInput();
        }

        /// <summary>
        /// Hides the quantity-edit panel when entering the DocQR scanning mode.
        /// </summary>
        public void HideDocQrQuantityEditPanel()
        {
            if (PN_DOCQR_SUASL1 == null)
                return;

            if (PN_DOCQR_SUASL1.InvokeRequired)
            {
                PN_DOCQR_SUASL1.BeginInvoke(new Action(HideDocQrQuantityEditPanel));
                return;
            }

            PN_DOCQR_SUASL1.Visible = false;
        }
    }
}
