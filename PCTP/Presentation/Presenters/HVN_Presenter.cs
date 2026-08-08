using DevExpress.XtraReports.UI;
using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure.Repositories;
using PCTP.Presentation.Views;
using PCTP.QRCODE_HVN.Report;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static DevExpress.Utils.Diagnostics.GUIResources;

namespace PCTP.Presentation.Presenters
{
    /// <summary>
    /// Điều phối giữa View ↔ Service ↔ EventBus.
    /// Không import DevExpress, System.Windows.Forms dialog, hay SQL.
    /// Unit test được bằng cách mock IHVNView.
    /// </summary>
    public class HVN_Presenter : IDisposable
    {
        private readonly IHVNView _view;
        private readonly PhieuService _phieuSvc;
        private readonly DocQRService _qrSvc;
        private readonly InPhieuService _inPhieuSvc;
        private readonly IGioXuatRepository _gioXuatRepo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;
        // ✅ Lưu trữ context của UI Thread ngay từ Constructor để tránh NullReferenceException
        private readonly SynchronizationContext _uiContext;

        // ── Trạng thái ───────────────────────────────────────────────────────
        private GioXuat _gioXuatHienTai = new GioXuat("'06'", "(6H)");
        private int _addNM = 1;
        private readonly bool _isMayBanQR;
        private readonly string _tenBan;
        private bool _isBanQR = false;
      
        public int AddNM => _addNM;
        public bool IsBanQR => _isBanQR;
        public GioXuat GioXuatHienTai => _gioXuatHienTai;

        // ════════════════════════════════════════════════════════════════════
        // Constructor
        // ════════════════════════════════════════════════════════════════════
        public HVN_Presenter(IHVNView view,
                             PhieuService phieuSvc,
                             DocQRService qrSvc,
                             InPhieuService inPhieuSvc,
                             IGioXuatRepository gioXuatRepo,
                             IEventBus bus,
                             bool isMayBanQR,
                             string tenBan,
                             CustomerConfig cfg)
        {
            _view = view;
            _phieuSvc = phieuSvc;
            _qrSvc = qrSvc;
            _inPhieuSvc = inPhieuSvc;
            _gioXuatRepo = gioXuatRepo;
            _bus = bus;
            _isMayBanQR = isMayBanQR;
            _tenBan = tenBan;
            _cfg = cfg;
            _isBanQR = false;

            // ✅ Gán context của UI thread chủ động
            _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            SubscribeViewEvents();
            SubscribeDomainEvents();
        }

        private void SubscribeViewEvents()
        {
            _view.FormLoaded += OnFormLoaded;
            _view.DateChanged += OnDateChanged;
            _view.GioXuatChanged += OnGioXuatChanged;
            _view.TabChanged += OnTabChanged;
            _view.CapNhapKhoClicked += OnCapNhapKho;
            _view.InPhieuClicked += OnInPhieu;
            _view.InGhepLotClicked += OnInGhepLot;
            _view.InTachLotClicked += OnInTachLot;
            _view.DocQRCodeClicked += OnDocQRCode;
            _view.KiemTraGhepLotClicked += OnKiemTraGhepLot;
            _view.KiemTraMaNGClicked += OnKiemTraMaNG;
            _view.QRCodeSubmitted += OnQRCodeSubmitted;
            _view.HoanThanhClicked += OnHoanThanh;
            _view.XoaDongQRClicked += OnXoaDongQR;
            _view.XoaToanBoQRClicked += OnXoaToanBoQR;
            _view.SuaSoLuongTemClicked += OnSuaSoLuongTem;
            _view.LayLaiLotNoClicked += OnLayLaiLotNo;
            _view.ThemDongGiaoDBClicked += OnThemDongGiaoDB;
            _view.XoaDongGiaoDBClicked += OnXoaDongGiaoDB;
            _view.LuuGiaoDBClicked += OnLuuGiaoDB;
            _view.CapNhapTTPHIEUClicked += OnCapNhapTTPHIEU;
            _view.HoanThanhYMVNClicked += OnHoanThanhYMVN;
            _view.UploadMilkrunSPClicked += OnUploadMilkrunSP;
            _view.LoaiPhieuChanged += OnLoaiPhieuChanged;
            _view.GioXuatCheckedChanged += OnGioXuatCheckedChanged;

            _view.ChonLotThuCongClicked += OnChonLotThuCong;
        }

        private void SubscribeDomainEvents()
        {
            // Sử dụng đặt tên hàm tường minh thay vì dùng lambda để có thể Unsubscribe lúc Dispose
            _bus.Subscribe<PhieuLoadedEvent>(OnPhieuLoaded);
            _bus.Subscribe<KhoUpdatedEvent>(OnKhoUpdated);
            _bus.Subscribe<TinhTongCompletedEvent>(OnTinhTongCompleted);
            _bus.Subscribe<QRScannedEvent>(OnQRScanned);
        }

        // ════════════════════════════════════════════════════════════════════
        // Domain Event Handlers (Đã bóc tách từ Lambda ra hàm rõ ràng)
        // ════════════════════════════════════════════════════════════════════
       
        private void OnPhieuLoaded(PhieuLoadedEvent e)
        {
            // Đẩy toàn bộ logic cập nhật giao diện về luồng chính (UI Thread) an toàn
            _uiContext.Post(_ =>
            {
                // ── Reset debounce flag ĐẦU TIÊN ─────────────────────────────
                _isLoadingPhieu = false;

                try
                {
                    // 1. Bind dữ liệu lên lưới
                    _view.BindDonHang(e.DonHangTable);
                    _view.BindHangThieu(e.HangThieuTable);
                    _view.SetGridCaption(e.Caption);
                    _view.ShowLoading(false);

                    // 2. Kiểm tra các điều kiện logic nghiệp vụ
                    bool coMaNG = !_cfg.CoGear && _phieuSvc.CheckCoMaNG();
                    bool showCNK = _phieuSvc.CheckCanCapNhapKho(e.DonHangTable);
                    bool showLayLai = _isMayBanQR
                             && _phieuSvc.CheckCoLotChuaCNK(e.DonHangTable);

                    // 3. Cập nhật trạng thái các nút bấm
                    _view.SetupPhieuButtons(
                        showCapNhapKho: showCNK && _isMayBanQR,
                        showKiemTraMaNG: coMaNG && _isMayBanQR,
                        showGhepLot: _isMayBanQR,
                        showDocQRCode: _isMayBanQR ,  // YMVN không có DOC QRCODE
                        showLayLaiLot: showLayLai);
                }
                catch (Exception ex)
                {
                    _view.ShowLoading(false);
                    _view.ShowError($"Lỗi khi hiển thị dữ liệu phiếu: {ex.Message}");
                }
            }, null);
        }
        //private void OnKhoUpdated(KhoUpdatedEvent message)
        //{
        //    _uiContext.Post(_ =>
        //    {
        //        bool coLoi = message.Errors != null && message.Errors.Rows.Count > 0;

        //        if (coLoi)
        //        {
        //            // ── CNK có lỗi tồn kho → chỉ hiển thị lỗi, KHÔNG reset, KHÔNG reload
        //            // User phải sửa lỗi rồi CNK lại — phiếu giữ nguyên trạng thái
        //            _view.ShowLoiCapNhapKho(message.Errors);
        //            return; // ← thoát sớm, không làm gì thêm
        //        }

