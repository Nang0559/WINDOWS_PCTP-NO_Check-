using PCTP.Modules.BaoCao.UI;
using PCTP.Shell.Help;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
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
            Height = 48;
            Dock = DockStyle.Top;
            BackColor = Color.WhiteSmoke;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(6);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));

            Label title = new Label
            {
                Text = "WMS CONTROL CENTER",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Tahoma", 9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0)
            };

            _quickSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 6, 4)
            };
            _quickSearch.KeyDown += QuickSearch_KeyDown;

            Button search = CreateButton("Tra cứu");
            search.Click += delegate { OpenQuickSearch(); };

            Button guide = CreateButton("Hướng dẫn");
            guide.Click += delegate { _help.Show(this, "Dashboard"); };

            Button report = CreateButton("Báo cáo");
            report.Click += delegate { OpenReports(); };

            Button refresh = CreateButton("Làm mới");
            refresh.Click += delegate { OnRefreshRequested(); };

            _status = new Label
            {
                Text = "Sẵn sàng",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray,
                Margin = new Padding(8, 0, 0, 0)
            };

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(_quickSearch, 1, 0);
            layout.Controls.Add(search, 2, 0);
            layout.Controls.Add(guide, 3, 0);
            layout.Controls.Add(report, 4, 0);
            layout.Controls.Add(refresh, 5, 0);
            layout.Controls.Add(_status, 6, 0);
            Controls.Add(layout);
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
            if (keyword.Length == 0)
            {
                _status.Text = "Nhập QR, LOT hoặc PART để tra cứu.";
                FocusQuickSearch(string.Empty);
                return;
            }

            try
            {
                BaoCaoNavigator.OpenTraceability(this, keyword);
                _status.Text = "Đã mở tra cứu.";
            }
            catch (Exception ex)
            {
                _status.Text = "Không thể mở tra cứu.";
                MessageBox.Show(this, ex.Message, "WMS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenReports()
        {
            try
            {
                BaoCaoNavigator.OpenMain(this);
                _status.Text = "Đã mở báo cáo.";
            }
            catch (Exception ex)
            {
                _status.Text = "Không thể mở báo cáo.";
                MessageBox.Show(this, ex.Message, "WMS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnRefreshRequested()
        {
            _status.Text = "Đang làm mới...";
            try
            {
                EventHandler handler = RefreshRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
                _status.Text = "Đã làm mới";
            }
            catch (Exception ex)
            {
                _status.Text = "Làm mới thất bại.";
                MessageBox.Show(this, ex.Message, "WMS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 28,
                Margin = new Padding(3, 2, 3, 2)
            };
        }
    }
}
