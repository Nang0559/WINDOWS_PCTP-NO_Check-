using System;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
{
    /// <summary>
    /// Owns DOC QR scan input presentation only. Scan/business processing stays outside this control.
    /// </summary>
    public sealed class DocQrInputControl : XtraUserControl
    {
        private TextBox _input;

        public event EventHandler<string> Submitted = delegate { };

        public string Text => _input == null ? string.Empty : _input.Text;

        public void Adopt(TextBox input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (_input != null) return;

            _input = input;
            _input.KeyPress -= Input_KeyPress;
            _input.KeyPress += Input_KeyPress;
        }

        public void Clear() { if (_input != null) _input.Text = string.Empty; }
        public void FocusInput() { if (_input != null) _input.Focus(); }

        private void Input_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter) return;
            e.Handled = true;
            Submitted.Invoke(this, Text);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _input != null)
                _input.KeyPress -= Input_KeyPress;
            base.Dispose(disposing);
        }
    }
}
