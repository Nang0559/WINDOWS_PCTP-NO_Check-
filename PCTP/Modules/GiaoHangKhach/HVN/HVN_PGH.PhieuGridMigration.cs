using System;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private PhieuGridControl _phieuGridControl;

        /// <summary>
        /// Phase 9A: move the existing Phiếu/Order grid visual tree under
        /// PhieuGridControl while keeping the original GridControl instance.
        ///
        /// Existing event handlers and binding code in HVN_PGH remain untouched.
        /// The legacy Designer fields are intentionally kept during this first
        /// migration step so rollback is trivial.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            MigratePhieuOrderGridToUserControl();
            base.OnLoad(e);
        }

        private void MigratePhieuOrderGridToUserControl()
        {
            if (_phieuGridControl != null || gridCtrDONHANG == null || sidePanel2 == null)
                return;

            int childIndex = sidePanel2.Controls.GetChildIndex(gridCtrDONHANG);

            _phieuGridControl = new PhieuGridControl();

            sidePanel2.Controls.Remove(gridCtrDONHANG);
            sidePanel2.Controls.Add(_phieuGridControl);
            sidePanel2.Controls.SetChildIndex(_phieuGridControl, childIndex);

            _phieuGridControl.AttachExistingLayout(gridCtrDONHANG);
        }
    }
}
