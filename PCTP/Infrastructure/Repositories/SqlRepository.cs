using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Infrastructure.Repositories
{
    /// <summary>
    /// Query SQL Server dùng chung — implement ISqlRepository.
    /// </summary>
    public class SqlRepository : SqlRepositoryBase, ISqlRepository
    {
        public SqlRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public int GetMinCloseQty(string maHang)
        {
            object kq = ExecuteScalar(
                "IF EXISTS (SELECT 1 FROM B20Item WHERE Code = @ma) " +
                "  SELECT CAST(MinCloseQty AS INT) FROM B20Item WHERE Code = @ma " +
                "ELSE SELECT 0",
                new SqlParameter("@ma", maHang ?? ""));
            return int.TryParse(kq?.ToString(), out int result) ? result : 0;
        }

        public string GetSavedLot(string cua, string truyen, string maHang,
            int soLuong, string ngayGiao, string gioGiao, string nhaMayLike)
        {
            string ngayGiaoSql = NormalizeNgayGiao(ngayGiao);

            object kq = ExecuteScalar(
                "SELECT LOT FROM LUUPHIEUGIAOHANG " +
                "WHERE CUA=@cua AND TRUYEN=@truyen AND MAHANG=@maHang AND SOLUONG=@sl " +
                "  AND NGAYGIAO=@ngayGiao AND GIOGIAO=@gioGiao AND NHAMAY LIKE @nhaMayLike",
                new SqlParameter("@cua", cua ?? ""),
                new SqlParameter("@truyen", truyen ?? ""),
                new SqlParameter("@maHang", maHang ?? ""),
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@ngayGiao", ngayGiaoSql),
                new SqlParameter("@gioGiao", gioGiao ?? ""),
                new SqlParameter("@nhaMayLike", $"%{nhaMayLike}%"));

            return kq?.ToString() ?? "";
        }

        private static string NormalizeNgayGiao(string ngayGiao)
        {
            if (string.IsNullOrWhiteSpace(ngayGiao)) return ngayGiao;
            if (ngayGiao.Length == 10 && ngayGiao[4] == '-') return ngayGiao;
            if (ngayGiao.Length == 8 && !ngayGiao.Contains('-'))
            {
                string dd = ngayGiao.Substring(0, 2);
                string mm = ngayGiao.Substring(2, 2);
                string yyyy = ngayGiao.Substring(4, 4);
                return $"{yyyy}-{mm}-{dd}";
            }
            return DateTime.TryParse(ngayGiao, out DateTime dt) ? dt.ToString("yyyy-MM-dd") : ngayGiao;
        }

        // ── Ghép lot ─────────────────────────────────────────────────
        public void XoaVaInsertTmpLotGhep(IEnumerable<GhepLotItem> items, string machineName)
        {
            if (string.IsNullOrWhiteSpace(machineName))
                throw new ArgumentException("machineName không được rỗng.", nameof(machineName));

            ExecuteNonQuery(
                "DELETE FROM TMPLOTGHEP WHERE MachineName = @machine",
                new SqlParameter("@machine", machineName));

            foreach (var item in items)
            {
                ExecuteNonQuery(
                    "INSERT INTO TMPLOTGHEP (MachineName, LOT, MAHANG, GIOXUAT, flag) " +
                    "VALUES (@machine, @lot, @maHang, @gioXuat, 0)",
                    new SqlParameter("@machine", machineName),
                    new SqlParameter("@lot", item.Lot ?? ""),
                    new SqlParameter("@maHang", item.MaHang ?? ""),
                    new SqlParameter("@gioXuat", item.GioXuat));
            }
        }

        public DataTable GetGhepLotPrint(string machineName)
        {
            DataSet ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_gheplotPrint",
                new SqlParameter("@MACHINE", machineName));
            return ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }

        // ── Tên máy bắn QR ───────────────────────────────────────────
        public string GetTenMayBanQR()
        {
            object kq = ExecuteScalar("SELECT TenMay FROM tbl_QR_MAY_DOCQR WHERE TT = 1");
            return kq?.ToString() ?? "";
        }

        public int GetAddCmdMang()
        {
            object kq = ExecuteScalar("SELECT dbo.ufn_QRcode_ADD_CMD_MANG()");
            return int.TryParse(kq?.ToString(), out int result) ? result : 0;
        }

        public PhieuMeta GetPhieuMeta()
        {
            DataTable dt = LoadData(
                "SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY FROM IFSPHIEUGIAOHANG");

            if (dt == null || dt.Rows.Count == 0) return null;

            DataRow r = dt.Rows[0];
            return new PhieuMeta
            {
                AddNm = int.TryParse(SafeStr(r["ADDNM"]), out int addNm) ? addNm : 1,
                NgayGiao = SafeStr(r["NGAYGIAO"]),
                GioGiaoFcc = SafeStr(r["GIOGIAOFCC"]),
                NhaMay = SafeStr(r["NHAMAY"])
            };
        }

        private static string SafeStr(object val) => val == null || val == DBNull.Value ? "" : val.ToString();
    }
}
