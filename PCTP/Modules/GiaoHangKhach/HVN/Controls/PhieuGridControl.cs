using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the normal delivery/order area of HVN_PGH.
    ///
    /// Phase 9A:
    /// - Owns the existing Phiếu/Order visual tree.
    /// - Does not move event handlers into the control.
    /// - Does not change DataSource/binding code.
    /// - Existing controls are attached here first so rollback remains trivial.
    /// </summary>
    public class PhieuGridControl : XtraUserControl
    {
        public PhieuGridControl()
        {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Moves the existing Phiếu UI subtree into this UserControl without
        /// recreating any child controls. This intentionally preserves the
        /// existing control instances, event handlers and bindings.
        /// </summary>
        public void AttachExistingLayout(Control existingLayout)
        {
            if (existingLayout == null)
                return;

            if (existingLayout.Parent == this)
                return;

            Controls.Clear();

            existingLayout.Dock = DockStyle.Fill;
            Controls.Add(existingLayout);
        }
    }
}
