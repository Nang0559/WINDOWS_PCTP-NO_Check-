using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraDiagram;

namespace PCTP.Shell.Help
{
    internal sealed class FormWmsHelp : Form
    {
        private readonly WmsHelpService _service;
        private readonly ListBox _topics;
        private readonly Label _title;
        private readonly RichTextBox _content;
        private readonly GroupBox _diagramGroup;
        private readonly DiagramControl _diagram;
        private readonly Label _diagramStatus;

        internal FormWmsHelp(WmsHelpTopic topic, WmsHelpService service)
        {
            if (topic == null)
                throw new ArgumentNullException("topic");
            if (service == null)
                throw new ArgumentNullException("service");

            _service = service;
            Text = "WMS - Hướng dẫn sử dụng";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1200;
            Height = 780;
            MinimumSize = new Size(980, 620);
            MinimizeBox = true;
            MaximizeBox = true;
            RightToLeft = RightToLeft.No;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.RightToLeft = RightToLeft.No;
            split.Panel1MinSize = 230;
            split.Panel2MinSize = 620;
            split.SplitterWidth = 5;
            split.SplitterDistance = 285;
            split.FixedPanel = FixedPanel.Panel1;

            _topics = new ListBox();
            _topics.Dock = DockStyle.Fill;
            _topics.DisplayMember = "Title";
            _topics.IntegralHeight = false;
            _topics.Font = new Font("Segoe UI", 9.5F);
            _topics.SelectedIndexChanged += Topics_SelectedIndexChanged;
            split.Panel1.Controls.Add(_topics);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(10);

            _title = new Label();
            _title.Dock = DockStyle.Top;
            _title.Height = 48;
            _title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _title.Padding = new Padding(4, 4, 4, 4);
            _title.AutoEllipsis = true;

            _diagramGroup = new GroupBox();
            _diagramGroup.Text = "Sơ đồ Mermaid nghiệp vụ";
            _diagramGroup.Dock = DockStyle.Bottom;
            _diagramGroup.Height = 285;
            _diagramGroup.Padding = new Padding(6, 20, 6, 6);

            _diagram = new DiagramControl();
            _diagram.Dock = DockStyle.Fill;
            _diagram.OptionsBehavior.ShowQuickShapes = false;
            _diagram.OptionsView.ShowGrid = false;
            _diagram.OptionsView.ShowRulers = false;
            _diagram.OptionsView.ShowPageBreaks = false;
            _diagram.AllowDrop = false;
            _diagramGroup.Controls.Add(_diagram);

            _diagramStatus = new Label();
            _diagramStatus.Dock = DockStyle.Bottom;
            _diagramStatus.Height = 22;
            _diagramStatus.TextAlign = ContentAlignment.MiddleLeft;
            _diagramStatus.ForeColor = Color.DimGray;
            _diagramStatus.BackColor = Color.White;
            _diagramGroup.Controls.Add(_diagramStatus);
            _diagram.BringToFront();

            _content = new RichTextBox();
            _content.Dock = DockStyle.Fill;
            _content.ReadOnly = true;
            _content.BackColor = Color.White;
            _content.Font = new Font("Segoe UI", 10F);
            _content.BorderStyle = BorderStyle.FixedSingle;
            _content.Margin = new Padding(0);

            right.Controls.Add(_content);
            right.Controls.Add(_diagramGroup);
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

            _content.Clear();
            AppendHeading("1. Mục đích", topic.Purpose);
            AppendHeading("2. Điều kiện trước khi thực hiện", topic.Preconditions);
            AppendHeading("3. Các bước thao tác", topic.Steps);
            AppendHeading("4. Điều kiện xác nhận", topic.Confirmation);
            AppendHeading("5. Lỗi thường gặp", topic.CommonErrors);
            AppendHeading("6. Cách xử lý", topic.Troubleshooting);
            _content.SelectionStart = 0;
            _content.SelectionLength = 0;

            string error;
            bool rendered = WmsMermaidDiagramRenderer.Render(topic.Mermaid, _diagram, out error);
            _diagramStatus.Text = rendered
                ? "Sơ đồ được dựng trực tiếp từ Mermaid. Có thể zoom/pan trên canvas."
                : "Không thể dựng sơ đồ Mermaid: " + error;
        }

        private void AppendHeading(string heading, string body)
        {
            _content.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            _content.AppendText(heading + Environment.NewLine);
            _content.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            _content.AppendText((body ?? string.Empty) + Environment.NewLine + Environment.NewLine);
        }
    }
}