        //        // ── CNK thành công hoàn toàn ─────────────────────────────────────
        //        if (message.SoLotCapNhap > 0)
        //            _view.ShowInfo($"Đã cập nhật {message.SoLotCapNhap} LOT thành công.");

        //        // Reset trạng thái sau CNK thành công
        //        _isBanQR = false;
        //        _qrSvc.SetCheDoBanSP(false);
        //        _view.UnlockAllRadio();



        //        // Load lại phiếu — lúc này DOCQRCODE đã xóa
        //        // XetTrangThai → !DangBan → LoadPhieuHienTai đúng, không hỏi xóa DocQR
        //        RunWithLoadingSync(() =>
        //        {
        //            LoadPhieuHienTai();
        //        }, "Đang tải lại dữ liệu phiếu...");

        //    }, null);
        //}
        private void OnKhoUpdated(KhoUpdatedEvent message)
        {
            _uiContext.Post(_ =>
            {
                bool coLoi = message.Errors != null && message.Errors.Rows.Count > 0;
                if (coLoi)
                {
                    _view.ShowLoiCapNhapKho(message.Errors);
                    return;
                }

                if (message.SoLotCapNhap > 0)
                    _view.ShowInfo($"Đã cập nhật {message.SoLotCapNhap} LOT thành công.");

                _isBanQR = false;
                _qrSvc.SetCheDoBanSP(false);
                _view.UnlockAllRadio();
                _view.UnlockDatePicker(); // ← unlock sau CNK thành công
                if (_cfg.CoGear)
                    _view.UnlockCheckListYMVN();
                RunWithLoadingSync(() =>
                {
                    LoadPhieuHienTai();
                }, "Đang tải lại dữ liệu phiếu...");
            }, null);
        }
        private void OnChonLotThuCong(object sender, ChonLotThuCongEventArgs e)
        {
            if (!_isMayBanQR) return;

            DataTable danhSachLot = _phieuSvc.GetDanhSachLotTuKho(e.MaHang);
            ChonLotResult result = _view.ShowChonLotTuKho(
                e.Stt, e.MaHang, e.SoLuong, danhSachLot);

            if (!result.Confirmed) return;
            if (string.IsNullOrWhiteSpace(result.LotGhep)) return;

            _phieuSvc.NhapLotThuCong(e.Stt, result.LotGhep, _tenBan);
            _view.RefreshLotRow(e.Stt, result.LotGhep);

            // ── Set _isBanQR = true để bảo vệ TMPPHIEUGIAOHANG ─────────────
            // Tương tự như đang bắn QR dở — không cho LuuVaLoad DELETE
            // Sẽ được reset về false sau khi CNK xong (OnKhoUpdated)
            _isBanQR = true;
            _view.LockRadioExcept(_gioXuatHienTai.Ma);  // lock radio giờ hiện tại

            // Cập nhật nút
            DataTable dtTmp = _phieuSvc.GetDonHangHienTai(_tenBan);
            bool showCNK = _phieuSvc.CheckCanCapNhapKho(dtTmp);
            bool showLayLai = _phieuSvc.CheckCoLotChuaCNK(dtTmp);
            bool coMaNG = !_cfg.CoGear && _phieuSvc.CheckCoMaNG();

            _view.SetupPhieuButtons(
                showCapNhapKho: showCNK && _isMayBanQR,
                showKiemTraMaNG: coMaNG && _isMayBanQR,
                showGhepLot: _isMayBanQR,
                showDocQRCode: _isMayBanQR && !_cfg.CoGear,
                showLayLaiLot: showLayLai && _isMayBanQR);
        }
        private void OnTinhTongCompleted(TinhTongCompletedEvent e)
        {
            foreach (var (stt, lot) in e.Results)
                _view.RefreshLotRow(stt, lot);
        }

        private void OnQRScanned(QRScannedEvent e)
        {
            _view.ClearQRInput();
            DataTable qrData = _qrSvc.LoadAll();
            _view.BindDocQRCode(qrData);
        }
        private void OnLoaiPhieuChanged(object sender, EventArgs e)
        {
            // Reload phiếu theo loại mới (MP hoặc SP)
            LoadPhieuHienTai();
        }
        // ════════════════════════════════════════════════════════════════════
        // Core handlers
        // ════════════════════════════════════════════════════════════════════
        private void OnFormLoaded(object sender, EventArgs e)
        {
            if (_cfg.CoGear)
            {
                // YMVN: không dùng addNM tab
                _addNM = _cfg.AddNmMacDinh;
                // Load danh sách giờ từ Purchase_Order_YMVN
                LoadGioXuatYMVN();
            }
            else
            {
                if (_cfg.CoNhieuNhaMay)
                    _addNM = _view.SelectedTabAddNM;
                else
                    _addNM = _cfg.AddNmMacDinh;
            }
            XetTrangThai();
        }
        private void LoadGioXuatYMVN()
        {
            var danhSachGio = _phieuSvc.GetDanhSachGioYMVN(
                _view.SelectedDate.ToString("MM/dd/yyyy"));

            _view.BindGioXuatCheckList(danhSachGio);

            if (danhSachGio.Count == 0) return;

            // Build GioXuat từ tất cả giờ (mặc định check hết)
            UpdateGioXuatFromCheckList(danhSachGio);
        }
        private void OnDateChanged(object sender, EventArgs e)
        {
            // Không có thuộc tính UI nào cần đọc trước, gọi trực tiếp wrapper rất chuẩn
            RunWithLoading(() =>
            {
                if (_cfg.CoGear)
                    LoadGioXuatYMVN();

                LoadPhieuHienTai(); // Hàm này đã được refactor an toàn luồng
            }, "Đang chuyển ngày...");
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (!_cfg.CoNhieuNhaMay) return;

            // ── BƯỚC 1: Đọc giá trị UI từ luồng chính (UI Thread) trước ──
            int selectedTab = _view.SelectedTabAddNM;

            // ── BƯỚC 2: Đẩy tác vụ nạp dữ liệu xuống luồng phụ ──
            RunWithLoading(() =>
            {
                _addNM = selectedTab;
                LoadPhieuHienTai();
            }, "Chuyển nhà máy...");
        }

        private void OnGioXuatChanged(object sender, EventArgs e)
        {
            RunWithLoading(() =>
            {
                if (_cfg.CoGear)
                {
                    LoadPhieuHienTai();
                    return;
                }

                // Bắn sự kiện EventBus: Ép sự kiện này phải được xử lý trên UI Thread 
                // để bảo vệ các hàm nhận sự kiện (Subscribers) không bị lỗi luồng.
                _uiContext.Send(_ =>
                {
                    _bus.Publish(new GioXuatChangedEvent(_gioXuatHienTai, _addNM));
                }, null);

                LoadPhieuHienTai();
            }, "Chuyển giờ xuất...");
        }

