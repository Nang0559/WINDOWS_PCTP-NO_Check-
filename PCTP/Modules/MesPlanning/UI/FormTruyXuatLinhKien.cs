using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Infrastructure.Security;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>Read-only genealogy query. Results are derived from actual LOT consumption/production facts.</summary>
    public sealed class FormTruyXuatLinhKien : XtraForm
    {
        private readonly IProductionTraceService _service;
        private readonly TextEdit _lotNo = new TextEdit();
        private readonly TextEdit _siteCode = new TextEdit();
        private readonly ComboBoxEdit _direction = new ComboBoxEdit();
        private readonly GridControl _results = new GridControl();
        private readonly GridView _view;
        private readonly LabelControl _status = new LabelControl();

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
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2) };
            filters.Controls.Add(new LabelControl { Text = "LOT cần truy xuất", AutoSize = true, Padding = new Padding(0, 9, 0, 0) });
            _lotNo.Width = 270;
            _lotNo.Properties.NullValuePrompt = "Quét / nhập mã LOT";
            filters.Controls.Add(_lotNo);
            filters.Controls.Add(new LabelControl { Text = "Site (bắt buộc)", AutoSize = true, Padding = new Padding(10, 9, 0, 0) });
            _siteCode.Width = 110;
            _siteCode.Properties.NullValuePrompt = "SiteCode";
            filters.Controls.Add(_siteCode);
            filters.Controls.Add(new LabelControl { Text = "Chiều truy xuất", AutoSize = true, Padding = new Padding(10, 9, 0, 0) });
            _direction.Width = 260;
            _direction.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _direction.Properties.Items.AddRange(new object[] { "Ngược: Thành phẩm → linh kiện", "Xuôi: Linh kiện → thành phẩm" });
            _direction.SelectedIndex = 0;
            filters.Controls.Add(_direction);
            var search = new SimpleButton { Text = "TRUY XUẤT GENEALOGY", Width = 190, Height = 32 };
            search.Click += SearchClick;
            filters.Controls.Add(search);

            _results.Dock = DockStyle.Fill;
            _results.MainView = _view;
            _results.ViewCollection.Add(_view);
            _view.OptionsBehavior.Editable = false;
            _view.OptionsBehavior.ReadOnly = true;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowAutoFilterRow = true;
            _view.OptionsView.ColumnAutoWidth = false;
            _view.OptionsSelection.MultiSelect = false;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.FocusRectStyle = DrawFocusRectStyle.RowFocus;

            _status.Dock = DockStyle.Fill;
            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _status.Text = "Tra cứu theo tiêu hao LOT thực tế, không suy diễn từ BOM kế hoạch.";

            root.Controls.Add(filters, 0, 0);
            root.Controls.Add(_results, 0, 1);
            root.Controls.Add(_status, 0, 2);
            Controls.Add(root);
        }

        private void SearchClick(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_siteCode.Text))
                    throw new InvalidOperationException("Nhập SiteCode để kiểm tra đúng phạm vi dữ liệu được phân quyền.");
                if (_direction.SelectedIndex < 0)
                    throw new InvalidOperationException("Chọn chiều truy xuất.");
                string direction = _direction.SelectedIndex == 0 ? "UPSTREAM" : "DOWNSTREAM";
                DataTable data = _service.Trace(_lotNo.Text, direction, _siteCode.Text);
                _results.DataSource = data;
                _view.BestFitColumns();
                _status.Text = data.Rows.Count == 0
                    ? "Không tìm thấy quan hệ genealogy cho LOT này."
                    : "Tìm thấy " + data.Rows.Count.ToString("N0") +
                      " quan hệ. Dữ liệu dựa trên LOT tiêu hao và LOT sản xuất thực tế.";
            }
            catch (Exception ex)
            {
                _results.DataSource = null;
                _status.Text = ex.Message;
                XtraMessageBox.Show(this, ex.Message, "Truy xuất linh kiện",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
