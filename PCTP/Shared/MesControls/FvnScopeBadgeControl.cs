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

            Controls.Add(_label);
            SetRelated(false);
        }

        public void SetRelated(bool related)
        {
            _label.Text = related ? "BỘ PHẬN LIÊN QUAN · CHỈ XEM" : "BỘ PHẬN TRONG PHẠM VI";
            _label.Appearance.Font = new Font(_label.Appearance.Font, FontStyle.Bold);
        }
    }
}
