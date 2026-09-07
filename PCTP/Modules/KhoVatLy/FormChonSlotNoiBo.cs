using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    /// <summary>
    /// Chọn 1 Slot đang chứa hàng (và 1 LOT cụ thể trong Slot đó, vì 1 Slot có thể
    /// chứa nhiều LOT — đặc biệt Slot ảo A0) để tạo Phiếu Xử Lý Bất Thường NGUỒN NỘI BỘ
    /// (Nguon = NoiBo), KHÔNG đi qua chứng từ khách trả (Mốc 1/2).
    ///
    /// Sau khi tạo thành công, phiếu rơi thẳng vào TrangThai = ChoQC (Mốc 3 — QC định hướng),
    /// y hệt phiếu sinh từ nhánh khách trả — dùng chung toàn bộ luồng QC/SX phía sau.
    /// </summary>
    public partial class FormChonSlotNoiBo : XtraForm
    {
        private readonly ISlotService _slotSvc;
        private readonly ITraNoiBoService _traNoiBoSvc;
        private readonly IPhieuTraHangRepository _phieuTraHangRepo;
        private readonly IQTChungService _qtChungSvc;

        private GridControl _grid;
        private GridView _gridView;
        private TextEdit _txtSearch;
        private LabelControl _lblSelected;

        private TextEdit _txtModel;
        private TextEdit _txtMaSanPham;
        private TextEdit _txtSoLo;
        private SpinEdit _spinSoLuongLoi;
        private MemoEdit _txtNoiDung;
        private TextEdit _txtNguoiPhatHien;

        private SimpleButton _btnTaoPhieu;
        private SimpleButton _btnRefresh;
        private SimpleButton _btnCancel;

        private DataRow _selectedRow;

        public FormChonSlotNoiBo(ITraNoiBoService traNoiBoSvc,
        IPhieuTraHangRepository phieuTraHangRepo,
        IQTChungService qtChungSvc,
        ISlotService slotSvc)
        {
            _traNoiBoSvc = traNoiBoSvc ?? throw new ArgumentNullException(nameof(traNoiBoSvc));
            _phieuTraHangRepo = phieuTraHangRepo ?? throw new ArgumentNullException(nameof(phieuTraHangRepo));
            _qtChungSvc = qtChungSvc ?? throw new ArgumentNullException(nameof(qtChungSvc));
            _slotSvc = slotSvc ?? throw new ArgumentNullException(nameof(slotSvc));
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "Tạo phiếu xử lý bất thường — Nguồn nội bộ (từ Slot)";
            Size = new Size(1050, 700);
            StartPosition = FormStartPosition.CenterParent;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // search
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 55));    // grid slot/lot
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 45));    // panel nhập liệu
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // buttons

            // ── Row 0: Search ──────────────────────────────────────────
            var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

            _txtSearch = new TextEdit
            {
                Dock = DockStyle.Fill,
                Properties = { NullValuePrompt = "🔍 Lọc theo Mã hàng / LOT / Kho / Rack..." }
            };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };
            searchPanel.Controls.Add(_txtSearch, 0, 0);

            _btnRefresh = new SimpleButton { Text = "🔄 Làm mới", Dock = DockStyle.Fill };
            _btnRefresh.Click += (s, e) => { LoadData(); _txtSearch.Text = ""; };
            searchPanel.Controls.Add(_btnRefresh, 1, 0);

            main.Controls.Add(searchPanel, 0, 0);

            // ── Row 1: Grid Slot/Lot ─────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _gridView.OptionsBehavior.Editable = false;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsSelection.EnableAppearanceFocusedCell = false;
            _gridView.FocusedRowChanged += GridView_FocusedRowChanged;
            _gridView.DoubleClick += (s, e) => { if (_selectedRow != null) _spinSoLuongLoi.Focus(); };

            _gridView.Columns.Add(new GridColumn { FieldName = "WarehouseName", Caption = "Kho", Width = 110, VisibleIndex = 0 });
            _gridView.Columns.Add(new GridColumn { FieldName = "RackName", Caption = "Rack", Width = 100, VisibleIndex = 1 });
            _gridView.Columns.Add(new GridColumn { FieldName = "SlotNumber", Caption = "Slot", Width = 60, VisibleIndex = 2 });
            _gridView.Columns.Add(new GridColumn { FieldName = "ItemCode", Caption = "Mã hàng", Width = 130, VisibleIndex = 3 });
            _gridView.Columns.Add(new GridColumn { FieldName = "LotNo", Caption = "LotNo", Width = 220, VisibleIndex = 4 });
            var colQty = new GridColumn { FieldName = "Quantity", Caption = "SL tồn", Width = 80, VisibleIndex = 5 };
            colQty.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            colQty.DisplayFormat.FormatString = "n0";
            _gridView.Columns.Add(colQty);
            _gridView.Columns.Add(new GridColumn { FieldName = "TemCode", Caption = "TemCode", Width = 130, VisibleIndex = 6 });

            main.Controls.Add(_grid, 0, 1);

            // ── Row 2: Panel nhập liệu ────────────────────────────────────
            var grpInput = new GroupControl { Text = "Thông tin phiếu xử lý bất thường (Nội bộ)", Dock = DockStyle.Fill };

            var inputLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(8)
            };
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _lblSelected = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Text = "Chưa chọn Slot/LOT nào.",
                Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkSlateGray }
            };
            inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            inputLayout.Controls.Add(_lblSelected, 0, 0);
            inputLayout.SetColumnSpan(_lblSelected, 2);

            int r = 1;
            void AddRow(string label, Control ctrl, int height = 28)
            {
                inputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                inputLayout.Controls.Add(new LabelControl { Text = label, Dock = DockStyle.Fill }, 0, r);
                inputLayout.Controls.Add(ctrl, 1, r);
                r++;
            }

            _txtModel = new TextEdit { Dock = DockStyle.Fill };
            AddRow("Model:", _txtModel);

            _txtMaSanPham = new TextEdit { Dock = DockStyle.Fill, Properties = { ReadOnly = true } };
            AddRow("Mã sản phẩm:", _txtMaSanPham);

            _txtSoLo = new TextEdit { Dock = DockStyle.Fill, Properties = { ReadOnly = true } };
            AddRow("Số lô (LOT):", _txtSoLo);

            _spinSoLuongLoi = new SpinEdit { Dock = DockStyle.Fill, Properties = { MinValue = 1, IsFloatValue = false } };
            AddRow("Số lượng lỗi:", _spinSoLuongLoi);

            _txtNoiDung = new MemoEdit { Dock = DockStyle.Fill };
            AddRow("Nội dung bất thường:", _txtNoiDung, 70);

            _txtNguoiPhatHien = new TextEdit { Dock = DockStyle.Fill, Text = Environment.UserName };
            AddRow("Người phát hiện:", _txtNguoiPhatHien);

            grpInput.Controls.Add(inputLayout);
            main.Controls.Add(grpInput, 0, 2);

            // ── Row 3: Buttons ─────────────────────────────────────────
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };

            _btnCancel = new SimpleButton { Text = "Hủy", Width = 100, Height = 36 };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnTaoPhieu = new SimpleButton
            {
                Text = "📋 Tạo phiếu xử lý bất thường",
                Width = 220,
                Height = 36,
                Enabled = false
            };
            _btnTaoPhieu.Appearance.BackColor = Color.SeaGreen;
            _btnTaoPhieu.Appearance.ForeColor = Color.White;
            _btnTaoPhieu.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnTaoPhieu.Click += BtnTaoPhieu_Click;

            btnPanel.Controls.Add(_btnCancel);
            btnPanel.Controls.Add(_btnTaoPhieu);
            main.Controls.Add(btnPanel, 0, 3);

            Controls.Add(main);
        }

        // ════════════════════════════════════════════════════════════════
        // Load / Filter
        // ════════════════════════════════════════════════════════════════
        private void LoadData()
        {
            // ✅ Không còn raw SQL — gọi Kho Core qua ISlotService
            var list = _slotSvc.GetAllActiveSlotLots();

            var dt = ToDataTable(list); // helper convert List<SlotLotViewInfo> -> DataTable cho GridControl
            _grid.DataSource = dt;
            _gridView.BestFitColumns();

            _selectedRow = null;
            ClearInputPanel();
        }

        private void ApplyFilter()
        {
            string kw = _txtSearch.Text.Trim().Replace("'", "''");
            _gridView.ActiveFilterString = string.IsNullOrEmpty(kw)
                ? ""
                : $"Contains([ItemCode], '{kw}') Or Contains([LotNo], '{kw}') Or " +
                  $"Contains([WarehouseName], '{kw}') Or Contains([RackName], '{kw}')";
        }

        // ════════════════════════════════════════════════════════════════
        // Chọn dòng
        // ════════════════════════════════════════════════════════════════
        private void GridView_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            _selectedRow = _gridView.GetFocusedDataRow();
            if (_selectedRow == null)
            {
                ClearInputPanel();
                return;
            }

            string itemCode = _selectedRow["ItemCode"]?.ToString() ?? "";
            string lotNo = _selectedRow["LotNo"]?.ToString() ?? "";
            int qty = _selectedRow["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(_selectedRow["Quantity"]);
            string wh = _selectedRow["WarehouseName"]?.ToString() ?? "";
            string rack = _selectedRow["RackName"]?.ToString() ?? "";
            int slotNo = _selectedRow["SlotNumber"] == DBNull.Value ? 0 : Convert.ToInt32(_selectedRow["SlotNumber"]);

            _lblSelected.Text = $"Đã chọn: {wh} / {rack} / Slot {slotNo}  —  Mã hàng: {itemCode}  —  LOT: {lotNo}  (tồn {qty})";

            _txtMaSanPham.Text = itemCode;
            _txtSoLo.Text = lotNo;
            _spinSoLuongLoi.Properties.MaxValue = qty;
            _spinSoLuongLoi.Value = Math.Min(1, qty);

            _btnTaoPhieu.Enabled = true;
        }

        private void ClearInputPanel()
        {
            _lblSelected.Text = "Chưa chọn Slot/LOT nào.";
            _txtMaSanPham.Text = "";
            _txtSoLo.Text = "";
            _spinSoLuongLoi.Properties.MaxValue = 999999;
            _spinSoLuongLoi.Value = 1;
            _btnTaoPhieu.Enabled = false;
        }

        // ════════════════════════════════════════════════════════════════
        // Tạo phiếu
        // ════════════════════════════════════════════════════════════════
        private void BtnTaoPhieu_Click(object sender, EventArgs e)
        {
            if (_selectedRow == null)
            {
                XtraMessageBox.Show("Vui lòng chọn 1 dòng Slot/LOT trước.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int soLuongLoi = Convert.ToInt32(_spinSoLuongLoi.Value);
            int slTonKho = _selectedRow["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(_selectedRow["Quantity"]);

            if (soLuongLoi <= 0 || soLuongLoi > slTonKho)
            {
                XtraMessageBox.Show($"Số lượng lỗi phải trong khoảng 1 - {slTonKho}.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtNoiDung.Text))
            {
                XtraMessageBox.Show("Vui lòng nhập nội dung bất thường.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtNoiDung.Focus();
                return;
            }

            if (XtraMessageBox.Show(
                $"Tạo phiếu xử lý bất thường NỘI BỘ cho LOT [{_txtSoLo.Text}] — SL lỗi: {soLuongLoi}?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                int slotId = Convert.ToInt32(_selectedRow["SlotId"]);
                string nguoiThucHien = _txtNguoiPhatHien.Text.Trim();

                // Bước 1: tạo Header + Detail (PhieuTraHang / PhieuTraHangCT)
                var phieuTraHang = new PhieuTraHang
                {
                    Nguon = NguonXuLyBatThuong.TraNoiBo,
                    LyDo = _txtNoiDung.Text.Trim(),
                    CreatedBy = nguoiThucHien,
                    ChiTiet = new List<PhieuTraHangCT>
        {
            new PhieuTraHangCT
            {
                SlotIdNguon = slotId,
                MaHang = _txtMaSanPham.Text.Trim(),
                LotNo = _txtSoLo.Text.Trim(),
                SoLuong = soLuongLoi
            }
        }
                };

                int phieuTraHangId = _traNoiBoSvc.TaoPhieuTraNoiBo(phieuTraHang);

                // Bước 2: lấy lại Id dòng chi tiết vừa tạo
                var items = _phieuTraHangRepo.GetItems(phieuTraHangId);
                int phieuTraHangCTId = items.First().Id;

                // Bước 3: tạo PhieuXuLyBatThuong từ dòng chi tiết đó
                int phieuXuLyId = _qtChungSvc.TaoPhieuXuLyBatThuong(
                    phieuTraHangCTId,
                    _txtModel.Text.Trim(),
                    "Hàng lỗi nội bộ (chưa xuất)",
                    nguoiThucHien,
                    nguoiThucHien);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Lỗi tạo phiếu:\n{ex.Message}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static DataTable ToDataTable(List<SlotLotViewInfo> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("SlotId", typeof(int));
            dt.Columns.Add("SlotLotId", typeof(int));
            dt.Columns.Add("WarehouseName", typeof(string));
            dt.Columns.Add("RackName", typeof(string));
            dt.Columns.Add("SlotNumber", typeof(int));
            dt.Columns.Add("ItemCode", typeof(string));
            dt.Columns.Add("LotNo", typeof(string));
            dt.Columns.Add("Quantity", typeof(int));
            dt.Columns.Add("TemCode", typeof(string));

            foreach (var x in list)
                dt.Rows.Add(x.SlotId, x.SlotLotId, x.WarehouseName, x.RackName,
                            x.SlotNumber, x.ItemCode, x.LotNo, x.Quantity, x.TemCode);
            return dt;
        }
    }
}