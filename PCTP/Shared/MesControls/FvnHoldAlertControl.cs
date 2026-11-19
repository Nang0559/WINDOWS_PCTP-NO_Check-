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
            _title.Appearance.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _reason.Dock = DockStyle.Fill;
            _reason.Appearance.Font = new Font("Segoe UI", 8.5f);
            Controls.Add(_reason);
            Controls.Add(_title);
            Visible = false;
        }

        public void SetDependency(string waitingAt, string reason, bool relatedReadOnly)
        {
            waitingAt = (waitingAt ?? string.Empty).Trim();
            reason = (reason ?? string.Empty).Trim();
            Visible = waitingAt.Length > 0 || reason.Length > 0;
            _title.Text = waitingAt.Length == 0 ? "ĐIỂM CHỜ / HOLD" : "ĐANG CHỜ TẠI: " + waitingAt;
            _reason.Text = reason.Length == 0 ? "Chưa có mô tả nguyên nhân." : reason;
            _title.Appearance.ForeColor = relatedReadOnly ? Color.FromArgb(105,115,125) : Color.FromArgb(153,86,20);
            _reason.Appearance.ForeColor = Color.FromArgb(75,85,95);
            Appearance.BackColor = relatedReadOnly ? Color.FromArgb(244,246,248) : Color.FromArgb(255,248,230);
            Appearance.Options.UseBackColor = true;
        }
    }
}
