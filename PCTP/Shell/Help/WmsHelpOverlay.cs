using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Shell.Help
{
    /// <summary>
    /// Adds one consistent contextual-help entry point to operational forms.
    /// The overlay is intentionally UI-only; topic selection remains in WmsHelpContext.
    /// </summary>
    internal static class WmsHelpOverlay
    {
        private const string ButtonName = "wmsHelpOverlayButton";
        private static readonly HashSet<Form> AttachedForms = new HashSet<Form>();
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

                AttachedForms.RemoveWhere(form => form == null || form.IsDisposed || !forms.Contains(form));
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

            if (AttachedForms.Contains(form))
            {
                PositionButton(form);
                return;
            }

            if (HasExistingHelpEntry(form))
            {
                AttachedForms.Add(form);
                return;
            }

            Button button = new Button();
            button.Name = ButtonName;
            button.Text = "Xem hướng dẫn";
            button.AutoSize = false;
            button.Size = new Size(118, 30);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.FlatStyle = FlatStyle.System;
            button.TabStop = false;
            button.BringToFront();
            button.Click += delegate { ShowHelp(form); };

            form.Controls.Add(button);
            form.Resize += Form_Resize;
            form.FormClosed += Form_FormClosed;
            form.ControlAdded += Form_ControlAdded;

            AttachedForms.Add(form);
            PositionButton(form);
        }

        private static bool HasExistingHelpEntry(Form form)
        {
            foreach (Control control in form.Controls)
            {
                if (string.Equals(control.Name, ButtonName, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(control.Text, "Hướng dẫn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(control.Text, "Xem hướng dẫn", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void PositionButton(Form form)
        {
            if (form == null || form.IsDisposed)
                return;

            Control button = form.Controls[ButtonName];
            if (button == null)
                return;

            button.Left = Math.Max(0, form.ClientSize.Width - button.Width - 10);
            button.Top = 8;
            button.BringToFront();
        }

        private static void Form_Resize(object sender, EventArgs e)
        {
            PositionButton(sender as Form);
        }

        private static void Form_ControlAdded(object sender, ControlEventArgs e)
        {
            Form form = sender as Form;
            if (form == null || e.Control == null || e.Control.Name == ButtonName)
                return;

            PositionButton(form);
        }

        private static void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form form = sender as Form;
            if (form == null)
                return;

            AttachedForms.Remove(form);
            form.Resize -= Form_Resize;
            form.FormClosed -= Form_FormClosed;
            form.ControlAdded -= Form_ControlAdded;
        }

        private static void ShowHelp(Form owner)
        {
            try
            {
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
    }
}
