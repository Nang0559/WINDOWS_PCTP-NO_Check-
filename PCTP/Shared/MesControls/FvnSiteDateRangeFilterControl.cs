using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    /// <summary>Reusable Site and date-range filter with an explicit refresh request.</summary>
    public sealed class FvnSiteDateRangeFilterControl : XtraUserControl
    {
        private readonly TextEdit _site = new TextEdit();
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly SimpleButton _refresh = new SimpleButton();

        public event EventHandler RefreshRequested;

        public FvnSiteDateRangeFilterControl()
        {
            Height = 48;
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2, 8, 0, 0)
            };
            panel.Controls.Add(Caption("SITE", 48));
            _site.Width = 110;
            _site.Properties.NullValuePrompt = "SiteCode";
            panel.Controls.Add(_site);
            panel.Controls.Add(Caption("TỪ NGÀY", 66));
            ConfigureDate(_from, DateTime.Today.AddDays(-7));
            panel.Controls.Add(_from);
            panel.Controls.Add(Caption("ĐẾN NGÀY", 66));
            ConfigureDate(_to, DateTime.Today.AddDays(30));
            panel.Controls.Add(_to);
            _refresh.Text = "LÀM MỚI";
            _refresh.Width = 112;
            _refresh.Height = 32;
            _refresh.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            panel.Controls.Add(_refresh);
            Controls.Add(panel);
        }

        public string SiteCode
        {
            get { return (Convert.ToString(_site.Text) ?? string.Empty).Trim(); }
            set { _site.Text = value ?? string.Empty; }
        }

        public DateTime? FromDate
        {
            get { return _from.EditValue == null || _from.EditValue == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(_from.EditValue).Date; }
            set { _from.EditValue = value.HasValue ? (object)value.Value.Date : null; }
        }

        public DateTime? ToDate
        {
            get { return _to.EditValue == null || _to.EditValue == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(_to.EditValue).Date; }
            set { _to.EditValue = value.HasValue ? (object)value.Value.Date : null; }
        }

        public void SetBusy(bool busy) { _refresh.Enabled = !busy; }

        private static LabelControl Caption(string text, int width)
        {
            return new LabelControl
            {
                Text = text, AutoSizeMode = LabelAutoSizeMode.None, Width = width, Height = 30,
                Padding = new Padding(2, 7, 2, 0),
                Appearance = { Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(82,96,112) }
            };
        }

        private static void ConfigureDate(DateEdit edit, DateTime value)
        {
            edit.Width = 116;
            edit.Properties.Mask.EditMask = "dd/MM/yyyy";
            edit.Properties.Mask.UseMaskAsDisplayFormat = true;
            edit.EditValue = value;
        }
    }
}
