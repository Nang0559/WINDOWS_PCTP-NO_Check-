

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
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
    using PCTP.Shared.Models;
    using System;
    using System.Collections.Generic;
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

        // ============================================================
        // MỞ LẠI PHIẾU / QR
        // ============================================================

        private enum FormMode { Create, Edit, ReadOnly }

        private readonly FormMode _mode = FormMode.Create;
        private readonly IPhieuTraHangRepository _phieuRepo;
        private readonly INoiBoLookupRepository _lookupRepo;
        private readonly ISlotService _slotService; // null → ẩn nút tra cứu Slot
        private readonly int _phieuId;
        private PhieuTraHang _editPhieu;

        // Đã có PhieuXuLyBatThuong → chỉ sửa thông tin chung.
        private bool _choSuaChiTiet = true;

        private TextEdit _txtQr;
        private LabelControl _lblQrStatus;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public FormTiepNhanPhieuKhachTra(
            IKhachTraHangService khachTraHangService,
            INoiBoLookupRepository lookupRepo = null,
            ISlotService slotService = null)
        {
            _khachTraHangService =
                khachTraHangService
                ?? throw new ArgumentNullException(
                    nameof(khachTraHangService));

            _lookupRepo = lookupRepo;
            _slotService = slotService;

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

            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    40));

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
                TaoQrPanel(),
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
            colMaHang.Width = 150;

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

            // ========================================================
            // LOT
            // ========================================================

            var colLot =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.LotNo));

            colLot.Caption =
                "Lot No";

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
            colSoLuong.VisibleIndex = 3;
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
            colLyDo.VisibleIndex = 4;
            colLyDo.Width = 250;

            // ========================================================
            // ĐỐI CHIẾU PHIẾU GIAO
            //
            // Ban đầu chưa có.
            // Service sẽ xử lý sau.
            // ========================================================

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

            colPo.Visible = false;

            var colNgayGiao =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.NgayGiao));

            colNgayGiao.Caption =
                "Ngày giao";

            colNgayGiao.Visible = false;

            var colNhaMay =
                _gridView.Columns.AddField(
                    nameof(PhieuTraHangCT.NhaMay));

            colNhaMay.Caption =
                "Nhà máy";

            colNhaMay.Visible = false;
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

        private TableLayoutPanel TaoQrPanel()
        {
            var qrPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };

            qrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            qrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            qrPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var lbl = new LabelControl
            {
                Text = "Quét QR thêm dòng",
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Padding = new Padding(5, 0, 0, 0)
            };
            lbl.Appearance.TextOptions.VAlignment =
                DevExpress.Utils.VertAlignment.Center;

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

            qrPanel.Controls.Add(lbl, 0, 0);
            qrPanel.Controls.Add(_txtQr, 1, 0);
            qrPanel.Controls.Add(_lblQrStatus, 2, 0);

            return qrPanel;
        }

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

            // Mã hàng phải có trong danh mục hàng (vB20Item).
            string tenHang = string.Empty;

            if (_lookupRepo != null)
            {
                tenHang = _lookupRepo.GetItemName(info.ItemCode);

                if (tenHang == null)
                {
                    BaoQr($"Mã hàng {info.ItemCode} không có trong danh mục.", false);
                    return;
                }
            }

            var data = _grid.DataSource as List<PhieuTraHangCT>;

            if (data == null)
            {
                data = new List<PhieuTraHangCT>();
                _grid.DataSource = data;
            }

            // Cùng Mã hàng + Lot (quét nhiều tem) → cộng dồn số lượng.
            var exist = data.FirstOrDefault(d =>
                string.Equals(d.MaHang?.Trim(), info.ItemCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.LotNo?.Trim(), info.Lot, StringComparison.OrdinalIgnoreCase));

            if (exist != null)
            {
                exist.SoLuong += info.Quantity;
            }
            else
            {
                data.Add(new PhieuTraHangCT
                {
                    SlotIdNguon = null,
                    MaHang = info.ItemCode,
                    TenHang = tenHang,
                    LotNo = info.Lot,
                    SoLuong = info.Quantity,
                    LyDoNg = string.Empty
                });
            }

            _grid.RefreshDataSource();

            BaoQr($"Đã thêm {info.ItemCode} / Lot {info.Lot} / SL {info.Quantity}.", true);
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

            _grid.DataSource =
                _phieuRepo.GetItems(_phieuId) ?? new List<PhieuTraHangCT>();

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

            _txtQr.Enabled = suaChiTiet;
            _btnThem.Visible = suaChiTiet;
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

                if (string.IsNullOrWhiteSpace(
                    item.LotNo))
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

                if (_mode == FormMode.Edit)
                {
                    LuuThayDoi(items);
                    return;
                }

                var phieu =
                    BuildPhieu(items);

                // ====================================================
                // SERVICE DUY NHẤT XỬ LÝ NGHIỆP VỤ
                // ====================================================

                int id =
                    _khachTraHangService
                        .TiepNhanPhieuKhachTra(phieu);

                XtraMessageBox.Show(
                    $"Đã tiếp nhận phiếu khách trả thành công.\r\n\r\n" +
                    $"Số phiếu: {phieu.SoPhieu}\r\n" +
                    $"ID: {id}\r\n" +
                    $"Số dòng: {phieu.ChiTiet.Count}\r\n" +
                    $"Tổng số lượng: {phieu.TongSoLuongNhan}",
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
