using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    /// <summary>
    /// Timeline dạng node + đường nối: đường dọc ở giữa, mỗi sự kiện là một vòng tròn đánh số,
    /// nét đứt nối sang thẻ nội dung (trái/phải xen kẽ khi đủ rộng, một bên khi hẹp).
    /// Tự vẽ (GDI+), cuộn dọc, bấm vào thẻ/node để chọn.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class TraceTimelineControl : ScrollableControl
    {
        private const int RowHeight = 100;
        private const int CardHeight = 84;
        private const int CircleRadius = 17;
        private const int TopPadding = 14;
        private const int NarrowWidth = 640;

        private readonly List<TraceTimelineNode> _nodes = new List<TraceTimelineNode>();
        private readonly List<Rectangle> _cards = new List<Rectangle>();
        private readonly List<Point> _centers = new List<Point>();
        private readonly List<bool> _cardOnRight = new List<bool>();
        private readonly Font _titleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        private readonly Font _timeFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        private readonly Font _bodyFont = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        private readonly Font _numberFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        private int _selected = -1;
        private string _emptyText = string.Empty;

        public event EventHandler SelectedNodeChanged;

        public TraceTimelineControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = Color.White;
            AutoScroll = true;
        }

        public TraceTimelineNode SelectedNode
        {
            get { return _selected >= 0 && _selected < _nodes.Count ? _nodes[_selected] : null; }
        }

        public void SetNodes(IList<TraceTimelineNode> nodes, string emptyText)
        {
            _nodes.Clear();
            if (nodes != null) _nodes.AddRange(nodes);
            _emptyText = emptyText ?? string.Empty;
            _selected = _nodes.Count > 0 ? 0 : -1;
            AutoScrollPosition = new Point(0, 0);
            RecomputeLayout();
            Invalidate();
            RaiseSelected();
        }

        public void Clear(string emptyText)
        {
            SetNodes(null, emptyText);
        }

        public void SelectNode(int index)
        {
            if (index < 0 || index >= _nodes.Count || index == _selected) return;
            _selected = index;
            Invalidate();
            RaiseSelected();
        }

        private void RaiseSelected()
        {
            EventHandler handler = SelectedNodeChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecomputeLayout();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }

        private void RecomputeLayout()
        {
            _cards.Clear();
            _centers.Clear();
            _cardOnRight.Clear();

            int width = Math.Max(240, ClientSize.Width);
            bool narrow = width < NarrowWidth;
            int lineX = narrow ? 34 : width / 2;
            int gap = 34;

            for (int i = 0; i < _nodes.Count; i++)
            {
                int cy = TopPadding + i * RowHeight + RowHeight / 2;
                _centers.Add(new Point(lineX, cy));

                bool right = narrow || i % 2 == 1;
                _cardOnRight.Add(right);
                Rectangle card;
                if (right)
                {
                    int x = lineX + CircleRadius + gap;
                    card = new Rectangle(x, cy - CardHeight / 2, Math.Max(160, width - x - 14), CardHeight);
                }
                else
                {
                    int right0 = lineX - CircleRadius - gap;
                    int x = 14;
                    card = new Rectangle(x, cy - CardHeight / 2, Math.Max(160, right0 - x), CardHeight);
                }
                _cards.Add(card);
            }

            int total = _nodes.Count == 0 ? 0 : TopPadding * 2 + _nodes.Count * RowHeight;
            AutoScrollMinSize = new Size(0, total);
        }

        public static Color KindColor(TraceNodeKind kind)
        {
            switch (kind)
            {
                case TraceNodeKind.Delivery: return Color.FromArgb(33, 150, 243);
                case TraceNodeKind.DeliveryLot: return Color.FromArgb(0, 172, 193);
                case TraceNodeKind.Production: return Color.FromArgb(255, 152, 0);
                case TraceNodeKind.StockIn: return Color.FromArgb(76, 175, 80);
                case TraceNodeKind.StockOut: return Color.FromArgb(142, 68, 173);
                case TraceNodeKind.StockOther: return Color.FromArgb(96, 125, 139);
                case TraceNodeKind.Missing: return Color.FromArgb(229, 57, 53);
                default: return Color.FromArgb(198, 40, 40);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_nodes.Count == 0)
            {
                TextRenderer.DrawText(g, _emptyText, _bodyFont, ClientRectangle, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            // Đường dọc nối các node.
            if (_centers.Count > 1)
            {
                using (var pen = new Pen(Color.FromArgb(176, 184, 196), 3F))
                    g.DrawLine(pen, _centers[0], _centers[_centers.Count - 1]);
            }

            int viewTop = -AutoScrollPosition.Y;
            int viewBottom = viewTop + ClientSize.Height;

            for (int i = 0; i < _nodes.Count; i++)
            {
                Rectangle card = _cards[i];
                if (card.Bottom < viewTop - 4 || card.Top > viewBottom + 4) continue;

                TraceTimelineNode node = _nodes[i];
                Color color = KindColor(node.Kind);
                Point c = _centers[i];
                bool selected = i == _selected;
                bool onRight = _cardOnRight[i];

                // Nét đứt nối vòng tròn -> thẻ.
                using (var dash = new Pen(color, 1.6F) { DashStyle = DashStyle.Dash })
                {
                    int x1 = onRight ? c.X + CircleRadius : c.X - CircleRadius;
                    int x2 = onRight ? card.Left : card.Right;
                    g.DrawLine(dash, x1, c.Y, x2, c.Y);
                }

                // Thẻ nội dung.
                using (var path = RoundedRect(card, 8))
                {
                    using (var fill = new SolidBrush(selected ? Color.FromArgb(255, 248, 235) : Color.White))
                        g.FillPath(fill, path);
                    using (var border = new Pen(selected ? color : Color.FromArgb(214, 220, 228), selected ? 2F : 1F))
                        g.DrawPath(border, path);
                }

                DrawCardText(g, card, node, color);

                // Vòng tròn đánh số.
                var circle = new Rectangle(c.X - CircleRadius, c.Y - CircleRadius, CircleRadius * 2, CircleRadius * 2);
                using (var white = new SolidBrush(Color.White))
                    g.FillEllipse(white, circle);
                using (var ring = new Pen(color, 3.5F))
                    g.DrawEllipse(ring, circle);
                TextRenderer.DrawText(g, (i + 1).ToString(), _numberFont, circle, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void DrawCardText(Graphics g, Rectangle card, TraceTimelineNode node, Color color)
        {
            int pad = 9;
            int innerWidth = card.Width - pad * 2;

            // Nhãn (pill) tiêu đề.
            Size titleSize = TextRenderer.MeasureText(node.Title ?? string.Empty, _titleFont);
            int pillWidth = Math.Min(innerWidth, titleSize.Width + 16);
            var pill = new Rectangle(card.Left + pad, card.Top + 7, pillWidth, 20);
            using (var path = RoundedRect(pill, 10))
            using (var brush = new SolidBrush(Color.FromArgb(46, color)))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, node.Title ?? string.Empty, _titleFont, pill, Darken(color),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // Thời gian (nổi bật như năm trong mẫu timeline).
            string time = node.EventTime.HasValue ? node.EventTime.Value.ToString("dd/MM/yyyy HH:mm") : "Không có thời gian";
            var timeRect = new Rectangle(card.Left + pad, card.Top + 30, innerWidth, 20);
            TextRenderer.DrawText(g, time, _timeFont, timeRect, Darken(color),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // Mô tả: LOT + chi tiết.
            string body = "LOT: " + TraceTimelineNode.ValueOrDash(node.LotNo) + Environment.NewLine + (node.Subtitle ?? string.Empty);
            var bodyRect = new Rectangle(card.Left + pad, card.Top + 51, innerWidth, card.Height - 55);
            TextRenderer.DrawText(g, body, _bodyFont, bodyRect, Color.FromArgb(60, 60, 60),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            int index = HitTest(e.Location);
            if (index >= 0) SelectNode(index);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = HitTest(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down) { SelectNode(Math.Min(_nodes.Count - 1, _selected + 1)); EnsureVisible(_selected); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { SelectNode(Math.Max(0, _selected - 1)); EnsureVisible(_selected); e.Handled = true; }
        }

        private void EnsureVisible(int index)
        {
            if (index < 0 || index >= _cards.Count) return;
            Rectangle card = _cards[index];
            int top = -AutoScrollPosition.Y;
            if (card.Top < top) AutoScrollPosition = new Point(0, Math.Max(0, card.Top - 8));
            else if (card.Bottom > top + ClientSize.Height) AutoScrollPosition = new Point(0, card.Bottom - ClientSize.Height + 8);
        }

        private int HitTest(Point clientPoint)
        {
            var p = new Point(clientPoint.X - AutoScrollPosition.X, clientPoint.Y - AutoScrollPosition.Y);
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Contains(p)) return i;
                int dx = p.X - _centers[i].X, dy = p.Y - _centers[i].Y;
                if (dx * dx + dy * dy <= CircleRadius * CircleRadius) return i;
            }
            return -1;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Darken(Color c)
        {
            return Color.FromArgb(c.A, (int)(c.R * 0.78), (int)(c.G * 0.78), (int)(c.B * 0.78));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _timeFont.Dispose();
                _bodyFont.Dispose();
                _numberFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
