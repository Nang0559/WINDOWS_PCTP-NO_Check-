using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP.Shared.MesControls
{
    /// <summary>
    /// Reusable Site and date-range filter with an explicit refresh request. Site là <see cref="FvnSiteLookupEdit"/>
    /// (dbo.vFVN_Site ∩ phạm vi tài khoản); nạp bằng <see cref="LoadSites"/>.
    /// </summary>
    public sealed class FvnSiteDateRangeFilterControl : XtraUserControl
    {
        private readonly FvnSiteLookupEdit _site = new FvnSiteLookupEdit();
        private readonly DateEdit _from = new DateEdit();
        private readonly DateEdit _to = new DateEdit();
        private readonly SimpleButton _refresh = new SimpleButton();

        public event EventHandler RefreshRequested;

        /// <summary>Phát khi người dùng đổi Site (không phát khi nạp bằng code).</summary>
        public event EventHandler SiteChanged;

        public FvnSiteDateRangeFilterControl()
        {
            Height = 56;
            BackColor = Color.White;
            Padding = new Padding(8, 0, 8, 0);
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.White,
                FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2, 11, 0, 0)
            };
            panel.Controls.Add(Caption("SITE", 48));
            _site.Width = 150;
            _site.SiteChanged += (s, e) => { EventHandler h = SiteChanged; if (h != null) h(this, EventArgs.Empty); };
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

        /// <summary>Mã Site đang chọn (rỗng khi chưa có Site trong phạm vi).</summary>
        public string SiteCode
        {
            get { return _site.SiteCode; }
            set { _site.SiteCode = value; }
        }

        /// <summary>Số Site trong phạm vi tài khoản sau lần nạp gần nhất.</summary>
        public int SiteCount { get { return _site.SiteCount; } }

        /// <summary>Lỗi của lần nạp Site gần nhất (rỗng nếu nạp được).</summary>
        public string SiteLoadError { get { return _site.LastError; } }

        /// <summary>Nạp Site từ dbo.vFVN_Site theo phạm vi của chức năng; trả về số Site.</summary>
        public int LoadSites(string functionCode, string preferred = null)
        {
            return _site.LoadForFunction(functionCode, false, preferred);
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

        public void SetBusy(bool busy)
        {
            _site.Enabled = !busy;
            _from.Enabled = !busy;
            _to.Enabled = !busy;
            _refresh.Enabled = !busy;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(214, 220, 229)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private static LabelControl Caption(string text, int width)
        {
            return new LabelControl
            {
                Text = text, AutoSizeMode = LabelAutoSizeMode.None, Width = width, Height = 30,
                Padding = new Padding(2, 7, 2, 0),

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
