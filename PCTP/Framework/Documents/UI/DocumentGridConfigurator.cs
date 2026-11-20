using System.Collections.Generic;
using DevExpress.Utils;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>Applies resolved (validated) field metadata to a GridView. Unregistered columns are hidden.</summary>
    public static class DocumentGridConfigurator
    {
        public static void Apply(GridView view, IReadOnlyList<ResolvedField> fields)
        {
            view.PopulateColumns();
            foreach (GridColumn column in view.Columns)
            {
                column.Visible = false;
                column.OptionsColumn.AllowEdit = false;
                column.OptionsColumn.ReadOnly = true;
            }
            int index = 0;
            foreach (var field in fields)
            {
                var column = view.Columns[field.FieldCode];
                if (column == null || !field.IsVisible) continue;
                column.Caption = field.Caption;
                column.Visible = true;
                column.VisibleIndex = index++;
                if (field.Width.HasValue) column.Width = field.Width.Value;
                if (!string.IsNullOrEmpty(field.DisplayFormat))
                {
                    column.DisplayFormat.FormatType = ToFormatType(field.Editor);
                    column.DisplayFormat.FormatString = field.DisplayFormat;
                }
            }
        }

        public static FormatType ToFormatType(DocumentFieldType type)
        {
            switch (type)
            {
                case DocumentFieldType.Number: return FormatType.Numeric;
                case DocumentFieldType.Date:
                case DocumentFieldType.DateTime: return FormatType.DateTime;
                default: return FormatType.Custom;
            }
        }
    }
}
