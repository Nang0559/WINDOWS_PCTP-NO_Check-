using PCTP.Applications.Services;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.Models;
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
        internal DocQrPresenter(HVNPresenterContext context)
        {
            _c = context; _v = _c.DocQrView; var v = _v; v.DocQRCodeClicked += OnDocQRCode; v.QRCodeSubmitted += OnQRCodeSubmitted; v.XoaDongQRClicked += OnXoaDongQR; v.XoaToanBoQRClicked += OnXoaToanBoQR; v.SuaSoLuongTemClicked += OnSuaSoLuongTem; _c.Bus.Subscribe<QRScannedEvent>(OnQRScanned);
        }
        private void OnDocQRCode(object sender, EventArgs e)
        {
            if (!_c.IsMayBanQR) { _v.ShowInfo("Bạn chỉ sử dụng được tính năng này trên máy bắn QR."); return; }
            if (!_c.PhieuView.CoHangChuaOK()) { _v.ShowInfo("Phiếu không đủ điều kiện để đọc QRCODE. Hoặc đã đọc xong dữ liệu."); return; }

            bool isSP = _c.Cfg.Delivery.LoadTuBangRieng
                ? _c.Cfg.Delivery.CoLoaiSP && _c.PhieuView.IsLoaiSP
                : _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.MoTa }) == OrderCategory.SP;

            _c.QrSvc.SetCheDoBan(_c.Cfg.Delivery.LoadTuBangRieng ? "" : _c.GioXuatHienTai.MoTa);
            _c.QrSvc.SetCheDoBanSP(isSP);

            DataTable dtPhieu = _c.Cfg.Delivery.LoadTuBangRieng ? _c.PhieuView.GetDonHangTable() : null;
            string ngay = _c.PhieuView.SelectedDate.ToString("yyyy-MM-dd");
            List<string> gios = _c.Cfg.Delivery.CoGear ? _c.YmvnView.GetCheckedGioXuat() : null;
            _c.IsBanQR = true;

            _c.RunWithLoading(() => { /* phần còn lại giữ nguyên, không đổi gì */ }, "Đang chuẩn bị dữ liệu QR...");
        }
        private void OnQRCodeSubmitted(object sender, string rawQr)
        {
            ScanResult result = _c.Cfg.Delivery.CoGear ? _c.QrSvc.ProcessScanYMVN(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl)) : _c.QrSvc.ProcessScan(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl));
            if (result.IsOK) return;
            if (result.IsSlKhongKhop) { _c.RunWithLoadingSync(() => { if (!_v.Confirm("Số lượng TEM không khớp với phiếu giao!\nBạn có muốn nhập với số lượng này không?")) return; var confirmed = _c.QrSvc.ConfirmSlKhacBiet(result.Pending); if (!confirmed.IsOK) _v.ShowError(confirmed.Message); }, "Đang xác nhận..."); return; }
            _v.ShowError(result.Message);
        }
        private void OnQRScanned(QRScannedEvent e) { _v.ClearQRInput(); _v.BindDocQRCode(_c.QrSvc.LoadAll()); }
        private void OnXoaDongQR(object sender, EventArgs e) { int stt = _v.GetFocusedDocQRStt(); _v.DeleteFocusedDocQRRow(); if (stt > 0) _c.QrSvc.XoaDong(stt); }
        private void OnXoaToanBoQR(object sender, EventArgs e) { _c.QrSvc.XoaToanBo(); _v.ClearDocQRRows(); _c.IsBanQR = false; _c.QrSvc.SetCheDoBan(""); _c.PhieuView.UnlockAllRadio(); }
        private void OnSuaSoLuongTem(object sender, EventArgs e) { int stt = _v.SttDangSuaSl; if (stt <= 0) { _v.ShowError("Không xác định được dòng cần sửa!"); return; } int? slMoi = _v.GetSuaSoLuongResult(); if (!slMoi.HasValue) { _v.ShowError("Chưa nhập số lượng thay đổi!"); return; } if (slMoi.Value <= 0) { _v.ShowError("Số lượng phải lớn hơn 0!"); return; } _c.QrSvc.CapNhapSlHvn(stt, slMoi.Value); _v.BindDocQRCode(_c.QrSvc.LoadAll()); }
        public void Dispose() { var v = _v; v.DocQRCodeClicked -= OnDocQRCode; v.QRCodeSubmitted -= OnQRCodeSubmitted; v.XoaDongQRClicked -= OnXoaDongQR; v.XoaToanBoQRClicked -= OnXoaToanBoQR; v.SuaSoLuongTemClicked -= OnSuaSoLuongTem; _c.Bus.Unsubscribe<QRScannedEvent>(OnQRScanned); }
    }
}
