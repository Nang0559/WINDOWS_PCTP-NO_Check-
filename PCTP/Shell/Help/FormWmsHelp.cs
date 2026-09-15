using DevExpress.XtraEditors;
using Microsoft.Web.WebView2.WinForms;
using PCTP.Shell.Help;
using System;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class FormWmsHelp : XtraForm
{
    private readonly WmsHelpService _service;
    private readonly ListBoxControl _topics;
    private readonly LabelControl _title;
    private readonly RichTextBox _content;
    private readonly GroupControl _diagramGroup;
    private readonly WebView2 _mermaidView;
    private readonly SplitContainerControl _rightSplit;   // ← mới: chia trên/dưới trong panel phải
    private bool _webViewReady;
    private WmsHelpTopic _pendingTopic;

    internal FormWmsHelp(WmsHelpTopic topic, WmsHelpService service)
    {
        _service = service;
        Text = "WMS - Hướng dẫn sử dụng";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1100;
        Height = 720;
        MinimizeBox = true;
        MaximizeBox = true;

        var split = new SplitContainerControl
        {
            Dock = DockStyle.Fill,
            Horizontal = true,                                   // trái/phải
            FixedPanel = DevExpress.XtraEditors.SplitFixedPanel.None,
            SplitterPosition = 220
        };

        _topics = new ListBoxControl
        {
            Dock = DockStyle.Fill,
            DisplayMember = "Title"
        };
        _topics.SelectedIndexChanged += Topics_SelectedIndexChanged;
        split.Panel1.Controls.Add(_topics);

        var right = new PanelControl
        {
            Dock = DockStyle.Fill,
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
        };

        _title = new LabelControl
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(12, 10, 12, 6),
            Appearance = { Font = new Font("Segoe UI", 12F, FontStyle.Bold), Options = { UseFont = true } }
        };

        // ── Panel phải, phần dưới tiêu đề: sơ đồ (trên) / chi tiết (dưới), kéo được ──
        _rightSplit = new SplitContainerControl
        {
            Dock = DockStyle.Fill,
            Horizontal = false,                                  // trên/dưới
            FixedPanel = DevExpress.XtraEditors.SplitFixedPanel.None,
            SplitterPosition = 300                                // vị trí splitter ban đầu, kéo lại được
        };

        _diagramGroup = new GroupControl
        {
            Text = "Sơ đồ Mermaid nghiệp vụ",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        _mermaidView = new WebView2 { Dock = DockStyle.Fill };
        _diagramGroup.Controls.Add(_mermaidView);
        _rightSplit.Panel1.Controls.Add(_diagramGroup);           // trên = sơ đồ

        _content = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.None
        };
        _rightSplit.Panel2.Controls.Add(_content);                // dưới = chi tiết mô tả

        // Tránh kéo splitter làm 1 trong 2 phần biến mất hoàn toàn
        _rightSplit.Panel1.MinSize = 120;
        _rightSplit.Panel2.MinSize = 120;

        right.Controls.Add(_rightSplit);
        right.Controls.Add(_title);
        split.Panel2.Controls.Add(right);
        Controls.Add(split);

        foreach (WmsHelpTopic item in _service.GetAll())
            _topics.Items.Add(item);

        Load += async (s, e) => await InitWebViewAsync();
        SelectTopic(topic);
    }
    private async Task InitWebViewAsync()
    {
        try
        {
            await _mermaidView.EnsureCoreWebView2Async();
            _webViewReady = true;
            if (_pendingTopic != null)
                RenderMermaid(_pendingTopic.Mermaid);
        }
        catch (Exception ex)
        {
            // Máy chưa cài WebView2 Runtime (Evergreen) — vẫn cho xem nội dung text,
            // chỉ sơ đồ không render được.
            XtraMessageBox.Show(
                "Không thể khởi tạo WebView2 để hiển thị sơ đồ (cần cài WebView2 Runtime):\n" + ex.Message,
                "WMS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Topics_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_topics.SelectedItem is WmsHelpTopic topic)
            Render(topic);
    }

    private void SelectTopic(WmsHelpTopic topic)
    {
        for (int i = 0; i < _topics.Items.Count; i++)
        {
            if (_topics.Items[i] is WmsHelpTopic item &&
                string.Equals(item.Key, topic.Key, StringComparison.OrdinalIgnoreCase))
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

        _pendingTopic = topic;
        RenderMermaid(topic.Mermaid);
    }

    private void AppendHeading(string heading, string body)
    {
        _content.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        _content.AppendText(heading + Environment.NewLine);
        _content.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Regular);
        _content.AppendText(body + Environment.NewLine + Environment.NewLine);
    }

    private void RenderMermaid(string mermaidSource)
    {
        if (!_webViewReady || string.IsNullOrWhiteSpace(mermaidSource))
            return;

        string html = $@"<!DOCTYPE html>
        <html><head><meta charset='utf-8'>
        <script src='https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js'></script>
        <style>
          html, body {{
            margin: 0;
            padding: 0;
            height: 100%;
            font-family: 'Segoe UI', sans-serif;
          }}
          body {{
            display: flex;
            align-items: center;       /* căn giữa theo chiều dọc */
            justify-content: center;   /* căn giữa theo chiều ngang */
            min-height: 100vh;
            overflow: auto;            /* sơ đồ lớn hơn panel thì scroll, không tràn/cắt */
            box-sizing: border-box;
            padding: 12px;
          }}
          pre.mermaid {{
            margin: 0;
          }}
        </style>
        </head><body>
        <pre class='mermaid'>{WebUtility.HtmlEncode(mermaidSource)}</pre>
        <script>mermaid.initialize({{ startOnLoad: true, theme: 'default' }});</script>
        </body></html>";

        _mermaidView.CoreWebView2.NavigateToString(html);
    }
}