using DevExpress.Utils;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraPrinting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Framework.UiLayout
{
    /// <summary>Applies a layout region to a GridView and reads user changes (order, width) back from it.</summary>
    public static class UiGridConfigurator
    {
        public static void Apply(GridView view, IEnumerable<UiColumnLayout> layout, string groupColumn)
        {
            view.OptionsBehavior.Editable = false;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ColumnAutoWidth = false;
            view.PopulateColumns();
            foreach (GridColumn column in view.Columns)
            {
                column.Visible = false;
                column.OptionsColumn.AllowEdit = false;
                column.OptionsColumn.ReadOnly = true;
            }
            int index = 0;
            foreach (var item in layout.OrderBy(x => x.SequenceNo))
            {
                var column = view.Columns[item.ColumnName];
                if (column == null) continue;
                column.Caption = item.Caption;
                column.Visible = item.IsVisible;
                if (item.IsVisible) column.VisibleIndex = index++;
                if (item.Width.HasValue) column.Width = item.Width.Value;
                var align = ToAlignment(item.Align);
                column.AppearanceCell.TextOptions.HorizontalAlignment = align;
                column.AppearanceHeader.TextOptions.HorizontalAlignment = align;
                if (!string.IsNullOrEmpty(item.DisplayFormat))
                {
                    column.DisplayFormat.FormatType = item.DisplayFormat.StartsWith("N", StringComparison.Ordinal)
                        ? FormatType.Numeric : FormatType.DateTime;
                    column.DisplayFormat.FormatString = item.DisplayFormat;
                }
            }
            if (!string.IsNullOrEmpty(groupColumn) && view.Columns[groupColumn] != null)
            {
                view.Columns[groupColumn].GroupIndex = 0;
                view.ExpandAllGroups();
            }
        }

        /// <summary>Pulls column order and width the user changed directly in the grid into the layout items.</summary>
        public static void ReadBack(GridView view, IList<UiColumnLayout> items, string skipWidthFor = null)
        {
            var visible = view.Columns.Cast<GridColumn>().Where(c => c.Visible).OrderBy(c => c.VisibleIndex).ToList();
            int sequence = 1;
            foreach (var column in visible)
            {
                var item = items.FirstOrDefault(i => string.Equals(i.ColumnName, column.FieldName, StringComparison.OrdinalIgnoreCase));
                if (item == null) continue;
                item.SequenceNo = sequence++;
                if (column.Width >= 20 && column.Width <= 1000 &&
                    !string.Equals(item.ColumnName, skipWidthFor, StringComparison.OrdinalIgnoreCase)) item.Width = column.Width;
            }
            foreach (var item in items.Where(i => !visible.Any(c =>
                         string.Equals(c.FieldName, i.ColumnName, StringComparison.OrdinalIgnoreCase))).OrderBy(i => i.SequenceNo).ToList())
                item.SequenceNo = sequence++;
        }

        private static HorzAlignment ToAlignment(string align)
        {
            switch (align)
            {
                case "RIGHT": return HorzAlignment.Far;
                case "CENTER": return HorzAlignment.Center;
                default: return HorzAlignment.Near;
            }
        }
    }
}