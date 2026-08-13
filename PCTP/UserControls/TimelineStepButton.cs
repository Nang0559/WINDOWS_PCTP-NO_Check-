using DevExpress.DXTemplateGallery.Extensions;
using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.UserControls
{
    /// <summary>
    /// 1 mốc trên thanh Timeline: SimpleButton nền phẳng + badge tròn đếm số lượng
    /// ở góc trên-phải, đổi màu badge theo mức độ khẩn cấp (đỏ/vàng/cam/xanh).
    /// </summary>
    public class TimelineStepButton : PanelControl
    {
        private readonly LabelControl _lblTitle;
        private readonly LabelControl _lblSubtitle;
        private readonly LabelControl _lblBadge;
        private bool _isActive;
        private int _count;
        private readonly Color _themeColor;

        public int StepIndex { get; }
        public event EventHandler StepClicked;

        public int Count
        {
            get => _count;
            set { _count = value; RefreshBadge(); }
        }

        public TimelineStepButton(int stepIndex, string title, string subtitle, Color themeColor)
        {
            StepIndex = stepIndex;
            _themeColor = themeColor;

            // Xóa Size cố định để TableLayoutPanel tự phân bổ kích thước theo tỷ lệ %
            MinimumSize = new Size(130, 85);
            Margin = new Padding(2);

            Appearance.BackColor = Color.White;
            Appearance.Options.UseBackColor = true;
            Appearance.BorderColor = Color.FromArgb(210, 215, 220);
            Appearance.Options.UseBorderColor = true;
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            Cursor = Cursors.Hand;

            _lblTitle = new LabelControl
            {
                Text = title,
                Location = new Point(8, 12),
                Appearance = { Font = new Font("Tahoma", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(40, 40, 40) }
            };

            _lblSubtitle = new LabelControl
            {
                Text = subtitle,
                Location = new Point(8, 36),
                Appearance = { Font = new Font("Tahoma", 8F), ForeColor = Color.DimGray }
            };

            // Ghim Badge sát góc phải của Panel động
            _lblBadge = new LabelControl
            {
                Text = "📌 0",
                Size = new Size(48, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Appearance =
            {
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = themeColor,
                Options = { UseBackColor = true, UseForeColor = true, UseTextOptions = true },
                TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Center,
                                VAlignment = DevExpress.Utils.VertAlignment.Center }
            }
            };

            Controls.Add(_lblTitle);
            Controls.Add(_lblSubtitle);
            Controls.Add(_lblBadge);

            // Canh chỉnh vị trí Badge khi Panel thay đổi kích thước (Resize)
            Layout += (s, e) => {
                _lblBadge.Location = new Point(Width - 52, 12);
            };

            foreach (Control c in new Control[] { this, _lblTitle, _lblSubtitle, _lblBadge })
                c.Click += (s, e) => StepClicked?.Invoke(this, EventArgs.Empty);

            RefreshBadge();
        }

        private void RefreshBadge()
        {
            string countStr = _count > 99 ? "99+" : _count.ToString();
            _lblBadge.Text = $"📌 {countStr}";
            _lblBadge.Appearance.BackColor = _count > 0 ? _themeColor : Color.FromArgb(160, 165, 170);
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (active)
            {
                Appearance.BackColor = Color.FromArgb(232, 243, 255);
                Appearance.BorderColor = Color.FromArgb(0, 114, 204);
                _lblTitle.Appearance.ForeColor = Color.FromArgb(0, 102, 204);
            }
            else
            {
                Appearance.BackColor = Color.White;
                Appearance.BorderColor = Color.FromArgb(210, 215, 220);
                _lblTitle.Appearance.ForeColor = Color.FromArgb(40, 40, 40);
            }
            Refresh();
        }
    }
}
