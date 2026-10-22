using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using DevExpress.XtraEditors.Repository;
    using DevExpress.XtraGrid.Views.Base;
    using PCTP.Modules.KhoVatLy;
    using PCTP.Modules.KhoVatLy.Application.Interfaces;
    using PCTP.Modules.XuLyHangLoi.Repository;
    using PCTP.Modules.XuLyHangLoi.Enums;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Modules.XuLyHangLoi.Services;
    using PCTP.Shared.Enums;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;

    public partial class FormTaoPhieuTraNoiBo : XtraForm
    {
        private readonly ITraNoiBoService _traNoiBoService;
        private readonly INoiBoLookupRepository _lookupRepo;
        private readonly ISlotService _slotService;

        // Dữ liệu lookup (load 1 lần khi mở form)
        private List<DeptLookupItem> _depts = new List<DeptLookupItem>();
        private List<ItemLookupItem> _items = new List<ItemLookupItem>();
        private List<SlotNguonLookupItem> _slotItems = new List<SlotNguonLookupItem>();
        private bool _updatingRow;

        private enum FormMode { Create, Edit, ReadOnly }

        private readonly FormMode _mode = FormMode.Create;
        private readonly IPhieuTraHangRepository _phieuRepo;
        private readonly int _phieuId;
        private PhieuTraHang _editPhieu;

        // Edit: đã có PhieuXuLyBatThuong → chỉ được sửa thông tin chung.
        private bool _choSuaChiTiet = true;

        // Số lượng gốc của dòng đã lưu (cho phép giữ nguyên dù tồn Slot đã đổi).
        private readonly Dictionary<PhieuTraHangCT, int> _soLuongGoc =
            new Dictionary<PhieuTraHangCT, int>();

        private TextEdit _txtQr;
        private LabelControl _lblQrStatus;
        private LabelControl _lblQr;
        private SimpleButton _btnXoaPhieu;
        private SimpleButton _btnChonSlot;

        // ============================================================
        // HEADER
        // ============================================================

        private TextEdit _txtSoPhieu;
        private SearchLookUpEdit _lookPhongBan;
        private SearchLookUpEdit _lookBoPhanPhatHienLoi;
        private TextEdit _txtLyDo;
        private TextEdit _txtNguoiTao;

        // ============================================================
        // GRID
        // ============================================================

        private DevExpress.XtraGrid.GridControl _grid;
        private DevExpress.XtraGrid.Views.Grid.GridView _gridView;

        private RepositoryItemSearchLookUpEdit _repoSlot;
        private RepositoryItemSearchLookUpEdit _repoMaHang;

        private DevExpress.XtraEditors.SimpleButton _btnThem;
        private DevExpress.XtraEditors.SimpleButton _btnXoa;
        private DevExpress.XtraEditors.SimpleButton _btnLuu;
        private DevExpress.XtraEditors.SimpleButton _btnHuy;

        public FormTaoPhieuTraNoiBo(
            ITraNoiBoService traNoiBoService,
            INoiBoLookupRepository lookupRepo,
            ISlotService slotService)
        {
            _traNoiBoService = traNoiBoService
                ?? throw new ArgumentNullException(nameof(traNoiBoService));

            _lookupRepo = lookupRepo
                ?? throw new ArgumentNullException(nameof(lookupRepo));

            _slotService = slotService
                ?? throw new ArgumentNullException(nameof(slotService));

            BuiUI();
            LoadLookups();
            CapNhatSoPhieuTuSinh();
            ApDungCheDo();
        }

        /// <summary>
        /// Mở lại phiếu đã tạo: chưa đóng → Lưu thay đổi / Xóa;
        /// đã đóng → chỉ xem.
        /// </summary>
        public FormTaoPhieuTraNoiBo(
            ITraNoiBoService traNoiBoService,
            INoiBoLookupRepository lookupRepo,
            ISlotService slotService,
            IPhieuTraHangRepository phieuRepo,
            int phieuTraHangId)
        {
            _traNoiBoService = traNoiBoService
                ?? throw new ArgumentNullException(nameof(traNoiBoService));

            _lookupRepo = lookupRepo
                ?? throw new ArgumentNullException(nameof(lookupRepo));

            _slotService = slotService
                ?? throw new ArgumentNullException(nameof(slotService));

            _phieuRepo = phieuRepo
                ?? throw new ArgumentNullException(nameof(phieuRepo));

            _phieuId = phieuTraHangId;

            _editPhieu = _traNoiBoService.GetById(phieuTraHangId)
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy phiếu trả nội bộ Id={phieuTraHangId}.");

            _mode = _traNoiBoService.LaPhieuDaDong(_editPhieu)
                ? FormMode.ReadOnly
                : FormMode.Edit;

            BuiUI();
            LoadLookups();
            NapPhieuDaTao();
            ApDungCheDo();
        }

        // ============================================================
        // LOAD LOOKUP: B20Dept (bộ phận) + Slot đang chứa hàng
        // ============================================================

        private void LoadLookups()
        {
            try
            {
                _depts = _lookupRepo.GetActiveDepartments()
                    ?? new List<DeptLookupItem>();

                _items = _lookupRepo.GetItemsInSlots()
                    ?? new List<ItemLookupItem>();

                var nameByCode = _items
                    .GroupBy(x => x.ItemCode ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().ItemName,
                        StringComparer.OrdinalIgnoreCase);

                _slotItems = (_slotService.GetAllActiveSlotLots()
                        ?? new List<PCTP.Modules.KhoCore.Models.SlotLotViewInfo>())
                    .Where(x => x.Quantity > 0)
                    .Select(x => new SlotNguonLookupItem
                    {
                        SlotLotId = x.SlotLotId,
                        SlotId = x.SlotId,
                        ViTri = $"{x.WarehouseName} / {x.RackName} / Slot {x.SlotNumber}",
                        ItemCode = x.ItemCode,
                        TenHang = x.ItemCode != null
                                  && nameByCode.TryGetValue(x.ItemCode.Trim(), out var n)
                            ? n
                            : string.Empty,
                        LotNo = x.LotNo,
                        Quantity = x.Quantity,
                        TemCode = x.TemCode
                    })
                    .ToList();

                BindDeptLookup(_lookPhongBan);
                BindDeptLookup(_lookBoPhanPhatHienLoi);

                _repoSlot.DataSource = _slotItems;
                _repoSlot.PopulateViewColumns();
                CauHinhCotSlotLookup();

                _repoMaHang.DataSource = _items;
                _repoMaHang.PopulateViewColumns();
                CauHinhCotMaHangLookup();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không tải được danh sách bộ phận / slot.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BindDeptLookup(SearchLookUpEdit look)
        {
            look.Properties.DataSource = _depts;
            look.Properties.DisplayMember = nameof(DeptLookupItem.Display);
            look.Properties.ValueMember = nameof(DeptLookupItem.Code);
            look.Properties.PopulateViewColumns();

            var view = look.Properties.View;

            if (view.Columns[nameof(DeptLookupItem.Display)] != null)
                view.Columns[nameof(DeptLookupItem.Display)].Visible = false;

            var colCode = view.Columns[nameof(DeptLookupItem.Code)];
            if (colCode != null)
            {
                colCode.Caption = "Mã bộ phận";
                colCode.Width = 110;
            }

            var colName = view.Columns[nameof(DeptLookupItem.Name)];
            if (colName != null)
            {
                colName.Caption = "Tên bộ phận";
                colName.Width = 320;
            }
        }

        private static SearchLookUpEdit TaoDeptLookup()
        {
            var look = new SearchLookUpEdit { Dock = DockStyle.Fill };
            look.Properties.NullText = "";
            look.Properties.ShowClearButton = true;
            look.Properties.ImmediatePopup = true;
            look.Properties.PopupFormSize = new System.Drawing.Size(520, 360);
            look.Properties.View.OptionsView.ShowAutoFilterRow = false;
            // Ô tìm kiếm tìm trên cả Code và Name (các cột đang hiển thị).
            look.Properties.View.OptionsFind.AlwaysVisible = true;
            look.Properties.View.OptionsFind.FindNullPrompt = "Tìm theo mã hoặc tên...";
            look.Properties.View.OptionsFind.ShowCloseButton = false;
            return look;
        }

        private static string GetDeptName(SearchLookUpEdit look, List<DeptLookupItem> depts)
        {
            var code = look.EditValue as string;
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;

            var dept = depts.FirstOrDefault(d =>
                string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));

            if (dept == null) return code;
            return string.IsNullOrWhiteSpace(dept.Name) ? dept.Code : dept.Name;
        }

        // ============================================================
        // UI
        // ============================================================

        private void BuiUI()
        {
            Text = "Tạo phiếu trả nội bộ";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1200;
            Height = 700;

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };

            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // header
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // QR
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // grid
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // buttons

            // ========================================================
            // HEADER
            // ========================================================

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3
            };

            for (int i = 0; i < 4; i++)
            {
                header.ColumnStyles.Add(
                    new ColumnStyle(
                        i % 2 == 0
                            ? SizeType.Absolute
                            : SizeType.Percent,
                        i % 2 == 0 ? 140 : 50));
            }

            for (int i = 0; i < 3; i++)
            {
                header.RowStyles.Add(
                    new RowStyle(SizeType.Percent, 33.33f));
            }

            // Ô nhập căn giữa theo chiều dọc ô (Anchor trái-phải, không Dock Fill)
            // để thẳng hàng với nhãn.
            _txtSoPhieu = new TextEdit
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(3, 3, 8, 3)
            };
            _txtSoPhieu.Properties.ReadOnly = true;
            _txtSoPhieu.Properties.NullValuePrompt =
                "Tự sinh khi chọn Phòng ban + BP phát hiện lỗi";

            _lookPhongBan = TaoDeptLookup();
            _lookBoPhanPhatHienLoi = TaoDeptLookup();

            _lookPhongBan.EditValueChanged += (s, e) => CapNhatSoPhieuTuSinh();
            _lookBoPhanPhatHienLoi.EditValueChanged += (s, e) => CapNhatSoPhieuTuSinh();

            _txtLyDo = new TextEdit
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(3, 3, 8, 3)
            };

            _txtNguoiTao = new TextEdit
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(3, 3, 8, 3),
                Text = Environment.UserName
            };
            _txtNguoiTao.Properties.ReadOnly = true;

            AddLabel(header, "Số phiếu", 0, 0);
            header.Controls.Add(_txtSoPhieu, 1, 0);

            AddLabel(header, "Phòng ban", 2, 0);
            header.Controls.Add(_lookPhongBan, 3, 0);

            AddLabel(header, "BP phát hiện lỗi", 0, 1);
            header.Controls.Add(_lookBoPhanPhatHienLoi, 1, 1);

            AddLabel(header, "Người tạo", 2, 1);
            header.Controls.Add(_txtNguoiTao, 3, 1);

            AddLabel(header, "Lý do", 0, 2);
            header.Controls.Add(_txtLyDo, 1, 2);
            header.SetColumnSpan(_txtLyDo, 3);

            main.Controls.Add(header, 0, 0);

            // ========================================================
            // QR: quét tem để thêm dòng nhanh
            // ========================================================

            var qrPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };

            qrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            qrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            qrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _lblQr = new LabelControl
            {
                Text = "Quét QR thêm dòng",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            _lblQr.Appearance.TextOptions.VAlignment =
                DevExpress.Utils.VertAlignment.Center;
            _lblQr.Padding = new Padding(5, 0, 0, 0);

            _txtQr = new TextEdit
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(3, 3, 8, 3)
            };
            _txtQr.Properties.NullValuePrompt =
                "Quét tem QR (Lot:Mã hàng:Ngày:SL:...) rồi Enter";
            _txtQr.KeyDown += TxtQr_KeyDown;

            _lblQrStatus = new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None
            };
            _lblQrStatus.Appearance.TextOptions.VAlignment =
                DevExpress.Utils.VertAlignment.Center;

            qrPanel.Controls.Add(_lblQr, 0, 0);
            qrPanel.Controls.Add(_txtQr, 1, 0);
            qrPanel.Controls.Add(_lblQrStatus, 2, 0);

            main.Controls.Add(qrPanel, 0, 1);

            // ========================================================
            // GRID CHI TIET
            // ========================================================

            _grid = new DevExpress.XtraGrid.GridControl
            {
                Dock = DockStyle.Fill
            };

            _gridView = new DevExpress.XtraGrid.Views.Grid.GridView(_grid);

            _grid.MainView = _gridView;
            _grid.ViewCollection.Add(_gridView);

            _gridView.OptionsBehavior.Editable = true;
            _gridView.OptionsView.ShowGroupPanel = false;
            _gridView.OptionsView.NewItemRowPosition =
                DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.None;

            TaoCotGrid();

            _gridView.ShownEditor += GridView_ShownEditor;
            _gridView.CellValueChanged += GridView_CellValueChanged;

            main.Controls.Add(_grid, 0, 2);

            // ========================================================
            // BUTTON
            // ========================================================

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            _btnHuy = new SimpleButton
            {
                Text = "Hủy",
                Width = 100
            };

            _btnLuu = new SimpleButton
            {
                Text = "💾 Lưu phiếu",
                Width = 130
            };

            _btnXoaPhieu = new SimpleButton
            {
                Text = "❌ Xóa phiếu",
                Width = 110
            };

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

            _btnChonSlot = new SimpleButton
            {
                Text = "📦 Chọn từ Slot",
                Width = 130
            };

            _btnHuy.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnLuu.Click += (s, e) => LuuPhieu();

            _btnXoaPhieu.Click += (s, e) => XoaPhieu();

            _btnThem.Click += (s, e) => ThemDong();

            _btnChonSlot.Click += (s, e) => MoChonSlot();

            _btnXoa.Click += (s, e) => XoaDong();

            buttons.Controls.Add(_btnHuy);
            buttons.Controls.Add(_btnLuu);
            buttons.Controls.Add(_btnXoaPhieu);
            buttons.Controls.Add(_btnXoa);
            buttons.Controls.Add(_btnThem);
            buttons.Controls.Add(_btnChonSlot);

            main.Controls.Add(buttons, 0, 3);

            Controls.Add(main);
        }

        private void AddLabel(
            TableLayoutPanel panel,
            string text,
            int column,
            int row)
        {
            var label = new LabelControl
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(5, 0, 0, 0)
            };

            // Căn giữa theo chiều dọc để thẳng hàng với ô nhập.
            label.Appearance.TextOptions.VAlignment =
                DevExpress.Utils.VertAlignment.Center;

            panel.Controls.Add(label, column, row);
        }

        // ============================================================
        // GRID
        // ============================================================

        private void TaoCotGrid()
        {
            _gridView.Columns.Clear();

            // ---- Repository: Slot nguồn (Slot đang chứa hàng) ----
            _repoSlot = new RepositoryItemSearchLookUpEdit
            {
                NullText = "",
                ValueMember = nameof(SlotNguonLookupItem.SlotLotId),
                DisplayMember = nameof(SlotNguonLookupItem.ViTri),
                ImmediatePopup = true,
                PopupFormSize = new System.Drawing.Size(820, 380)
            };
            _repoSlot.DataSource = _slotItems;
            _repoSlot.PopulateViewColumns();
            _repoSlot.View.OptionsFind.AlwaysVisible = true;
            _repoSlot.View.OptionsFind.FindNullPrompt = "Tìm theo vị trí, mã hàng, lot...";
            _repoSlot.View.OptionsFind.ShowCloseButton = false;
            CauHinhCotSlotLookup();

            // ---- Repository: Mã hàng (mã hàng có trong Slot) ----
            _repoMaHang = new RepositoryItemSearchLookUpEdit
            {
                NullText = "",
                ValueMember = nameof(ItemLookupItem.ItemCode),
                DisplayMember = nameof(ItemLookupItem.ItemCode),
                ImmediatePopup = true,
                PopupFormSize = new System.Drawing.Size(560, 360)
            };
            _repoMaHang.DataSource = _items;
            _repoMaHang.PopulateViewColumns();
            _repoMaHang.View.OptionsFind.AlwaysVisible = true;
            _repoMaHang.View.OptionsFind.FindNullPrompt = "Tìm theo mã hoặc tên hàng...";
            _repoMaHang.View.OptionsFind.ShowCloseButton = false;
            CauHinhCotMaHangLookup();

            var repoSoLuong = new RepositoryItemSpinEdit
            {
                IsFloatValue = false,
                MinValue = 0,
                MaxValue = int.MaxValue
            };

            _grid.RepositoryItems.AddRange(
                new RepositoryItem[] { _repoSlot, _repoMaHang, repoSoLuong });

            var colSlot = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SlotLotIdNguon));
            colSlot.Caption = "Slot nguồn";
            colSlot.Visible = true;
            colSlot.Width = 230;
            colSlot.ColumnEdit = _repoSlot;

            var colSlotId = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SlotIdNguon));
            colSlotId.Caption = "SlotId";
            colSlotId.Visible = false;

            var colMaHang = _gridView.Columns.AddField(nameof(PhieuTraHangCT.MaHang));
            colMaHang.Caption = "Mã hàng";
            colMaHang.Visible = true;
            colMaHang.Width = 140;
            colMaHang.ColumnEdit = _repoMaHang;

            var colTenHang = _gridView.Columns.AddField(nameof(PhieuTraHangCT.TenHang));
            colTenHang.Caption = "Tên hàng";
            colTenHang.Visible = true;
            colTenHang.Width = 180;
            colTenHang.OptionsColumn.AllowEdit = false;

            var colLot = _gridView.Columns.AddField(nameof(PhieuTraHangCT.LotNo));
            colLot.Caption = "Lot No";
            colLot.Visible = true;
            colLot.Width = 130;
            colLot.OptionsColumn.AllowEdit = false;

            var colSoLuong = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SoLuong));
            colSoLuong.Caption = "Số lượng";
            colSoLuong.Visible = true;
            colSoLuong.Width = 90;
            colSoLuong.ColumnEdit = repoSoLuong;

            var colLyDo = _gridView.Columns.AddField(nameof(PhieuTraHangCT.LyDoNg));
            colLyDo.Caption = "Lý do NG";
            colLyDo.Visible = true;
            colLyDo.Width = 220;

            // Các cột đối chiếu phiếu giao:
            var colDinhDanh = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.DinhDanhPhieuGiao));

            colDinhDanh.Caption = "Phiếu giao gốc";
            colDinhDanh.Visible = false;

            var colPo = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.PoNo));

            colPo.Caption = "PO";
            colPo.Visible = false;

            var colNgayGiao = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.NgayGiao));

            colNgayGiao.Caption = "Ngày giao";
            colNgayGiao.Visible = false;

            var colNhaMay = _gridView.Columns.AddField(
                nameof(PhieuTraHangCT.NhaMay));

            colNhaMay.Caption = "Nhà máy";
            colNhaMay.Visible = false;
        }

        private void CauHinhCotSlotLookup()
        {
            var v = _repoSlot.View;

            void Cot(string field, string caption, int width, bool visible = true)
            {
                var c = v.Columns[field];
                if (c == null) return;
                c.Caption = caption;
                c.Width = width;
                c.Visible = visible;
            }

            Cot(nameof(SlotNguonLookupItem.SlotLotId), "SlotLotId", 0, false);
            Cot(nameof(SlotNguonLookupItem.SlotId), "SlotId", 0, false);
            Cot(nameof(SlotNguonLookupItem.TemCode), "Tem", 0, false);
            Cot(nameof(SlotNguonLookupItem.ViTri), "Vị trí", 230);
            Cot(nameof(SlotNguonLookupItem.ItemCode), "Mã hàng", 120);
            Cot(nameof(SlotNguonLookupItem.TenHang), "Tên hàng", 180);
            Cot(nameof(SlotNguonLookupItem.LotNo), "Lot No", 120);
            Cot(nameof(SlotNguonLookupItem.Quantity), "Tồn", 70);
        }

        private void CauHinhCotMaHangLookup()
        {
            var v = _repoMaHang.View;

            var c1 = v.Columns[nameof(ItemLookupItem.ItemCode)];
            if (c1 != null) { c1.Caption = "Mã hàng"; c1.Width = 140; }

            var c2 = v.Columns[nameof(ItemLookupItem.ItemName)];
            if (c2 != null) { c2.Caption = "Tên hàng"; c2.Width = 360; }
        }

        // Khi mở editor: lọc Slot theo Mã hàng của dòng, giới hạn Số lượng theo tồn.
        private void GridView_ShownEditor(object sender, EventArgs e)
        {
            var row = _gridView.GetFocusedRow() as PhieuTraHangCT;
            var field = _gridView.FocusedColumn?.FieldName;

            if (row == null || field == null)
                return;

            if (field == nameof(PhieuTraHangCT.SlotLotIdNguon)
                && _gridView.ActiveEditor is SearchLookUpEdit slotEdit)
            {
                slotEdit.Properties.DataSource =
                    string.IsNullOrWhiteSpace(row.MaHang)
                        ? _slotItems
                        : _slotItems
                            .Where(x => string.Equals(
                                x.ItemCode?.Trim(), row.MaHang.Trim(),
                                StringComparison.OrdinalIgnoreCase))
                            .ToList();
            }
            else if (field == nameof(PhieuTraHangCT.SoLuong)
                && _gridView.ActiveEditor is SpinEdit spin)
            {
                var slot = TimSlotItem(row.SlotLotIdNguon);
                spin.Properties.MaxValue = slot != null ? GetMaxQty(row, slot) : int.MaxValue;
            }
        }

        private void GridView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (_updatingRow)
                return;

            var row = _gridView.GetRow(e.RowHandle) as PhieuTraHangCT;
            if (row == null)
                return;

            _updatingRow = true;

            try
            {
                if (e.Column.FieldName == nameof(PhieuTraHangCT.SlotLotIdNguon))
                {
                    var slot = TimSlotItem(row.SlotLotIdNguon);

                    if (slot == null)
                    {
                        row.SlotLotIdNguon = null;
                        row.SlotIdNguon = null;
                        row.LotNo = string.Empty;
                        row.SoLuong = 0;
                    }
                    else
                    {
                        row.SlotIdNguon = slot.SlotId;
                        row.MaHang = slot.ItemCode;
                        row.TenHang = slot.TenHang;
                        row.LotNo = slot.LotNo;
                        row.SoLuong = slot.Quantity;
                    }
                }
                else if (e.Column.FieldName == nameof(PhieuTraHangCT.MaHang))
                {
                    var item = _items.FirstOrDefault(x => string.Equals(
                        x.ItemCode, row.MaHang, StringComparison.OrdinalIgnoreCase));

                    row.TenHang = item?.ItemName ?? string.Empty;

                    // Đổi mã hàng → Slot đã chọn trước đó không còn khớp thì bỏ.
                    var slot = TimSlotItem(row.SlotLotIdNguon);
                    if (slot != null && !string.Equals(
                            slot.ItemCode?.Trim(), row.MaHang?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        row.SlotLotIdNguon = null;
                        row.SlotIdNguon = null;
                        row.LotNo = string.Empty;
                        row.SoLuong = 0;
                    }
                }
            }
            finally
            {
                _updatingRow = false;
            }

            _gridView.RefreshRow(e.RowHandle);
        }

        private SlotNguonLookupItem TimSlotItem(int? slotLotId)
        {
            if (!slotLotId.HasValue)
                return null;

            return _slotItems.FirstOrDefault(x => x.SlotLotId == slotLotId.Value);
        }

        // ============================================================
        // THÊM DÒNG
        // ============================================================

        private void ThemDong()
        {
            var item = new PhieuTraHangCT
            {
                MaHang = "",
                TenHang = "",
                LotNo = "",
                SoLuong = 0,
                LyDoNg = "",
                SlotIdNguon = null,
                SlotLotIdNguon = null
            };

            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
            {
                data = new List<PhieuTraHangCT>();
                _grid.DataSource = data;
            }

            data.Add(item);

            _grid.RefreshDataSource();

            int rowHandle = _gridView.GetRowHandle(data.Count - 1);

            if (rowHandle >= 0)
            {
                _gridView.FocusedRowHandle = rowHandle;
                _gridView.ShowEditor();
            }
        }

        // ============================================================
        // XÓA DÒNG
        // ============================================================

        private void XoaDong()
        {
            int rowHandle = _gridView.FocusedRowHandle;

            if (rowHandle < 0)
                return;

            var item = _gridView.GetRow(rowHandle) as PhieuTraHangCT;

            if (item == null)
                return;

            var data = _grid.DataSource as List<PhieuTraHangCT>;

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
            if (string.IsNullOrWhiteSpace(_lookPhongBan.EditValue as string))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn Phòng ban.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _lookPhongBan.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(_lookBoPhanPhatHienLoi.EditValue as string))
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn Bộ phận phát hiện lỗi.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _lookBoPhanPhatHienLoi.Focus();
                return false;
            }

            // Phiếu đã có phiếu xử lý: chỉ sửa thông tin chung, không kiểm dòng.
            if (!_choSuaChiTiet)
                return true;

            if (items == null || items.Count == 0)
            {
                XtraMessageBox.Show(
                    "Phiếu phải có ít nhất một dòng hàng.",
                    "Thiếu dữ liệu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (string.IsNullOrWhiteSpace(item.MaHang))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa nhập Mã hàng.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (string.IsNullOrWhiteSpace(item.LotNo))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa nhập Lot No.",
                        "Thiếu dữ liệu",
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

                if (item.SlotIdNguon == null)
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: chưa chọn Slot nguồn.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                var slot = TimSlotItem(item.SlotLotIdNguon);

                if (slot != null && item.SoLuong > GetMaxQty(item, slot))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: Số lượng ({item.SoLuong}) vượt quá tồn trong Slot ({GetMaxQty(item, slot)}).",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }

                if (items.Take(i).Any(x => x.SlotLotIdNguon == item.SlotLotIdNguon))
                {
                    XtraMessageBox.Show(
                        $"Dòng {i + 1}: Slot/LOT này đã được chọn ở dòng khác.",
                        "Dữ liệu trùng",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }
            }

            return true;
        }

        // ============================================================
        // SỐ PHIẾU TỰ SINH: Code(Phòng ban)-Code(BP phát hiện lỗi)-ddMMyy
        // ============================================================

        private void CapNhatSoPhieuTuSinh()
        {
            // Chỉ sinh khi tạo mới; phiếu đã lưu giữ nguyên số phiếu.
            if (_mode != FormMode.Create)
                return;

            string pb = _lookPhongBan.EditValue as string;
            string bp = _lookBoPhanPhatHienLoi.EditValue as string;

            _txtSoPhieu.Text =
                string.IsNullOrWhiteSpace(pb) || string.IsNullOrWhiteSpace(bp)
                    ? string.Empty
                    : $"{pb.Trim()}-{bp.Trim()}-{DateTime.Now:ddMMyy}";
        }

        // ============================================================
        // MỞ LẠI PHIẾU ĐÃ TẠO
        // ============================================================

        private void NapPhieuDaTao()
        {
            _txtSoPhieu.Text = _editPhieu.SoPhieu;
            _txtLyDo.Text = _editPhieu.LyDo;
            _txtNguoiTao.Text = _editPhieu.CreatedBy;

            _lookPhongBan.EditValue = TimMaBoPhan(_editPhieu.PhongBan);
            _lookBoPhanPhatHienLoi.EditValue = TimMaBoPhan(_editPhieu.BoPhanPhatHienLoi);

            var items = _phieuRepo.GetItems(_phieuId) ?? new List<PhieuTraHangCT>();

            foreach (var it in items)
            {
                var slot = _slotItems.FirstOrDefault(x =>
                    it.SlotIdNguon.HasValue
                    && x.SlotId == it.SlotIdNguon.Value
                    && string.Equals(x.LotNo?.Trim(), it.LotNo?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.ItemCode?.Trim(), it.MaHang?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (slot == null)
                {
                    // Slot/LOT không còn tồn: giữ lại dòng bằng mục tổng hợp để vẫn hiển thị.
                    slot = new SlotNguonLookupItem
                    {
                        SlotLotId = -(_slotItems.Count + 1),
                        SlotId = it.SlotIdNguon ?? 0,
                        ViTri = $"Slot #{it.SlotIdNguon} (không còn tồn)",
                        ItemCode = it.MaHang,
                        TenHang = it.TenHang,
                        LotNo = it.LotNo,
                        Quantity = it.SoLuong
                    };

                    _slotItems.Add(slot);
                }

                it.SlotLotIdNguon = slot.SlotLotId;
                _soLuongGoc[it] = it.SoLuong;
            }

            _repoSlot.DataSource = _slotItems;
            _repoSlot.PopulateViewColumns();
            CauHinhCotSlotLookup();

            // Mã hàng của dòng cũ có thể không còn trong Slot → bổ sung để hiển thị.
            foreach (var it in items)
            {
                if (!string.IsNullOrWhiteSpace(it.MaHang)
                    && !_items.Any(x => string.Equals(x.ItemCode, it.MaHang, StringComparison.OrdinalIgnoreCase)))
                {
                    _items.Add(new ItemLookupItem { ItemCode = it.MaHang, ItemName = it.TenHang });
                }
            }

            _repoMaHang.DataSource = _items;
            _repoMaHang.PopulateViewColumns();
            CauHinhCotMaHangLookup();

            _grid.DataSource = items;

            _choSuaChiTiet = !_traNoiBoService.CoPhieuXuLy(_phieuId);
        }

        private string TimMaBoPhan(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return null;

            stored = stored.Trim();

            var dept = _depts.FirstOrDefault(d =>
                string.Equals(d.Name?.Trim(), stored, StringComparison.OrdinalIgnoreCase))
                ?? _depts.FirstOrDefault(d =>
                string.Equals(d.Code?.Trim(), stored, StringComparison.OrdinalIgnoreCase));

            if (dept == null)
            {
                // Bộ phận đã ngừng dùng / không có trong B20Dept: thêm mục để hiển thị.
                dept = new DeptLookupItem { Code = stored, Name = stored };
                _depts.Add(dept);

                _lookPhongBan.Properties.DataSource = _depts;
                _lookBoPhanPhatHienLoi.Properties.DataSource = _depts;
            }

            return dept.Code;
        }

        private int GetMaxQty(PhieuTraHangCT row, SlotNguonLookupItem slot)
        {
            int goc;
            return _soLuongGoc.TryGetValue(row, out goc)
                ? Math.Max(slot.Quantity, goc)
                : slot.Quantity;
        }

        // ============================================================
        // CHẾ ĐỘ: Tạo / Sửa / Chỉ xem
        // ============================================================

        private void ApDungCheDo()
        {
            if (_mode == FormMode.Create)
            {
                _grid.DataSource = new List<PhieuTraHangCT>();
                return;
            }

            bool daDong = _mode == FormMode.ReadOnly;
            bool suaHeader = !daDong;
            bool suaChiTiet = !daDong && _choSuaChiTiet;

            Text = daDong
                ? $"Phiếu trả nội bộ {_editPhieu.SoPhieu} (đã đóng - chỉ xem)"
                : $"Phiếu trả nội bộ {_editPhieu.SoPhieu}";

            _lookPhongBan.Properties.ReadOnly = !suaHeader;
            _lookBoPhanPhatHienLoi.Properties.ReadOnly = !suaHeader;
            _txtLyDo.Properties.ReadOnly = !suaHeader;

            _gridView.OptionsBehavior.Editable = suaChiTiet;

            _txtQr.Enabled = suaChiTiet;
            _btnThem.Visible = suaChiTiet;
            _btnXoa.Visible = suaChiTiet;
            _btnChonSlot.Visible = suaChiTiet;

            // Đã mở lại phiếu → không còn chức năng "Tạo phiếu".
            _btnLuu.Text = "💾 Lưu thay đổi";
            _btnLuu.Visible = suaHeader;
            _btnXoaPhieu.Visible = suaChiTiet; // xóa phiếu chỉ khi chưa có phiếu xử lý
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

        // ============================================================
        // QUÉT QR → THÊM DÒNG NHANH
        // ============================================================

        private void TxtQr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            string raw = _txtQr.Text;
            _txtQr.Text = string.Empty;

            XuLyQr(raw);

            _txtQr.Focus();
        }

        private void BaoQr(string text, bool ok)
        {
            _lblQrStatus.Text = text;
            _lblQrStatus.Appearance.ForeColor =
                ok ? System.Drawing.Color.ForestGreen : System.Drawing.Color.Firebrick;
        }

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

            // Item phải khớp với Slot đang chứa hàng (lookup của grid).
            var matches = QrItemParser.TimSlotKhop(_slotItems.Where(x => x.SlotLotId > 0), info);

            if (matches.Count == 0)
            {
                BaoQr($"Không có Slot nào đang chứa {info.ItemCode} / Lot {info.Lot}.", false);
                return;
            }

            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
            {
                data = new List<PhieuTraHangCT>();
                _grid.DataSource = data;
            }

            var slot = matches.FirstOrDefault(m =>
                !data.Any(d => d.SlotLotIdNguon == m.SlotLotId));

            if (slot == null)
            {
                BaoQr($"Slot/LOT của {info.ItemCode} đã có trong phiếu.", false);
                return;
            }

            // Dùng chung 1 hàm dựng dòng với "Chọn từ Slot" → cùng định dạng dòng.
            ThemDongTuSlotItem(slot, Math.Min(info.Quantity, slot.Quantity), string.Empty);

            _grid.RefreshDataSource();

            BaoQr(
                $"Đã thêm {slot.ItemCode} / Lot {slot.LotNo} / SL {Math.Min(info.Quantity, slot.Quantity)} ({slot.ViTri}).",
                true);
        }

        // ============================================================
        // CHỌN NHANH TỪ SLOT → THÊM DÒNG
        // (FormChonSlotNoiBo chỉ là lối tắt để add dòng vào phiếu này)
        // ============================================================

        /// <summary>Dựng 1 dòng chi tiết từ Slot/LOT — dùng chung cho QR, chọn từ Slot...</summary>
        private PhieuTraHangCT ThemDongTuSlotItem(SlotNguonLookupItem slot, int soLuong, string lyDoNg)
        {
            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
            {
                data = new List<PhieuTraHangCT>();
                _grid.DataSource = data;
            }

            var dong = new PhieuTraHangCT
            {
                SlotLotIdNguon = slot.SlotLotId,
                SlotIdNguon = slot.SlotId,
                MaHang = slot.ItemCode,
                TenHang = slot.TenHang,
                LotNo = slot.LotNo,
                SoLuong = soLuong,
                LyDoNg = lyDoNg ?? string.Empty
            };

            data.Add(dong);
            return dong;
        }

        /// <summary>
        /// Thêm các dòng đã chọn nhanh từ Slot vào lưới. Bỏ qua SlotLot đã có trong phiếu;
        /// trả về số dòng thực sự được thêm.
        /// </summary>
        public int ThemDongTuSlot(IEnumerable<SlotNguonChon> chon)
        {
            if (chon == null) return 0;

            if (_mode == FormMode.ReadOnly || !_choSuaChiTiet)
                return 0;

            var data = _grid.DataSource as List<PhieuTraHangCT>;
            int added = 0;

            foreach (var c in chon)
            {
                var slot = TimSlotItem(c.SlotLotId);
                if (slot == null || slot.SlotLotId <= 0)
                    continue;

                data = _grid.DataSource as List<PhieuTraHangCT>;
                if (data != null && data.Any(d => d.SlotLotIdNguon == slot.SlotLotId))
                    continue;

                int sl = Math.Max(1, Math.Min(c.SoLuong, slot.Quantity));
                ThemDongTuSlotItem(slot, sl, c.LyDoNg);
                added++;
            }

            _grid.RefreshDataSource();
            return added;
        }

        private void MoChonSlot()
        {
            _gridView.CloseEditor();
            _gridView.UpdateCurrentRow();

            var daCo = ((_grid.DataSource as List<PhieuTraHangCT>) ?? new List<PhieuTraHangCT>())
                .Where(x => x.SlotLotIdNguon.HasValue)
                .Select(x => x.SlotLotIdNguon.Value)
                .ToList();

            using (var f = new FormChonSlotNoiBo(_slotService, daCo))
            {
                if (f.ShowDialog(this) != DialogResult.OK)
                    return;

                int added = ThemDongTuSlot(f.KetQua);

                BaoQr(
                    added > 0
                        ? $"Đã thêm {added} dòng từ Slot."
                        : "Không có dòng nào được thêm (Slot/LOT đã có trong phiếu).",
                    added > 0);
            }
        }

        // ============================================================
        // XÓA PHIẾU
        // ============================================================

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
                _traNoiBoService.XoaPhieu(_phieuId, Environment.UserName);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không thể xóa phiếu.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // LƯU
        // ============================================================

        private void LuuPhieu()
        {
            try
            {
                _gridView.CloseEditor();
                _gridView.UpdateCurrentRow();

                var items = (_grid.DataSource as List<PhieuTraHangCT>)
                    ?? new List<PhieuTraHangCT>();

                if (!ValidateForm(items))
                    return;

                if (_mode == FormMode.Edit)
                {
                    LuuThayDoi(items);
                    return;
                }

                string soPhieu = _txtSoPhieu.Text.Trim();

                string nguoiTao = _txtNguoiTao.Text.Trim();

                if (string.IsNullOrWhiteSpace(nguoiTao))
                    nguoiTao = Environment.UserName;

                var phieu = new PhieuTraHang
                {
                    Nguon = NguonXuLyBatThuong.TraNoiBo,

                    // Tự sinh: Code(PB)-Code(BP)-ddMMyy (service tự thêm hậu tố nếu trùng).
                    SoPhieu = soPhieu,

                    CustomerNo = null,
                    SoPhieuKhach = null,
                    NgayPhatHanh = null,
                    SlipNo = null,
                    Ca = null,
                    TenKhachHang = null,

                    PhongBan = GetDeptName(_lookPhongBan, _depts),

                    LyDo = _txtLyDo.Text.Trim(),

                    BoPhanPhatHienLoi =
                        GetDeptName(_lookBoPhanPhatHienLoi, _depts),

                    XacNhanBPPhatHienLoi = null,
                    XacNhanQCKhach = null,
                    XacNhanNhaCungCap = null,

                    NgayNhanKho = null,

                    TongSoLuongNhan = items.Sum(x => x.SoLuong),

                    Status = PhieuTraHangStatus.Moi,

                    BoPhanNhanLai = null,
                    SoLuongGiaoLai = null,
                    NgayGiaoLaiBoPhan = null,
                    NguoiGiaoLaiBoPhan = null,

                    Note = null,

                    CreatedAt = DateTime.Now,
                    CreatedBy = nguoiTao,

                    UpdatedAt = null,
                    UpdatedBy = null,

                    ChiTiet = items
                };

                foreach (var item in phieu.ChiTiet)
                {
                    item.Id = 0;
                    item.PhieuTraHangId = 0;

                    item.DinhDanhPhieuGiao = null;
                    item.PoNo = null;
                    item.NgayGiao = null;
                    item.NhaMay = null;
                }

                int id = _traNoiBoService.TaoPhieuTraNoiBo(phieu);

                XtraMessageBox.Show(
                    $"Đã tạo phiếu trả nội bộ thành công.\r\n\r\n" +
                    $"Số phiếu: {phieu.SoPhieu}\r\n" +
                    $"ID: {id}\r\n" +
                    $"Số dòng: {phieu.ChiTiet.Count}\r\n" +
                    $"Tổng số lượng: {phieu.TongSoLuongNhan}",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không thể lưu phiếu trả nội bộ.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LuuThayDoi(List<PhieuTraHangCT> items)
        {
            _editPhieu.PhongBan = GetDeptName(_lookPhongBan, _depts);
            _editPhieu.BoPhanPhatHienLoi = GetDeptName(_lookBoPhanPhatHienLoi, _depts);
            _editPhieu.LyDo = _txtLyDo.Text.Trim();

            if (_choSuaChiTiet)
            {
                foreach (var item in items)
                {
                    item.PhieuTraHangId = _phieuId;
                }

                _editPhieu.ChiTiet = items;
            }
            else
            {
                _editPhieu.ChiTiet = null; // chỉ cập nhật header
            }

            _traNoiBoService.CapNhatPhieu(_editPhieu, Environment.UserName);

            XtraMessageBox.Show(
                "Đã lưu thay đổi phiếu.",
                "Thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
