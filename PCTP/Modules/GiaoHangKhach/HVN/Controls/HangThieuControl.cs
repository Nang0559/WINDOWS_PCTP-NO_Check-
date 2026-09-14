using System.Data;
using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.BandedGrid;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
{
    /// <summary>
    /// UI boundary for the legacy "Hàng thiếu" grid.
    /// Owns binding and presentation of the grid; business logic stays outside.
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
            if (grid == null) return;
            Control parent = grid.Parent;
            if (parent != null) parent.Controls.Remove(grid);
            Controls.Clear();
            Controls.Add(grid);
            grid.Dock = DockStyle.Fill;
            _grid = grid;
        }

        public GridControl Grid { get { return _grid; } }
        public BandedGridView View { get { return _grid != null ? _grid.MainView as BandedGridView : null; } }

        public void Bind(DataTable data)
        {
            if (_grid != null) _grid.DataSource = data;
        }

        public void ShowAndBringToFront()
        {
            Visible = true;
            BringToFront();
        }
    }
}
