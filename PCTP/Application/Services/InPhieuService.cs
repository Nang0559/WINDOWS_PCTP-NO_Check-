using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
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
    public class InPhieuService
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
                    .ToString("yyyy-MM-dd");
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
        public DataTable InGhepLot(IEnumerable<GhepLotItem> selectedRows = null)
        {
            if (selectedRows != null && selectedRows.Any())
                _sqlRepo.XoaVaInsertTmpLotGhep(selectedRows);

            return _sqlRepo.GetGhepLotPrint();
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
        // Thêm method riêng cho YMVN
        public DataTable BuildReportDataYMVN(DataTable donHang)
        {
            // YMVN: dùng data từ grid trực tiếp
            // Tương đương CMD_INPHIEUGIAO_Click trong GIAOHANGYMN
            var tbl = donHang.Copy();
            foreach (DataRow row in tbl.Rows)
            {
                string po = row["CUSTOMER_PO_NO"]?.ToString() ?? "";
                if (po.Length < 6)
                {
                    int padLen = 6 - po.Length;
                    row["CUSTOMER_PO_NO"] = "#" + po.PadLeft(5, '0');
                }
                string status = row["STATUS"]?.ToString() ?? "";
                row["LOT"] = status == "OK" ? "OK Check" : "Not Check";
            }
            return tbl;
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
            string nhamay = _cfg.TenNhaMay;
            string diaChi = "";

            if (addressTable != null && addressTable.Rows.Count > 0)
            {
                // Dùng SHIP_ADDR_NO = AddNmMacDinh của customer
                string shipNo = _cfg.AddNmMacDinh.ToString();
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
                              : _cfg.TenNhaMay;
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
                var dict = _cfg.AddNmMacDinh == 1 ? _dictGioVP : _dictGioHN;
                row["KGX"] = dict.TryGetValue(gio, out string kgx) ? kgx : "";
                if (_cfg.LoadTheoNgay &&
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
            return DateTime.TryParse(ngayXuat, out DateTime dt)
                ? dt.ToString("MM/dd/yyyy")
                : ngayXuat;
        }
    }
    
}
