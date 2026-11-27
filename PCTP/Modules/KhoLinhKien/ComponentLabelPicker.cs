using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>Hộp chọn các tem cần in lại (tick từng dòng) để không phải in lại cả PO khi chỉ rách vài tem.</summary>
    public static class ComponentLabelPicker
    {
        /// <summary>Trả về bảng chỉ gồm các dòng được chọn; null khi người dùng hủy hoặc không chọn dòng nào.</summary>
        public static DataTable Pick(IWin32Window owner, DataTable source, string title, IList<KeyValuePair<string, string>> columns)
        {
            using (var form = new XtraForm
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(760, 520),
                MinimizeBox = false,
                MaximizeBox = true
            })
            {
                var grid = new GridControl { Dock = DockStyle.Fill };
                var view = new GridView(grid);
                grid.MainView = view;
                grid.ViewCollection.Add(view);
                view.OptionsBehavior.Editable = false;
                view.OptionsView.ShowAutoFilterRow = true;
                view.OptionsView.ColumnAutoWidth = false;
                view.OptionsSelection.MultiSelect = true;
                view.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
                grid.DataSource = source;

                foreach (DevExpress.XtraGrid.Columns.GridColumn c in view.Columns) c.Visible = false;
                int order = 0;
                foreach (var col in columns)
                {
                    var c = view.Columns[col.Key];
                    if (c == null) continue;
                    c.Caption = col.Value; c.Visible = true; c.VisibleIndex = order++;
                }
                view.BestFitColumns();

                var hint = new LabelControl { Dock = DockStyle.Top, Height = 28, Text = "Tick các tem cần in lại (Ctrl+A chọn tất cả), rồi bấm \"In lại\"." };
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
                var ok = new SimpleButton { Text = "In lại", Width = 110, Height = 30, DialogResult = DialogResult.OK };
                var cancel = new SimpleButton { Text = "Hủy", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
                bar.Controls.Add(ok); bar.Controls.Add(cancel);
                form.AcceptButton = ok; form.CancelButton = cancel;
                form.Controls.Add(grid); form.Controls.Add(hint); form.Controls.Add(bar);

                if (form.ShowDialog(owner) != DialogResult.OK) return null;
                int[] handles = view.GetSelectedRows();
                if (handles.Length == 0) return null;
                DataTable result = source.Clone();
                foreach (int h in handles.Where(h => h >= 0))
                {
                    DataRow r = view.GetDataRow(h);
                    if (r != null) result.ImportRow(r);
                }
                return result;
            }
        }
    }
}
