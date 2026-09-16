using System;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        /// <summary>
        /// Disables the physical QR input while a mismatch confirmation dialog is active.
        /// The presenter remains the authoritative gate for scan processing.
        /// </summary>
        public void SetDocQrScanInputEnabled(bool enabled)
        {
            if (_docQrInputControl == null)
                return;

            _docQrInputControl.Enabled = enabled;

            if (enabled)
                _docQrInputControl.FocusInput();
        }

        /// <summary>
        /// Quantity correction is no longer part of the QR workflow.
        /// Keep the legacy designer panel hidden so it cannot be used accidentally.
        /// </summary>
        public void HideDocQrQuantityEditPanel()
        {
            try
            {
                PN_DOCQR_SUASL1.Visible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HideDocQrQuantityEditPanel] " + ex.Message);
            }

            if (_phieuBottomStateControl != null)
                _phieuBottomStateControl.HideSuaSoLuong();
        }
    }
}
