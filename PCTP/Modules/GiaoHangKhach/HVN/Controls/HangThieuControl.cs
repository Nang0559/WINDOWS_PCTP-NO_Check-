using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.BandedGrid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the legacy "Hàng thiếu" grid.
    /// The existing Designer grid is adopted so its configuration and
    /// compatibility references remain unchanged during the migration.
    /// </summary>
    public sealed class HangThieuControl : DevExpress.XtraEditors.XtraUserControl
    {
        private GridControl _grid;

        public HangThieuControl()
        {
            Dock = DockStyle.Fill;
            Name = "hangThieuControl";
        }

        public void Adopt(GridControl grid)
        {
            if (grid == null)
                return;

            Control parent = grid.Parent;
            if (parent != null)
                parent.Controls.Remove(grid);

            Controls.Clear();
            Controls.Add(grid);
            grid.Dock = DockStyle.Fill;
            _grid = grid;
        }

        public GridControl Grid
        {
            get { return _grid; }
        }

        public BandedGridView View
        {
            get { return _grid != null ? _grid.MainView as BandedGridView : null; }
        }
    }
}
