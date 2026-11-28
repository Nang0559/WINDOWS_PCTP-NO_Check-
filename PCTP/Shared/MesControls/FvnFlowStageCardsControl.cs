using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PCTP.Shared.MesControls
{
    public enum FvnCardBadgeKind { None, Info, Warning, Danger, Success }

    /// <summary>Dữ liệu một thẻ chặng (stage card). Control chỉ vẽ, không truy vấn database.</summary>
    public sealed class FvnFlowStageCard
    {
        public string Caption = string.Empty;      // "01 KẾ HOẠCH"
        public string Value = "0";                 // số lớn
        public string Unit = string.Empty;         // "Lệnh", "Lô", "pcs"
        public string SubText = string.Empty;      // dòng phụ bên dưới số lớn
        public string BadgeText = string.Empty;    // nhãn cảnh báo (bên phải dòng phụ)
        public FvnCardBadgeKind BadgeKind = FvnCardBadgeKind.None;
        public string BadgeText2 = string.Empty;   // nhãn phụ thứ hai (tuỳ chọn)
        public FvnCardBadgeKind BadgeKind2 = FvnCardBadgeKind.None;
        public string Glyph = "■";                 // ký hiệu trong ô icon
        public Color Accent = Color.FromArgb(46, 110, 190);
    }

    /// <summary>
    /// Dải thẻ chặng có mũi tên nối: dùng cho "Dòng chảy sản xuất" (5 chặng) và "Dòng chảy linh kiện" (6 chặng).
    /// Mỗi thẻ gồm caption, icon, số lớn + đơn vị, dòng phụ, badge cảnh báo và thanh màu ở đáy.
    /// Bấm vào thẻ phát sự kiện CardClicked (chỉ số 0-based) để form lọc lưới theo chặng.
    /// </summary>
    public sealed class FvnFlowStageCardsControl : Control
    {
        private readonly List<FvnFlowStageCard> _cards = new List<FvnFlowStageCard>();
        private int _selected = -1;
        private int _hot = -1;

        public event EventHandler<FvnStageCardClickEventArgs> CardClicked;

        public FvnFlowStageCardsControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Height = 112;
            MinimumSize = new Size(600, 96);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.Tan;
        }

        /// <summary>Chặng đang được chọn để lọc (-1 = không lọc).</summary>
        public int SelectedIndex
        {
            get { return _selected; }
            set { if (_selected == value) return; _selected = value; Invalidate(); }
        }

        public void SetCards(IList<FvnFlowStageCard> cards)
        {
            _cards.Clear();
            if (cards != null) _cards.AddRange(cards);
            if (_selected >= _cards.Count) _selected = -1;
            Invalidate();
        }

        private const float ArrowGap = 26f;

        private RectangleF CardRect(int index)
        {
            int n = Math.Max(1, _cards.Count);
            float width = (ClientSize.Width - 8f - ArrowGap * (n - 1)) / n;
            float x = 4f + index * (width + ArrowGap);
            return new RectangleF(x, 2f, width, ClientSize.Height - 8f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var back = new SolidBrush(Parent != null ? Parent.BackColor : SystemColors.Control))
                g.FillRectangle(back, ClientRectangle);
            if (_cards.Count == 0) return;

            for (int i = 0; i < _cards.Count; i++)
            {
                RectangleF r = CardRect(i);
                DrawCard(g, _cards[i], r, i == _selected, i == _hot);
                if (i < _cards.Count - 1) DrawArrow(g, r);
            }
        }

        private static void DrawArrow(Graphics g, RectangleF card)
        {
            float cx = card.Right + ArrowGap / 2f;
            float cy = card.Top + card.Height / 2f;
            using (var pen = new Pen(Color.FromArgb(130, 140, 155), 1.6f))
            {
                pen.EndCap = LineCap.Round;
                pen.StartCap = LineCap.Round;
                g.DrawLine(pen, cx - 7, cy, cx + 7, cy);
                g.DrawLine(pen, cx + 2, cy - 5, cx + 7, cy);
                g.DrawLine(pen, cx + 2, cy + 5, cx + 7, cy);
            }
        }

        private void DrawCard(Graphics g, FvnFlowStageCard card, RectangleF r, bool selected, bool hot)
        {
            float shadowOffset = 2f;
            var shadow = r;
            shadow.Offset(0, shadowOffset);
            using (var sp = Round(shadow, 8f))
            using (var sb = new SolidBrush(Color.FromArgb(22, 20, 40, 70)))
                g.FillPath(sb, sp);

            using (var path = Round(r, 8f))
            using (var fill = new SolidBrush(hot ? Color.FromArgb(250, 252, 255) : Color.White))
            using (var border = new Pen(selected ? card.Accent : Color.FromArgb(218, 224, 233), selected ? 2f : 1f))
            { g.FillPath(fill, path); g.DrawPath(border, path); }

            // thanh màu đáy thẻ
            var oldClip = g.Clip;
            using (var clipPath = Round(r, 8f))
            {
                g.SetClip(clipPath, CombineMode.Intersect);
                using (var bar = new SolidBrush(card.Accent))
                    g.FillRectangle(bar, r.X, r.Bottom - 4f, r.Width, 4f);
            }
            g.Clip = oldClip;

            // caption
            using (var capFont = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point))
            using (var capBrush = new SolidBrush(Color.FromArgb(30, 38, 52)))
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(card.Caption, capFont, capBrush,
                    new RectangleF(r.X + 14, r.Y + 12, Math.Max(10, r.Width - 66), 22), fmt);

            // icon
            var icon = new RectangleF(r.Right - 52, r.Y + 8, 38, 34);
            using (var ip = Round(icon, 8f))
            using (var ib = new SolidBrush(Color.FromArgb(40, card.Accent)))
                g.FillPath(ib, ip);
            using (var gf = new Font("Segoe UI Symbol", 13F, FontStyle.Bold, GraphicsUnit.Point))
            using (var gb = new SolidBrush(card.Accent))
            using (var gfmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(card.Glyph, gf, gb, icon, gfmt);

            // số lớn + đơn vị
            float valueTop = r.Y + 44;
            using (var vf = new Font("Segoe UI", 22F, FontStyle.Bold, GraphicsUnit.Point))
            using (var vb = new SolidBrush(Color.FromArgb(24, 30, 42)))
            {
                SizeF vs = g.MeasureString(card.Value, vf, 400, StringFormat.GenericTypographic);
                g.DrawString(card.Value, vf, vb, r.X + 14, valueTop, StringFormat.GenericTypographic);
                if (!string.IsNullOrEmpty(card.Unit))
                    using (var uf = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point))
                    using (var ub = new SolidBrush(Color.FromArgb(70, 80, 96)))
                        g.DrawString(card.Unit, uf, ub, r.X + 14 + vs.Width + 6, valueTop + 12, StringFormat.GenericTypographic);
            }

            // dòng phụ + badge
            float subY = r.Bottom - 30;
            float badgeRight = r.Right - 12;
            if (!string.IsNullOrEmpty(card.BadgeText))
                badgeRight = DrawBadge(g, card.BadgeText, card.BadgeKind, badgeRight, subY - 2);
            if (!string.IsNullOrEmpty(card.BadgeText2))
                badgeRight = DrawBadge(g, card.BadgeText2, card.BadgeKind2, badgeRight - 4, subY - 2);
            using (var sf = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point))
            using (var sb = new SolidBrush(Color.FromArgb(90, 100, 116)))
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                g.DrawString(card.SubText, sf, sb,
                    new RectangleF(r.X + 14, subY - 2, Math.Max(10, badgeRight - r.X - 20), 22), fmt);
        }

        /// <summary>Vẽ badge căn phải tại right; trả về mép trái của badge để vẽ tiếp bên trái.</summary>
        private static float DrawBadge(Graphics g, string text, FvnCardBadgeKind kind, float right, float y)
        {
            Color fore, back;
            switch (kind)
            {
                case FvnCardBadgeKind.Danger: fore = Color.FromArgb(190, 35, 35); back = Color.FromArgb(253, 228, 228); break;
                case FvnCardBadgeKind.Warning: fore = Color.FromArgb(160, 90, 0); back = Color.FromArgb(255, 240, 210); break;
                case FvnCardBadgeKind.Success: fore = Color.FromArgb(24, 110, 66); back = Color.FromArgb(222, 244, 232); break;
                case FvnCardBadgeKind.Info: fore = Color.FromArgb(30, 90, 170); back = Color.FromArgb(226, 238, 252); break;
                default: fore = Color.FromArgb(70, 80, 96); back = Color.FromArgb(238, 241, 245); break;
            }
            string mark = kind == FvnCardBadgeKind.Danger ? "● " : kind == FvnCardBadgeKind.Warning ? "▲ " : string.Empty;
            string label = mark + text;
            using (var f = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point))
            {
                SizeF s = g.MeasureString(label, f, 400, StringFormat.GenericTypographic);
                var rect = new RectangleF(right - s.Width - 14, y, s.Width + 14, 22);
                using (var p = Round(rect, 5f))
                using (var bb = new SolidBrush(back))
                    g.FillPath(bb, p);
                using (var fb = new SolidBrush(fore))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(label, f, fb, rect, fmt);
                return rect.Left;
            }
        }

        private static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _cards.Count; i++)
                if (CardRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTest(e.Location);
            if (hit == _hot) return;
            _hot = hit;
            Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot < 0) return;
            _hot = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            int hit = HitTest(e.Location);
            if (hit < 0) return;
            // bấm lại thẻ đang chọn = bỏ lọc
            _selected = _selected == hit ? -1 : hit;
            Invalidate();
            var handler = CardClicked;
            if (handler != null) handler(this, new FvnStageCardClickEventArgs(_selected));
        }
    }

    public sealed class FvnStageCardClickEventArgs : EventArgs
    {
        public FvnStageCardClickEventArgs(int index) { Index = index; }
        /// <summary>Chặng đang chọn (0-based); -1 = đã bỏ lọc.</summary>
        public int Index { get; private set; }
    }
}