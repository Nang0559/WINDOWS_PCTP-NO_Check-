using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PCTP.Shared.MesControls
{
    /// <summary>
    /// Tiêu đề màn hình điều hành: tiêu đề lớn + dòng mô tả + nhãn RUNTIME ACTIVE ở góc phải.
    /// Chỉ trình bày; nội dung do form truyền vào.
    /// </summary>
    public sealed class FvnConsoleHeaderControl : Control
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private string _runtime = string.Empty;

        public FvnConsoleHeaderControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 64;
            Dock = DockStyle.Top;
        }

        public string Title { get { return _title; } set { _title = value ?? string.Empty; Invalidate(); } }
        public string Subtitle { get { return _subtitle; } set { _subtitle = value ?? string.Empty; Invalidate(); } }
        /// <summary>Ví dụ "RUNTIME ACTIVE | Line 01 | 28/11/2026 11:58". Rỗng = ẩn nhãn.</summary>
        public string RuntimeText { get { return _runtime; } set { _runtime = value ?? string.Empty; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var back = new SolidBrush(Parent != null ? Parent.BackColor : SystemColors.Control))
                g.FillRectangle(back, ClientRectangle);

            float badgeLeft = ClientSize.Width - 8;
            if (_runtime.Length > 0)
            {
                using (var f = new Font("Segoe UI", 9.5F, FontStyle.Regular))
                {
                    // Đo bằng cùng StringFormat dùng để vẽ (GenericTypographic đo hẹp hơn nên chữ cuối bị cắt) và chừa lề phải.
                    SizeF s = g.MeasureString(_runtime, f, 2000, StringFormat.GenericDefault);
                    float badgeWidth = Math.Min(ClientSize.Width - 16, s.Width + 52);
                    var r = new RectangleF(ClientSize.Width - badgeWidth - 12, 10, badgeWidth, 30);
                    using (var p = Round(r, 7f))
                    using (var fill = new SolidBrush(Color.FromArgb(232, 247, 238)))
                    using (var border = new Pen(Color.FromArgb(86, 190, 128), 1.2f))
                    {
                        g.FillPath(fill, p);
                        g.DrawPath(border, p);
                    }
                    using (var dot = new SolidBrush(Color.FromArgb(34, 160, 84)))
                        g.FillEllipse(dot, r.X + 12, r.Y + 11, 9, 9);
                    using (var tb = new SolidBrush(Color.FromArgb(20, 90, 50)))
                    using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(_runtime, f, tb, new RectangleF(r.X + 28, r.Y, r.Width - 34, r.Height), fmt);
                    badgeLeft = r.Left;
                }
            }

            using (var tf = new Font("Segoe UI", 15F, FontStyle.Bold))
            using (var tb = new SolidBrush(Color.FromArgb(24, 30, 42)))
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(_title, tf, tb, new RectangleF(8, 4, Math.Max(10, badgeLeft - 16), 30), fmt);
            using (var sf = new Font("Segoe UI", 9.5F, FontStyle.Regular))
            using (var sb = new SolidBrush(Color.FromArgb(70, 80, 96)))
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(_subtitle, sf, sb, new RectangleF(8, 36, Math.Max(10, ClientSize.Width - 16), 22), fmt);
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
    }

    /// <summary>
    /// Thanh trạng thái đáy màn hình: thông điệp bên trái (có thể là cảnh báo), bên phải
    /// "User: ..." | "System: Connected/Disconnected" | giờ hiện tại. Tự cập nhật giờ mỗi giây.
    /// </summary>
    public sealed class FvnConsoleStatusBarControl : Control
    {
        private string _message = string.Empty;
        private bool _warning;
        private string _user = string.Empty;
        private bool _connected = true;
        private readonly Timer _clock = new Timer();

        public FvnConsoleStatusBarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 30;
            Dock = DockStyle.Bottom;
            _clock.Interval = 1000;
            _clock.Tick += (s, e) => Invalidate();
            _clock.Start();
        }

        public string Message { get { return _message; } set { _message = value ?? string.Empty; Invalidate(); } }
        public bool IsWarning { get { return _warning; } set { _warning = value; Invalidate(); } }
        public string UserName { get { return _user; } set { _user = value ?? string.Empty; Invalidate(); } }
        public bool IsConnected { get { return _connected; } set { _connected = value; Invalidate(); } }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _clock.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Color back = _warning ? Color.FromArgb(255, 244, 214) : Color.FromArgb(243, 245, 248);
            using (var b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);
            using (var line = new Pen(Color.FromArgb(214, 220, 229)))
                g.DrawLine(line, 0, 0, ClientSize.Width, 0);

            string right = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            string sys = "System: " + (_connected ? "Connected" : "Disconnected");
            string user = "User: " + _user;
            using (var f = new Font("Segoe UI", 9F))
            using (var tb = new SolidBrush(Color.FromArgb(50, 58, 72)))
            using (var fmtR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            using (var fmtL = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                float x = ClientSize.Width - 10;
                float wTime = g.MeasureString(right, f, 400, StringFormat.GenericTypographic).Width;
                g.DrawString(right, f, tb, new RectangleF(x - wTime - 4, 0, wTime + 8, ClientSize.Height), fmtR);
                x -= wTime + 28;
                float wSys = g.MeasureString(sys, f, 400, StringFormat.GenericTypographic).Width;
                using (var dot = new SolidBrush(_connected ? Color.FromArgb(34, 160, 84) : Color.FromArgb(200, 55, 45)))
                    g.FillEllipse(dot, x - wSys - 18, ClientSize.Height / 2f - 4, 8, 8);
                g.DrawString(sys, f, tb, new RectangleF(x - wSys - 4, 0, wSys + 8, ClientSize.Height), fmtR);
                x -= wSys + 40;
                float wUser = g.MeasureString(user, f, 600, StringFormat.GenericTypographic).Width;
                g.DrawString(user, f, tb, new RectangleF(x - wUser - 4, 0, wUser + 8, ClientSize.Height), fmtR);
                float leftLimit = x - wUser - 20;

                string prefix = _warning ? "⚠  " : string.Empty;
                g.DrawString(prefix + _message, f, tb,
                    new RectangleF(10, 0, Math.Max(10, leftLimit - 10), ClientSize.Height), fmtL);
            }
        }
    }
}