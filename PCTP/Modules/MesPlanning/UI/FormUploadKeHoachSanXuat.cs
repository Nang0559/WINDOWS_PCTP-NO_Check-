using PCTP.Infrastructure.Security;
using PCTP.Shared.MesControls;
using OfficeOpenXml;
using PCTP.Modules.MesPlanning;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Modules.MesPlanning.UI
{
    public sealed class FormUploadKeHoachSanXuat : XtraForm
    {
        private readonly IProductionPlanService _service;
        private readonly ProductionPlanTargetPanel _target = new ProductionPlanTargetPanel();
        private readonly TextEdit _file = new TextEdit();
        private readonly ComboBoxEdit _sheet = new ComboBoxEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _gridView;
        private FvnConsoleScreenFrame _frame;
        private readonly LabelControl _columnHint = new LabelControl();
        private readonly SimpleButton _choose = new SimpleButton();
        private readonly SimpleButton _preview = new SimpleButton();
        private readonly SimpleButton _upload = new SimpleButton();
        private readonly SimpleButton _template = new SimpleButton();
        private readonly SimpleButton _managePlans = new SimpleButton();
        private string _filePath;
        private List<ProductionPlanImportRow> _rows = new List<ProductionPlanImportRow>();

        public FormUploadKeHoachSanXuat() : this(new ProductionPlanService(new ProductionPlanRepository())) { }
    
        public FormUploadKeHoachSanXuat(IProductionPlanService service)
        {
            AuthorizationGuard.Demand("MES.PLAN_UPLOAD", "VIEW");
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _gridView = new GridView(_grid);
            Text = "Upload kế hoạch sản xuất theo ca";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            Size = new Size(1180, 720);
            MinimumSize = new Size(900, 580);
            BuildUi();
        }

        private void BuildUi()
        {
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 4), ColumnCount = 4, RowCount = 2, BackColor = Color.White };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _file.Properties.ReadOnly = true; _file.Properties.NullValuePrompt = "Chưa chọn file Excel kế hoạch"; _file.Dock = DockStyle.Fill;
            _choose.Text = "CHỌN EXCEL"; _choose.Dock = DockStyle.Fill; _choose.Click += ChooseClick;
            _sheet.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor; _sheet.Dock = DockStyle.Fill;
            _preview.Text = "XEM TRƯỚC"; _preview.Dock = DockStyle.Fill; _preview.Click += PreviewClick;
            _upload.Text = "UPLOAD KẾ HOẠCH"; _upload.Dock = DockStyle.Fill; _upload.Click += UploadClick; _upload.Enabled = false;
            _template.Text = "TẢI TEMPLATE"; _template.Dock = DockStyle.Fill; _template.Click += TemplateClick;
            _managePlans.Text = "QUẢN LÝ WORKFLOW"; _managePlans.Dock = DockStyle.Fill;
            _managePlans.Click += (s, e) => { using (var form = new FormProductionPlanWorkflow()) form.ShowDialog(this); };
            _columnHint.Dock = DockStyle.Fill;
            _columnHint.AutoSizeMode = LabelAutoSizeMode.None;
            _columnHint.Appearance.ForeColor = Color.DarkSlateGray;
            _columnHint.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _columnHint.Text = "Cột Excel yêu cầu: PlanNo, PlanDate, LineNo, ShiftCode, ProductPartNo, PlannedQty, UomCode; PlannedStart/PlannedEnd tùy chọn.";
            top.Controls.Add(_file, 0, 0); top.Controls.Add(_choose, 1, 0);
            top.Controls.Add(_sheet, 2, 0); top.Controls.Add(_preview, 3, 0);
            top.Controls.Add(_columnHint, 0, 1); top.Controls.Add(_template, 1, 1);
            top.Controls.Add(_managePlans, 2, 1); top.Controls.Add(_upload, 3, 1);
            var fileCard = new FvnCardPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(1), Margin = new Padding(0) };
            fileCard.Controls.Add(top);

            FvnConsoleScreen.StyleGrid(_grid, _gridView);
            var gridCard = new FvnCardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            gridCard.Controls.Add(_grid);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = FvnConsoleTheme.Background };
            body.Controls.Add(gridCard);
            body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = FvnConsoleTheme.Background });
            body.Controls.Add(fileCard);

            _target.SelectionChanged += TargetChanged;
            _frame = FvnConsoleScreen.Compose(this,
                "UPLOAD KẾ HOẠCH SẢN XUẤT THEO CA",
                "Chọn Site và Chuyền → chọn file Excel → xem trước → upload (một transaction, lỗi một dòng thì rollback toàn batch)",
                _target, 86, body);
            _target.Initialize(_service, null);
            SetStatus(!string.IsNullOrEmpty(_target.Hint) ? _target.Hint : "Chọn Site và Chuyền ở trên, sau đó chọn file Excel kế hoạch.",
                !string.IsNullOrEmpty(_target.Hint));
        }

        private void SetStatus(string text, bool warning)
        {
            if (_frame != null) _frame.SetStatus(text, warning);
        }

        /// <summary>Đổi Site / Chuyền thì dữ liệu đã xem trước (gắn với lựa chọn cũ) phải xem trước lại.</summary>
        private void TargetChanged(object sender, EventArgs e)
        {
            if (_rows.Count == 0) return;
            _rows.Clear(); _grid.DataSource = null; _upload.Enabled = false;
            SetStatus("Đã đổi Site / Chuyền. Bấm Xem trước lại trước khi upload.", true);
        }

        private void ChooseClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", Title = "Chọn kế hoạch sản xuất" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _filePath = dlg.FileName; _file.Text = _filePath; _rows.Clear(); _upload.Enabled = false;
                try
                {
                    using (var package = new ExcelPackage(new FileInfo(_filePath)))
                    {
                        _sheet.Properties.Items.Clear();
                        foreach (var ws in package.Workbook.Worksheets) _sheet.Properties.Items.Add(ws.Name);
                        if (_sheet.Properties.Items.Count > 0) _sheet.SelectedIndex = 0;
                    }
                    SetStatus("Đã đọc danh sách Sheet. Chọn Sheet và bấm Xem trước.", false);
                }
                catch (Exception ex) { SetStatus("Không mở được Excel: " + ex.Message, true); }
            }
        }

        private void TemplateClick(object sender, EventArgs e)
        {
            try
            {
                string status = PCTP.Shared.Helpers.ExcelTemplateFlow.Run(this, ProductionPlanTemplateDefinition.Create());
                if (status != null) SetStatus(status, false);
            }
            catch (Exception ex)
            {
                SetStatus("Không tạo được template: " + ex.Message, true);
                MessageBox.Show(this, ex.Message, "Tải template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PreviewClick(object sender, EventArgs e)
        {
            if (!_target.IsComplete) { SetStatus("Chọn Site và Chuyền sản xuất trước khi xem trước.", true); return; }
            if (string.IsNullOrWhiteSpace(_filePath) || string.IsNullOrWhiteSpace(_sheet.Text)) return;
            try
            {
                _rows = ReadRows(_filePath, _sheet.Text, _target.SelectedSite, _target.SelectedLine);
                _grid.DataSource = _rows.Select(x => new {
                    x.SiteCode,x.PlanNo,PlanDate=x.PlanDate.ToString("yyyy-MM-dd"),x.LineNo,x.ShiftCode,
                    x.ProductionLineCode,x.ProductPartNo,x.PlannedQty,x.UomCode,
                    PlannedStart=x.PlannedStart?.ToString("yyyy-MM-dd HH:mm"),
                    PlannedEnd=x.PlannedEnd?.ToString("yyyy-MM-dd HH:mm")
                }).ToList();
                _gridView.BestFitColumns();
                _upload.Enabled = _rows.Count > 0;
                var legacy = ProductionPlanExcelReader.LegacyColumnsPresent(_filePath, _sheet.Text);
                SetStatus("Đọc được " + _rows.Count + " dòng cho Site " + _target.SelectedSite + ", chuyền " + _target.SelectedLine +
                    ". Upload chạy trong một transaction; nếu một dòng lỗi, toàn bộ batch rollback." +
                    (legacy.Count > 0 ? " Cột " + string.Join(", ", legacy) + " trong file bị bỏ qua." : string.Empty), false);
            }
            catch (Exception ex) { _rows.Clear(); _upload.Enabled = false; SetStatus("Lỗi dữ liệu Excel: " + ex.Message, true); }
        }

        private static List<ProductionPlanImportRow> ReadRows(string path, string sheetName, string siteCode, string lineCode)
        {
            return ProductionPlanExcelReader.Read(path, sheetName, siteCode, lineCode);
        }

        private void UploadClick(object sender, EventArgs e)
        {
            _upload.Enabled = false;
            try
            {
                _service.ImportRows(_rows,_filePath == null ? null : Path.GetFileName(_filePath),Environment.UserName);
                string done = "Đã upload " + _rows.Count + " dòng kế hoạch theo ca vào MES.";
                SetStatus("✔  " + done, false);
                MessageBox.Show(this,done,"Upload kế hoạch",MessageBoxButtons.OK,MessageBoxIcon.Information);
                _rows.Clear(); _upload.Enabled=false;
            }
            catch(Exception ex)
            {
                string failed = "Upload thất bại: " + ex.Message;
                SetStatus(failed, true);
                MessageBox.Show(this,failed,"Lỗi upload",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                _upload.Enabled=_rows.Count>0;
            }
        }
    }
}