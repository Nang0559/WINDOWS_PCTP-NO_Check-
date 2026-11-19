using System.Drawing;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    /// <summary>Displays whether the selected record belongs to the user's scope or is related read-only context.</summary>
    public sealed class FvnScopeBadgeControl : XtraUserControl
    {
        private readonly LabelControl _label = new LabelControl();

        public FvnScopeBadgeControl()
        {
            Height = 28;
            Width = 320;
            _label.Dock = System.Windows.Forms.DockStyle.Fill;
            _label.Appearance.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            Controls.Add(_label);
            SetRelated(false);
        }

        public void SetRelated(bool related)
        {
            _label.Text = related ? "BỘ PHẬN LIÊN QUAN · CHỈ XEM" : "BỘ PHẬN TRONG PHẠM VI";
            _label.Appearance.ForeColor = related ? Color.FromArgb(105,115,125) : Color.FromArgb(23,89,154);
            _label.Appearance.BackColor = related ? Color.FromArgb(244,246,248) : Color.FromArgb(235,243,255);
            _label.Appearance.Options.UseBackColor = true;
        }
    }
}