        private void OnCapNhapKho(object sender, EventArgs e)
            => RunWithLoadingSync(() =>
            {
                // 1. Nhánh YMVN (Gọi hàm con, an toàn tuyệt đối trên UI Thread)
                if (_cfg.CoGear)
                {
                    OnCapNhapKhoYMVN();
                    return;
                }
                // ── HTN (LoadTuBangRieng + !CoGear) ──────────────────────────────
                // Không có giờ xuất, không có loại SP — CNK theo ngày
                if (_cfg.LoadTuBangRieng)
                {
                    if (!_view.CoLotDeLuuKho())
                    {
                        _view.ShowInfo("Không có dữ liệu cho CNK !!!!!");
                        return;
                    }
                    string nhaMayHTN = GetNhaMay();
                    // HTN: gioGiaoFcc = "" vì không filter theo giờ
                    _phieuSvc.CapNhapKho("", nhaMayHTN);
                    return;
                }
                // 2. Kiểm tra điều kiện nghiệp vụ dựa trên UI
                bool isLoaiSP = PhieuService.IsLoaiSP(_gioXuatHienTai.MoTa);

                if (!isLoaiSP && !_view.CoLotDeLuuKho())
                {
                    _view.ShowInfo("Không có dữ liệu cho CNK !!!!!");
                    return;
                }

                // Lấy thông tin cần thiết từ UI trước khi ghi xuống DB
                string nhaMay = GetNhaMay();
                string ngayGiao = _view.SelectedDate.ToString("yyyy-MM-dd");

                // 3. Thực hiện lưu phiếu nếu là loại sản phẩm (SP)
                if (isLoaiSP)
                {
                    _phieuSvc.LuuPhieuSP(
                        nhaMay,
                        ngayGiao,
                        _gioXuatHienTai.MoTa,
                        _gioXuatHienTai.Ma);
                }

                // 4. Thực hiện Cập nhật kho (Đã xóa hoàn toàn try-catch và ShowLoading thủ công)
                _phieuSvc.CapNhapKho(_gioXuatHienTai.MoTa, nhaMay, _gioXuatHienTai.Ma);

            }, "Đang cập nhật kho...");

        private void OnCapNhapKhoYMVN()
        {
            // KHÔNG gọi _view.ShowLoading(true) nữa vì hàm cha OnCapNhapKho đã bật rồi

            // 1. Đọc dữ liệu từ UI an toàn (Vì đang chạy trên UI Thread thông qua RunWithLoadingSync)
            var checkedGios = _view.GetCheckedGioXuat();
            if (!checkedGios.Any())
            {
                _view.ShowInfo("Bạn chưa chọn giờ xuất!");
                return;
            }

            string gioXuat = string.Join(",", checkedGios.Select(g => $"'{g}'"));
            string ngayGiao = _view.SelectedDate.ToString("MM/dd/yyyy");
            DataTable dtDonHang = _view.GetDonHangTable();

            // 2. Gọi Service xử lý Database nghiệp vụ nặng
            _phieuSvc.CapNhapKhoYMVN(ngayGiao, gioXuat, GetNhaMay(), dtDonHang);

            // KHÔNG cần try-catch-finally tại đây. Nếu Service ném ra ngoại lệ (Exception),
            // wrapper RunWithLoadingSync ở hàm cha sẽ tự động bắt được để hiển thị thông báo lỗi 
            // và tắt Loading một cách chuẩn hóa.
        }
        private void OnCapNhapTTPHIEU(object sender, TTPHIEUEventArgs e)
    => RunWithLoadingSync(() =>
    {
        // 1. Kiểm tra điều kiện (Nếu không thỏa mãn thì ngắt sớm)
        if (!PhieuService.IsLoaiSP(_gioXuatHienTai.Ma)) return;

        // 2. Đọc dữ liệu từ UI một cách an toàn trên luồng chính
        string ngayGiao = _view.SelectedDate.ToString("yyyy-MM-dd");
        string nhaMay = GetNhaMay();

        // 3. Gọi Service cập nhật thông tin xuống Database
        _phieuSvc.CapNhapTTPHIEU(
            nhaMay,
            ngayGiao,
            _gioXuatHienTai.MoTa,
            e.Stt,
            e.GhiChu);

        // Không cần viết try-catch ở đây. Nếu DB bị lỗi (timeout, mất kết nối...), 
        // wrapper RunWithLoadingSync sẽ tự động bẫy lỗi, hiển thị ShowError và tắt Loading.
    }, "Đang cập nhật thông tin phiếu...");
        private void OnInPhieu(object sender, EventArgs e)
        {
            // ── BƯỚC 1: Hỏi hình thức in — chỉ HVN mới cần ─────────────────────
            int hinhThucIn = 0;

            bool canHoiHinhThuc = !_cfg.CoGear              // không phải YMVN
                               && !_cfg.LoadTuBangRieng      // không phải HTN
                               && _gioXuatHienTai.Ma != "#"; // không phải GIAO DB

            if (canHoiHinhThuc)
            {
                hinhThucIn = _view.ShowChonHinhThucIn();
                if (hinhThucIn == -1) return;
            }

            // ── BƯỚC 2: Build report ─────────────────────────────────────────────
            RunWithLoadingSync(() =>
            {
                DataTable data;

                // ── YMVN: report riêng ───────────────────────────────────────────
                if (_cfg.CoGear)
                {
                    data = _view.GetDonHangTable();
                    _view.ShowReportYMVN(data);
                    return;
                }

                // ── HTN: load từ bảng riêng → dùng report giống YMVN hoặc report chung
                // HTN không qua IFS nên không dùng BuildReportData
                if (_cfg.LoadTuBangRieng)
                {
                    string ngayXuatBR = _view.SelectedDate.ToString("ddMMyyyy");
                    DataTable donHangBR = _view.GetDonHangTable();
                    DataTable dtAddrBR = _view.GetAddressTable();

                    DataTable dataBR = _inPhieuSvc.BuildReportDataTuBangRieng(
                        donHangBR, dtAddrBR, ngayXuatBR);

                    if (_cfg.CoGear)
                        _view.ShowReportYMVN(dataBR);
                    else
                    {
                        _view.ShowReportWithGioHeader(dataBR,
                        _cfg.LoadTheoNgay ? "PO No" : "Giờ");

                    }
                    return;
                }

                // ── GIAO DB ──────────────────────────────────────────────────────
                if (_gioXuatHienTai.Ma == "#")
                {
                    data = _inPhieuSvc.BuildReportDataGiaoDB(_view.GetDonHangTable());
                    _view.ShowReport(data);
                    return;
                }

                // ── HVN: build report từ IFS ─────────────────────────────────────
                string ngayGiao = _view.SelectedDate.ToString("ddMMyyyy");
                DataTable dtAddr = _view.GetAddressTable();
                string nhaMay = GetNhaMay();

                data = _inPhieuSvc.BuildReportData(
                    ngayGiao,
                    _gioXuatHienTai.Ma,
                    _gioXuatHienTai.MoTa,
                    nhaMay,
                    _addNM,
                    hinhThucIn,
                    dtAddr);

                _view.ShowReport(data);

            }, "Đang khởi tạo biểu mẫu in...");
        }

        //private void OnInPhieu(object sender, EventArgs e)
        //{
        //    // BƯỚC 1: Xử lý các tương tác UI / Hỏi ý kiến User trước khi bật Loading
        //    int hinhThucIn = 0;

        //    // Nếu không phải Gear và không phải mã "#" thì mới cần hiện Dialog hỏi User
        //    if (!_cfg.CoGear && _gioXuatHienTai.Ma != "#")
        //    {
        //        hinhThucIn = _view.ShowChonHinhThucIn();

        //        // Nếu user bấm Cancel hoặc tắt Dialog không chọn (giả định trả về -1 hoặc 0 tùy code của bạn)
        //        if (hinhThucIn == -1) return;
        //    }

        //    // BƯỚC 2: Bật wrapper quản lý Loading để xử lý tác vụ tính toán dữ liệu nặng
        //    RunWithLoadingSync(() =>
        //    {
        //        DataTable data;

