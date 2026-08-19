using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Infrastructure.Repositories
{
    /// <summary>
    /// Query SQL Server dùng chung — implement ISqlRepository.
    /// </summary>
    public class SqlRepository : ISqlRepository
    {
        private readonly SQLPROVIDER _sql;
        public SqlRepository(SQLPROVIDER sql) => _sql = sql;

        // ── Số hộp ──────────────────────────────────────────────────────────────
        public int GetMinCloseQty(string maHang)
        {
            string sql =
                "IF EXISTS (SELECT 1 FROM B20Item WHERE Code = '" + Esc(maHang) + "') " +
                "    SELECT CAST(MinCloseQty AS INT) FROM B20Item WHERE Code = '" + Esc(maHang) + "' " +
                "ELSE SELECT 0";
            string kq = _sql.ExecuteReader(_sql.B7R2_FCCdb, sql);
            int result;
            return int.TryParse(kq, out result) ? result : 0;
        }

        // ── LOT đã lưu ──────────────────────────────────────────────────────────
        public string GetSavedLot(string cua, string truyen, string maHang,
                           int soLuong, string ngayGiao, string gioGiao,
                           string nhaMayLike)
        {
            // FIX: ngayGiao có thể là ddMMyyyy hoặc yyyy-MM-dd
            // Chuẩn hóa về yyyy-MM-dd trước khi truyền vào SQL Server
            string ngayGiaoSql = NormalizeNgayGiao(ngayGiao);

            string sql =
                "SELECT LOT FROM LUUPHIEUGIAOHANG " +
                $"WHERE CUA      = '{Esc(cua)}' " +
                $"AND   TRUYEN   = '{Esc(truyen)}' " +
                $"AND   MAHANG   = '{Esc(maHang)}' " +
                $"AND   SOLUONG  = {soLuong} " +
                $"AND   NGAYGIAO = '{ngayGiaoSql}' " +  // ← yyyy-MM-dd
                $"AND   GIOGIAO  = '{Esc(gioGiao)}' " +
                $"AND   NHAMAY LIKE '%{Esc(nhaMayLike)}%'";

            string lot = _sql.ExecuteReader(_sql.B7R2_FCCdb, sql);
            return lot ?? "";
        }

        // Chuẩn hóa ngày về yyyy-MM-dd
        private static string NormalizeNgayGiao(string ngayGiao)
        {
            if (string.IsNullOrWhiteSpace(ngayGiao)) return ngayGiao;

            // Đã đúng format yyyy-MM-dd
            if (ngayGiao.Length == 10 && ngayGiao[4] == '-')
                return ngayGiao;

            // Format ddMMyyyy (8 ký tự, không có dấu -)
            if (ngayGiao.Length == 8 && !ngayGiao.Contains('-'))
            {
                string dd = ngayGiao.Substring(0, 2);
                string mm = ngayGiao.Substring(2, 2);
                string yyyy = ngayGiao.Substring(4, 4);
                return $"{yyyy}-{mm}-{dd}";
            }

            // Thử parse tổng quát
            if (DateTime.TryParse(ngayGiao, out DateTime dt))
                return dt.ToString("yyyy-MM-dd");

            return ngayGiao;
        }

        // ── Ghép lot ────────────────────────────────────────────────────────────
        public void XoaVaInsertTmpLotGhep(IEnumerable<GhepLotItem> items)
        {
            // Xóa toàn bộ trước
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, "DELETE FROM TMPLOTGHEP");

            foreach (var item in items)
            {
                string sql =
                    "INSERT INTO TMPLOTGHEP (LOT, MAHANG, GIOXUAT, flag) " +
                    "VALUES ('" + Esc(item.Lot) + "', " +
                            "'" + Esc(item.MaHang) + "', " +
                                 item.GioXuat + ", 0)";
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql);
            }
        }

        public DataTable GetGhepLotPrint()
        {
            // Form gốc: ExecuteProcedureReturnDataSet("Usp_gheplotPrint").Tables[0]
            DataSet ds = _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb, "Usp_gheplotPrint");
            return ds != null && ds.Tables.Count > 0
                ? ds.Tables[0]
                : new DataTable();
        }

        // ── Tên máy bắn QR ──────────────────────────────────────────────────────
        public string GetTenMayBanQR()
        {
            // Form gốc HVN_PGH_Load():
            // "select TenMay from tbl_QR_MAY_DOCQR where TT = 1"
            string kq = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT TenMay FROM tbl_QR_MAY_DOCQR WHERE TT = 1");
            return kq ?? "";
        }

        // ── Kiểm tra hiện nút CNK + KiemTraMaNG ─────────────────────────────────
        public int GetAddCmdMang()
        {
            // Form gốc SET_PHIEU():
            // "select dbo.ufn_QRcode_ADD_CMD_MANG() gt"
            // Trả về 1 hoặc 2 → hiện nút; 0 → ẩn
            string kq = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT dbo.ufn_QRcode_ADD_CMD_MANG()");
            int result;
            return int.TryParse(kq, out result) ? result : 0;
        }

        // ── Metadata phiếu đang dở ──────────────────────────────────────────────
        public PhieuMeta GetPhieuMeta()
        {
            // Form gốc LoadDL() nhánh khi DOCQRCODE đã có data:
            // "select top(1) ADDNM,NGAYGIAO,GIOGIAOFCC,NHAMAY from IFSPHIEUGIAOHANG"
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                "SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY FROM IFSPHIEUGIAOHANG");

            if (dt == null || dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            int addNm;
            return new PhieuMeta
            {
                AddNm = int.TryParse(SafeStr(r["ADDNM"]), out addNm) ? addNm : 1,
                NgayGiao = SafeStr(r["NGAYGIAO"]),
                GioGiaoFcc = SafeStr(r["GIOGIAOFCC"]),
                NhaMay = SafeStr(r["NHAMAY"])
            };
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("'", "''");
        }

        private static string SafeStr(object val)
        {
            if (val == null || val == DBNull.Value) return "";
            return val.ToString();
        }
    }
}
