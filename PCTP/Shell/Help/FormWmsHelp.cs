using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Help
{
    internal sealed class FormWmsHelp : Form
    {
        private readonly WmsHelpService _service;
        private readonly ListBox _topics;
        private readonly Label _title;
        private readonly RichTextBox _content;
        private readonly TextBox _mermaid;

        internal FormWmsHelp(WmsHelpTopic topic, WmsHelpService service)
        {
            _service = service;
            Text = "WMS - Hướng dẫn sử dụng";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 720;
            MinimizeBox = true;
            MaximizeBox = true;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 220;

            _topics = new ListBox();
            _topics.Dock = DockStyle.Fill;
            _topics.DisplayMember = "Title";
            _topics.SelectedIndexChanged += Topics_SelectedIndexChanged;
            split.Panel1.Controls.Add(_topics);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;

            _title = new Label();
            _title.Dock = DockStyle.Top;
            _title.Height = 52;
            _title.Font = new Font(Font, FontStyle.Bold);
            _title.Padding = new Padding(12, 10, 12, 6);

            _content = new RichTextBox();
            _content.Dock = DockStyle.Fill;
            _content.ReadOnly = true;
            _content.BackColor = Color.White;
            _content.Font = new Font("Segoe UI", 10F);
            _content.BorderStyle = BorderStyle.None;

            GroupBox diagram = new GroupBox();
            diagram.Text = "Sơ đồ Mermaid nghiệp vụ";
            diagram.Dock = DockStyle.Bottom;
            diagram.Height = 190;
            diagram.Padding = new Padding(8);

            _mermaid = new TextBox();
            _mermaid.Multiline = true;
            _mermaid.ReadOnly = true;
            _mermaid.ScrollBars = ScrollBars.Both;
            _mermaid.Dock = DockStyle.Fill;
            _mermaid.Font = new Font("Consolas", 9F);
            diagram.Controls.Add(_mermaid);

            right.Controls.Add(_content);
            right.Controls.Add(diagram);
            right.Controls.Add(_title);
            split.Panel2.Controls.Add(right);
            Controls.Add(split);

            foreach (WmsHelpTopic item in _service.GetAll())
                _topics.Items.Add(item);

            SelectTopic(topic);
        }

        private void Topics_SelectedIndexChanged(object sender, EventArgs e)
        {
            WmsHelpTopic topic = _topics.SelectedItem as WmsHelpTopic;
            if (topic != null)
                Render(topic);
        }

        private void SelectTopic(WmsHelpTopic topic)
        {
            for (int i = 0; i < _topics.Items.Count; i++)
            {
                WmsHelpTopic item = _topics.Items[i] as WmsHelpTopic;
                if (item != null && string.Equals(item.Key, topic.Key, StringComparison.OrdinalIgnoreCase))
                {
                    _topics.SelectedIndex = i;
                    return;
                }
            }

            if (_topics.Items.Count > 0)
                _topics.SelectedIndex = 0;
        }

        private void Render(WmsHelpTopic topic)
        {
            _title.Text = topic.Title;
            _mermaid.Text = topic.Mermaid;

            _content.Clear();
            AppendHeading("1. Mục đích", topic.Purpose);
            AppendHeading("2. Điều kiện trước khi thực hiện", topic.Preconditions);
            AppendHeading("3. Các bước thao tác", topic.Steps);
            AppendHeading("4. Điều kiện xác nhận", topic.Confirmation);
            AppendHeading("5. Lỗi thường gặp", topic.CommonErrors);
            AppendHeading("6. Cách xử lý", topic.Troubleshooting);
            _content.SelectionStart = 0;
            _content.SelectionLength = 0;
        }

        private void AppendHeading(string heading, string body)
        {
            _content.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            _content.AppendText(heading + Environment.NewLine);
            _content.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            _content.AppendText(body + Environment.NewLine + Environment.NewLine);
        }
    }
}
