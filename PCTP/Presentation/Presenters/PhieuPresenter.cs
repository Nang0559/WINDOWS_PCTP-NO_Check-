using DevExpress.XtraReports.UI;
using PCTP.Applications.Services;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Presentation.Views;
using PCTP.QRCODE_HVN.Report;
using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Presentation.Presenters
{
    internal sealed class PhieuPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IPhieuView _v;
        internal PhieuPresenter(HVNPresenterContext context)
        {
            _c = context; _v = _c.PhieuView; var v = _v;
            v.FormLoaded += OnFormLoaded; v.DateChanged += OnDateChanged; v.GioXuatChanged += OnGioXuatChanged; v.TabChanged += OnTabChanged; v.CapNhapKhoClicked += OnCapNhapKho; v.InPhieuClicked += OnInPhieu; v.InGhepLotClicked += OnInGhepLot; v.InTachLotClicked += OnInTachLot; v.KiemTraGhepLotClicked += OnKiemTraGhepLot; v.KiemTraMaNGClicked += OnKiemTraMaNG; v.HoanThanhClicked += OnHoanThanh; v.LoaiPhieuChanged += OnLoaiPhieuChanged; v.ChonLotThuCongClicked += OnChonLotThuCong; v.XemHangThieuCaNgayClicked += OnXemHangThieuCaNgay; v.LayLaiLotNoClicked += OnLayLaiLotNo; v.CapNhapTTPHIEUClicked += OnCapNhapTTPHIEU;
            _c.Bus.Subscribe<PhieuLoadedEvent>(OnPhieuLoaded); _c.Bus.Subscribe<KhoUpdatedEvent>(OnKhoUpdated); _c.Bus.Subscribe<TinhTongCompletedEvent>(OnTinhTongCompleted);
        }
        private void OnFormLoaded(object sender, EventArgs e) { if (_c.Cfg.Delivery.CoGear) { _c.AddNM = _c.Cfg.Delivery.AddNmMacDinh; _c.LoadGioXuatYMVN(); } else _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay ? _v.SelectedTabAddNM : _c.Cfg.Delivery.AddNmMacDinh; XetTrangThai(); }
        private void OnDateChanged(object sender, EventArgs e) => _c.RunWithLoading(() => { if (_c.Cfg.Delivery.CoGear) _c.LoadGioXuatYMVN(); if (_c.GioXuatHienTai.Ma == "#") { _c.LoadPhieuGiaoDB(); return; } _c.LoadPhieuHienTai(); }, "Đang chuyển ngày...");
        private void OnTabChanged(object sender, EventArgs e) { if (!_c.Cfg.Delivery.CoNhieuNhaMay) return; int selectedTab = _v.SelectedTabAddNM; _c.RunWithLoading(() => { _c.AddNM = selectedTab; _c.LoadPhieuHienTai(); }, "Chuyển nhà máy..."); }
        private void OnGioXuatChanged(object sender, EventArgs e)
        {
            _c.UpdateGioXuat(_v.CurrentGioXuat);
            _c.RunWithLoading(() =>
            {
                if (_c.Cfg.Delivery.CoGear)
                {
                    _c.LoadPhieuHienTai();
                    return;
                }
                if (_c.GioXuatHienTai.Ma == "#")
                {
                    _c.UiContext.Post(_ => _c.LoadPhieuGiaoDB(), null);
                    return;
                }
                _c.UiContext.Send(_ => _c.Bus.Publish(new GioXuatChangedEvent(_c.GioXuatHienTai, _c.AddNM)), null);
                _c.LoadPhieuHienTai();
            }, "Chuyển giờ xuất...");
        }
        private void OnCapNhapKho(object sender, EventArgs e) => _c.RunWithLoadingSync(() => { if (_c.Cfg.Delivery.CoGear) { var gs = _c.YmvnView.GetCheckedGioXuat(); if (!gs.Any()) { _v.ShowInfo("Bạn chưa chọn giờ xuất!"); return; } _c.PhieuSvc.CapNhapKhoYMVN(_v.SelectedDate.ToString("MM/dd/yyyy"), string.Join(",", gs.Select(g => $"'{g}'")), _c.GetNhaMay(), _v.GetDonHangTable()); return; } if (_c.Cfg.Delivery.LoadTuBangRieng) { if (!_v.CoLotDeLuuKho()) { _v.ShowInfo("Không có dữ liệu cho CNK !!!!!"); return; } _c.PhieuSvc.CapNhapKho("", _c.GetNhaMay()); return; } bool isLoaiSP = _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.MoTa }) == OrderCategory.SP; if (!isLoaiSP && !_v.CoLotDeLuuKho()) { _v.ShowInfo("Không có dữ liệu cho CNK !!!!!"); return; } string nhaMay = _c.GetNhaMay(); string ngayGiao = _v.SelectedDate.ToString("yyyy-MM-dd"); if (isLoaiSP) _c.PhieuSvc.LuuPhieuSP(nhaMay, ngayGiao, _c.GioXuatHienTai.MoTa, _c.GioXuatHienTai.Ma); _c.PhieuSvc.CapNhapKho(_c.GioXuatHienTai.MoTa, nhaMay, _c.GioXuatHienTai.Ma); }, "Đang cập nhật kho...");
        private void OnCapNhapTTPHIEU(object sender, TTPHIEUEventArgs e) => _c.RunWithLoadingSync(() => { if (_c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = _c.GioXuatHienTai.Ma }) != OrderCategory.SP) return; _c.PhieuSvc.CapNhapTTPHIEU(_c.GetNhaMay(), _v.SelectedDate.ToString("yyyy-MM-dd"), _c.GioXuatHienTai.MoTa, e.Stt, e.GhiChu); }, "Đang cập nhật thông tin phiếu...");
        private void OnInPhieu(object sender, EventArgs e)
        {
            int hinhThucIn = 0; if (!_c.Cfg.Delivery.CoGear && !_c.Cfg.Delivery.LoadTuBangRieng && _c.GioXuatHienTai.Ma != "#") { hinhThucIn = _v.ShowChonHinhThucIn(); if (hinhThucIn == -1) return; }
            _c.RunWithLoadingSync(() => { if (_c.Cfg.Delivery.CoGear) { _c.YmvnView.ShowReportYMVN(_v.GetDonHangTable()); return; } if (_c.Cfg.Delivery.LoadTuBangRieng) { DataTable d = _c.InPhieuSvc.BuildReportDataTuBangRieng(_v.GetDonHangTable(), _v.GetAddressTable(), _v.SelectedDate.ToString("ddMMyyyy")); _v.ShowReportWithGioHeader(d, _c.Cfg.Delivery.LoadTheoNgay ? "PO No" : "Giờ"); return; } if (_c.GioXuatHienTai.Ma == "#") { _v.ShowReport(_c.InPhieuSvc.BuildReportDataGiaoDB(_v.GetDonHangTable())); return; } _v.ShowReport(_c.InPhieuSvc.BuildReportData(_v.SelectedDate.ToString("ddMMyyyy"), _c.GioXuatHienTai.Ma, _c.GioXuatHienTai.MoTa, _c.GetNhaMay(), _c.AddNM, hinhThucIn, _v.GetAddressTable())); }, "Đang khởi tạo biểu mẫu in...");
        }
        private void OnInGhepLot(object sender, EventArgs e) { var selectedRows = _v.GetSelectedGhepLotRows(); DataTable reportData = null; _c.RunWithLoadingSync(() => reportData = _c.InPhieuSvc.InGhepLot(selectedRows.Any() ? selectedRows : null, Environment.MachineName), "Đang tổng hợp dữ liệu ghép LOT..."); if (reportData != null) new ReportPrintTool(new GHEPLOT { DataSource = reportData }).ShowPreviewDialog(); }
        private void OnInTachLot(object sender, EventArgs e) => _v.ShowTachLot();
        private void OnKiemTraGhepLot(object sender, EventArgs e) => _c.RunWithLoading(() => { DataTable dt = _c.PhieuSvc.LoadGhepLot(); _c.UiContext.Post(_ => _v.BindGhepLot(dt), null); }, "Đang kiểm tra ghép LOT...");
        private void OnKiemTraMaNG(object sender, EventArgs e) { string ma = _v.GetFocusedDonHangMaHang(); if (!string.IsNullOrWhiteSpace(ma)) _v.ShowKiemTraMaNG(ma); }
        private void OnHoanThanh(object sender, EventArgs e) => _c.RunWithLoadingSync(() => { int n = _c.QrSvc.CountChuaDG(); if (n > 0) { DataTable t = _c.PhieuSvc.GetDonHangChuaLot(_c.QrSvc.IsBanSP); if (t != null && t.Rows.Count > 0) _c.PhieuSvc.TinhTongLot(t, lv => _v.ShowChonSttTrungMa(lv), (stt, lot) => _v.RefreshLotRow(stt, lot), _c.QrSvc.IsBanSP); if (_c.QrSvc.CountChuaDG() == 0) { _c.IsBanQR = false; _v.UnlockAllRadio(); } _v.SwitchToPhieuView(); DataTable latest = _c.PhieuSvc.GetDonHangHienTai(_c.TenBan); _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(latest)); return; } _c.IsBanQR = false; _c.QrSvc.SetCheDoBan(""); _v.UnlockAllRadio(); if (_c.GioXuatHienTai.Ma == "#") { DataTable d = _c.LoadPhieuGiaoDB(); _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(d)); } else { _v.SwitchToPhieuView(); _c.LoadPhieuHienTai(); } }, "Đang tổng hợp dữ liệu hoàn thành...");
        private void OnLoaiPhieuChanged(object sender, EventArgs e) => _c.LoadPhieuHienTai();
        private void OnChonLotThuCong(object sender, ChonLotThuCongEventArgs e) { if (!_c.IsMayBanQR) return; DataTable lots = _c.PhieuSvc.GetDanhSachLotTuKho(e.MaHang); ChonLotResult r = _v.ShowChonLotTuKho(e.Stt, e.MaHang, e.SoLuong, lots); if (!r.Confirmed || string.IsNullOrWhiteSpace(r.LotGhep)) return; _c.PhieuSvc.NhapLotThuCong(e.Stt, r.LotGhep, _c.TenBan); _v.RefreshLotRow(e.Stt, r.LotGhep); _c.IsBanQR = true; _v.LockRadioExcept(_c.GioXuatHienTai.Ma); DataTable dt = _c.PhieuSvc.GetDonHangHienTai(_c.TenBan); _c.SetupPhieuButtonsDefault(_c.PhieuSvc.CheckCanCapNhapKho(dt), false, _c.PhieuSvc.CheckCoLotChuaCNK(dt)); }
        private void OnLayLaiLotNo(object sender, LayLaiLotEventArgs e) { if (!_v.Confirm($"Bạn có chắc chắn muốn reset dữ liệu LOT của dòng có STT {e.Stt} không?")) return; _c.RunWithLoadingSync(() => { _c.PhieuSvc.LayLaiLotNo(e.Stt, _c.QrSvc.IsBanSP); _c.LoadPhieuHienTai(); }, "Đang xử lý lấy lại số LOT..."); }
        private void OnXemHangThieuCaNgay(object sender, EventArgs e) => _c.RunWithLoading(() => { DataTable dt = _c.HangThieuCaNgayService.TinhHangThieuCaNgay(_v.SelectedDate, _c.GetNhaMay(), _c.AddNM, _c.Cfg); _c.UiContext.Post(_ => _v.ShowHangThieuCaNgay(dt), null); }, "Đang tính hàng thiếu cả ngày...");
        private void XetTrangThai()
        {
            _c.RunWithLoadingSync(() => { if (!_c.IsMayBanQR) { _c.IsBanQR = false; _v.UnlockAllRadio(); _v.UnlockDatePicker(); _c.LoadPhieuHienTai(); return; } var tt = _c.PhieuSvc.GetTrangThaiDangBan(); if (!tt.DangBan && _c.Cfg.Delivery.CoConfigSP) { var sp = _c.PhieuSvc.GetTrangThaiDangBanSP(); if (sp.DangBan) { tt = sp; _c.PhieuSvc.SetTrangThaiBan(true, true); _c.QrSvc.SetCheDoBanSP(true); } } if (!tt.DangBan) { _c.IsBanQR = false; _c.QrSvc.SetCheDoBanSP(false); _v.UnlockAllRadio(); _v.UnlockDatePicker(); _c.LoadPhieuHienTai(); return; } if (tt.DataKhongKhop) { if (_c.DocQrView.HoiXoaDocQR()) _c.PhieuSvc.XoaDocQRCode(); _c.IsBanQR = false; _c.QrSvc.SetCheDoBanSP(false); _v.UnlockAllRadio(); _v.UnlockDatePicker(); _c.LoadPhieuHienTai(); return; } if (DateTime.TryParse(tt.NgayGiao, out DateTime ngay)) _v.SetDate(ngay); _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay ? tt.AddNM : _c.Cfg.Delivery.AddNmMacDinh; if (_c.Cfg.Delivery.CoNhieuNhaMay) _v.SetTab(tt.AddNM); _c.IsBanQR = true; _v.LockDatePicker(); if (_c.Cfg.Delivery.CoGear) { var gs = _c.ParseGioYMVN(tt.GioGiaoFCC); bool sp = _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = tt.GioGiaoFCC }) == OrderCategory.SP; _c.QrSvc.SetCheDoBanSP(sp); _v.SuspendGioXuatChanged(); try { _c.YmvnView.SetCheckedGiosYMVN(gs); _c.YmvnView.LockCheckListYMVN(); } finally { _v.ResumeGioXuatChanged(); } _c.PhieuSvc.LoadPhieuTuBangRieng_Internal(tt.NgayGiao, gs, sp, _c.IsMayBanQR, true); return; } string gio = tt.GioGiaoFCC, ma = "", mota = ""; var ds = _c.AddNM == 1 ? _c.GioXuatRepo.GetDanhSachGioVP() : _c.GioXuatRepo.GetDanhSachGioHN(); foreach (var g in ds) { string mb = GioXuatRepository.ParseGioThuong(g.MoTa); if (mb.Contains($"'{gio}'")) { ma = g.Ma; mota = g.MoTa; break; } } if (string.IsNullOrEmpty(ma)) { ma = $"'{gio}'"; mota = gio + "H"; } _c.QrSvc.SetCheDoBanSP(_c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = mota }) == OrderCategory.SP); _v.SuspendGioXuatChanged(); try { _c.GioXuatHienTai = new GioXuat(ma, mota); _c.GiaoDbView.UpdateGioXuatFromDB(ma); _v.LockRadioExcept(ma); } finally { _v.ResumeGioXuatChanged(); } _c.LoadPhieuHienTai(); }, "Đang kiểm tra trạng thái phiên làm việc cũ...");
        }
        private void OnPhieuLoaded(PhieuLoadedEvent e)
        {
            _c.UiContext.Post(_ => { _c.IsLoadingPhieu = false; try { _v.BindDonHang(e.DonHangTable); _v.BindHangThieu(e.HangThieuTable); _v.SetGridCaption(e.Caption); if (_c.Cfg.Delivery.LoadTuBangRieng) _v.BindLechIFS(_c.PhieuSvc.TinhLechIFS(e.DonHangTable, _v.SelectedDate.ToString("ddMMyyyy"))); bool ng = e.CoMaNG; bool cnk = _c.PhieuSvc.CheckCanCapNhapKho(e.DonHangTable); bool lay = _c.IsMayBanQR && _c.PhieuSvc.CheckCoLotChuaCNK(e.DonHangTable); _v.SetupPhieuButtons(cnk && _c.IsMayBanQR, ng && _c.IsMayBanQR, _c.IsMayBanQR, _c.IsMayBanQR, lay, !_c.Cfg.Delivery.LoadTuBangRieng); _c.AwaitingPhieuLoadedEvent = false; _v.ShowLoading(false); } catch (Exception ex) { _c.AwaitingPhieuLoadedEvent = false; _v.ShowLoading(false); _v.ShowError($"Lỗi khi hiển thị dữ liệu phiếu: {ex.Message}"); } if (!string.IsNullOrEmpty(e.CanhBao)) _v.ShowWarning(e.CanhBao); }, null);
        }
        private void OnKhoUpdated(KhoUpdatedEvent e) { _c.UiContext.Post(_ => { if (e.Errors != null && e.Errors.Rows.Count > 0) { _v.ShowLoiCapNhapKho(e.Errors); return; } if (e.SoLotCapNhap > 0) _v.ShowInfo($"Đã cập nhật {e.SoLotCapNhap} LOT thành công."); _c.IsBanQR = false; _c.QrSvc.SetCheDoBanSP(false); _v.UnlockAllRadio(); _v.UnlockDatePicker(); if (_c.Cfg.Delivery.CoGear) _v.UnlockCheckListYMVN(); _c.RunWithLoadingSync(() => _c.LoadPhieuHienTai(), "Đang tải lại dữ liệu phiếu..."); }, null); }
        private void OnTinhTongCompleted(TinhTongCompletedEvent e) { foreach (var (stt, lot) in e.Results) _v.RefreshLotRow(stt, lot); }
        public void Dispose()
        {
            var v = _c.View; v.FormLoaded -= OnFormLoaded; v.DateChanged -= OnDateChanged; v.GioXuatChanged -= OnGioXuatChanged; v.TabChanged -= OnTabChanged; v.CapNhapKhoClicked -= OnCapNhapKho; v.InPhieuClicked -= OnInPhieu; v.InGhepLotClicked -= OnInGhepLot; v.InTachLotClicked -= OnInTachLot; v.KiemTraGhepLotClicked -= OnKiemTraGhepLot; v.KiemTraMaNGClicked -= OnKiemTraMaNG; v.HoanThanhClicked -= OnHoanThanh; v.LoaiPhieuChanged -= OnLoaiPhieuChanged; v.ChonLotThuCongClicked -= OnChonLotThuCong; v.XemHangThieuCaNgayClicked -= OnXemHangThieuCaNgay; v.LayLaiLotNoClicked -= OnLayLaiLotNo; v.CapNhapTTPHIEUClicked -= OnCapNhapTTPHIEU; _c.Bus.Unsubscribe<PhieuLoadedEvent>(OnPhieuLoaded); _c.Bus.Unsubscribe<KhoUpdatedEvent>(OnKhoUpdated); _c.Bus.Unsubscribe<TinhTongCompletedEvent>(OnTinhTongCompleted);
        }
    }
}
