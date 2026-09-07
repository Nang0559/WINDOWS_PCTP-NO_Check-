using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using PCTP.Modules.XuLyHangLoi.Enums;
    using PCTP.Modules.XuLyHangLoi.Services;
    using PCTP.Shared.Helpers;
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    public partial class FormQCDinhHuong : XtraForm
    {
        private readonly IQTChungService _qtChungService;
        private readonly int _phieuXuLyId;

        // ============================================================
        // UI
        // ============================================================
        private LabelControl _lblSoPhieu;
        private LabelControl _lblModel;
        private LabelControl _lblMaHang;
        private LabelControl _lblLot;
        private LabelControl _lblSoLuong;
        private LabelControl _lblTrangThai;

        private ComboBoxEdit _cboHuongXuLy;
        private MemoEdit _txtGhiChu;
        private TextEdit _txtNguoiThucHien;

        private SimpleButton _btnXacNhan;
        private SimpleButton _btnHuy;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public FormQCDinhHuong(
            IQTChungService qtChungService,
            int phieuXuLyId)
        {
            _qtChungService = qtChungService
                ?? throw new ArgumentNullException(nameof(qtChungService));

            if (phieuXuLyId <= 0)
                throw new ArgumentException(
                    "PhieuXuLyId không hợp lệ.",
                    nameof(phieuXuLyId));

            _phieuXuLyId = phieuXuLyId;

            BuildUI();
            LoadData();
        }

        // ============================================================
        // BUILD UI
        // ============================================================
        private void BuildUI()
        {
            Text = "QC định hướng xử lý hàng lỗi";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 520);
            MinimizeBox = false;
            MaximizeBox = false;

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(15)
            };

            main.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 150));
            main.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            main.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 38));
            for (int i = 1; i < 8; i++)
            {
                main.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 42));
            }
            main.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            // ========================================================
            // TIÊU ĐỀ
            // ========================================================
            var lblTitle = new LabelControl
            {
                Text = "QC ĐỊNH HƯỚNG XỬ LÝ",
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 13F, FontStyle.Bold),
                    ForeColor = Color.DarkBlue
                }
            };
            main.Controls.Add(lblTitle, 0, 0);
            main.SetColumnSpan(lblTitle, 2);

            // ========================================================
            // THÔNG TIN PHIẾU
            // ========================================================
            _lblSoPhieu = CreateValueLabel();
            _lblModel = CreateValueLabel();
            _lblMaHang = CreateValueLabel();
            _lblLot = CreateValueLabel();
            _lblSoLuong = CreateValueLabel();
            _lblTrangThai = CreateValueLabel();

            AddRow(main, 1, "Số phiếu:", _lblSoPhieu);
            AddRow(main, 2, "Model:", _lblModel);
            AddRow(main, 3, "Mã hàng:", _lblMaHang);
            AddRow(main, 4, "Lot:", _lblLot);
            AddRow(main, 5, "Số lượng lỗi:", _lblSoLuong);
            AddRow(main, 6, "Trạng thái:", _lblTrangThai);

            // ========================================================
            // HƯỚNG XỬ LÝ
            // ========================================================
            var lblHuong = new LabelControl
            {
                Text = "Hướng xử lý:",
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 9F, FontStyle.Bold)
                }
            };
            main.Controls.Add(lblHuong, 0, 7);

            _cboHuongXuLy = new ComboBoxEdit
            {
                Dock = DockStyle.Fill
            };
            _cboHuongXuLy.Properties.TextEditStyle =
                DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            // Thứ tự khớp với HuongXuLyBatThuong (ChuaXacDinh=0 không hiển thị):
            //   TuChoiGiaoBu = 1, ChiGiaoBu = 2, CanRework = 3
            _cboHuongXuLy.Properties.Items.Add(
                "Từ chối giao bù");
            _cboHuongXuLy.Properties.Items.Add(
                "Chỉ giao bù");
            _cboHuongXuLy.Properties.Items.Add(
                "Cần Rework");
            _cboHuongXuLy.SelectedIndex = -1;

            main.Controls.Add(_cboHuongXuLy, 1, 7);

            // ========================================================
            // BOTTOM
            // ========================================================
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2
            };
            bottom.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 130));
            bottom.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 130));
            bottom.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));
            bottom.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 40));

            // Ghi chú
            var lblGhiChu = new LabelControl
            {
                Text = "Ghi chú:",
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 9F, FontStyle.Bold)
                }
            };
            bottom.Controls.Add(lblGhiChu, 0, 0);

            _txtGhiChu = new MemoEdit
            {
                Dock = DockStyle.Fill
            };
            bottom.Controls.Add(_txtGhiChu, 0, 1);

            // Người thực hiện
            var pnlNguoi = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            pnlNguoi.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 100));
            pnlNguoi.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            var lblNguoi = new LabelControl
            {
                Text = "Người thực hiện:",
                Dock = DockStyle.Fill
            };
            _txtNguoiThucHien = new TextEdit
            {
                Dock = DockStyle.Fill,
                Text = Environment.UserName
            };
            pnlNguoi.Controls.Add(lblNguoi, 0, 0);
            pnlNguoi.Controls.Add(_txtNguoiThucHien, 1, 0);

            bottom.Controls.Add(pnlNguoi, 1, 0);
            bottom.SetColumnSpan(pnlNguoi, 2);

            // Buttons
            _btnXacNhan = new SimpleButton
            {
                Text = "✔ Xác nhận",
                Dock = DockStyle.Fill
            };
            _btnXacNhan.Appearance.Font =
                new Font("Tahoma", 9F, FontStyle.Bold);
            _btnXacNhan.Click += BtnXacNhan_Click;

            _btnHuy = new SimpleButton
            {
                Text = "Đóng",
                Dock = DockStyle.Fill
            };
            _btnHuy.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            bottom.Controls.Add(_btnXacNhan, 1, 1);
            bottom.Controls.Add(_btnHuy, 2, 1);

            main.Controls.Add(bottom, 0, 8);
            main.SetColumnSpan(bottom, 2);

            Controls.Add(main);
            AcceptButton = _btnXacNhan;
            CancelButton = _btnHuy;
        }

        // ============================================================
        // UI HELPER
        // ============================================================
        private static LabelControl CreateValueLabel()
        {
            return new LabelControl
            {
                Dock = DockStyle.Fill,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance =
                {
                    Font = new Font("Tahoma", 9F)
                }
            };
        }

        private static void AddRow(
            TableLayoutPanel panel,
            int row,
            string caption,
            Control value)
        {
            var label = new LabelControl
            {
                Text = caption,
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 9F, FontStyle.Bold)
                }
            };
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(value, 1, row);
        }

        // ============================================================
        // LOAD DATA
        // ============================================================
        private void LoadData()
        {
            try
            {
                var p = _qtChungService.GetById(_phieuXuLyId);
                if (p == null)
                {
                    XtraMessageBox.Show(
                        "Không tìm thấy phiếu xử lý bất thường.",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                _lblSoPhieu.Text = p.SoPhieu;
                _lblModel.Text = p.Model;
                _lblMaHang.Text = p.MaSanPham;
                _lblLot.Text = p.SoLo;
                _lblSoLuong.Text = p.SoLuongLoi.ToString("n0");

                // ── ĐÃ SỬA ──────────────────────────────────────────────
                // IQTChungService.GetTrangThai(int) đã bị bỏ khỏi
                // QTChungService (method "14. GET STATUS" hiện bị comment
                // out trong service) — gọi lại sẽ lỗi biên dịch vì
                // interface không còn khai báo method này.
                // Trạng thái nay lấy trực tiếp từ PhieuXuLyBatThuong.Status
                // (đã có sẵn trong đối tượng trả về từ GetById).
                var status = p.Status;
                _lblTrangThai.Text = status.ToString();

                // Chỉ cho định hướng khi đang ở trạng thái
                // DaTaoPhieuBatThuong — khớp điều kiện thật trong
                // QTChungService.QCDinhHuong: "if (phieu.Status !=
                // QTChungStatus.DaTaoPhieuBatThuong) return ScanResult.Fail(...)".
                if (status != QTChungStatus.DaTaoPhieuBatThuong)
                {
                    _btnXacNhan.Enabled = false;
                    XtraMessageBox.Show(
                        $"Phiếu hiện đang ở trạng thái [{status}].\r\n\r\n" +
                        "Không thể thực hiện QC định hướng tại trạng thái này.",
                        "Không thể định hướng",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Không thể tải thông tin phiếu.\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        // ============================================================
        // XÁC NHẬN
        // ============================================================
        private void BtnXacNhan_Click(object sender, EventArgs e)
        {
            if (_cboHuongXuLy.SelectedIndex < 0)
            {
                XtraMessageBox.Show(
                    "Vui lòng chọn hướng xử lý.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _cboHuongXuLy.Focus();
                return;
            }

            string nguoiThucHien =
                _txtNguoiThucHien.Text.Trim();

            if (string.IsNullOrWhiteSpace(nguoiThucHien))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập người thực hiện.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _txtNguoiThucHien.Focus();
                return;
            }

            HuongXuLyBatThuong huong;
            switch (_cboHuongXuLy.SelectedIndex)
            {
                case 0:
                    huong = HuongXuLyBatThuong.TuChoiGiaoBu;
                    break;
                case 1:
                    huong = HuongXuLyBatThuong.ChiGiaoBu;
                    break;
                case 2:
                    huong = HuongXuLyBatThuong.CanRework;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Hướng xử lý không hợp lệ.");
            }

            string tenHuong = GetTenHuong(huong);

            var confirm = XtraMessageBox.Show(
                $"Xác nhận hướng xử lý:\r\n\r\n" +
                $"Phiếu: {_lblSoPhieu.Text}\r\n" +
                $"Mã hàng: {_lblMaHang.Text}\r\n" +
                $"Lot: {_lblLot.Text}\r\n" +
                $"Hướng: {tenHuong}\r\n\r\n" +
                $"Người thực hiện: {nguoiThucHien}",
                "Xác nhận QC định hướng",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                _btnXacNhan.Enabled = false;

                // Tên phương thức và kiểu ScanResult (PCTP.Shared.Helpers)
                // đúng như QTChungService.QCDinhHuong thật:
                //   public ScanResult QCDinhHuong(int phieuXuLyId,
                //       HuongXuLyBatThuong huong, string nguoiThucHien)
                // (Lưu ý: đây KHÔNG phải cùng class ScanResult dùng cho
                // quét QR ở Applications.Services — class đó có .Success,
                // còn class này ở Shared.Helpers có .IsOK / .OK() / .Fail().)
                ScanResult result =
                    _qtChungService.QCDinhHuong(
                        _phieuXuLyId,
                        huong,
                        nguoiThucHien);

                if (result == null)
                {
                    XtraMessageBox.Show(
                        "QC định hướng không trả về kết quả.",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (!result.IsOK)
                {
                    XtraMessageBox.Show(
                        result.Message ?? "QC định hướng không thành công.",
                        "Không thành công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                XtraMessageBox.Show(
                    result.Message ??
                    $"Đã định hướng: {tenHuong}.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    $"Lỗi QC định hướng:\r\n\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _btnXacNhan.Enabled = true;
            }
        }

        // ============================================================
        // DISPLAY
        // ============================================================
        private static string GetTenHuong(
            HuongXuLyBatThuong huong)
        {
            switch (huong)
            {
                case HuongXuLyBatThuong.TuChoiGiaoBu:
                    return "Từ chối giao bù";
                case HuongXuLyBatThuong.ChiGiaoBu:
                    return "Chỉ giao bù";
                case HuongXuLyBatThuong.CanRework:
                    return "Cần Rework";
                default:
                    return huong.ToString();
            }
        }
    }
}