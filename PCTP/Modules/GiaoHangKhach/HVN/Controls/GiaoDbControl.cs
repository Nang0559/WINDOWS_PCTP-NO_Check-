using System;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Applications.Services;
using PCTP.Modules.GiaoHangKhach.SubForm;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the special-delivery (GiaoDB) area of HVN_PGH.
    /// Business orchestration remains in PhieuGiaoDbService / GiaoDbPresenter.
    /// </summary>
    public class GiaoDbControl : XtraUserControl
    {
        private PhieuService _phieuService;

        public GiaoDbControl()
        {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Supplies the service required by the legacy upload dialog.
        /// The control does not execute GiaoDB business logic; it only owns
        /// the dialog lifetime and modal UI boundary.
        /// </summary>
        public void Configure(PhieuService phieuService)
        {
            _phieuService = phieuService ?? throw new ArgumentNullException(nameof(phieuService));
        }

        /// <summary>
        /// Shows the GiaoDB upload/manual-entry dialog owned by this UI boundary.
        /// </summary>
        public DialogResult ShowUploadDialog(IWin32Window owner = null)
        {
            if (_phieuService == null)
                throw new InvalidOperationException("GiaoDbControl chưa được Configure(PhieuService).");

            using (var form = new FRM_UploadGiaoDB(_phieuService))
            {
                return owner == null
                    ? form.ShowDialog()
                    : form.ShowDialog(owner);
            }
        }
    }
}
