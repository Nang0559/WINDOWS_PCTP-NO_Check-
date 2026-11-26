using System;
using System.Windows.Forms;

namespace PCTP.Framework.UiLayout
{
    /// <summary>
    /// An extra toolbar button on a layout-driven screen (for example "Upload Excel PO…"). The framework only draws the
    /// button and reloads the grid afterwards; what the action does, and who may do it, stays in the owning module, whose
    /// service re-checks permission, Site scope and workflow state. Hiding the button is a usability aid, not a gate.
    /// </summary>
    public sealed class UiScreenAction
    {
        public string Caption { get; private set; }
        public int Width { get; private set; }
        /// <summary>Column the action reads from the focused row (null for actions that do not use a row).</summary>
        public string RowKeyColumn { get; private set; }
        /// <summary>True when the action acts on the focused row, so the screen enables the button per row.</summary>
        public bool IsRowAction { get { return _executeOnRow != null; } }
        private readonly Func<bool> _isAvailable;
        private readonly Func<IWin32Window, string, string> _execute;
        private readonly Func<UiScreenRowContext, bool> _isEnabledForRow;
        private readonly Func<IWin32Window, string, UiScreenRowContext, string> _executeOnRow;

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
        /// Action that works on the focused row (for example PO workflow steps Kiểm tra / Phát hành / Đóng / Hủy).
        /// The button is shown by <paramref name="isAvailable"/> (permission, a usability aid) and enabled per focused row by
        /// <paramref name="isEnabledForRow"/> (workflow state, also a usability aid: the service and SQL re-check both).
        /// </summary>
        /// <param name="rowKeyColumn">Column that identifies the row; the screen does not enable the button when it is empty.</param>
        /// <param name="execute">Receives the owner window, the screen's Site and the focused row. Same return/throw contract as above.</param>
        public UiScreenAction(string caption, int width, string rowKeyColumn, Func<bool> isAvailable,
            Func<UiScreenRowContext, bool> isEnabledForRow, Func<IWin32Window, string, UiScreenRowContext, string> execute)
        {
            if (string.IsNullOrWhiteSpace(caption)) throw new ArgumentException("Caption là bắt buộc.", "caption");
            if (string.IsNullOrWhiteSpace(rowKeyColumn)) throw new ArgumentException("rowKeyColumn là bắt buộc.", "rowKeyColumn");
            if (execute == null) throw new ArgumentNullException("execute");
            Caption = caption;
            Width = width < 60 ? 60 : width;
            RowKeyColumn = rowKeyColumn;
            _isAvailable = isAvailable;
            _isEnabledForRow = isEnabledForRow;
            _executeOnRow = execute;
        }

        public bool IsAvailable()
        {
            try { return _isAvailable == null || _isAvailable(); }
            catch (Exception) { return false; }
        }

        /// <summary>Whether the button is clickable for the focused row. Always true for actions that do not use a row.</summary>
        public bool IsEnabledFor(UiScreenRowContext row)
        {
            if (_executeOnRow == null) return true;
            try
            {
                if (row == null || !row.HasRow || row.GetValue(RowKeyColumn) == null) return false;
                return _isEnabledForRow == null || _isEnabledForRow(row);
            }
            catch (Exception) { return false; }
        }

        public string Execute(IWin32Window owner, string siteCode)
        {
            return Execute(owner, siteCode, UiScreenRowContext.Empty);
        }

        public string Execute(IWin32Window owner, string siteCode, UiScreenRowContext row)
        {
            if (_executeOnRow != null) return _executeOnRow(owner, siteCode, row ?? UiScreenRowContext.Empty);
            return _execute(owner, siteCode);
        }
    }
}
