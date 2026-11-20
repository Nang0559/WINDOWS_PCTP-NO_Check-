using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    /// <summary>Reusable pair of progress indicators for material issue and routing completion.</summary>
    public sealed class FvnProgressSummaryControl : XtraUserControl
    {
        private readonly ProgressBarControl _components = new ProgressBarControl();
        private readonly ProgressBarControl _operations = new ProgressBarControl();

        public FvnProgressSummaryControl()
        {
            Height = 82;
            Dock = DockStyle.Bottom;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
                Padding = new Padding(2, 3, 2, 2)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            layout.Controls.Add(MakeCaption("CẤP LINH KIỆN"), 0, 0);
            layout.Controls.Add(Configure(_components), 0, 1);
            layout.Controls.Add(MakeCaption("CÔNG ĐOẠN"), 0, 2);
            layout.Controls.Add(Configure(_operations), 0, 3);
            Controls.Add(layout);
        }

        public void SetProgress(int componentPercent, int operationPercent)
        {
            _components.Position = Clamp(componentPercent);
            _operations.Position = Clamp(operationPercent);
        }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(100, value)); }
        private static LabelControl MakeCaption(string text)
        {
            return new LabelControl
            {
                Text = text, Dock = DockStyle.Fill,

            };
        }
        private static ProgressBarControl Configure(ProgressBarControl control)
        {
            control.Dock = DockStyle.Left;
            control.Width = 260;
            control.Properties.Minimum = 0;
            control.Properties.Maximum = 100;
            control.Properties.ShowTitle = true;
            control.Position = 0;
            return control;
        }
    }
}
