using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Chuẩn bị DataTable cho báo cáo — không biết gì về ReportPrintTool.
    /// Tương thích C# 7.3 (không dùng nullable reference types).
    /// </summary>
    public class InPhieuService: IInPhieuService
    {
        private readonly IIFSRepository _ifsRepo;
        private readonly IPhieuRepository _phieuRepo;
        private readonly ISqlRepository _sqlRepo;
        private readonly IReadOnlyDictionary<string, string> _dictGioVP;
        private readonly IReadOnlyDictionary<string, string> _dictGioHN;
        private readonly CustomerConfig _cfg;   // ← THÊM

        public InPhieuService(IIFSRepository ifsRepo,
                              IPhieuRepository phieuRepo,
                              ISqlRepository sqlRepo,
                              IReadOnlyDictionary<string, string> dictGioVP,
                              IReadOnlyDictionary<string, string> dictGioHN,
                              CustomerConfig cfg)  // ← THÊM
        {
            _ifsRepo = ifsRepo;
            _phieuRepo = phieuRepo;
            _sqlRepo = sqlRepo;
            _dictGioVP = dictGioVP;
            _dictGioHN = dictGioHN;
            _cfg = cfg;        // ← THÊM
        }

        // ════════════════════════════════════════════════════════════════════
        // Nhánh GIAO DB — dùng DONHANG đã có sẵn, không cần query IFS
        // ════════════════════════════════════════════════════════════════════
        public DataTable BuildReportDataGiaoDB(DataTable donHang) => donHang;

        // ════════════════════════════════════════════════════════════════════
        // Nhánh thường — query IFS rồi enrich từng dòng
        // ════════════════════════════════════════════════════════════════════
        public DataTable BuildReportData(string ngayXuat,
                                         string gioXuat,
                                         string gioXuatH,
                                         string nhaMay,
                                         int addNm,
                                         int hinhThucIn,
                                         DataTable addressTable)
        {
            // ← FIX: thêm _cfg vào cuối — dùng overload có hinhThucIn
            DataTable pgh = _ifsRepo.GetCustomerOrderJoin(
                ngayXuat, gioXuat, gioXuatH, nhaMay, addNm, hinhThucIn, _cfg);

            EnsureColumn(pgh, "DIA_CHI", typeof(string));
            EnsureColumn(pgh, "KGX", typeof(string));
            EnsureColumn(pgh, "HOP", typeof(string));
            EnsureColumn(pgh, "LOT", typeof(string));

            foreach (DataRow row in pgh.Rows)
            {
                string maHang = SafeStr(row["MAHANG"]);
                string gio = SafeStr(row["GIOGIAO"]);
                string cua = SafeStr(row["CUA"]);
                string truyen = SafeStr(row["TRUYEN"]);
                int sl = SafeInt(row["SOLUONG"]);
                int shipNo = SafeInt(row["SHIP_ADDR_NO"]);

                // ── Số hộp ──────────────────────────────────────────────────
                int qcDg = _sqlRepo.GetMinCloseQty(maHang);
                if (qcDg > 0)
                {
                    int hop = sl / qcDg + (sl % qcDg > 0 ? 1 : 0);
                    row["HOP"] = hop.ToString();
                }

                // ── Địa chỉ + Nhà máy ───────────────────────────────────────
                FillAddress(row, addressTable);

                // ── KGX ─────────────────────────────────────────────────────
                var dict = shipNo == 1 ? _dictGioVP : _dictGioHN;
                if (dict.TryGetValue(gio, out string kgx))
                    row["KGX"] = kgx;

                // ── LOT đã lưu ──────────────────────────────────────────────
                string ngayGiaoSql = DateTime.ParseExact(
                    ngayXuat, "ddMMyyyy",
                    System.Globalization.CultureInfo.InvariantCulture)
                    .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                string nm = shipNo == 1 ? "VP" : "HA NAM";
                string gioHH = gio + "h";
                row["LOT"] = _sqlRepo.GetSavedLot(
                    cua, truyen, maHang, sl, ngayGiaoSql, gioHH, nm);
            }

            return pgh;
        }

        // ════════════════════════════════════════════════════════════════════
        // In Ghép Lot — không liên quan CustomerConfig, giữ nguyên
        // ════════════════════════════════════════════════════════════════════
        /// <summary>
        /// ✅ FIX: nhận thêm machineName — ISqlRepository.XoaVaInsertTmpLotGhep/
        /// GetGhepLotPrint đã đổi sang lọc theo TMPLOTGHEP.MachineName (tránh 2
        /// máy đá dữ liệu ghép lot của nhau), nên bắt buộc phải truyền vào đây.
        /// </summary>
        public DataTable InGhepLot(IEnumerable<GhepLotItem> selectedRows, string machineName)
        {
            if (selectedRows != null && selectedRows.Any())
                _sqlRepo.XoaVaInsertTmpLotGhep(selectedRows, machineName);

            return _sqlRepo.GetGhepLotPrint(machineName);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════
        private static void FillAddress(DataRow row, DataTable addressTable)
        {
            string shipNo = SafeStr(row["SHIP_ADDR_NO"]);
            foreach (DataRow a in addressTable.Rows)
            {
                if (SafeStr(a["ADDRESS_ID"]) != shipNo) continue;
                row["DIA_CHI"] = (SafeStr(a["ADDRESS1"]) + " " + SafeStr(a["ADDRESS2"])).Trim();
                row["NHAMAY"] = SafeStr(a["IDENTITY_NAME"]) + " - " + SafeStr(a["ADDRESS2"]);
                break;
            }
        }
        // ════════════════════════════════════════════════════════════════════
        // Nhánh YMVN — dựng dữ liệu RIÊNG cho mẫu rpPhieuGiaoHangYAM.
        // Lưới phiếu hiện tại dùng tên cột chung (MAHANG, TENHANG, SOLUONG, NGAYGIAO,
        // PO_ITEM, ...) còn mẫu in YMVN đọc [PO], [CUSTOMER_PART_NO], [CATALOG_DESC],
        // [BUY_QTY_DUE], [WANTED_DELIVERY_DATE], [Gear], [XE], [HOP], [LOT] — nên phải
        // ánh xạ lại tại đây thay vì đưa thẳng bảng lưới vào mẫu in.
        // ════════════════════════════════════════════════════════════════════
        public DataTable BuildReportDataYMVN(DataTable donHang)
            => BuildReportDataYMVN(donHang, null);

        /// <summary>
        /// <paramref name="checkedGios"/>: các giờ đang tích trong danh sách giờ xuất (dạng "HH:mm:ss"
        /// hoặc "HH:mm"); khác rỗng thì chỉ in các dòng thuộc những giờ đó. Rỗng/null = in toàn bộ lưới.
        /// </summary>
        public DataTable BuildReportDataYMVN(DataTable donHang, IList<string> checkedGios)
        {
            var report = new DataTable("YMVN_PGH");
            report.Columns.Add("STT", typeof(string));
            report.Columns.Add("WANTED_DELIVERY_DATE", typeof(DateTime));
            report.Columns.Add("PO", typeof(string));
            report.Columns.Add("DOCK_CODE", typeof(string));
            report.Columns.Add("CUSTOMER_PART_NO", typeof(string));
            report.Columns.Add("Gear", typeof(string));
            report.Columns.Add("CATALOG_DESC", typeof(string));
            report.Columns.Add("DV", typeof(string));
            report.Columns.Add("BUY_QTY_DUE", typeof(decimal));
            report.Columns.Add("HOP", typeof(string));
            report.Columns.Add("STATUS", typeof(string));
            report.Columns.Add("LOT", typeof(string));
            report.Columns.Add("XE", typeof(string));

            if (donHang == null || donHang.Rows.Count == 0)
                return report;

            var gioFilter = YmvnGioMatcher.Create(checkedGios);
            int stt = 0;

            foreach (DataRow src in donHang.Rows)
            {
                DateTime ngay;
                bool coNgay = TryGetDate(src, out ngay);
                string gioGiao = YmvnRowValue(src, "GIOGIAO", "GIO");

                if (!gioFilter.Matches(coNgay ? (DateTime?)ngay : null, gioGiao))
                    continue;

                string status = YmvnRowValue(src, "STATUS");
                string hop = YmvnRowValue(src, "HOP");
                string xe = "";
                int soHop;
                if (int.TryParse(hop, out soHop) && soHop > 0)
                    xe = ((soHop + 9) / 10).ToString();

                // Số PO của YMVN nằm ở PO_ITEM (đã chuyển từ CUA). Dòng cũ chưa có PO_ITEM thì
                // mới lùi về PO_NO để không in trống.
                string po = YmvnRowValue(src, "PO_ITEM");
                if (string.IsNullOrWhiteSpace(po))
                    po = YmvnRowValue(src, "PO_NO", "ORDER_NO");

                decimal sl;
                decimal.TryParse(YmvnRowValue(src, "SOLUONG", "BUY_QTY_DUE"),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out sl);

                DataRow row = report.NewRow();
                row["STT"] = (++stt).ToString();
                if (coNgay) row["WANTED_DELIVERY_DATE"] = ngay;
                row["PO"] = YmvnPoFormat.Format(po);
                row["DOCK_CODE"] = YmvnRowValue(src, "TRUYEN", "DOCK_CODE", "CUA");
                row["CUSTOMER_PART_NO"] = YmvnRowValue(src, "MAHANG", "CUSTOMER_PART_NO");
                row["Gear"] = YmvnRowValue(src, "GEAR", "Gear");
                row["CATALOG_DESC"] = YmvnRowValue(src, "TENHANG", "CATALOG_DESC");
                row["DV"] = YmvnRowValue(src, "DV");
                row["BUY_QTY_DUE"] = sl;
                row["HOP"] = hop;
                row["STATUS"] = status;
                // Cột "LOT NO FCC" trên phiếu YMVN không in số LOT: chỉ in "OK Check" cho dòng đã có LOT
                // (hoặc đã OK), dòng chưa có LOT in "Not Check" (giống phiếu legacy).
                string lot = YmvnRowValue(src, "LOT", "LOTNO");
                bool daCoLot = lot.Length > 0 && !string.Equals(lot, "Not Check", StringComparison.OrdinalIgnoreCase);
                row["LOT"] = (daCoLot || string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    ? "OK Check" : "Not Check";
                row["XE"] = xe;
                report.Rows.Add(row);
            }

            return report;
        }

        private static string YmvnRowValue(DataRow row, params string[] columns)
        {
            foreach (string c in columns)
            {
                if (!row.Table.Columns.Contains(c)) continue;
                string v = SafeStr(row[c]).Trim();
                if (v.Length > 0) return v;
            }
            return "";
        }

        private static bool TryGetDate(DataRow row, out DateTime value)
        {
            value = default(DateTime);
            foreach (string c in new[] { "NGAYGIAO", "WANTED_DELIVERY_DATE" })
            {
                if (!row.Table.Columns.Contains(c) || row[c] == DBNull.Value) continue;
                if (PCTP.Shared.Helpers.DateText.TryParseDb(row[c], out value)) return true;
            }
            return false;
        }
        // ════════════════════════════════════════════════════════════════════
        // Nhánh LoadTuBangRieng (HTN/YMVN) — dùng data từ grid, enrich thêm
        // địa chỉ từ addressTable + thông tin từ CustomerConfig
        // ════════════════════════════════════════════════════════════════════
        public DataTable BuildReportDataTuBangRieng(
            DataTable donHang,
            DataTable addressTable,
            string ngayXuat)
        {
            if (donHang == null || donHang.Rows.Count == 0)
                return new DataTable();

            // Clone — không sửa DataSource của grid
            DataTable dt = donHang.Copy();

            // ── Thêm cột metadata nếu chưa có ───────────────────────────────
            EnsureColumn(dt, "NHAMAY", typeof(string));
            EnsureColumn(dt, "DIA_CHI", typeof(string));
            EnsureColumn(dt, "KGX", typeof(string));
            EnsureColumn(dt, "HOP", typeof(string));
            EnsureColumn(dt, "LOT", typeof(string));

            // ── Lấy địa chỉ từ addressTable (đã load lúc form load) ─────────
            string nhamay = _cfg.Delivery.TenNhaMay;
            string diaChi = "";

            if (addressTable != null && addressTable.Rows.Count > 0)
            {
                // Dùng SHIP_ADDR_NO = AddNmMacDinh của customer
                string shipNo = _cfg.Delivery.AddNmMacDinh.ToString();
                DataRow addr = null;

                foreach (DataRow a in addressTable.Rows)
                {
                    if (SafeStr(a["ADDRESS_ID"]) == shipNo)
                    { addr = a; break; }
                }

                // Nếu không tìm theo ADDRESS_ID thì lấy dòng đầu
                if (addr == null && addressTable.Rows.Count > 0)
                    addr = addressTable.Rows[0];

                if (addr != null)
                {
                    diaChi = (SafeStr(addr["ADDRESS1"]) + " " +
                               SafeStr(addr["ADDRESS2"])).Trim();
                    nhamay = !string.IsNullOrEmpty(SafeStr(addr["IDENTITY_NAME"]))
                              ? SafeStr(addr["IDENTITY_NAME"]) + " - " +
                                SafeStr(addr["ADDRESS2"])
                              : _cfg.Delivery.TenNhaMay;
                }
            }

            // ── Batch query QcDongGoi 1 lần ──────────────────────────────────
            var maHangList = dt.Rows.Cast<DataRow>()
                .Select(r => SafeStr(r["MAHANG"]))
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct()
                .ToList();

            // Dùng GetQcDongGoiBatch nếu có, fallback GetMinCloseQty từng dòng
            Dictionary<string, int> qcMap = null;
            try
            {
                qcMap = _phieuRepo.GetQcDongGoiBatch(maHangList);
            }
            catch
            {
                qcMap = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            }

            // ── Enrich từng dòng ─────────────────────────────────────────────
            foreach (DataRow row in dt.Rows)
            {
                // Nhà máy + Địa chỉ
                row["NHAMAY"] = nhamay;
                row["DIA_CHI"] = diaChi;

                // KGX — HTN/YMVN thường không có GioVP/GioHN
                // Lấy từ GIOGIAO nếu có trong dict, fallback rỗng
                string gio = SafeStr(row.Table.Columns.Contains("GIO")
                    ? row["GIO"] : DBNull.Value);
                var dict = _cfg.Delivery.AddNmMacDinh == 1 ? _dictGioVP : _dictGioHN;
                row["KGX"] = dict.TryGetValue(gio, out string kgx) ? kgx : "";
                if (_cfg.Delivery.LoadTheoNgay &&
                row.Table.Columns.Contains("PO_NO") &&
                row.Table.Columns.Contains("GIO"))
                {
                    row["GIO"] = SafeStr(row["PO_NO"]);
                }
                // HOP — tính từ QcDongGoi
                string maHang = SafeStr(row["MAHANG"]);
                int sl = SafeInt(row.Table.Columns.Contains("SOLUONG")
                    ? row["SOLUONG"] : DBNull.Value);

                if (!qcMap.TryGetValue(maHang, out int qcDg) || qcDg <= 0)
                    qcDg = _sqlRepo.GetMinCloseQty(maHang);  // fallback

                if (qcDg > 0 && sl > 0)
                {
                    int hop = sl / qcDg + (sl % qcDg > 0 ? 1 : 0);
                    row["HOP"] = hop.ToString();
                }

                // LOT — HTN/YMVN đã có trong grid, không cần query lại
                // Chỉ đảm bảo không null
                if (row["LOT"] == DBNull.Value)
                    row["LOT"] = "";
            }

            return dt;
        }
        private static void EnsureColumn(DataTable dt, string col, Type type)
        {
            if (!dt.Columns.Contains(col))
                dt.Columns.Add(col, type);
        }

        private static string SafeStr(object val)
            => (val == null || val == DBNull.Value) ? "" : val.ToString();

        private static int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            return int.TryParse(val.ToString(), out int v) ? v : 0;
        }

        private static string ParseNgay(string ngayXuat)
        {
            return PCTP.Shared.Helpers.DateText.TryParseApp(ngayXuat, out DateTime dt)
                ? PCTP.Shared.Helpers.DateText.ToMdy(dt)
                : ngayXuat;
        }
    }
}
