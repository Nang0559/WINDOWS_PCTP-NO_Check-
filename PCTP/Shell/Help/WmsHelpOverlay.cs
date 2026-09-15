using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace PCTP.Shell.Help
{
    /// <summary>
    /// Provides contextual WMS help without placing controls over the form client area.
    /// A small Help button is rendered in the non-client title bar, immediately before
    /// the standard Minimize/Maximize/Close caption buttons. F1 remains supported.
    /// </summary>
    internal static class WmsHelpOverlay
    {
        private static readonly HashSet<Form> AttachedForms = new HashSet<Form>();
        private static readonly Dictionary<Form, WmsHelpNativeWindow> NativeWindows =
            new Dictionary<Form, WmsHelpNativeWindow>();
        private static readonly WmsHelpService HelpService = new WmsHelpService();
        private static bool _started;

        internal static void Start()
        {
            if (_started)
                return;

            _started = true;
            Application.Idle += Application_Idle;
        }

        private static void Application_Idle(object sender, EventArgs e)
        {
            try
            {
                Form[] forms = Application.OpenForms.Cast<Form>().ToArray();

                foreach (Form form in forms)
                    EnsureAttached(form);

                Form[] staleForms = AttachedForms
                    .Where(form => form == null || form.IsDisposed || !forms.Contains(form))
                    .ToArray();

                foreach (Form form in staleForms)
                    Detach(form);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WmsHelpOverlay: " + ex);
            }
        }

        private static void EnsureAttached(Form form)
        {
            if (form == null || form.IsDisposed || form.Disposing)
                return;

            if (form is FormWmsHelp)
                return;

            if (!form.IsHandleCreated)
                return;

            if (!AttachedForms.Contains(form))
            {
                WmsHelpNativeWindow nativeWindow = new WmsHelpNativeWindow(form, ShowHelp);
                nativeWindow.Attach();
                NativeWindows[form] = nativeWindow;

                form.FormClosed += Form_FormClosed;
                AttachF1(form);
                AttachedForms.Add(form);
            }
        }

        private static void AttachF1(Form form)
        {
            form.KeyPreview = true;
            form.KeyDown -= Form_KeyDown;
            form.KeyDown += Form_KeyDown;
        }

        private static void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F1 || e.Handled)
                return;

            Form form = sender as Form;
            if (form == null || form.IsDisposed)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            ShowHelp(form);
        }

        private static void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            Detach(sender as Form);
        }

        private static void Detach(Form form)
        {
            if (form == null)
                return;

            WmsHelpNativeWindow nativeWindow;
            if (NativeWindows.TryGetValue(form, out nativeWindow))
            {
                nativeWindow.Dispose();
                NativeWindows.Remove(form);
            }

            form.FormClosed -= Form_FormClosed;
            form.KeyDown -= Form_KeyDown;
            AttachedForms.Remove(form);
        }

        private static void ShowHelp(Form owner)
        {
            try
            {
                if (owner == null || owner.IsDisposed)
                    return;

                HelpService.ShowCurrent(owner);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WmsHelpOverlay.ShowHelp: " + ex);
                MessageBox.Show(
                    owner,
                    "Không thể mở hướng dẫn cho màn hình hiện tại.\r\n" + ex.Message,
                    "WMS - Hướng dẫn",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private sealed class WmsHelpNativeWindow : NativeWindow
        {
            private const int WM_NCHITTEST = 0x0084;
            private const int WM_NCLBUTTONDOWN = 0x00A1;
            private const int WM_NCPAINT = 0x0085;
            private const int WM_NCACTIVATE = 0x0086;
            private const int WM_SIZE = 0x0005;
            private const int HTHELP = 21;

            private readonly Form _owner;
            private readonly Action<Form> _showHelp;

            internal WmsHelpNativeWindow(Form owner, Action<Form> showHelp)
            {
                _owner = owner;
                _showHelp = showHelp;
            }

            internal void Attach()
            {
                if (_owner == null || !_owner.IsHandleCreated)
                    return;

                AssignHandle(_owner.Handle);
                RedrawCaptionButton();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    if (IsHelpButtonPoint(m.LParam))
                    {
                        m.Result = (IntPtr)HTHELP;
                        return;
                    }
                }
                else if (m.Msg == WM_NCLBUTTONDOWN)
                {
                    if (m.WParam.ToInt32() == HTHELP)
                    {
                        _showHelp(_owner);
                        return;
                    }
                }

                base.WndProc(ref m);

                if (m.Msg == WM_NCPAINT || m.Msg == WM_NCACTIVATE || m.Msg == WM_SIZE)
                    RedrawCaptionButton();
            }

            private bool IsHelpButtonPoint(IntPtr lParam)
            {
                int x = GetSignedLowWord(lParam);
                int y = GetSignedHighWord(lParam);
                return GetHelpButtonRectangle().Contains(new Point(x, y));
            }

            private Rectangle GetHelpButtonRectangle()
            {
                Rectangle window = _owner.RectangleToScreen(_owner.ClientRectangle);
                Point windowOrigin = _owner.PointToScreen(Point.Empty);

                int windowLeft = windowOrigin.X;
                int windowTop = windowOrigin.Y;
                int windowRight = windowLeft + _owner.Width;

                int buttonWidth = Math.Max(30, SystemInformation.CaptionButtonSize.Width);
                int buttonHeight = Math.Max(20, SystemInformation.CaptionHeight);
                int standardButtonCount = GetStandardCaptionButtonCount();

                int right = windowRight - (buttonWidth * standardButtonCount);
                return new Rectangle(
                    right - buttonWidth,
                    windowTop,
                    buttonWidth,
                    buttonHeight);
            }

            private int GetStandardCaptionButtonCount()
            {
                int count = 0;

                if (_owner.ControlBox)
                    count++;

                if (_owner.MaximizeBox && _owner.FormBorderStyle != FormBorderStyle.FixedDialog)
                    count++;

                if (_owner.MinimizeBox && _owner.FormBorderStyle != FormBorderStyle.FixedDialog)
                    count++;

                return Math.Max(1, count);
            }

            private void RedrawCaptionButton()
            {
                if (_owner == null || _owner.IsDisposed || !_owner.IsHandleCreated)
                    return;

                IntPtr hdc = GetWindowDC(_owner.Handle);
                if (hdc == IntPtr.Zero)
                    return;

                try
                {
                    Rectangle screenRect = GetHelpButtonRectangle();
                    Rectangle windowRect = _owner.RectangleToScreen(_owner.ClientRectangle);
                    Rectangle drawRect = new Rectangle(
                        screenRect.Left - windowRect.Left,
                        screenRect.Top - windowRect.Top,
                        screenRect.Width,
                        screenRect.Height);

                    using (Graphics graphics = Graphics.FromHdc(hdc))
                    {
                        DrawCaptionHelpButton(graphics, drawRect);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("WmsHelpOverlay.RedrawCaptionButton: " + ex);
                }
                finally
                {
                    ReleaseDC(_owner.Handle, hdc);
                }
            }

            private static void DrawCaptionHelpButton(Graphics graphics, Rectangle bounds)
            {
                try
                {
                    VisualStyleElement element = VisualStyleElement.Window.CaptionButton.Help;
                    if (VisualStyleRenderer.IsElementDefined(element))
                    {
                        VisualStyleRenderer renderer = new VisualStyleRenderer(element);
                        renderer.DrawBackground(graphics, bounds);
                        return;
                    }
                }
                catch
                {
                    // Fall through to the lightweight fallback glyph.
                }

                using (SolidBrush brush = new SolidBrush(SystemColors.ActiveCaptionText))
                using (Font font = new Font("Segoe UI", 10f, FontStyle.Bold))
                {
                    StringFormat format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    graphics.DrawString("?", font, brush, bounds, format);
                }
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
            private static extern IntPtr GetWindowDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        }
    }
}
