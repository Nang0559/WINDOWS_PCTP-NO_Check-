using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// Visual boundary for the delivery-form header area.
    /// The existing panel is adopted intact so this phase does not change
    /// layout, events, or business behavior.
    /// </summary>
    public sealed class PhieuHeaderControl : XtraUserControl
    {
        private Control _content;

        public PhieuHeaderControl()
        {
            Dock = DockStyle.Fill;
            Name = "phieuHeaderControl";
        }

        public Control ContentControl
        {
            get { return _content; }
        }

        public void Adopt(Control content)
        {
            if (content == null || ReferenceEquals(_content, content))
                return;

            if (_content != null)
            {
                Controls.Remove(_content);
            }

            _content = content;
            Controls.Add(_content);
            _content.Dock = DockStyle.Fill;
            _content.Margin = new Padding(0);
        }
    }
}
