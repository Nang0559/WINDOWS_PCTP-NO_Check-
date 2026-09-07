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

    public class FormQCXacNhanCuoi : XtraForm
    {
        private readonly IQTChungService _qtChungService;
        private readonly int _phieuXuLyId;

        // ============================================================
        // UI
        // ============================================================
        private LabelControl _lblSoPhieu;
        private LabelControl _lblModel;
        private LabelControl _lblMaSanPham;
        private LabelControl _lblLot;
        private LabelControl _lblSoLuongLoi;
        private LabelControl _lblTrangThai;

        private SpinEdit _spSoLuongOK;
        private SpinEdit _spSoLuongNG;
        private CheckEdit _chkKiemTraTem;

        // ── THÊM MỚI ──────────────────────────────────────────────────
        // QTChungService.QCXacNhanCuoi bắt buộc LotNo khi OK>0 hoặc NG>0,
        // và SlotIdOK/SlotIdNG tương ứng khi OK/NG > 0 — form gốc thiếu
        // hoàn toàn các input này nên mọi lần xác nhận đều bị Fail.
        private TextEdit _txtLotNhapLai;
        private SpinEdit _spSlotIdOK;
        private SpinEdit _spSlotIdNG;

        private TextEdit _txtNguoiQC;
        private MemoEdit _txtGhiChu;

        private SimpleButton _btnXacNhan;
        private SimpleButton _btnDong;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public FormQCXacNhanCuoi(
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
            Text = "QC xác nhận cuối";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 680);
            MinimizeBox = false;
            MaximizeBox = false;

            // Bố cục hàng (đã thêm 3 hàng mới so với bản gốc: LotNhapLai,
            // SlotIdOK, SlotIdNG — chèn sau CheckEdit KiemTraTem, trước Ghi chú):
            //   0  Title
            //   1  Số phiếu
            //   2  Model
            //   3  Mã sản phẩm
            //   4  Lot
            //   5  SL lỗi ban đầu
            //   6  Trạng thái
            //   7  Số lượng OK
            //   8  Số lượng NG
            //   9  Kiểm tra tem
            //   10 Lot nhập lại      ← MỚI
            //   11 Slot OK           ← MỚI
            //   12 Slot NG           ← MỚI
            //   13 Ghi chú (percent, giãn hết phần còn lại)
            //   14 Button panel
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 15,
                Padding = new Padding(15)
            };

            main.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 170));
            main.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            // Title
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            // Rows 1..12: info + inputs, đều absolute 40
            for (int i = 1; i <= 12; i++)
                main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            // Row 13: Ghi chú — giãn hết phần còn lại
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            // Row 14: Button panel
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            // ========================================================
            // TITLE
            // ========================================================
            var lblTitle = new LabelControl
            {
                Text = "QC XÁC NHẬN KẾT QUẢ CUỐI",
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
            _lblMaSanPham = CreateValueLabel();
            _lblLot = CreateValueLabel();
            _lblSoLuongLoi = CreateValueLabel();
            _lblTrangThai = CreateValueLabel();

            AddRow(main, 1, "Số phiếu:", _lblSoPhieu);
            AddRow(main, 2, "Model:", _lblModel);
            AddRow(main, 3, "Mã sản phẩm:", _lblMaSanPham);
            AddRow(main, 4, "Lot:", _lblLot);
            AddRow(main, 5, "SL lỗi ban đầu:", _lblSoLuongLoi);
            AddRow(main, 6, "Trạng thái:", _lblTrangThai);

            // ========================================================
            // SỐ LƯỢNG OK
            // ========================================================
            var lblOK = CreateCaptionLabel("Số lượng OK:");
            main.Controls.Add(lblOK, 0, 7);

            _spSoLuongOK = new SpinEdit { Dock = DockStyle.Fill };
            _spSoLuongOK.Properties.IsFloatValue = false;
            _spSoLuongOK.Properties.MinValue = 0;
            _spSoLuongOK.Properties.MaxValue = 999999999;
            _spSoLuongOK.Properties.Buttons[0].Visible = true;
            _spSoLuongOK.EditValueChanged += SoLuong_EditValueChanged;
            main.Controls.Add(_spSoLuongOK, 1, 7);

            // ========================================================
            // SỐ LƯỢNG NG
            // ========================================================
            var lblNG = CreateCaptionLabel("Số lượng NG:");
            main.Controls.Add(lblNG, 0, 8);

            _spSoLuongNG = new SpinEdit { Dock = DockStyle.Fill };
            _spSoLuongNG.Properties.IsFloatValue = false;
            _spSoLuongNG.Properties.MinValue = 0;
            _spSoLuongNG.Properties.MaxValue = 999999999;
            _spSoLuongNG.EditValueChanged += SoLuong_EditValueChanged;
            main.Controls.Add(_spSoLuongNG, 1, 8);

            // ========================================================
            // KIỂM TRA TEM
            // ========================================================
            _chkKiemTraTem = new CheckEdit
            {
                Text = "Đã kiểm tra tem / Inspection",
                Dock = DockStyle.Fill
            };
            _chkKiemTraTem.Properties.Appearance.Font =
                new Font("Tahoma", 9F, FontStyle.Bold);
            main.Controls.Add(_chkKiemTraTem, 1, 9);

            // ========================================================
            // LOT NHẬP LẠI — MỚI
            // QTChungService.QCXacNhanCuoi yêu cầu lotNo khi OK>0 hoặc NG>0
            // (dùng làm LOT ghi vào ISlotService.AddQuantity / NhapLaiHangNG).
            // Mặc định = Lot gốc của phiếu, người dùng có thể sửa lại.
            // ========================================================
            var lblLotNhapLai = CreateCaptionLabel("Lot nhập lại:");
            main.Controls.Add(lblLotNhapLai, 0, 10);

            _txtLotNhapLai = new TextEdit { Dock = DockStyle.Fill };
            main.Controls.Add(_txtLotNhapLai, 1, 10);

            // ========================================================
            // SLOT OK — MỚI
            // Bắt buộc khi Số lượng OK > 0 (QTChungService validate SlotIdOK).
            // ========================================================
            var lblSlotOK = CreateCaptionLabel("Slot nhập OK:");
            main.Controls.Add(lblSlotOK, 0, 11);

            _spSlotIdOK = new SpinEdit { Dock = DockStyle.Fill };
            _spSlotIdOK.Properties.IsFloatValue = false;
            _spSlotIdOK.Properties.MinValue = 0;
            _spSlotIdOK.Properties.MaxValue = 999999999;
            _spSlotIdOK.Enabled = false;
            main.Controls.Add(_spSlotIdOK, 1, 11);

            // ========================================================
            // SLOT NG — MỚI
            // Bắt buộc khi Số lượng NG > 0 (QTChungService validate SlotIdNG).
            // ========================================================
            var lblSlotNG = CreateCaptionLabel("Slot nhập NG:");
            main.Controls.Add(lblSlotNG, 0, 12);

            _spSlotIdNG = new SpinEdit { Dock = DockStyle.Fill };
            _spSlotIdNG.Properties.IsFloatValue = false;
            _spSlotIdNG.Properties.MinValue = 0;
            _spSlotIdNG.Properties.MaxValue = 999999999;
            _spSlotIdNG.Enabled = false;
            main.Controls.Add(_spSlotIdNG, 1, 12);

            // ========================================================
            // GHI CHÚ
            // ========================================================
            var notePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            notePanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 170));
            notePanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            var lblNote = CreateCaptionLabel("Ghi chú:");
            _txtGhiChu = new MemoEdit { Dock = DockStyle.Fill };

            notePanel.Controls.Add(lblNote, 0, 0);
            notePanel.Controls.Add(_txtGhiChu, 1, 0);

            main.Controls.Add(notePanel, 0, 13);
            main.SetColumnSpan(notePanel, 2);

            // ========================================================
            // BUTTON PANEL
            // ========================================================
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3
            };
            buttonPanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));
            buttonPanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 140));
            buttonPanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 120));

            // Người QC
            _txtNguoiQC = new TextEdit
            {
                Dock = DockStyle.Fill,
                Text = Environment.UserName
            };
            buttonPanel.Controls.Add(_txtNguoiQC, 0, 0);

            _btnXacNhan = new SimpleButton
            {
                Text = "✔ Xác nhận",
                Dock = DockStyle.Fill
            };
            _btnXacNhan.Appearance.Font =
                new Font("Tahoma", 9.5F, FontStyle.Bold);
            _btnXacNhan.Click += BtnXacNhan_Click;
            buttonPanel.Controls.Add(_btnXacNhan, 1, 0);

            _btnDong = new SimpleButton
            {
                Text = "Đóng",
                Dock = DockStyle.Fill
            };
            _btnDong.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttonPanel.Controls.Add(_btnDong, 2, 0);

            _txtNguoiQC.Properties.NullValuePrompt = "Người QC thực hiện";

            main.Controls.Add(buttonPanel, 0, 14);
            main.SetColumnSpan(buttonPanel, 2);

            Controls.Add(main);
            AcceptButton = _btnXacNhan;
            CancelButton = _btnDong;
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

        private static LabelControl CreateCaptionLabel(string text)
        {
            return new LabelControl
            {
                Text = text,
                Dock = DockStyle.Fill,
                Appearance =
                {
                    Font = new Font("Tahoma", 9F, FontStyle.Bold)
                }
            };
        }

        private static void AddRow(
            TableLayoutPanel panel,
            int row,
            string caption,
            Control value)
        {
            panel.Controls.Add(CreateCaptionLabel(caption), 0, row);
            panel.Controls.Add(value, 1, row);
        }

        // ============================================================
        // LOAD
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

                _lblSoPhieu.Text = p.SoPhieu ?? "";
                _lblModel.Text = p.Model ?? "";
                _lblMaSanPham.Text = p.MaSanPham ?? "";
                _lblLot.Text = p.SoLo ?? "";
                _lblSoLuongLoi.Text = p.SoLuongLoi.ToString("n0");

                // ── ĐÃ SỬA ──────────────────────────────────────────────
                // IQTChungService.GetTrangThai(int) đã bị bỏ khỏi
                // QTChungService (method "14. GET STATUS" bị comment out
                // trong service thật) — gọi lại sẽ lỗi biên dịch.
                // Trạng thái nay lấy trực tiếp từ PhieuXuLyBatThuong.Status.
                var status = p.Status;
                _lblTrangThai.Text = status.ToString();

                // ====================================================
                // CHỈ ĐƯỢC QC XÁC NHẬN CUỐI KHI: DaGiaoSanXuat
                // (khớp ReworkMap[DaGiaoSanXuat] = { DaQCXacNhanCuoi, Huy }
                // trong state machine thật của QTChungService)
                // ====================================================
                if (status != QTChungStatus.DaGiaoSanXuat)
                {
                    _btnXacNhan.Enabled = false;
                    XtraMessageBox.Show(
                        $"Phiếu hiện đang ở trạng thái [{status}].\r\n\r\n" +
                        "Chỉ được QC xác nhận cuối khi phiếu đã giao cho sản xuất/rework.",
                        "Không thể xác nhận",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _spSoLuongOK.EditValue = 0;
                _spSoLuongNG.EditValue = 0;

                // Mặc định Lot nhập lại = Lot gốc của phiếu — người dùng
                // có thể sửa nếu QC muốn đặt lot mới cho phần nhập lại.
                _txtLotNhapLai.Text = p.SoLo ?? "";
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể tải thông tin phiếu.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        // ============================================================
        // TỰ KIỂM TRA TỔNG OK + NG
        // ============================================================
        private void SoLuong_EditValueChanged(object sender, EventArgs e)
        {
            int ok = Convert.ToInt32(_spSoLuongOK.EditValue ?? 0);
            int ng = Convert.ToInt32(_spSoLuongNG.EditValue ?? 0);
            int tong = ok + ng;
            int loi = GetSoLuongLoi();

            if (tong > loi)
            {
                _spSoLuongOK.EditValue = Math.Min(ok, loi);
                ok = Convert.ToInt32(_spSoLuongOK.EditValue ?? 0);
                _spSoLuongNG.EditValue = Math.Max(0, loi - ok);
            }

            // ── THÊM MỚI ──────────────────────────────────────────────
            // Chỉ bật (và coi là bắt buộc) Slot OK/NG khi số lượng tương
            // ứng > 0 — khớp điều kiện validate thật trong
            // QTChungService.QCXacNhanCuoi.
            ok = Convert.ToInt32(_spSoLuongOK.EditValue ?? 0);
            ng = Convert.ToInt32(_spSoLuongNG.EditValue ?? 0);

            _spSlotIdOK.Enabled = ok > 0;
            if (ok == 0) _spSlotIdOK.EditValue = 0;

            _spSlotIdNG.Enabled = ng > 0;
            if (ng == 0) _spSlotIdNG.EditValue = 0;
        }

        private int GetSoLuongLoi()
        {
            string text = _lblSoLuongLoi.Text
                .Replace(",", "")
                .Replace(".", "")
                .Trim();
            return int.TryParse(text, out int value) ? value : 0;
        }

        // ============================================================
        // XÁC NHẬN QC CUỐI
        // ============================================================
        private void BtnXacNhan_Click(object sender, EventArgs e)
        {
            int soLuongOK = Convert.ToInt32(_spSoLuongOK.EditValue ?? 0);
            int soLuongNG = Convert.ToInt32(_spSoLuongNG.EditValue ?? 0);
            int tong = soLuongOK + soLuongNG;
            int soLuongLoi = GetSoLuongLoi();

            if (tong != soLuongLoi)
            {
                XtraMessageBox.Show(
                    $"Tổng OK + NG phải bằng số lượng lỗi.\r\n\r\n" +
                    $"SL lỗi: {soLuongLoi:n0}\r\n" +
                    $"OK: {soLuongOK:n0}\r\n" +
                    $"NG: {soLuongNG:n0}\r\n" +
                    $"Tổng: {tong:n0}",
                    "Số lượng không hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // ── THÊM MỚI ──────────────────────────────────────────────
            // Validate trước ở UI cho đỡ tốn 1 round-trip service, nhưng
            // vẫn khớp đúng các điều kiện thật trong QTChungService.QCXacNhanCuoi.
            string lotNhapLai = _txtLotNhapLai.Text.Trim();
            int slotIdOKValue = Convert.ToInt32(_spSlotIdOK.EditValue ?? 0);
            int slotIdNGValue = Convert.ToInt32(_spSlotIdNG.EditValue ?? 0);

            if ((soLuongOK > 0 || soLuongNG > 0) &&
                string.IsNullOrWhiteSpace(lotNhapLai))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Lot nhập lại.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _txtLotNhapLai.Focus();
                return;
            }

            if (soLuongOK > 0 && slotIdOKValue <= 0)
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Slot nhập OK (có hàng OK cần nhập lại kho).",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _spSlotIdOK.Focus();
                return;
            }

            if (soLuongNG > 0 && slotIdNGValue <= 0)
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập Slot nhập NG (có hàng NG cần định tuyến riêng).",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _spSlotIdNG.Focus();
                return;
            }

            string nguoiQC = _txtNguoiQC.Text.Trim();
            if (string.IsNullOrWhiteSpace(nguoiQC))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập người QC.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _txtNguoiQC.Focus();
                return;
            }

            // ========================================================
            // KIỂM TRA KẾT QUẢ
            // ========================================================
            string ketLuan;
            if (soLuongNG == 0)
                ketLuan = "TẤT CẢ OK";
            else if (soLuongOK == 0)
                ketLuan = "TẤT CẢ NG";
            else
                ketLuan = $"OK {soLuongOK:n0} / NG {soLuongNG:n0}";

            var confirm = XtraMessageBox.Show(
                $"Xác nhận kết quả QC cuối?\r\n\r\n" +
                $"Phiếu: {_lblSoPhieu.Text}\r\n" +
                $"Mã hàng: {_lblMaSanPham.Text}\r\n" +
                $"Lot: {_lblLot.Text}\r\n" +
                $"Lot nhập lại: {lotNhapLai}\r\n\r\n" +
                $"OK: {soLuongOK:n0}" +
                (soLuongOK > 0 ? $" (Slot {slotIdOKValue})" : "") + "\r\n" +
                $"NG: {soLuongNG:n0}" +
                (soLuongNG > 0 ? $" (Slot {slotIdNGValue})" : "") + "\r\n" +
                $"Kết luận: {ketLuan}\r\n\r\n" +
                $"Người QC: {nguoiQC}",
                "Xác nhận QC cuối",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                _btnXacNhan.Enabled = false;

                // ====================================================
                // TOÀN BỘ NGHIỆP VỤ ĐI QUA IQTChungService
                //
                // ── ĐÃ SỬA ──────────────────────────────────────────
                // Form gốc chỉ truyền 4 tham số đầu, bỏ trống
                // slotIdOK/slotIdNG/lotNo (default null) — trong khi
                // QTChungService.QCXacNhanCuoi validate:
                //   soLuongOK>0  → bắt buộc slotIdOK
                //   soLuongNG>0  → bắt buộc slotIdNG
                //   OK>0 || NG>0 → bắt buộc lotNo
                // nên mọi lần xác nhận trước đây đều trả về
                // ScanResult.Fail. Nay truyền đủ tham số từ UI mới thêm.
                // ====================================================
                int? slotIdOKParam = soLuongOK > 0 ? (int?)slotIdOKValue : null;
                int? slotIdNGParam = soLuongNG > 0 ? (int?)slotIdNGValue : null;
                string lotNoParam =
                    (soLuongOK > 0 || soLuongNG > 0) ? lotNhapLai : null;

                ScanResult result =
                    _qtChungService.QCXacNhanCuoi(
                        _phieuXuLyId,
                        soLuongOK,
                        soLuongNG,
                        nguoiQC,
                        slotIdOKParam,
                        slotIdNGParam,
                        lotNoParam);

                if (result == null)
                {
                    XtraMessageBox.Show(
                        "QC xác nhận cuối không trả về kết quả.",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (!result.IsOK)
                {
                    XtraMessageBox.Show(
                        result.Message ?? "QC xác nhận cuối không thành công.",
                        "Không thành công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // ====================================================
                // THÀNH CÔNG
                //
                // Service đã tự quyết định:
                //   NG = 0  → HoanTat (đồng thời nhập lại OK ngay trong
                //             QCXacNhanCuoi qua NhapLaiHangOK)
                //   NG > 0  → NhapLaiHangNG → DaNhapLaiKho → HoanTat
                //             (tất cả trong cùng 1 lần gọi QCXacNhanCuoi,
                //             không cần form gọi thêm NhapLaiHangNG riêng)
                // ====================================================
                XtraMessageBox.Show(
                    result.Message ??
                    $"QC đã xác nhận thành công.\r\n\r\n" +
                    $"OK: {soLuongOK:n0}\r\n" +
                    $"NG: {soLuongNG:n0}",
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Lỗi QC xác nhận cuối:\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _btnXacNhan.Enabled = true;
            }
        }
    }
}