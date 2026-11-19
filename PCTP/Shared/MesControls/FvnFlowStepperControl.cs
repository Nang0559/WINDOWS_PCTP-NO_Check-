using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    public enum FvnFlowStepState { Pending, Active, Complete, Hold, Related }

    /// <summary>Reusable five-stage flow strip. The caller supplies authorized facts; this control is display-only.</summary>
    public sealed class FvnFlowStepperControl : XtraUserControl
    {
        private readonly LabelControl[] _titles = new LabelControl[5];
        private readonly LabelControl[] _states = new LabelControl[5];
        private static readonly string[] Names =
        {
            "01 · KẾ HOẠCH", "02 · CẤP LINH KIỆN", "03 · LỆNH / LOT",
            "04 · CÔNG ĐOẠN", "05 · HOÀN THÀNH"
        };

        public FvnFlowStepperControl()
        {
            Height = 86;
            Dock = DockStyle.Top;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
                Padding = new Padding(2, 4, 2, 4)
            };
            for (int i = 0; i < 5; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
                var tile = new PanelControl { Dock = DockStyle.Fill, Margin = new Padding(3), Padding = new Padding(5) };
                _titles[i] = new LabelControl { Dock = DockStyle.Top, Height = 28, Text = Names[i] };
                _titles[i].Appearance.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _states[i] = new LabelControl { Dock = DockStyle.Fill, Text = "Chưa đến" };
                _states[i].Appearance.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                tile.Controls.Add(_states[i]);
                tile.Controls.Add(_titles[i]);
                layout.Controls.Add(tile, i, 0);
            }
            Controls.Add(layout);
            SetStates(FvnFlowStepState.Pending, FvnFlowStepState.Pending,
                FvnFlowStepState.Pending, FvnFlowStepState.Pending, FvnFlowStepState.Pending);
        }

        public void SetStates(FvnFlowStepState plan, FvnFlowStepState components,
            FvnFlowStepState order, FvnFlowStepState production, FvnFlowStepState done)
        {
            var values = new[] { plan, components, order, production, done };
            for (int i = 0; i < values.Length; i++)
            {
                _states[i].Text = Label(values[i]);
                _states[i].Appearance.ForeColor = ColorFor(values[i]);
                _titles[i].Appearance.ForeColor = ColorFor(values[i]);
            }
        }

        private static string Label(FvnFlowStepState state)
        {
            switch (state)
            {
                case FvnFlowStepState.Complete: return "✓ Hoàn thành";
                case FvnFlowStepState.Active: return "● Đang xử lý";
                case FvnFlowStepState.Hold: return "Ⅱ HOLD";
                case FvnFlowStepState.Related: return "↗ Bộ phận liên quan";
                default: return "○ Chưa đến";
            }
        }

        private static Color ColorFor(FvnFlowStepState state)
        {
            switch (state)
            {
                case FvnFlowStepState.Complete: return Color.ForestGreen;
                case FvnFlowStepState.Active: return Color.RoyalBlue;
                case FvnFlowStepState.Hold: return Color.DarkOrange;
                case FvnFlowStepState.Related: return Color.DimGray;
                default: return SystemColors.GrayText;
            }
        }
    }
}