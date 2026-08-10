using PCTP.ClassSQL;
using PCTP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public class TraHangRepository : ITraHangRepository
    {
        private readonly SQLPROVIDER _sql;
        public TraHangRepository(SQLPROVIDER sql) => _sql = sql;

        private static string Esc(string s) => (s ?? "").Replace("'", "''");

        // ════════════════════════════════════════════════════════════════
        // STOCKTPTRAHANG — ghi nhận 1 lần trả hàng NG (chưa được nhận lại)
        // ════════════════════════════════════════════════════════════════
        public void InsertTraHang(SqlConnection conn, SqlTransaction tran,
            string lot, int slTra, string lyDoNg, string nguon)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
                INSERT INTO STOCKTPTRAHANG
                    (LOT, NGAYTRA, SLTRA, SLNHANLAI, LY_DO_NG, STATUS)
                VALUES
                    (@lot, GETDATE(), @sl, 0, @lyDo, 0)",
                new SqlParameter("@lot", lot),
                new SqlParameter("@sl", slTra),
                new SqlParameter("@lyDo", $"[{nguon}] " + (lyDoNg ?? "")));
        }

        public void TruSlConLai(SqlConnection conn, SqlTransaction tran, string lot, int soLuong)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE STOCKTP SET SLCONLAI = ISNULL(SLCONLAI,0) - @sl WHERE LOT = @lot",
                new SqlParameter("@sl", soLuong), new SqlParameter("@lot", lot));
        }

        // Khách trả hàng: cộng lại tồn, trừ SLXUAT (vì hàng coi như "chưa xuất" nữa)
        public void NhapLaiHangKhachTra(SqlConnection conn, SqlTransaction tran, string lot, int soLuong)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
                UPDATE STOCKTP SET
                    SLCONLAI = ISNULL(SLCONLAI,0) + @sl,
                    SLXUAT   = ISNULL(SLXUAT,0)   - @sl
                WHERE LOT = @lot",
                new SqlParameter("@sl", soLuong), new SqlParameter("@lot", lot));
        }

        public void InsertNhanTraTheoIDP(SqlConnection conn, SqlTransaction tran,
            string lot, int slNhanTra, int idp)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
                INSERT INTO STOCKTPNHANTRA
                    (LOT, PART_NO, PART_NAME, NGAY_NHAN_TRA, SL_NHAN_TRA, LY_DO_NG)
                SELECT TOP 1 @lot, PART, NAME, GETDATE(), @sl, N'Khách trả — Phiếu ' + CAST(@idp AS NVARCHAR(20))
                FROM STOCKTP WHERE LOT = @lot",
                new SqlParameter("@lot", lot),
                new SqlParameter("@sl", slNhanTra),
                new SqlParameter("@idp", idp));
        }

        // ════════════════════════════════════════════════════════════════
        // TMPCHOGIAO — staging chờ giao
        // ════════════════════════════════════════════════════════════════
        public void InsertChoGiao(int slotIdNguon, string lotThung, string lotGoc,
            string maHang, int soLuong, string phieuGiaoId)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, @"
                INSERT INTO TMPCHOGIAO
                    (LotThung, LotGoc, MaHang, SoLuong, SlotIdNguon, PhieuGiaoId, TrangThai)
                VALUES
                    (@lt, @lg, @mh, @sl, @slot, @pg, 'CHO_GIAO')",
                new SqlParameter("@lt", lotThung),
                new SqlParameter("@lg", lotGoc),
                new SqlParameter("@mh", maHang),
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@slot", (object)slotIdNguon ?? DBNull.Value),
                new SqlParameter("@pg", (object)phieuGiaoId ?? DBNull.Value));
        }

        public List<ChoGiaoItem> GetChoGiaoTheoDanhSach(IEnumerable<int> ids)
        {
            var idList = ids?.ToList() ?? new List<int>();
            var result = new List<ChoGiaoItem>();
            if (idList.Count == 0) return result;

            string inClause = string.Join(",", idList);
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT * FROM TMPCHOGIAO WHERE Id IN ({inClause})");

            foreach (DataRow r in dt.Rows)
                result.Add(new ChoGiaoItem
                {
                    Id = Convert.ToInt32(r["Id"]),
                    LotThung = r["LotThung"].ToString(),
                    LotGoc = r["LotGoc"].ToString(),
                    MaHang = r["MaHang"].ToString(),
                    SoLuong = Convert.ToInt32(r["SoLuong"]),
                    SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),
                    TrangThai = r["TrangThai"].ToString()
                });
            return result;
        }

        public void CapNhatTrangThaiChoGiao(SqlConnection conn, SqlTransaction tran,
            IEnumerable<int> ids, string trangThaiMoi)
        {
            var idList = ids?.ToList() ?? new List<int>();
            if (idList.Count == 0) return;

            string inClause = string.Join(",", idList);
            _sql.ExecuteNonQuery(conn, tran,
                $"UPDATE TMPCHOGIAO SET TrangThai = @tt WHERE Id IN ({inClause})",
                new SqlParameter("@tt", trangThaiMoi));
        }

        // ════════════════════════════════════════════════════════════════
        // TMPQUETTHUNGTRA — quét thùng khách trả
        // ════════════════════════════════════════════════════════════════
        public bool ExistsThungDaQuet(int idp, string lotThung)
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM TMPQUETTHUNGTRA WHERE IDP=@idp AND LotThung=@lt",
                new[] { new SqlParameter("@idp", idp), new SqlParameter("@lt", lotThung) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public void InsertThungQuetTra(int idp, string lotThung, string lotGoc,
            string maHang, int slThung)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, @"
                INSERT INTO TMPQUETTHUNGTRA (IDP, LotThung, LotGoc, MaHang, SlThung, DaXuLy)
                VALUES (@idp, @lt, @lg, @mh, @sl, 0)",
                new SqlParameter("@idp", idp),
                new SqlParameter("@lt", lotThung),
                new SqlParameter("@lg", lotGoc),
                new SqlParameter("@mh", maHang),
                new SqlParameter("@sl", slThung));
        }

        public List<NhomLotTraInfo> GetNhomLotChuaXuLy(int idp)
        {
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, @"
                SELECT LotGoc, MaHang, SUM(SlThung) AS TongSL
                FROM TMPQUETTHUNGTRA
                WHERE IDP = @idp AND DaXuLy = 0
                GROUP BY LotGoc, MaHang",
                new List<SqlParameter> { new SqlParameter("@idp", idp) });

            return dt.Rows.Cast<DataRow>().Select(r => new NhomLotTraInfo
            {
                LotGoc = r["LotGoc"].ToString(),
                MaHang = r["MaHang"].ToString(),
                TongSl = Convert.ToInt32(r["TongSL"])
            }).ToList();
        }

        public void DanhDauDaXuLy(SqlConnection conn, SqlTransaction tran, int idp)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE TMPQUETTHUNGTRA SET DaXuLy = 1 WHERE IDP = @idp AND DaXuLy = 0",
                new SqlParameter("@idp", idp));
        }

        public void DanhDauPhieuDaNhapKho(SqlConnection conn, SqlTransaction tran, int idp)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE TMPPHIEUNHANDB SET DA_NHAP_KHO = 1 WHERE IDP = @idp",
                new SqlParameter("@idp", idp));
        }
    }
}
