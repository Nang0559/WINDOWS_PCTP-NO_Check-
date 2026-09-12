using System;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the three mutually exclusive bottom states of HVN_PGH:
    /// Lệch IFS, Ghép Lot and Sửa Số Lượng.
    ///
    /// Important: all three grids intentionally remain children of this same
    /// UserControl. Existing BringToFront() calls therefore keep the original
    /// three-state switching semantics without changing HVN_PGH business code.
    /// </summary>
    public sealed class PhieuBottomStateControl : XtraUserControl
    {
        public GridControl LechGrid { get; private set; }
        public GridControl GhepLotGrid { get; private set; }
        public GridControl SuaSlGrid { get; private set; }
        public GridView SuaSlView { get; private set; }

        public PhieuBottomStateControl()
        {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Moves the existing Designer-created grids into this boundary.
        /// The grid instances themselves are preserved so DataSource, views,
        /// event handlers and existing field references remain intact.
        /// </summary>
        public void Adopt(
            GridControl lechGrid,
            GridControl ghepLotGrid,
            GridControl suaSlGrid,
            GridView suaSlView)
        {
            if (lechGrid == null) throw new ArgumentNullException("lechGrid");
            if (ghepLotGrid == null) throw new ArgumentNullException("ghepLotGrid");
            if (suaSlGrid == null) throw new ArgumentNullException("suaSlGrid");
            if (suaSlView == null) throw new ArgumentNullException("suaSlView");

            LechGrid = lechGrid;
            GhepLotGrid = ghepLotGrid;
            SuaSlGrid = suaSlGrid;
            SuaSlView = suaSlView;

            // All three controls must share this immediate parent. This is the
            // key compatibility rule for the legacy BringToFront() switching.
            Reparent(LechGrid);
            Reparent(GhepLotGrid);
            Reparent(SuaSlGrid);

            // Keep the legacy z-order: SUASL is the last-added child, while
            // actual visibility/z-order is still controlled by HVN_PGH.
            Controls.SetChildIndex(LechGrid, 2);
            Controls.SetChildIndex(GhepLotGrid, 1);
            Controls.SetChildIndex(SuaSlGrid, 0);
        }

        private void Reparent(Control control)
        {
            Control parent = control.Parent;
            if (parent != null)
                parent.Controls.Remove(control);

            control.Dock = DockStyle.Fill;
            Controls.Add(control);
        }

        /// <summary>
        /// Explicit helper for callers that want to express the state switch
        /// through the boundary. Existing HVN_PGH code can continue using the
        /// grid fields and BringToFront() unchanged.
        /// </summary>
        public void ShowLech()
        {
            LechGrid.Visible = true;
            GhepLotGrid.Visible = false;
            SuaSlGrid.Visible = false;
            LechGrid.BringToFront();
        }

        public void ShowGhepLot()
        {
            LechGrid.Visible = false;
            GhepLotGrid.Visible = true;
            SuaSlGrid.Visible = false;
            GhepLotGrid.BringToFront();
        }

        public void ShowSuaSoLuong()
        {
            LechGrid.Visible = false;
            GhepLotGrid.Visible = false;
            SuaSlGrid.Visible = true;
            SuaSlGrid.BringToFront();
        }
    }
}
