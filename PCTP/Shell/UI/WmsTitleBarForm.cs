using PCTP.Shell.Help;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shell.UI
{
    /// <summary>
    /// PCTP custom title bar base form.
    /// Native ControlBox/caption buttons are disabled and replaced by:
    /// [Icon] [Title] [ ? ][ _ ][ □ ][ X ]
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    public abstract class WmsTitleBarForm : XtraForm
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private readonly PanelControl _titleBar;
        private readonly LabelControl _iconLabel;
        private readonly LabelControl _titleLabel;
        private readonly SimpleButton _helpButton;
        private readonly SimpleButton _minimizeButton;
        private readonly SimpleButton _maximizeButton;
        private readonly SimpleButton _closeButton;
        private readonly PanelControl _contentHost;
        private bool _layoutBuilt;
        private bool _normalStateWasMaximized;
        private Rectangle _restoreBounds;

        protected WmsTitleBarForm()
        {
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            MinimumSize = new Size(320, 180);

            _titleBar = new PanelControl
            {
                Dock = DockStyle.Top,
                Height = 38,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                LookAndFeel = { UseDefaultLookAndFeel = false }
            };
            _titleBar.Appearance.BackColor = Color.FromArgb(45, 45, 48);
            _titleBar.Appearance.Options.UseBackColor = true;

            _iconLabel = new LabelControl
            {
                Dock = DockStyle.Left,
                Width = 38,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Center, VAlignment = DevExpress.Utils.VertAlignment.Center } }
            };

            _closeButton = CreateCaptionButton("X", true);
            _maximizeButton = CreateCaptionButton("□", false);
            _minimizeButton = CreateCaptionButton("_", false);
            _helpButton = CreateCaptionButton("?", false);

            _closeButton.Dock = DockStyle.Right;
            _maximizeButton.Dock = DockStyle.Right;
            _minimizeButton.Dock = DockStyle.Right;
            _helpButton.Dock = DockStyle.Right;

            _titleLabel = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(6, 0, 8, 0),
                Appearance =
                {
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Options = { UseFont = true, UseForeColor = true },
                    TextOptions = { VAlignment = DevExpress.Utils.VertAlignment.Center }
                }
            };

            _titleBar.Controls.Add(_titleLabel);
            _titleBar.Controls.Add(_helpButton);
            _titleBar.Controls.Add(_minimizeButton);
            _titleBar.Controls.Add(_maximizeButton);
            _titleBar.Controls.Add(_closeButton);
            _titleBar.Controls.Add(_iconLabel);

            _contentHost = new PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };

            _closeButton.Click += delegate { Close(); };
            _minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            _maximizeButton.Click += delegate { ToggleMaximize(); };
            _helpButton.Click += delegate { ShowHelp(); };
            _titleBar.DoubleClick += delegate { ToggleMaximize(); };
            _titleLabel.DoubleClick += delegate { ToggleMaximize(); };
            _iconLabel.DoubleClick += delegate { ToggleMaximize(); };
            _titleBar.MouseDown += TitleBar_MouseDown;
            _titleLabel.MouseDown += TitleBar_MouseDown;
            _iconLabel.MouseDown += TitleBar_MouseDown;
            TextChanged += delegate { _titleLabel.Text = Text; };
            KeyDown += WmsTitleBarForm_KeyDown;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (_layoutBuilt)
                return;

            _layoutBuilt = true;
            BuildContentHost();
            _titleLabel.Text = Text;
            UpdateIcon();
            UpdateMaximizeGlyph();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateIcon();
            UpdateMaximizeGlyph();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_layoutBuilt)
                UpdateMaximizeGlyph();
        }

        private void BuildContentHost()
        {
            Control[] existing = new Control[Controls.Count];
            Controls.CopyTo(existing, 0);

            foreach (Control control in existing)
            {
                if (control == _titleBar || control == _contentHost)
                    continue;
                Controls.Remove(control);
                _contentHost.Controls.Add(control);
            }

            Controls.Add(_contentHost);
            Controls.Add(_titleBar);
            _titleBar.BringToFront();
        }

        private SimpleButton CreateCaptionButton(string text, bool isClose)
        {
            var button = new SimpleButton
            {
                Text = text,
                Width = 42,
                Height = 38,
                Dock = DockStyle.Right,
                AllowFocus = false,
                ShowFocusRectangle = false,
                ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                LookAndFeel = { UseDefaultLookAndFeel = false }
            };
            button.Appearance.BackColor = Color.FromArgb(45, 45, 48);
            button.Appearance.ForeColor = Color.White;
            button.Appearance.Font = new Font("Segoe UI", 10F, isClose ? FontStyle.Bold : FontStyle.Regular);
            button.Appearance.Options.UseBackColor = true;
            button.Appearance.Options.UseForeColor = true;
            button.Appearance.Options.UseFont = true;
            button.AppearanceHovered.BackColor = isClose ? Color.FromArgb(196, 43, 28) : Color.FromArgb(70, 70, 74);
            button.AppearanceHovered.Options.UseBackColor = true;
            return button;
        }

        private void UpdateIcon()
        {
            if (Icon == null)
            {
                _iconLabel.Text = "";
                return;
            }

            _iconLabel.ImageOptions.Image = Icon.ToBitmap();
            _iconLabel.ImageOptions.SvgImage = null;
            _iconLabel.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
        }

        private void UpdateMaximizeGlyph()
        {
            _maximizeButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
        }

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                if (_restoreBounds.Width > 0 && _restoreBounds.Height > 0)
                    Bounds = _restoreBounds;
            }
            else
            {
                _restoreBounds = Bounds;
                _normalStateWasMaximized = true;
                WindowState = FormWindowState.Maximized;
            }
            UpdateMaximizeGlyph();
        }

        private void ShowHelp()
        {
            try
            {
                new WmsHelpService().ShowCurrent(this);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this,
                    "Không thể mở hướng dẫn cho màn hình hiện tại.\r\n" + ex.Message,
                    "WMS - Hướng dẫn",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void WmsTitleBarForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F1 || e.Handled)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            ShowHelp();
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();
            SendMessage(Handle, 0xA1, new IntPtr(HTCAPTION), IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState != FormWindowState.Maximized)
            {
                int x = GetSignedLowWord(m.LParam);
                int y = GetSignedHighWord(m.LParam);
                Point point = PointToClient(new Point(x, y));
                int grip = 6;

                if (point.Y < grip)
                {
                    if (point.X < grip) { m.Result = (IntPtr)HTTOPLEFT; return; }
                    if (point.X >= Width - grip) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                    m.Result = (IntPtr)HTTOP;
                    return;
                }

                if (point.Y >= Height - grip)
                {
                    if (point.X < grip) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                    if (point.X >= Width - grip) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    m.Result = (IntPtr)HTBOTTOM;
                    return;
                }

                if (point.X < grip) { m.Result = (IntPtr)HTLEFT; return; }
                if (point.X >= Width - grip) { m.Result = (IntPtr)HTRIGHT; return; }
            }

            base.WndProc(ref m);
        }

        private static int GetSignedLowWord(IntPtr value)
        {
            return (short)((long)value & 0xFFFF);
        }

        private static int GetSignedHighWord(IntPtr value)
        {
            return (short)(((long)value >> 16) & 0xFFFF);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
