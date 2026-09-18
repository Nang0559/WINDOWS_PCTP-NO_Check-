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
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Presentation.Presenters
{
    internal sealed class PhieuPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IPhieuView _v;

        // Immutable UI restore snapshot for an active DOCQRCODE session.
        // TMP/DOCQRCODE is the source of truth; normal UI loads must never
        // replace this header context with the previous screen selection.
        private DateTime _qrRestoreDate = DateTime.MinValue;
        private int _qrRestoreAddNM;
        private string _qrRestoreConcreteHour = string.Empty;
        private bool _qrRestoreIsSP;
        // Initial control binding can synchronously raise context-change events.
        // Those events must not acquire _busy before the persisted QR session
        // has been restored.
        private bool _initializing;
        // Ignore header events raised before FormLoaded. DevExpress can raise
        // Date/Tab/GioXuat/LoaiPhieuChanged while controls are being created,
        // before OnFormLoaded has a chance to establish the QR session.
        private bool _formLoaded;
        internal PhieuPresenter(HVNPresenterContext context)
        {
            _c = context; _v = _c.PhieuView; var v = _v;
            v.FormLoaded += OnFormLoaded; v.DateChanged += OnDateChanged; v.GioXuatChanged += OnGioXuatChanged; v.TabChanged += OnTabChanged; v.CapNhapKhoClicked += OnCapNhapKho; v.InPhieuClicked += OnInPhieu; v.InGhepLotClicked += OnInGhepLot; v.InTachLotClicked += OnInTachLot; v.KiemTraGhepLotClicked += OnKiemTraGhepLot; v.KiemTraMaNGClicked += OnKiemTraMaNG; v.HoanThanhClicked += OnHoanThanh; v.LoaiPhieuChanged += OnLoaiPhieuChanged; v.ChonLotThuCongClicked += OnChonLotThuCong; v.XemHangThieuCaNgayClicked += OnXemHangThieuCaNgay; v.LayLaiLotNoClicked += OnLayLaiLotNo; v.CapNhapTTPHIEUClicked += OnCapNhapTTPHIEU;
            _c.Bus.Subscribe<PhieuLoadedEvent>(OnPhieuLoaded); _c.Bus.Subscribe<KhoUpdatedEvent>(OnKhoUpdated); _c.Bus.Subscribe<TinhTongCompletedEvent>(OnTinhTongCompleted);
        }
        private void OnFormLoaded(object sender, EventArgs e)
        {
            // DevExpress binding may synchronously raise Date/Tab/GioXuat/
            // LoaiPhieuChanged while the form is being initialized. These are
            // not user changes and must not start a competing async load.
            _initializing = true;
            try
            {
                if (_c.Cfg.Delivery.CoGear)
                {
                    _c.AddNM = _c.Cfg.Delivery.AddNmMacDinh;
                    _c.LoadGioXuatYMVN();
                }
                else
                {
                    _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay
                        ? _v.SelectedTabAddNM
                        : _c.Cfg.Delivery.AddNmMacDinh;
                }
            }
            finally
            {
                _initializing = false;
            }

            // From this point header events are real user/UI changes. Events
            // raised before this point are only control initialization noise.
            _formLoaded = true;
            XetTrangThai();
        }
        private void OnDateChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing) return;

            // QR session identity = ngày + nhà máy + giờ. Không được reload TMP
            // bằng một context mới khi đang có DOCQRCODE.
            if (_c.IsBanQR) return;

            _c.RunWithLoading(() =>
            {
                if (_c.Cfg.Delivery.CoGear) _c.LoadGioXuatYMVN();
                if (_c.GioXuatHienTai.Ma == "#")
                {
                    _c.LoadPhieuGiaoDB();
                    return;
                }
                _c.LoadPhieuHienTai();
            }, "Đang chuyển ngày...");
        }
        private void OnTabChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing) return;

            // Khi đã có DOCQRCODE, nhà máy là một phần của session identity.
            if (_c.IsBanQR || !_c.Cfg.Delivery.CoNhieuNhaMay) return;

            int selectedTab = _v.SelectedTabAddNM;
            _c.RunWithLoading(() =>
            {
                _c.AddNM = selectedTab;
                _c.LoadPhieuHienTai();
            }, "Chuyển nhà máy...");
        }
        private void OnGioXuatChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing) return;

            // Không cho thay đổi hour context trong một QR session đang mở.
            if (_c.IsBanQR) return;

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
        private void OnHoanThanh(object sender, EventArgs e) => _c.RunWithLoadingSync(() =>
        {
            int n = _c.QrSvc.CountChuaDG();
            if (n > 0)
            {
                DataTable t = _c.PhieuSvc.GetDonHangChuaLot(_c.QrSvc.IsBanSP);
                if (t != null && t.Rows.Count > 0) _c.LotSvc.TinhTongLot(t, _c.TenBan, _c.Cfg.Delivery.GetDocQRTable(_c.QrSvc.IsBanSP), _c.Cfg.Delivery.GetTmpTable(_c.QrSvc.IsBanSP), rows => _v.ShowChonSttTrungMa(rows));
                if (_c.QrSvc.CountChuaDG() > 0)
                    return;

                _c.IsBanQR = false;
                _c.QrSvc.SetCheDoBan("");
                _c.ClearDeliverySession();
                _c.DocQrView.UnlockDocQrDeliveryContext();
                if (_c.Cfg.Delivery.CoGear)
                    _c.YmvnView.UnlockCheckListYMVN();
                _v.UnlockAllRadio();
                _v.UnlockDatePicker();
                _v.SwitchToPhieuView();
                DataTable current = _v.GetDonHangTable();
                DataTable latest = _c.PhieuSvc.GetDonHangHienTai(_c.TenBan);
                MergeDocQrResultIntoOrderTable(current, latest);
                _v.BindDonHang(current);
                _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(current));
                return;
            }
            _c.IsBanQR = false;
            _c.QrSvc.SetCheDoBan("");
            _c.ClearDeliverySession();
            _c.DocQrView.UnlockDocQrDeliveryContext();
            if (_c.Cfg.Delivery.CoGear)
                _c.YmvnView.UnlockCheckListYMVN();
            _v.UnlockAllRadio();
            _v.UnlockDatePicker();
            if (_c.GioXuatHienTai.Ma == "#")
            {
                DataTable d = _c.LoadPhieuGiaoDB();
                _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(d));
            }
            else
            {
                _v.SwitchToPhieuView();
                _c.LoadPhieuHienTai();
            }
        }, "Đang tổng hợp dữ liệu hoàn thành...");

        private static void MergeDocQrResultIntoOrderTable(DataTable current, DataTable latest)
        {
            if (current == null || latest == null || current.Rows.Count == 0 || latest.Rows.Count == 0)
                return;
            foreach (DataRow source in latest.Rows)
            {
                string stt = source.Table.Columns.Contains("STT") ? source["STT"]?.ToString().Trim() : "";
                if (string.IsNullOrEmpty(stt)) continue;
                DataRow target = current.AsEnumerable().FirstOrDefault(r => r.Table.Columns.Contains("STT") && string.Equals(r["STT"]?.ToString().Trim(), stt, StringComparison.OrdinalIgnoreCase));
                if (target == null) continue;
                CopyIfBothColumnsExist(source, target, "LOT");
                CopyIfBothColumnsExist(source, target, "STATUS");
                CopyIfBothColumnsExist(source, target, "STATUSDOC");
            }
        }

        private static void CopyIfBothColumnsExist(DataRow source, DataRow target, string columnName)
        {
            if (!source.Table.Columns.Contains(columnName) || !target.Table.Columns.Contains(columnName)) return;
            target[columnName] = source[columnName] == DBNull.Value ? (object)DBNull.Value : source[columnName];
        }

        private void OnLoaiPhieuChanged(object sender, EventArgs e)
        {
            if (!_formLoaded || _initializing)
                return;

            // MP/SP is a view mode, but each mode has its own DOCQR/TMP
            // session. Re-run the same persisted-session detection whenever
            // the user switches the view:
            //
            //   MP -> check DOCQRCODE + TMPPHIEUGIAOHANG
            //   SP -> check DOCQRCODE_SP + TMPPHIEUGIAOHANG_SP
            //
            // If the selected mode has no DOCQR data, that mode is NOT in a
            // QR session: unlock Date/Plant/Hour and load its normal orders.
            // If DOCQR exists, restore its TMP context and lock exactly as
            // during FormLoaded.
            XetTrangThai();
        }
        private void OnChonLotThuCong(object sender, ChonLotThuCongEventArgs e) { if (!_c.IsMayBanQR) return; DataTable lots = _c.PhieuSvc.GetDanhSachLotTuKho(e.MaHang); ChonLotResult r = _v.ShowChonLotTuKho(e.Stt, e.MaHang, e.SoLuong, lots); if (!r.Confirmed || string.IsNullOrWhiteSpace(r.LotGhep)) return; _c.PhieuSvc.NhapLotThuCong(e.Stt, r.LotGhep, _c.TenBan);
            _v.RefreshLotRow(e.Stt, r.LotGhep);
            _c.IsBanQR = true;
            bool isSpSession = _c.Cfg.Delivery.CoLoaiSP && _v.IsLoaiSP;
            _c.BeginDeliverySession(
                _v.SelectedDate,
                _c.AddNM,
                isSpSession ? string.Empty : _c.GioXuatHienTai.Ma,
                _c.GetNhaMay(),
                isSpSession);
            _v.LockRadioExcept(_c.GioXuatHienTai.Ma);
            _v.LockDatePicker();
            _c.DocQrView.LockDocQrDeliveryContext(
                isSpSession,
                isSpSession ? string.Empty : _c.GioXuatHienTai.Ma);
            DataTable dt = _c.PhieuSvc.GetDonHangHienTai(_c.TenBan); _c.SetupPhieuButtonsDefault(_c.PhieuSvc.CheckCanCapNhapKho(dt), false, _c.PhieuSvc.CheckCoLotChuaCNK(dt)); }
        private void OnLayLaiLotNo(object sender, LayLaiLotEventArgs e) { if (!_v.Confirm($"Bạn có chắc chắn muốn reset dữ liệu LOT của dòng có STT {e.Stt} không?")) return; _c.RunWithLoadingSync(() => { _c.PhieuSvc.LayLaiLotNo(e.Stt, _c.QrSvc.IsBanSP); _c.LoadPhieuHienTai(); }, "Đang xử lý lấy lại số LOT..."); }
        private void OnXemHangThieuCaNgay(object sender, EventArgs e) => _c.RunWithLoading(() => { DataTable dt = _c.HangThieuCaNgayService.TinhHangThieuCaNgay(_v.SelectedDate, _c.GetNhaMay(), _c.AddNM, _c.Cfg); _c.UiContext.Post(_ => _v.ShowHangThieuCaNgay(dt), null); }, "Đang tính hàng thiếu cả ngày...");
        private void XetTrangThai()
        {
            System.Diagnostics.Debug.WriteLine(
             $"[XetTrangThai] ENTER Thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            _c.RunWithLoadingSync(() => {
                System.Diagnostics.Debug.WriteLine(
            $"[XetTrangThai] INSIDE RunWithLoadingSync Thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                if (!_c.IsMayBanQR)
                {
                    _c.IsBanQR = false; _v.UnlockAllRadio(); _v.UnlockDatePicker();
                    _c.LoadPhieuHienTai(); return;
                }
                // The selected MP/SP view owns its own DOCQR table.
                // Never fall back from one category to the other here:
                // switching MP -> SP must re-evaluate DOCQRCODE_SP, and
                // switching SP -> MP must re-evaluate DOCQRCODE.
                bool selectedIsSP = _v.IsLoaiSP && _c.Cfg.Delivery.CoConfigSP;
                var tt = selectedIsSP
                    ? _c.PhieuSvc.GetTrangThaiDangBanSP()
                    : _c.PhieuSvc.GetTrangThaiDangBan();

                if (!tt.DangBan)
                {
                    // No DOCQR in the currently selected mode means there is
                    // no immutable QR context for THIS view. Do not delete the
                    // other mode's DOCQR/TMP session. Just unlock the header
                    // and load the selected mode normally.
                    _c.IsBanQR = false;
                    _c.ClearDeliverySession();
                    _c.QrSvc.SetCheDoBanSP(selectedIsSP);
                    _v.UnlockAllRadio();
                    _v.UnlockDatePicker();
                    _c.DocQrView.UnlockDocQrDeliveryContext();
                    _c.LoadPhieuHienTai();
                    return;
                }
                if (tt.DataKhongKhop)
                {
                    if (_c.DocQrView.HoiXoaDocQR()) _c.PhieuSvc.XoaDocQRCode(); _c.IsBanQR = false;
                    _c.QrSvc.SetCheDoBanSP(false); _v.UnlockAllRadio(); _v.UnlockDatePicker(); _c.LoadPhieuHienTai(); return;
                }
                if (!DateTime.TryParse(tt.NgayGiao, out DateTime ngay))
                {
                    _c.IsBanQR = false;
                    _c.ClearDeliverySession();
                    _c.QrSvc.SetCheDoBanSP(false);
                    _v.UnlockAllRadio();
                    _v.UnlockDatePicker();
                    _c.LoadPhieuHienTai();
                    return;
                }

                // IMPORTANT:
                // Mark QR mode BEFORE touching Date/Tab. SetDate/SetTab can
                // raise their events synchronously. If IsBanQR is still false,
                // those handlers start a normal IFS load and race the restore,
                // which can reset the context immediately after we lock it.
                _c.AddNM = _c.Cfg.Delivery.CoNhieuNhaMay ? tt.AddNM : _c.Cfg.Delivery.AddNmMacDinh;
                _c.IsBanQR = true;

                // Snapshot the TMP identity before touching any UI control.
                // This snapshot is reapplied after every PhieuLoadedEvent so a
                // later grid/header refresh cannot revert date/tab/hour to the
                // previous user context.
                _qrRestoreDate = ngay.Date;
                _qrRestoreAddNM = _c.AddNM;
                _qrRestoreConcreteHour = tt.GioGiaoFCC ?? string.Empty;

                if (_c.Cfg.Delivery.CoNhieuNhaMay)
                    _v.SetTab(_qrRestoreAddNM);
                _v.SetDate(_qrRestoreDate);

                // DIAGNOSTIC #1: đọc ngược lại control thật ngay sau khi gán.
                // Nếu dòng này in ra -> DevExpress không "nhận" giá trị gán
                // (rất có thể do control chưa HandleCreated lúc Form_Load chạy).
                if (_v.SelectedTabAddNM != _qrRestoreAddNM || _v.SelectedDate.Date != _qrRestoreDate.Date)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[XetTrangThai] Header did not accept restore: " +
                        $"expected ADDNM={_qrRestoreAddNM}/Date={_qrRestoreDate:yyyy-MM-dd}, " +
                        $"but control now shows ADDNM={_v.SelectedTabAddNM}/Date={_v.SelectedDate:yyyy-MM-dd}.");
                }

                if (_c.Cfg.Delivery.CoGear)
                {
                    var gs = _c.ParseGioYMVN(tt.GioGiaoFCC);
                    bool sp = _c.CategoryResolver.Resolve(new OrderLoadContext { GioFccMoTa = tt.GioGiaoFCC }) == OrderCategory.SP;
                    _c.QrSvc.SetCheDoBanSP(sp);
                    _c.BeginDeliverySession(
                        ngay.Date,
                        _c.AddNM,
                        tt.GioGiaoFCC,
                        _c.GetNhaMay(),
                        sp);
                    _v.SuspendGioXuatChanged();
                    try
                    {
                        _c.YmvnView.SetCheckedGiosYMVN(gs);
                        _c.YmvnView.LockCheckListYMVN();
                    }
                    finally { _v.ResumeGioXuatChanged(); }

                    // Khoá ngay ngày + loại phiếu + toàn bộ hour selector của session.
                    _c.DocQrView.LockDocQrDeliveryContext(sp, sp ? string.Empty : tt.GioGiaoFCC);
                    _c.LoadPhieuHienTai();
                    return;
                }

                // TMP.GIOGIAO is a concrete delivery hour (for example "15").
                // The Radio may represent a group (for example "'15','16'").
                // Resolve the Radio by its Ma/hour-set, never by parsing MoTa.
                string gio = tt.GioGiaoFCC, ma = "", mota = "";
                var ds = _c.AddNM == 1
                    ? _c.GioXuatRepo.GetDanhSachGioVP()
                    : _c.GioXuatRepo.GetDanhSachGioHN();

                foreach (var g in ds)
                {
                    if (DeliverySessionIdentity.ContainsHour(g.Ma, gio))
                    {
                        ma = g.Ma;
                        mota = g.MoTa;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(ma))
                {
                    // Preserve special/non-standard hours, but do not invent a
                    // grouped radio identity when the repository has no match.
                    ma = $"'{gio.Trim().Trim('\'')}'";
                    mota = gio + "H";
                }

                bool isSpSession = _c.CategoryResolver.Resolve(
                    new OrderLoadContext { GioFccMoTa = mota }) == OrderCategory.SP;
                _qrRestoreIsSP = isSpSession;

                _c.QrSvc.SetCheDoBanSP(isSpSession);

                _v.SuspendGioXuatChanged();
                try
                {
                    // IMPORTANT: update the real DevExpress RadioGroup selection,
                    // not only HVNPresenterContext.GioXuatHienTai. TMP stores a
                    // concrete hour (15), while the Radio item can be a group
                    // ('15','16'). The header resolves the containing item and
                    // becomes the single source of truth for the visible UI.
                    bool radioRestored = _v.SelectGioXuatByConcreteHour(gio);
                    System.Diagnostics.Debug.WriteLine(
                    $"[XetTrangThai] AFTER SelectGioXuatByConcreteHour " +
                    $"gio={gio}, " +
                    $"radioRestored={radioRestored}, " +
                    $"AddNM={_c.AddNM}, " +
                    $"Time={DateTime.Now:HH:mm:ss.fff}");
                    // FIX: luôn đồng bộ GioXuatHienTai, không chỉ khi restore thất bại.
                    _c.GioXuatHienTai = new GioXuat(ma, mota);

                    if (!radioRestored)
                    {
                        // DIAGNOSTIC #2: đây chính là dấu hiệu Radio bị "kẹt" ở item
                        // mặc định (ví dụ 6H) thay vì giờ thật từ TMP.
                        System.Diagnostics.Debug.WriteLine(
                            $"[XetTrangThai] SelectGioXuatByConcreteHour('{gio}') FAILED " +
                            $"for ADDNM={_c.AddNM} — Radio still shows its default item.");
                    }
                }
                finally { _v.ResumeGioXuatChanged(); }

                // The immutable session keeps the concrete TMP hour. Validation
                // accepts the selected Radio only when that hour belongs to its
                // hour-set.
                _c.BeginDeliverySession(
                    ngay.Date,
                    _c.AddNM,
                    gio,
                    _c.GetNhaMay(),
                    isSpSession);

                // QR + TMP metadata now define the exact date/factory/hour.
                _c.DocQrView.LockDocQrDeliveryContext(
                    isSpSession,
                    isSpSession ? string.Empty : ma);
                _c.LoadPhieuHienTai();
            }, "Đang kiểm tra trạng thái phiên làm việc cũ...");
        }

        private void RestoreQrHeaderFromSnapshot()
        {
            if (!_c.IsBanQR || _qrRestoreDate == DateTime.MinValue)
                return;

            _v.SuspendGioXuatChanged();
            try
            {
                if (_c.Cfg.Delivery.CoNhieuNhaMay)
                    _v.SetTab(_qrRestoreAddNM);

                _v.SetDate(_qrRestoreDate);

                if (!_qrRestoreIsSP && !string.IsNullOrWhiteSpace(_qrRestoreConcreteHour))
                {
                    bool radioRestored = _v.SelectGioXuatByConcreteHour(_qrRestoreConcreteHour);
                    if (!radioRestored)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RestoreQrHeaderFromSnapshot] SelectGioXuatByConcreteHour('{_qrRestoreConcreteHour}') " +
                            $"FAILED for ADDNM={_qrRestoreAddNM}, Date={_qrRestoreDate:yyyy-MM-dd}.");
                    }
                }
            }
            finally
            {
                _v.ResumeGioXuatChanged();
            }
        }

        private void OnPhieuLoaded(PhieuLoadedEvent e)
        {
            DataTable data = e?.DonHangTable;
            string caption = e?.Caption ?? string.Empty;

            _c.UiContext.Post(_ =>
            {
                // The caption is part of the same OrderLoadContext as the
                // DataTable. Do not leave the previous plant/hour caption on
                // the grid when a restored QR session is loaded.
                _v.SetGridCaption(caption);
                _v.BindDonHang(data ?? new DataTable());

                _c.SetupPhieuButtonsDefault(
                    true,
                    e != null && e.CoMaNG,
                    _c.PhieuSvc.CheckCoLotChuaCNK(data));

                _c.IsLoadingPhieu = false;
                _c.AwaitingPhieuLoadedEvent = false;
                _c.HideLoadingUnlessAwaitingPhieuLoad();

                // IMPORTANT: this must be the LAST synchronous UI operation
                // in the load callback. Some legacy button/grid setup code can
                // touch the header indirectly. TMP/DOCQRCODE remains the
                // immutable source of truth for the active QR session.
                RestoreQrHeaderFromSnapshot();

                // Also queue one final restore behind any BeginInvoke work
                // already posted by DevExpress/legacy controls during binding.
                if (_c.IsBanQR)
                {
                    _c.UiContext.Post(__ => RestoreQrHeaderFromSnapshot(), null);
                }
            }, null);
        }
        private void OnKhoUpdated(KhoUpdatedEvent e)
        {
            _c.UiContext.Post(_ =>
            {
                if (e != null && e.Errors != null && e.Errors.Rows.Count > 0)
                {
                    bool onlyFifo = true;
                    foreach (DataRow row in e.Errors.Rows)
                    {
                        string status = row.Table.Columns.Contains("STATUS") ? row["STATUS"]?.ToString() ?? string.Empty : string.Empty;
                        if (status.IndexOf("FIFO:", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            onlyFifo = false;
                            break;
                        }
                    }

                    if (onlyFifo)
                    {
                        _v.ShowWarning(BuildFifoWarning(e.Errors));
                        _c.LoadPhieuHienTai();
                        return;
                    }

                    _v.ShowLoiCapNhapKho(e.Errors);
                    return;
                }
                _c.LoadPhieuHienTai();
            }, null);
        }

        private static string BuildFifoWarning(DataTable errors)
        {
            var lines = new List<string>();
            foreach (DataRow row in errors.Rows)
            {
                string stt = row.Table.Columns.Contains("STT") ? row["STT"]?.ToString() ?? "" : "";
                string mh = row.Table.Columns.Contains("MH") ? row["MH"]?.ToString() ?? "" : "";
                string lot = row.Table.Columns.Contains("LOT") ? row["LOT"]?.ToString() ?? "" : "";
                string status = row.Table.Columns.Contains("STATUS") ? row["STATUS"]?.ToString() ?? "" : "";
                lines.Add(string.Format("STT: {0}\nMã hàng: {1}\nLOT đã chọn: {2}\n{3}", stt, mh, lot, status));
            }

            return "CẢNH BÁO FIFO – TỒN KHO ĐÃ THAY ĐỔI\n\n" +
                   string.Join("\n\n--------------------\n\n", lines) +
                   "\n\nCác dòng FIFO không hợp lệ đã được lấy lại LOT và không được đưa vào lần cập nhật kho này. Vui lòng quét/chọn lại LOT FIFO hiện tại.";
        }
        private void OnTinhTongCompleted(TinhTongCompletedEvent e)
        {
            var results = e?.Results;
            _c.UiContext.Post(_ =>
            {
                DataTable current = _v.GetDonHangTable();
                ApplyTinhTongResults(current, results);
                _v.BindDonHang(current);
                _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(current));
            }, null);
        }

        private static void ApplyTinhTongResults(DataTable table, IReadOnlyList<(int Stt, string Lot)> results)
        {
            if (table == null || results == null || results.Count == 0 || !table.Columns.Contains("STT") || !table.Columns.Contains("LOT")) return;
            foreach (var result in results)
            {
                DataRow target = table.AsEnumerable().FirstOrDefault(r => string.Equals(r["STT"]?.ToString().Trim(), result.Stt.ToString(), StringComparison.OrdinalIgnoreCase));
                if (target != null) target["LOT"] = result.Lot ?? string.Empty;
            }
        }

        public void Dispose() { var v = _v; v.FormLoaded -= OnFormLoaded; v.DateChanged -= OnDateChanged; v.GioXuatChanged -= OnGioXuatChanged; v.TabChanged -= OnTabChanged; v.CapNhapKhoClicked -= OnCapNhapKho; v.InPhieuClicked -= OnInPhieu; v.InGhepLotClicked -= OnInGhepLot; v.InTachLotClicked -= OnInTachLot; v.KiemTraGhepLotClicked -= OnKiemTraGhepLot; v.KiemTraMaNGClicked -= OnKiemTraMaNG; v.HoanThanhClicked -= OnHoanThanh; v.LoaiPhieuChanged -= OnLoaiPhieuChanged; v.ChonLotThuCongClicked -= OnChonLotThuCong; v.XemHangThieuCaNgayClicked -= OnXemHangThieuCaNgay; v.LayLaiLotNoClicked -= OnLayLaiLotNo; v.CapNhapTTPHIEUClicked -= OnCapNhapTTPHIEU; _c.Bus.Unsubscribe<PhieuLoadedEvent>(OnPhieuLoaded); _c.Bus.Unsubscribe<KhoUpdatedEvent>(OnKhoUpdated); _c.Bus.Unsubscribe<TinhTongCompletedEvent>(OnTinhTongCompleted); }
    }
}
