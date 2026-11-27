using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Chọn Site + Chuyền sản xuất cho đợt upload kế hoạch. Site lấy theo phạm vi dữ liệu của tài khoản; Chuyền lấy từ danh mục
    /// chuyền đang hoạt động của Site đó (không ai phải nhớ mã chuyền, và file Excel không còn mang Site / Chuyền).
    /// </summary>
    public sealed class ProductionPlanTargetPanel : XtraUserControl
    {
        private sealed class LineItem
        {
            public string Code;
            public string Text;
            public override string ToString() { return Text; }
        }

        private readonly ComboBoxEdit _site = new ComboBoxEdit();
        private readonly ComboBoxEdit _line = new ComboBoxEdit();
        private readonly LabelControl _hint = new LabelControl();
        private IProductionPlanService _service;
        private bool _loading;

        /// <summary>Đổi Site hoặc Chuyền: dữ liệu đã xem trước với lựa chọn cũ không còn đúng.</summary>
        public event EventHandler SelectionChanged;

        public ProductionPlanTargetPanel()
        {
            Height = 62;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 0), ColumnCount = 4, RowCount = 2 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _site.Properties.TextEditStyle = TextEditStyles.DisableTextEditor; _site.Dock = DockStyle.Fill;
            _line.Properties.TextEditStyle = TextEditStyles.DisableTextEditor; _line.Dock = DockStyle.Fill;
            _site.SelectedIndexChanged += (s, e) => { if (!_loading) { LoadLines(); RaiseChanged(); } };
            _line.SelectedIndexChanged += (s, e) => { if (!_loading) RaiseChanged(); };
            _hint.Dock = DockStyle.Fill; _hint.Appearance.ForeColor = Color.Firebrick;
            table.Controls.Add(new LabelControl { Text = "Site", Dock = DockStyle.Fill }, 0, 0);
            table.Controls.Add(_site, 1, 0);
            table.Controls.Add(new LabelControl { Text = "Chuyền", Dock = DockStyle.Fill }, 2, 0);
            table.Controls.Add(_line, 3, 0);
            table.Controls.Add(_hint, 0, 1); table.SetColumnSpan(_hint, 4);
            Controls.Add(table);
        }

        /// <summary>Site đã chọn (hoặc null).</summary>
        public string SelectedSite { get { return _site.SelectedItem as string; } }

        /// <summary>Mã chuyền đã chọn (hoặc null).</summary>
        public string SelectedLine { get { var i = _line.SelectedItem as LineItem; return i == null ? null : i.Code; } }

        public bool IsComplete { get { return SelectedSite != null && SelectedLine != null; } }

        /// <summary>Nội dung hiển thị của chuyền đã chọn, dùng cho thông báo xác nhận.</summary>
        public string SelectedLineText { get { var i = _line.SelectedItem as LineItem; return i == null ? null : i.Text; } }

        public string Hint { get { return _hint.Text; } }

        public void Initialize(IProductionPlanService service, string preferredSite)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
            _loading = true;
            try
            {
                _site.Properties.Items.Clear();
                _line.Properties.Items.Clear();
                _hint.Text = string.Empty;
                try
                {
                    foreach (string site in service.GetUploadSites()) _site.Properties.Items.Add(site);
                }
                catch (Exception ex) { _hint.Text = ex.Message; return; }

                if (_site.Properties.Items.Count == 0)
                {
                    _hint.Text = "Tài khoản chưa được gán Site cho chức năng upload kế hoạch (MES.PLAN_UPLOAD).";
                    return;
                }
                int index = 0;
                if (!string.IsNullOrWhiteSpace(preferredSite))
                    for (int i = 0; i < _site.Properties.Items.Count; i++)
                        if (string.Equals((string)_site.Properties.Items[i], preferredSite.Trim(), StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                _site.SelectedIndex = index;
            }
            finally { _loading = false; }
            LoadLines();
            RaiseChanged();
        }

        private void LoadLines()
        {
            bool previous = _loading;
            _loading = true;
            try
            {
                _line.Properties.Items.Clear();
                _line.SelectedIndex = -1;
                _hint.Text = string.Empty;
                string site = SelectedSite;
                if (site == null) return;
                DataTable lines;
                try { lines = _service.GetProductionLines(site); }
                catch (Exception ex) { _hint.Text = "Không tải được danh sách chuyền: " + ex.Message; return; }

                foreach (DataRow r in lines.Rows)
                {
                    string code = Convert.ToString(r["LineCode"]).Trim();
                    string name = Convert.ToString(r["LineName"]);
                    string dept = Convert.ToString(r["DepartmentCode"]);
                    string text = code;
                    if (!string.IsNullOrWhiteSpace(name)) text += " — " + name.Trim();
                    if (!string.IsNullOrWhiteSpace(dept)) text += " (Bộ phận " + dept.Trim() + ")";
                    _line.Properties.Items.Add(new LineItem { Code = code, Text = text });
                }
                if (_line.Properties.Items.Count == 0)
                    _hint.Text = "Site " + site + " chưa khai báo chuyền sản xuất (MES.FVN_ProductionLotCodeMap). Liên hệ quản trị để khai báo.";
                else if (_line.Properties.Items.Count == 1)
                    _line.SelectedIndex = 0;
            }
            finally { _loading = previous; }
        }

        private void RaiseChanged()
        {
            var handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    /// <summary>Hộp thoại chọn Site + Chuyền, dùng cho nút "Upload Excel kế hoạch…" của màn hình layout.</summary>
    public static class ProductionPlanTargetDialog
    {
        /// <returns>false khi người dùng hủy.</returns>
        public static bool Pick(IWin32Window owner, IProductionPlanService service, string preferredSite,
            out string siteCode, out string lineCode, out string lineText)
        {
            siteCode = lineCode = lineText = null;
            using (var dialog = new XtraForm())
            {
                dialog.Text = "Upload kế hoạch — chọn Site và Chuyền";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false; dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(560, 150);

                var panel = new ProductionPlanTargetPanel { Dock = DockStyle.Top };
                var note = new LabelControl
                {
                    Text = "Toàn bộ dòng trong file sẽ được tạo cho Site và Chuyền này. File Excel không cần có mã Site / Chuyền.",
                    Dock = DockStyle.Top, Padding = new Padding(12, 8, 12, 0), AutoSizeMode = LabelAutoSizeMode.Vertical
                };
                var ok = new SimpleButton { Text = "Tiếp tục", Width = 100, Height = 30, Enabled = false, DialogResult = DialogResult.OK };
                var cancel = new SimpleButton { Text = "Hủy", Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
                panel.SelectionChanged += (s, e) => ok.Enabled = panel.IsComplete;
                dialog.Controls.Add(note);
                dialog.Controls.Add(panel);
                dialog.Controls.Add(buttons);
                dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                panel.Initialize(service, preferredSite);

                if (dialog.ShowDialog(owner) != DialogResult.OK || !panel.IsComplete) return false;
                siteCode = panel.SelectedSite; lineCode = panel.SelectedLine; lineText = panel.SelectedLineText;
                return true;
            }
        }
    }
}