        //        if (_cfg.CoGear)
        //        {
        //            // YMVN: dùng report riêng
        //            data = _view.GetDonHangTable();
        //            _view.ShowReportYMVN(data);
        //            return;
        //        }

        //        if (_gioXuatHienTai.Ma == "#")
        //        {
        //            data = _inPhieuSvc.BuildReportDataGiaoDB(_view.GetDonHangTable());
        //        }
        //        else
        //        {
        //            // Đọc các thông tin UI còn lại an toàn trên UI Thread
        //            string ngayGiao = _view.SelectedDate.ToString("ddMMyyyy");
        //            DataTable dtAddress = _view.GetAddressTable();
        //            string nhaMay = GetNhaMay();

        //            // Gọi dịch vụ Build Report (Tác vụ nặng chiếm dụng CPU/Memory)
        //            data = _inPhieuSvc.BuildReportData(
        //                ngayGiao,
        //                _gioXuatHienTai.Ma,
        //                _gioXuatHienTai.MoTa,
        //                nhaMay,
        //                _addNM,
        //                hinhThucIn, // Biến đã được lấy sẵn từ trước khi Loading bật
        //                dtAddress);
        //        }

        //        // Hiển thị màn hình Report
        //        _view.ShowReport(data);

        //    }, "Đang khởi tạo biểu mẫu in...");
        //}

        private void OnInGhepLot(object sender, EventArgs e)
        {
            // 1. Đọc danh sách các dòng được chọn từ UI trước
            var selectedRows = _view.GetSelectedGhepLotRows();

            DataTable reportData = null;

            // 2. Bật Loading để chạy tác vụ truy vấn và build dữ liệu in (Tác vụ nặng)
            RunWithLoadingSync(() =>
            {
                reportData = _inPhieuSvc.InGhepLot(selectedRows.Any() ? selectedRows : null);
            }, "Đang tổng hợp dữ liệu ghép LOT...");

            // 3. Sau khi RunWithLoadingSync chạy xong, Loading đã tự đóng giải phóng UI.
            // Lúc này hiện Dialog Preview lên là an toàn và mượt mà nhất.
            if (reportData != null)
            {
                var report = new GHEPLOT { DataSource = reportData };
                new ReportPrintTool(report).ShowPreviewDialog();
            }
        }

        private void OnInTachLot(object sender, EventArgs e) => _view.ShowTachLot();

        private void OnKiemTraGhepLot(object sender, EventArgs e)
    => RunWithLoading(() =>
    {
        // 1. Tải dữ liệu từ DB dưới luồng phụ (Async)
        DataTable dt = _phieuSvc.LoadGhepLot();

        // 2. Đồng bộ kết quả trả về luồng chính (UI Thread) để gán lên lưới
        _uiContext.Post(_ =>
        {
            _view.BindGhepLot(dt);
        }, null);

    }, "Đang kiểm tra ghép LOT...");

        private void OnKiemTraMaNG(object sender, EventArgs e)
        {
            string ma = _view.GetFocusedDonHangMaHang();
            if (string.IsNullOrWhiteSpace(ma)) return;
            _view.ShowKiemTraMaNG(ma);
        }

        private void OnDocQRCode(object sender, EventArgs e)
        {
            if (!_isMayBanQR)
            {
                _view.ShowInfo("Bạn chỉ sử dụng được tính năng này trên máy bắn QR.");
                return;
            }
            if (!_view.CoHangChuaOK())
            {
                _view.ShowInfo("Phiếu không đủ điều kiện để đọc QRCODE. Hoặc đã đọc xong dữ liệu.");
                return;
            }

            // ── Xác định isSP theo từng customer ────────────────────────────────
            // HVN  : từ radio giờ (O TYPE = SP)
            // YMVN : từ toggle button _view.IsLoaiSP
            // HTN  : từ toggle button _view.IsLoaiSP (nếu có SP/MP, không thì false)
            bool isSP;
            if (_cfg.LoadTuBangRieng)
                isSP = _cfg.CoLoaiSP && _view.IsLoaiSP;  // YMVN/HTN
            else
                isSP = PhieuService.IsLoaiSP(_gioXuatHienTai.MoTa);  // HVN

            // ── SetCheDoBan — chỉ HVN mới có ý nghĩa theo giờ ───────────────────
            // YMVN/HTN không có radio giờ → truyền rỗng
            string cheDoBan = _cfg.LoadTuBangRieng ? "" : _gioXuatHienTai.MoTa;
            _qrSvc.SetCheDoBan(cheDoBan);
            _qrSvc.SetCheDoBanSP(isSP);

            // ── Đọc UI trước khi vào background thread ───────────────────────────
            DataTable dtPhieu = _cfg.LoadTuBangRieng ? _view.GetDonHangTable() : null;
            string ngay = _view.SelectedDate.ToString("yyyy-MM-dd");
            List<string> gios = _cfg.CoGear ? _view.GetCheckedGioXuat() : null;

            _isBanQR = true;

            RunWithLoading(() =>
            {
                try
                {
                    if (_cfg.LoadTuBangRieng)
                    {
                        // ── YMVN + HTN: sync từ grid, không qua IFS Oracle ───────
                        // YMVN (CoGear=true): truyền checkedGios để filter giờ
                        // HTN  (LoadTheoNgay=true): gios=null → không filter giờ
                        if (_qrSvc.CountChuaDG() == 0 && !_qrSvc.CoDocQRNao())
                            _phieuSvc.SyncPhieuTuBangRiengChoDocQR(
                                dtPhieu, ngay, gios);
                    }
                    else
                    {
                        // ── HVN + customer IFS khác: sync từ Oracle ──────────────
                        if (_qrSvc.CountChuaDG() == 0 && !_qrSvc.CoDocQRNao())
                            _phieuSvc.SyncIfsPhieuChoDocQR(
                                ngay, GetNhaMay(),
                                _gioXuatHienTai.Ma,
                                _gioXuatHienTai.MoTa,
                                _addNM);
                    }

                    DataTable qrData = _qrSvc.LoadAll();
                    _uiContext.Post(_ =>
                    {
                        _view.BindDocQRCode(qrData);
                        _view.SwitchToDocQRView();
                    }, null);
                }
                catch (Exception ex)
                {
                    _isBanQR = false;
                    _view.ShowError($"Lỗi chuẩn bị dữ liệu QR: {ex.Message}");
                }
            }, "Đang chuẩn bị dữ liệu QR...");
        }

        private void OnQRCodeSubmitted(object sender, string rawQr)
        {
            Applications.Services.ScanResult result;

            if (_cfg.CoGear)
            {
                // ── YMVN ─────────────────────────────────────────────────────────
                result = _qrSvc.ProcessScanYMVN(
                    rawQr,
                    kiemTraMaTrongPhieu: ma => _phieuSvc.KiemTraMaTrongPhieu(ma),
                    kiemTraSlDaBan: (ma, sl) => _qrSvc.KiemTraSlDaBan(ma, sl));
            }
            else
            {
                // ── HVN / HTN ────────────────────────────────────────────────────
                result = _qrSvc.ProcessScan(
                    rawQr,
                    kiemTraMaTrongPhieu: ma => _phieuSvc.KiemTraMaTrongPhieu(ma),
                    kiemTraSlDaBan: (ma, sl) => _qrSvc.KiemTraSlDaBan(ma, sl));
            }

            if (result.Success) return;

            if (result.SlKhacBiet)
            {
                RunWithLoadingSync(() =>
                {
                    bool confirm = _view.Confirm(
                        "Số lượng TEM không khớp với phiếu giao!\n" +
                        "Bạn có muốn nhập với số lượng này không?");

                    if (!confirm) return;

                    // ← _qrSvc.ConfirmSlKhacBiet tự biết dùng bảng nào qua _cfg
                    var confirmed = _qrSvc.ConfirmSlKhacBiet(result.Item);
                    if (!confirmed.Success)
                        _view.ShowError(confirmed.Message);

                }, "Đang xác nhận...");
                return;
            }

            _view.ShowError(result.Message);
        }


