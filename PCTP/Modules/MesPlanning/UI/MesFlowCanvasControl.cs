using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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
    /// Nền vẽ sơ đồ quy trình MES theo "canvas logic" cố định (1288 x 580) rồi co giãn đều theo kích thước control,
    /// nên bố cục luôn đúng tỉ lệ như thiết kế. Control chỉ vẽ và phát sự kiện NodeClicked; việc cho phép bấm do form quyết định.
    /// Thành phần: cột (lane), khung nhóm, ô thông tin, ô lệnh (bấm được), nhãn trạng thái (pill), ghi chú, đường nối và dải cảnh báo.
    /// </summary>
    public abstract class MesFlowCanvasControl : Control
    {
        public const float CanvasWidth = 1288f;
        public const float CanvasHeight = 580f;

        private sealed class Lane { public string Title; public float X0, X1; }
        private sealed class Group { public RectangleF Rect; public string Title, Subtitle; }
        private sealed class Box { public RectangleF Rect; public string Text, Sub; }
        private sealed class Pill { public string Key; public RectangleF Rect; public string Text; public Color Accent; public bool Active, Always; }
        private sealed class Note { public PointF At; public string Text; public bool Bold; public Color Color; }
        private sealed class Connector { public PointF[] Points; public bool Arrow; public string Label; public PointF LabelAt; }

        private sealed class Node
        {
            public string Key, Caption, Tip = string.Empty;
            public Color Accent;
            public FlowNodeVisual Visual = FlowNodeVisual.Disabled;
            public bool Enabled;
            public RectangleF Rect;
        }

        private readonly List<Lane> _lanes = new List<Lane>();
        private readonly List<Group> _groups = new List<Group>();
        private readonly List<Box> _boxes = new List<Box>();
        private readonly List<Pill> _pills = new List<Pill>();
        private readonly List<Note> _notes = new List<Note>();
        private readonly List<Connector> _connectors = new List<Connector>();
        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, Node> _nodeByKey = new Dictionary<string, Node>();
        private readonly Dictionary<string, Pill> _pillByKey = new Dictionary<string, Pill>();
        private readonly ToolTip _toolTip = new ToolTip();
        private Node _hot;
        private int _activeLane = -1;
        private string _banner = string.Empty;
        private float _scale = 1f, _offsetX, _offsetY;

        public event EventHandler<FlowNodeEventArgs> NodeClicked;

        protected MesFlowCanvasControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            MinimumSize = new Size(760, 340);
            Size = new Size(1000, 450);
            Font = new Font("Segoe UI", 9F);
        }

        // ------------------------------------------------------------------ dựng sơ đồ (dùng ở constructor lớp con)
        protected void AddLane(string title, float x0, float x1)
        {
            _lanes.Add(new Lane { Title = title, X0 = x0, X1 = x1 });
        }

        protected void AddGroup(float x, float y, float w, float h, string title, string subtitle)
        {
            _groups.Add(new Group { Rect = new RectangleF(x, y, w, h), Title = title, Subtitle = subtitle });
        }

        protected void AddBox(float x, float y, float w, float h, string text, string sub)
        {
            _boxes.Add(new Box { Rect = new RectangleF(x, y, w, h), Text = text, Sub = sub });
        }

        protected void AddNode(string key, string caption, float x, float y, float w, float h, Color accent)
        {
            var node = new Node { Key = key, Caption = caption, Accent = accent, Rect = new RectangleF(x, y, w, h) };
            _nodes.Add(node);
            _nodeByKey[key] = node;
        }

        /// <summary>Nhãn trạng thái. always=true: luôn tô màu (ví dụ Số lượng OK / NG).</summary>
        protected void AddPill(string key, string text, float x, float y, float w, float h, Color accent, bool always)
        {
            var pill = new Pill { Key = key, Text = text, Rect = new RectangleF(x, y, w, h), Accent = accent, Always = always };
            _pills.Add(pill);
            if (!string.IsNullOrEmpty(key)) _pillByKey[key] = pill;
        }

        protected void AddNote(float x, float y, string text, bool bold, Color color)
        {
            _notes.Add(new Note { At = new PointF(x, y), Text = text, Bold = bold, Color = color });
        }

        protected void AddConnector(bool arrow, params float[] xy)
        {
            var points = new PointF[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            _connectors.Add(new Connector { Points = points, Arrow = arrow });
        }

        protected void AddLabeledConnector(string label, float labelX, float labelY, params float[] xy)
        {
            var points = new PointF[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            _connectors.Add(new Connector { Points = points, Arrow = true, Label = label, LabelAt = new PointF(labelX, labelY) });
        }

        protected void SetBanner(string text)
        {
            _banner = text ?? string.Empty;
        }

        // ------------------------------------------------------------------ API cho form
        public void SetNode(string key, FlowNodeVisual visual, bool enabled, string tooltip)
        {
            Node node;
            if (!_nodeByKey.TryGetValue(key, out node)) return;
            string tip = tooltip ?? string.Empty;
            if (node.Visual == visual && node.Enabled == enabled && node.Tip == tip) return;
            node.Visual = visual;
            node.Enabled = enabled;
            node.Tip = tip;
            Invalidate();
        }

        public void SetPill(string key, bool active)
        {
            Pill pill;
            if (!_pillByKey.TryGetValue(key, out pill) || pill.Active == active) return;
            pill.Active = active;
            Invalidate();
        }

        public void SetPillText(string key, string text)
        {
            Pill pill;
            if (!_pillByKey.TryGetValue(key, out pill) || pill.Text == text) return;
            pill.Text = text;
            Invalidate();
        }

        /// <summary>Cột đang ở bước hiện tại được tô nền; -1 = không tô.</summary>
        public void SetActiveLane(int lane)
        {
            if (_activeLane == lane) return;
            _activeLane = lane;
            Invalidate();
        }

        // ------------------------------------------------------------------ vẽ
        private void UpdateTransform()
        {
            float sx = ClientSize.Width / CanvasWidth;
            float sy = ClientSize.Height / CanvasHeight;
            _scale = Math.Max(0.2f, Math.Min(sx, sy));
            _offsetX = Math.Max(0f, (ClientSize.Width - CanvasWidth * _scale) / 2f);
            _offsetY = 0f;
        }

        private PointF ToCanvas(Point p)
        {
            return new PointF((p.X - _offsetX) / _scale, (p.Y - _offsetY) / _scale);
        }

        private static Font PixelFont(float px, FontStyle style)
        {
            return new Font("Segoe UI", px, style, GraphicsUnit.Pixel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Color.FromArgb(247, 249, 252));
            UpdateTransform();
            g.TranslateTransform(_offsetX, _offsetY);
            g.ScaleTransform(_scale, _scale);

            using (var small = PixelFont(11f, FontStyle.Regular))
            using (var normal = PixelFont(12.5f, FontStyle.Regular))
            using (var bold = PixelFont(13f, FontStyle.Bold))
            using (var lane = PixelFont(14f, FontStyle.Bold))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            using (var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            {
                DrawLanes(g, lane, center);
                foreach (var group in _groups) DrawGroup(g, group, bold, small, center);
                foreach (var c in _connectors) DrawConnector(g, c, small, center);
                foreach (var box in _boxes) DrawBox(g, box, normal, small, center);
                foreach (var pill in _pills) DrawPill(g, pill, small, center);
                foreach (var note in _notes) DrawNote(g, note, small, left);
                foreach (var node in _nodes) DrawNode(g, node, bold, center);
                DrawBanner(g, normal);
            }
        }

        private void DrawLanes(Graphics g, Font font, StringFormat center)
        {
            using (var sep = new Pen(Color.FromArgb(218, 223, 231)))
            using (var activeBrush = new SolidBrush(Color.FromArgb(46, 110, 190)))
            using (var normalBrush = new SolidBrush(Color.FromArgb(40, 48, 60)))
            using (var tint = new SolidBrush(Color.FromArgb(232, 240, 251)))
            {
                for (int i = 0; i < _lanes.Count; i++)
                {
                    var l = _lanes[i];
                    if (i == _activeLane) g.FillRectangle(tint, l.X0, 4, l.X1 - l.X0, 500);
                    if (i > 0) g.DrawLine(sep, l.X0, 8, l.X0, 500);
                    g.DrawString(l.Title, font, i == _activeLane ? activeBrush : normalBrush,
                        new RectangleF(l.X0 + 4, 4, l.X1 - l.X0 - 8, 50), center);
                }
            }
        }

        private static void DrawGroup(Graphics g, Group group, Font bold, Font small, StringFormat center)
        {
            using (var path = RoundedRectangle(group.Rect, 10f))
            using (var fill = new SolidBrush(Color.White))
            using (var border = new Pen(Color.FromArgb(214, 220, 229), 1.2f))
            using (var text = new SolidBrush(Color.FromArgb(30, 36, 46)))
            using (var sub = new SolidBrush(Color.FromArgb(95, 105, 120)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
                g.DrawString(group.Title, bold, text, new RectangleF(group.Rect.X, group.Rect.Y + 8, group.Rect.Width, 20), center);
                if (!string.IsNullOrEmpty(group.Subtitle))
                    g.DrawString(group.Subtitle, small, sub, new RectangleF(group.Rect.X, group.Rect.Y + 28, group.Rect.Width, 18), center);
            }
        }

        private void DrawConnector(Graphics g, Connector c, Font small, StringFormat center)
        {
            using (var pen = new Pen(Color.FromArgb(125, 140, 160), 1.6f))
            {
                if (c.Arrow) pen.CustomEndCap = new AdjustableArrowCap(4.5f, 4.5f, true);
                g.DrawLines(pen, c.Points);
            }
            if (string.IsNullOrEmpty(c.Label)) return;
            SizeF size = g.MeasureString(c.Label, small);
            var r = new RectangleF(c.LabelAt.X - size.Width / 2f - 3, c.LabelAt.Y - size.Height / 2f, size.Width + 6, size.Height);
            using (var back = new SolidBrush(Color.FromArgb(247, 249, 252)))
            using (var text = new SolidBrush(Color.FromArgb(60, 70, 85)))
            {
                g.FillRectangle(back, r);
                g.DrawString(c.Label, small, text, r, center);
            }
        }

        private static void DrawBox(Graphics g, Box box, Font normal, Font small, StringFormat center)
        {
            using (var path = RoundedRectangle(box.Rect, 7f))
            using (var fill = new SolidBrush(Color.White))
            using (var border = new Pen(Color.FromArgb(190, 198, 210), 1.2f))
            using (var text = new SolidBrush(Color.FromArgb(30, 36, 46)))
            using (var sub = new SolidBrush(Color.FromArgb(95, 105, 120)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
                if (string.IsNullOrEmpty(box.Sub))
                    g.DrawString(box.Text, normal, text, box.Rect, center);
                else
                {
                    var top = new RectangleF(box.Rect.X, box.Rect.Y + 2, box.Rect.Width, box.Rect.Height / 2f);
                    var bottom = new RectangleF(box.Rect.X, box.Rect.Y + box.Rect.Height / 2f - 2, box.Rect.Width, box.Rect.Height / 2f);
                    g.DrawString(box.Text, normal, text, top, center);
                    g.DrawString(box.Sub, small, sub, bottom, center);
                }
            }
        }

        private static void DrawPill(Graphics g, Pill pill, Font small, StringFormat center)
        {
            bool filled = pill.Active || pill.Always;
            Color fill = filled ? pill.Accent : Color.FromArgb(238, 241, 245);
            Color border = filled ? pill.Accent : Color.FromArgb(170, 180, 195);
            Color text = filled ? Color.White : Color.FromArgb(70, 80, 95);
            using (var path = RoundedRectangle(pill.Rect, pill.Rect.Height / 2f))
            using (var fillBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, 1.2f))
            using (var textBrush = new SolidBrush(text))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString(pill.Text, small, textBrush, pill.Rect, center);
            }
        }

        private static void DrawNote(Graphics g, Note note, Font small, StringFormat left)
        {
            using (var font = new Font(small, note.Bold ? FontStyle.Bold : FontStyle.Regular))
            using (var brush = new SolidBrush(note.Color))
                g.DrawString(note.Text, font, brush, note.At, left);
        }

        private void DrawNode(Graphics g, Node node, Font bold, StringFormat center)
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
                    fill = node == _hot ? Color.FromArgb(226, 238, 252) : Color.FromArgb(244, 249, 255);
                    border = node.Accent; text = Color.FromArgb(30, 36, 46); break;
                default:
                    fill = Color.FromArgb(240, 241, 243); border = Color.FromArgb(205, 209, 215);
                    text = Color.FromArgb(140, 146, 156); break;
            }
            var shadow = node.Rect;
            shadow.Offset(0, 2);
            using (var shadowPath = RoundedRectangle(shadow, 8f))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(24, 30, 50, 80)))
                g.FillPath(shadowBrush, shadowPath);
            using (var path = RoundedRectangle(node.Rect, 8f))
            using (var fillBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, node.Visual == FlowNodeVisual.Ready || node.Visual == FlowNodeVisual.Current ? 2f : 1.3f))
            using (var textBrush = new SolidBrush(text))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
                var inner = node.Rect;
                inner.Inflate(-6, 0);
                g.DrawString(caption, bold, textBrush, inner, center);
            }
        }

        private void DrawBanner(Graphics g, Font font)
        {
            if (_banner.Length == 0) return;
            var r = new RectangleF(14, 524, CanvasWidth - 28, 40);
            using (var path = RoundedRectangle(r, 6f))
            using (var fill = new SolidBrush(Color.FromArgb(255, 244, 214)))
            using (var accent = new SolidBrush(Color.FromArgb(240, 170, 20)))
            using (var border = new Pen(Color.FromArgb(235, 205, 130), 1f))
            using (var text = new SolidBrush(Color.FromArgb(60, 50, 20)))
            using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
                g.FillRectangle(accent, r.X, r.Y + 4, 5, r.Height - 8);
                g.DrawString("⚠  " + _banner, font, text, new RectangleF(r.X + 14, r.Y, r.Width - 24, r.Height), format);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ------------------------------------------------------------------ chuột
        private Node HitTest(Point point)
        {
            UpdateTransform();
            PointF p = ToCanvas(point);
            foreach (var node in _nodes)
                if (node.Rect.Contains(p)) return node;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var node = HitTest(e.Location);
            if (node == _hot) return;
            _hot = node;
            Cursor = node != null && node.Enabled ? Cursors.Hand : Cursors.Default;
            _toolTip.SetToolTip(this, node != null && node.Tip.Length > 0 ? node.Tip : string.Empty);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot == null) return;
            _hot = null;
            Cursor = Cursors.Default;
            Invalidate();
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
