using System;
using System.Windows.Forms;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private PhieuGridControl _phieuGridControl;

        /// <summary>
        /// Phase 9A: move the existing Phiếu/Order visual subtree under
        /// PhieuGridControl while keeping the original control instances.
        ///
        /// The original event handlers and binding code in HVN_PGH remain
        /// untouched. A later step can remove the legacy Designer ownership
        /// after the migration has been verified.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            MigratePhieuUiToUserControl();
            base.OnLoad(e);
        }

        private void MigratePhieuUiToUserControl()
        {
            if (_phieuGridControl != null || panelPhieu == null || PN_DOCQR_TOP == null)
                return;

            int childIndex = PN_DOCQR_TOP.Controls.GetChildIndex(panelPhieu);

            _phieuGridControl = new PhieuGridControl();

            PN_DOCQR_TOP.Controls.Remove(panelPhieu);
            PN_DOCQR_TOP.Controls.Add(_phieuGridControl);
            PN_DOCQR_TOP.Controls.SetChildIndex(_phieuGridControl, childIndex);

            _phieuGridControl.AttachExistingLayout(panelPhieu);
        }
    }
}
