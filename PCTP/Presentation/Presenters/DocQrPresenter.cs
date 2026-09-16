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
        private int _pendingSlFcc;
        private int _pendingSlStt;
        private string _pendingLotFcc;

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
            ScanResult result = _c.Cfg.Delivery.CoGear
                ? _c.QrSvc.ProcessScanYMVN(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl))
                : _c.QrSvc.ProcessScan(rawQr, ma => _c.PhieuSvc.KiemTraMaTrongPhieu(ma), (ma, sl) => _c.QrSvc.KiemTraSlDaBan(ma, sl));

            if (result.IsOK) return;

            if (result.IsSlKhongKhop)
            {
                // Không bypass mismatch bằng "KHAC SLTEM".
                // FCC là số lượng chuẩn; người dùng phải sửa số lượng tem khách hàng
                // cho đúng FCC rồi mới được pass.
                var temInfo = _v.GetFocusedDocQRTemInfo();
                _pendingSlStt = result.Pending.STT;
                _pendingSlFcc = temInfo.SlFcc;
                _pendingLotFcc = temInfo.LotFcc;

                _v.ShowWarning($"Số lượng tem khách hàng không khớp số lượng tem FCC.\n\nSố lượng FCC: {_pendingSlFcc}\nSố lượng khách hàng: {result.Pending.SlTemHVN}\n\nVui lòng sửa số lượng tem khách hàng bằng số lượng FCC để tiếp tục.");
                _v.ShowSuaSoLuongTem(result.Pending.STT, _pendingLotFcc, _pendingSlFcc, result.Pending.SlTemHVN);
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
        private void OnQRScanned(QRScannedEvent e) { _v.ClearQRInput(); _v.BindDocQRCode(_c.QrSvc.LoadAll()); }
        private void OnXoaDongQR(object sender, EventArgs e) { int stt = _v.GetFocusedDocQRStt(); _v.DeleteFocusedDocQRRow(); if (stt > 0) _c.QrSvc.XoaDong(stt); }
        private void OnXoaToanBoQR(object sender, EventArgs e) { _c.QrSvc.XoaToanBo(); _v.ClearDocQRRows(); _c.IsBanQR = false; _c.QrSvc.SetCheDoBan(""); _c.PhieuView.UnlockAllRadio(); ClearPendingQuantityFix(); }
        private void OnSuaSoLuongTem(object sender, EventArgs e)
        {
            int stt = _v.SttDangSuaSl;
            if (stt <= 0) { _v.ShowError("Không xác định được dòng cần sửa!"); return; }
            int? slMoi = _v.GetSuaSoLuongResult();
            if (!slMoi.HasValue) { _v.ShowError("Chưa nhập số lượng thay đổi!"); return; }
            if (slMoi.Value <= 0) { _v.ShowError("Số lượng phải lớn hơn 0!"); return; }

            int slFcc = stt == _pendingSlStt && _pendingSlFcc > 0 ? _pendingSlFcc : _v.GetFocusedDocQRTemInfo().SlFcc;
            string lotFcc = stt == _pendingSlStt ? _pendingLotFcc : _v.GetFocusedDocQRTemInfo().LotFcc;
            if (slFcc <= 0)
            {
                _v.ShowError("Không xác định được số lượng tem FCC để đối chiếu.");
                return;
            }

            // Chỉ cập nhật khi số lượng khách hàng khớp chính xác FCC.
            if (slMoi.Value != slFcc)
            {
                _v.ShowWarning($"Số lượng tem khách hàng ({slMoi.Value}) vẫn không khớp số lượng tem FCC ({slFcc}).\n\nVui lòng nhập đúng {slFcc} để tiếp tục.");
                _v.ShowSuaSoLuongTem(stt, lotFcc, slFcc, slMoi.Value);
                return;
            }

            _c.QrSvc.CapNhapSlHvn(stt, slMoi.Value);
            _v.BindDocQRCode(_c.QrSvc.LoadAll());
            _v.ShowInfo($"Đã cập nhật số lượng tem khách hàng = {slMoi.Value}. Số lượng đã khớp tem FCC.");
            ClearPendingQuantityFix();
        }
        private void ClearPendingQuantityFix()
        {
            _pendingSlStt = 0;
            _pendingSlFcc = 0;
            _pendingLotFcc = null;
        }
        public void Dispose() { var v = _v; v.DocQRCodeClicked -= OnDocQRCode; v.QRCodeSubmitted -= OnQRCodeSubmitted; v.XoaDongQRClicked -= OnXoaDongQR; v.XoaToanBoQRClicked -= OnXoaToanBoQR; v.SuaSoLuongTemClicked -= OnSuaSoLuongTem; _c.Bus.Unsubscribe<QRScannedEvent>(OnQRScanned); }
    }
}
