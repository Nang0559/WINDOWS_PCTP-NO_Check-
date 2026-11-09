

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using DevExpress.XtraEditors.Repository;
    using DevExpress.XtraGrid;
    using DevExpress.XtraGrid.Columns;
    using DevExpress.XtraGrid.Views.Grid;
    using PCTP.Modules.KhoVatLy;
    using PCTP.Modules.KhoVatLy.Application.Interfaces;
    using PCTP.Modules.XuLyHangLoi.Enums;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Modules.XuLyHangLoi.Repository;
    using PCTP.Modules.XuLyHangLoi.Services;
    using PCTP.Shared.Enums;
    using PCTP.Shared.Lookup;
    using PCTP.Shared.Models;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Windows.Forms;

    public partial class FormTiepNhanPhieuKhachTra : XtraForm
    {
        // ============================================================
        // SERVICE
        // ============================================================

        private readonly IKhachTraHangService _khachTraHangService;

        // ============================================================
        // HEADER
        // ============================================================

        private TextEdit _txtSoPhieu;
        private LookUpEdit _cboNguonKhachTra;
        private List<KhachHangLookupItem> _khachHangs = new List<KhachHangLookupItem>();
        private bool _dangNapDuLieu; // true khi nạp phiếu cũ: không tự ghi đè Tên khách hàng đã lưu
        private TextEdit _txtSoPhieuKhach;
        private DateEdit _dtNgayPhatHanh;
        private TextEdit _txtSlipNo;
        private TextEdit _txtCa;
        private TextEdit _txtTenKhachHang;
        private TextEdit _txtLyDo;
        private TextEdit _txtBoPhanPhatHienLoi;
        private TextEdit _txtNguoiTao;

        // ============================================================
        // GRID DETAIL
        // ============================================================

        private GridControl _grid;
        private GridView _gridView;

        private SimpleButton _btnThem;
        private SimpleButton _btnXoa;
        private SimpleButton _btnLuu;
        private SimpleButton _btnHuy;
        private SimpleButton _btnXoaPhieu;
        private SimpleButton _btnTraSlot;
        private SimpleButton _btnDoiChieu;
        private SimpleButton _btnLuongDi;

        // ============================================================
        // MỞ LẠI PHIẾU / QR
        // ============================================================

        private enum FormMode { Create, Edit, ReadOnly }

        private readonly FormMode _mode = FormMode.Create;
        private readonly IPhieuTraHangRepository _phieuRepo;
        private readonly INoiBoLookupRepository _lookupRepo;
        private readonly ISlotService _slotService; // null → ẩn nút tra cứu Slot
        private readonly IReworkStockService _reworkStockService; // null → không nhập kho NG khi tiếp nhận

        /// <summary>Id phiếu vừa tiếp nhận (chế độ tạo mới); null nếu chưa lưu.</summary>
        public int? CreatedPhieuId { get; private set; }
        private readonly int _phieuId;
        private PhieuTraHang _editPhieu;

        // Đã có PhieuXuLyBatThuong → chỉ sửa thông tin chung.
        private bool _choSuaChiTiet = true;

        // Tra cứu dùng chung (Tìm theo mã / Tìm theo LOT-QR) thay cho ô "Quét QR thêm dòng".
        private StockLookupControl _stockLookup;
        private LabelControl _lblQrStatus;

        // Danh mục mã hàng đầy đủ (vB20Item): nguồn DUY NHẤT cho Mã hàng của phiếu khách trả.
        // Không lấy từ tồn kho vì hàng khách trả có thể đã hết kho / LOT không còn.
        private List<ItemLookupItem> _itemCatalog = new List<ItemLookupItem>();
        private readonly Dictionary<string, ItemLookupItem> _itemByCode =
            new Dictionary<string, ItemLookupItem>(StringComparer.OrdinalIgnoreCase);
        private string _catalogLoadError;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public FormTiepNhanPhieuKhachTra(
            IKhachTraHangService khachTraHangService,
            INoiBoLookupRepository lookupRepo = null,
            ISlotService slotService = null,
            IReworkStockService reworkStockService = null)
        {
            _khachTraHangService =
                khachTraHangService
                ?? throw new ArgumentNullException(
                    nameof(khachTraHangService));

            _lookupRepo = lookupRepo;
            _slotService = slotService;
            _reworkStockService = reworkStockService;

            BuidIU();

            KhoiTaoDuLieuBanDau();
        }

        /// <summary>
        /// Mở lại phiếu khách trả đã tiếp nhận: chưa đóng → Lưu thay đổi / Xóa;
        /// đã đóng → chỉ xem.
        /// </summary>
        public FormTiepNhanPhieuKhachTra(
            IKhachTraHangService khachTraHangService,
            IPhieuTraHangRepository phieuRepo,
            int phieuTraHangId,
            INoiBoLookupRepository lookupRepo = null,
            ISlotService slotService = null)
        {
            _khachTraHangService =
                khachTraHangService
                ?? throw new ArgumentNullException(
                    nameof(khachTraHangService));

            _phieuRepo = phieuRepo
                ?? throw new ArgumentNullException(nameof(phieuRepo));

            _lookupRepo = lookupRepo;
            _slotService = slotService;
            _phieuId = phieuTraHangId;

            _editPhieu = _khachTraHangService.GetById(phieuTraHangId)
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy phiếu khách trả Id={phieuTraHangId}.");

            _mode = _khachTraHangService.LaPhieuDaDong(_editPhieu)
                ? FormMode.ReadOnly
                : FormMode.Edit;

            BuidIU();

            NapPhieuDaTao();
            ApDungCheDo();
        }

        // ============================================================
        // INITIALIZE UI
        // ============================================================

        private void BuidIU()
        {
            Text = "Tiếp nhận phiếu khách trả hàng";

            StartPosition =
                FormStartPosition.CenterParent;

            Width = 1400;
            Height = 800;

            // Danh mục mã hàng phải sẵn sàng trước khi dựng lưới / ô tra cứu.
            NapDanhMucMaHang();

            // ========================================================
            // MAIN
            // ========================================================

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };

            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    205));

            // Hàng tra cứu: StockLookupControl cần ~78px (2 tab).
            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    84));

            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100));

            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    48));

            // ========================================================
            // HEADER
            // ========================================================

            var header = TaoHeader();

            CanGiuaOCuaHeader(header);

            mainLayout.Controls.Add(
                header,
                0,
                0);

            mainLayout.Controls.Add(
                TaoTraCuuPanel(),
                0,
                1);

            // ========================================================
            // DETAIL GRID
            // ========================================================

            _grid = new GridControl
            {
                Dock = DockStyle.Fill
            };

            _gridView =
                new GridView(_grid);

            _grid.MainView =
                _gridView;

            _grid.ViewCollection.Add(
                _gridView);

            CauHinhGrid();

            mainLayout.Controls.Add(
                _grid,
                0,
                2);

            // ========================================================
            // BUTTON BAR
            // ========================================================

            var buttonPanel =
                new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection =
                        FlowDirection.RightToLeft,
                    Padding =
                        new Padding(0, 5, 0, 0)
                };

            _btnHuy = new SimpleButton
            {
                Text = "Hủy",
                Width = 100
            };

            _btnLuu = new SimpleButton
            {
                Text = "💾 Tiếp nhận phiếu",
                Width = 150
            };

            _btnXoaPhieu = new SimpleButton
            {
                Text = "❌ Xóa phiếu",
                Width = 110
            };

            _btnXoaPhieu.Click +=
                (s, e) => XoaPhieu();

            _btnXoa = new SimpleButton
            {
                Text = "🗑 Xóa dòng",
                Width = 110
            };

            _btnThem = new SimpleButton
            {
                Text = "➕ Thêm dòng",
                Width = 110
            };

            _btnTraSlot = new SimpleButton
            {
                Text = "📦 Tra Slot theo LOT",
                Width = 160,
                Visible = _slotService != null
            };

            _btnTraSlot.Click +=
                (s, e) => TraSlotTheoLot();

            // Khách trả phải đối chiếu được với lần giao gốc (hàng đã giao).
            _btnDoiChieu = new SimpleButton
            {
                Text = "🔗 Đối chiếu phiếu giao",
                Width = 190
            };

            _btnDoiChieu.Click +=
                (s, e) => DoiChieuPhieuGiao();

            // Hàng báo lỗi: xem đủ luồng đi (SX → kho → giao) để xác định đúng nguồn gốc lịch sử.
            _btnLuongDi = new SimpleButton
            {
                Text = "🧭 Luồng đi LOT",
                Width = 150
            };

            _btnLuongDi.Click +=
                (s, e) => XemLuongDi();

            _btnHuy.Click +=
                (s, e) =>
                {
                    DialogResult =
                        DialogResult.Cancel;

                    Close();
                };

            _btnLuu.Click +=
                (s, e) => LuuPhieu();

            _btnThem.Click +=
                (s, e) => ThemDong();

            _btnXoa.Click +=
                (s, e) => XoaDong();

            buttonPanel.Controls.Add(_btnHuy);
            buttonPanel.Controls.Add(_btnLuu);
            buttonPanel.Controls.Add(_btnXoaPhieu);
            buttonPanel.Controls.Add(_btnXoa);
            buttonPanel.Controls.Add(_btnThem);
            buttonPanel.Controls.Add(_btnDoiChieu);
            buttonPanel.Controls.Add(_btnLuongDi);
            buttonPanel.Controls.Add(_btnTraSlot);

            mainLayout.Controls.Add(
                buttonPanel,
                0,
                3);

            Controls.Add(mainLayout);
        }

        // ============================================================
        // HEADER UI
        // ============================================================

        private TableLayoutPanel TaoHeader()
        {
            var header =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 6
                };

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145));

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50));

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    145));

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50));

            for (int i = 0; i < 6; i++)
            {
                header.RowStyles.Add(
                    new RowStyle(
                        SizeType.Percent,
                        16.66f));
            }

            // ========================================================
            // SỐ PHIẾU
            // ========================================================

            _txtSoPhieu =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "Số phiếu nội bộ",
                0,
                0);

            header.Controls.Add(
                _txtSoPhieu,
                1,
                0);

            // ========================================================
            // NGUỒN KHÁCH TRẢ
            // ========================================================

            // Lookup Mã + Tên, dữ liệu lấy từ CustomerTableConfig (nguồn duy nhất).
            _khachHangs = BuildKhachHangLookup();

            _cboNguonKhachTra =
                new LookUpEdit
                {
                    Dock = DockStyle.Fill
                };

            _cboNguonKhachTra.Properties.TextEditStyle =
                DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            _cboNguonKhachTra.Properties.NullText = string.Empty;
            _cboNguonKhachTra.Properties.DataSource = _khachHangs;
            _cboNguonKhachTra.Properties.ValueMember = nameof(KhachHangLookupItem.CustomerNo);
            _cboNguonKhachTra.Properties.DisplayMember = nameof(KhachHangLookupItem.Display);
            _cboNguonKhachTra.Properties.PopupWidth = 420;
            _cboNguonKhachTra.Properties.Columns.Clear();
            _cboNguonKhachTra.Properties.Columns.Add(
                new DevExpress.XtraEditors.Controls.LookUpColumnInfo(
                    nameof(KhachHangLookupItem.CustomerNo), "Mã KH", 90));
            _cboNguonKhachTra.Properties.Columns.Add(
                new DevExpress.XtraEditors.Controls.LookUpColumnInfo(
                    nameof(KhachHangLookupItem.TenKhachHang), "Tên khách hàng", 260));

            // Chọn khách → tự điền Tên khách hàng.
            _cboNguonKhachTra.EditValueChanged +=
                (s, e) => CapNhatTenKhachHangTuNguon();

            AddLabel(
                header,
                "Nguồn khách trả",
                2,
                0);

            header.Controls.Add(
                _cboNguonKhachTra,
                3,
                0);

            // ========================================================
            // SỐ PHIẾU KHÁCH
            // ========================================================

            _txtSoPhieuKhach =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "Số phiếu khách",
                0,
                1);

            header.Controls.Add(
                _txtSoPhieuKhach,
                1,
                1);

            // ========================================================
            // NGÀY PHÁT HÀNH
            // ========================================================

            _dtNgayPhatHanh =
                new DateEdit
                {
                    Dock = DockStyle.Fill
                };

            _dtNgayPhatHanh.Properties.DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.DateTime;

            _dtNgayPhatHanh.Properties.DisplayFormat.FormatString =
                "dd/MM/yyyy";

            _dtNgayPhatHanh.Properties.EditFormat.FormatType =
                DevExpress.Utils.FormatType.DateTime;

            _dtNgayPhatHanh.Properties.EditFormat.FormatString =
                "dd/MM/yyyy";

            AddLabel(
                header,
                "Ngày phát hành",
                2,
                1);

            header.Controls.Add(
                _dtNgayPhatHanh,
                3,
                1);

            // ========================================================
            // SLIP NO
            // ========================================================

            _txtSlipNo =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "Slip No",
                0,
                2);

            header.Controls.Add(
                _txtSlipNo,
                1,
                2);

            // ========================================================
            // CA
            // ========================================================

            _txtCa =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "Ca",
                2,
                2);

            header.Controls.Add(
                _txtCa,
                3,
                2);

            // ========================================================
            // KHÁCH HÀNG
            // ========================================================

            _txtTenKhachHang =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "Tên khách hàng",
                0,
                3);

            header.Controls.Add(
                _txtTenKhachHang,
                1,
                3);

            // ========================================================
            // BỘ PHẬN PHÁT HIỆN
            // ========================================================

            _txtBoPhanPhatHienLoi =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "BP phát hiện lỗi",
                2,
                3);

            header.Controls.Add(
                _txtBoPhanPhatHienLoi,
                3,
                3);

            // ========================================================
            // LÝ DO
            // ========================================================

            _txtLyDo =
                new TextEdit
                {
                    Dock = DockStyle.Fill
                };

            AddLabel(
                header,
                "Lý do",
                0,
                4);

            header.Controls.Add(
                _txtLyDo,
                1,
                4);

            header.SetColumnSpan(
                _txtLyDo,
                3);

            // ========================================================
            // NGƯỜI TẠO
            // ========================================================

            _txtNguoiTao =
                new TextEdit
                {
                    Dock = DockStyle.Fill,
                    Text = Environment.UserName
                };

            AddLabel(
                header,
                "Người tiếp nhận",
                0,
                5);

            header.Controls.Add(
                _txtNguoiTao,
                1,
                5);

            return header;
        }

        // ============================================================
        // LABEL
        // ============================================================

        private void AddLabel(
            TableLayoutPanel panel,
            string text,
            int column,
            int row)
        {
            var label =
                new LabelControl
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    AutoSizeMode =
                        LabelAutoSizeMode.None,
                    Padding =
                        new Padding(5, 0, 0, 0)
                };

            // Căn giữa theo chiều dọc để thẳng hàng với ô nhập.
            label.Appearance.TextOptions.VAlignment =
                DevExpress.Utils.VertAlignment.Center;

            panel.Controls.Add(
                label,
                column,
                row);
        }

        // ============================================================
        // GRID
        // ============================================================

        private void CauHinhGrid()
        {
            _gridView.Columns.Clear();

            _gridView.OptionsBehavior.Editable = true;

            _gridView.OptionsView.ShowGroupPanel = false;

            _gridView.OptionsView.NewItemRowPosition =
                NewItemRowPosition.None;

            _gridView.OptionsSelection.MultiSelect = false;

            // ========================================================
            // SLOT NGUỒN
            //
            // Khách trả:
            // SlotIdNguon = null
            //
            // Không dùng field này.
            // ========================================================

            var colSlot =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.SlotIdNguon));

            colSlot.Caption =
                "Slot nguồn";

            colSlot.Visible = false;

            // ========================================================
            // MÃ HÀNG
            // ========================================================

            var colMaHang =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.MaHang));

            colMaHang.Caption =
                "Mã hàng";

            colMaHang.Visible = true;
            colMaHang.VisibleIndex = 0;
            colMaHang.Width = 170;

            // Chọn từ danh mục mã hàng chung (gõ để lọc), không nhập tay tự do.
            colMaHang.ColumnEdit = TaoEditorMaHang();

            // ========================================================
            // TÊN HÀNG
            // ========================================================

            var colTenHang =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.TenHang));

            colTenHang.Caption =
                "Tên hàng";

            colTenHang.Visible = true;
            colTenHang.VisibleIndex = 1;
            colTenHang.Width = 220;

            // Tên hàng tự điền theo Mã hàng đã chọn.
            colTenHang.OptionsColumn.AllowEdit = false;

            // ========================================================
            // LOT
            // ========================================================

            var colLot =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.LotNo));

            colLot.Caption =
                "Lot No (nếu có)";

            colLot.Visible = true;
            colLot.VisibleIndex = 2;
            colLot.Width = 140;

            // ========================================================
            // SỐ LƯỢNG
            // ========================================================

            var colSoLuong =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.SoLuong));

            colSoLuong.Caption =
                "Số lượng";

            colSoLuong.Visible = true;
            colSoLuong.VisibleIndex = 4;
            colSoLuong.Width = 90;

            colSoLuong.DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.Numeric;

            colSoLuong.DisplayFormat.FormatString =
                "n0";

            // ========================================================
            // LÝ DO NG
            // ========================================================

            var colLyDo =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.LyDoNg));

            colLyDo.Caption =
                "Lý do NG";

            colLyDo.Visible = true;
            colLyDo.VisibleIndex = 8;
            colLyDo.Width = 250;

            // ========================================================
            // ĐỐI CHIẾU PHIẾU GIAO
            //
            // Ban đầu chưa có.
            // Service sẽ xử lý sau.
            // ========================================================

            // Phiếu giao gốc đã đối chiếu: khóa định danh (ẩn) + PO / Nhà máy / SL đã giao (hiện, chỉ đọc).
            var colDinhDanh =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.DinhDanhPhieuGiao));

            colDinhDanh.Caption =
                "Phiếu giao gốc";

            colDinhDanh.Visible = false;

            var colPo =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.PoNo));

            colPo.Caption =
                "PO";

            colPo.Visible = true;
            colPo.VisibleIndex = 6;
            colPo.Width = 110;
            colPo.OptionsColumn.AllowEdit = false;

            // Khách có thể chỉ báo "mã hàng + ngày giao" (không có LOT):
            // dùng để khoanh vùng và đối chiếu phiếu giao gốc ở bước sau.
            var colNgayGiao =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.NgayGiao));

            colNgayGiao.Caption =
                "Ngày giao (khách báo)";

            colNgayGiao.Visible = true;
            colNgayGiao.VisibleIndex = 3;
            colNgayGiao.Width = 130;
            colNgayGiao.ColumnEdit = TaoEditorNgay();

            var colNhaMay =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.NhaMay));

            colNhaMay.Caption =
                "Nhà máy";

            colNhaMay.Visible = true;
            colNhaMay.VisibleIndex = 7;
            colNhaMay.Width = 170;
            colNhaMay.OptionsColumn.AllowEdit = false;

            var colDaGiao =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.SoLuongDaGiao));

            colDaGiao.Caption =
                "SL đã giao";

            colDaGiao.Visible = true;
            colDaGiao.VisibleIndex = 5;
            colDaGiao.Width = 90;
            colDaGiao.OptionsColumn.AllowEdit = false;
            colDaGiao.DisplayFormat.FormatType =
                DevExpress.Utils.FormatType.Numeric;
            colDaGiao.DisplayFormat.FormatString =
                "n0";

            var colLuongDi =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.LuongDiTomTat));

            colLuongDi.Caption =
                "Luồng đi (nguồn gốc)";

            colLuongDi.Visible = true;
            colLuongDi.VisibleIndex = 8;
            colLuongDi.Width = 380;
            colLuongDi.OptionsColumn.AllowEdit = false;

            _gridView.ShowingEditor += GridView_ShowingEditor;
            _gridView.RowCellStyle += GridView_RowCellStyle;
            _gridView.ValidatingEditor += GridView_ValidatingEditor;
            _gridView.CellValueChanged += GridView_CellValueChanged;
        }

        // ============================================================
        // DANH MỤC MÃ HÀNG + EDITOR TRONG LƯỚI
        // ============================================================

        private void NapDanhMucMaHang()
        {
            _itemByCode.Clear();
            _catalogLoadError = null;

            try
            {
                _itemCatalog = (_lookupRepo != null
                    ? _lookupRepo.GetItemCatalog()
                    : null) ?? new List<ItemLookupItem>();
            }
            catch (Exception ex)
            {
                _itemCatalog = new List<ItemLookupItem>();
                _catalogLoadError = ex.Message;
            }

            foreach (var item in _itemCatalog)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemCode))
                    continue;

                item.ItemCode = item.ItemCode.Trim();
                item.ItemName = (item.ItemName ?? string.Empty).Trim();
                _itemByCode[item.ItemCode] = item;
            }
        }

        private ItemLookupItem TimItem(string maHang)
        {
            if (string.IsNullOrWhiteSpace(maHang))
                return null;

            ItemLookupItem item;
            return _itemByCode.TryGetValue(maHang.Trim(), out item) ? item : null;
        }

        private DataTable TaoBangGoiYMaHang()
        {
            var table = new DataTable();
            table.Columns.Add("ItemCode", typeof(string));
            table.Columns.Add("ItemName", typeof(string));

            foreach (var item in _itemCatalog)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemCode))
                    continue;

                table.Rows.Add(item.ItemCode, item.ItemName ?? string.Empty);
            }

            return table;
        }

        private RepositoryItem TaoEditorMaHang()
        {
            // Không có danh mục (chưa cấu hình repo / lỗi DB) → ô nhập thường, vẫn lập được phiếu.
            if (_itemByCode.Count == 0)
                return new RepositoryItemTextEdit();

            var repo = new RepositoryItemGridLookUpEdit
            {
                DataSource = _itemCatalog,
                ValueMember = nameof(ItemLookupItem.ItemCode),
                DisplayMember = nameof(ItemLookupItem.ItemCode),
                NullText = string.Empty,
                TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard,
                PopupFilterMode = DevExpress.XtraEditors.PopupFilterMode.Contains,
                ImmediatePopup = true,
                PopupFormMinSize = new System.Drawing.Size(520, 300)
            };

            repo.View.OptionsView.ShowGroupPanel = false;
            repo.View.OptionsView.ShowAutoFilterRow = true;
            repo.View.OptionsBehavior.Editable = false;
            repo.View.Columns.Clear();
            repo.View.Columns.AddVisible(nameof(ItemLookupItem.ItemCode), "Mã hàng");
            repo.View.Columns.AddVisible(nameof(ItemLookupItem.ItemName), "Tên hàng");

            _grid.RepositoryItems.Add(repo);

            return repo;
        }

        private RepositoryItem TaoEditorNgay()
        {
            var repo = new RepositoryItemDateEdit
            {
                NullText = string.Empty
            };

            repo.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            repo.DisplayFormat.FormatString = "dd/MM/yyyy";
            repo.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            repo.EditFormat.FormatString = "dd/MM/yyyy";
            repo.Mask.EditMask = "dd/MM/yyyy";
            repo.Mask.UseMaskAsDisplayFormat = true;

            _grid.RepositoryItems.Add(repo);

            return repo;
        }

        // Chặn mã hàng không có trong danh mục (gõ sai) ngay khi rời ô.
        private void GridView_ValidatingEditor(
            object sender,
            DevExpress.XtraEditors.Controls.BaseContainerValidateEditorEventArgs e)
        {
            var view = sender as GridView;

            if (view == null ||
                view.FocusedColumn == null ||
                view.FocusedColumn.FieldName != nameof(PhieuTraHangCT.MaHang))
                return;

            if (_itemByCode.Count == 0)
                return;

            string typed = Convert.ToString(e.Value)?.Trim();

            if (string.IsNullOrEmpty(typed) && view.ActiveEditor != null)
                typed = view.ActiveEditor.Text?.Trim();

            if (string.IsNullOrEmpty(typed))
                return; // xóa ô: ValidateForm sẽ báo thiếu khi lưu

            var item = TimItem(typed);

            if (item == null)
            {
                e.Valid = false;
                e.ErrorText = $"Mã hàng '{typed}' không có trong danh mục. Hãy chọn từ danh sách.";
                return;
            }

            e.Value = item.ItemCode; // chuẩn hóa đúng mã của danh mục
        }

        private void GridView_CellValueChanged(
            object sender,
            DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            if (e.Column.FieldName != nameof(PhieuTraHangCT.MaHang))
                return;

            var row = _gridView.GetRow(e.RowHandle) as PhieuTraHangCT;

            if (row == null)
                return;

            var item = TimItem(row.MaHang);
            row.TenHang = item != null ? item.ItemName : string.Empty;

            _gridView.RefreshRow(e.RowHandle);
        }

        // Dòng đã gắn phiếu giao gốc: khóa Mã hàng / Lot / Ngày giao để không lệch với lần giao đã đối chiếu.
        // Muốn đổi → bấm "Đối chiếu phiếu giao" lại (chọn lần giao khác) hoặc xóa dòng.
        private void GridView_ShowingEditor(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuTraHangCT;
            var field = _gridView.FocusedColumn?.FieldName;

            if (row == null || string.IsNullOrWhiteSpace(row.DinhDanhPhieuGiao))
                return;

            if (field == nameof(PhieuTraHangCT.MaHang) ||
                field == nameof(PhieuTraHangCT.LotNo) ||
                field == nameof(PhieuTraHangCT.NgayGiao))
                e.Cancel = true;
        }

        // SL khách trả lớn hơn SL đã giao → tô đỏ (cho phép, chỉ cảnh báo).
        private void GridView_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            if (e.Column.FieldName != nameof(PhieuTraHangCT.SoLuong))
                return;

            var row = _gridView.GetRow(e.RowHandle) as PhieuTraHangCT;

            if (row != null &&
                row.SoLuongDaGiao.HasValue &&
                row.SoLuong > row.SoLuongDaGiao.Value)
                e.Appearance.ForeColor = System.Drawing.Color.Firebrick;
        }

        // ============================================================
        // ĐỐI CHIẾU PHIẾU GIAO GỐC
        //
        // Khách trả → phải biết hàng đó đã giao lần nào (ngày/PO/nhà máy) và giao bao nhiêu.
        // Cùng mã/LOT có thể giao nhiều lần nên chọn theo khoảng thời gian giao; một dòng khách
        // trả có thể tách thành nhiều dòng, mỗi dòng gắn một lần giao với SL trả riêng.
        // ============================================================

        private void DoiChieuPhieuGiao()
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            var row = _gridView.GetFocusedRow() as PhieuTraHangCT;

            if (row == null)
            {
                XtraMessageBox.Show(
                    "Chọn dòng hàng cần đối chiếu phiếu giao.",
                    "Chưa chọn dòng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.MaHang))
            {
                XtraMessageBox.Show(
                    "Dòng chưa có Mã hàng. Hãy chọn mã hàng trước khi đối chiếu.",
                    "Thiếu dữ liệu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string customerNo = (_cboNguonKhachTra.EditValue as string ?? string.Empty).Trim();

            if (customerNo.Length == 0)
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn khách hàng trả trước khi đối chiếu phiếu giao.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var f = new FormDoiChieuPhieuGiao(
                    _khachTraHangService,
                    customerNo,
                    row.MaHang,
                    row.TenHang,
                    row.LotNo,
                    row.NgayGiao,
                    row.SoLuong))
                {
                    if (f.ShowDialog(this) != DialogResult.OK)
                        return;

                    GanPhieuGiaoVaoDong(row, f.LotNo, f.KetQua, f.LuongDiTomTat);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể đối chiếu phiếu giao.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Xem luồng đi của LOT (SX → nhập/xuất kho → giao khách) của dòng đang chọn.
        // Dùng chung FormDoiChieuPhieuGiao với bước đối chiếu; truy vấn trực tiếp nên phiếu đã lưu
        // mở lại vẫn xem được, và chế độ chỉ xem không sửa dữ liệu kho.
        private void XemLuongDi()
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            var row = _gridView.GetFocusedRow() as PhieuTraHangCT;

            if (row == null)
            {
                XtraMessageBox.Show(
                    "Chọn dòng hàng cần xem luồng đi.",
                    "Chưa chọn dòng",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var f = FormDoiChieuPhieuGiao.ChoXemLuongDi(row.MaHang, row.TenHang, row.LotNo))
                {
                    if (f.ShowDialog(this) != DialogResult.OK
                        || string.IsNullOrWhiteSpace(f.LuongDiTomTat))
                        return;

                    row.LuongDiTomTat = f.LuongDiTomTat;
                    _grid.RefreshDataSource();
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể xem luồng đi.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Lần giao đầu tiên gán vào dòng đang chọn; các lần giao còn lại thành dòng mới ngay bên dưới
        // (cùng mã/LOT/lý do NG). Trùng (mã + LOT + lần giao) với dòng có sẵn → cộng dồn SL.
        private void GanPhieuGiaoVaoDong(PhieuTraHangCT row, string lotNo, List<PhieuGiaoChon> chon, string luongDiTomTat = null)
        {
            if (chon == null || chon.Count == 0)
                return;

            var data = LayDanhSachDong();
            string lot = (lotNo ?? string.Empty).Trim();
            int viTriChen = data.IndexOf(row) + 1;

            for (int i = 0; i < chon.Count; i++)
            {
                var c = chon[i];
                var slip = c.Phieu;

                var trung = data.FirstOrDefault(d =>
                    !ReferenceEquals(d, row)
                    && string.Equals(d.DinhDanhPhieuGiao, slip.DinhDanhKey, StringComparison.Ordinal)
                    && string.Equals(d.MaHang?.Trim(), row.MaHang?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals((d.LotNo ?? string.Empty).Trim(), lot, StringComparison.OrdinalIgnoreCase));

                if (trung != null)
                {
                    trung.SoLuong += c.SoLuongTra;
                    continue;
                }

                PhieuTraHangCT dich;

                if (i == 0)
                {
                    dich = row;
                }
                else
                {
                    dich = new PhieuTraHangCT
                    {
                        SlotIdNguon = null,
                        MaHang = row.MaHang,
                        TenHang = row.TenHang,
                        LyDoNg = row.LyDoNg
                    };

                    data.Insert(viTriChen++, dich);
                }

                dich.LotNo = lot;
                dich.SoLuong = c.SoLuongTra;
                dich.SoLuongDaGiao = c.SoLuongDaGiao;
                dich.DinhDanhPhieuGiao = slip.DinhDanhKey;
                dich.PoNo = slip.PO_NO;
                dich.NgayGiao = slip.NGAYGIAO;
                dich.NhaMay = slip.NHAMAY;

                if (!string.IsNullOrWhiteSpace(luongDiTomTat))
                    dich.LuongDiTomTat = luongDiTomTat;
            }

            _grid.RefreshDataSource();

            int tong = chon.Sum(x => x.SoLuongTra);
            BaoQr($"Đã gắn {chon.Count} lần giao cho {row.MaHang} (tổng SL trả {tong:n0}).", true);
        }

        // Phiếu mở lại: điền SL đã giao cho từng dòng đã gắn phiếu giao.
        private void NapSoLuongDaGiao(List<PhieuTraHangCT> items)
        {
            if (items == null)
                return;

            foreach (var ct in items)
            {
                if (string.IsNullOrWhiteSpace(ct.DinhDanhPhieuGiao))
                    continue;

                try
                {
                    ct.SoLuongDaGiao = _khachTraHangService.LaySoLuongDaGiao(
                        ct.DinhDanhPhieuGiao, ct.LotNo);
                }
                catch
                {
                    ct.SoLuongDaGiao = null; // không tra được: chỉ mất cột đối chiếu, vẫn xem được phiếu
                }
            }
        }

        // Ô nhập căn giữa theo chiều dọc ô (thay Dock Fill) để thẳng hàng với nhãn.
        private static void CanGiuaOCuaHeader(TableLayoutPanel header)
        {
            foreach (Control c in header.Controls)
            {
                if (c is LabelControl)
                    continue;

                c.Dock = DockStyle.None;
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                c.Margin = new Padding(3, 3, 8, 3);
            }
        }

        // ============================================================
        // TRA CỨU / QUÉT TEM  (StockLookupControl dùng chung)
        //
        // Tab "Tìm theo mã"      : chọn mã hàng từ danh mục (vB20Item) → thêm dòng khoanh vùng.
        // Tab "Tìm theo LOT / QR": quét tem QR (Lot:Mã hàng:Ngày:SL:...) → thêm dòng đầy đủ;
        //                          hoặc nhập LOT thô → tự suy ra mã hàng (kho + lịch sử).
        //
        // Không yêu cầu LOT còn tồn kho: hàng khách trả có thể đã xuất hết.
        // ============================================================

        private TableLayoutPanel TaoTraCuuPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 600));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _stockLookup = new StockLookupControl { Dock = DockStyle.Fill };
            _stockLookup.SetItemSuggestions(TaoBangGoiYMaHang(), "ItemCode", "ItemName");
            _stockLookup.SetLotQrPlaceholder("Quét tem QR (4/6 trường) hoặc nhập LOT rồi Enter...");
            _stockLookup.SearchRequested += (s, e) => XuLyTraCuu();
            _stockLookup.SelectionCleared += (s, e) =>
            {
                if (_lblQrStatus != null)
                    _lblQrStatus.Text = string.Empty;
            };

            _lblQrStatus = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(8, 0, 0, 0)
            };
            _lblQrStatus.Appearance.TextOptions.VAlignment =
                DevExpress.Utils.VertAlignment.Center;

            panel.Controls.Add(_stockLookup, 0, 0);
            panel.Controls.Add(_lblQrStatus, 1, 0);

            if (!string.IsNullOrEmpty(_catalogLoadError))
                BaoQr("Không tải được danh mục mã hàng: " + _catalogLoadError, false);
            else if (_itemByCode.Count == 0)
                BaoQr("Chưa có danh mục mã hàng: nhập mã hàng thủ công trong lưới.", false);

            return panel;
        }

        private void BaoQr(string text, bool ok)
        {
            _lblQrStatus.Text = text;
            _lblQrStatus.Appearance.ForeColor =
                ok ? System.Drawing.Color.ForestGreen : System.Drawing.Color.Firebrick;
        }

        private List<PhieuTraHangCT> LayDanhSachDong()
        {
            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
            {
                data = new List<PhieuTraHangCT>();
                _grid.DataSource = data;
            }

            return data;
        }

        /// <summary>
        /// Thêm dòng (hoặc cộng dồn SL nếu đã có cùng Mã hàng + LOT).
        /// soLuong = 0 → dòng khoanh vùng, người dùng nhập SL sau.
        /// </summary>
        private PhieuTraHangCT ThemHoacCongDon(string maHang, string tenHang, string lot, int soLuong)
        {
            var data = LayDanhSachDong();

            string ma = (maHang ?? string.Empty).Trim();
            string lotChuan = (lot ?? string.Empty).Trim();

            var exist = data.FirstOrDefault(d =>
                string.Equals((d.MaHang ?? string.Empty).Trim(), ma, StringComparison.OrdinalIgnoreCase)
                && string.Equals((d.LotNo ?? string.Empty).Trim(), lotChuan, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(d.DinhDanhPhieuGiao)); // dòng đã gắn lần giao thì không cộng dồn thêm

            if (exist != null)
            {
                if (soLuong > 0)
                    exist.SoLuong += soLuong;

                return exist;
            }

            var item = new PhieuTraHangCT
            {
                SlotIdNguon = null,
                MaHang = ma,
                TenHang = tenHang ?? string.Empty,
                LotNo = lotChuan,
                SoLuong = soLuong,
                LyDoNg = string.Empty
            };

            data.Add(item);

            return item;
        }

        private void FocusO(PhieuTraHangCT item, string fieldName)
        {
            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null || item == null)
                return;

            _grid.RefreshDataSource();

            int rowHandle = _gridView.GetRowHandle(data.IndexOf(item));

            if (rowHandle < 0)
                return;

            _gridView.FocusedRowHandle = rowHandle;
            _gridView.FocusedColumn = _gridView.Columns[fieldName];
            _gridView.ShowEditor();
        }

        private void XuLyTraCuu()
        {
            string input = _stockLookup.InputText;

            if (string.IsNullOrWhiteSpace(input))
            {
                BaoQr("Chọn mã hàng, nhập LOT hoặc quét tem QR trước khi tra cứu.", false);
                _stockLookup.FocusInput();
                return;
            }

            if (_stockLookup.IsItemSearch)
            {
                XuLyChonMaHang(input);
                return;
            }

            if (input.IndexOf(':') >= 0)
                XuLyQr(input);
            else
                XuLyLotThuan(input);

            _stockLookup.ClearInput();
            _stockLookup.FocusInput();
        }

        // Tab "Tìm theo mã": khách chỉ báo mã hàng (kèm ngày giao) → dòng khoanh vùng.
        private void XuLyChonMaHang(string input)
        {
            string code = _stockLookup.SelectedItemCode;

            if (string.IsNullOrWhiteSpace(code))
                code = input;

            var item = TimItem(code);

            if (item == null)
            {
                BaoQr($"Mã hàng '{input}' không có trong danh mục. Hãy chọn từ danh sách gợi ý.", false);
                _stockLookup.FocusInput();
                return;
            }

            // Khách chỉ báo mã hàng (+ ngày giao): đối chiếu phiếu giao TRƯỚC, chỉ thêm dòng khi có phiếu hợp lệ.
            if (MoDoiChieuVaThemDong(item.ItemCode, item.ItemName, string.Empty, null, 0))
                _stockLookup.ClearInput();
        }

        /// <summary>
        /// Cổng duy nhất đưa hàng khách trả vào lưới: chuyển thông tin tra cứu sang form đối chiếu phiếu giao,
        /// CHỈ khi người dùng chọn được lần giao hợp lệ mới thêm dòng (đã gắn phiếu giao). Hủy / không chọn → không thêm gì.
        /// </summary>
        private bool MoDoiChieuVaThemDong(string maHang, string tenHang, string lot, DateTime? ngayGiao, int soLuong)
        {
            string customerNo = (_cboNguonKhachTra.EditValue as string ?? string.Empty).Trim();

            if (customerNo.Length == 0)
            {
                BaoQr("Chọn Khách hàng trả (Nguồn khách trả) trước khi tra cứu phiếu giao.", false);
                _cboNguonKhachTra.Focus();
                return false;
            }

            try
            {
                using (var f = new FormDoiChieuPhieuGiao(
                    _khachTraHangService, customerNo, maHang, tenHang, lot, ngayGiao, soLuong))
                {
                    if (f.ShowDialog(this) != DialogResult.OK || f.KetQua == null || f.KetQua.Count == 0)
                    {
                        BaoQr("Chưa có phiếu giao hợp lệ nên chưa thêm dòng hàng.", false);
                        return false;
                    }

                    var row = new PhieuTraHangCT
                    {
                        SlotIdNguon = null,
                        MaHang = (maHang ?? string.Empty).Trim(),
                        TenHang = tenHang ?? string.Empty,
                        LotNo = (f.LotNo ?? string.Empty).Trim(),
                        SoLuong = 0,
                        LyDoNg = string.Empty
                    };

                    var data = LayDanhSachDong();
                    data.Add(row);

                    GanPhieuGiaoVaoDong(row, f.LotNo, f.KetQua, f.LuongDiTomTat);

                    // Lần giao đã có sẵn trong lưới → số lượng được cộng vào dòng cũ, bỏ dòng trống vừa tạo.
                    if (string.IsNullOrWhiteSpace(row.DinhDanhPhieuGiao))
                        data.Remove(row);

                    _grid.RefreshDataSource();
                    return true;
                }
            }
            catch (Exception ex)
            {
                BaoQr("Không thể đối chiếu phiếu giao: " + ex.Message, false);
                return false;
            }
        }

        // Tab "LOT / QR" – QR đầy đủ (Lot:Mã hàng:Ngày:SL:...).
        private void XuLyQr(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            QrItemInfo info;
            string error;

            if (!QrItemParser.TryParse(raw, out info, out error))
            {
                BaoQr(error, false);
                return;
            }

            // Mã hàng phải có trong danh mục (vB20Item) – KHÔNG cần còn tồn kho.
            string tenHang;
            string maHang = ChuanHoaMaHang(info.ItemCode, out tenHang);

            if (maHang == null)
            {
                BaoQr($"Mã hàng {info.ItemCode} không có trong danh mục.", false);
                return;
            }

            // Tem QR là thông tin khoanh vùng: phải đối chiếu được phiếu giao mới thêm dòng.
            MoDoiChieuVaThemDong(maHang, tenHang, info.Lot, null, info.Quantity);
        }

        // Tab "LOT / QR" – chỉ có LOT (13/20 ký tự): suy mã hàng từ kho + lịch sử.
        private void XuLyLotThuan(string lotNhap)
        {
            string lot = lotNhap.Trim();

            List<ItemLookupItem> items = null;

            try
            {
                if (_lookupRepo != null)
                    items = _lookupRepo.FindItemsByLot(lot);
            }
            catch (Exception ex)
            {
                BaoQr("Không tra được mã hàng theo LOT: " + ex.Message, false);
            }

            int n = items == null ? 0 : items.Count;

            if (n == 1)
            {
                var it = items[0];
                MoDoiChieuVaThemDong(it.ItemCode, it.ItemName, lot, null, 0);
                return;
            }

            // Không xác định được duy nhất một mã từ LOT (LOT lạ / đã xóa / thuộc nhiều mã):
            // KHÔNG thêm dòng trống. Chọn mã hàng ở tab "Tìm theo mã" rồi nhập LOT trong form đối chiếu.
            BaoQr(
                n == 0
                    ? $"LOT {lot} chưa có trong kho/lịch sử: chọn mã hàng ở tab \"Tìm theo mã\" rồi nhập LOT ở form đối chiếu."
                    : $"LOT {lot} thuộc {n} mã hàng: chọn mã hàng ở tab \"Tìm theo mã\" để đối chiếu đúng mã.",
                false);
        }

        /// <summary>Trả mã chuẩn của danh mục (và tên); null nếu mã không tồn tại.</summary>
        private string ChuanHoaMaHang(string maHang, out string tenHang)
        {
            tenHang = string.Empty;

            if (_itemByCode.Count > 0)
            {
                var item = TimItem(maHang);

                if (item == null)
                    return null;

                tenHang = item.ItemName;
                return item.ItemCode;
            }

            // Chưa tải được danh mục → hỏi thẳng repo (nếu có); không có repo thì chấp nhận mã quét.
            if (_lookupRepo != null)
            {
                string ten = _lookupRepo.GetItemName(maHang);

                if (ten == null)
                    return null;

                tenHang = ten;
            }

            return (maHang ?? string.Empty).Trim();
        }

        // ============================================================
        // MỞ LẠI PHIẾU: nạp dữ liệu + chế độ
        // ============================================================

        private void NapPhieuDaTao()
        {
            _txtSoPhieu.Text = _editPhieu.SoPhieu;
            _txtSoPhieuKhach.Text = _editPhieu.SoPhieuKhach;
            _dtNgayPhatHanh.EditValue = _editPhieu.NgayPhatHanh;
            _txtSlipNo.Text = _editPhieu.SlipNo;
            _txtCa.Text = _editPhieu.Ca;
            _txtLyDo.Text = _editPhieu.LyDo;
            _txtBoPhanPhatHienLoi.Text = _editPhieu.BoPhanPhatHienLoi;
            _txtNguoiTao.Text = _editPhieu.CreatedBy;

            // Nạp nguồn khách rồi mới nạp Tên đã lưu (không để lookup ghi đè).
            _dangNapDuLieu = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(_editPhieu.CustomerNo))
                {
                    string no = _editPhieu.CustomerNo.Trim();

                    // Khách đã bị gỡ khỏi CustomerTableConfig → vẫn hiển thị để xem phiếu cũ.
                    if (!_khachHangs.Any(x => x.CustomerNo == no))
                    {
                        _khachHangs.Add(new KhachHangLookupItem { CustomerNo = no, TenKhachHang = _editPhieu.TenKhachHang });
                        _cboNguonKhachTra.Properties.DataSource = null;
                        _cboNguonKhachTra.Properties.DataSource = _khachHangs;
                    }

                    _cboNguonKhachTra.EditValue = no;
                }

                _txtTenKhachHang.Text = _editPhieu.TenKhachHang;
            }
            finally
            {
                _dangNapDuLieu = false;
            }

            var chiTiet =
                _phieuRepo.GetItems(_phieuId) ?? new List<PhieuTraHangCT>();

            NapSoLuongDaGiao(chiTiet);

            _grid.DataSource = chiTiet;

            _choSuaChiTiet = !_khachTraHangService.CoPhieuXuLy(_phieuId);
        }

        private void ApDungCheDo()
        {
            if (_mode == FormMode.Create)
                return;

            bool daDong = _mode == FormMode.ReadOnly;
            bool suaHeader = !daDong;
            bool suaChiTiet = !daDong && _choSuaChiTiet;

            Text = daDong
                ? $"Phiếu khách trả {_editPhieu.SoPhieu} (đã đóng - chỉ xem)"
                : $"Phiếu khách trả {_editPhieu.SoPhieu}";

            _txtSoPhieu.Properties.ReadOnly = true;
            _cboNguonKhachTra.Properties.ReadOnly = !suaHeader;
            _txtSoPhieuKhach.Properties.ReadOnly = !suaHeader;
            _dtNgayPhatHanh.Properties.ReadOnly = !suaHeader;
            _txtSlipNo.Properties.ReadOnly = !suaHeader;
            _txtCa.Properties.ReadOnly = !suaHeader;
            _txtTenKhachHang.Properties.ReadOnly = !suaHeader;
            _txtLyDo.Properties.ReadOnly = !suaHeader;
            _txtBoPhanPhatHienLoi.Properties.ReadOnly = !suaHeader;
            _txtNguoiTao.Properties.ReadOnly = true;

            _gridView.OptionsBehavior.Editable = suaChiTiet;

            _stockLookup.Enabled = suaChiTiet;
            _btnThem.Visible = suaChiTiet;
            _btnDoiChieu.Visible = suaChiTiet;
            _btnXoa.Visible = suaChiTiet;

            // Đã mở lại phiếu → không còn "Tiếp nhận phiếu", chỉ Lưu thay đổi / Xóa.
            _btnLuu.Text = "💾 Lưu thay đổi";
            _btnLuu.Visible = suaHeader;
            _btnXoaPhieu.Visible = suaChiTiet;
            _btnHuy.Text = "Đóng";

            if (!daDong && !_choSuaChiTiet)
            {
                _lblQrStatus.Text =
                    "Đã có Phiếu xử lý bất thường: chỉ sửa được thông tin chung.";
                _lblQrStatus.Appearance.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else if (daDong)
            {
                _lblQrStatus.Text = "Phiếu đã đóng - chỉ xem.";
                _lblQrStatus.Appearance.ForeColor = System.Drawing.Color.DimGray;
            }
        }

        private void XoaPhieu()
        {
            if (_mode != FormMode.Edit)
                return;

            var confirm = XtraMessageBox.Show(
                $"Xóa phiếu {_editPhieu.SoPhieu}?\r\nThao tác này không thể hoàn tác.",
                "Xác nhận xóa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                _khachTraHangService.XoaPhieu(_phieuId, Environment.UserName);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể xóa phiếu.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // LOOKUP NGUỒN KHÁCH TRẢ  ←  CustomerTableConfig
        // ============================================================

        private static List<KhachHangLookupItem> BuildKhachHangLookup()
        {
            var list = new List<KhachHangLookupItem>();

            foreach (var cfg in CustomerTableConfig.All)
            {
                list.Add(new KhachHangLookupItem
                {
                    CustomerNo = cfg.CustomerNo,
                    TenKhachHang = string.IsNullOrWhiteSpace(cfg.CustomerName)
                        ? cfg.DisplayName
                        : cfg.CustomerName
                });
            }

            return list.OrderBy(x => x.CustomerNo, StringComparer.Ordinal).ToList();
        }

        private void CapNhatTenKhachHangTuNguon()
        {
            if (_dangNapDuLieu)
                return;

            var no = _cboNguonKhachTra.EditValue as string;

            if (string.IsNullOrWhiteSpace(no))
                return;

            var item = _khachHangs.FirstOrDefault(x => x.CustomerNo == no);

            if (item != null)
                _txtTenKhachHang.Text = item.TenKhachHang;
        }

        // ============================================================
        // DEFAULT DATA
        // ============================================================

        private void KhoiTaoDuLieuBanDau()
        {
            _txtSoPhieu.Text =
                TaoSoPhieuTam();

            _dtNgayPhatHanh.EditValue =
                DateTime.Today;

            if (_khachHangs.Count > 0)
            {
                // Kích hoạt EditValueChanged → tự điền Tên khách hàng mặc định.
                _cboNguonKhachTra.EditValue = _khachHangs[0].CustomerNo;
            }

            _grid.DataSource =
                new List<PhieuTraHangCT>();
        }

        private string TaoSoPhieuTam()
        {
            return
                $"KH-{DateTime.Now:yyyyMMddHHmmss}";
        }

        // ============================================================
        // THÊM DÒNG
        // ============================================================

        private void ThemDong()
        {
            var data =
                _grid.DataSource
                as List<PhieuTraHangCT>;

            if (data == null)
            {
                data =
                    new List<PhieuTraHangCT>();

                _grid.DataSource =
                    data;
            }

            var item =
                new PhieuTraHangCT
                {
                    Id = 0,
                    PhieuTraHangId = 0,

                    SlotIdNguon = null,

                    MaHang = "",
                    TenHang = "",
                    LotNo = "",
                    SoLuong = 0,
                    LyDoNg = "",

                    DinhDanhPhieuGiao = null,
                    PoNo = null,
                    NgayGiao = null,
                    NhaMay = null
                };

            data.Add(item);

            _grid.RefreshDataSource();

            int rowHandle =
                _gridView.GetRowHandle(
                    data.Count - 1);

            if (rowHandle >= 0)
            {
                _gridView.FocusedRowHandle =
                    rowHandle;

                // Bắt đầu từ ô Mã hàng (chọn từ danh mục).
                _gridView.FocusedColumn =
                    _gridView.Columns[nameof(PhieuTraHangCT.MaHang)];

                _gridView.ShowEditor();
            }
        }

        // ============================================================
        // TRA SLOT THEO LOT (hàng khách trả còn tồn ở đâu trong nhà máy)
        //
        // Khách báo lỗi 1 mã hàng thì luôn kèm LotNo lỗi → liệt kê mọi Slot còn tồn
        // đúng Mã hàng + LOT đó để có hướng xử lý. CHỈ TRA CỨU: không thêm dòng vào phiếu
        // (dòng khách trả là hàng khách gửi về, không có Slot nguồn).
        // ============================================================

        private void TraSlotTheoLot()
        {
            if (_slotService == null)
                return;

            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            var data = _grid.DataSource as List<PhieuTraHangCT>;

            var keys = (data ?? new List<PhieuTraHangCT>())
                .Where(x => !string.IsNullOrWhiteSpace(x.LotNo))
                .Select(x => new KeyValuePair<string, string>(x.MaHang, x.LotNo))
                .ToList();

            if (keys.Count == 0)
            {
                XtraMessageBox.Show(
                    "Chưa có dòng nào có Lot No để tra cứu.\r\nHãy nhập / quét Lot No ở các dòng hàng trước.",
                    "Thiếu dữ liệu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            try
            {
                using (var f = FormChonSlotNoiBo.TraCuuTheoLot(_slotService, keys))
                    f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không thể tra cứu Slot.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // XÓA DÒNG
        // ============================================================

        private void XoaDong()
        {
            int rowHandle =
                _gridView.FocusedRowHandle;

            if (rowHandle < 0)
                return;

            var item =
                _gridView.GetRow(rowHandle)
                as PhieuTraHangCT;

            if (item == null)
                return;

            var data =
                _grid.DataSource
                as List<PhieuTraHangCT>;

            if (data == null)
                return;

            data.Remove(item);

            _grid.RefreshDataSource();
        }

        // ============================================================
        // VALIDATE
        // ============================================================

        private bool ValidateForm(
            List<PhieuTraHangCT> items)
        {
            // --------------------------------------------------------
            // Nguồn khách trả
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(_cboNguonKhachTra.EditValue as string))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn khách hàng trả.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _cboNguonKhachTra.Focus();

                return false;
            }

            // --------------------------------------------------------
            // Số phiếu khách
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                _txtSoPhieuKhach.Text))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Số phiếu/chứng từ khách.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtSoPhieuKhach.Focus();

                return false;
            }

            // --------------------------------------------------------
            // Khách hàng
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                _txtTenKhachHang.Text))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Tên khách hàng.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtTenKhachHang.Focus();

                return false;
            }

            // --------------------------------------------------------
            // Bộ phận phát hiện lỗi
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                _txtBoPhanPhatHienLoi.Text))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Bộ phận phát hiện lỗi.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtBoPhanPhatHienLoi.Focus();

                return false;
            }

            // --------------------------------------------------------
            // Detail
            // --------------------------------------------------------

            if (items == null ||
                items.Count == 0)
            {
                XtraMessageBox.Show(
                    "Phiếu khách trả phải có ít nhất một dòng hàng.",
                    "Thiếu dữ liệu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            for (int i = 0;
                 i < items.Count;
                 i++)
            {
                var item = items[i];

                if (string.IsNullOrWhiteSpace(
                    item.MaHang))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa nhập Mã hàng.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                // Lot No KHÔNG bắt buộc: khách có thể chỉ báo mã hàng / ngày giao,
                // hoặc LOT đã không còn trong kho. Dòng không LOT khoanh vùng theo
                // toàn bộ tồn của mã (AffectedLotTraceService.Trace).
                item.LotNo = (item.LotNo ?? string.Empty).Trim();

                // Khách trả BẮT BUỘC có thông tin hàng đã giao (lần giao gốc).
                // Phiếu cũ (đang sửa) chưa từng gắn phiếu giao thì không chặn việc sửa.
                if (_mode == FormMode.Create &&
                    string.IsNullOrWhiteSpace(item.DinhDanhPhieuGiao))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1} ({item.MaHang}): chưa đối chiếu phiếu giao gốc.\r\n\r\n" +
                        "Chọn dòng rồi bấm \"Đối chiếu phiếu giao\" để chọn lần giao " +
                        "(theo mã hàng / LOT / khoảng thời gian giao).",
                        "Thiếu thông tin hàng đã giao",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    _gridView.FocusedRowHandle = _gridView.GetRowHandle(i);

                    return false;
                }

                if (_itemByCode.Count > 0 && TimItem(item.MaHang) == null)
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: mã hàng '{item.MaHang}' không có trong danh mục.\r\n" +
                        "Hãy chọn lại mã hàng từ danh sách.",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (item.SoLuong <= 0)
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: Số lượng phải lớn hơn 0.",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }
            }

            return true;
        }

        // ============================================================
        // BUILD PHIEU
        // ============================================================

        private PhieuTraHang BuildPhieu(
            List<PhieuTraHangCT> items)
        {
            string customerNo =
                (_cboNguonKhachTra.EditValue as string ?? string.Empty).Trim();

            string nguoiTao =
                _txtNguoiTao.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                nguoiTao))
            {
                nguoiTao =
                    Environment.UserName;
            }

            var phieu =
                new PhieuTraHang
                {
                    // =================================================
                    // NGUỒN
                    // =================================================

                    Nguon =
                        NguonXuLyBatThuong.KhachTra,

                    CustomerNo =
                        customerNo,

                    // =================================================
                    // CHỨNG TỪ KHÁCH
                    // =================================================

                    SoPhieu =
                        _txtSoPhieu.Text.Trim(),

                    SoPhieuKhach =
                        _txtSoPhieuKhach.Text.Trim(),

                    NgayPhatHanh =
                        _dtNgayPhatHanh.EditValue
                        as DateTime?,

                    SlipNo =
                        _txtSlipNo.Text.Trim(),

                    Ca =
                        _txtCa.Text.Trim(),

                    // =================================================
                    // KHÁCH HÀNG
                    // =================================================

                    TenKhachHang =
                        _txtTenKhachHang.Text.Trim(),

                    // =================================================
                    // NGHIỆP VỤ
                    // =================================================

                    PhongBan = null,

                    LyDo =
                        _txtLyDo.Text.Trim(),

                    BoPhanPhatHienLoi =
                        _txtBoPhanPhatHienLoi.Text.Trim(),

                    // =================================================
                    // XÁC NHẬN
                    // =================================================

                    XacNhanBPPhatHienLoi = null,
                    XacNhanQCKhach = null,
                    XacNhanNhaCungCap = null,

                    // =================================================
                    // NHẬN KHO
                    // =================================================

                    NgayNhanKho = null,

                    TongSoLuongNhan =
                        items.Sum(x => x.SoLuong),

                    // =================================================
                    // STATE
                    // =================================================

                    Status =
                        PhieuTraHangStatus.Moi,

                    // =================================================
                    // GIAO LẠI BỘ PHẬN
                    //
                    // Chỉ TraNoiBo dùng.
                    // =================================================

                    BoPhanNhanLai = null,
                    SoLuongGiaoLai = null,
                    NgayGiaoLaiBoPhan = null,
                    NguoiGiaoLaiBoPhan = null,

                    // =================================================
                    // NOTE
                    // =================================================

                    Note = null,

                    // =================================================
                    // AUDIT
                    // =================================================

                    CreatedAt =
                        DateTime.Now,

                    CreatedBy =
                        nguoiTao,

                    UpdatedAt = null,
                    UpdatedBy = null,

                    // =================================================
                    // DETAIL
                    // =================================================

                    ChiTiet = items
                };

            return phieu;
        }

        private void LuuThayDoi(List<PhieuTraHangCT> items)
        {
            _editPhieu.CustomerNo = (_cboNguonKhachTra.EditValue as string ?? string.Empty).Trim();
            _editPhieu.SoPhieuKhach = _txtSoPhieuKhach.Text.Trim();
            _editPhieu.NgayPhatHanh = _dtNgayPhatHanh.EditValue as DateTime?;
            _editPhieu.SlipNo = _txtSlipNo.Text.Trim();
            _editPhieu.Ca = _txtCa.Text.Trim();
            _editPhieu.TenKhachHang = _txtTenKhachHang.Text.Trim();
            _editPhieu.LyDo = _txtLyDo.Text.Trim();
            _editPhieu.BoPhanPhatHienLoi = _txtBoPhanPhatHienLoi.Text.Trim();

            if (_choSuaChiTiet)
            {
                foreach (var item in items)
                    item.PhieuTraHangId = _phieuId;

                _editPhieu.ChiTiet = items;
            }
            else
            {
                _editPhieu.ChiTiet = null; // chỉ cập nhật header
            }

            _khachTraHangService.CapNhatPhieu(_editPhieu, Environment.UserName);

            XtraMessageBox.Show(
                "Đã lưu thay đổi phiếu.",
                "Thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }

        // ============================================================
        // LƯU
        // ============================================================

        private void LuuPhieu()
        {
            try
            {
                // DevExpress commit dòng đang edit
                _gridView.CloseEditor();
                _gridView.UpdateCurrentRow();

                var items =
                    _grid.DataSource
                    as List<PhieuTraHangCT>;

                if (items == null)
                {
                    items =
                        new List<PhieuTraHangCT>();
                }

                if (!ValidateForm(items))
                    return;

                // SL trả được phép khác SL đã giao (trả một phần / gộp nhiều lần giao) → chỉ cảnh báo.
                var vuot = items
                    .Where(x => x.SoLuongDaGiao.HasValue && x.SoLuong > x.SoLuongDaGiao.Value)
                    .ToList();

                if (vuot.Count > 0 &&
                    XtraMessageBox.Show(
                        $"{vuot.Count} dòng có SL khách trả LỚN HƠN SL đã giao của lần giao gốc.\r\n" +
                        "Vẫn tiếp tục lưu phiếu?",
                        "SL trả lớn hơn SL đã giao",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                if (_mode == FormMode.Edit)
                {
                    LuuThayDoi(items);
                    return;
                }

                var phieu =
                    BuildPhieu(items);

                // Bước 1 của quy trình khách trả: hàng khách trả về thật (có LOT) được NHẬP KHO NG (có StockHistory).
                // Nhưng phiếu khách trả còn có thể chỉ là KHOANH VÙNG (khách chỉ báo mã hàng / ngày giao,
                // hoặc hàng chưa về / LOT đã hết kho) → luôn cho lập phiếu mà không nhập kho NG.
                // Chọn Slot NG TRƯỚC khi tạo phiếu để hủy là không tạo gì.
                int soDongCoLot = items.Count(x => !string.IsNullOrWhiteSpace(x.LotNo));
                int soDongKhongLot = items.Count - soDongCoLot;

                int? slotNG = null;
                bool chuaNhapKhoNG = true;

                if (_reworkStockService != null && _slotService != null && soDongCoLot > 0)
                {
                    var chon = XtraMessageBox.Show(
                        $"Có {soDongCoLot} dòng có LOT.\r\n\r\n" +
                        "• Có: hàng đã được trả về kho → chọn Slot và NHẬP KHO NG.\r\n" +
                        "• Không: chỉ lập phiếu khoanh vùng (hàng chưa về / chưa nhận / LOT đã hết kho), KHÔNG nhập kho.\r\n" +
                        "• Hủy: quay lại chỉnh sửa." +
                        (soDongKhongLot > 0
                            ? $"\r\n\r\n{soDongKhongLot} dòng không có LOT luôn chỉ khoanh vùng theo toàn bộ tồn của mã (không nhập kho)."
                            : string.Empty),
                        "Nhập kho NG hàng khách trả",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (chon == DialogResult.Cancel)
                        return;

                    if (chon == DialogResult.Yes)
                    {
                        slotNG = SlotNhapNGPicker.Chon(this, _slotService, "Nhập kho NG hàng khách trả");
                        if (!slotNG.HasValue)
                            return;

                        chuaNhapKhoNG = false;
                    }
                }

                // ====================================================
                // SERVICE DUY NHẤT XỬ LÝ NGHIỆP VỤ
                // ====================================================

                int id =
                    _khachTraHangService
                        .TiepNhanPhieuKhachTra(phieu);

                if (slotNG.HasValue)
                {
                    // Chỉ dòng có LOT mới tạo được SlotLot NG; dòng khoanh vùng bỏ qua.
                    var lines = phieu.ChiTiet
                        .Where(ct => !string.IsNullOrWhiteSpace(ct.LotNo))
                        .Select(ct => new KhachTraNhapNGLine
                        {
                            PhieuTraHangCTId = ct.Id,
                            MaHang = ct.MaHang,
                            LotNo = ct.LotNo,
                            SoLuong = ct.SoLuong
                        })
                        .ToList();

                    var ng = _reworkStockService.NhapKhoNGKhachTra(id, slotNG.Value, lines, Environment.UserName);
                    if (!ng.IsOK)
                    {
                        // Nhập kho thất bại (đã rollback) → gỡ phiếu vừa tạo để không còn phiếu "mồ côi" chưa có tồn NG.
                        string xoaLoi = null;
                        try { _khachTraHangService.XoaPhieu(id, Environment.UserName); }
                        catch (Exception exXoa) { xoaLoi = exXoa.Message; }

                        XtraMessageBox.Show(
                            "Không thể nhập kho NG nên chưa tiếp nhận phiếu.\r\n\r\n" + ng.Message +
                            (xoaLoi == null
                                ? string.Empty
                                : "\r\n\r\nPhiếu Id=" + id + " đã được tạo nhưng chưa gỡ được: " + xoaLoi +
                                  "\r\nHãy xóa phiếu này thủ công rồi tiếp nhận lại."),
                            "Lỗi",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }

                CreatedPhieuId = id;

                XtraMessageBox.Show(
                    $"Đã tiếp nhận phiếu khách trả thành công.\r\n\r\n" +
                    $"Số phiếu: {phieu.SoPhieu}\r\n" +
                    $"ID: {id}\r\n" +
                    $"Số dòng: {phieu.ChiTiet.Count}\r\n" +
                    $"Tổng số lượng: {phieu.TongSoLuongNhan}" +
                    (chuaNhapKhoNG
                        ? "\r\n\r\nLưu ý: chưa nhập kho NG (phiếu khoanh vùng / hàng chưa về)."
                        : string.Empty),
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult =
                    DialogResult.OK;

                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể tiếp nhận phiếu khách trả.\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
