using DevExpress.DataAccess.DataFederation;
using DevExpress.Office;
using DevExpress.Pdf.Native;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.WorkingState;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Facade nghiệp vụ cho phiếu giao hàng.
    ///
    /// Phase 5/6: LoadPhieu đã được tách sang PhieuLoadService + OrderLoadResult.
    /// Phase 7: các business flow Kho / GiaoDB / YMVN được chuyển sang service riêng;
    /// PhieuService chỉ giữ API tương thích với UI/Presenter hiện tại.
    /// </summary>
    public class PhieuService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IIFSRepository _ifsRepo;
        private readonly IGioXuatRepository _gioXuatRepo;
        private readonly IEventBus _bus;
        private readonly string _tenBan;
        private readonly bool _isMayBanQR;
        private readonly CustomerConfig _cfg;
        private readonly ITableOrderRepository _tableOrderRepo;
        private readonly IOrderSourceFactory _orderSourceFactory;
        private readonly IRowCategoryFilter _rowCategoryFilter;
        private readonly IDeliveryWorkingState _workingState;
        private readonly IPhieuLoadService _loadService;

        private readonly PhieuKhoService _khoService;
        private readonly PhieuGiaoDbService _giaoDbService;
        private readonly PhieuYmvnService _ymvnService;

        private bool _isBanQR;
        private bool _isLoaiSP;

        // Legacy cache: TinhLechIFS vẫn đọc snapshot đã lọc sau lần load gần nhất.
        private DataTable _ifsDataCache;
        private string _ifsLoadWarning;

        public PhieuService(
            IPhieuRepository phieuRepo,
            IIFSRepository ifsRepo,
            IEventBus bus,
            IGioXuatRepository gioXuatRepo,
            string tenBan,
            CustomerConfig cfg,
            bool isMayBanQR,
            ITableOrderRepository tableOrderRepo,
            IPhieuGiaoDBRepository giaoDbRepo,
            IOrderSourceFactory orderSourceFactory,
            IRowCategoryFilter rowCategoryFilter,
            IDeliveryWorkingState workingState,
            IPhieuLoadService loadService = null)
        {
            _phieuRepo = phieuRepo;
            _ifsRepo = ifsRepo;
            _bus = bus;
            _gioXuatRepo = gioXuatRepo;
            _tenBan = tenBan;
            _cfg = cfg;
            _isMayBanQR = isMayBanQR;
            _tableOrderRepo = tableOrderRepo ?? throw new ArgumentNullException(nameof(tableOrderRepo));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
            _rowCategoryFilter = rowCategoryFilter ?? throw new ArgumentNullException(nameof(rowCategoryFilter));
            _workingState = workingState ?? throw new ArgumentNullException(nameof(workingState));

            _loadService = loadService ?? new PhieuLoadService(
                _phieuRepo,
                _ifsRepo,
                _orderSourceFactory,
                _rowCategoryFilter,
                _workingState,
                _cfg,
                _tenBan);

            // Phase 7: business services được assemble tại facade trong migration.
            // Phase 11 sẽ chuyển phần composition này ra ModuleFactory.
            _khoService = new PhieuKhoService(_phieuRepo, _bus, _cfg);
            _giaoDbService = new PhieuGiaoDbService(
                _phieuRepo,
                giaoDbRepo ?? throw new ArgumentNullException(nameof(giaoDbRepo)),
                _orderSourceFactory,
                _cfg,
                () => _isLoaiSP);
            _ymvnService = new PhieuYmvnService(
                _phieuRepo,
                _tableOrderRepo,
                _bus,
                _cfg);
        }

        public void SetTrangThaiBan(bool isBanQR, bool isLoaiSP)
        {
            _isBanQR = isBanQR;
            _isLoaiSP = isLoaiSP;
        }

        private string GetTenBan() => GetTenBan(_isLoaiSP);

        private string GetTenBan(bool isSP)
        {
            if (_isMayBanQR)
                return _cfg.Delivery.GetTmpTable(isSP);

            return _tenBan;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Phase 5/6 — Load facade
        // ════════════════════════════════════════════════════════════════════════
        public void LoadPhieu(
            string ngayGiao,
            string nhaMay,
            string gioFcc,
            string gioFccMoTa,
            int addNm,
            bool isMayBanQR,
            bool isBanQR,
            List<string> checkedGios = null,
            bool isLoaiSP = false)
        {
            SetTrangThaiBan(isBanQR, isLoaiSP);

            string ngayGiaoDate = string.IsNullOrEmpty(ngayGiao)
                ? string.Empty
                : (ngayGiao.Length >= 10 ? ngayGiao.Substring(0, 10) : ngayGiao);

            if (!DateTime.TryParse(ngayGiaoDate, out DateTime dt) || dt.Year < 2000)
            {
                PublishEmptyPhieuLoaded();
                return;
            }

            try
            {
                var context = CreateOrderLoadContext(
                    dt, nhaMay, gioFcc, gioFccMoTa, addNm,
                    isMayBanQR, isBanQR, checkedGios, isLoaiSP);

                OrderLoadResult result = _loadService.Load(context)
                    ?? OrderLoadResult.Empty(context);

                _ifsDataCache = context.IfsDataDaLoc;
                _ifsLoadWarning = context.IfsLoadError;

                PublishPhieuLoaded(result);
            }
            catch (Exception)
            {
                PublishEmptyPhieuLoaded();
                throw;
            }
        }

        private void PublishPhieuLoaded(OrderLoadResult result)
        {
            if (result == null)
                result = new OrderLoadResult
                {
                    Orders = new DataTable(),
                    HangThieu = new DataTable(),
                    Caption = string.Empty
                };

            DataTable donHang = result.Orders ?? new DataTable();
            DataTable hangThieu = result.HangThieu ?? new DataTable();
            string caption = result.Caption ?? string.Empty;

            if (result.Source == OrderSourceKind.TableOrder
                && !string.IsNullOrWhiteSpace(result.Warning))
            {
                _bus.Publish(new PhieuLoadedEvent(
                    donHang, hangThieu, caption, result.HasMaNG, result.Warning));
                return;
            }

            _bus.Publish(new PhieuLoadedEvent(
                donHang, hangThieu, caption, result.HasMaNG));
        }

        private void PublishEmptyPhieuLoaded()
        {
            _bus.Publish(new PhieuLoadedEvent(
                new DataTable(), new DataTable(), ""));
        }

        public void SyncIfsPhieuChoDocQR(
            string ngayGiao,
            string nhaMay,
            string gioFcc,
            string gioFccMoTa,
            int addNm)
        {
            if (!DateTime.TryParse(ngayGiao, out DateTime dt) || dt.Year < 2000)
                return;

            bool isSP = _isLoaiSP;
            string ngayXuat = dt.ToString("ddMMyyyy");
            string gioFccSP = _cfg.Delivery.LoadTheoNgay ? "" : gioFcc;
            string gioMoTaSP = _cfg.Delivery.LoadTheoNgay ? "Tất cả ca" : gioFccMoTa;

            DataTable ifs;
            bool coBangRieng = _cfg.Delivery.DanhSachAddNm != null
                               && _cfg.Delivery.DanhSachAddNm.Count > 1;

            if (coBangRieng)
                ifs = _ifsRepo.GetFullCustomerOrder(ngayXuat, _cfg);
            else
                ifs = _ifsRepo.GetCustomerOrderJoin(
                    ngayXuat, gioFccSP, gioMoTaSP, nhaMay, addNm, 1, _cfg);

            EnrichSttHop(ifs);

            var context = CreateOrderLoadContext(
                dt, nhaMay, gioFccSP, gioMoTaSP, addNm,
                true, true, null, isSP);

            _workingState.SaveFromSource(
                context, ifs, "Usp_Qrcode_LOAD_PHIEU_DOCQR2405");
        }

        public bool KiemTraMaTrongPhieu(string maHang)
            => _phieuRepo.KiemTraMaTrongPhieu(maHang, GetTenBan());

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
            if (!_cfg.Delivery.LoadTuBangRieng || _ifsDataCache == null)
                return new DataTable();

            return _phieuRepo.SoSanhLechIFS(donHangBangRieng, _ifsDataCache);
        }

        public TrangThaiBan GetTrangThaiDangBan()
            => _workingState.GetTrangThaiDangBan(
                new OrderLoadContext { Cfg = _cfg, Category = OrderCategory.MP });

        public TrangThaiBan GetTrangThaiDangBanSP()
        {
            if (!_cfg.Delivery.CoConfigSP)
                return new TrangThaiBan { DangBan = false };

            return _workingState.GetTrangThaiDangBan(
                new OrderLoadContext { Cfg = _cfg, Category = OrderCategory.SP });
        }

        public bool XoaDocQRCode(bool isSP = false)
        {
            _phieuRepo.XoaDocQRCode(_cfg.Delivery.GetDocQRTable(isSP));
            return true;
        }

        public DataTable GetDonHangHienTai(string tenbang)
            => _phieuRepo.GetDonHangHienTai(tenbang);

        public DataTable GetDonHangChuaLot(bool isSP = false)
            => _phieuRepo.GetDonHangChuaLot(
                GetTenBan(isSP), _cfg.Delivery.GetDocQRTable(isSP));

        public DataTable LoadGhepLot()
        {
            string tenBan = GetTenBan(_isLoaiSP);

            if (_cfg.Delivery.LoadTuBangRieng)
                return _phieuRepo.LoadGhepLot(tenBan, tenBan);

            string ifsTable = _isMayBanQR
                ? _cfg.Delivery.GetIfsTable(_isLoaiSP)
                : _cfg.Delivery.GetIfsViewTable(_isLoaiSP);

            return _phieuRepo.LoadGhepLot(tenBan, ifsTable);
        }

        public void LayLaiLotNo(int stt, bool isSP = false)
        {
            _phieuRepo.LayLaiLotNo(
                stt, GetTenBan(isSP), _cfg.Delivery.GetDocQRTable(isSP));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Phase 7 — GiaoDB facade delegation
        // ════════════════════════════════════════════════════════════════════════
        public DataTable GetDanhSachMaHangGiaoDB()
            => _giaoDbService.GetDanhSachMaHang();

        public int TaoPhieuVaChiTietGiaoDB(
            string ten,
            DateTime ngayLap,
            int nhaMay,
            string nhaMayName,
            string note,
            DataTable chiTiet)
            => _giaoDbService.TaoPhieuVaChiTiet(
                ten, ngayLap, nhaMay, nhaMayName, note, chiTiet);

        public void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm)
            => _giaoDbService.LuuGiaoDB(donHang, gioXuat, addNm);

        public DataTable LoadTmpPhieuGiaoDB(DateTime ngayGiao, int addNm)
            => _giaoDbService.LoadTmpPhieuGiaoDB(ngayGiao, addNm);

        public void XuLySauUploadGiaoDB()
            => _giaoDbService.XuLySauUpload();

        // ════════════════════════════════════════════════════════════════════════
        // Legacy Lot operations — facade vẫn giữ API UI hiện tại
        // ════════════════════════════════════════════════════════════════════════
        public List<(int Stt, string Lot)> TinhTongLot(
            DataTable bangTam,
            Func<ListView, int> chonSttKhiTrung,
            Action<int, string> capNhapGrid,
            bool isSP = false)
        {
            string tenBan = GetTenBan(isSP);
            string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);
            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            var results = new List<(int, string)>();

            foreach (DataRow row in bangTam.Rows)
            {
                string maHang = row["MAHANG"].ToString().Trim();
                int sl = SafeInt(row["SOLUONG"]);
                int stt = SafeInt(row["STT"]);
                if (stt <= 0 || sl <= 0) continue;

                DataTable trungDt = _phieuRepo.GetDanhSachTrungMaSl(
                    maHang, sl, tenBan, docQRTable);
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
                    docQRTable: docQRTable,
                    tmpTable: tmpTable);

                if (!string.IsNullOrWhiteSpace(lot))
                {
                    _phieuRepo.CapNhapLotTmpPhieu(stt, lot, tenBan);
                    capNhapGrid(stt, lot);
                    results.Add((stt, lot));
                }
            }

            _bus.Publish(new TinhTongCompletedEvent(results));
            return results;
        }

        public int LuuPhieuSP(
            string nhaMay,
            string ngayGiao,
            string gioGiaoFcc,
            string loaiPhieu)
            => _phieuRepo.LuuPhieuSP(nhaMay, ngayGiao, gioGiaoFcc, loaiPhieu);

        public void CapNhapTTPHIEU(
            string nhaMay,
            string ngayGiao,
            string gioGiaoFcc,
            int stt,
            string ghiChu)
            => _phieuRepo.CapNhapTTPHIEU(
                nhaMay, ngayGiao, gioGiaoFcc, stt, ghiChu);

        // ════════════════════════════════════════════════════════════════════════
        // Phase 7 — Kho facade delegation
        // ════════════════════════════════════════════════════════════════════════
        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa = "")
            => _khoService.CapNhapKho(gioGiaoFcc, nhaMay, gioMa, _isLoaiSP);

        // ════════════════════════════════════════════════════════════════════════
        // Phase 7 — YMVN facade delegation
        // ════════════════════════════════════════════════════════════════════════
        public void CapNhapKhoYMVN(
            string ngayGiao,
            string gioXuat,
            string nhaMay,
            DataTable donHang)
            => _ymvnService.CapNhapKho(ngayGiao, gioXuat, nhaMay, donHang);

        public void HoanThanhYMVN(bool isLoaiSP = false)
            => _ymvnService.HoanThanh(isLoaiSP);

        public List<string> GetDanhSachGioYMVN(string ngayXuatMDY)
            => _ymvnService.GetDanhSachGio(ngayXuatMDY);

        public void UploadMilkrunSP(DataTable donHang, string ngayGiao)
            => _ymvnService.UploadMilkrunSP(donHang, ngayGiao);

        public void SyncPhieuTuBangRiengChoDocQR(
            DataTable donHang,
            string ngayGiao,
            List<string> checkedGios = null)
            => _ymvnService.SyncPhieuTuBangRiengChoDocQR(
                donHang, ngayGiao, checkedGios);

        // Chỉ còn phục vụ SyncIfsPhieuChoDocQR.
        private void EnrichSttHop(DataTable donHangIFS)
        {
            if (donHangIFS == null || donHangIFS.Rows.Count == 0)
                return;

            var maHangList = donHangIFS.AsEnumerable()
                .Select(r => r["MAHANG"].ToString().Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct()
                .ToList();

            Dictionary<string, int> qcDict = _phieuRepo.GetQcDongGoiBatch(maHangList);

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
            {
                lv.Items.Add(new ListViewItem(new[]
                {
                    row["STT"].ToString(), row["GIOGIAO"].ToString(),
                    row["MAHANG"].ToString(), row["TENHANG"].ToString(),
                    row["SOLUONG"].ToString(), row["STATUS"].ToString()
                }));
            }
            return lv;
        }

        public DataTable GetDanhSachLotTuKho(string maHang)
            => _phieuRepo.GetDanhSachLotTuKho(maHang);

        public void NhapLotThuCong(int stt, string lotNo, string tenbang)
            => _phieuRepo.CapNhapLotTmpPhieu(stt, lotNo, GetTenBan());

        private OrderLoadContext CreateOrderLoadContext(
            DateTime ngayGiao,
            string nhaMay,
            string gioFcc,
            string gioFccMoTa,
            int addNm,
            bool isMayBanQR,
            bool isBanQR,
            List<string> checkedGios,
            bool isLoaiSP)
        {
            return new OrderLoadContext
            {
                Cfg = _cfg,
                NgayGiao = ngayGiao,
                NhaMay = nhaMay,
                AddNm = addNm,
                GioFcc = gioFcc,
                GioFccMoTa = gioFccMoTa,
                Category = isLoaiSP ? OrderCategory.SP : OrderCategory.MP,
                Source = _cfg.Delivery.LoadTuBangRieng
                    ? OrderSourceKind.TableOrder
                    : OrderSourceKind.IFS,
                MachineRole = isMayBanQR
                    ? MachineRole.DuocBanQR
                    : MachineRole.ChiXem,
                IsBanQR = isBanQR,
                CheckedGios = checkedGios ?? new List<string>(),
                IfsDataDaLoc = null,
                IfsLoadError = null
            };
        }
    }
}
