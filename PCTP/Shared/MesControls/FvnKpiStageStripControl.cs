using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    /// <summary>Reusable five-stage MES KPI strip. Receives values; it never queries the database.</summary>
    public sealed class FvnKpiStageStripControl : XtraUserControl
    {
        private readonly LabelControl[] _values = new LabelControl[5];
        private static readonly string[] Captions =
        {
            "01  KẾ HOẠCH", "02  CẤP LINH KIỆN", "03  LỆNH SẢN XUẤT",
            "04  SẢN XUẤT THỰC TẾ", "05  HOÀN THÀNH"
        };

        public FvnKpiStageStripControl()
        {
            Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
                Padding = new Padding(0, 4, 0, 4), BackColor = Color.Transparent
            };
            for (int i = 0; i < 5; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                var card = new PanelControl
                {
                    Dock = DockStyle.Fill, Margin = new Padding(4),
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple
                };
                var caption = new LabelControl
                {
                    Dock = DockStyle.Top, Height = 27, Text = Captions[i],

                };
                _values[i] = new LabelControl
                {
                    Dock = DockStyle.Fill, Text = "0",

                };
                card.Controls.Add(_values[i]);
                card.Controls.Add(caption);
                layout.Controls.Add(card, i, 0);
            }
            Controls.Add(layout);
        }

        public void SetValues(int plan, int components, int orders, int production, int done)
        {
            int[] values = { plan, components, orders, production, done };
            for (int i = 0; i < values.Length; i++)
                _values[i].Text = Math.Max(0, values[i]).ToString("N0");
        }
    }
}
