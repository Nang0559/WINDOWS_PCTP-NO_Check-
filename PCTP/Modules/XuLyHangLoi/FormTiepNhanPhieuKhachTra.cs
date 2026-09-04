

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using DevExpress.XtraGrid;
    using DevExpress.XtraGrid.Columns;
    using DevExpress.XtraGrid.Views.Grid;
    using PCTP.Modules.XuLyHangLoi.Enums;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Modules.XuLyHangLoi.Services;
    using PCTP.Shared.Enums;
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
        private ComboBoxEdit _cboNguonKhachTra;
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

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public FormTiepNhanPhieuKhachTra(
            IKhachTraHangService khachTraHangService)
        {
            _khachTraHangService =
                khachTraHangService
                ?? throw new ArgumentNullException(
                    nameof(khachTraHangService));

            BuidIU();

            KhoiTaoDuLieuBanDau();
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
                RowCount = 3,
                Padding = new Padding(8)
            };

            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    205));

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

            mainLayout.Controls.Add(
                header,
                0,
                0);

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
                1);

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
            buttonPanel.Controls.Add(_btnXoa);
            buttonPanel.Controls.Add(_btnThem);

            mainLayout.Controls.Add(
                buttonPanel,
                0,
                2);

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

            _cboNguonKhachTra =
                new ComboBoxEdit
                {
                    Dock = DockStyle.Fill
                };

            _cboNguonKhachTra.Properties.TextEditStyle =
                DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            foreach (NguonKhachTra value
                     in Enum.GetValues(typeof(NguonKhachTra)))
            {
                _cboNguonKhachTra.Properties.Items.Add(value);
            }

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
                        new Padding(5, 8, 0, 0)
                };

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

        // ============================================================
        // DEFAULT DATA
        // ============================================================

        private void KhoiTaoDuLieuBanDau()
        {
            _txtSoPhieu.Text =
                TaoSoPhieuTam();

            _dtNgayPhatHanh.EditValue =
                DateTime.Today;

            var values =
                Enum.GetValues(typeof(NguonKhachTra));

            if (values.Length > 0)
            {
                _cboNguonKhachTra.SelectedIndex = 0;
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

            if (_cboNguonKhachTra.EditValue == null)
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn nguồn khách trả.",
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
            var nguon =
                (NguonKhachTra)
                _cboNguonKhachTra.EditValue;

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

                    NguonKhachTra =
                        nguon,

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
