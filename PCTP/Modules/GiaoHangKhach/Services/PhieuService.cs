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
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Facade nghiệp vụ cho phiếu giao hàng.
    ///
    /// Phase 5/6:
    /// - PhieuService chỉ giữ facade, EventBus và các nghiệp vụ legacy khác.
    /// - PhieuLoadService chịu trách nhiệm orchestration của pipeline load đơn hàng.
    /// - OrderLoadResult là contract kết quả chuẩn giữa load pipeline và facade.
    /// </summary>
    public class PhieuService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IIFSRepository _ifsRepo;
        private readonly IGioXuatRepository _gioXuatRepo;
        private readonly IPhieuGiaoDBRepository _giaoDbRepo;
        private readonly IDeliveryWorkingState _workingState;
        private readonly IEventBus _bus;
        private readonly string _tenBan;
        private readonly bool _isMayBanQR;
        private readonly CustomerConfig _cfg;
        private readonly ITableOrderRepository _tableOrderRepo;
        private readonly IOrderSourceFactory _orderSourceFactory;
        private readonly IRowCategoryFilter _rowCategoryFilter;
        private readonly IPhieuLoadService _loadService;

        // ── Trạng thái hiện tại — được set từ Presenter ─────────────────────
        private bool _isBanQR = false;
        private bool _isLoaiSP = false;

        // Legacy cache: TinhLechIFS vẫn đọc snapshot đã lọc sau lần load gần nhất.
        private DataTable _ifsDataCache;
        private string _ifsLoadWarning;

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
            _giaoDbRepo = giaoDbRepo ?? throw new ArgumentNullException(nameof(giaoDbRepo));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
            _rowCategoryFilter = rowCategoryFilter ?? throw new ArgumentNullException(nameof(rowCategoryFilter));
            _workingState = workingState ?? throw new ArgumentNullException(nameof(workingState));

            // Transitional composition-root fallback:
            // giữ tương thích với các call-site hiện tại trong khi Presenter/DI
            // chưa truyền IPhieuLoadService trực tiếp.
            _loadService = loadService ?? new PhieuLoadService(
                _phieuRepo,
                _ifsRepo,
                _orderSourceFactory,
                _rowCategoryFilter,
                _workingState,
                _cfg,
                _tenBan);
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

            if (!DateTime.TryParse(ngayGiaoDate, out DateTime dt)
                || dt.Year < 2000)
            {
                PublishEmptyPhieuLoaded();
                return;
            }

            try
            {
                var context = CreateOrderLoadContext(
                    dt,
                    nhaMay,
                    gioFcc,
                    gioFccMoTa,
                    addNm,
                    isMayBanQR,
                    isBanQR,
                    checkedGios,
                    isLoaiSP);

                // Phase 5: toàn bộ source/orchestration load đi qua một service.
                // Phase 6: facade chỉ nhận OrderLoadResult và chuyển sang EventBus.
                OrderLoadResult result = _loadService.Load(context)
                    ?? OrderLoadResult.Empty(context);

                // Giữ tương thích với TinhLechIFS() của UI/legacy caller.
                _ifsDataCache = context.IfsDataDaLoc;
                _ifsLoadWarning = context.IfsLoadError;

                DataTable donHang = result.Orders ?? new DataTable();
                DataTable hangThieu = BuildHangThieuForEvent(result, donHang);

                PublishPhieuLoaded(result, donHang, hangThieu);
            }
            catch (Exception)
            {
                PublishEmptyPhieuLoaded();
                throw;
            }
        }

        /// <summary>
        /// PhieuLoadedEvent cũ yêu cầu DataTable hàng thiếu.
        /// OrderLoadResult hiện chỉ mang trạng thái HasDifference nên phần này
        /// chỉ tính lại hàng thiếu cho TableOrder, đúng nơi flow legacy yêu cầu.
        /// IFS/GiaoDB vẫn giữ DataTable rỗng như hành vi cũ.
        /// </summary>
        private DataTable BuildHangThieuForEvent(
            OrderLoadResult result,
            DataTable donHang)
        {
            if (result == null || result.Source != OrderSourceKind.TableOrder)
                return new DataTable();

            return _phieuRepo.TinhHangThieuTuDonHang(donHang)
                   ?? new DataTable();
        }

        private void PublishPhieuLoaded(
            OrderLoadResult result,
            DataTable donHang,
            DataTable hangThieu)
        {
            string caption = result.Caption ?? string.Empty;

            // Warning của TableOrder là warning đã được legacy flow đưa vào event.
            // Với IFS, giữ hành vi cũ: warning vẫn nằm trong OrderLoadResult/cache,
            // không tự thay đổi UI contract của EventBus.
            if (result.Source == OrderSourceKind.TableOrder
                && !string.IsNullOrWhiteSpace(result.Warning))
            {
                _bus.Publish(new PhieuLoadedEvent(
                    donHang,
                    hangThieu,
                    caption,
                    result.HasMaNG,
                    result.Warning));
                return;
            }

            _bus.Publish(new PhieuLoadedEvent(
                donHang,
                hangThieu,
                caption,
                result.HasMaNG));
        }

        private void PublishEmptyPhieuLoaded()
        {
            _bus.Publish(new PhieuLoadedEvent(
                new DataTable(),
                new DataTable(),
                ""));
        }

        public void SyncIfsPhieuChoDocQR(string ngayGiao, string nhaMay,
                          string gioFcc, string gioFccMoTa,
                          int addNm)
        {
            if (!DateTime.TryParse(ngayGiao, out DateTime dt) || dt.Year < 2000) return;
            bool isSP = _isLoaiSP;
            string ngayXuat = dt.ToString("ddMMyyyy");
            string gioFccSP = _cfg.Delivery.LoadTheoNgay ? "" : gioFcc;
            string gioMoTaSP = _cfg.Delivery.LoadTheoNgay ? "Tất cả ca" : gioFccMoTa;

            DataTable ifs;
            bool coBangRieng = _cfg.Delivery.DanhSachAddNm != null
                                && _cfg.Delivery.DanhSachAddNm.Count > 1;

            if (coBangRieng)
            {
                ifs = _ifsRepo.GetFullCustomerOrder(ngayXuat, _cfg);
            }
            else
            {
                ifs = _ifsRepo.GetCustomerOrderJoin(
                    ngayXuat, gioFccSP, gioMoTaSP, nhaMay, addNm, 1, _cfg);
            }

            EnrichSttHop(ifs);

            var context = CreateOrderLoadContext(
                dt,
                nhaMay,
                gioFccSP,
                gioMoTaSP,
                addNm,
                isMayBanQR: true,
                isBanQR: true,
                checkedGios: null,
                isLoaiSP: isSP);

            _workingState.SaveFromSource(
                context,
                ifs,
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405");
        }

        public bool KiemTraMaTrongPhieu(string maHang)
        {
            return _phieuRepo.KiemTraMaTrongPhieu(maHang, GetTenBan());
        }

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

        public TrangThaiBan GetTrangThaiDangBan()
            => _workingState.GetTrangThaiDangBan(
                new OrderLoadContext { Cfg = _cfg, Category = OrderCategory.MP });

        public TrangThaiBan GetTrangThaiDangBanSP()
        {
            if (!_cfg.Delivery.CoConfigSP)
                return new TrangThaiBan { DangBan = false };

            var spContext = new OrderLoadContext
            {
                Cfg = _cfg,
                Category = OrderCategory.SP
            };

            return _workingState.GetTrangThaiDangBan(spContext);
        }

        public bool XoaDocQRCode(bool isSP = false)
        {
            _phieuRepo.XoaDocQRCode(_cfg.Delivery.GetDocQRTable(isSP));
            return true;
        }

        public DataTable GetDonHangHienTai(string tenbang)
        {
            return _phieuRepo.GetDonHangHienTai(tenbang);
        }

        public DataTable GetDonHangChuaLot(bool isSP = false)
        {
            return _phieuRepo.GetDonHangChuaLot(
                GetTenBan(isSP),
                _cfg.Delivery.GetDocQRTable(isSP));
        }

        public DataTable LoadGhepLot()
        {
            string tenBan = GetTenBan(_isLoaiSP);

            if (_cfg.Delivery.LoadTuBangRieng)
            {
                return _phieuRepo.LoadGhepLot(tenBan, tenBan);
            }

            string ifsTable = _isMayBanQR
                ? _cfg.Delivery.GetIfsTable(_isLoaiSP)
                : _cfg.Delivery.GetIfsViewTable(_isLoaiSP);

            return _phieuRepo.LoadGhepLot(tenBan, ifsTable);
        }

        public void LayLaiLotNo(int stt, bool isSP = false)
        {
            _phieuRepo.LayLaiLotNo(
                stt,
                GetTenBan(isSP),
                _cfg.Delivery.GetDocQRTable(isSP));
        }

        public DataTable GetDanhSachMaHangGiaoDB() => _phieuRepo.GetDanhSachMaHang();

        public int TaoPhieuVaChiTietGiaoDB(
            string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
            string note, DataTable chiTiet) =>
            _phieuRepo.TaoPhieuVaChiTietGiaoDB(
                ten, ngayLap, nhaMay, nhaMayName, note, chiTiet);

        public void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm)
            => _giaoDbRepo.LuuGiaoDB(
                donHang,
                gioXuat.MoTa,
                addNm,
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

            var nhomTheoNhaMay = donHang.AsEnumerable()
                .GroupBy(r => DbValueHelper.SafeInt(r["ADDNM"]));

            foreach (var nhom in nhomTheoNhaMay)
            {
                DataTable phanNhom = donHang.Clone();
                foreach (var r in nhom) phanNhom.ImportRow(r);

                _phieuRepo.LuuGiaoDB(
                    phanNhom,
                    "(GIAO DB)",
                    addNm: nhom.Key,
                    tmpTable: "TMPPHIEUGIAOHANGDB",
                    ifsTable: "TMPPHIEUGIAOHANGDB_IFS");
            }
        }

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

        public int LuuPhieuSP(string nhaMay, string ngayGiao,
                               string gioGiaoFcc, string loaiPhieu) =>
            _phieuRepo.LuuPhieuSP(nhaMay, ngayGiao, gioGiaoFcc, loaiPhieu);

        public void CapNhapTTPHIEU(string nhaMay, string ngayGiao,
                                    string gioGiaoFcc, int stt, string ghiChu) =>
            _phieuRepo.CapNhapTTPHIEU(nhaMay, ngayGiao, gioGiaoFcc, stt, ghiChu);

        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa = "")
        {
            int soLot;
            DataTable errors;
            try
            {
                bool isSP = _isLoaiSP;

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
                    soLot = _phieuRepo.CapNhapKho(
                        gioGiaoFcc,
                        nhaMay,
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

        public void CapNhapKhoYMVN(string ngayGiao, string gioXuat,
                                   string nhaMay, DataTable donHang)
        {
            var errors = new List<DS_ERR_CNK>();
            var soLot = 0;

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
                    stt,
                    lot,
                    maHang,
                    ngayGiao,
                    giogiao,
                    nhaMay,
                    out DS_ERR_CNK err);

                if (ok) soLot++;
                else if (err != null) errors.Add(err);
            }

            DataTable errDt = ToDataTable(errors);
            _bus.Publish(new KhoUpdatedEvent(soLot, errDt));
        }

        public DataTable ThemDongGiaoDB() => _phieuRepo.GetDanhSachMaHang();

        // Chỉ còn phục vụ SyncIfsPhieuChoDocQR.
        // LoadPhieu chính không còn phụ thuộc vào enrichment orchestration này.
        private void EnrichSttHop(DataTable donHangIFS)
        {
            if (donHangIFS == null || donHangIFS.Rows.Count == 0)
                return;

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
                    if (slGiao % qcDg > 0)
                        hop++;
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
                    row["STT"].ToString(), row["GIOGIAO"].ToString(),
                    row["MAHANG"].ToString(), row["TENHANG"].ToString(),
                    row["SOLUONG"].ToString(), row["STATUS"].ToString()
                }));
            return lv;
        }

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

            _bus.Publish(new HoanThanhYMVNCompletedEvent(result));
        }

        public List<string> GetDanhSachGioYMVN(string ngayXuatMDY)
            => _tableOrderRepo.GetDanhSachGioYMVN(ngayXuatMDY).ToList();

        public void UploadMilkrunSP(DataTable donHang, string ngayGiao)
        {
            _tableOrderRepo.UploadMilkrunSP(donHang, ngayGiao);
        }

        public void SyncPhieuTuBangRiengChoDocQR(
            DataTable donHang,
            string ngayGiao,
            List<string> checkedGios = null)
        {
            if (donHang == null || donHang.Rows.Count == 0) return;

            _phieuRepo.XoaTmpPhieu(_cfg.Delivery.TmpTable);

            foreach (DataRow row in donHang.Rows)
            {
                string status = row["STATUS"]?.ToString() ?? "";
                if (status == "OK") continue;

                string gio = "";
                if (row.Table.Columns.Contains("NGAYGIAO") &&
                    row["NGAYGIAO"] != DBNull.Value &&
                    DateTime.TryParse(row["NGAYGIAO"].ToString(), out DateTime dt))
                    gio = dt.ToString("HH:mm");

                if (checkedGios != null && checkedGios.Any())
                {
                    bool match = checkedGios.Any(g =>
                        gio.StartsWith(g.Length >= 2 ? g.Substring(0, 2) : g));
                    if (!match) continue;
                }

                string nxh = row.Table.Columns.Contains("NGAYGIAO") &&
                             row["NGAYGIAO"] != DBNull.Value &&
                             DateTime.TryParse(row["NGAYGIAO"].ToString(), out DateTime ngay)
                    ? ngay.ToString("yyyy-MM-dd HH:mm:ss")
                    : ngayGiao + " 00:00:00";

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
            dt.Columns.Add("MH");
            dt.Columns.Add("LOT");
            dt.Columns.Add("SLC", typeof(int));
            dt.Columns.Add("SLTK", typeof(int));
            dt.Columns.Add("SLT", typeof(int));
            dt.Columns.Add("STATUS");
            foreach (var e in list)
                dt.Rows.Add(e.MH, e.LOT, e.SLC, e.SLTK, e.SLT, e.Ms);
            return dt;
        }

        public DataTable GetDanhSachLotTuKho(string maHang)
            => _phieuRepo.GetDanhSachLotTuKho(maHang);

        public void NhapLotThuCong(int stt, string lotNo, string tenbang)
        {
            _phieuRepo.CapNhapLotTmpPhieu(stt, lotNo, GetTenBan());
        }

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
                Category = isLoaiSP
                    ? OrderCategory.SP
                    : OrderCategory.MP,
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