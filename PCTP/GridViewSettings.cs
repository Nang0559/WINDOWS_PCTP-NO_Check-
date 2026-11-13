using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP
{
    /// <summary>
    /// Cấu hình chung cho mọi GridView của ứng dụng: Ctrl+C chỉ copy giá trị (không kèm tiêu đề cột).
    /// Gắn tập trung qua Application.Idle (giống WmsHelpOverlay) nên không cần sửa từng form.
    /// </summary>
    public static class GridViewSettings
    {
        private static readonly ConditionalWeakTable<GridControl, object> Hooked =
            new ConditionalWeakTable<GridControl, object>();

        private static bool _started;
        private static int _lastScanTick;

        public static void Start()
        {
            if (_started) return;
            _started = true;
            Application.Idle += Application_Idle;
        }

        private static void Application_Idle(object sender, EventArgs e)
        {
            // Giới hạn tần suất quét để không tốn CPU mỗi lần idle.
            int now = Environment.TickCount;
            if (unchecked(now - _lastScanTick) < 500) return;
            _lastScanTick = now;

            try
            {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
                {
                    if (form != null && !form.IsDisposed && !form.Disposing)
                        Scan(form);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GridViewSettings: " + ex);
            }
        }

        private static void Scan(Control root)
        {
            GridControl grid = root as GridControl;
            if (grid != null)
                Hook(grid);

            foreach (Control child in root.Controls)
                Scan(child);
        }

        private static void Hook(GridControl grid)
        {
            object marker;
            if (Hooked.TryGetValue(grid, out marker)) return;
            Hooked.Add(grid, new object());

            // View tạo sau (detail/pattern clone) cũng được áp dụng.
            grid.ViewRegistered += (s, e) => Apply(e.View);

            foreach (BaseView view in grid.ViewCollection.Cast<BaseView>().ToArray())
                Apply(view);
            Apply(grid.MainView);
        }

        public static void Apply(BaseView view)
        {
            GridView gv = view as GridView; // gồm cả BandedGridView / AdvBandedGridView
            if (gv == null) return;

            gv.OptionsClipboard.CopyColumnHeaders = DefaultBoolean.False;

            gv.KeyDown -= GridView_KeyDown;
            gv.KeyDown += GridView_KeyDown;
        }

        private static void GridView_KeyDown(object sender, KeyEventArgs e)
        {
            bool isCopy = (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.C)
                       || (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.Insert);
            if (!isCopy) return;

            GridView gv = sender as GridView;
            if (gv == null || gv.IsEditing) return; // đang sửa cell: để editor tự copy phần chữ đã chọn

            // Người dùng đã chọn nhiều ô/nhiều dòng: dùng copy mặc định (đã tắt tiêu đề cột).
            if (gv.OptionsSelection.MultiSelect)
            {
                bool nhieu = gv.OptionsSelection.MultiSelectMode == GridMultiSelectMode.CellSelect
                    ? gv.GetSelectedCells().Length > 1
                    : gv.GetSelectedRows().Length > 1;
                if (nhieu) return;
            }

            if (gv.FocusedColumn == null || !gv.IsDataRow(gv.FocusedRowHandle)) return;

            string text = gv.GetRowCellDisplayText(gv.FocusedRowHandle, gv.FocusedColumn) ?? string.Empty;
            try
            {
                Clipboard.SetDataObject(text, true);
                e.Handled = true;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard đang bị tiến trình khác giữ: bỏ qua, để người dùng bấm lại.
            }
        }
    }
}
