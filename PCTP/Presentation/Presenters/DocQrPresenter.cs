using PCTP.Applications.Services;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Presentation.Dialogs;
using PCTP.Presentation.Views;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Presentation.Presenters
{
    internal sealed class DocQrPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IDocQrView _v;
        private bool _awaitingSlMismatchConfirmation;

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
            if (!_c.PhieuView.CoHangChuaOK())
            {
                _v.ShowInfo("Phiếu không đủ điều kiện để đọc QRCODE. Hoặc đã đọc xong dữ liệu.");
                return;
            }

            bool isSP = _c.Cfg.Delivery.LoadTuBangRieng
                ? _c.Cfg.Delivery.CoLoaiSP && _c.PhieuView.IsLoaiSP
                : _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.MoTa }) == OrderCategory.SP;

            _c.QrSvc.SetCheDoBan(_c.Cfg.Delivery.LoadTuBangRieng ? "" : _c.GioXuatHienTai.MoTa);
            _c.QrSvc.SetCheDoBanSP(isSP);

            DataTable dtPhieu = _c.Cfg.Delivery.LoadTuBangRieng ? _c.PhieuView.GetDonHangTable() : null;
            string ngay = _c.PhieuView.SelectedDate.ToString("yyyy-MM-dd");
            List<string> gios = _c.Cfg.Delivery.CoGear ? _c.YmvnView.GetCheckedGioXuat() : null;
            _c.IsBanQR = true;

            if (!_c.Cfg.Delivery.CoGear && !_c.Cfg.Delivery.LoadTuBangRieng)
                _c.PhieuView.LockRadioExcept(_c.GioXuatHienTai.Ma);

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
                    _c.PhieuView.UnlockAllRadio();
                    _v.ShowError($"Lỗi chuẩn bị dữ liệu QR: {ex.Message}");
                }
            }, "Đang chuẩn bị dữ liệu QR...");
        }

        private void OnQRCodeSubmitted(object sender, string rawQr)
        {
            // Keyboard-wedge scanner có thể phát Enter/dữ liệu trong lúc dialog đang mở.
            // Không được cho phép bất kỳ scan nào đi xuyên qua confirmation gate.
            if (_awaitingSlMismatchConfirmation)
            {
                _v.ClearQRInput();
                return;
            }

            ScanResult result = _c.Cfg.Delivery.CoGear
                ? _c.QrSvc.ProcessScanYMVN(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl))
                : _c.QrSvc.ProcessScan(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl));

            if (result.IsOK) return;

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
                    // Không dùng XtraMessageBox/MessageBox YesNo ở đây.
                    // Scanner gửi Enter như bàn phím; Enter không bao giờ được phép
                    // kích hoạt Đồng ý. Chỉ pointer click trực tiếp mới xác nhận.
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

        private void OnQRScanned(QRScannedEvent e)
        {
            _v.ClearQRInput();
            _v.BindDocQRCode(_c.QrSvc.LoadAll());
        }

        private void OnXoaDongQR(object sender, EventArgs e)
        {
            if (_awaitingSlMismatchConfirmation)
                return;

            int stt = _v.GetFocusedDocQRStt();
            _v.DeleteFocusedDocQRRow();
            if (stt > 0) _c.QrSvc.XoaDong(stt);
        }

        private void OnXoaToanBoQR(object sender, EventArgs e)
        {
            if (_awaitingSlMismatchConfirmation)
                return;

            _c.QrSvc.XoaToanBo();
            _v.ClearDocQRRows();
            _c.IsBanQR = false;
            _c.QrSvc.SetCheDoBan("");
            _c.PhieuView.UnlockAllRadio();
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
}
