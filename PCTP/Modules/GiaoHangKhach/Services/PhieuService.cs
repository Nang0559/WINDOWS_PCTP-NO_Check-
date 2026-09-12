using DevExpress.DataAccess.DataFederation;
using DevExpress.Office;
using DevExpress.Pdf.Native;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Toàn bộ logic nghiệp vụ liên quan đến phiếu giao hàng.
    /// KHÔNG import DevExpress, KHÔNG import System.Windows.Forms.
    /// </summary>
    public class PhieuService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IIFSRepository _ifsRepo;
        private readonly IGioXuatRepository _gioXuatRepo;
        private readonly IPhieuGiaoDBRepository _giaoDbRepo;
        private readonly IEventBus _bus;
        private readonly string _tenBan;
        private readonly bool _isMayBanQR;
        private readonly CustomerConfig _cfg;
        private DataTable _ifsDataCache;
        private string _ifsLoadWarning;

        // ✅ FIX: LoadPhieuTuBangRieng / GetDanhSachGioYMVN / UploadMilkrunSP /
        // InsertTmpYMVN nằm trong ITableOrderRepository — đã được tách riêng khỏi
        // IPhieuRepository (xem comment trong ITableOrderRepository.cs: "Tách khỏi
        // IPhieuRepository để OrderTableLoadStrategy chỉ phụ thuộc đúng những gì nó
        // cần"). PhieuRepository hiện KHÔNG còn implement các method này nữa, nên
        // PhieuService phải nhận riêng dependency này (implementation: TableOrderRepo).
        private readonly ITableOrderRepository _tableOrderRepo;
        private readonly IOrderSourceFactory _orderSourceFactory;

        private readonly IRowCategoryFilter _rowCategoryFilter;

        // ── Trạng thái hiện tại — được set từ Presenter ─────────────────────
        private bool _isBanQR = false;
        private bool _isLoaiSP = false;

        public PhieuService(IPhieuRepository phieuRepo,
                            IIFSRepository ifsRepo,
                            IEventBus bus,
                            IGioXuatRepository gioXuatRepo,
                            string tenBan,
                            CustomerConfig cfg,
                            bool isMayBanQR,
                            ITableOrderRepository tableOrderRepo,
                            IPhieuGiaoDBRepository giaoDbRepo,
                            IOrderSourceFactory orderSourceFactory,
                            IRowCategoryFilter rowCategoryFilter)
        {
            _phieuRepo = phieuRepo;
            _ifsRepo = ifsRepo;
            _bus = bus;
            _gioXuatRepo = gioXuatRepo;
            _tenBan = tenBan;
            _cfg = cfg;
            _isMayBanQR = isMayBanQR;
            _tableOrderRepo = tableOrderRepo ?? throw new ArgumentNullException(nameof(tableOrderRepo));
            _giaoDbRepo = giaoDbRepo ?? throw new ArgumentNullException(nameof(giaoDbRepo));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
            _rowCategoryFilter = rowCategoryFilter ?? throw new ArgumentNullException(nameof(rowCategoryFilter));
        }
        public void SetTrangThaiBan(bool isBanQR, bool isLoaiSP)
        {
            _isBanQR = isBanQR;
            _isLoaiSP = isLoaiSP;
        }

        // ── GetTenBan dùng _isLoaiSP đã set — không cần tham số ────────────
        private string GetTenBan() => GetTenBan(_isLoaiSP);
        private string GetTenBan(bool isSP)
        {
            if (_isMayBanQR)
                return _cfg.Delivery.GetTmpTable(isSP);
            else
                return _tenBan;
        }
        // ════════════════════════════════════════════════════════════════════════
        // Load phiếu
        // ════════════════════════════════════════════════════════════════════════
        public void LoadPhieu(string ngayGiao, string nhaMay,
       string gioFcc, string gioFccMoTa,
       int addNm, bool isMayBanQR, bool isBanQR,
       List<string> checkedGios = null,
       bool isLoaiSP = false)
        {
            // FIX: gọi 1 lần duy nhất ở đầu method
            SetTrangThaiBan(isBanQR, isLoaiSP);

            // ── YMVN / HTN: load từ bảng riêng ──────────────────────────────────
            if (_cfg.Delivery.LoadTuBangRieng)
            {
                if (!DateTime.TryParse(
                        ngayGiao.Length >= 10 ? ngayGiao.Substring(0, 10) : ngayGiao,
                        out DateTime dt) || dt.Year < 2000)
                {
                    _bus.Publish(new PhieuLoadedEvent(
                        new DataTable(), new DataTable(), ""));
                    return;
                }

                string ngayGiaoSP = dt.ToString("yyyy-MM-dd");
                // FIX: dùng _isLoaiSP thay vì isLoaiSP local
                bool isSP = _isLoaiSP;
                string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
                string ifsTable = _cfg.Delivery.GetIfsTable(isSP);
                string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);

                if (isMayBanQR && isBanQR)
                {
                    int demQR = SWLog.Measure("1. CountDocQRCode",
                        () => _phieuRepo.CountDocQRCode(docQRTable));

                    if (demQR > 0)
                    {
                        DataTable donHangTemp = null;
                        DataTable hangThieuTemp = null;

                        // ✅ FIX: "Lỗi tải phiếu: There is already an open DataReader
                        // associated with this Command which must be closed first."
                        // Parallel.Invoke chạy 2 query CÙNG LÚC trên 2 thread, nhưng cả
                        // LoadPhieuDocQR và LoadHangThieu đều dùng CHUNG 1 SqlConnection
                        // (_phieuRepo._db — PhieuSqlExecutor dùng chung theo Uow, xem
                        // HVN_PGH.BuildPresenter). SqlConnection/SqlCommand không thread-safe
                        // và connection string không bật MultipleActiveResultSets — chạy song
                        // song trên cùng connection sẽ đá nhau. Đổi lại tuần tự.
                        donHangTemp = SWLog.Measure("2. LoadPhieuDocQR",
                            () => _phieuRepo.LoadPhieuDocQR(
                                      ngayGiaoSP, nhaMay, gioFcc, addNm,
                                      tmpTable, ifsTable, docQRTable));
                         hangThieuTemp = SWLog.Measure("2P. TinhHangThieuTuDonHang",
                            () => _phieuRepo.TinhHangThieuTuDonHang(donHangTemp));

                        string captionQR = $"ĐƠN HÀNG {_cfg.DisplayName}: {dt:dd/MM/yyyy}";
                        bool coMaNG2 = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tmpTable);
                        _bus.Publish(new PhieuLoadedEvent(
                            donHangTemp, hangThieuTemp, captionQR,coMaNG2));
                        return;
                    }
                }

                LoadPhieuTuBangRieng_Internal(
                    ngayGiao,
                    checkedGios ?? new List<string>(),
                    _isLoaiSP,      // FIX: dùng _isLoaiSP
                    isMayBanQR,
                    isBanQR);
                return;
            }

            // ── HVN và các customer dùng IFS Oracle ──────────────────────────────
            // FIX: BỎ SetTrangThaiBan thứ 2 — đã gọi ở đầu method rồi

            string ngayGiaoDate = ngayGiao.Length >= 10
                ? ngayGiao.Substring(0, 10) : ngayGiao;

            if (!DateTime.TryParse(ngayGiaoDate, out DateTime dtHvn) || dtHvn.Year < 2000)
            {
                _bus.Publish(new PhieuLoadedEvent(new DataTable(), new DataTable(), ""));
                return;
            }

            try
            {
                string ngayGiaoSP = dtHvn.ToString("yyyy-MM-dd");
                string ngayXuat = dtHvn.ToString("ddMMyyyy");
                string gioFccSP = _cfg.Delivery.LoadTheoNgay ? "" : gioFcc;
                string gioMoTaSP = _cfg.Delivery.LoadTheoNgay ? "Tất cả ca" : gioFccMoTa;

                if (!_cfg.Delivery.LoadTheoNgay && isMayBanQR && isBanQR &&
                    (string.IsNullOrWhiteSpace(gioFccMoTa) || !gioFccMoTa.Contains("H")))
                {
                    var danhSachGio = (addNm == 1)
                        ? _gioXuatRepo.GetDanhSachGioVP()
                        : _gioXuatRepo.GetDanhSachGioHN();

                    var trungKhop = danhSachGio.FirstOrDefault(
                        g => g.Ma.Equals(gioFcc, StringComparison.OrdinalIgnoreCase));
                    if (trungKhop != null)
                        gioMoTaSP = trungKhop.MoTa;
                }

                // FIX: khai báo isSP từ _isLoaiSP — thay cho dòng comment cũ
                bool isSP = _isLoaiSP;
                string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
                string ifsTable = _cfg.Delivery.GetIfsTable(isSP);
                string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);

                string caption = _cfg.Delivery.LoadTheoNgay
                    ? $"ĐƠN HÀNG: {_cfg.DisplayName} - {nhaMay}"
                    : $"ĐƠN HÀNG: {_cfg.DisplayName} - {nhaMay}   GIỜ GIAO: {gioMoTaSP}";

                if (isMayBanQR)
                {
                    int demQR = SWLog.Measure("1. CountDocQRCode",
                        () => _phieuRepo.CountDocQRCode(docQRTable));

                    if (demQR > 0 && isBanQR)
                    {
                        DataTable donHangTemp = null;
                        DataTable hangThieuTemp = null;

                        // ✅ FIX: cùng bug DataReader như nhánh trên — không chạy song
                        // song 2 query trên cùng 1 SqlConnection dùng chung (_phieuRepo).
                        donHangTemp = SWLog.Measure("2. LoadPhieuDocQR",
                            () => _phieuRepo.LoadPhieuDocQR(
                                      ngayGiaoSP, nhaMay, gioFccSP, addNm,
                                      tmpTable, ifsTable, docQRTable));
                        bool coMaNG = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tmpTable);

                        _bus.Publish(new PhieuLoadedEvent(
                            donHangTemp, new DataTable(), caption,coMaNG));
                        return;
                    }

                    DataTable donHangIFS = SWLog.Measure("2. GetCustomerOrderJoin [IFS]",
                        () => _ifsRepo.GetCustomerOrderJoin(
                                  ngayXuat, gioFccSP, gioMoTaSP, nhaMay, addNm, 1,
                                  _cfg));

                    SWLog.Measure($"3. EnrichSttHop ({donHangIFS.Rows.Count})",
                        () => EnrichSttHop(donHangIFS));

                    DataTable donHang = SWLog.Measure("4. LuuVaLoad [IFS→TMP]",
                        () => _phieuRepo.LuuVaLoad(
                                  ifsTable,
                                  "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                                  donHangIFS,
                                  ngayGiaoSP, nhaMay, gioFccSP, addNm,
                                  tmpTable, docQRTable));

                    bool coMaNG3 = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tmpTable);

                    _bus.Publish(new PhieuLoadedEvent(donHang, new DataTable(), caption,coMaNG3));
                }
                else
                {
                    string ifsViewTable = _cfg.Delivery.GetIfsViewTable();
                    // FIX: dùng _isLoaiSP thay vì isSP local (nhất quán)
                    string tenBanView = GetTenBan(_isLoaiSP);

                    DataTable donHangIFS = SWLog.Measure("2. GetCustomerOrderJoin [IFS - view]",
                        () => _ifsRepo.GetCustomerOrderJoin(
                                  ngayXuat, gioFccSP, gioMoTaSP, nhaMay, addNm, 1,
                                  _cfg));

                    SWLog.Measure($"3. EnrichSttHop ({donHangIFS.Rows.Count})",
                        () => EnrichSttHop(donHangIFS));

                    DataTable donHang = SWLog.Measure("4. LuuVaLoad [IFSView→TMPView]",
                        () => _phieuRepo.LuuVaLoad(
                                  ifsViewTable,
                                  "Usp_Qrcode_LOAD_PHIEU_DOCQRView2405",
                                  donHangIFS,
                                  ngayGiaoSP, nhaMay, gioFccSP, addNm,
                                  tenBanView,
                                  docQRTable,
                                  ifsViewTable));

                    bool coMaNG4 = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tenBanView);

                    _bus.Publish(new PhieuLoadedEvent(donHang, new DataTable(), caption,coMaNG4));
                }
            }
            catch (Exception)
            {
                _bus.Publish(new PhieuLoadedEvent(new DataTable(), new DataTable(), ""));
                throw;
            }
        }

        public void LoadPhieuTuBangRieng_Internal(
            string ngayGiao,
            List<string> checkedGios,
            bool isLoaiSP,
            bool isMayBanQR,
            bool isBanQR)
        {
            if (!DateTime.TryParse(ngayGiao, out DateTime dt) || dt.Year < 2000)
            {
                _bus.Publish(new PhieuLoadedEvent(new DataTable(), new DataTable(), ""));
                return;
            }

            string ngayGiaoSP = dt.ToString("yyyy-MM-dd");

            // ════════════════════════════════════════════════════════════════
            // TÁC VỤ 1 — Đẩy dữ liệu IFS (Oracle) vào IFSPHIEUGIAOHANG...
            // LUÔN chạy, không phụ thuộc bảng riêng có giờ/dữ liệu hay không.
            // ════════════════════════════════════════════════════════════════
            try
            {
                string ifsTable = isLoaiSP ? _cfg.Delivery.IfsTableSP : _cfg.Delivery.IfsTable;
                string ngayXuatIFS = dt.ToString("ddMMyyyy");

                DataTable ifsData = _ifsRepo.GetFullCustomerOrder(ngayXuatIFS, _cfg);
                _phieuRepo.PushIfsSnapshot(ifsTable, ifsData); // ghi SQL: luôn full (SP+MP, cả ngày)

                DataTable ifsScoped = ifsData;

                if (_cfg.Delivery.CoGear)
                    ifsScoped = GioRowFilter.Filter(ifsScoped, checkedGios);

                if (_cfg.Delivery.CoLoaiSP)
                {
                    OrderCategory category = isLoaiSP ? OrderCategory.SP : OrderCategory.MP;
                    ifsScoped = _rowCategoryFilter.Filter(ifsScoped, category, _cfg);
                }

                _ifsDataCache = ifsScoped;
                _ifsLoadWarning = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LoadPhieuTuBangRieng_Internal] Lỗi đồng bộ IFS snapshot: {ex.Message}");
                _ifsDataCache = null;
                _ifsLoadWarning = "⚠ Không kết nối được IFS để so sánh lệch (dữ liệu đơn hàng chính vẫn hiển thị bình thường). " +
                         $"Chi tiết: {ex.Message}";
            }

            // ════════════════════════════════════════════════════════════════
            // TÁC VỤ 2 — Load đơn hàng thật từ bảng riêng vào TMP/gridDH.
            // ════════════════════════════════════════════════════════════════
            if (_cfg.Delivery.CoGear && (checkedGios == null || checkedGios.Count == 0))
            {
                _bus.Publish(new PhieuLoadedEvent(new DataTable(), new DataTable(), ""));
                return;
            }

            string gioFcc = "";
            string gioMoTa = "";

            if (checkedGios != null && checkedGios.Count > 0)
            {
                var hours = checkedGios
                    .Select(g => g.Split(':')[0].PadLeft(2, '0'))
                    .Distinct().OrderBy(h => h).ToList();
                gioFcc = string.Join(",", hours.Select(h => $"'{h}'"));
                gioMoTa = string.Join("+", checkedGios) + "H";
            }

            // ✅ FIX: luôn truyền giá trị thật của DockCodeSP — để
            // TableOrderRepo.LoadPhieuTuBangRieng tự quyết định = / <> theo isLoaiSP.
            // Trước đây truyền "" khi isLoaiSP=false khiến "Xem MP" lọc sai
            // (AND RTRIM(o.CUA) <> '' không loại được CUA='VSP1').
            string dockCodeSP = _cfg.Delivery.DockCodeSP;

            DataTable donHang;

            if (isMayBanQR)
            {
                string docQRTable = isLoaiSP
                    ? (_cfg.Delivery.DocQRTableSP ?? _cfg.Delivery.DocQRTable)
                    : _cfg.Delivery.DocQRTable;

                int demQR = _phieuRepo.CountDocQRCode(docQRTable);

                if (demQR > 0 && isBanQR)
                {
                    string tmpTable = isLoaiSP
                        ? (_cfg.Delivery.TmpTableSP ?? _cfg.Delivery.TmpTable)
                        : _cfg.Delivery.TmpTable;

                    donHang = _phieuRepo.LoadTuTmpTable(tmpTable);
                }
                else
                {
                    donHang = _tableOrderRepo.LoadPhieuTuBangRieng(
                        ngayGiaoSP, gioFcc, isLoaiSP, dockCodeSP, _cfg);
                }
            }
            else
            {
                donHang = _tableOrderRepo.LoadPhieuTuBangRieng(
                    ngayGiaoSP, gioFcc, isLoaiSP, dockCodeSP, _cfg);
            }

            DataTable hangThieu = _phieuRepo.TinhHangThieuTuDonHang(donHang);

            string caption;
            if (_cfg.Delivery.CoGear)
            {
                string loai = isLoaiSP ? "SP" : "MP";
                caption = $"ĐƠN HÀNG {_cfg.DisplayName} ({loai}): " +
                          $"{dt:dd/MM/yyyy}   GIỜ: {gioMoTa}";
            }
            else
            {
                caption = $"ĐƠN HÀNG {_cfg.DisplayName}: {dt:dd/MM/yyyy}";
            }

            _bus.Publish(new PhieuLoadedEvent(donHang, hangThieu, caption, coMaNG: false, canhBao: _ifsLoadWarning));
        }

        public void SyncIfsPhieuChoDocQR(string ngayGiao, string nhaMay,
                          string gioFcc, string gioFccMoTa,
                          int addNm)
        {
            if (!DateTime.TryParse(ngayGiao, out DateTime dt) || dt.Year < 2000) return;
            bool isSP = _isLoaiSP;
            string ngayXuat = dt.ToString("ddMMyyyy");
            string ngayGiaoSP = dt.ToString("yyyy-MM-dd");

            string gioFccSP = _cfg.Delivery.LoadTheoNgay ? "" : gioFcc;
            string gioMoTaSP = _cfg.Delivery.LoadTheoNgay ? "Tất cả ca" : gioFccMoTa;

            DataTable ifs;

            // Chỉ dùng GetFullCustomerOrder khi cfg có cấu hình bảng riêng
            // (danh sách nhiều addNm cần gộp)
            bool coBangRieng = _cfg.Delivery.DanhSachAddNm != null
                                && _cfg.Delivery.DanhSachAddNm.Count > 1; // hoặc 1 cờ riêng, vd _cfg.SuDungBangRieng

            if (coBangRieng)
            {
                // Lấy toàn bộ đơn hàng theo danh sách addNm cấu hình riêng cho khách hàng này
                ifs = _ifsRepo.GetFullCustomerOrder(ngayXuat, _cfg);
            }
            else
            {
                // Luồng bình thường: 1 nhà máy, có lọc giờ
                ifs = _ifsRepo.GetCustomerOrderJoin(
                    ngayXuat, gioFccSP, gioMoTaSP, nhaMay, addNm, 1, _cfg);
            }

            EnrichSttHop(ifs);

            _phieuRepo.LuuVaLoad(
                _cfg.Delivery.GetIfsTable(isSP),
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                ifs,
                ngayGiaoSP, nhaMay, gioFccSP, addNm,
                _cfg.Delivery.GetTmpTable(isSP),
                _cfg.Delivery.GetDocQRTable(isSP));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Kiểm tra mã trong phiếu
        // ════════════════════════════════════════════════════════════════════════
        public bool KiemTraMaTrongPhieu(string maHang)
        {

            return _phieuRepo.KiemTraMaTrongPhieu(maHang, GetTenBan());
        }

        // ════════════════════════════════════════════════════════════════════════
        // Kiểm tra trạng thái
        // ════════════════════════════════════════════════════════════════════════
        public bool CheckCoLotChuaCNK(DataTable donHang)
        {
            foreach (DataRow row in donHang.Rows)
            {
                string lot = row["LOT"]?.ToString().Trim() ?? "";
                string status = row["STATUS"]?.ToString().Trim() ?? "";
                if (lot != "" && status != "OK")
                    return true;
            }
            return false;
        }

        public bool CheckCanCapNhapKho(DataTable donHang)
        {
            foreach (DataRow row in donHang.Rows)
                if (row["LOT"]?.ToString().Trim() != "")
                    return true;
            return false;
        }

        public bool CheckCoMaNG() => _phieuRepo.CheckCoMaNG(GetTenBan());

        public DataTable TinhLechIFS(DataTable donHangBangRieng, string ngayXuatIFS)
        {
            if (!_cfg.Delivery.LoadTuBangRieng) return new DataTable();
            if (_ifsDataCache == null) return new DataTable();

            return _phieuRepo.SoSanhLechIFS(donHangBangRieng, _ifsDataCache);
        }

        // ════════════════════════════════════════════════════════════════════════
        // DOCQRCODE — dùng _cfg.DocQRTable
        // ════════════════════════════════════════════════════════════════════════
        public TrangThaiBan GetTrangThaiDangBan()
        {
            if (_cfg.Delivery.CoGear)
                return _phieuRepo.GetTrangThaiDangBanYMVN(_cfg.Delivery.TmpTable, _cfg.Delivery.DocQRTable);
            return _phieuRepo.GetTrangThaiDangBan(_cfg.Delivery.TmpTable, _cfg.Delivery.DocQRTable);
        }
        // PhieuRepository — thêm method riêng

        public TrangThaiBan GetTrangThaiDangBanSP() =>
        _cfg.Delivery.CoConfigSP
        ? _phieuRepo.GetTrangThaiDangBan(_cfg.Delivery.TmpTableSP, _cfg.Delivery.DocQRTableSP)
        : new TrangThaiBan { DangBan = false };
        public bool XoaDocQRCode(bool isSP = false)
        {
            _phieuRepo.XoaDocQRCode(_cfg.Delivery.GetDocQRTable(isSP));
            return true;
        }
        public DataTable GetDonHangHienTai(string tenbang)
        {
            return _phieuRepo.GetDonHangHienTai(tenbang);
        }
        // ════════════════════════════════════════════════════════════════════════
        // Lot — dùng _cfg.DocQRTable
        // ════════════════════════════════════════════════════════════════════════
        public DataTable GetDonHangChuaLot(bool isSP = false)
        {
            return _phieuRepo.GetDonHangChuaLot(GetTenBan(isSP), _cfg.Delivery.GetDocQRTable(isSP));
        }

        public DataTable LoadGhepLot()
        {
            string tenBan = GetTenBan(_isLoaiSP);

            if (_cfg.Delivery.LoadTuBangRieng)
            {
                return _phieuRepo.LoadGhepLot(tenBan, tenBan);
            }

            string ifsTable = _isMayBanQR
                ? _cfg.Delivery.GetIfsTable(_isLoaiSP)          // hoặc _isLoaiSP ? _cfg.IfsTableSP : _cfg.IfsTable, tuỳ CustomerConfig thật
                : _cfg.Delivery.GetIfsViewTable(_isLoaiSP);

            return _phieuRepo.LoadGhepLot(tenBan, ifsTable);
        }

        public void LayLaiLotNo(int stt, bool isSP = false)
        {
            _phieuRepo.LayLaiLotNo(stt, GetTenBan(isSP), _cfg.Delivery.GetDocQRTable(isSP));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Giao DB
        // ════════════════════════════════════════════════════════════════════════
        public DataTable GetDanhSachMaHangGiaoDB() => _phieuRepo.GetDanhSachMaHang();


        public int TaoPhieuVaChiTietGiaoDB(
        string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
        string note, DataTable chiTiet) =>
            _phieuRepo.TaoPhieuVaChiTietGiaoDB(
         ten,  ngayLap,nhaMay,  nhaMayName,
         note,  chiTiet);
        public void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm)
         => _giaoDbRepo.LuuGiaoDB(
                donHang, gioXuat.MoTa, addNm,
                "TMPPHIEUGIAOHANGDB",
                "TMPPHIEUGIAOHANGDB_IFS");

        public DataTable LoadTmpPhieuGiaoDB(DateTime ngayGiao, int addNm)
        {
            var ctx = new OrderLoadContext
            {
                Cfg = _cfg,
                NgayGiao = ngayGiao,
                AddNm = addNm,
                Source = OrderSourceKind.GiaoDB,
                Category = _isLoaiSP ? OrderCategory.SP : OrderCategory.MP
            };

            var source = _orderSourceFactory.GetSource(ctx);
            return source.Load(ctx).Orders;
        }
        public void XuLySauUploadGiaoDB()
        {
            DataTable donHang = _phieuRepo.BuildDonHangTuUpload();
            if (donHang == null || donHang.Rows.Count == 0) return;

            // Nhóm theo từng ADDNM thật (nhà máy) có trong dữ liệu vừa upload,
            // gọi LuuGiaoDB riêng cho từng nhóm — tránh hard-code addNm=0 làm
            // lệch với addNm=1/2 mà LoadPhieuGiaoDB() dùng để lọc khi đọc lại.
            var nhomTheoNhaMay = donHang.AsEnumerable()
                .GroupBy(r => DbValueHelper.SafeInt(r["ADDNM"]));

            foreach (var nhom in nhomTheoNhaMay)
            {
                DataTable phanNhom = donHang.Clone();
                foreach (var r in nhom) phanNhom.ImportRow(r);

                _phieuRepo.LuuGiaoDB(phanNhom, "(GIAO DB)", addNm: nhom.Key,
                    tmpTable: "TMPPHIEUGIAOHANGDB",
                    ifsTable: "TMPPHIEUGIAOHANGDB_IFS");
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // TinhTongLot — truyền _cfg.DocQRTable xuống repo
        // ════════════════════════════════════════════════════════════════════════
        public List<(int Stt, string Lot)> TinhTongLot(
            DataTable bangTam,
            Func<ListView, int> chonSttKhiTrung,
            Action<int, string> capNhapGrid,
            bool isSP = false)
        {
            string tenBan = GetTenBan(isSP);              // ← THÊM
            string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);
            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);       // ← THÊM

            var results = new List<(int, string)>();

            foreach (DataRow row in bangTam.Rows)
            {
                string maHang = row["MAHANG"].ToString().Trim();
                int sl = SafeInt(row["SOLUONG"]);
                int stt = SafeInt(row["STT"]);

                if (stt <= 0 || sl <= 0) continue;

                DataTable trungDt = _phieuRepo.GetDanhSachTrungMaSl(
                    maHang, sl, tenBan, docQRTable);  // ← tenBan thay _tenBan
                int dem = trungDt.Rows.Count;

                if (dem == 0) continue;

                if (dem > 1)
                {
                    ListView lv = BuildListViewTrungMaSl(trungDt);
                    int sttChon = chonSttKhiTrung(lv);
                    if (sttChon <= 0) continue;
                    stt = sttChon;
                }

                string lot = _phieuRepo.GetLotNo(
                    maHang, stt, dem, sl,
                    docQRTable: docQRTable,  // ← dùng biến local
                    tmpTable: tmpTable);   // ← dùng biến local

                if (!string.IsNullOrWhiteSpace(lot))
                {
                    _phieuRepo.CapNhapLotTmpPhieu(stt, lot, tenBan);  // ← tenBan thay _tenBan
                    capNhapGrid(stt, lot);
                    results.Add((stt, lot));
                }
            }

            _bus.Publish(new TinhTongCompletedEvent(results));
            return results;
        }

        // ════════════════════════════════════════════════════════════════════════
        // SP / Kho
        // ════════════════════════════════════════════════════════════════════════
       // public static bool IsLoaiSP(string gioMoTa)
       // => !string.IsNullOrEmpty(gioMoTa)
       //&& (gioMoTa.Contains("SP6") || gioMoTa.Contains("SP#"));
       // public static bool IsLoaiOType(string gioMoTa)
       // => !string.IsNullOrEmpty(gioMoTa)
       //&& gioMoTa.Contains("O TYPE");
        public int LuuPhieuSP(string nhaMay, string ngayGiao,
                               string gioGiaoFcc, string loaiPhieu) =>
            _phieuRepo.LuuPhieuSP(nhaMay, ngayGiao, gioGiaoFcc, loaiPhieu);

        public void CapNhapTTPHIEU(string nhaMay, string ngayGiao,
                                    string gioGiaoFcc, int stt, string ghiChu) =>
            _phieuRepo.CapNhapTTPHIEU(nhaMay, ngayGiao, gioGiaoFcc, stt, ghiChu);

        // CapNhapKho trong PhieuService — không cần truyền table
        // SP Usp_Qrcode_Update_Stock dùng LUUPHIEUGIAOHANG (bảng hệ thống)
        // → không cần tmpTable/docQRTable
        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa = "")
        {
            int soLot;
            DataTable errors;
            try
            {
                bool isSP = _isLoaiSP;

                // ── HTN: LoadTuBangRieng → dùng SP riêng không cần gioGiaoFcc ───
                if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
                {
                    soLot = _phieuRepo.CapNhapKhoHTN(
                        nhaMay,
                        _cfg.Delivery.GetTmpTable(isSP),
                        _cfg.Delivery.GetDocQRTable(isSP),
                        out errors);
                }
                else
                {
                    // ── HVN / YMVN ────────────────────────────────────────────────
                    soLot = _phieuRepo.CapNhapKho(
                        gioGiaoFcc, nhaMay,
                        _cfg.Delivery.GetTmpTable(isSP),
                        _cfg.Delivery.GetDocQRTable(isSP),
                        out errors);
                }

                if (errors?.Rows.Count > 0)
                    foreach (DataRow r in errors.Rows)
                        System.Diagnostics.Debug.WriteLine(
                            $"[CapNhapKho ERROR] MH={r["MH"]}, LOT={r["LOT"]}, STATUS={r["STATUS"]}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapNhapKho EXCEPTION] {ex.Message}");
                throw;
            }
            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }

        // CNK YMVN — loop từng dòng LOT, tự trừ kho (không dùng SP chung)
        public void CapNhapKhoYMVN(string ngayGiao, string gioXuat,
                              string nhaMay, DataTable donHang)
        {
            var errors = new List<DS_ERR_CNK>();
            var soLot = 0;

            // Tính GIOGIAO từ gioXuat string "'06','07'"
            string giogiao = string.Join("+",
                gioXuat.Split(',')
                       .Select(g => g.Trim().Trim('\'')
                                     .PadLeft(2, '0')));

            foreach (DataRow row in donHang.Rows)
            {
                string lot = row["LOT"]?.ToString().Trim() ?? "";
                string status = row["STATUS"]?.ToString().Trim() ?? "";
                int stt = SafeInt(row["STT"]);
                string maHang = row["MAHANG"]?.ToString().Trim() ?? "";

                if (lot == "" || status == "OK") continue;

                bool ok = _phieuRepo.CapNhapKhoYMVN(
                    stt, lot, maHang,
                    ngayGiao, giogiao, nhaMay,
                    out DS_ERR_CNK err);

                if (ok) soLot++;
                else if (err != null) errors.Add(err);
            }

            DataTable errDt = ToDataTable(errors);
            _bus.Publish(new KhoUpdatedEvent(soLot, errDt));
        }
        public DataTable ThemDongGiaoDB() => _phieuRepo.GetDanhSachMaHang();

        // ════════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════════
        private void EnrichSttHop(DataTable donHangIFS)
        {// Batch 1 query lấy QcDongGoi cho tất cả mã — không query từng mã
            var maHangList = donHangIFS.AsEnumerable()
                .Select(r => r["MAHANG"].ToString().Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct()
                .ToList();

            Dictionary<string, int> qcDict =
                _phieuRepo.GetQcDongGoiBatch(maHangList);

            for (int i = 0; i < donHangIFS.Rows.Count; i++)
            {
                DataRow row = donHangIFS.Rows[i];
                row["STT"] = (i + 1).ToString();

                string maHang = row["MAHANG"].ToString().Trim();
                int slGiao = Convert.ToInt32(row["SOLUONG"]);

                if (qcDict.TryGetValue(maHang, out int qcDg) && qcDg > 0)
                {
                    int hop = slGiao / qcDg;
                    if (slGiao % qcDg > 0) hop++;
                    row["HOP"] = hop.ToString();
                }
            }
        }


        public static int TinhSoHop(int soLuong, int qcDongGoi)
        {
            if (qcDongGoi <= 0) return 0;
            int hop = soLuong / qcDongGoi;
            if (soLuong % qcDongGoi > 0) hop++;
            return hop;
        }

        private static int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            try { return Convert.ToInt32(val); }
            catch { return 0; }
        }

        private static ListView BuildListViewTrungMaSl(DataTable dt)
        {
            var lv = new ListView();
            foreach (DataRow row in dt.Rows)
                lv.Items.Add(new ListViewItem(new[]
                {
                row["STT"].ToString(),     row["GIOGIAO"].ToString(),
                row["MAHANG"].ToString(),  row["TENHANG"].ToString(),
                row["SOLUONG"].ToString(), row["STATUS"].ToString()
            }));
            return lv;
        }


        ///////////////
        // Hoàn thành YMVN — gọi SP Usp_Qrcode_Take_LotYMVN
        ///////////////
        // Hoàn thành YMVN — gọi SP Usp_Qrcode_Take_LotYMVN
        public void HoanThanhYMVN(bool isLoaiSP = false)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HoanThanhYMVN] TmpTable={_cfg.Delivery.TmpTable}, DocQRTable={_cfg.Delivery.DocQRTable}, isLoaiSP={isLoaiSP}");

            DataTable result = _phieuRepo.TakeLotYMVN(
                _cfg.Delivery.TmpTable,
                _cfg.Delivery.DocQRTable,
                isLoaiSP);

            if (result == null || result.Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[HoanThanhYMVN] Không có dữ liệu trả về!");
                _bus.Publish(new HoanThanhYMVNCompletedEvent(new DataTable()));
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[HoanThanhYMVN] Số dòng trả về: {result.Rows.Count}");
            foreach (DataRow row in result.Rows)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"  STT={row["STT"]}, MAHANG={row["MAHANG"]}, LOT={row["LOT"]}, " +
                    $"SOLUONG={row["SOLUONG"]}, STATUS={row["STATUS"]}, " +
                    $"TONG_SLHVN={row["TONG_SLHVN"]}, SL_GIAO={row["SL_GIAO"]}, IsOK={row["IsOK"]}");
            }

            // ── Đẩy kết quả ra ngoài qua EventBus — Presenter subscribe và bind lên View ──
            _bus.Publish(new HoanThanhYMVNCompletedEvent(result));
        }

        // Lấy danh sách giờ từ Purchase_Order_YMVN
        public List<string> GetDanhSachGioYMVN(string ngayXuatMDY)
    => _tableOrderRepo.GetDanhSachGioYMVN(ngayXuatMDY).ToList();


        // Upload Milkrun SP — tương đương UploadMIKR()
        public void UploadMilkrunSP(DataTable donHang, string ngayGiao)
        {
            _tableOrderRepo.UploadMilkrunSP(donHang, ngayGiao);
        }


        // PhieuService — thêm SyncPhieuYMVNChoDocQR (tương đương loadG_SQL)
        // ── Gộp SyncPhieuYMVNChoDocQR + SyncPhieuTuBangRiengChoDocQR ────────────
        public void SyncPhieuTuBangRiengChoDocQR(
            DataTable donHang,
            string ngayGiao,
            List<string> checkedGios = null)  // null = HTN (không filter giờ)
        {
            if (donHang == null || donHang.Rows.Count == 0) return;

            _phieuRepo.XoaTmpPhieu(_cfg.Delivery.TmpTable);

            foreach (DataRow row in donHang.Rows)
            {
                string status = row["STATUS"]?.ToString() ?? "";
                if (status == "OK") continue;

                // ── Lấy giờ từ NGAYGIAO ─────────────────────────────────────────
                string gio = "";
                if (row.Table.Columns.Contains("NGAYGIAO") &&
                    row["NGAYGIAO"] != DBNull.Value &&
                    DateTime.TryParse(row["NGAYGIAO"].ToString(), out DateTime dt))
                    gio = dt.ToString("HH:mm");

                // ── Filter theo checkedGios — chỉ YMVN mới có ───────────────────
                if (checkedGios != null && checkedGios.Any())
                {
                    bool match = checkedGios.Any(g =>
                        gio.StartsWith(g.Length >= 2 ? g.Substring(0, 2) : g));
                    if (!match) continue;
                }

                // ── Build ngayGiao đầy đủ ────────────────────────────────────────
                string nxh = row.Table.Columns.Contains("NGAYGIAO") &&
                             row["NGAYGIAO"] != DBNull.Value &&
                             DateTime.TryParse(row["NGAYGIAO"].ToString(), out DateTime ngay)
                    ? ngay.ToString("yyyy-MM-dd HH:mm:ss")
                    : ngayGiao + " 00:00:00";

                // ── Các cột tùy chọn ─────────────────────────────────────────────
                string Get(string col) => row.Table.Columns.Contains(col)
                    ? row[col]?.ToString() ?? "" : "";

                string gear = Get("GEAR");
                string poNo = Get("PO_NO");
                string orderNo = Get("ORDER_NO");
                string gioXuat = Get("GIO");
                if (string.IsNullOrEmpty(gioXuat)) gioXuat = gio;

                _tableOrderRepo.InsertTmpYMVN(
                    stt: Get("STT"),
                    cua: Get("CUA"),
                    truyen: Get("TRUYEN"),
                    maHang: Get("MAHANG"),
                    tenHang: Get("TENHANG"),
                    lot: Get("LOT"),
                    dv: !string.IsNullOrEmpty(Get("DV")) ? Get("DV") : "PCS",
                    slXuat: SafeIntStatic(row.Table.Columns.Contains("SOLUONG")
                                  ? row["SOLUONG"] : DBNull.Value),
                    ngayGiao: nxh,
                    gear: gear,
                    gioXuat: gioXuat,
                    tmpTable: _cfg.Delivery.TmpTable,
                    poNo: poNo,
                    cusPoNo: orderNo);
            }
        }
        public static int SafeIntStatic(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            return int.TryParse(val.ToString(), out int v) ? v : 0;
        }
        private static DataTable ToDataTable(List<DS_ERR_CNK> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("MH"); dt.Columns.Add("LOT");
            dt.Columns.Add("SLC", typeof(int)); dt.Columns.Add("SLTK", typeof(int));
            dt.Columns.Add("SLT", typeof(int)); dt.Columns.Add("STATUS");
            foreach (var e in list)
                dt.Rows.Add(e.MH, e.LOT, e.SLC, e.SLTK, e.SLT, e.Ms);
            return dt;
        }

        public DataTable GetDanhSachLotTuKho(string maHang)
        => _phieuRepo.GetDanhSachLotTuKho(maHang);
        public void NhapLotThuCong(int stt, string lotNo, string tenbang)
        {
            // Ghi LOT vào TMP — giống CapNhapLotTmpPhieu
            _phieuRepo.CapNhapLotTmpPhieu(stt, lotNo, GetTenBan());
        }
    }

}