        private void OnHoanThanh(object sender, EventArgs e)
    => RunWithLoadingSync(() =>
    {
        int slChuaDG = _qrSvc.CountChuaDG();

        if (slChuaDG > 0)
        {
            DataTable bangTam = _phieuSvc.GetDonHangChuaLot(isSP: _qrSvc.IsBanSP);
            if (bangTam != null && bangTam.Rows.Count > 0)
            {
                // Do chạy đồng bộ trên UI Thread thông qua RunWithLoadingSync, 
                // các callback hiển thị Dialog và cập nhật Grid dưới đây hoạt động an toàn 100%.
                _phieuSvc.TinhTongLot(
                    bangTam,
                    chonSttKhiTrung: lv => _view.ShowChonSttTrungMa(lv),
                    capNhapGrid: (stt, lot) => _view.RefreshLotRow(stt, lot),
                    isSP: _qrSvc.IsBanSP);
            }

            int conChuaDG = _qrSvc.CountChuaDG();
            if (conChuaDG == 0)
            {
                _isBanQR = false;
                _view.UnlockAllRadio();
            }

            _view.SwitchToPhieuView();
            DataTable dtHienTai = _view.GetDonHangTable();
            DataTable dtMoiNhat = _phieuSvc.GetDonHangHienTai(_tenBan);
            bool showLayLai = _phieuSvc.CheckCoLotChuaCNK(dtMoiNhat);

            SetupPhieuButtonsDefault(
                showCapNhapKho: true,
                showLayLaiLot: showLayLai);
        }
        else
        {
            _isBanQR = false;
            _qrSvc.SetCheDoBan("");
            _view.UnlockAllRadio();

            if (_gioXuatHienTai.Ma == "#")
            {
                DataTable dt = _phieuSvc.LoadTmpPhieuGiaoDB();
                _view.BindDonHang(dt);
                _view.SwitchToPhieuDBView();
                bool showLayLai = _phieuSvc.CheckCoLotChuaCNK(dt);
                SetupPhieuButtonsDefault(showCapNhapKho: true, showLayLaiLot: showLayLai);
            }
            else
            {
                _view.SwitchToPhieuView();
                // Hàm LoadPhieuHienTai đã được refactor an toàn ở bước trước
                LoadPhieuHienTai();
            }
        }
    }, "Đang tổng hợp dữ liệu hoàn thành...");
        /// <summary>
        /// YMVN
        /// </summary>
        // Hoàn thành YMVN — gọi SP Take_LotYMVN thay vì TinhTongLot
        private void OnGioXuatCheckedChanged(object sender, EventArgs e)
        {
            var checkedList = _view.GetCheckedGioXuat();
            if (checkedList.Count == 0) return;

            UpdateGioXuatFromCheckList(checkedList);
            LoadPhieuHienTai();
        }
        private void OnHoanThanhYMVN(object sender, EventArgs e)
         => RunWithLoading(() =>
         {
             // Truyền isLoaiSP từ trạng thái hiện tại
             bool isLoaiSP = _view.IsLoaiSP;
             _phieuSvc.HoanThanhYMVN(isLoaiSP);

             _uiContext.Post(_ =>
             {
                 //_isBanQR = false;
                 _qrSvc.SetCheDoBanSP(false);
                 _view.UnlockAllRadio();
                 _view.SwitchToPhieuView();
                 LoadPhieuHienTai();
             }, null);
         }, "Đang xử lý hoàn thành...");

        // Upload Milkrun SP — tương đương UploadMIKR()
        //private void OnUploadMilkrunSP(object sender, EventArgs e)
        //{
        //    // Bước 1: Mở FRM_UploadMikrun — user upload file Excel vào DB
        //    using (var frm = new FRM_UploadMikrun(new SQLPROVIDER()))
        //    {
        //        frm.ShowDialog();
        //        // ShowDialog() block tại đây cho đến khi user đóng form
        //    }

        //    // Bước 2: Sau khi đóng form, reload lại phiếu để hiển thị dữ liệu mới
        //    _view.ShowLoading(true);
        //    try
        //    {
        //        LoadPhieuHienTai();   // ← reload grid cho tươi
        //    }
        //    catch (Exception ex)
        //    {
        //        _view.ShowError("Lỗi reload sau upload: " + ex.Message);
        //    }
        //    finally
        //    {
        //        _view.ShowLoading(false);
        //    }
        //}

