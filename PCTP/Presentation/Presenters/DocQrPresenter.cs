using PCTP.Applications.Services;
using PCTP.Domain.Events;
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
        internal DocQrPresenter(HVNPresenterContext context)
        {
            _c = context; var v = _c.View; v.DocQRCodeClicked += OnDocQRCode; v.QRCodeSubmitted += OnQRCodeSubmitted; v.XoaDongQRClicked += OnXoaDongQR; v.XoaToanBoQRClicked += OnXoaToanBoQR; v.SuaSoLuongTemClicked += OnSuaSoLuongTem; _c.Bus.Subscribe<QRScannedEvent>(OnQRScanned);
        }
        private void OnDocQRCode(object sender, EventArgs e)
        {
            if (!_c.IsMayBanQR) { _c.View.ShowInfo("Bạn chỉ sử dụng được tính năng này trên máy bắn QR."); return; }
            if (!_c.View.CoHangChuaOK()) { _c.View.ShowInfo("Phiếu không đủ điều kiện để đọc QRCODE. Hoặc đã đọc xong dữ liệu."); return; }
            bool isSP = _c.Cfg.Delivery.LoadTuBangRieng ? _c.Cfg.Delivery.CoLoaiSP && _c.View.IsLoaiSP : _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.MoTa }) == PCTP.Shared.Enums.OrderCategory.SP;
            _c.QrSvc.SetCheDoBan(_c.Cfg.Delivery.LoadTuBangRieng ? "" : _c.GioXuatHienTai.MoTa); _c.QrSvc.SetCheDoBanSP(isSP);
            DataTable dtPhieu = _c.Cfg.Delivery.LoadTuBangRieng ? _c.View.GetDonHangTable() : null; string ngay = _c.View.SelectedDate.ToString("yyyy-MM-dd"); List<string> gios = _c.Cfg.Delivery.CoGear ? _c.View.GetCheckedGioXuat() : null; _c.IsBanQR = true;
            _c.RunWithLoading(() => { try { if (_c.Cfg.Delivery.LoadTuBangRieng) { if (_c.QrSvc.CountChuaDG() == 0 && !_c.QrSvc.CoDocQRNao()) _c.PhieuSvc.SyncPhieuTuBangRiengChoDocQR(dtPhieu, ngay, gios); } else if (_c.QrSvc.CountChuaDG() == 0 && !_c.QrSvc.CoDocQRNao()) _c.PhieuSvc.SyncIfsPhieuChoDocQR(ngay, _c.GetNhaMay(), _c.GioXuatHienTai.Ma, _c.GioXuatHienTai.MoTa, _c.AddNM); DataTable qrData = _c.QrSvc.LoadAll(); _c.UiContext.Post(_ => { _c.View.BindDocQRCode(qrData); _c.View.SwitchToDocQRView(); }, null); } catch (Exception ex) { _c.IsBanQR = false; _c.UiContext.Post(_ => _c.View.ShowError($"Lỗi chuẩn bị dữ liệu QR: {ex.Message}"), null); } }, "Đang chuẩn bị dữ liệu QR...");
        }
        private void OnQRCodeSubmitted(object sender, string rawQr)
        {
            ScanResult result = _c.Cfg.Delivery.CoGear ? _c.QrSvc.ProcessScanYMVN(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl)) : _c.QrSvc.ProcessScan(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl));
            if (result.IsOK) return;
            if (result.IsSlKhongKhop) { _c.RunWithLoadingSync(() => { if (!_c.View.Confirm("Số lượng TEM không khớp với phiếu giao!\nBạn có muốn nhập với số lượng này không?")) return; var confirmed = _c.QrSvc.ConfirmSlKhacBiet(result.Pending); if (!confirmed.IsOK) _c.View.ShowError(confirmed.Message); }, "Đang xác nhận..."); return; }
            _c.View.ShowError(result.Message);
        }
        private void OnQRScanned(QRScannedEvent e) { _c.View.ClearQRInput(); _c.View.BindDocQRCode(_c.QrSvc.LoadAll()); }
        private void OnXoaDongQR(object sender, EventArgs e) { int stt = _c.View.GetFocusedDocQRStt(); _c.View.DeleteFocusedDocQRRow(); if (stt > 0) _c.QrSvc.XoaDong(stt); }
        private void OnXoaToanBoQR(object sender, EventArgs e) { _c.QrSvc.XoaToanBo(); _c.View.ClearDocQRRows(); _c.IsBanQR = false; _c.QrSvc.SetCheDoBan(""); _c.View.UnlockAllRadio(); }
        private void OnSuaSoLuongTem(object sender, EventArgs e) { int stt = _c.View.SttDangSuaSl; if (stt <= 0) { _c.View.ShowError("Không xác định được dòng cần sửa!"); return; } int? slMoi = _c.View.GetSuaSoLuongResult(); if (!slMoi.HasValue) { _c.View.ShowError("Chưa nhập số lượng thay đổi!"); return; } if (slMoi.Value <= 0) { _c.View.ShowError("Số lượng phải lớn hơn 0!"); return; } _c.QrSvc.CapNhapSlHvn(stt, slMoi.Value); _c.View.BindDocQRCode(_c.QrSvc.LoadAll()); }
        public void Dispose() { var v = _c.View; v.DocQRCodeClicked -= OnDocQRCode; v.QRCodeSubmitted -= OnQRCodeSubmitted; v.XoaDongQRClicked -= OnXoaDongQR; v.XoaToanBoQRClicked -= OnXoaToanBoQR; v.SuaSoLuongTemClicked -= OnSuaSoLuongTem; _c.Bus.Unsubscribe<QRScannedEvent>(OnQRScanned); }
    }
}
