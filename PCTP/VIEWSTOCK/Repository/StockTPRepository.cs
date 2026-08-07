using PCTP.ClassSQL;
using PCTP.Domain.Events;
using PCTP.Infrastructure;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    /// <summary>
    /// Thao tác với STOCKTP / vNhapTP / NHAP_TP_HIS / STOCKTPTRAHANG / STOCKTPNHANTRA.
    /// Toàn bộ chạy trên _sql.B7R2_FCCdb (đúng DB chứa các bảng này — KHÔNG phải
    /// B7R2_FCCdbb, nơi chứa Warehouse/Rack/Slot/SlotLot).
    /// </summary>
    public class StockTpRepository : IStockTpRepository
    {
        private readonly SQLPROVIDER _sql;

        public StockTpRepository(SQLPROVIDER sql) => _sql = sql;

        // ══════════════ PHIẾU SẢN XUẤT (vNhapTP) ══════════════
        public PhieuNhapInfo GetPhieuByFind(string find)
        {
            const string sql = @"SELECT STT, FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM,
                                 CA_SAN_XUAT, NGAY_SAN_XUAT, SL_DA_SAN_XUAT,
                                 SL_DA_NHAP, SL_DA_TRA, LY_DO_TRA, TON_KHO_TP, KET_THUC_LOT
                          FROM vNhapTP WHERE FIND = @find";

            // ← ĐỔI: LoadData1 thay vì ExecuteQuery(List<SqlParameter>)
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@find", find));

            return dt != null && dt.Rows.Count > 0 ? MapPhieu(dt.Rows[0]) : null;
        }

        public List<PhieuNhapInfo> GetPhieuTong()
        {
            const string sql = @"SELECT STT, FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM,
                                         CA_SAN_XUAT, NGAY_SAN_XUAT, SL_DA_SAN_XUAT,
                                         SL_DA_NHAP, SL_DA_TRA, LY_DO_TRA, TON_KHO_TP, KET_THUC_LOT
                                  FROM vNhapTP ORDER BY NGAY_SAN_XUAT DESC";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            return dt.Rows.Cast<DataRow>().Select(MapPhieu).ToList();
        }

        private static PhieuNhapInfo MapPhieu(DataRow r) => new PhieuNhapInfo
        {
            Stt = SafeInt(r["STT"]),
            Find = r["FIND"] as string,
            LotNo = r["LOT_NO"] as string,
            Model = r["MODEL"] as string,
            TenSP = r["TEN_SAN_PHAM"] as string,
            MaSP = r["MA_SAN_PHAM"] as string,
            CaSX = SafeInt(r["CA_SAN_XUAT"]),
            NgaySX = SafeDate(r["NGAY_SAN_XUAT"]),
            SlSanXuat = SafeInt(r["SL_DA_SAN_XUAT"]),
            SlDaNhap = SafeInt(r["SL_DA_NHAP"]),
            SlDaTra = SafeInt(r["SL_DA_TRA"]),
            LyDoTra = r["LY_DO_TRA"] as string,
            TonKhoTP = SafeInt(r["TON_KHO_TP"]),
            KetThucLot = SafeInt(r["KET_THUC_LOT"]) == 1
        };

        // ── Helper: parse int an toàn — chịu được NULL, "", khoảng trắng,
        // hoặc chuỗi số có phần thập phân ("16000.00") mà không throw ────────
        private static int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;

            // Trường hợp cột thật sự là kiểu số (int/decimal/double...) — convert trực tiếp
            if (val is int i) return i;
            if (val is decimal || val is double || val is float)
            {
                try { return Convert.ToInt32(val); } catch { return 0; }
            }

            // Trường hợp cột là string (rỗng, có khoảng trắng, hoặc "123.00")
            string s = val.ToString().Trim();
            if (string.IsNullOrEmpty(s)) return 0;

            if (int.TryParse(s, out int result))
                return result;

            // Chuỗi số dạng thập phân "123.00" — thử parse qua decimal rồi ép về int
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal d))
                return (int)d;

            return 0; // không parse được -> 0, không throw
        }

        private static DateTime SafeDate(object val)
        {
            if (val == null || val == DBNull.Value) return DateTime.MinValue;
            if (val is DateTime dt) return dt;

            return DateTime.TryParse(val.ToString(), out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }

        // ══════════════ STOCKTP ══════════════
        public bool ExistsStockTp(string lot)
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM STOCKTP WHERE LOT = @lot",
                new[] { new SqlParameter("@lot", lot) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public StockItem GetByLot(string lot)
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                "SELECT * FROM STOCKTP WHERE LOT = @lot",
               new SqlParameter("@lot", lot));

            if (dt == null || dt.Rows.Count == 0) return null;

            DataRow r = dt.Rows[0];

            return new StockItem
            {
                Lot = r["LOT"] as string,
                Part = r["PART"] as string,
                Name = r["NAME"] as string,
                Model = r["MODEL"] as string,
                SlNhap = r["SLNHAP"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SLNHAP"]),
                SlConLai = r["SLCONLAI"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SLCONLAI"]),
                SlXuat = r["SLXUAT"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SLXUAT"]),
                Satus = r["SATUS"] == DBNull.Value ? (short?)null : Convert.ToInt16(r["SATUS"]),
                CaSX = r["CASX"] == DBNull.Value ? (short?)null : Convert.ToInt16(r["CASX"]),
                NgaySX = r["NGAYSX"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYSX"]),
                NgayNhap = r["NGAYNHAP"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYNHAP"])
            };
        }

        public int GetSlConLai(string lot)
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT ISNULL(SLCONLAI, 0) FROM STOCKTP WHERE LOT = @lot",
                new[] { new SqlParameter("@lot", lot) });
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public void InsertStockTp(NhapKhoItem item, int status)
        {
            const string sql = @"INSERT INTO STOCKTP
                (LOT, MODEL, Part, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, Satus)
                VALUES (@lot, @model, @part, @name, @casx, @ngaysx, @slsx, @ngaynhap, @slnhap, @ngaynhap, 0, @slnhap, @status)";

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@lot", item.Lot),
                new SqlParameter("@model", (object)item.Model ?? ""),
                new SqlParameter("@part", (object)item.Part ?? ""),
                new SqlParameter("@name", (object)item.Name ?? ""),
                new SqlParameter("@casx", item.CaSX),
                new SqlParameter("@ngaysx", (object)item.NgaySX ?? DBNull.Value),
                new SqlParameter("@slsx", item.SlSanXuat),
                new SqlParameter("@ngaynhap", DateTime.Now),
                new SqlParameter("@slnhap", item.SlNhap),
                new SqlParameter("@status", status));
        }

        /// <summary>Nhập kho — CỘNG DỒN vào SLNHAP/SLCONLAI. Dùng khi có thêm hàng nhập vào LOT đã tồn tại.</summary>
        public void UpdateStockTp(string lot, int slSeNhap, int status)
        {
            const string sql = @"UPDATE STOCKTP SET
                SLNHAP = ISNULL(SLNHAP,0) + @sl,
                SLCONLAI = ISNULL(SLCONLAI,0) + @sl,
                NGAYNHAP = CAST(GETDATE() AS smalldatetime),
                Satus = @status
                WHERE LOT = @lot";

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@sl", slSeNhap),
                new SqlParameter("@status", status),
                new SqlParameter("@lot", lot));
        }

        /// <summary>
        /// Xuất kho THẬT — giao hàng ra khỏi nhà máy. TRỪ SLCONLAI, CỘNG SLXUAT, cập nhật NGAYXUAT.
        /// KHÔNG dùng cho export/move nội bộ giữa các Slot trong kho (nội bộ chỉ đụng SlotLot,
        /// không đụng STOCKTP — invariant: SUM(SlotLot Active theo LOT) == STOCKTP.SLCONLAI).
        /// </summary>
        public void XuatKhoThat(string lot, int slXuat)
        {
            if (slXuat <= 0) return;

            const string sql = @"UPDATE STOCKTP SET
                SLXUAT = ISNULL(SLXUAT,0) + @sl,
                SLCONLAI = ISNULL(SLCONLAI,0) - @sl,
                NGAYXUAT = CAST(GETDATE() AS smalldatetime)
                WHERE LOT = @lot";

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@sl", slXuat),
                new SqlParameter("@lot", lot));
        }

        // ══════════════ ĐỐI CHIẾU TỒN KHO ══════════════
        public List<(string Lot, int SlConLai)> GetDanhSachLotConTon()
        {
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT LOT, ISNULL(SLCONLAI,0) AS SLCONLAI FROM STOCKTP WHERE ISNULL(SLCONLAI,0) > 0");

            return dt.Rows.Cast<DataRow>()
                .Select(r => (Lot: r["LOT"].ToString(), SlConLai: Convert.ToInt32(r["SLCONLAI"])))
                .ToList();
        }

        public Dictionary<string, int> GetSlConLaiBatch(IEnumerable<string> lots)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lotList = lots?.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            if (lotList == null || lotList.Count == 0) return result;

            string inClause = string.Join(",",
                lotList.Select(l => $"'{l.Replace("'", "''")}'"));

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT LOT, ISNULL(SLCONLAI,0) AS SLCONLAI FROM STOCKTP WHERE LOT IN ({inClause})");

            foreach (DataRow r in dt.Rows)
                result[r["LOT"].ToString()] = Convert.ToInt32(r["SLCONLAI"]);

            return result;
        }

        // ══════════════ CASE DEDUP (NHAP_TP_HIS) ══════════════
        public bool ExistsCaseHistory(string caseNo)
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM NHAP_TP_HIS WHERE LOTCASE = @caseNo",
                new[] { new SqlParameter("@caseNo", caseNo) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public void InsertCaseHistory(string caseNo)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                "INSERT INTO NHAP_TP_HIS (LOTCASE) VALUES (@caseNo)",
                new SqlParameter("@caseNo", caseNo));
        }

        // ══════════════ NG (STOCKTPTRAHANG / STOCKTPNHANTRA) ══════════════
        public List<StockTraHangInfo> GetTraHangConLai(string lot)
        {
            const string sql = @"SELECT LOT, NGAYTRA, SLTRA, SLNHANLAI, LY_DO_NG
                                  FROM STOCKTPTRAHANG WHERE STATUS = 0 AND LOT = @lot";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, sql,
             new SqlParameter("@lot", lot));

            var list = new List<StockTraHangInfo>();
            if (dt == null) return list;
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new StockTraHangInfo
                {
                    Lot = r["LOT"] as string,
                    NgayTra = r["NGAYTRA"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYTRA"]),
                    SlTra = r["SLTRA"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTRA"]),
                    SlNhanLai = r["SLNHANLAI"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLNHANLAI"]),
                    LyDoNg = r["LY_DO_NG"] as string
                });
            }
            return list;
        }

        public void InsertNhanTra(string lot, string part, string name, int slNhanLai, string lyDoNg)
        {
            const string sql = @"INSERT INTO STOCKTPNHANTRA
                (LOT, PART_NO, PART_NAME, NGAY_NHAN_TRA, SL_NHAN_TRA, LY_DO_NG)
                VALUES (@lot, @part, @name, @ngay, @sl, @lyDo)";

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@lot", lot),
                new SqlParameter("@part", part ?? ""),
                new SqlParameter("@name", name ?? ""),
                new SqlParameter("@ngay", DateTime.Now),
                new SqlParameter("@sl", slNhanLai),
                new SqlParameter("@lyDo", lyDoNg ?? ""));
        }

        public void UpdateTraHangSauNhanLai(string lot, string lyDoNg, int slNhanLai, int status)
        {
            const string sql = @"UPDATE STOCKTPTRAHANG SET
                SLNHANLAI = SLNHANLAI + @sl,
                SLCONLAI = SLCONLAI - @sl,
                STATUS = @status
                WHERE LOT = @lot AND LY_DO_NG = @lyDo";

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@sl", slNhanLai),
                new SqlParameter("@status", status),
                new SqlParameter("@lot", lot),
                new SqlParameter("@lyDo", lyDoNg ?? ""));
        }
        // ══════════════ Overload transaction-aware (dùng trong NhapTpReceivingService) ══════════════
        public bool ExistsStockTp(SqlConnection conn, SqlTransaction tran, string lot)
        {
            object kq = _sql.ExecuteScalar(conn, tran,
                "SELECT COUNT(*) FROM STOCKTP WHERE LOT = @lot",
                new[] { new SqlParameter("@lot", lot) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public void InsertStockTp(SqlConnection conn, SqlTransaction tran, NhapKhoItem item, int status)
        {
            const string sql = @"INSERT INTO STOCKTP
        (LOT, MODEL, Part, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, Satus)
        VALUES (@lot, @model, @part, @name, @casx, @ngaysx, @slsx, @ngaynhap, @slnhap, @ngaynhap, 0, @slnhap, @status)";

            _sql.ExecuteNonQuery(conn, tran, sql,
                new SqlParameter("@lot", item.Lot),
                new SqlParameter("@model", (object)item.Model ?? ""),
                new SqlParameter("@part", (object)item.Part ?? ""),
                new SqlParameter("@name", (object)item.Name ?? ""),
                new SqlParameter("@casx", item.CaSX),
                new SqlParameter("@ngaysx", (object)item.NgaySX ?? DBNull.Value),
                new SqlParameter("@slsx", item.SlSanXuat),
                new SqlParameter("@ngaynhap", DateTime.Now),
                new SqlParameter("@slnhap", item.SlNhap),
                new SqlParameter("@status", status));
        }

        public void UpdateStockTp(SqlConnection conn, SqlTransaction tran, string lot, int slSeNhap, int status)
        {
            const string sql = @"UPDATE STOCKTP SET
        SLNHAP = ISNULL(SLNHAP,0) + @sl,
        SLCONLAI = ISNULL(SLCONLAI,0) + @sl,
        NGAYNHAP = CAST(GETDATE() AS smalldatetime),
        Satus = @status
        WHERE LOT = @lot";

            _sql.ExecuteNonQuery(conn, tran, sql,
                new SqlParameter("@sl", slSeNhap),
                new SqlParameter("@status", status),
                new SqlParameter("@lot", lot));
        }

        public bool ExistsCaseHistory(SqlConnection conn, SqlTransaction tran, string caseNo)
        {
            object kq = _sql.ExecuteScalar(conn, tran,
                "SELECT COUNT(*) FROM NHAP_TP_HIS WHERE LOTCASE = @caseNo",
                new[] { new SqlParameter("@caseNo", caseNo) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public void InsertCaseHistory(SqlConnection conn, SqlTransaction tran, string caseNo)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "INSERT INTO NHAP_TP_HIS (LOTCASE) VALUES (@caseNo)",
                new SqlParameter("@caseNo", caseNo));
        }
        // StockTpRepository.cs — thêm (using PCTP.VIEWSTOCK.Fuction;)
        // StockTpRepository.cs — thêm (using PCTP.VIEWSTOCK.Fuction;)
        public PhieuNhapInfo TimPhieuTheoLotQR(string rawLotNoSL, string maHang)
        {
            if (string.IsNullOrWhiteSpace(rawLotNoSL) || string.IsNullOrWhiteSpace(maHang))
                return null;

            string idPadded = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT STUFF('00000', 5-LEN(id)+1, LEN(id), id) " +
                $"FROM B20Item WHERE code = '{maHang.Replace("'", "''")}'");
            if (string.IsNullOrWhiteSpace(idPadded)) return null;

            PhieuNhapInfo phieu = null;

            // BƯỚC 1
            var finds = LotNoHelper.BuildFindList(rawLotNoSL, idPadded);
            foreach (var find in finds)
            {
                phieu = GetPhieuByFind(find);
                if (phieu != null) break;
            }

            // BƯỚC 2 (FALLBACK) — chỉ chạy nếu bước 1 không tìm thấy gì
            if (phieu == null && rawLotNoSL.Length >= 11)
            {
                string prefix11 = rawLotNoSL.Substring(0, 11);
                string ca = rawLotNoSL.Length > 11 ? rawLotNoSL.Substring(11, 1) : "";

                if (!string.IsNullOrEmpty(ca))
                    phieu = GetPhieuByLotPrefix(prefix11 + ca, maHang);

                if (phieu == null)
                    phieu = GetPhieuByLotPrefix(prefix11, maHang);
            }

            // ── Điểm đối chiếu DUY NHẤT — áp dụng cho cả 2 bước, không sót nhánh nào ──
            if (phieu != null)
                DongBoSLSXVaMoLaiNeuThayDoi(phieu.LotNo, phieu.Find, phieu.SlSanXuat);

            return phieu;
        }

        private PhieuNhapInfo GetPhieuByLotPrefix(string lotPrefix, string maHang)
        {
            const string sql = @"SELECT STT, FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM,
                                 CA_SAN_XUAT, NGAY_SAN_XUAT, SL_DA_SAN_XUAT,
                                 SL_DA_NHAP, SL_DA_TRA, LY_DO_TRA, TON_KHO_TP, KET_THUC_LOT
                          FROM vNhapTP
                          WHERE LOT_NO LIKE @prefix + '%'
                            AND MA_SAN_PHAM = @maHang";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@prefix", lotPrefix),
                new SqlParameter("@maHang", maHang));

            if (dt == null || dt.Rows.Count == 0) return null;

            // ⚠️ Chỉ chấp nhận khi khớp DUY NHẤT — nếu >1 dòng thì prefix chưa đủ để
            // phân biệt, thà trả null (báo "không tìm thấy phiếu") còn hơn nhập nhầm LOT.
            if (dt.Rows.Count > 1) return null;

            return MapPhieu(dt.Rows[0]);
        }
        // StockTpRepository.cs — thêm
        public int GetSlDaNhap(SqlConnection conn, SqlTransaction tran, string lot)
        {
            object kq = _sql.ExecuteScalar(conn, tran,
                "SELECT ISNULL(SLNHAP, 0) FROM STOCKTP WHERE LOT = @lot",
                new[] { new SqlParameter("@lot", lot) });
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }
        // StockTpRepository
        public void MoLaiLot(string lot, string find = null)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                "UPDATE STOCKTP SET Satus = 0 WHERE LOT = @lot",
                new SqlParameter("@lot", lot));

            AppEventBus.Instance.Publish(new LotStatusResetEvent(lot, find));
        }
        // StockTpRepository.cs — thêm
        public List<PhieuNhapInfo> GetPhieuDangSanXuat(int soNgayGanDay = 30)
        {
            const string sql = @"SELECT STT, FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM,
                                 CA_SAN_XUAT, NGAY_SAN_XUAT, SL_DA_SAN_XUAT,
                                 SL_DA_NHAP, SL_DA_TRA, LY_DO_TRA, TON_KHO_TP, KET_THUC_LOT
                          FROM vNhapTP
                          WHERE NGAY_SAN_XUAT >= DATEADD(DAY, -@SoNgay, CAST(GETDATE() AS DATE))
                          ORDER BY NGAY_SAN_XUAT DESC, STT DESC";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@SoNgay", soNgayGanDay));

            if (dt == null) return new List<PhieuNhapInfo>();
            var list = dt.Rows.Cast<DataRow>().Select(MapPhieu).ToList();
            foreach (var p in list)
                DongBoSLSXVaMoLaiNeuThayDoi(p.LotNo, p.Find, p.SlSanXuat);

            return list;
        }
        // StockTpRepository — thêm method đối chiếu + tự mở khoá
        /// <summary>
        /// Đối chiếu SLSX hiện tại (từ vNhapTP — nguồn MES sống) với SLSX đã lưu trong STOCKTP.
        /// Nếu khác nhau (MES tăng/giảm sản lượng), coi như "kế hoạch SX đổi" -> tự mở lại LOT
        /// (Satus = 0) và đồng bộ lại SLSX mới, publish event cho các form đang mở biết.
        /// Trả về true nếu vừa thực hiện reset (để caller biết mà refresh UI nếu cần).
        /// </summary>
        public bool DongBoSLSXVaMoLaiNeuThayDoi(string lot, string find, int slsxMoiTuMES)
        {
            if (string.IsNullOrWhiteSpace(lot)) return false;

            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT Satus, ISNULL(SLSX,0) FROM STOCKTP WHERE LOT = @lot",
                new[] { new SqlParameter("@lot", lot) });

            // Chưa từng nhập kho LOT này -> không có gì để đồng bộ
            if (kq == null || kq == DBNull.Value) return false;

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                "SELECT Satus, ISNULL(SLSX,0) AS SLSX FROM STOCKTP WHERE LOT = @lot",
                new SqlParameter("@lot", lot));
            if (dt.Rows.Count == 0) return false;

            int satusHienTai = dt.Rows[0]["Satus"] == DBNull.Value ? 0 : Convert.ToInt32(dt.Rows[0]["Satus"]);
            int slsxDaLuu = Convert.ToInt32(dt.Rows[0]["SLSX"]);

            // Chỉ cần xử lý khi đang bị khoá (Satus=1) VÀ SLSX MES đã đổi so với lúc khoá
            if (satusHienTai != 1 || slsxMoiTuMES == slsxDaLuu)
                return false;

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                "UPDATE STOCKTP SET Satus = 0, SLSX = @slsxMoi WHERE LOT = @lot",
                new SqlParameter("@slsxMoi", slsxMoiTuMES),
                new SqlParameter("@lot", lot));

            AppEventBus.Instance.Publish(new LotStatusResetEvent(lot, find));
            return true;
        }
    }
}