        private void OnUploadMilkrunSP(object sender, EventArgs e)
        {
            if (_cfg.CoGear)            // YMVN 100002 — Upload Milkrun SP
            {
                using (var frm = new FRM_UploadMikrun(new SQLPROVIDER(),_cfg))
                    frm.ShowDialog();
            }
            else if (_cfg.LoadTheoNgay) // HTN 100003 — Upload PO HTN
            {
                using (var frm = new FRM_UploadMikrun(
                    new SQLPROVIDER(),_cfg,
                    targetTable: "Purchase_Order_HTN",
                    title: "Upload PO HTN"))
                    frm.ShowDialog();
            }
            else return; // Customer khác không có upload

            // Reload sau khi đóng form — dùng chung
            _view.ShowLoading(true);
            try
            {
                LoadPhieuHienTai();
            }
            catch (Exception ex)
            {
                _view.ShowError("Lỗi reload sau upload: " + ex.Message);
            }
            finally
            {
                _view.ShowLoading(false);
            }
        }
        // ── 2. XetTrangThai ──────────────────────────────────────────────────────
        private void XetTrangThai()
            => RunWithLoadingSync(() =>
                 {
                     // ── Máy không có quyền bắn QR → load thẳng ──────────────────────
                     if (!_isMayBanQR)
                     {
                         _isBanQR = false;
                         _view.UnlockAllRadio();
                         _view.UnlockDatePicker();
                         LoadPhieuHienTai();
                         return;
                     }

                     // ── 1. Check trạng thái theo đúng bảng của _cfg ──────────────────
                     // GetTrangThaiDangBan() đã tự dùng _cfg.TmpTable/_cfg.DocQRTable
                     var tt = _phieuSvc.GetTrangThaiDangBan();

                     if (!tt.DangBan && _cfg.CoConfigSP)
                     {
                         var ttSP = _phieuSvc.GetTrangThaiDangBanSP();
                         if (ttSP.DangBan)
                         {
                             tt = ttSP;
                             _phieuSvc.SetTrangThaiBan(true, true);
                             _qrSvc.SetCheDoBanSP(true);
                         }
                     }

                     if (!tt.DangBan)
                     {
                         _isBanQR = false;
                         _qrSvc.SetCheDoBanSP(false);
                         _view.UnlockAllRadio();
                         _view.UnlockDatePicker();
                         LoadPhieuHienTai();
                         return;
                     }

                     // ── 2. DataKhongKhop ─────────────────────────────────────────────
                     if (tt.DataKhongKhop)
                     {
                         bool xoa = _view.HoiXoaDocQR();
                         if (xoa) _phieuSvc.XoaDocQRCode();
                         _isBanQR = false;
                         _qrSvc.SetCheDoBanSP(false);
                         _view.UnlockAllRadio();
                         _view.UnlockDatePicker();
                         LoadPhieuHienTai();
                         return;
                     }

                     // ── 3. Đang bắn dở → lock ngày ───────────────────────────────────
                     if (DateTime.TryParse(tt.NgayGiao, out DateTime ngay))
                         _view.SetDate(ngay);

                     _addNM = _cfg.CoNhieuNhaMay ? tt.AddNM : _cfg.AddNmMacDinh;
                     if (_cfg.CoNhieuNhaMay)
                         _view.SetTab(tt.AddNM);

                     _isBanQR = true;

                     // Lock ngày — áp dụng mọi customer khi đang bắn dở
                     _view.LockDatePicker();

                     // ── YMVN (CoGear): parse giờ từ GIOGIAOFCC ───────────────────────
                     if (_cfg.CoGear)
                     {
                         var checkedGios = ParseGioYMVN(tt.GioGiaoFCC);
                         bool isSP = PhieuService.IsLoaiSP(tt.GioGiaoFCC);
                         _qrSvc.SetCheDoBanSP(isSP);

                         _view.SuspendGioXuatChanged();
                         try
                         {
                             _view.SetCheckedGiosYMVN(checkedGios);
                             _view.LockCheckListYMVN(); // ← thay LockRadioYMVN
                         }
                         finally { _view.ResumeGioXuatChanged(); }

                         _phieuSvc.LoadPhieuTuBangRieng_Internal(
                             tt.NgayGiao, checkedGios, isSP, _isMayBanQR, true);
                         return;
                     }

                     // ── HVN + 100003 + YMVN không CoGear: khôi phục giờ ─────────────
                     string gioDonTuDB = tt.GioGiaoFCC;
                     string maKhung = "";
                     string moTaKhung = "";

                     var danhSachGio = _addNM == 1
                         ? _gioXuatRepo.GetDanhSachGioVP()
                         : _gioXuatRepo.GetDanhSachGioHN();

                     foreach (var gio in danhSachGio)
                     {
                         string maBam = GioXuatRepository.ParseGioThuong(gio.MoTa);
                         if (maBam.Contains($"'{gioDonTuDB}'"))
                         {
                             maKhung = gio.Ma;
                             moTaKhung = gio.MoTa;
                             break;
                         }
                     }

                     if (string.IsNullOrEmpty(maKhung))
                     {
                         maKhung = $"'{gioDonTuDB}'";
                         moTaKhung = gioDonTuDB + "H";
                     }

                     bool isSPHvn = PhieuService.IsLoaiSP(moTaKhung);
                     _qrSvc.SetCheDoBanSP(isSPHvn);

                     _view.SuspendGioXuatChanged();
                     try
                     {
                         _gioXuatHienTai = new GioXuat(maKhung, moTaKhung);
                         _view.UpdateGioXuatFromDB(maKhung);
                         _view.LockRadioExcept(maKhung);
                     }
                     finally { _view.ResumeGioXuatChanged(); }

                     // 100003: LoadPhieuHienTai sẽ tự vào nhánh LoadTuBangRieng
                     // với isBanQR=true → load từ TMP_100003 hiện tại
                     LoadPhieuHienTai();

                 }, "Đang kiểm tra trạng thái phiên làm việc cũ...");
        //    private void XetTrangThai()
        //=> RunWithLoadingSync(() =>
        //{
        //    // ── Máy không có quyền bắn QR → load thẳng ──────────────────────
        //    if (!_isMayBanQR)
        //    {
        //        _isBanQR = false;
        //        _qrSvc.SetCheDoBan("");
        //        _view.UnlockAllRadio();

        //        LoadPhieuHienTai();
        //        return;
        //    }

        //    // ── YMVN (CoGear) → load thẳng, không check TMP ─────────────────
        //    if (_cfg.CoGear)
        //    {
        //        _isBanQR = false;
        //        _qrSvc.SetCheDoBan("");
        //        _view.UnlockAllRadio();
        //        LoadPhieuHienTai();
        //        return;
        //    }

        //    // ── FIX: HTN (LoadTuBangRieng + !CoGear) ─────────────────────────
        //    // HTN dùng GetTrangThaiDangBan giống HVN nhưng cần xử lý
        //    // khác: không có GioXuat RadioGroup, không lock radio
        //    if (_cfg.LoadTuBangRieng)
        //    {
        //        var ttHTN = _phieuSvc.GetTrangThaiDangBan();

        //        if (!ttHTN.DangBan)
        //        {
        //            _isBanQR = false;
        //            _qrSvc.SetCheDoBan("");
        //            LoadPhieuHienTai();
        //            return;
        //        }

        //        if (ttHTN.DataKhongKhop)
        //        {
        //            bool xoa = _view.HoiXoaDocQR();
        //            if (xoa) _phieuSvc.XoaDocQRCode();
        //            _isBanQR = false;
        //            _qrSvc.SetCheDoBan("");
        //            LoadPhieuHienTai();
        //            return;
        //        }

        //        // ── Đang bắn dở → khôi phục ngày, set _isBanQR = true ───────
        //        if (DateTime.TryParse(ttHTN.NgayGiao, out DateTime ngayHTN))
        //            _view.SetDate(ngayHTN);

        //        _isBanQR = true;
        //        // HTN không có RadioGroup giờ → không cần lock radio
        //        // HTN không có loại SP/OType → SetCheDoBan("")
        //        _qrSvc.SetCheDoBan("");
        //        _phieuSvc.SetTrangThaiBan(_isBanQR, false);

        //        LoadPhieuHienTai();
        //        return;
        //    }

        //    // ── HVN: check TMP + DocQR ────────────────────────────────────────
        //    var tt = _phieuSvc.GetTrangThaiDangBan();

        //    if (!tt.DangBan && _cfg.CoConfigSP)
        //    {
        //        var ttSP = _phieuSvc.GetTrangThaiDangBanSP();
        //        if (ttSP.DangBan) tt = ttSP;
        //    }

        //    if (!tt.DangBan)
        //    {
        //        _isBanQR = false;
        //        _qrSvc.SetCheDoBan("");
        //        _view.UnlockAllRadio();
        //        LoadPhieuHienTai();
        //        return;
        //    }

        //    if (tt.DataKhongKhop)
        //    {
        //        bool xoa = _view.HoiXoaDocQR();
        //        if (xoa) _phieuSvc.XoaDocQRCode();
        //        _isBanQR = false;
        //        _qrSvc.SetCheDoBan("");
        //        _view.UnlockAllRadio();
        //        LoadPhieuHienTai();
        //        return;
        //    }

        //    // ── HVN đang bắn dở → khôi phục ────────────────────────────────
        //    if (DateTime.TryParse(tt.NgayGiao, out DateTime ngay))
        //        _view.SetDate(ngay);

        //    _addNM = _cfg.CoNhieuNhaMay ? tt.AddNM : _cfg.AddNmMacDinh;
        //    if (_cfg.CoNhieuNhaMay)
        //        _view.SetTab(tt.AddNM);

        //    _isBanQR = true;
        //    string gioDonTuDB = tt.GioGiaoFCC;
        //    string maKhungGio = "";
        //    string moTaKhungGio = "";

