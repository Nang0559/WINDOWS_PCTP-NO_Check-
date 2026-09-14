using PCTP.Modules.BaoCao.UI;
using PCTP.Shell.Help;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// Lightweight WMS Control Center hosted by Main_APP.
    /// It only coordinates navigation; query/business logic stays in modules.
    /// </summary>
    internal sealed class WmsControlCenterBar : UserControl
    {
        private readonly WmsHelpService _help;
        private readonly TextBox _quickSearch;
        private readonly Label _status;

        internal WmsControlCenterBar(WmsHelpService help)
        {
            if (help == null)
                throw new ArgumentNullException("help");

            _help = help;
            Height = 46;
            Dock = DockStyle.Top;
            BackColor = Color.WhiteSmoke;
            BorderStyle = BorderStyle.FixedSingle;

            Label title = new Label
            {
                Text = "WMS CONTROL CENTER",
                AutoSize = true,
                Location = new Point(10, 14),
                Font = new Font("Tahoma", 9f, FontStyle.Bold)
            };
            Controls.Add(title);

            _quickSearch = new TextBox
            {
                Width = 260,
                Height = 24,
                Location = new Point(170, 10),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                ToolTipText = ""
            };
            _quickSearch.KeyDown += QuickSearch_KeyDown;
            Controls.Add(_quickSearch);

            Button search = CreateButton("Tra cứu", 80, 170);
            search.Click += delegate { OpenQuickSearch(); };
            Controls.Add(search);

            Button guide = CreateButton("Hướng dẫn", 82, 255);
            guide.Click += delegate { _help.Show(this, "Dashboard"); };
            Controls.Add(guide);

            Button refresh = CreateButton("Làm mới", 72, 343);
            refresh.Click += delegate { OnRefreshRequested(); };
            Controls.Add(refresh);

            _status = new Label
            {
                Text = "Sẵn sàng",
                AutoSize = true,
                Location = new Point(430, 14),
                ForeColor = Color.DimGray,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            Controls.Add(_status);
        }

        internal event EventHandler RefreshRequested;

        internal void FocusQuickSearch(string value)
        {
            _quickSearch.Text = value ?? string.Empty;
            _quickSearch.Focus();
            _quickSearch.SelectAll();
        }

        private void QuickSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            OpenQuickSearch();
        }

        private void OpenQuickSearch()
        {
            string keyword = (_quickSearch.Text ?? string.Empty).Trim();
            using (FormBaoCaoTraceability form = new FormBaoCaoTraceability(keyword))
            {
                form.ShowDialog(this.FindForm());
            }
        }

        private void OnRefreshRequested()
        {
            _status.Text = "Đang làm mới...";
            EventHandler handler = RefreshRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
            _status.Text = "Đã làm mới";
        }

        private static Button CreateButton(string text, int width, int left)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = 26,
                Location = new Point(left, 9),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
        }
    }
}
