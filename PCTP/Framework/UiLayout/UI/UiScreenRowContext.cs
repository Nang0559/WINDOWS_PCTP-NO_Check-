using System;
using System.Data;
using System.Globalization;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// Read-only view of the row focused in a layout-driven screen, handed to a module's <see cref="UiScreenAction"/>.
    /// The framework knows nothing about the module's columns: the action names the column it needs
    /// (for example PurchaseOrderId, Status). Reading a column that is not in the data source returns null, never throws.
    /// </summary>
    public sealed class UiScreenRowContext
    {
        public static readonly UiScreenRowContext Empty = new UiScreenRowContext(null);

        private readonly DataRow _row;

        public UiScreenRowContext(DataRow row)
        {
            _row = row;
        }

        /// <summary>True when a data row is focused (and is not deleted/detached).</summary>
        public bool HasRow
        {
            get { return _row != null && _row.RowState != DataRowState.Deleted && _row.RowState != DataRowState.Detached; }
        }

        public bool HasColumn(string column)
        {
            return HasRow && !string.IsNullOrEmpty(column) && _row.Table.Columns.Contains(column);
        }

        /// <summary>Raw cell value, or null when there is no row, no such column, or the cell is NULL.</summary>
        public object GetValue(string column)
        {
            if (!HasColumn(column)) return null;
            object value = _row[column];
            return value == null || value == DBNull.Value ? null : value;
        }

        /// <summary>Cell text (invariant culture, trimmed), or null when there is no value.</summary>
        public string GetText(string column)
        {
            object value = GetValue(column);
            if (value == null) return null;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return text == null ? null : text.Trim();
        }
    }
}
