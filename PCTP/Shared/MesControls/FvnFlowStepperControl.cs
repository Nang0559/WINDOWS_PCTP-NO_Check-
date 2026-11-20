using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    public enum FvnFlowStepState { Pending, Active, Complete, Blocked, Hold, Related }

    /// <summary>
    /// Five-stage flow strip. Each stage is a colour-coded card (state), a caption, a state label and an
    /// optional one-line fact (e.g. "Đã cấp 120/200"). The caller supplies already-authorized facts;
    /// this control is display-only and exposes no command.
    /// </summary>
    public sealed class FvnFlowStepperControl : XtraUserControl
    {
        private const int StageCount = 5;
        private readonly PanelControl[] _cards = new PanelControl[StageCount];
        private readonly LabelControl[] _titles = new LabelControl[StageCount];
        private readonly LabelControl[] _states = new LabelControl[StageCount];
        private readonly LabelControl[] _details = new LabelControl[StageCount];
        private static readonly string[] Names =
        {
            "01 · KẾ HOẠCH", "02 · CẤP LINH KIỆN", "03 · LỆNH / LOT", "04 · CÔNG ĐOẠN", "05 · HOÀN THÀNH"
        };

        public FvnFlowStepperControl()
        {
            Height = 112;
            Dock = DockStyle.Top;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = StageCount, RowCount = 1,
                Padding = new Padding(2, 4, 2, 4)
            };
            for (int i = 0; i < StageCount; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / StageCount));
                var card = new PanelControl { Dock = DockStyle.Fill, Margin = new Padding(3), Padding = new Padding(8, 5, 8, 5) };
                card.Appearance.Options.UseBackColor = true;
                card.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
                _titles[i] = new LabelControl { Dock = DockStyle.Top, Height = 22, Text = Names[i] };
                _titles[i].Appearance.Font = new Font(_titles[i].Appearance.Font, FontStyle.Bold);
                _states[i] = new LabelControl { Dock = DockStyle.Top, Height = 24 };
                _states[i].Appearance.Font = new Font(_states[i].Appearance.Font.FontFamily, 10.5f, FontStyle.Bold);
                _details[i] = new LabelControl { Dock = DockStyle.Fill, Text = string.Empty };
                _details[i].Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
                _details[i].AutoSizeMode = LabelAutoSizeMode.None;
                // Dock order: the last control added is docked first.
                card.Controls.Add(_details[i]);
                card.Controls.Add(_states[i]);
                card.Controls.Add(_titles[i]);
                _cards[i] = card;
                layout.Controls.Add(card, i, 0);
            }
            Controls.Add(layout);
            SetStates(FvnFlowStepState.Pending, FvnFlowStepState.Pending,
                FvnFlowStepState.Pending, FvnFlowStepState.Pending, FvnFlowStepState.Pending);
        }

        public void SetStates(FvnFlowStepState plan, FvnFlowStepState components,
            FvnFlowStepState order, FvnFlowStepState production, FvnFlowStepState done)
        {
            var values = new[] { plan, components, order, production, done };
            for (int i = 0; i < values.Length; i++) Apply(i, values[i]);
        }

        /// <summary>One short fact per stage (null/empty clears it). Display text only.</summary>
        public void SetDetails(string plan, string components, string order, string production, string done)
        {
            var values = new[] { plan, components, order, production, done };
            for (int i = 0; i < values.Length; i++) _details[i].Text = values[i] ?? string.Empty;
        }

        private void Apply(int index, FvnFlowStepState state)
        {
            Color back, fore;
            Palette(state, out back, out fore);
            _cards[index].Appearance.BackColor = back;
            _states[index].Text = Label(state);
            _states[index].Appearance.ForeColor = fore;
            _titles[index].Appearance.ForeColor = fore;
            _details[index].Appearance.ForeColor = Color.FromArgb(55, 71, 79);
        }

        // Light card + dark foreground keeps contrast on every DevExpress skin.
        private static void Palette(FvnFlowStepState state, out Color back, out Color fore)
        {
            switch (state)
            {
                case FvnFlowStepState.Complete: back = Color.FromArgb(232, 245, 233); fore = Color.FromArgb(27, 94, 32); break;
                case FvnFlowStepState.Active:   back = Color.FromArgb(227, 242, 253); fore = Color.FromArgb(13, 71, 161); break;
                case FvnFlowStepState.Blocked:  back = Color.FromArgb(255, 243, 224); fore = Color.FromArgb(191, 87, 0); break;
                case FvnFlowStepState.Hold:     back = Color.FromArgb(253, 236, 234); fore = Color.FromArgb(183, 28, 28); break;
                case FvnFlowStepState.Related:  back = Color.FromArgb(236, 239, 241); fore = Color.FromArgb(69, 90, 100); break;
                default:                        back = Color.FromArgb(250, 250, 250); fore = Color.FromArgb(117, 117, 117); break;
            }
        }

        private static string Label(FvnFlowStepState state)
        {
            switch (state)
            {
                case FvnFlowStepState.Complete: return "✓ Hoàn thành";
                case FvnFlowStepState.Active: return "● Đang xử lý";
                case FvnFlowStepState.Blocked: return "⚠ Đang chờ";
                case FvnFlowStepState.Hold: return "Ⅱ HOLD";
                case FvnFlowStepState.Related: return "↗ Bộ phận liên quan (chỉ xem)";
                default: return "○ Chưa đến";
            }
        }
    }
}
