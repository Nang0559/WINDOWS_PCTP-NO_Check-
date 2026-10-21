using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    /// <summary>
    /// Chọn nhanh 1 hoặc nhiều Slot/LOT đang chứa hàng để THÊM DÒNG vào form tạo phiếu
    /// (FormTaoPhieuTraNoiBo). Form này KHÔNG tự tạo phiếu / phiếu xử lý nữa — chỉ là bước
    /// tắt thay cho việc chọn từng dòng trong lookup; mọi nhập liệu header (Phòng ban,
    /// BP phát hiện lỗi, Lý do, Số phiếu...), kiểm tra hợp lệ và lưu đều do form tạo phiếu làm,
    /// nên phiếu tạo từ Slot luôn cùng định dạng với phiếu tạo thủ công.
    ///
    /// Kết quả: <see cref="KetQua"/> (SlotLotId + Số lượng + Lý do NG) khi DialogResult = OK.
    /// </summary>
    public partial class FormChonSlotNoiBo : XtraForm
    {
        private readonly ISlotService _slotSvc;
        private readonly HashSet<int> _daCo;

        private GridControl _grid;
        private GridView _gridView;
        private TextEdit _txtSearch;
        private LabelControl _lblInfo;
        private SimpleButton _btnThem;
        private SimpleButton _btnRefresh;
        private SimpleButton _btnCancel;
        private RepositoryItemSpinEdit _repoSoLuong;

        /// <summary>Các dòng đã chọn (hợp lệ) — chỉ có giá trị khi DialogResult = OK.</summary>
        public List<SlotNguonChon> KetQua { get; private set; } = new List<SlotNguonChon>();

        /// <param name="slotSvc">Nguồn dữ liệu Slot/LOT (cùng nguồn với lookup của form tạo phiếu).</param>
        /// <param name="slotLotIdDaCo">SlotLotId đã có trong phiếu — ẩn khỏi danh sách để không chọn trùng.</param>
        public FormChonSlotNoiBo(ISlotService slotSvc, IEnumerable<int> slotLotIdDaCo = null)
        {
            _slotSvc = slotSvc ?? throw new ArgumentNullException(nameof(slotSvc));
            _daCo = new HashSet<int>(slotLotIdDaCo ?? Enumerable.Empty<int>());

            InitializeComponent();
            BuildUI();
            LoadData();
        }

        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "Chọn nhanh từ Slot — thêm dòng vào phiếu";
            Size = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // search
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // grid
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // info
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

            // ── Row 1: Grid Slot/Lot (tick chọn nhiều dòng, sửa SL + Lý do NG ngay trên lưới) ──
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _grid.MainView = _gridView;
            _grid.ViewCollection.Add(_gridView);

            _gridView.OptionsBehavior.Editable = true;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsSelection.MultiSelect = true;
            _gridView.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
            _gridView.OptionsSelection.CheckBoxSelectorColumnWidth = 30;
            _gridView.SelectionChanged += (s, e) => CapNhatThongTin();
            _gridView.ShownEditor += GridView_ShownEditor;
            _gridView.CellValueChanged += GridView_CellValueChanged;
            _gridView.DoubleClick += GridView_DoubleClick;

            _repoSoLuong = new RepositoryItemSpinEdit { IsFloatValue = false, MinValue = 1, MaxValue = int.MaxValue };
            _grid.RepositoryItems.Add(_repoSoLuong);

            GridColumn Cot(string field, string caption, int width, int index, bool edit = false)
            {
                var c = new GridColumn { FieldName = field, Caption = caption, Width = width, VisibleIndex = index };
                c.OptionsColumn.AllowEdit = edit;
                _gridView.Columns.Add(c);
                return c;
            }

            Cot("WarehouseName", "Kho", 110, 0);
            Cot("RackName", "Rack", 100, 1);
            Cot("SlotNumber", "Slot", 55, 2);
            Cot("ItemCode", "Mã hàng", 130, 3);
            Cot("LotNo", "LotNo", 200, 4);
            var colQty = Cot("Quantity", "SL tồn", 75, 5);
            colQty.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            colQty.DisplayFormat.FormatString = "n0";
            Cot("TemCode", "TemCode", 130, 6);

            var colSl = Cot("SoLuongLoi", "SL lỗi", 80, 7, edit: true);
            colSl.ColumnEdit = _repoSoLuong;
            Cot("LyDoNg", "Lý do NG", 200, 8, edit: true);

            main.Controls.Add(_grid, 0, 1);

            // ── Row 2: Info ────────────────────────────────────────────
            _lblInfo = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Text = "Tick các dòng Slot/LOT cần đưa vào phiếu (sửa SL lỗi / Lý do NG ngay trên lưới).",
                Appearance = { Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkSlateGray }
            };
            main.Controls.Add(_lblInfo, 0, 2);

            // ── Row 3: Buttons ─────────────────────────────────────────
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };

            _btnCancel = new SimpleButton { Text = "Hủy", Width = 100, Height = 36 };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnThem = new SimpleButton
            {
                Text = "➕ Thêm vào phiếu",
                Width = 200,
                Height = 36,
                Enabled = false
            };
            _btnThem.Appearance.BackColor = Color.SeaGreen;
            _btnThem.Appearance.ForeColor = Color.White;
            _btnThem.Appearance.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnThem.Click += BtnThem_Click;

            btnPanel.Controls.Add(_btnCancel);
            btnPanel.Controls.Add(_btnThem);
            main.Controls.Add(btnPanel, 0, 3);

            Controls.Add(main);
        }

        // ════════════════════════════════════════════════════════════════
        // Load / Filter
        // ════════════════════════════════════════════════════════════════
        private void LoadData()
        {
            // Cùng tiêu chí với lookup của FormTaoPhieuTraNoiBo: chỉ Slot/LOT còn tồn (> 0),
            // và bỏ những SlotLot đã có trong phiếu.
            var list = (_slotSvc.GetAllActiveSlotLots() ?? new List<SlotLotViewInfo>())
                .Where(x => x.Quantity > 0 && !_daCo.Contains(x.SlotLotId))
                .ToList();

            _grid.DataSource = ToDataTable(list);
            _gridView.ClearSelection();
            _gridView.BestFitColumns();
            CapNhatThongTin();
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
        // Grid events
        // ════════════════════════════════════════════════════════════════
        // Giới hạn SL lỗi theo tồn của đúng dòng đang sửa (giống FormTaoPhieuTraNoiBo).
        private void GridView_ShownEditor(object sender, EventArgs e)
        {
            if (_gridView.FocusedColumn?.FieldName != "SoLuongLoi") return;
            var row = _gridView.GetFocusedDataRow();
            if (row == null) return;

            if (_gridView.ActiveEditor is SpinEdit spin)
                spin.Properties.MaxValue = Convert.ToInt32(row["Quantity"]);
        }

        // Sửa SL / Lý do trên 1 dòng → tự tick dòng đó để khỏi quên.
        private void GridView_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            if (e.Column.FieldName != "SoLuongLoi" && e.Column.FieldName != "LyDoNg") return;
            if (!_gridView.IsRowSelected(e.RowHandle))
                _gridView.SelectRow(e.RowHandle);
        }

        // Double-click vào cột thông tin → tick / bỏ tick dòng.
        private void GridView_DoubleClick(object sender, EventArgs e)
        {
            var pt = _grid.PointToClient(Control.MousePosition);
            var hit = _gridView.CalcHitInfo(pt);
            if (!hit.InRow) return;
            if (hit.InRowCell && hit.Column != null && hit.Column.OptionsColumn.AllowEdit) return;

            if (_gridView.IsRowSelected(hit.RowHandle)) _gridView.UnselectRow(hit.RowHandle);
            else _gridView.SelectRow(hit.RowHandle);
        }

        private void CapNhatThongTin()
        {
            int n = _gridView.GetSelectedRows().Count(h => h >= 0);
            _btnThem.Enabled = n > 0;
            _lblInfo.Text = n == 0
                ? "Tick các dòng Slot/LOT cần đưa vào phiếu (sửa SL lỗi / Lý do NG ngay trên lưới)."
                : $"Đã chọn {n} dòng.";
        }

        // ════════════════════════════════════════════════════════════════
        // Thêm vào phiếu
        // ════════════════════════════════════════════════════════════════
        private void BtnThem_Click(object sender, EventArgs e)
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            var chon = new List<SlotNguonChon>();

            foreach (int handle in _gridView.GetSelectedRows().Where(h => h >= 0))
            {
                var row = _gridView.GetDataRow(handle);
                if (row == null) continue;

                int ton = Convert.ToInt32(row["Quantity"]);
                int sl = row["SoLuongLoi"] == DBNull.Value ? 0 : Convert.ToInt32(row["SoLuongLoi"]);
                string desc = $"{row["ItemCode"]} / LOT {row["LotNo"]}";

                if (sl <= 0 || sl > ton)
                {
                    XtraMessageBox.Show($"{desc}: SL lỗi phải trong khoảng 1 - {ton}.", "Thông báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _gridView.FocusedRowHandle = handle;
                    _gridView.FocusedColumn = _gridView.Columns["SoLuongLoi"];
                    return;
                }

                chon.Add(new SlotNguonChon
                {
                    SlotLotId = Convert.ToInt32(row["SlotLotId"]),
                    SoLuong = sl,
                    LyDoNg = (row["LyDoNg"] as string ?? string.Empty).Trim()
                });
            }

            if (chon.Count == 0)
            {
                XtraMessageBox.Show("Vui lòng tick ít nhất 1 dòng Slot/LOT.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            KetQua = chon;
            DialogResult = DialogResult.OK;
            Close();
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
            dt.Columns.Add("SoLuongLoi", typeof(int));
            dt.Columns.Add("LyDoNg", typeof(string));

            foreach (var x in list)
                dt.Rows.Add(x.SlotId, x.SlotLotId, x.WarehouseName, x.RackName,
                            x.SlotNumber, x.ItemCode, x.LotNo, x.Quantity, x.TemCode,
                            x.Quantity, string.Empty); // mặc định SL lỗi = toàn bộ tồn (giống chọn Slot trong lookup)
            return dt;
        }
    }
}
