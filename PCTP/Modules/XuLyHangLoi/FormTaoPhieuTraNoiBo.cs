using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
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

        // ============================================================
        // HEADER
        // ============================================================

        private TextEdit _txtSoPhieu;
        private TextEdit _txtPhongBan;
        private TextEdit _txtBoPhanPhatHienLoi;
        private TextEdit _txtLyDo;
        private TextEdit _txtNguoiTao;

        // ============================================================
        // GRID
        // ============================================================

        private DevExpress.XtraGrid.GridControl _grid;
        private DevExpress.XtraGrid.Views.Grid.GridView _gridView;

        private DevExpress.XtraEditors.SimpleButton _btnThem;
        private DevExpress.XtraEditors.SimpleButton _btnXoa;
        private DevExpress.XtraEditors.SimpleButton _btnLuu;
        private DevExpress.XtraEditors.SimpleButton _btnHuy;

        public FormTaoPhieuTraNoiBo(
            ITraNoiBoService traNoiBoService)
        {
            _traNoiBoService = traNoiBoService
                ?? throw new ArgumentNullException(nameof(traNoiBoService));

            BuiUI();
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
                RowCount = 3,
                Padding = new Padding(8)
            };

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 150));

            main.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

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

            _txtSoPhieu = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtPhongBan = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtBoPhanPhatHienLoi = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtLyDo = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtNguoiTao = new TextEdit
            {
                Dock = DockStyle.Fill,
                Text = Environment.UserName
            };

            AddLabel(header, "Số phiếu", 0, 0);
            header.Controls.Add(_txtSoPhieu, 1, 0);

            AddLabel(header, "Phòng ban", 2, 0);
            header.Controls.Add(_txtPhongBan, 3, 0);

            AddLabel(header, "BP phát hiện lỗi", 0, 1);
            header.Controls.Add(_txtBoPhanPhatHienLoi, 1, 1);

            AddLabel(header, "Người tạo", 2, 1);
            header.Controls.Add(_txtNguoiTao, 3, 1);

            AddLabel(header, "Lý do", 0, 2);
            header.Controls.Add(_txtLyDo, 1, 2);
            header.SetColumnSpan(_txtLyDo, 3);

            main.Controls.Add(header, 0, 0);

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

            main.Controls.Add(_grid, 0, 1);

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

            _btnHuy.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnLuu.Click += (s, e) => LuuPhieu();

            _btnThem.Click += (s, e) => ThemDong();

            _btnXoa.Click += (s, e) => XoaDong();

            buttons.Controls.Add(_btnHuy);
            buttons.Controls.Add(_btnLuu);
            buttons.Controls.Add(_btnXoa);
            buttons.Controls.Add(_btnThem);

            main.Controls.Add(buttons, 0, 2);

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
                Padding = new Padding(5, 8, 0, 0)
            };

            panel.Controls.Add(label, column, row);
        }

        // ============================================================
        // GRID
        // ============================================================

        private void TaoCotGrid()
        {
            _gridView.Columns.Clear();

            var colSlot = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SlotIdNguon));
            colSlot.Caption = "Slot nguồn";
            colSlot.Visible = true;
            colSlot.Width = 100;

            var colMaHang = _gridView.Columns.AddField(nameof(PhieuTraHangCT.MaHang));
            colMaHang.Caption = "Mã hàng";
            colMaHang.Visible = true;
            colMaHang.Width = 140;

            var colTenHang = _gridView.Columns.AddField(nameof(PhieuTraHangCT.TenHang));
            colTenHang.Caption = "Tên hàng";
            colTenHang.Visible = true;
            colTenHang.Width = 180;

            var colLot = _gridView.Columns.AddField(nameof(PhieuTraHangCT.LotNo));
            colLot.Caption = "Lot No";
            colLot.Visible = true;
            colLot.Width = 130;

            var colSoLuong = _gridView.Columns.AddField(nameof(PhieuTraHangCT.SoLuong));
            colSoLuong.Caption = "Số lượng";
            colSoLuong.Visible = true;
            colSoLuong.Width = 90;

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
                SlotIdNguon = null
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
            if (string.IsNullOrWhiteSpace(_txtPhongBan.Text))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Phòng ban.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtPhongBan.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtBoPhanPhatHienLoi.Text))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Bộ phận phát hiện lỗi.",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtBoPhanPhatHienLoi.Focus();
                return false;
            }

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
                        $"Dòng {i + 1}: chưa xác định Slot nguồn.",
                        "Thiếu dữ liệu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }
            }

            return true;
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

                string soPhieu = _txtSoPhieu.Text.Trim();

                if (string.IsNullOrWhiteSpace(soPhieu))
                {
                    soPhieu =
                        $"TNBI-{DateTime.Now:yyyyMMddHHmmss}";
                }

                string nguoiTao = _txtNguoiTao.Text.Trim();

                if (string.IsNullOrWhiteSpace(nguoiTao))
                    nguoiTao = Environment.UserName;

                // ========================================================
                // HEADER
                // ========================================================

                var phieu = new PhieuTraHang
                {
                    Nguon = NguonXuLyBatThuong.TraNoiBo,

                    SoPhieu = soPhieu,

                    // TraNoiBo không dùng:
                    NguonKhachTra = null,
                    SoPhieuKhach = null,
                    NgayPhatHanh = null,
                    SlipNo = null,
                    Ca = null,
                    TenKhachHang = null,

                    // Dùng cho nội bộ
                    PhongBan = _txtPhongBan.Text.Trim(),

                    LyDo = _txtLyDo.Text.Trim(),

                    BoPhanPhatHienLoi =
                        _txtBoPhanPhatHienLoi.Text.Trim(),

                    XacNhanBPPhatHienLoi = null,
                    XacNhanQCKhach = null,
                    XacNhanNhaCungCap = null,

                    NgayNhanKho = null,

                    TongSoLuongNhan =
                        items.Sum(x => x.SoLuong),

                    // Service sẽ xử lý state ban đầu.
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

                // ========================================================
                // DETAIL
                // ========================================================

                foreach (var item in phieu.ChiTiet)
                {
                    item.Id = 0;
                    item.PhieuTraHangId = 0;

                    // TraNoiBo dùng Slot nguồn.
                    // Các thông tin đối chiếu phiếu giao
                    // ban đầu chưa có.
                    item.DinhDanhPhieuGiao = null;
                    item.PoNo = null;
                    item.NgayGiao = null;
                    item.NhaMay = null;
                }

                // ========================================================
                // SERVICE
                // ========================================================

                int id =
                    _traNoiBoService.TaoPhieuTraNoiBo(phieu);

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
                    $"Không thể tạo phiếu trả nội bộ.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
