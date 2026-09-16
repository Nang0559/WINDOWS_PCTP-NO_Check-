using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Infrastructure.Queries;
using PCTP.Shell.UI;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    [DesignerCategory("Code")]
    public sealed class FormBaoCaoQualityHistory : WmsTitleBarForm
    {
        private readonly IQualityHistoryQuery _query;
        private DateEdit _from;
        private DateEdit _to;
        private TextEdit _itemCode;
        private ComboBoxEdit _result;
        private GridControl _masterGrid;
        private GridView _masterView;
        private GridControl _detailGrid;
        private GridView _detailView;
        private LabelControl _summary;

        public FormBaoCaoQualityHistory(IQualityHistoryQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            _query = query;
            BuildUi();
            _from.DateTime = DateTime.Today.AddDays(-7);
            _to.DateTime = DateTime.Today;
            _ = LoadAsync();   // ✅ fire-and-forget tường minh — constructor không await được
        }

        public FormBaoCaoQualityHistory() : this(new QualityHistoryQueryService()) { }

        private void BuildUi()
        {
            Text = "Lịch sử kiểm tra chất lượng";
            Size = new Size(1150, 720);
            StartPosition = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(6)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

            var filter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
                WrapContents = false
            };

            filter.Controls.Add(new LabelControl { Text = "Từ ngày:", Padding = new Padding(0, 7, 4, 0) });
            _from = new DateEdit { Width = 105 };
            _from.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            _from.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            filter.Controls.Add(_from);
            filter.Controls.Add(new LabelControl { Text = "Đến ngày:", Padding = new Padding(12, 7, 4, 0) });
            _to = new DateEdit { Width = 105 };
            _to.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            _to.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            filter.Controls.Add(_to);
            filter.Controls.Add(new LabelControl { Text = "Mã hàng:", Padding = new Padding(12, 7, 4, 0) });
            _itemCode = new TextEdit { Width = 150 };
            filter.Controls.Add(_itemCode);
            filter.Controls.Add(new LabelControl { Text = "Kết quả:", Padding = new Padding(12, 7, 4, 0) });
            _result = new ComboBoxEdit { Width = 90 };
            _result.Properties.Items.AddRange(new[] { "Tất cả", "PASS", "FAIL" });
            _result.SelectedIndex = 0;
            filter.Controls.Add(_result);

            var search = new SimpleButton { Text = "Tìm", Width = 75 };
            search.Click += async (s, e) => await LoadAsync();
            filter.Controls.Add(search);
            var export = new SimpleButton { Text = "Excel", Width = 75 };
            export.Click += ExportClick;
            filter.Controls.Add(export);
            root.Controls.Add(filter, 0, 0);

            _summary = new LabelControl { Dock = DockStyle.Fill, Padding = new Padding(4, 5, 0, 0) };
            root.Controls.Add(_summary, 0, 1);

            var masterGroup = new GroupControl { Text = "Danh sách phiên kiểm tra", Dock = DockStyle.Fill };
            _masterGrid = new GridControl { Dock = DockStyle.Fill };
            _masterView = new GridView(_masterGrid);
            _masterGrid.MainView = _masterView;
            _masterView.OptionsBehavior.Editable = false;
            _masterView.OptionsView.ShowGroupPanel = false;
            AddColumn("InspectionCode", "Mã phiên", 150);
            AddColumn("ItemCode", "Mã hàng", 120);
            AddColumn("LotNo", "Lot tổng", 170);
            AddColumn("ProductionDate", "Ngày SX", 90);
            AddColumn("TotalQuantity", "SL tổng", 70);
            AddColumn("TotalBox", "Số thùng", 75);
            AddColumn("PassCount", "Đạt", 55);
            AddColumn("FailCount", "Không đạt", 75);
            AddColumn("FinalResult", "Kết quả", 75);
            AddColumn("CheckedAt", "Thời gian", 135);
            _masterView.FocusedRowChanged += MasterFocusedRowChanged;
            masterGroup.Controls.Add(_masterGrid);
            root.Controls.Add(masterGroup, 0, 2);

            var detailGroup = new GroupControl { Text = "Chi tiết thùng kiểm tra", Dock = DockStyle.Fill };
            _detailGrid = new GridControl { Dock = DockStyle.Fill };
            _detailView = new GridView(_detailGrid);
            _detailGrid.MainView = _detailView;
            _detailView.OptionsBehavior.Editable = false;
            _detailView.OptionsView.ShowGroupPanel = false;
            AddDetailColumn("BoxLotNo", "Lot thùng", 220);
            AddDetailColumn("BoxProductionDate", "Ngày SX", 100);
            AddDetailColumn("IsMatch", "Khớp", 60);
            AddDetailColumn("MismatchFields", "Trường sai", 320);
            AddDetailColumn("CheckedAt", "Thời gian", 140);
            detailGroup.Controls.Add(_detailGrid);
            root.Controls.Add(detailGroup, 0, 3);
            Controls.Add(root);
        }

        private void AddColumn(string field, string caption, int width)
        {
            _masterView.Columns.Add(new GridColumn { FieldName = field, Caption = caption, Width = width, Visible = true });
        }

        private void AddDetailColumn(string field, string caption, int width)
        {
            _detailView.Columns.Add(new GridColumn { FieldName = field, Caption = caption, Width = width, Visible = true });
        }

        private async Task LoadAsync()
        {
            try
            {
                UseWaitCursor = true;
                var from = _from.DateTime.Date;
                var to = _to.DateTime.Date.AddDays(1).AddTicks(-1);
                if (from > to)
                {
                    XtraMessageBox.Show("Khoảng ngày không hợp lệ.", "Tra cứu");
                    return;
                }

                var rows = await _query.SearchAsync(
                    from,
                    to,
                    _itemCode.Text.Trim(),
                    _result.EditValue == null ? null : _result.EditValue.ToString(),
                    CancellationToken.None);

                _masterGrid.DataSource = rows.ToList();
                _detailGrid.DataSource = null;
                var pass = rows.Count(r => string.Equals(r.FinalResult, "PASS", StringComparison.OrdinalIgnoreCase));
                var fail = rows.Count(r => string.Equals(r.FinalResult, "FAIL", StringComparison.OrdinalIgnoreCase));
                _summary.Text = string.Format("Tổng phiên: {0} | PASS: {1} | FAIL: {2}", rows.Count, pass, fail);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Không thể tải lịch sử kiểm tra.\n" + ex.Message, "Lỗi");
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async void MasterFocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            var row = _masterView.GetFocusedRow() as QualityHistoryRow;
            if (row == null || string.IsNullOrWhiteSpace(row.InspectionCode))
            {
                _detailGrid.DataSource = null;
                return;
            }

            try
            {
                var details = await _query.GetDetailsAsync(row.InspectionCode, CancellationToken.None);
                _detailGrid.DataSource = details.ToList();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Không thể tải chi tiết phiên kiểm tra.\n" + ex.Message, "Lỗi");
            }
        }

        private void ExportClick(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel|*.xlsx";
                dialog.FileName = "LichSuKiemTra_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx";
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    _masterGrid.ExportToXlsx(dialog.FileName);
                    XtraMessageBox.Show("Xuất Excel thành công.", "OK");
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show("Lỗi xuất Excel.\n" + ex.Message, "Lỗi");
                }
            }
        }
    }
}
