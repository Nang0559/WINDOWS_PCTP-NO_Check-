using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PCTP.Modules.MesPlanning.UI
{
    public enum FlowNodeVisual { Disabled, Ready, Done, Current }

    public sealed class FlowNodeEventArgs : EventArgs
    {
        public FlowNodeEventArgs(string key) { Key = key; }
        public string Key { get; private set; }
    }

    /// <summary>
    /// Sơ đồ luồng điều hành sản xuất 5 cột (Khởi tạo → Phát hành → LOT &amp; BOM → Vận hành → Hoàn thành &amp; đóng).
    /// Mỗi ô vừa hiển thị trạng thái (đã xong / đang ở bước này / có thể bấm / chưa thể bấm) vừa là nút lệnh.
    /// Control chỉ vẽ và phát sự kiện; việc cho phép bấm do form quyết định (quyền + trạng thái workflow).
    /// </summary>
    public sealed class MesProductionFlowMapControl : Control
    {
        private sealed class Node
        {
            public string Key, Caption, Tip = string.Empty;
            public int Lane, Row;
            public Color Accent;
            public FlowNodeVisual Visual = FlowNodeVisual.Disabled;
            public bool Enabled;
            public RectangleF Bounds;
        }

        private static readonly string[] LaneTitles =
        {
            "KHỞI TẠO & LẬP KẾ HOẠCH", "PHÁT HÀNH LỆNH", "TẠO LOT SẢN XUẤT & BOM",
            "VẬN HÀNH SẢN XUẤT", "HOÀN THÀNH & ĐÓNG LỆNH"
        };

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, Node> _byKey = new Dictionary<string, Node>();
        private readonly List<KeyValuePair<string, string>> _arrows = new List<KeyValuePair<string, string>>();
        private readonly ToolTip _toolTip = new ToolTip();
        private Node _hot;
        private int _activeLane = -1;

        public event EventHandler<FlowNodeEventArgs> NodeClicked;

        public MesProductionFlowMapControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 214;
            MinimumSize = new Size(760, 190);
            Font = new Font("Segoe UI", 9F);

            var blue = Color.FromArgb(46, 110, 190);
            Add("select", "CHỌN KẾ HOẠCH FIRM", 0, 0, blue);
            Add("create", "TẠO LỆNH TỪ KẾ HOẠCH", 0, 1, blue);
            Add("release", "RELEASE LỆNH", 1, 0, blue);
            Add("lot", "GHI NHẬN OK/NG + TẠO LOT", 2, 0, blue);
            Add("consume", "TIÊU HAO LOT LINH KIỆN", 2, 1, Color.FromArgb(96, 84, 170));
            Add("start", "START LỆNH", 3, 0, Color.FromArgb(34, 139, 84));
            Add("pause", "PAUSE LỆNH", 3, 1, Color.FromArgb(214, 122, 0));
            Add("resume", "RESUME LỆNH", 3, 2, blue);
            Add("complete", "COMPLETE LỆNH", 4, 0, Color.FromArgb(0, 112, 160));
            Add("close", "CLOSE LỆNH", 4, 1, Color.FromArgb(84, 100, 120));
            Add("history", "LỊCH SỬ LỆNH", 4, 2, Color.FromArgb(110, 120, 135));

            Link("select", "create"); Link("create", "release"); Link("release", "lot");
            Link("lot", "consume"); Link("lot", "start"); Link("start", "pause"); Link("pause", "resume");
            Link("start", "complete"); Link("complete", "close"); Link("close", "history");
        }

        private void Add(string key, string caption, int lane, int row, Color accent)
        {
            var node = new Node { Key = key, Caption = caption, Lane = lane, Row = row, Accent = accent };
            _nodes.Add(node);
            _byKey[key] = node;
        }

        private void Link(string from, string to)
        {
            _arrows.Add(new KeyValuePair<string, string>(from, to));
        }

        public void SetNode(string key, FlowNodeVisual visual, bool enabled, string tooltip)
        {
            Node node;
            if (!_byKey.TryGetValue(key, out node)) return;
            if (node.Visual == visual && node.Enabled == enabled && node.Tip == (tooltip ?? string.Empty)) return;
            node.Visual = visual;
            node.Enabled = enabled;
            node.Tip = tooltip ?? string.Empty;
            Invalidate();
        }

        /// <summary>Cột đang ở bước hiện tại (0..4) được tô đậm; -1 = không tô.</summary>
        public void SetActiveLane(int lane)
        {
            if (_activeLane == lane) return;
            _activeLane = lane;
            Invalidate();
        }

        private void Layout(out float laneWidth, out float top, out float nodeHeight, out float step)
        {
            float k = DeviceDpi / 96f;
            laneWidth = ClientSize.Width / (float)LaneTitles.Length;
            top = 30f * k;
            nodeHeight = 32f * k;
            step = 44f * k;
            foreach (var node in _nodes)
            {
                float width = laneWidth - 22f * k;
                node.Bounds = new RectangleF(node.Lane * laneWidth + 11f * k, top + node.Row * step, width, nodeHeight);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(250, 251, 253));
            float laneWidth, top, nodeHeight, step;
            Layout(out laneWidth, out top, out nodeHeight, out step);

            var centered = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            // Cột + tiêu đề
            using (var separator = new Pen(Color.FromArgb(222, 226, 232)))
            using (var titleFont = new Font(Font, FontStyle.Bold))
            using (var activeBrush = new SolidBrush(Color.FromArgb(46, 110, 190)))
            using (var normalBrush = new SolidBrush(Color.FromArgb(70, 78, 90)))
            {
                for (int i = 0; i < LaneTitles.Length; i++)
                {
                    float x = i * laneWidth;
                    if (i == _activeLane)
                        using (var tint = new SolidBrush(Color.FromArgb(236, 243, 252)))
                            g.FillRectangle(tint, x, 0, laneWidth, Height);
                    if (i > 0) g.DrawLine(separator, x, 6, x, Height - 6);
                    g.DrawString(LaneTitles[i], titleFont, i == _activeLane ? activeBrush : normalBrush,
                        new RectangleF(x + 4, 4, laneWidth - 8, top - 8), centered);
                }
            }

            // Mũi tên
            using (var arrowPen = new Pen(Color.FromArgb(150, 162, 178), 1.6f))
            {
                arrowPen.CustomEndCap = new AdjustableArrowCap(4, 4, true);
                foreach (var arrow in _arrows)
                {
                    var a = _byKey[arrow.Key].Bounds;
                    var b = _byKey[arrow.Value].Bounds;
                    if (Math.Abs(a.Left - b.Left) < 1f)
                        g.DrawLine(arrowPen, a.Left + a.Width / 2, a.Bottom, b.Left + b.Width / 2, b.Top - 1);
                    else
                    {
                        float midX = a.Right + (b.Left - a.Right) / 2f;
                        g.DrawLines(arrowPen, new[]
                        {
                            new PointF(a.Right, a.Top + a.Height / 2), new PointF(midX, a.Top + a.Height / 2),
                            new PointF(midX, b.Top + b.Height / 2), new PointF(b.Left - 1, b.Top + b.Height / 2)
                        });
                    }
                }
            }

            // Ô lệnh
            using (var boldFont = new Font(Font, FontStyle.Bold))
                foreach (var node in _nodes) DrawNode(g, node, boldFont, centered);
        }

        private void DrawNode(Graphics g, Node node, Font boldFont, StringFormat format)
        {
            Color fill, border, text;
            string caption = node.Caption;
            switch (node.Visual)
            {
                case FlowNodeVisual.Current:
                    fill = node.Accent; border = node.Accent; text = Color.White; break;
                case FlowNodeVisual.Done:
                    fill = Color.FromArgb(226, 243, 233); border = Color.FromArgb(34, 139, 84);
                    text = Color.FromArgb(24, 110, 66); caption = "✔  " + caption; break;
                case FlowNodeVisual.Ready:
                    fill = node == _hot ? Color.FromArgb(232, 241, 252) : Color.White;
                    border = node.Accent; text = Color.FromArgb(30, 36, 46); break;
                default:
                    fill = Color.FromArgb(240, 241, 243); border = Color.FromArgb(214, 217, 222);
                    text = Color.FromArgb(150, 154, 162); break;
            }
            using (var path = RoundedRectangle(node.Bounds, 7f))
            using (var fillBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, node.Visual == FlowNodeVisual.Ready || node.Visual == FlowNodeVisual.Current ? 1.8f : 1.2f))
            using (var textBrush = new SolidBrush(text))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
                var bounds = node.Bounds;
                bounds.Inflate(-4, 0);
                g.DrawString(caption, node.Enabled || node.Visual != FlowNodeVisual.Disabled ? boldFont : Font, textBrush, bounds, format);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Node HitTest(Point point)
        {
            foreach (var node in _nodes)
                if (node.Bounds.Contains(point)) return node;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var node = HitTest(e.Location);
            if (node != _hot)
            {
                _hot = node;
                Cursor = node != null && node.Enabled ? Cursors.Hand : Cursors.Default;
                if (node != null && node.Tip.Length > 0) _toolTip.SetToolTip(this, node.Tip);
                else _toolTip.SetToolTip(this, string.Empty);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != null) { _hot = null; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            var node = HitTest(e.Location);
            if (node == null || !node.Enabled) return;
            var handler = NodeClicked;
            if (handler != null) handler(this, new FlowNodeEventArgs(node.Key));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }
}