using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuLuuTruRepository : SqlRepositoryBase, IPhieuLuuTruRepository
    {
        

        public PhieuLuuTruRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow)
        {
           
        }

        #region ═══════════════════════════════════════════════════════════════
        #region IPhieuLuuTruRepository
        #endregion ═══════════════════════════════════════════════════════════
        // PhieuLuuTruRepository — implement
        public DataTable LoadLuuPhieuCaNgay(string nhaMay, string ngayGiao)
        {
            return LoadData(
                @"SELECT MAHANG, GIOGIAO, SOLUONG " +
                "FROM LUUPHIEUGIAOHANG " +
                "WHERE NHAMAY = @nm AND CAST(NGAYGIAO AS DATE) = @ng",
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao));
        }
        /// <summary>
        /// Load các phiếu đã lưu trong LUUPHIEUGIAOHANG
        /// theo:
        ///     - Nhà máy
        ///     - Ngày giao
        ///     - Giờ giao FCC
        /// </summary>
        public DataTable LoadLuuPhieu(
            string nhaMay,
            string ngayGiao,
            string gioGiaoFcc)
        {
            const string sql = @"
                SELECT
                    STT,
                    CUA,
                    TRUYEN,
                    MAHANG,
                    TENHANG,
                    LOT,
                    DV,
                    SOLUONG,
                    NGAYGIAO,
                    GIOGIAO,
                    STATUS,
                    TTPHIEU,
                    NHAMAY,
                    HOP,
                    STATUSDOC,
                    Note,
                    ISNULL(PO_NO, '')   AS PO_NO,
                    ISNULL(PO_ITEM, '') AS PO_ITEM
                FROM LUUPHIEUGIAOHANG
                WHERE NHAMAY = @nm
                  AND NGAYGIAO = @ng
                  AND GIOGIAOFCC = @gg";

            return Db.LoadData(
                sql,
                new SqlParameter(
                    "@nm",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = string.IsNullOrWhiteSpace(nhaMay)
                        ? (object)DBNull.Value
                        : nhaMay.Trim()
                },

                new SqlParameter(
                    "@ng",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = string.IsNullOrWhiteSpace(ngayGiao)
                        ? (object)DBNull.Value
                        : ngayGiao.Trim()
                },

                new SqlParameter(
                    "@gg",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = string.IsNullOrWhiteSpace(gioGiaoFcc)
                        ? (object)DBNull.Value
                        : gioGiaoFcc.Trim()
                });
        }

        /// <summary>
        /// Xử lý phiếu SP đã lưu.
        ///
        /// Logic cũ:
        /// 1. Nếu LOT đang rỗng và có PO_NO
        ///    => LOT = PO_NO + '-' + PO_ITEM
        ///
        /// 2. Sau đó đếm số dòng có LOT.
        /// </summary>
        public int LuuPhieuSP(
            string nhaMay,
            string ngayGiao,
            string gioGiaoFcc,
            string loaiPhieu)
        {
            // loaiPhieu hiện chưa được sử dụng trong implementation cũ.
            // Giữ parameter để tương thích interface hiện tại.

            const string updateSql = @"
                    UPDATE LUUPHIEUGIAOHANG
                    SET LOT = ISNULL(PO_NO, '') + '-' + ISNULL(PO_ITEM, '')
                    WHERE NHAMAY = @nm
                      AND NGAYGIAO = @ng
                      AND GIOGIAOFCC = @gg
                      AND (LOT IS NULL OR LOT = '')
                      AND ISNULL(PO_NO, '') <> ''";

            Db.ExecuteNonQuery(
                updateSql,

                new SqlParameter(
                    "@nm",
                    nhaMay ?? ""),

                new SqlParameter(
                    "@ng",
                    ngayGiao ?? ""),

                new SqlParameter(
                    "@gg",
                    gioGiaoFcc ?? ""));

            const string countSql = @"
                    SELECT COUNT(*)
                    FROM LUUPHIEUGIAOHANG
                    WHERE NHAMAY = @nm
                      AND NGAYGIAO = @ng
                      AND GIOGIAOFCC = @gg
                      AND LOT IS NOT NULL
                      AND LOT <> ''";

            object raw = Db.ExecuteScalar(
                countSql,

                new SqlParameter(
                    "@nm",
                    nhaMay ?? ""),

                new SqlParameter(
                    "@ng",
                    ngayGiao ?? ""),

                new SqlParameter(
                    "@gg",
                    gioGiaoFcc ?? ""));

            return DbValueHelper.SafeInt(raw);
        }

        /// <summary>
        /// Cập nhật trạng thái TTPHIEU của phiếu đã lưu.
        ///
        /// Chỉ cho phép:
        ///     STOP => STOP
        ///     giá trị khác => ""
        /// </summary>
        public void CapNhapTTPHIEU(
            string nhaMay,
            string ngayGiao,
            string gioGiaoFcc,
            int stt,
            string ghiChu)
        {
            string safe =
                ghiChu?.Trim() == "STOP"
                    ? "STOP"
                    : "";

            const string sql = @"
            UPDATE LUUPHIEUGIAOHANG
            SET TTPHIEU = @gc
            WHERE NHAMAY = @nm
              AND NGAYGIAO = @ng
              AND GIOGIAOFCC = @gg
              AND STT = @stt";

            Db.ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@gc",
                    safe),

                new SqlParameter(
                    "@nm",
                    nhaMay ?? ""),

                new SqlParameter(
                    "@ng",
                    ngayGiao ?? ""),

                new SqlParameter(
                    "@gg",
                    gioGiaoFcc ?? ""),

                new SqlParameter(
                    "@stt",
                    stt));
        }
        public Dictionary<string, int> LoadTonKhoBatch(List<string> maHangList)
        {
            string inClause = string.Join(",",
                maHangList.Select(m => $"'{SqlHelper.Esc(m)}'"));

            DataTable tonDt = Db.LoadData(
                $"SELECT PART, ISNULL(SUM(SLCONLAI),0) AS TONG_TON " +
                $"FROM STOCKTP WHERE PART IN ({inClause}) GROUP BY PART");

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in tonDt.Rows)
                map[row["PART"].ToString().Trim()] = Convert.ToInt32(row["TONG_TON"]);
            return map;
        }
        #endregion
    }
}
