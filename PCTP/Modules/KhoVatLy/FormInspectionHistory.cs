using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    public partial class FormInspectionHistory : DevExpress.XtraEditors.XtraForm
    {
        private readonly IInspectionLogRepository _logRepo;
        private readonly IWarehouseService _warehouseSvc;

        private DateEdit _dtFrom, _dtTo;
        private SearchLookUpEdit _cmbItemCode; // ✅ thay TextEdit
        private ComboBoxEdit _cmbResult;
        private GridControl _gridMaster, _gridDetail;
        private GridView _viewMaster, _viewDetail;
        private LabelControl _lblSummary;
        private DataTable _dtItems;     // ✅ thêm

        public FormInspectionHistory(IInspectionLogRepository logRepo, IWarehouseService warehouseSvc)
        {
            InitializeComponent();
            _logRepo = logRepo ?? throw new ArgumentNullException(nameof(logRepo));
            _warehouseSvc = warehouseSvc ?? throw new ArgumentNullException(nameof(warehouseSvc));
            LoadItems(); // ✅ load trước khi BuildUI
            BuildUI();
            _dtFrom.DateTime = DateTime.Today.AddDays(-7);
            _dtTo.DateTime = DateTime.Today;
            LoadData();
        }

        // ✅ Load danh sách mã hàng từ B7R2_FCC
        private void LoadItems()
        {
            try
            {
                _dtItems = _warehouseSvc.GetActiveItemList();
            }
            catch
            {
                _dtItems = new DataTable();
                _dtItems.Columns.Add("Code");
                _dtItems.Columns.Add("Name");
            }
        }

        private void BuildUI()
        {
            this.Text = "Lịch sử kiểm tra hàng hóa";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            // ── Row 0: Filter ──────────────────────────────────────
            var grpFilter = new GroupControl { Text = "🔍 Tìm kiếm", Dock = DockStyle.Fill };

            var filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                Padding = new Padding(5)
            };
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); // ✅ rộng hơn cho lookup
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            filterLayout.Controls.Add(new LabelControl
            {
                Text = "Từ ngày:",
                AutoSize = true,
                Padding = new Padding(0, 7, 0, 0)
            }, 0, 0);

            _dtFrom = new DateEdit { Width = 105 };
            _dtFrom.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            _dtFrom.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            _dtFrom.Properties.Mask.MaskType =
                DevExpress.XtraEditors.Mask.MaskType.DateTimeAdvancingCaret;
            filterLayout.Controls.Add(_dtFrom, 1, 0);

            filterLayout.Controls.Add(new LabelControl
            {
                Text = "Đến ngày:",
                AutoSize = true,
                Padding = new Padding(0, 7, 0, 0)
            }, 2, 0);

            _dtTo = new DateEdit { Width = 105 };
            _dtTo.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            _dtTo.Properties.EditFormat.FormatString = "dd/MM/yyyy";
            filterLayout.Controls.Add(_dtTo, 3, 0);

            filterLayout.Controls.Add(new LabelControl
            {
                Text = "Mã hàng:",
                AutoSize = true,
                Padding = new Padding(0, 7, 0, 0)
            }, 4, 0);

            // ✅ SearchLookUpEdit thay TextEdit
            _cmbItemCode = new SearchLookUpEdit { Width = 205 };
            _cmbItemCode.Properties.DataSource = _dtItems;
            _cmbItemCode.Properties.ValueMember = "Code";
            _cmbItemCode.Properties.DisplayMember = "Code";
            _cmbItemCode.Properties.NullText = "-- Tất cả --";

            var popupView = _cmbItemCode.Properties.View;
            popupView.Columns.Clear();
            popupView.Columns.AddVisible("Code", "Mã hàng");
            popupView.Columns.AddVisible("Name", "Tên hàng");
            popupView.Columns["Code"].Width = 130;
            popupView.Columns["Name"].Width = 250;
            popupView.OptionsView.ShowAutoFilterRow = true;

            filterLayout.Controls.Add(_cmbItemCode, 5, 0);

            _cmbResult = new ComboBoxEdit { Width = 85 };
            _cmbResult.Properties.Items.AddRange(new[] { "Tất cả", "PASS", "FAIL" });
            _cmbResult.EditValue = "Tất cả";
            filterLayout.Controls.Add(_cmbResult, 6, 0);

            var btnSearch = new SimpleButton { Text = "🔍 Tìm", Width = 80, Height = 30 };
            btnSearch.Appearance.BackColor = Color.SteelBlue;
            btnSearch.Appearance.ForeColor = Color.White;
            btnSearch.Click += (s, e) => LoadData();

            var btnExport = new SimpleButton { Text = "📥 Excel", Width = 85, Height = 30 };
            btnExport.Click += BtnExport_Click;

            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5, 5, 0, 0)
            };
            btnFlow.Controls.Add(btnSearch);
            btnFlow.Controls.Add(btnExport);
            filterLayout.Controls.Add(btnFlow, 7, 0);

            grpFilter.Controls.Add(filterLayout);
            mainLayout.Controls.Add(grpFilter, 0, 0);

            // ── Row 1: Summary ─────────────────────────────────────
            _lblSummary = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance =
            {
                Font      = new Font("Tahoma", 9, FontStyle.Bold),
                ForeColor = Color.DarkSlateGray
            },
                Padding = new Padding(8, 5, 0, 0)
            };
            mainLayout.Controls.Add(_lblSummary, 0, 1);

            // ── Row 2: Grid Master ─────────────────────────────────
            var grpMaster = new GroupControl
            {
                Text = "📋 Danh sách phiên kiểm tra",
                Dock = DockStyle.Fill
            };

            _gridMaster = new GridControl { Dock = DockStyle.Fill };
            _viewMaster = new GridView(_gridMaster);
            _gridMaster.MainView = _viewMaster;
            _viewMaster.OptionsView.ShowGroupPanel = false;
            _viewMaster.OptionsBehavior.Editable = false;

            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "InspectionCode", Caption = "Mã phiên KT", Width = 170, VisibleIndex = 0 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "ItemCode", Caption = "Mã hàng", Width = 160, VisibleIndex = 1 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "LotNoTong", Caption = "LotNo Tổng", Width = 200, VisibleIndex = 2 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "NSXTong", Caption = "Ngày SX", Width = 90, VisibleIndex = 3 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "SoLuongTong", Caption = "SL Tổng", Width = 70, VisibleIndex = 4 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "TotalBox", Caption = "Số thùng KT", Width = 90, VisibleIndex = 5 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "PassCount", Caption = "Đạt", Width = 55, VisibleIndex = 6 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "FailCount", Caption = "Không đạt", Width = 80, VisibleIndex = 7 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "FinalResult", Caption = "Kết quả", Width = 80, VisibleIndex = 8 });
            _viewMaster.Columns.Add(new GridColumn
            { FieldName = "CheckedAt", Caption = "Thời gian KT", Width = 140, VisibleIndex = 9 });

            _viewMaster.Columns["CheckedAt"].DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.DateTime;
            _viewMaster.Columns["CheckedAt"].DisplayFormat.FormatString =
                "dd/MM/yyyy HH:mm:ss";

            _viewMaster.RowStyle += (s, e) =>
            {
                var row = _viewMaster.GetRow(e.RowHandle) as System.Data.DataRowView;
                if (row == null) return;
                string res = row["FinalResult"]?.ToString();
                if (res == "PASS") e.Appearance.BackColor = Color.FromArgb(220, 255, 220);
                if (res == "FAIL") e.Appearance.BackColor = Color.FromArgb(255, 220, 220);
            };

            _viewMaster.FocusedRowChanged += ViewMaster_FocusedRowChanged;
            grpMaster.Controls.Add(_gridMaster);
            mainLayout.Controls.Add(grpMaster, 0, 2);

            // ── Row 3: Grid Detail ─────────────────────────────────
            var grpDetail = new GroupControl
            {
                Text = "📦 Chi tiết từng thùng kiểm tra",
                Dock = DockStyle.Fill
            };

            _gridDetail = new GridControl { Dock = DockStyle.Fill };
            _viewDetail = new GridView(_gridDetail);
            _gridDetail.MainView = _viewDetail;
            _viewDetail.OptionsView.ShowGroupPanel = false;
            _viewDetail.OptionsBehavior.Editable = false;

            _viewDetail.Columns.Add(new GridColumn
            { FieldName = "BoxLotNo", Caption = "LotNo Thùng", Width = 220, VisibleIndex = 0 });
            _viewDetail.Columns.Add(new GridColumn
            { FieldName = "BoxNSX", Caption = "Ngày SX Thùng", Width = 110, VisibleIndex = 1 });
            _viewDetail.Columns.Add(new GridColumn
            { FieldName = "IsMatch", Caption = "Khớp", Width = 60, VisibleIndex = 2 });
            _viewDetail.Columns.Add(new GridColumn
            { FieldName = "MismatchFields", Caption = "Trường sai", Width = 300, VisibleIndex = 3 });
            _viewDetail.Columns.Add(new GridColumn
            { FieldName = "CheckedAt", Caption = "Thời gian", Width = 140, VisibleIndex = 4 });

            _viewDetail.Columns["CheckedAt"].DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.DateTime;
            _viewDetail.Columns["CheckedAt"].DisplayFormat.FormatString =
                "dd/MM/yyyy HH:mm:ss";

            _viewDetail.RowStyle += (s, e) =>
            {
                var row = _viewDetail.GetRow(e.RowHandle) as System.Data.DataRowView;
                if (row == null) return;
                bool match = row["IsMatch"] != DBNull.Value &&
                             Convert.ToBoolean(row["IsMatch"]);
                e.Appearance.BackColor = match ? Color.LightGreen : Color.LightSalmon;
            };

            grpDetail.Controls.Add(_gridDetail);
            mainLayout.Controls.Add(grpDetail, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private void LoadData()
        {
            DateTime from = _dtFrom.DateTime.Date;
            DateTime to = _dtTo.DateTime.Date.AddDays(1).AddSeconds(-1);
            string itemCode = _cmbItemCode.EditValue?.ToString();
            string result = _cmbResult.EditValue?.ToString();

            var dt = _logRepo.GetHistoryMaster(from, to, itemCode, result);
            _gridMaster.DataSource = dt;

            int total = dt.Rows.Count;
            int passCount = dt.AsEnumerable().Count(r => r["FinalResult"]?.ToString() == "PASS");
            int failCount = dt.AsEnumerable().Count(r => r["FinalResult"]?.ToString() == "FAIL");

            _lblSummary.Text = $"Tổng phiên: {total}   |   ✅ PASS: {passCount}   |   ❌ FAIL: {failCount}";
            _gridDetail.DataSource = null;
        }

        private void ViewMaster_FocusedRowChanged(object sender,
            DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            var row = _viewMaster.GetFocusedRow() as DataRowView;
            if (row == null) return;

            string inspCode = row["InspectionCode"]?.ToString();
            if (string.IsNullOrEmpty(inspCode)) return;

            _gridDetail.DataSource = _logRepo.GetHistoryDetail(inspCode);
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel|*.xlsx";
                dlg.FileName = $"LichSuKiemTra_{DateTime.Now:yyyyMMdd}";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    _gridMaster.ExportToXlsx(dlg.FileName);
                    MessageBox.Show("Xuất Excel thành công!", "OK",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất Excel:\n{ex.Message}",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
