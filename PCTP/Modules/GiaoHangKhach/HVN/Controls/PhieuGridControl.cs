using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.BandedGrid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the normal Phiếu/Order grid area of HVN_PGH.
    ///
    /// Phase 9A:
    /// - Owns the existing Order GridControl instance.
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
        /// The original GridControl after it has been attached.
        /// Exposed for the next extraction step so HVN_PGH can gradually stop
        /// owning the concrete grid field without changing the grid instance.
        /// </summary>
        public GridControl OrderGrid { get; private set; }

        /// <summary>
        /// The original BandedGridView after it has been attached.
        /// </summary>
        public BandedGridView OrderView
        {
            get { return OrderGrid != null ? OrderGrid.MainView as BandedGridView : null; }
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

            var grid = existingLayout as GridControl;
            if (grid == null)
                throw new System.ArgumentException(
                    "PhieuGridControl chỉ nhận DevExpress GridControl hiện có.",
                    nameof(existingLayout));

            if (existingLayout.Parent == this)
            {
                OrderGrid = grid;
                return;
            }

            Controls.Clear();

            existingLayout.Dock = DockStyle.Fill;
            Controls.Add(existingLayout);
            OrderGrid = grid;
        }
    }
}
