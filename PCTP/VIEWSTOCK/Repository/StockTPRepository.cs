using PCTP.ClassSQL;
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

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql,
                new List<SqlParameter> { new SqlParameter("@find", find) });

            return dt.Rows.Count > 0 ? MapPhieu(dt.Rows[0]) : null;
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
            Stt = r["STT"] == DBNull.Value ? 0 : Convert.ToInt32(r["STT"]),
            Find = r["FIND"] as string,
            LotNo = r["LOT_NO"] as string,
            Model = r["MODEL"] as string,
            TenSP = r["TEN_SAN_PHAM"] as string,
            MaSP = r["MA_SAN_PHAM"] as string,
            CaSX = r["CA_SAN_XUAT"] == DBNull.Value ? 0 : Convert.ToInt32(r["CA_SAN_XUAT"]),
            NgaySX = r["NGAY_SAN_XUAT"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["NGAY_SAN_XUAT"]),
            SlSanXuat = r["SL_DA_SAN_XUAT"] == DBNull.Value ? 0 : Convert.ToInt32(r["SL_DA_SAN_XUAT"]),
            SlDaNhap = r["SL_DA_NHAP"] == DBNull.Value ? 0 : Convert.ToInt32(r["SL_DA_NHAP"]),
            SlDaTra = r["SL_DA_TRA"] == DBNull.Value ? 0 : Convert.ToInt32(r["SL_DA_TRA"]),
            LyDoTra = r["LY_DO_TRA"] as string,
            TonKhoTP = r["TON_KHO_TP"] == DBNull.Value ? 0 : Convert.ToInt32(r["TON_KHO_TP"]),
            KetThucLot = r["KET_THUC_LOT"] != DBNull.Value && Convert.ToInt32(r["KET_THUC_LOT"]) == 1
        };

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
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT * FROM STOCKTP WHERE LOT = @lot",
                new List<SqlParameter> { new SqlParameter("@lot", lot) });

            if (dt.Rows.Count == 0) return null;
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

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql,
                new List<SqlParameter> { new SqlParameter("@lot", lot) });

            var list = new List<StockTraHangInfo>();
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
    }
}
