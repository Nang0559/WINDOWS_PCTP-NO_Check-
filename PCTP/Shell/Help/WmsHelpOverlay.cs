using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Shell.Help
{
    /// <summary>
    /// Adds a lightweight floating "Xem hướng dẫn" action to operational forms.
    /// The control is attached once per Form and delegates all topic resolution to WmsHelpContext.
    /// </summary>
    internal static class WmsHelpOverlay
    {
        private const string Marker = "__PCTP_WMS_HELP_OVERLAY";
        private static readonly HashSet<Form> AttachedForms = new HashSet<Form>();
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
            Form[] forms = Application.OpenForms.Count == 0
                ? new Form[0]
                : GetFormsSnapshot();

            for (int i = 0; i < forms.Length; i++)
                Attach(forms[i]);
        }

        internal static void Attach(Form form)
        {
            if (form == null || form.IsDisposed || form.Disposing)
                return;

            if (form.GetType().Name == "FormWmsHelp")
                return;

            if (AttachedForms.Contains(form))
                return;

            AttachedForms.Add(form);
            form.FormClosed += Form_FormClosed;

            Panel host = new Panel();
            host.Name = Marker;
            host.Size = new Size(150, 36);
            host.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            host.BackColor = Color.Transparent;
            host.Padding = new Padding(0);

            Button button = new Button();
            button.Text = "Xem hướng dẫn";
            button.AutoSize = false;
            button.Size = new Size(146, 30);
            button.Location = new Point(2, 2);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.FlatStyle = FlatStyle.Standard;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.Click += delegate { ShowHelp(form); };

            host.Controls.Add(button);
            form.Controls.Add(host);
            Position(form, host);
            form.Resize += delegate { Position(form, host); };
            form.ControlAdded += delegate { Position(form, host); };
            host.BringToFront();
        }

        private static Form[] GetFormsSnapshot()
        {
            Form[] result = new Form[Application.OpenForms.Count];
            Application.OpenForms.CopyTo(result, 0);
            return result;
        }

        private static void Position(Form form, Control host)
        {
            if (form == null || host == null || form.IsDisposed)
                return;

            host.Location = new Point(
                Math.Max(0, form.ClientSize.Width - host.Width - 12),
                10);
            host.BringToFront();
        }

        private static void ShowHelp(Form form)
        {
            try
            {
                WmsHelpService service = new WmsHelpService();
                service.ShowCurrent(form);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    form,
                    "Không thể mở hướng dẫn cho màn hình hiện tại.\n\n" + ex.Message,
                    "WMS - Hướng dẫn",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form form = sender as Form;
            if (form == null)
                return;

            AttachedForms.Remove(form);
        }
    }
}