        //    var danhSachGio = (_addNM == 1)
        //        ? _gioXuatRepo.GetDanhSachGioVP()
        //        : _gioXuatRepo.GetDanhSachGioHN();

        //    foreach (var gio in danhSachGio)
        //    {
        //        string maBam = GioXuatRepository.ParseGioThuong(gio.MoTa);
        //        if (maBam.Contains($"'{gioDonTuDB}'"))
        //        {
        //            maKhungGio = gio.Ma;
        //            moTaKhungGio = gio.MoTa;
        //            break;
        //        }
        //    }

        //    if (string.IsNullOrEmpty(maKhungGio))
        //    {
        //        maKhungGio = $"'{gioDonTuDB}'";
        //        moTaKhungGio = gioDonTuDB + "H";
        //    }

        //    _qrSvc.SetCheDoBan(moTaKhungGio);
        //    _phieuSvc.SetTrangThaiBan(_isBanQR, PhieuService.IsLoaiSP(moTaKhungGio));

        //    _view.SuspendGioXuatChanged();
        //    try
        //    {
        //        _gioXuatHienTai = new GioXuat(maKhungGio, moTaKhungGio);
        //        _view.UpdateGioXuatFromDB(maKhungGio);
        //        _view.LockRadioExcept(maKhungGio);
        //    }
        //    finally
        //    {
        //        _view.ResumeGioXuatChanged();
        //    }

        //    LoadPhieuHienTai();

        //}, "Đang kiểm tra trạng thái phiên làm việc cũ...");

        // ── Helper: parse GIOGIAOFCC của YMVN thành List<string> ─────────────────
        private List<string> ParseGioYMVN(string gioDonTuDB)
        {
            if (string.IsNullOrWhiteSpace(gioDonTuDB))
                return new List<string>();

            // Format có thể: "14:30", "14:30,15:00", "14:30+15:00H"
            return gioDonTuDB
                .Replace("H", "")
                .Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim())
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList();
        }

        private void OnXoaDongQR(object sender, EventArgs e)
        {
            int stt = _view.GetFocusedDocQRStt();
            _view.DeleteFocusedDocQRRow();
            if (stt > 0) _qrSvc.XoaDong(stt);
        }

        private void OnXoaToanBoQR(object sender, EventArgs e)
        {
            _qrSvc.XoaToanBo();
            _view.ClearDocQRRows();
            _isBanQR = false;
            _qrSvc.SetCheDoBan("");
            _view.UnlockAllRadio();
        }

        private void OnSuaSoLuongTem(object sender, EventArgs e)
        {
            int stt = _view.SttDangSuaSl;
            if (stt <= 0)
            {
                _view.ShowError("Không xác định được dòng cần sửa!");
                return;
            }

            int? slMoi = _view.GetSuaSoLuongResult();
            if (!slMoi.HasValue)
            {
                _view.ShowError("Chưa nhập số lượng thay đổi!");
                return;
            }

            if (slMoi.Value <= 0)
            {
                _view.ShowError("Số lượng phải lớn hơn 0!");
                return;
            }

            _qrSvc.CapNhapSlHvn(stt, slMoi.Value);
            DataTable qrData = _qrSvc.LoadAll();
            _view.BindDocQRCode(qrData);
        }

        private void OnLayLaiLotNo(object sender, LayLaiLotEventArgs e)
        {
            // BƯỚC 1: Hỏi xác nhận trên UI trước (Màn hình Loading chưa bật)
            if (!_view.Confirm($"Bạn có chắc chắn muốn reset dữ liệu LOT của dòng có STT {e.Stt} không?"))
                return;

            // BƯỚC 2: Bật wrapper để khóa UI và thực thi tác vụ sửa đổi DB + nạp lại dữ liệu
            RunWithLoadingSync(() =>
            {
                // Thực hiện xóa/reset dữ liệu LOT dưới DB
                _phieuSvc.LayLaiLotNo(e.Stt, isSP: _qrSvc.IsBanSP);

                // Tải lại dữ liệu mới nhất (Hàm này đã được refactor an toàn luồng trước đó)
                LoadPhieuHienTai();

            }, "Đang xử lý lấy lại số LOT...");
        }

        public bool OnGiaoDBChanging(int addNm) => _view.XuLyChuyenGiaoDB(addNm);

        private void OnThemDongGiaoDB(object sender, EventArgs e)
        {
            DataTable danhSachMa = _phieuSvc.ThemDongGiaoDB();
            _view.ThemDongGiaoDB(danhSachMa);
        }

        private void OnXoaDongGiaoDB(object sender, EventArgs e)
        {
            _view.XoaDongGiaoDB();
        }

        private void OnLuuGiaoDB(object sender, EventArgs e)
    => RunWithLoadingSync(() =>
    {
        // 1. Đọc dữ liệu từ UI (An toàn tuyệt đối trên luồng chính)
        DataTable dt = _view.GetDonHangTable();

        // 2. Gọi Service xử lý lưu Database tác vụ nặng
        _phieuSvc.LuuGiaoDB(dt, _gioXuatHienTai, _addNM);

        // 3. Hiển thị thông báo thành công cho người dùng
        _view.ShowInfo("Xong !!!");

        // Không cần viết try-catch-finally. Nếu DB bị lỗi (khóa bảng, mất kết nối...), 
        // wrapper sẽ tự động bắt được ngoại lệ, hiển thị ShowError và giải phóng màn hình Loading.
    }, "Đang lưu dữ liệu giao DB...");


        private bool _isLoadingPhieu = false;  // đổi tên tránh conflict với _isLoading của View

        private void LoadPhieuHienTai()
        {
            // ── Debounce: chặn gọi lại khi đang load ─────────────────────────
            if (_isLoadingPhieu) return;
            _isLoadingPhieu = true;

            // ── BƯỚC 1: Đọc UI an toàn ───────────────────────────────────────
            string ngayGiao = "";
            List<string> checkedGios = null;
            bool isLoaiSP = false;
            string nhaMay = "";
            string gioMa = _gioXuatHienTai.Ma;
            string gioMoTa = _gioXuatHienTai.MoTa;

            Action readUiAction = () =>
            {
                ngayGiao = _cfg.CoGear
                    ? _view.SelectedDate.ToString("MM/dd/yyyy")
                    : _view.SelectedDate.ToString("yyyy-MM-dd");
                if (_cfg.CoGear)
                {
                    checkedGios = _view.GetCheckedGioXuat();
                    isLoaiSP = _view.IsLoaiSP;
                }
                else
                {
                    nhaMay = GetNhaMay();
                }
            };

            if (_uiContext == SynchronizationContext.Current)
                readUiAction();
            else
                _uiContext.Send(_ => readUiAction(), null);

            // ── BƯỚC 2: Gọi Service ──────────────────────────────────────────
            try
            {
                //if (_cfg.CoGear)
                //{
                //    _phieuSvc.LoadPhieuYMVN_Internal(
                //        ngayGiao, checkedGios, isLoaiSP,
                //        _isMayBanQR, _isBanQR);
                //}
                //else
                //{
                //    _phieuSvc.LoadPhieu(
                //        ngayGiao, nhaMay, gioMa, gioMoTa,
                //        _addNM, _isMayBanQR, _isBanQR);
                //}
                if (_cfg.LoadTuBangRieng)
                {
                    _phieuSvc.LoadPhieuTuBangRieng_Internal(
                        ngayGiao,
                        _cfg.CoGear ? checkedGios : null,  // HTN: null = không filter giờ
                        isLoaiSP,
                        _isMayBanQR,
                        _isBanQR);
                }
                else  // HVN
                {
                    _phieuSvc.LoadPhieu(
                        ngayGiao, nhaMay, gioMa, gioMoTa,
                        _addNM, _isMayBanQR, _isBanQR);
                }
            }
            catch (Exception ex)
            {
                _isLoadingPhieu = false;  // ← reset nếu exception
                _uiContext.Post(_ =>
                {
                    _view.ShowLoading(false);
                    _view.ShowError($"Lỗi tải phiếu: {ex.Message}");
                }, null);
            }
        }

