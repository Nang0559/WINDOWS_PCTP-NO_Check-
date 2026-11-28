using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Security;

namespace PCTP.Shared.MesControls
{
    /// <summary>Thẻ trắng viền mảnh dùng làm nền cho thanh bộ lọc / vùng lưới (giống thẻ trong màn Control Tower).</summary>
    public sealed class FvnCardPanel : Panel
    {
        public FvnCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.White;
            Padding = new Padding(10, 6, 10, 6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var back = new SolidBrush(BackColor)) e.Graphics.FillRectangle(back, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(214, 220, 229)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>Các thành phần của khung màn hình chuẩn mà form cần giữ lại để cập nhật.</summary>
    public sealed class FvnConsoleScreenFrame
    {
        public FvnConsoleHeaderControl Header { get; internal set; }
        public FvnConsoleStatusBarControl StatusBar { get; internal set; }

        /// <summary>Ghi thông điệp lên thanh trạng thái đáy; <paramref name="warning"/> = true thì nền vàng.</summary>
        public void SetStatus(string message, bool warning)
        {
            StatusBar.IsWarning = warning;
            StatusBar.Message = message ?? string.Empty;
        }

        /// <summary>Cập nhật nhãn RUNTIME ở góc phải tiêu đề, ví dụ "RUNTIME ACTIVE | Site 1000 | 28/11/2026 11:58".</summary>
        public void SetRuntime(string scope, bool active)
        {
            Header.RuntimeText = (active ? "RUNTIME ACTIVE" : "RUNTIME READY") +
                (string.IsNullOrWhiteSpace(scope) ? string.Empty : " | " + scope.Trim()) +
                " | " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }
    }

    /// <summary>
    /// Khung giao diện thống nhất cho màn hình MES / Kho linh kiện: nền sáng, tiêu đề lớn + nhãn RUNTIME,
    /// thẻ bộ lọc trắng (SITE + điều kiện), vùng nội dung, thanh trạng thái đáy (User | System | giờ).
    /// Chỉ trình bày; không có nghiệp vụ, không mở kết nối SQL.
    /// </summary>
    public static class FvnConsoleScreen
    {
        public const int FilterCardHeight = 56;

        /// <summary>
        /// Dựng khung cho <paramref name="form"/>: header (64px) → thẻ bộ lọc → body (co giãn) + status bar ở đáy.
        /// <paramref name="filterCard"/> có thể null khi màn hình không có bộ lọc.
        /// </summary>
        public static FvnConsoleScreenFrame Compose(Form form, string title, string subtitle,
            Control filterCard, int filterHeight, Control body)
        {
            if (form == null) throw new ArgumentNullException("form");
            if (body == null) throw new ArgumentNullException("body");

            var frame = new FvnConsoleScreenFrame
            {
                Header = new FvnConsoleHeaderControl { Dock = DockStyle.Fill, Title = title, Subtitle = subtitle },
                StatusBar = new FvnConsoleStatusBarControl()
            };
            UserSession session = UserSession.Current;
            frame.StatusBar.UserName = session == null ? string.Empty
                : (string.IsNullOrWhiteSpace(session.DisplayName) ? session.UserName : session.DisplayName);
            frame.SetRuntime(null, false);

            bool hasFilter = filterCard != null;
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(12, 8, 12, 8), BackColor = FvnConsoleTheme.Background
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, hasFilter ? filterHeight + 8 : 0));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(frame.Header, 0, 0);
            if (hasFilter)
            {
                filterCard.Dock = DockStyle.Fill;
                filterCard.Margin = new Padding(0, 0, 0, 8);
                root.Controls.Add(filterCard, 0, 1);
            }
            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(0);
            root.Controls.Add(body, 0, 2);

            form.BackColor = FvnConsoleTheme.Background;
            form.Controls.Add(root);
            form.Controls.Add(frame.StatusBar);   // thêm sau cùng để neo đáy trước, root lấp phần còn lại
            return frame;
        }

        /// <summary>Thẻ bộ lọc: các control xếp ngang trong một thẻ trắng; dùng <see cref="Caption"/> cho nhãn.</summary>
        public static FvnCardPanel CreateFilterCard(params Control[] controls)
        {
            var card = new FvnCardPanel();
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false, AutoScroll = false,
                FlowDirection = FlowDirection.LeftToRight, BackColor = Color.White, Padding = new Padding(0, 3, 0, 0)
            };
            foreach (Control c in controls)
            {
                if (c == null) continue;
                if (!(c is LabelControl)) c.Margin = new Padding(2, 2, 10, 0);
                flow.Controls.Add(c);
            }
            card.Controls.Add(flow);
            return card;
        }

        /// <summary>Nhãn in hoa của thẻ bộ lọc ("SITE", "TỪ NGÀY"...).</summary>
        public static LabelControl Caption(string text)
        {
            return new LabelControl
            {
                Text = (text ?? string.Empty).ToUpperInvariant(),
                AutoSizeMode = LabelAutoSizeMode.Default,
                Margin = new Padding(4, 0, 2, 0),
                Padding = new Padding(0, 7, 0, 0)
            };
        }

        public static void StyleButton(SimpleButton button, string text, int width, EventHandler click, bool enabled = true)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 32;
            button.Enabled = enabled;
            button.Margin = new Padding(2, 0, 8, 0);
            if (click != null) button.Click += click;
        }

        /// <summary>Lưới chỉ đọc theo chuẩn màn hình điều hành (giống FvnDepartmentTaskGridControl).</summary>
        public static void StyleGrid(DevExpress.XtraGrid.GridControl grid, GridView view)
        {
            grid.Dock = DockStyle.Fill;
            grid.MainView = view;
            grid.ViewCollection.Add(view);
            view.OptionsBehavior.Editable = false;
            view.OptionsBehavior.ReadOnly = true;
            view.OptionsView.ShowGroupPanel = false;
            view.OptionsView.ShowAutoFilterRow = true;
            view.OptionsView.ShowIndicator = true;
            view.OptionsView.ColumnAutoWidth = false;
            view.OptionsSelection.MultiSelect = false;
            view.OptionsSelection.EnableAppearanceFocusedCell = false;
            view.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            view.RowHeight = 28;
        }
    }
}
