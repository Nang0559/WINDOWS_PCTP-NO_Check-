using PCTP.Applications.Services;
using PCTP.Domain.Events;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Presentation.Views;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Presentation.Presenters
{
    internal sealed class DocQrPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IDocQrView _v;
        private bool _awaitingSlMismatchConfirmation;
        private bool _scanBlocking;

        internal DocQrPresenter(HVNPresenterContext context)
        {
            _c = context;
            _v = _c.DocQrView;
            var v = _v;
            v.DocQRCodeClicked += OnDocQRCode;
            v.QRCodeSubmitted += OnQRCodeSubmitted;
            v.XoaDongQRClicked += OnXoaDongQR;
            v.XoaToanBoQRClicked += OnXoaToanBoQR;
            _c.Bus.Subscribe<QRScannedEvent>(OnQRScanned);
        }

        private void OnDocQRCode(object sender, EventArgs e)
        {
            if (!_c.IsMayBanQR)
            {
                _v.ShowInfo("Bạn chỉ sử dụng được tính năng này trên máy bắn QR.");
                return;
            }

            // DOCQRCODE/TMP luôn là source of truth khi đã có phiên QR.
            // Không được lấy ngày/nhà máy/giờ hiện tại trên UI để mở lại một
            // phiên đang tồn tại. Trường hợp điển hình: TMP/DOCQRCODE đang là
            // 15H nhưng người dùng đang đứng ở radio 6H; trước đây code bên
            // dưới vẫn BeginDeliverySession(6H) rồi LoadAll(), làm dữ liệu
            // 15H hiển thị dưới header 6H và toàn bộ lock bị sai ngữ cảnh.
            if (TryRestoreExistingQrSession())
                return;

            if (!_c.PhieuView.CoHangChuaOK())
            {
                _v.ShowInfo("Phiếu không đủ điều kiện để đọc QRCODE. Hoặc đã đọc xong dữ liệu.");
                return;
            }

            // Category của phiếu hiện tại là source of truth.
            // Không suy diễn SP từ giờ xuất hiện tại vì SP không phải luồng theo giờ.
            bool isSP = _c.Cfg.Delivery.CoLoaiSP && _c.PhieuView.IsLoaiSP;

            // SP dùng phạm vi ngày + nhà máy + dock, không dùng giờ hiện tại.
            _c.QrSvc.SetCheDoBan(isSP || _c.Cfg.Delivery.LoadTuBangRieng ? "" : _c.GioXuatHienTai.MoTa);
            _c.QrSvc.SetCheDoBanSP(isSP);

            DataTable dtPhieu = _c.Cfg.Delivery.LoadTuBangRieng ? _c.PhieuView.GetDonHangTable() : null;
            string ngay = _c.PhieuView.SelectedDate.ToString("yyyy-MM-dd");
            List<string> gios = _c.Cfg.Delivery.CoGear ? _c.YmvnView.GetCheckedGioXuat() : null;
            _c.IsBanQR = true;
            _c.BeginDeliverySession(
                _c.PhieuView.SelectedDate,
                _c.AddNM,
                isSP || _c.Cfg.Delivery.CoGear || _c.Cfg.Delivery.LoadTuBangRieng
                    ? string.Empty
                    : _c.GioXuatHienTai.Ma,
                _c.GetNhaMay(),
                isSP);

            // Khoá session context NGAY + NHÀ MÁY + GIỜ ngay trước khi chạy
            // đồng bộ bất đồng bộ. Không để người dùng đổi selector trong
            // khoảng thời gian TMP đang được chuẩn bị cho DOCQRCODE.
            _v.LockDocQrDeliveryContext(
                isSP,
                isSP || _c.Cfg.Delivery.CoGear || _c.Cfg.Delivery.LoadTuBangRieng
                    ? string.Empty
                    : _c.GioXuatHienTai.Ma);

            _c.RunWithLoading(() =>
            {
                try
                {
                    if (_c.Cfg.Delivery.LoadTuBangRieng)
                    {
                        if (_c.QrSvc.CountChuaDG() == 0 && !_c.QrSvc.CoDocQRNao())
                            _c.PhieuSvc.SyncPhieuTuBangRiengChoDocQR(dtPhieu, ngay, gios);
                    }
                    else
                    {
                        if (_c.QrSvc.CountChuaDG() == 0 && !_c.QrSvc.CoDocQRNao())
                            _c.PhieuSvc.SyncIfsPhieuChoDocQR(ngay, _c.GetNhaMay(), _c.GioXuatHienTai.Ma, _c.GioXuatHienTai.MoTa, _c.AddNM);
                    }

                    DataTable fifoOrderRows = _c.PhieuView.GetDonHangTable();
                    _c.QrSvc.InitializeFifo(fifoOrderRows);
                    DataTable qrData = _c.QrSvc.LoadAll();
                    _c.UiContext.Post(_ =>
                    {
                        _v.BindDocQRCode(qrData);
                        _v.SwitchToDocQRView();
                        _v.HideDocQrQuantityEditPanel();
                    }, null);
                }
                catch (Exception ex)
                {
                    _c.IsBanQR = false;
                    _c.ClearDeliverySession();
                    _v.UnlockDocQrDeliveryContext();
                    if (_c.Cfg.Delivery.CoGear)
                        _c.YmvnView.UnlockCheckListYMVN();
                    _c.PhieuView.UnlockAllRadio();
                    _c.PhieuView.UnlockDatePicker();
                    _v.ShowError($"Lỗi chuẩn bị dữ liệu QR: {ex.Message}");
                }
            }, "Đang chuẩn bị dữ liệu QR...");
        }

        /// <summary>
        /// Nếu DOCQRCODE/TMP đã có dữ liệu thì khôi phục đúng session từ DB
        /// và chuyển thẳng sang QR view. Tuyệt đối không dùng context hiện tại
        /// trên header làm identity cho dữ liệu QR đã tồn tại.
        /// </summary>
        private bool TryRestoreExistingQrSession()
        {
            var tt = _c.PhieuSvc.GetTrangThaiDangBan();
            bool isSP = false;

            if (!tt.DangBan && _c.Cfg.Delivery.CoConfigSP)
            {
                var sp = _c.PhieuSvc.GetTrangThaiDangBanSP();
                if (sp.DangBan)
                {
                    tt = sp;
                    isSP = true;
                }
            }

            if (!tt.DangBan)
                return false;

            if (tt.DataKhongKhop)
            {
                _v.ShowError(
                    "DOCQRCODE/TMP đang có dữ liệu nhưng session không hợp lệ. " +
                    "Không thể mở một session mới trên context khác.");
                return true;
            }

            if (!DateTime.TryParse(tt.NgayGiao, out DateTime ngay))
            {
                _v.ShowError("Không xác định được Ngày Xuất Hàng của phiên DOCQRCODE đang tồn tại.");
                return true;
            }

            _c.IsBanQR = true;
            _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay
                ? tt.AddNM
                : _c.Cfg.Delivery.AddNmMacDinh;

            _v.SetDate(ngay.Date);
            if (_c.Cfg.Delivery.CoNhieuNhaMay)
                _v.SetTab(_c.AddNM);

            if (_c.Cfg.Delivery.CoGear)
            {
                bool gearSp = _c.CategoryResolver.Resolve(
                    new OrderLoadContext { GioFccMoTa = tt.GioGiaoFCC }) == OrderCategory.SP;
                isSP = gearSp;
            }

            string ma = string.Empty;
            string mota = string.Empty;

            if (!isSP && !_c.Cfg.Delivery.LoadTuBangRieng)
            {
                string gio = tt.GioGiaoFCC ?? string.Empty;
                var ds = _c.AddNM == 1
                    ? _c.GioXuatRepo.GetDanhSachGioVP()
                    : _c.GioXuatRepo.GetDanhSachGioHN();

                foreach (var g in ds)
                {
                    string mb = GioXuatRepository.ParseGioThuong(g.MoTa);
                    if (mb.Contains($"'{gio}'"))
                    {
                        ma = g.Ma;
                        mota = g.MoTa;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(ma))
                {
                    ma = $"'{gio}'";
                    mota = gio + "H";
                }

                _c.SuspendGioXuatChanged();
                try
                {
                    _c.GioXuatHienTai = new GioXuat(ma, mota);
                    _c.GiaoDbView.UpdateGioXuatFromDB(ma);
                }
                finally
                {
                    _c.ResumeGioXuatChanged();
                }

                _c.QrSvc.SetCheDoBan(mota);
                _c.QrSvc.SetCheDoBanSP(false);
            }
            else
            {
                _c.QrSvc.SetCheDoBan(string.Empty);
                _c.QrSvc.SetCheDoBanSP(isSP);
            }

            _c.BeginDeliverySession(
                ngay.Date,
                _c.AddNM,
                isSP || _c.Cfg.Delivery.CoGear || _c.Cfg.Delivery.LoadTuBangRieng
                    ? string.Empty
                    : ma,
                _c.GetNhaMay(),
                isSP);

            _c.DocQrView.LockDocQrDeliveryContext(
                isSP,
                isSP || _c.Cfg.Delivery.CoGear || _c.Cfg.Delivery.LoadTuBangRieng
                    ? string.Empty
                    : ma);

            DataTable qrData = _c.QrSvc.LoadAll();
            _v.BindDocQRCode(qrData);
            _v.SwitchToDocQRView();
            _v.HideDocQrQuantityEditPanel();

            return true;
        }

        private void OnQRCodeSubmitted(object sender, string rawQr)
        {
            if (_awaitingSlMismatchConfirmation || _scanBlocking)
            {
                _v.ClearQRInput();
                return;
            }

            if (_c.Cfg.Delivery.CoGear)
            {
                DataTable orderRows = _c.PhieuView.GetDonHangTable();
                DataTable scannedRows = _c.QrSvc.LoadAll();
                string ymvnError;
                if (!YmvnGearQuantityValidator.ValidateScan(rawQr, orderRows, scannedRows, out ymvnError))
                {
                    ShowBlockingScanError(ymvnError);
                    return;
                }
            }

            ScanResult result = _c.Cfg.Delivery.CoGear
                ? _c.QrSvc.ProcessScanYMVN(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl))
                : _c.QrSvc.ProcessScan(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl));

            if (result.IsOK) return;

            if (IsBlockingScanError(result.Message))
            {
                ShowBlockingScanError(result.Message);
                return;
            }

            if (result.IsSlKhongKhop)
            {
                _v.ClearQRInput();

                var temInfo = _v.GetFocusedDocQRTemInfo();
                if (temInfo.SlFcc <= 0)
                {
                    _v.ShowError("Không xác định được số lượng tem FCC để xác nhận chênh lệch. QR chưa được ghi nhận.");
                    return;
                }

                _awaitingSlMismatchConfirmation = true;
                _v.SetDocQrScanInputEnabled(false);

                try
                {
                    bool accepted = DirectClickConfirmDialog.Show(
                        $"Số lượng tem khách hàng không khớp số lượng tem FCC.\r\n\r\n" +
                        $"Số lượng FCC: {temInfo.SlFcc}\r\n" +
                        $"Số lượng khách hàng: {result.Pending.SlTemHVN}\r\n\r\n" +
                        "Đồng ý để ghi nhận QR này và tiếp tục scan dòng tiếp theo?\r\n" +
                        "Lưu ý: số lượng nghiệp vụ luôn lấy theo tem FCC.");

                    if (!accepted)
                    {
                        _v.ClearQRInput();
                        return;
                    }

                    ScanResult confirmed = _c.QrSvc.ConfirmSlKhacBiet(result.Pending);
                    if (!confirmed.IsOK)
                    {
                        _v.ShowError(confirmed.Message);
                        return;
                    }
                }
                finally
                {
                    _awaitingSlMismatchConfirmation = false;
                    _v.SetDocQrScanInputEnabled(true);
                    _v.ClearQRInput();
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Message) && result.Message.StartsWith("CẢNH BÁO FIFO", StringComparison.OrdinalIgnoreCase))
            {
                _v.ShowWarning(result.Message);
                _v.BindDocQRCode(_c.QrSvc.LoadAll());
                return;
            }
            _v.ShowError(result.Message);
        }

        private bool IsBlockingScanError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            string normalized = message.Trim();
            return normalized.Equals("Sai Thứ tự bắn!", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Mã Hàng HVN không khớp với FCC!", StringComparison.OrdinalIgnoreCase);
        }

        private void ShowBlockingScanError(string message)
        {
            if (_scanBlocking) return;

            _scanBlocking = true;
            _v.SetDocQrScanInputEnabled(false);
            _v.ClearQRInput();
            try
            {
                DirectClickBlockingAlertDialog.Show(message);
            }
            finally
            {
                _scanBlocking = false;
                _v.ClearQRInput();
                _v.SetDocQrScanInputEnabled(true);
            }
        }

        private void OnQRScanned(QRScannedEvent e)
        {
            if (_scanBlocking || _awaitingSlMismatchConfirmation) return;
            _v.ClearQRInput();
            _v.BindDocQRCode(_c.QrSvc.LoadAll());
        }

        private void OnXoaDongQR(object sender, EventArgs e)
        {
            if (_awaitingSlMismatchConfirmation || _scanBlocking)
                return;

            int stt = _v.GetFocusedDocQRStt();
            _v.DeleteFocusedDocQRRow();
            if (stt > 0) _c.QrSvc.XoaDong(stt);
        }

        private void OnXoaToanBoQR(object sender, EventArgs e)
        {
            if (_awaitingSlMismatchConfirmation || _scanBlocking)
                return;

            _c.QrSvc.XoaToanBo();
            _v.ClearDocQRRows();
            _c.IsBanQR = false;
            _c.QrSvc.SetCheDoBan("");
            _c.ClearDeliverySession();
            _v.UnlockDocQrDeliveryContext();
            if (_c.Cfg.Delivery.CoGear)
                _c.YmvnView.UnlockCheckListYMVN();
            _c.PhieuView.UnlockAllRadio();
            _c.PhieuView.UnlockDatePicker();
        }

        public void Dispose()
        {
            var v = _v;
            v.DocQRCodeClicked -= OnDocQRCode;
            v.QRCodeSubmitted -= OnQRCodeSubmitted;
            v.XoaDongQRClicked -= OnXoaDongQR;
            v.XoaToanBoQRClicked -= OnXoaToanBoQR;
            _c.Bus.Unsubscribe<QRScannedEvent>(OnQRScanned);
        }
    }

    internal sealed class DirectClickConfirmDialog : Form
    {
        private readonly Button _btnAgree;
        private readonly Button _btnNo;
        private bool _agreePointerDown;
        private bool _noPointerDown;

        private DirectClickConfirmDialog(string message)
        {
            Text = "Xác nhận chênh lệch số lượng";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            Width = 560;
            Height = 280;
            AcceptButton = null;
            CancelButton = null;

            var lbl = new Label { AutoSize = false, Dock = DockStyle.Fill, Text = message, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 12, 18, 8) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10), WrapContents = false };
            _btnAgree = new Button { Text = "Đồng ý", Width = 120, Height = 36, TabStop = false };
            _btnNo = new Button { Text = "Không", Width = 120, Height = 36, TabStop = false };
            _btnAgree.MouseDown += BtnAgree_MouseDown;
            _btnAgree.MouseUp += BtnAgree_MouseUp;
            _btnNo.MouseDown += BtnNo_MouseDown;
            _btnNo.MouseUp += BtnNo_MouseUp;
            buttons.Controls.Add(_btnAgree);
            buttons.Controls.Add(_btnNo);
            Controls.Add(lbl);
            Controls.Add(buttons);
            Shown += delegate { ActiveControl = null; };
            KeyDown += DirectClickConfirmDialog_KeyDown;
            FormClosing += DirectClickConfirmDialog_FormClosing;
        }

        public static bool Show(string message)
        {
            using (var dialog = new DirectClickConfirmDialog(message))
            {
                dialog.ShowDialog();
                return dialog.DialogResult == DialogResult.Yes;
            }
        }

        private void DirectClickConfirmDialog_KeyDown(object sender, KeyEventArgs e) { e.Handled = true; e.SuppressKeyPress = true; }
        private void BtnAgree_MouseDown(object sender, MouseEventArgs e) { _agreePointerDown = e.Button == MouseButtons.Left; }
        private void BtnAgree_MouseUp(object sender, MouseEventArgs e)
        {
            bool directClick = _agreePointerDown && e.Button == MouseButtons.Left;
            _agreePointerDown = false;
            if (!directClick) return;
            DialogResult = DialogResult.Yes;
            Close();
        }
        private void BtnNo_MouseDown(object sender, MouseEventArgs e) { _noPointerDown = e.Button == MouseButtons.Left; }
        private void BtnNo_MouseUp(object sender, MouseEventArgs e)
        {
            bool directClick = _noPointerDown && e.Button == MouseButtons.Left;
            _noPointerDown = false;
            if (!directClick) return;
            DialogResult = DialogResult.No;
            Close();
        }
        private void DirectClickConfirmDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.None) DialogResult = DialogResult.No;
        }
    }

    internal sealed class DirectClickBlockingAlertDialog : Form
    {
        private readonly Button _btnOk;
        private bool _okPointerDown;
        private bool _accepted;

        private DirectClickBlockingAlertDialog(string message)
        {
            Text = "Lỗi đọc QRCode";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            ControlBox = true;
            Width = 560;
            Height = 230;
            AcceptButton = null;
            CancelButton = null;

            var lbl = new Label { AutoSize = false, Dock = DockStyle.Fill, Text = message + "\r\n\r\nVui lòng nhấn OK để tiếp tục bắn lại tem đúng.", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 12, 18, 8) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10), WrapContents = false };
            _btnOk = new Button { Text = "OK", Width = 120, Height = 36, TabStop = false };
            _btnOk.MouseDown += BtnOk_MouseDown;
            _btnOk.MouseUp += BtnOk_MouseUp;
            buttons.Controls.Add(_btnOk);
            Controls.Add(lbl);
            Controls.Add(buttons);
            Shown += delegate { ActiveControl = null; };
            KeyDown += DirectClickBlockingAlertDialog_KeyDown;
            FormClosing += DirectClickBlockingAlertDialog_FormClosing;
        }

        public static void Show(string message)
        {
            using (var dialog = new DirectClickBlockingAlertDialog(message)) dialog.ShowDialog();
        }
        private void DirectClickBlockingAlertDialog_KeyDown(object sender, KeyEventArgs e) { e.Handled = true; e.SuppressKeyPress = true; }
        private void BtnOk_MouseDown(object sender, MouseEventArgs e) { _okPointerDown = e.Button == MouseButtons.Left; }
        private void BtnOk_MouseUp(object sender, MouseEventArgs e)
        {
            bool directClick = _okPointerDown && e.Button == MouseButtons.Left;
            _okPointerDown = false;
            if (!directClick) return;
            _accepted = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        private void DirectClickBlockingAlertDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_accepted) e.Cancel = true;
        }
    }
}
