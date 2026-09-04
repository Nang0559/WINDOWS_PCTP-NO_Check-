using DevExpress.Office;
using DevExpress.Pdf.Native;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
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
        private readonly IEventBus _bus;
        private readonly string _tenBan;
        private readonly bool _isMayBanQR;
        private readonly CustomerConfig _cfg;
        // ── Trạng thái hiện tại — được set từ Presenter ─────────────────────
        private bool _isBanQR = false;
        private bool _isLoaiSP = false;

        public PhieuService(IPhieuRepository phieuRepo,
                            IIFSRepository ifsRepo,
                            IEventBus bus,
                            IGioXuatRepository gioXuatRepo,
                            string tenBan,
                            CustomerConfig cfg,
                            bool isMayBanQR
                            )
        {
            _phieuRepo = phieuRepo;
            _ifsRepo = ifsRepo;
            _bus = bus;
            _gioXuatRepo = gioXuatRepo;
            _tenBan = tenBan;
            _cfg = cfg;
            _isMayBanQR = isMayBanQR;
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
                return _cfg.GetTmpTable(isSP);
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
            if (_cfg.LoadTuBangRieng)
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
                string tmpTable = _cfg.GetTmpTable(isSP);
                string ifsTable = _cfg.GetIfsTable(isSP);
                string docQRTable = _cfg.GetDocQRTable(isSP);

                if (isMayBanQR && isBanQR)
                {
                    int demQR = SWLog.Measure("1. CountDocQRCode",
                        () => _phieuRepo.CountDocQRCode(docQRTable));

                    if (demQR > 0)
                    {
                        DataTable donHangTemp = null;
                        DataTable hangThieuTemp = null;

                        Parallel.Invoke(
                            () => donHangTemp = SWLog.Measure("2. LoadPhieuDocQR",
                                      () => _phieuRepo.LoadPhieuDocQR(
                                                ngayGiaoSP, nhaMay, gioFcc, addNm,
                                                tmpTable, ifsTable, docQRTable)),
                            () => hangThieuTemp = SWLog.Measure("2P. LoadHangThieu",
                                      () => _phieuRepo.LoadHangThieu(
                                                isMayBanQR, tmpTable))
                        );

                        string captionQR = $"ĐƠN HÀNG {_cfg.DisplayName}: {dt:dd/MM/yyyy}";
                        _bus.Publish(new PhieuLoadedEvent(
                            donHangTemp, hangThieuTemp, captionQR));
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
                string gioFccSP = _cfg.LoadTheoNgay ? "" : gioFcc;
                string gioMoTaSP = _cfg.LoadTheoNgay ? "Tất cả ca" : gioFccMoTa;

                if (!_cfg.LoadTheoNgay && isMayBanQR && isBanQR &&
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
                string tmpTable = _cfg.GetTmpTable(isSP);
                string ifsTable = _cfg.GetIfsTable(isSP);
                string docQRTable = _cfg.GetDocQRTable(isSP);

                string caption = _cfg.LoadTheoNgay
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

                        Parallel.Invoke(
                            () => donHangTemp = SWLog.Measure("2. LoadPhieuDocQR",
                                      () => _phieuRepo.LoadPhieuDocQR(
                                                ngayGiaoSP, nhaMay, gioFccSP, addNm,
                                                tmpTable, ifsTable, docQRTable)),
                            () => hangThieuTemp = SWLog.Measure("2P. LoadHangThieu",
                                      () => _phieuRepo.LoadHangThieu(
                                                isMayBanQR, tmpTable))
                        );

                        _bus.Publish(new PhieuLoadedEvent(
                            donHangTemp, hangThieuTemp, caption));
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

                    DataTable hangThieu = SWLog.Measure("5. LoadHangThieu",
                        () => _phieuRepo.LoadHangThieu(isMayBanQR, tmpTable));

                    _bus.Publish(new PhieuLoadedEvent(donHang, hangThieu, caption));
                }
                else
                {
                    string ifsViewTable = _cfg.GetIfsViewTable();
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

                    DataTable hangThieu = SWLog.Measure("5. LoadHangThieu",
                        () => _phieuRepo.LoadHangThieu(isMayBanQR, tenBanView));

                    _bus.Publish(new PhieuLoadedEvent(donHang, hangThieu, caption));
                }
            }
            catch (Exception)
            {
                _bus.Publish(new PhieuLoadedEvent(new DataTable(), new DataTable(), ""));
                throw;
            }
        }

        // ── Method riêng cho YMVN (thay LoadPhieuGH_GIO cũ) ─────────────────────
        /// <summary>
        /// Load phiếu từ bảng riêng (Purchase_Order_YMVN / Purchase_Order_HTN)
        /// Dùng chung cho 100002 (YMVN - có CheckGX) và 100003 (HTN - load theo ngày)
        /// </summary>
        public void LoadPhieuTuBangRieng_Internal(
         string ngayGiao,
         List<string> checkedGios,
         bool isLoaiSP,
         bool isMayBanQR,
         bool isBanQR)
        {
            if (!DateTime.TryParse(ngayGiao, out DateTime dt) || dt.Year < 2000)
            {
                _bus.Publish(new PhieuLoadedEvent(
                    new DataTable(), new DataTable(), ""));
                return;
            }

            if (_cfg.CoGear && (checkedGios == null || checkedGios.Count == 0))
            {
                _bus.Publish(new PhieuLoadedEvent(
                    new DataTable(), new DataTable(), ""));
                return;
            }

            string ngayGiaoSP = dt.ToString("yyyy-MM-dd");
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

            string dockCodeSP = isLoaiSP ? _cfg.DockCodeSP : "";
            DataTable donHang;

            if (isMayBanQR)
            {
                // ── Máy bắn QR ──────────────────────────────────────────────────
                string docQRTable = isLoaiSP
                    ? (_cfg.DocQRTableSP ?? _cfg.DocQRTable)
                    : _cfg.DocQRTable;

                int demQR = _phieuRepo.CountDocQRCode(docQRTable);

                if (demQR > 0 && isBanQR)
                {
                    // ── Đang bắn dở → đọc từ TMP (đã có LOT/STATUS) ────────────
                    string tmpTable = isLoaiSP
                        ? (_cfg.TmpTableSP ?? _cfg.TmpTable)
                        : _cfg.TmpTable;

                    donHang = _phieuRepo.LoadTuTmpTable(tmpTable);
                }
                else
                {
                    // ── Chưa bắn hoặc đã hoàn thành → query Purchase_Order ──────
                    // Rồi merge LOT từ LUUPHIEUGIAOHANG
                    donHang = _phieuRepo.LoadPhieuTuBangRieng(
                        ngayGiaoSP, gioFcc, isLoaiSP, dockCodeSP, _cfg);
                }
            }
            else
            {
                // ── Máy view → luôn query Purchase_Order + merge LUUPHIEUGIAOHANG ─
                donHang = _phieuRepo.LoadPhieuTuBangRieng(
                    ngayGiaoSP, gioFcc, isLoaiSP, dockCodeSP, _cfg);
            }

            string caption;
            if (_cfg.CoGear)
            {
                string loai = isLoaiSP ? "SP" : "MP";
                caption = $"ĐƠN HÀNG {_cfg.DisplayName} ({loai}): " +
                          $"{dt:dd/MM/yyyy}   GIỜ: {gioMoTa}";
            }
            else
            {
                caption = $"ĐƠN HÀNG {_cfg.DisplayName}: {dt:dd/MM/yyyy}";
            }

            _bus.Publish(new PhieuLoadedEvent(donHang, new DataTable(), caption));
        }

        private DataTable LoadPhieuYMVNTuIFS(string ngayXuatIFS,
                                      bool isLoaiSP,
                                      List<string> checkedGios)
        {
            string dockFilter = isLoaiSP
                ? $"AND DOCK_CODE = '{_cfg.DockCodeSP ?? "VSP1"}'"
                : $"AND DOCK_CODE <> '{_cfg.DockCodeSP ?? "VSP1"}'";

            DataTable ifsData = _ifsRepo.GetCustomerOrderJoinYMVN(
                ngayXuatIFS, _cfg.CustomerNo, dockFilter);

            if (ifsData == null || ifsData.Rows.Count == 0)
                return new DataTable();

            // ── Thêm cột nếu chưa có ────────────────────────────────────────
            foreach (string col in new[] { "STT", "HOP", "XE", "LOT", "STATUS", "STATUSDOC" })
                if (!ifsData.Columns.Contains(col))
                    ifsData.Columns.Add(col, typeof(string));

            // ── Batch query 1 lần thay vì N lần ─────────────────────────────
            var maHangList = ifsData.Rows
                .Cast<DataRow>()
                .Select(r => r["MAHANG"]?.ToString()?.Trim() ?? "")
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct()
                .ToList();

            Dictionary<string, int> qcMap = _phieuRepo.GetQcDongGoiBatch(maHangList);

            // ── Tính STT / HOP / XE ─────────────────────────────────────────
            int rowIdx = 1;
            foreach (DataRow row in ifsData.Rows)
            {
                string pno = row["MAHANG"]?.ToString()?.Trim() ?? "";
                int qty = SafeInt(row["SOLUONG"]);

                // Lookup từ Dictionary — O(1), không gọi DB
                qcMap.TryGetValue(pno, out int qcDg);
                if (qcDg <= 0) qcDg = 1;

                int hop = qty / qcDg + (qty % qcDg > 0 ? 1 : 0);
                int xe = hop / 10 + (hop % 10 > 0 ? 1 : 0);

                row["STT"] = rowIdx++.ToString();
                row["HOP"] = hop.ToString();
                row["XE"] = xe.ToString();
                row["LOT"] = "";
                row["STATUS"] = "NG";
                row["STATUSDOC"] = "NG";
            }

            return ifsData;
        }
        private void EnrichDockCodeDvFromIFS(DataTable donHang,
                                      string ngayXuatIFS,
                                      bool isLoaiSP)
        {
            if (donHang == null || donHang.Rows.Count == 0) return;

            // Thêm cột nếu chưa có
            if (!donHang.Columns.Contains("CUA"))
                donHang.Columns.Add("CUA", typeof(string));
            if (!donHang.Columns.Contains("DV"))
                donHang.Columns.Add("DV", typeof(string));

            string dockFilter = isLoaiSP
                ? "AND DOCK_CODE = 'VSP1'"
                : "AND DOCK_CODE <> 'VSP1'";

            foreach (DataRow row in donHang.Rows)
            {
                string po = row["CUSTOMER_PO_NO"]?.ToString() ?? "";
                string pno = row["MAHANG"]?.ToString() ?? "";

                // Query IFS lấy DOCK_CODE + DV — giống form gốc
                try
                {
                    DataTable ifsRow = _ifsRepo.GetDockCodeDv(
                        po, pno, _cfg.CustomerNo, dockFilter);

                    if (ifsRow != null && ifsRow.Rows.Count > 0)
                    {
                        row["CUA"] = ifsRow.Rows[0]["CUA"]?.ToString() ?? "";
                        row["DV"] = ifsRow.Rows[0]["DV"]?.ToString() ?? "";
                    }
                }
                catch { /* bỏ qua nếu IFS lỗi */ }
            }
        }
        // ════════════════════════════════════════════════════════════════════════
        // Sync IFS → TMP trước khi bắt đầu scan QR
        // ════════════════════════════════════════════════════════════════════════
        public void SyncIfsPhieuChoDocQR(string ngayGiao, string nhaMay,
                                  string gioFcc, string gioFccMoTa,
                                  int addNm)
        {
            if (!DateTime.TryParse(ngayGiao, out DateTime dt) || dt.Year < 2000) return;
            bool isSP = _isLoaiSP;
            string ngayXuat = dt.ToString("ddMMyyyy");
            string ngayGiaoSP = dt.ToString("yyyy-MM-dd");

            // ── Nếu LoadTheoNgay → bỏ filter giờ ────────────────────────────
            string gioFccSP = _cfg.LoadTheoNgay ? "" : gioFcc;
            string gioMoTaSP = _cfg.LoadTheoNgay ? "Tất cả ca" : gioFccMoTa;

            DataTable ifs = _ifsRepo.GetCustomerOrderJoin(
                ngayXuat, gioFccSP, gioMoTaSP, nhaMay, addNm, 1,
                _cfg);

            EnrichSttHop(ifs);

            _phieuRepo.LuuVaLoad(
                _cfg.GetIfsTable(isSP),
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                ifs,
                ngayGiaoSP, nhaMay, gioFccSP, addNm,
                _cfg.GetTmpTable(isSP),                    // TMPPHIEUGIAOHANG_SP
               _cfg.GetDocQRTable(isSP));                 // DOCQRCODE_SP
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

        // ════════════════════════════════════════════════════════════════════════
        // DOCQRCODE — dùng _cfg.DocQRTable
        // ════════════════════════════════════════════════════════════════════════
        public TrangThaiBan GetTrangThaiDangBan()
        {
            if (_cfg.CoGear)
                return _phieuRepo.GetTrangThaiDangBanYMVN(_cfg.TmpTable, _cfg.DocQRTable);
            return _phieuRepo.GetTrangThaiDangBan(_cfg.TmpTable, _cfg.DocQRTable);
        }
        // PhieuRepository — thêm method riêng

        public TrangThaiBan GetTrangThaiDangBanSP() =>
        _cfg.CoConfigSP
        ? _phieuRepo.GetTrangThaiDangBan(_cfg.TmpTableSP, _cfg.DocQRTableSP)
        : new TrangThaiBan { DangBan = false };
        public bool XoaDocQRCode(bool isSP = false)
        {
            _phieuRepo.XoaDocQRCode(_cfg.GetDocQRTable(isSP));
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
            return _phieuRepo.GetDonHangChuaLot(GetTenBan(isSP), _cfg.GetDocQRTable(isSP));
        }

        public DataTable LoadGhepLot() => _phieuRepo.LoadGhepLot();

        public void LayLaiLotNo(int stt, bool isSP = false)
        {
            _phieuRepo.LayLaiLotNo(stt, GetTenBan(isSP), _cfg.GetDocQRTable(isSP));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Giao DB
        // ════════════════════════════════════════════════════════════════════════
        public DataTable GetDanhSachMaHangGiaoDB() => _phieuRepo.GetDanhSachMaHang();

        public void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm) =>
            _phieuRepo.LuuGiaoDB(donHang, gioXuat.MoTa, addNm,
                              _cfg.TmpTable,   // ← FIX
                              _cfg.IfsTable);

        public DataTable LoadTmpPhieuGiaoDB() =>
            _phieuRepo.LoadTmpPhieuGiaoDB(GetTenBan());

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
            string docQRTable = _cfg.GetDocQRTable(isSP);
            string tmpTable = _cfg.GetTmpTable(isSP);       // ← THÊM

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
        public static bool IsLoaiSP(string gioMoTa)
        => !string.IsNullOrEmpty(gioMoTa)
       && (gioMoTa.Contains("SP6") || gioMoTa.Contains("SP#"));
        public static bool IsLoaiOType(string gioMoTa)
        => !string.IsNullOrEmpty(gioMoTa)
       && gioMoTa.Contains("O TYPE");
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
                if (_cfg.LoadTuBangRieng && !_cfg.CoGear)
                {
                    soLot = _phieuRepo.CapNhapKhoHTN(
                        nhaMay,
                        _cfg.GetTmpTable(isSP),
                        _cfg.GetDocQRTable(isSP),
                        out errors);
                }
                else
                {
                    // ── HVN / YMVN ────────────────────────────────────────────────
                    soLot = _phieuRepo.CapNhapKho(
                        gioGiaoFcc, nhaMay,
                        _cfg.GetTmpTable(isSP),
                        _cfg.GetDocQRTable(isSP),
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
        public void HoanThanhYMVN(bool isLoaiSP = false)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HoanThanhYMVN] TmpTable={_cfg.TmpTable}, DocQRTable={_cfg.DocQRTable}, isLoaiSP={isLoaiSP}");

            DataTable result = _phieuRepo.ExecSPWithResult("Usp_Qrcode_Take_LotYMVN2405",
                new SqlParameter("@TMPTABLE", _cfg.TmpTable),
                new SqlParameter("@DOCQRTABLE", _cfg.DocQRTable),
                new SqlParameter("@ISLOAISP", isLoaiSP ? 1 : 0));

            if (result == null || result.Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[HoanThanhYMVN] Không có dữ liệu trả về!");
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

            // TODO: gán result vào GridControl/BindingSource để hiển thị lên UI
            // ví dụ: gridControl1.DataSource = result;
        }

        // Lấy danh sách giờ từ Purchase_Order_YMVN
        public List<string> GetDanhSachGioYMVN(string ngayXuatMDY)
    => _phieuRepo.GetDanhSachGioYMVN(ngayXuatMDY).ToList();


        // Upload Milkrun SP — tương đương UploadMIKR()
        public void UploadMilkrunSP(DataTable donHang, string ngayGiao)
        {
            _phieuRepo.UploadMilkrunSP(donHang, ngayGiao);
        }


        // PhieuService — thêm SyncPhieuYMVNChoDocQR (tương đương loadG_SQL)
        // ── Gộp SyncPhieuYMVNChoDocQR + SyncPhieuTuBangRiengChoDocQR ────────────
        public void SyncPhieuTuBangRiengChoDocQR(
            DataTable donHang,
            string ngayGiao,
            List<string> checkedGios = null)  // null = HTN (không filter giờ)
        {
            if (donHang == null || donHang.Rows.Count == 0) return;

            _phieuRepo.XoaTmpPhieu(_cfg.TmpTable);

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

                _phieuRepo.InsertTmpYMVN(
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
                    tmpTable: _cfg.TmpTable,
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
        public void NhapLotThuCong(int stt, string lotNo,string tenbang)
        {
            // Ghi LOT vào TMP — giống CapNhapLotTmpPhieu
            _phieuRepo.CapNhapLotTmpPhieu(stt, lotNo, GetTenBan());
        }
    }
   
}
