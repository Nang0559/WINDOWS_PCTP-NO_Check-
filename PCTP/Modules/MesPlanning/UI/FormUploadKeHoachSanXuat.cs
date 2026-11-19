using PCTP.Infrastructure.Security;
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
        private readonly TextEdit _file = new TextEdit();
        private readonly ComboBoxEdit _sheet = new ComboBoxEdit();
        private readonly GridControl _grid = new GridControl();
        private readonly GridView _gridView;
        private readonly LabelControl _status = new LabelControl();
        private readonly SimpleButton _choose = new SimpleButton();
        private readonly SimpleButton _preview = new SimpleButton();
        private readonly SimpleButton _upload = new SimpleButton();
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
            Size = new Size(1180, 720);
            MinimumSize = new Size(900, 580);
            BuildUi();
        }

        private void BuildUi()
        {
            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(12), ColumnCount = 4, RowCount = 2 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            _file.Properties.ReadOnly = true; _file.Dock = DockStyle.Fill;
            _choose.Text = "Chọn Excel"; _choose.Dock = DockStyle.Fill; _choose.Click += ChooseClick;
            _sheet.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor; _sheet.Dock = DockStyle.Fill;
            _preview.Text = "Xem trước"; _preview.Dock = DockStyle.Fill; _preview.Click += PreviewClick;
            _upload.Text = "UPLOAD KẾ HOẠCH"; _upload.Dock = DockStyle.Fill; _upload.Click += UploadClick; _upload.Enabled = false;
            _managePlans.Text = "QUẢN LÝ WORKFLOW"; _managePlans.Dock = DockStyle.Fill;
            _managePlans.Click += (s, e) => { using (var form = new FormProductionPlanWorkflow()) form.ShowDialog(this); };
            top.Controls.Add(_file,0,0); top.Controls.Add(_choose,1,0);
            top.Controls.Add(_sheet,2,0); top.Controls.Add(_preview,3,0);
            top.Controls.Add(_managePlans,2,1); top.Controls.Add(_upload,3,1);
            _status.Text = "Cột Excel yêu cầu: SiteCode, PlanNo, PlanDate, LineNo, ShiftCode, ProductionLineCode, ProductPartNo, PlannedQty, UomCode; PlannedStart/PlannedEnd tùy chọn.";
            _status.Dock = DockStyle.Fill; _status.Appearance.ForeColor = Color.DarkSlateGray;
            top.Controls.Add(_status,0,1); top.SetColumnSpan(_status,2);
            _grid.Dock = DockStyle.Fill;
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.ColumnAutoWidth = false;
            _gridView.OptionsSelection.MultiSelect = false;
            _gridView.OptionsView.ShowIndicator = true;
            Controls.Add(_grid); Controls.Add(top);
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
                    _status.Text = "Đã đọc danh sách Sheet. Chọn Sheet và bấm Xem trước.";
                }
                catch (Exception ex) { _status.Text = "Không mở được Excel: " + ex.Message; }
            }
        }

        private void PreviewClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_filePath) || string.IsNullOrWhiteSpace(_sheet.Text)) return;
            try
            {
                _rows = ReadRows(_filePath, _sheet.Text);
                _grid.DataSource = _rows.Select(x => new {
                    x.SiteCode,x.PlanNo,PlanDate=x.PlanDate.ToString("yyyy-MM-dd"),x.LineNo,x.ShiftCode,
                    x.ProductionLineCode,x.ProductPartNo,x.PlannedQty,x.UomCode,
                    PlannedStart=x.PlannedStart?.ToString("yyyy-MM-dd HH:mm"),
                    PlannedEnd=x.PlannedEnd?.ToString("yyyy-MM-dd HH:mm")
                }).ToList();
                _gridView.BestFitColumns();
                _upload.Enabled = _rows.Count > 0;
                _status.Text = "Đọc được " + _rows.Count + " dòng. Upload sẽ chạy trong một transaction; nếu một dòng lỗi, toàn bộ batch rollback.";
            }
            catch (Exception ex) { _rows.Clear(); _upload.Enabled = false; _status.Text = "Lỗi dữ liệu Excel: " + ex.Message; }
        }

        private static List<ProductionPlanImportRow> ReadRows(string path, string sheetName)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                var ws = package.Workbook.Worksheets[sheetName];
                if (ws == null || ws.Dimension == null) throw new InvalidOperationException("Sheet không có dữ liệu.");
                var headers = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
                for (int c=1;c<=ws.Dimension.End.Column;c++)
                {
                    var h=(ws.Cells[1,c].Text ?? "").Trim();
                    if (h.Length>0) headers[h]=c;
                }
                string[] required={"SiteCode","PlanNo","PlanDate","LineNo","ShiftCode","ProductionLineCode","ProductPartNo","PlannedQty","UomCode"};
                foreach(var h in required) if(!headers.ContainsKey(h)) throw new InvalidOperationException("Thiếu cột bắt buộc: "+h);
                Func<int,string,string> cell=(row,key)=>headers.ContainsKey(key)?(ws.Cells[row,headers[key]].Text ?? "").Trim():null;
                var result=new List<ProductionPlanImportRow>();
                for(int r=2;r<=ws.Dimension.End.Row;r++)
                {
                    if(Enumerable.Range(1,ws.Dimension.End.Column).All(c=>string.IsNullOrWhiteSpace(ws.Cells[r,c].Text))) continue;
                    DateTime planDate;
                    if(!DateTime.TryParse(cell(r,"PlanDate"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out planDate)
                       && !DateTime.TryParse(cell(r,"PlanDate"),out planDate))
                        throw new InvalidOperationException("PlanDate không hợp lệ tại dòng "+r+".");
                    int lineNo; if(!int.TryParse(cell(r,"LineNo"),out lineNo)) throw new InvalidOperationException("LineNo không hợp lệ tại dòng "+r+".");
                    decimal qty; if(!decimal.TryParse(cell(r,"PlannedQty"),NumberStyles.Number,CultureInfo.InvariantCulture,out qty)
                       && !decimal.TryParse(cell(r,"PlannedQty"),out qty)) throw new InvalidOperationException("PlannedQty không hợp lệ tại dòng "+r+".");
                    DateTime start,end;
                    var startText=cell(r,"PlannedStart"); var endText=cell(r,"PlannedEnd");
                    DateTime? startValue=DateTime.TryParse(startText,out start)?(DateTime?)start:null;
                    DateTime? endValue=DateTime.TryParse(endText,out end)?(DateTime?)end:null;
                    result.Add(new ProductionPlanImportRow {
                        SiteCode=cell(r,"SiteCode"),PlanNo=cell(r,"PlanNo"),PlanDate=planDate,LineNo=lineNo,
                        ShiftCode=cell(r,"ShiftCode"),ProductionLineCode=cell(r,"ProductionLineCode"),
                        ProductPartNo=cell(r,"ProductPartNo"),PlannedQty=qty,UomCode=cell(r,"UomCode"),
                        PlannedStart=startValue,PlannedEnd=endValue
                    });
                }
                return result;
            }
        }

        private void UploadClick(object sender, EventArgs e)
        {
            _upload.Enabled = false;
            try
            {
                _service.ImportRows(_rows,_filePath == null ? null : Path.GetFileName(_filePath),Environment.UserName);
                _status.Text = "Đã upload " + _rows.Count + " dòng kế hoạch theo ca vào MES.";
                MessageBox.Show(this,_status.Text,"Upload kế hoạch",MessageBoxButtons.OK,MessageBoxIcon.Information);
                _rows.Clear(); _upload.Enabled=false;
            }
            catch(Exception ex)
            {
                _status.Text = "Upload thất bại: " + ex.Message;
                MessageBox.Show(this,_status.Text,"Lỗi upload",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                _upload.Enabled=_rows.Count>0;
            }
        }
    }
}