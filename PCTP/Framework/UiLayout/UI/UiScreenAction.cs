using System;
using System.Data;
using System.Windows.Forms;

namespace PCTP.Framework.UiLayout
{
    /// <summary>The master-grid row that has focus when a row-aware <see cref="UiScreenAction"/> is evaluated or run.</summary>
    public sealed class UiScreenRowContext
    {
        public static readonly UiScreenRowContext None = new UiScreenRowContext(null);

        private readonly DataRow _row;

        public UiScreenRowContext(DataRow row) { _row = row; }

        public bool HasRow { get { return _row != null; } }

        /// <summary>Value of a column of the screen's data source, or null when there is no row, no such column or DBNull.</summary>
        public object GetValue(string column)
        {
            if (_row == null || string.IsNullOrEmpty(column) || !_row.Table.Columns.Contains(column) || _row.IsNull(column))
                return null;
            return _row[column];
        }

        public string GetText(string column)
        {
            object v = GetValue(column);
            return v == null ? null : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// An extra toolbar button on a layout-driven screen (for example "Upload Excel PO…"). The framework only draws the
    /// button and reloads the grid afterwards; what the action does, and who may do it, stays in the owning module, whose
    /// service re-checks permission, Site scope and workflow state. Hiding the button is a usability aid, not a gate.
    /// </summary>
    public sealed class UiScreenAction
    {
        public string Caption { get; private set; }
        public int Width { get; private set; }
        private readonly Func<bool> _isAvailable;
        private readonly Func<IWin32Window, string, string> _execute;
        private readonly Func<UiScreenRowContext, bool> _isEnabledForRow;
        private readonly Func<IWin32Window, string, UiScreenRowContext, string> _executeForRow;

        /// <summary>
        /// Column whose value identifies the row the action works on; after the action the grid is reloaded and the same
        /// row is focused again. Null for actions that do not use the selected row.
        /// </summary>
        public string RowKeyColumn { get; private set; }

        /// <param name="isAvailable">Null means always shown.</param>
        /// <param name="execute">
        /// Receives the owner window and the screen's Site. Returns a status line to show, or null when the user cancelled.
        /// Throw to report a failure; the form shows the message.
        /// </param>
        public UiScreenAction(string caption, int width, Func<bool> isAvailable, Func<IWin32Window, string, string> execute)
        {
            if (string.IsNullOrWhiteSpace(caption)) throw new ArgumentException("Caption là bắt buộc.", "caption");
            if (execute == null) throw new ArgumentNullException("execute");
            Caption = caption;
            Width = width < 60 ? 60 : width;
            _isAvailable = isAvailable;
            _execute = execute;
        }

        /// <summary>
        /// Row-aware action (for example PO workflow buttons). The button is enabled only while
        /// <paramref name="isEnabledForRow"/> accepts the focused row; <paramref name="execute"/> receives that row.
        /// Enabling is a usability aid; the module's service and SQL re-check everything when the action runs.
        /// </summary>
        public UiScreenAction(string caption, int width, string rowKeyColumn, Func<bool> isAvailable,
            Func<UiScreenRowContext, bool> isEnabledForRow, Func<IWin32Window, string, UiScreenRowContext, string> execute)
        {
            if (string.IsNullOrWhiteSpace(caption)) throw new ArgumentException("Caption là bắt buộc.", "caption");
            if (execute == null) throw new ArgumentNullException("execute");
            Caption = caption;
            Width = width < 60 ? 60 : width;
            RowKeyColumn = string.IsNullOrWhiteSpace(rowKeyColumn) ? null : rowKeyColumn.Trim();
            _isAvailable = isAvailable;
            _isEnabledForRow = isEnabledForRow;
            _executeForRow = execute;
        }

        public bool IsAvailable()
        {
            try { return _isAvailable == null || _isAvailable(); }
            catch (Exception) { return false; }
        }

        /// <summary>Whether the button is clickable for the focused row. Always true for actions that do not use a row.</summary>
        public bool IsEnabledFor(UiScreenRowContext row)
        {
            if (_isEnabledForRow == null) return true;
            try { return _isEnabledForRow(row ?? UiScreenRowContext.None); }
            catch (Exception) { return false; }
        }

        public string Execute(IWin32Window owner, string siteCode)
        {
            return Execute(owner, siteCode, UiScreenRowContext.None);
        }

        public string Execute(IWin32Window owner, string siteCode, UiScreenRowContext row)
        {
            if (_executeForRow != null) return _executeForRow(owner, siteCode, row ?? UiScreenRowContext.None);
            return _execute(owner, siteCode);
        }
    }
}
