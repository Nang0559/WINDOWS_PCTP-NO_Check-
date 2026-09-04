

namespace PCTP.Modules.XuLyHangLoi
{
    using DevExpress.XtraEditors;
    using PCTP.Modules.XuLyHangLoi.Services;
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    public partial class FormNhapLaiHangNG : XtraForm
    {
        private readonly IReworkStockService _reworkStockService;
        private readonly int _phieuXuLyId;
        private readonly int _soLuongNGToiDa;

        // ============================================================
        // UI
        // ============================================================

        private TextEdit _txtLotNo;
        private SpinEdit _spnSoLuong;
        private SpinEdit _spnSlotOK;
        private SpinEdit _spnSlotNG;

        private SimpleButton _btnNhap;
        private SimpleButton _btnHuy;

        private LabelControl _lblPhieu;
        private LabelControl _lblSoLuongNG;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public FormNhapLaiHangNG(
            IReworkStockService reworkStockService,
            int phieuXuLyId,
            int soLuongNG)
        {
            _reworkStockService = reworkStockService
                ?? throw new ArgumentNullException(nameof(reworkStockService));

            if (phieuXuLyId <= 0)
                throw new ArgumentException(
                    "PhieuXuLyId không hợp lệ.",
                    nameof(phieuXuLyId));

            if (soLuongNG <= 0)
                throw new ArgumentException(
                    "Số lượng NG phải lớn hơn 0.",
                    nameof(soLuongNG));

            _phieuXuLyId = phieuXuLyId;
            _soLuongNGToiDa = soLuongNG;

            BuildUI();
        }

        // ============================================================
        // UI
        // ============================================================

        private void BuildUI()
        {
            Text = "Nhập lại hàng NG";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(520, 330);
            MinimizeBox = false;
            MaximizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(15)
            };

            root.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 150));

            root.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            root.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 35));

            root.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 35));

            root.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

            root.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

            root.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

            root.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 45));

            root.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            // --------------------------------------------------------
            // Phiếu
            // --------------------------------------------------------

            root.Controls.Add(
                new LabelControl
                {
                    Text = "Phiếu xử lý:",
                    Dock = DockStyle.Fill,
                    Appearance =
                    {
                    Font = new Font("Tahoma", 9F, FontStyle.Bold)
                    }
                },
                0, 0);

            _lblPhieu = new LabelControl
            {
                Text = _phieuXuLyId.ToString(),
                Dock = DockStyle.Fill
            };

            root.Controls.Add(_lblPhieu, 1, 0);

            // --------------------------------------------------------
            // SL NG
            // --------------------------------------------------------

            root.Controls.Add(
                new LabelControl
                {
                    Text = "SL NG QC xác nhận:",
                    Dock = DockStyle.Fill
                },
                0, 1);

            _lblSoLuongNG = new LabelControl
            {
                Text = _soLuongNGToiDa.ToString("N0"),
                Dock = DockStyle.Fill,
                Appearance =
            {
                Font = new Font("Tahoma", 10F, FontStyle.Bold)
            }
            };

            root.Controls.Add(_lblSoLuongNG, 1, 1);

            // --------------------------------------------------------
            // LOT
            // --------------------------------------------------------

            root.Controls.Add(
                new LabelControl
                {
                    Text = "Lot nhập:",
                    Dock = DockStyle.Fill
                },
                0, 2);

            _txtLotNo = new TextEdit
            {
                Dock = DockStyle.Fill
            };

            _txtLotNo.Properties.NullValuePrompt =
                "Nhập / scan Lot No...";

            root.Controls.Add(_txtLotNo, 1, 2);

            // --------------------------------------------------------
            // Số lượng
            // --------------------------------------------------------

            root.Controls.Add(
                new LabelControl
                {
                    Text = "Số lượng NG:",
                    Dock = DockStyle.Fill
                },
                0, 3);

            _spnSoLuong = new SpinEdit
            {
                Dock = DockStyle.Fill
            };

            _spnSoLuong.Properties.IsFloatValue = false;
            _spnSoLuong.Properties.MinValue = 1;
            _spnSoLuong.Properties.MaxValue = _soLuongNGToiDa;
            _spnSoLuong.EditValue = _soLuongNGToiDa;

            root.Controls.Add(_spnSoLuong, 1, 3);

            // --------------------------------------------------------
            // Slot OK
            // --------------------------------------------------------

            root.Controls.Add(
                new LabelControl
                {
                    Text = "Slot OK:",
                    Dock = DockStyle.Fill
                },
                0, 4);

            _spnSlotOK = new SpinEdit
            {
                Dock = DockStyle.Fill
            };

            _spnSlotOK.Properties.IsFloatValue = false;
            _spnSlotOK.Properties.MinValue = 0;
            _spnSlotOK.Properties.MaxValue = int.MaxValue;
            _spnSlotOK.EditValue = 0;

            root.Controls.Add(_spnSlotOK, 1, 4);

            // --------------------------------------------------------
            // Slot NG
            // --------------------------------------------------------

            root.Controls.Add(
                new LabelControl
                {
                    Text = "Slot NG:",
                    Dock = DockStyle.Fill
                },
                0, 5);

            _spnSlotNG = new SpinEdit
            {
                Dock = DockStyle.Fill
            };

            _spnSlotNG.Properties.IsFloatValue = false;
            _spnSlotNG.Properties.MinValue = 0;
            _spnSlotNG.Properties.MaxValue = int.MaxValue;
            _spnSlotNG.EditValue = 0;

            root.Controls.Add(_spnSlotNG, 1, 5);

            // --------------------------------------------------------
            // Buttons
            // --------------------------------------------------------

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            _btnHuy = new SimpleButton
            {
                Text = "Hủy",
                Width = 100,
                Height = 32
            };

            _btnHuy.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnNhap = new SimpleButton
            {
                Text = "📥 Nhập lại",
                Width = 130,
                Height = 32
            };

            _btnNhap.Appearance.Font =
                new Font("Tahoma", 9F, FontStyle.Bold);

            _btnNhap.Click += BtnNhap_Click;

            buttonPanel.Controls.Add(_btnHuy);
            buttonPanel.Controls.Add(_btnNhap);

            root.Controls.Add(buttonPanel, 0, 6);
            root.SetColumnSpan(buttonPanel, 2);

            Controls.Add(root);

            AcceptButton = _btnNhap;
            CancelButton = _btnHuy;
        }

        // ============================================================
        // NHẬP LẠI
        // ============================================================

        private void BtnNhap_Click(object sender, EventArgs e)
        {
            try
            {
                string lotNo = _txtLotNo.Text.Trim();

                if (string.IsNullOrWhiteSpace(lotNo))
                {
                    XtraMessageBox.Show(
                        this,
                        "Vui lòng nhập hoặc scan Lot No.",
                        "Thiếu thông tin",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    _txtLotNo.Focus();
                    return;
                }

                int soLuongNG = Convert.ToInt32(
                    _spnSoLuong.EditValue ?? 0);

                if (soLuongNG <= 0)
                {
                    XtraMessageBox.Show(
                        this,
                        "Số lượng NG phải lớn hơn 0.",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                if (soLuongNG > _soLuongNGToiDa)
                {
                    XtraMessageBox.Show(
                        this,
                        $"Số lượng nhập NG không được vượt quá " +
                        $"{_soLuongNGToiDa:N0}.",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                int? slotIdOK = GetNullableSlotId(_spnSlotOK);
                int? slotIdNG = GetNullableSlotId(_spnSlotNG);

                string nguoiNhap = Environment.UserName;

                var result = _reworkStockService.NhapLaiHangNG(
                    _phieuXuLyId,
                    lotNo,
                    soLuongNG,
                    slotIdOK,
                    slotIdNG,
                    nguoiNhap);

                if (result == null)
                {
                    XtraMessageBox.Show(
                        this,
                        "Không nhận được kết quả từ nghiệp vụ nhập lại.",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                if (!result.IsOK)
                {
                    XtraMessageBox.Show(
                        this,
                        result.Message ?? "Không thể nhập lại hàng NG.",
                        "Không thành công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                XtraMessageBox.Show(
                    this,
                    result.Message ?? "Đã nhập lại hàng NG thành công.",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    this,
                    $"Lỗi nhập lại hàng NG:\r\n{ex.Message}",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // SLOT
        // ============================================================

        private int? GetNullableSlotId(SpinEdit editor)
        {
            int value = Convert.ToInt32(
                editor.EditValue ?? 0);

            return value > 0 ? value : (int?)null;
        }
    }
}