        private string GetNhaMay()
        {
            if (!_cfg.CoNhieuNhaMay)
                return _cfg.TenNhaMay;  // "NHA MAY 10003" cố định

            // 100001: theo tab đang chọn
            return _addNM == 1
                ? "HON DA - VIET NAM(NHA MAY VP)"
                : "HON DA - VIET NAM(NHA MAY HA NAM)";
        }

        public void UpdateGioXuat(GioXuat gio) => _gioXuatHienTai = gio;
        // Loại 1: tác vụ thuần DB — dùng Task.Run
        // =====================================================================
        // Wrapper điều phối luồng An Toàn (Fix Cross-Threading & UI Freeze)
        // =====================================================================

        /// <summary>
        /// Loại 1: Chạy tác vụ nặng dưới Background Thread (Task.Run)
        /// Tự động đưa việc Bật/Tắt Loading và Xử lý Lỗi về UI Thread một cách an toàn.
        /// </summary>
        private void RunWithLoading(Action action, string caption = "Đang xử lý...")
        {
            // 1. Luôn bật Loading trên UI Thread trước khi tạo Thread mới
            _view.ShowLoading(true, caption);

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 2. Thực thi logic nghiệp vụ (Nên là các hàm thuần DB/Services)
                    action();
                }
                catch (Exception ex)
                {
                    // 3. Nếu lỗi, Marshal (gửi) thông báo lỗi về UI Thread để hiển thị
                    _uiContext.Post(_ =>
                    {
                        _view.ShowError("Lỗi hệ thống: " + ex.Message);
                    }, null);
                }
                finally
                {
                    // 4. Bất kể thành công hay lỗi, luôn tắt Loading trên UI Thread
                    _uiContext.Post(_ =>
                    {
                        _view.ShowLoading(false);
                    }, null);
                }
            });
        }

        /// <summary>
        /// Loại 2: Chạy tác vụ đồng bộ bắt buộc trên UI Thread (Ví dụ: Có Show Dialog, Show Report)
        /// Kết hợp Application.DoEvents() để ép UI vẽ hộp thoại Loading trước khi block luồng xử lý.
        /// </summary>
        private void RunWithLoadingSync(Action action, string caption = "Đang xử lý...")
        {
            try
            {
                // 1. Bật loading trên UI Thread
                _view.ShowLoading(true, caption);

                // 2. Ép WinForms vẽ lại giao diện (Render) ngay lập tức để người dùng kịp thấy chữ "Đang xử lý..."
                System.Windows.Forms.Application.DoEvents();

                // 3. Thực thi action đồng bộ
                action();
            }
            catch (Exception ex)
            {
                _view.ShowError("Lỗi: " + ex.Message);
            }
            finally
            {
                // 4. Tắt loading
                _view.ShowLoading(false);
            }
        }
        private void UpdateGioXuatFromCheckList(List<string> danhSachGio)
        {
            // "08:30" → hour = "08" → gioFcc = "'08'"
            var hours = danhSachGio
                .Select(g => g.Split(':')[0].PadLeft(2, '0'))
                .Distinct()
                .OrderBy(h => h)
                .ToList();

            string gioFcc = string.Join(",", hours.Select(h => $"'{h}'"));
            string gioMoTa = string.Join("+", danhSachGio) + "H";

            _gioXuatHienTai = new GioXuat(gioFcc, gioMoTa);

            System.Diagnostics.Debug.WriteLine(
                $"[YMVN GioXuat] Ma={gioFcc} | MoTa={gioMoTa}");
        }
        
        // ════════════════════════════════════════════════════════════════════
        // Dispose giải phóng triệt để sự kiện (Fix Memory Leak)
        // ════════════════════════════════════════════════════════════════════
        public void Dispose()
        {
            // Unsubscribe View Events
            _view.FormLoaded -= OnFormLoaded;
            _view.DateChanged -= OnDateChanged;
            _view.GioXuatChanged -= OnGioXuatChanged;
            _view.TabChanged -= OnTabChanged;
            _view.CapNhapKhoClicked -= OnCapNhapKho;
            _view.InPhieuClicked -= OnInPhieu;
            _view.InGhepLotClicked -= OnInGhepLot;
            _view.InTachLotClicked -= OnInTachLot;
            _view.DocQRCodeClicked -= OnDocQRCode;
            _view.KiemTraGhepLotClicked -= OnKiemTraGhepLot;
            _view.KiemTraMaNGClicked -= OnKiemTraMaNG;
            _view.QRCodeSubmitted -= OnQRCodeSubmitted;
            _view.HoanThanhClicked -= OnHoanThanh;
            _view.XoaDongQRClicked -= OnXoaDongQR;
            _view.XoaToanBoQRClicked -= OnXoaToanBoQR;
            _view.SuaSoLuongTemClicked -= OnSuaSoLuongTem;
            _view.LayLaiLotNoClicked -= OnLayLaiLotNo;
            _view.ThemDongGiaoDBClicked -= OnThemDongGiaoDB;
            _view.XoaDongGiaoDBClicked -= OnXoaDongGiaoDB;
            _view.LuuGiaoDBClicked -= OnLuuGiaoDB;
            _view.CapNhapTTPHIEUClicked -= OnCapNhapTTPHIEU;
            _view.HoanThanhYMVNClicked -= OnHoanThanhYMVN;
            _view.UploadMilkrunSPClicked -= OnUploadMilkrunSP;
            _view.LoaiPhieuChanged -= OnLoaiPhieuChanged;
            _view.ChonLotThuCongClicked -= OnChonLotThuCong;
            // ✅ ĐÃ THÊM: Unsubscribe Domain Events để giải phóng bộ nhớ!
            if (_bus != null)
            {
                _bus.Unsubscribe<PhieuLoadedEvent>(OnPhieuLoaded);
                _bus.Unsubscribe<KhoUpdatedEvent>(OnKhoUpdated);
                _bus.Unsubscribe<TinhTongCompletedEvent>(OnTinhTongCompleted);
                _bus.Unsubscribe<QRScannedEvent>(OnQRScanned);
            }
        }

        private void SetupPhieuButtonsDefault(bool showCapNhapKho = false,
                                       bool showKiemTraMaNG = false,
                                       bool showLayLaiLot = false,
                                       bool showStop = false)
        {
            bool coMaNG = showKiemTraMaNG || _phieuSvc.CheckCoMaNG();

            _view.SetupPhieuButtons(
                showCapNhapKho: showCapNhapKho && _isMayBanQR,
                showKiemTraMaNG: coMaNG && _isMayBanQR,
                showGhepLot: _isMayBanQR,
                showDocQRCode: _isMayBanQR,
                // ✅ THỐNG NHẤT LOGIC: Ép thêm điều kiện thiết bị và trạng thái quét QR ở đây
                showLayLaiLot: showLayLaiLot && _isMayBanQR && !_isBanQR,
                showStop: showStop);
        }
    }
    
}
