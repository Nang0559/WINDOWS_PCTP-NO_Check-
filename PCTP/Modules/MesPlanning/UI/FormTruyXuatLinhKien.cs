using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;
using PCTP.Shared.MesControls;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>Read-only genealogy query. Results are derived from actual LOT consumption/production facts.</summary>
    public sealed class FormTruyXuatLinhKien : XtraForm
    {
        private readonly IProductionTraceService _service;
        private readonly TextEdit _lotNo = new TextEdit();
        private readonly FvnSiteLookupEdit _site = new FvnSiteLookupEdit();
        private const string Function = "REPORT.COMPONENT_TRACEABILITY";
        private readonly ComboBoxEdit _direction = new ComboBoxEdit();
        private readonly GridControl _results = new GridControl();
        private readonly GridView _view;
        private FvnConsoleScreenFrame _frame;

        public FormTruyXuatLinhKien()
            : this(new ProductionTraceService(new ProductionTraceRepository()))
        {
        }

        public FormTruyXuatLinhKien(IProductionTraceService service)
        {
            AuthorizationGuard.Demand("REPORT.COMPONENT_TRACEABILITY", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _view = new GridView(_results);
            Text = "Truy xuất linh kiện / LOT sản xuất";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1050, 650);
            BuildUi();
            Load += (s, e) =>
            {
                // Site chuẩn: dbo.vFVN_Site (FVN_PCTPSite liên kết IFS.FVN_Site) ∩ phạm vi tài khoản của chức năng.
                _site.LoadForFunction(Function);
                if (_site.SiteCount == 0)
                    SetStatus(string.IsNullOrEmpty(_site.LastError)
                        ? "Không tìm thấy Site trong phạm vi được cấp cho tài khoản."
                        : _site.LastError, true);
                else _frame.SetRuntime("Site " + _site.SiteCode, true);
            };
        }

        private void BuildUi()
        {
            _lotNo.Width = 280;
            _lotNo.Properties.NullValuePrompt = "Quét / nhập mã LOT";
            _lotNo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchClick(s, e); } };
            _site.Width = 150;
            _site.SiteChanged += (s, e) => _frame.SetRuntime("Site " + _site.SiteCode, true);
            _direction.Width = 270;
            _direction.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _direction.Properties.Items.AddRange(new object[] { "Ngược: Thành phẩm → linh kiện", "Xuôi: Linh kiện → thành phẩm" });
            _direction.SelectedIndex = 0;
            var search = new SimpleButton();
            FvnConsoleScreen.StyleButton(search, "TRUY XUẤT GENEALOGY", 190, SearchClick);

            var filter = FvnConsoleScreen.CreateFilterCard(
                FvnConsoleScreen.Caption("Site"), _site,
                FvnConsoleScreen.Caption("LOT cần truy xuất"), _lotNo,
                FvnConsoleScreen.Caption("Chiều truy xuất"), _direction, search);

            FvnConsoleScreen.StyleGrid(_results, _view);
            var gridCard = new FvnCardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            gridCard.Controls.Add(_results);

            _frame = FvnConsoleScreen.Compose(this,
                "TRUY XUẤT LINH KIỆN / LOT SẢN XUẤT",
                "Genealogy theo LOT tiêu hao và LOT sản xuất thực tế, không suy diễn từ BOM kế hoạch | Site theo phạm vi tài khoản",
                filter, FvnConsoleScreen.FilterCardHeight, gridCard);
            SetStatus("Tra cứu theo tiêu hao LOT thực tế, không suy diễn từ BOM kế hoạch.", false);
        }

        private void SetStatus(string text, bool warning)
        {
            if (_frame != null) _frame.SetStatus(text, warning);
        }

        private void SearchClick(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_site.SiteCode))
                    throw new InvalidOperationException("Chọn Site để kiểm tra đúng phạm vi dữ liệu được phân quyền.");
                if (_direction.SelectedIndex < 0)
                    throw new InvalidOperationException("Chọn chiều truy xuất.");
                string direction = _direction.SelectedIndex == 0 ? "UPSTREAM" : "DOWNSTREAM";
                DataTable data = _service.Trace(_lotNo.Text, direction, _site.SiteCode);
                _results.DataSource = data;
                _view.BestFitColumns();
                SetStatus(data.Rows.Count == 0
                    ? "Không tìm thấy quan hệ genealogy cho LOT này."
                    : "Tìm thấy " + data.Rows.Count.ToString("N0") +
                      " quan hệ. Dữ liệu dựa trên LOT tiêu hao và LOT sản xuất thực tế.", data.Rows.Count == 0);
            }
            catch (Exception ex)
            {
                _results.DataSource = null;
                SetStatus(ex.Message, true);
                XtraMessageBox.Show(this, ex.Message, "Truy xuất linh kiện",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
