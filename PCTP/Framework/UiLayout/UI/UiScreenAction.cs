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
        private readonly Func<bool> _isAvailable;
        private readonly Func<IWin32Window, string, string> _execute;

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

        public bool IsAvailable()
        {
            try { return _isAvailable == null || _isAvailable(); }
            catch (Exception) { return false; }
        }

        public string Execute(IWin32Window owner, string siteCode)
        {
            return _execute(owner, siteCode);
        }
    }
}
