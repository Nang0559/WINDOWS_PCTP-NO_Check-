using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DevExpress.Diagram.Core;
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

        private sealed class MermaidNode
        {
            internal string Id;
            internal string Label;
            internal ShapeDescription Shape;
        }

        private sealed class MermaidEdge
        {
            internal string From;
            internal string To;
            internal string Label;
        }

        private static readonly Regex MermaidNodeRegex = new Regex(
            @"(?<id>[A-Za-z_][A-Za-z0-9_-]*)\s*(?<shape>\[[^\]]*\]|\([^\)]*\)|\{[^\}]*\})",
            RegexOptions.Compiled);

        private static readonly Regex MermaidEdgeRegex = new Regex(
            @"(?<from>[A-Za-z_][A-Za-z0-9_-]*)\s*(?:-->|==>|-\.->|---)\s*(?:\|(?<label>[^|]+)\|\s*)?(?<to>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.Compiled);

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
            bool rendered = RenderMermaid(topic.Mermaid, out error);
            _diagramStatus.Text = rendered
                ? "Sơ đồ được dựng trực tiếp từ Mermaid. Có thể zoom/pan trên canvas."
                : "Không thể dựng sơ đồ Mermaid: " + error;
        }

        private bool RenderMermaid(string mermaid, out string error)
        {
            error = null;

            try
            {
                _diagram.BeginUpdate();
                _diagram.Items.Clear();

                List<MermaidNode> nodes = new List<MermaidNode>();
                List<MermaidEdge> edges = new List<MermaidEdge>();
                Dictionary<string, MermaidNode> nodeMap = new Dictionary<string, MermaidNode>(StringComparer.OrdinalIgnoreCase);

                ParseMermaid(mermaid, nodeMap, edges);
                nodes.AddRange(nodeMap.Values);

                if (nodes.Count == 0)
                {
                    error = "Không tìm thấy node Mermaid.";
                    return false;
                }

                Dictionary<string, DiagramShape> shapes = new Dictionary<string, DiagramShape>(StringComparer.OrdinalIgnoreCase);

                foreach (MermaidNode node in nodes)
                {
                    DiagramShape shape = new DiagramShape();
                    shape.Shape = node.Shape;
                    shape.Content = node.Label;
                    shape.Size = new SizeF(190F, 58F);
                    shape.CanEdit = false;
                    shape.CanMove = false;
                    shape.CanResize = false;
                    shape.CanDelete = false;
                    shape.CanCopy = false;
                    shapes[node.Id] = shape;
                    _diagram.Items.Add(shape);
                }

                foreach (MermaidEdge edge in edges)
                {
                    DiagramShape from;
                    DiagramShape to;
                    if (!shapes.TryGetValue(edge.From, out from) || !shapes.TryGetValue(edge.To, out to))
                        continue;

                    DiagramConnector connector = new DiagramConnector(from, to);
                    connector.EndArrow = ArrowDescriptions.Filled90;
                    connector.Content = edge.Label ?? string.Empty;
                    connector.CanEdit = false;
                    connector.CanMove = false;
                    connector.CanDelete = false;
                    _diagram.Items.Add(connector);
                }

                try
                {
                    _diagram.ApplySugiyamaLayout(Direction.Down, _diagram.Items);
                }
                catch
                {
                    _diagram.ApplyTreeLayout(Direction.Down, _diagram.Items);
                }

                _diagram.AlignPage(null, null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                _diagram.EndUpdate();
            }
        }

        private static void ParseMermaid(
            string mermaid,
            Dictionary<string, MermaidNode> nodeMap,
            List<MermaidEdge> edges)
        {
            if (string.IsNullOrWhiteSpace(mermaid))
                return;

            string[] lines = mermaid.Replace("\r", string.Empty).Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("%%", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith("flowchart ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("graph ", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (Match match in MermaidNodeRegex.Matches(line))
                {
                    string id = match.Groups["id"].Value;
                    string token = match.Groups["shape"].Value;
                    MermaidNode node;
                    if (!nodeMap.TryGetValue(id, out node))
                    {
                        node = new MermaidNode { Id = id };
                        nodeMap.Add(id, node);
                    }

                    node.Label = CleanMermaidLabel(token);
                    node.Shape = ResolveMermaidShape(token);
                }

                foreach (Match match in MermaidEdgeRegex.Matches(line))
                {
                    string from = match.Groups["from"].Value;
                    string to = match.Groups["to"].Value;
                    string label = match.Groups["label"].Success
                        ? CleanMermaidLabel("[" + match.Groups["label"].Value + "]")
                        : string.Empty;

                    EnsureMermaidNode(nodeMap, from);
                    EnsureMermaidNode(nodeMap, to);
                    edges.Add(new MermaidEdge { From = from, To = to, Label = label });
                }
            }
        }

        private static void EnsureMermaidNode(Dictionary<string, MermaidNode> nodeMap, string id)
        {
            if (nodeMap.ContainsKey(id))
                return;

            nodeMap.Add(id, new MermaidNode
            {
                Id = id,
                Label = id,
                Shape = BasicFlowchartShapes.Process
            });
        }

        private static string CleanMermaidLabel(string token)
        {
            if (string.IsNullOrEmpty(token))
                return string.Empty;

            string value = token.Length >= 2
                ? token.Substring(1, token.Length - 2)
                : token;

            return value
                .Replace("<br/>", " ")
                .Replace("<br>", " ")
                .Replace("<br />", " ")
                .Trim();
        }

        private static ShapeDescription ResolveMermaidShape(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BasicFlowchartShapes.Process;
            if (token[0] == '{')
                return BasicFlowchartShapes.Decision;
            if (token[0] == '(')
                return BasicFlowchartShapes.StartEnd;
            return BasicFlowchartShapes.Process;
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
