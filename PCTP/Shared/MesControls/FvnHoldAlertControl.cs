using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    /// <summary>Read-only dependency/HOLD indicator. The caller supplies the already-authorized workflow context.</summary>
    public sealed class FvnHoldAlertControl : XtraUserControl
    {
        private readonly LabelControl _title = new LabelControl();
        private readonly LabelControl _reason = new LabelControl();

        public FvnHoldAlertControl()
        {
            Height = 58;
            Dock = DockStyle.Top;
            Padding = new Padding(8, 4, 8, 4);
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            _title.Dock = DockStyle.Top;
            _title.Height = 21;

            _reason.Dock = DockStyle.Fill;

            Controls.Add(_reason);
            Controls.Add(_title);
            Visible = false;
        }

        public void SetDependency(string waitingAt, string reason, bool relatedReadOnly)
        {
            waitingAt = (waitingAt ?? string.Empty).Trim();
            reason = (reason ?? string.Empty).Trim();
            Visible = waitingAt.Length > 0 || reason.Length > 0;
            _title.Text = waitingAt.Length == 0 ? "ĐIỂM CHẶN / CẦN XỬ LÝ" : "ĐANG CHỜ TẠI: " + waitingAt;
            _reason.Text = reason.Length == 0 ? "Chưa có mô tả nguyên nhân." : reason;
            _title.Appearance.Font = new Font(_title.Appearance.Font, FontStyle.Bold);
        }
    }
}
