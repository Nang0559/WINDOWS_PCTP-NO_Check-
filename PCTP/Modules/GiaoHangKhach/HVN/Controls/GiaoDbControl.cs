using System;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.Applications.Services;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.GiaoHangKhach.SubForm;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
{
    /// <summary>
    /// UI boundary for the special-delivery (GiaoDB) workflow.
    ///
    /// Owns only GiaoDB-specific dialog lifetime/presentation concerns.
    /// Business orchestration remains in GiaoDbPresenter / PhieuService and
    /// persistence remains in PhieuGiaoDBRepository.
    /// </summary>
    public sealed class GiaoDbControl : XtraUserControl
    {
        private PhieuService _phieuService;

        public GiaoDbControl()
        {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Supplies the application service required by the legacy GiaoDB
        /// upload/manual dialog. The control does not execute the workflow.
        /// </summary>
        public void Configure(PhieuService phieuService)
        {
            _phieuService = phieuService ?? throw new ArgumentNullException(nameof(phieuService));
        }

        /// <summary>
        /// Shows the GiaoDB upload/manual-entry dialog and owns its lifetime.
        /// </summary>
        public DialogResult ShowUploadDialog(IWin32Window owner = null)
        {
            if (_phieuService == null)
                throw new InvalidOperationException(
                    "GiaoDbControl chưa được Configure(PhieuService).");

            using (var form = new FRM_UploadGiaoDB(_phieuService))
            {
                return owner == null
                    ? form.ShowDialog()
                    : form.ShowDialog(owner);
            }
        }
    }
}
