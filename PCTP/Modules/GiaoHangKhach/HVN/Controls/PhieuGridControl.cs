using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the normal Phiếu/Order grid area of HVN_PGH.
    ///
    /// Phase 9A:
    /// - Owns the existing Order grid control instance.
    /// - Does not move event handlers into the control.
    /// - Does not change DataSource/binding code.
    /// - The existing GridControl/GridView are re-parented intact.
    /// </summary>
    public class PhieuGridControl : XtraUserControl
    {
        public PhieuGridControl()
        {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Moves the existing Order GridControl into this UserControl without
        /// recreating it. This preserves the existing GridView, columns,
        /// row-style handlers and all current DataSource bindings.
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
