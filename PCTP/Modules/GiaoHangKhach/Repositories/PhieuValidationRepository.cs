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
    public sealed class PhieuValidationRepository
    : SqlRepositoryBase, IPhieuValidationRepository
    {
        public PhieuValidationRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }
        // ============================================================
        // FIFO
        //
        // Quy tắc nghiệp vụ:
        //   FIFO = 7 ký tự đầu của LOTNO
        //
        //   YYMMDD + CA
        //
        //   CA:
        //      0 = Hành chính
        //      1 = Ca 1
        //      2 = Ca 2
        //      3 = Ca 3
        //
        // Không sử dụng ImportDate / CreatedDate / NGAYNHAP
        // để quyết định FIFO.
        // ============================================================
        public List<FifoViolation> CheckFifoViolations(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);

            string sql = $@"
        ;WITH LotTon AS
        (
            SELECT
                sl.ItemCode,
                LEFT(sl.LotNo, 13) AS LotKey13,
                SUM(sl.Quantity) AS TongTon
            FROM SlotLot sl
            WHERE sl.PhieuStatus = 0
              AND sl.Quantity > 0
              AND LEN(sl.LotNo) >= 13
            GROUP BY
                sl.ItemCode,
                LEFT(sl.LotNo, 13)
        ),
        LotFifo AS
        (
            SELECT
                ItemCode,
                LotKey13,
                ROW_NUMBER() OVER
                (
                    PARTITION BY ItemCode
                    ORDER BY
                        LEFT(LotKey13, 6) ASC,
                        SUBSTRING(LotKey13, 12, 1) ASC,
                        LotKey13 ASC
                ) AS Rn
            FROM LotTon
            WHERE TongTon > 0
        ),
        LotDungFifo AS
        (
            SELECT
                ItemCode,
                LotKey13
            FROM LotFifo
            WHERE Rn = 1
        )
        SELECT
            tmp.MAHANG AS MaHang,
            tmp.LOT AS LotDaChon,
            fifo.LotKey13 AS LotDungRaPhaiChon
        FROM [{tmpTable}] tmp
        INNER JOIN LotDungFifo fifo
            ON RTRIM(fifo.ItemCode) = RTRIM(tmp.MAHANG)
        WHERE LEFT(tmp.LOT, 13) <> fifo.LotKey13
          AND ISNULL(tmp.STATUS, '') <> 'NG';";

            DataTable dt = LoadData(sql);

            var result = new List<FifoViolation>();

            foreach (DataRow row in dt.Rows)
            {
                result.Add(new FifoViolation
                {
                    MaHang = row["MaHang"] == DBNull.Value
                        ? null
                        : row["MaHang"].ToString(),

                    LotDaChon = row["LotDaChon"] == DBNull.Value
                        ? null
                        : row["LotDaChon"].ToString(),

                    LotDungRaPhaiChon = row["LotDungRaPhaiChon"] == DBNull.Value
                        ? null
                        : row["LotDungRaPhaiChon"].ToString()
                });
            }

            return result;
        }

        #region ═══════════════════════════════════════════════════════════════
        #region IPhieuValidationRepository
        #endregion ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Đếm số dòng hiện có trong bảng DOCQRCODE.
        /// </summary>
        public int CountDocQRCode(string docQRTable)
        {
            Db.ValidateTableName(docQRTable);

            object raw = Db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{docQRTable}]");

            return DbValueHelper.SafeInt(raw);
        }

        /// <summary>
        /// Kiểm tra hệ thống có mã NG hay không.
        ///
        /// Giá trị trả về từ dbo.ufn_QRcode_ADD_CMD_MANG():
        ///     1 hoặc 2 => có mã NG
        ///     khác     => không có
        /// </summary>
        public bool CheckCoMaNG(string tenBan)
        {
            // Giữ nguyên logic cũ.
            //
            // tenBan hiện không được sử dụng trong SQL vì function
            // dbo.ufn_QRcode_ADD_CMD_MANG() tự xác định trạng thái.
            object raw = Db.ExecuteScalar(
                "SELECT dbo.ufn_QRcode_ADD_CMD_MANG()");

            string value = raw?.ToString() ?? "0";

            return int.TryParse(value, out int result)
                   && (result == 1 || result == 2);
        }

        /// <summary>
        /// Kiểm tra mã hàng đã tồn tại trong bảng phiếu hay chưa.
        /// </summary>
        public bool KiemTraMaTrongPhieu(
            string maHang,
            string tenBan)
        {
            Db.ValidateTableName(tenBan);

            object raw = Db.ExecuteScalar(
                $"SELECT COUNT(*) " +
                $"FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma",
                new SqlParameter(
                    "@ma",
                    maHang ?? (object)DBNull.Value));

            return DbValueHelper.SafeInt(raw) > 0;
        }

        // ====================================================================
        // GetDanhSachTrungMaSl
        // ====================================================================

        /// <summary>
        /// Overload sử dụng PhieuTableSet.
        /// </summary>
        public DataTable GetDanhSachTrungMaSl(
            string maHang,
            int sl,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetDanhSachTrungMaSl(
                maHang,
                sl,
                tables.TmpTable,
                tables.DocQRTable);
        }

        /// <summary>
        /// Lấy danh sách phiếu có:
        ///
        ///     MAHANG = maHang
        ///     SOLUONG = sl
        ///     LOT chưa có
        ///     MAHANG tồn tại trong DOCQRCODE
        ///     KETQUA khác DG
        ///
        /// Đây là logic được cut nguyên từ PhieuRepository cũ.
        /// </summary>
        public DataTable GetDanhSachTrungMaSl(
            string maHang,
            int sl,
            string tenBan,
            string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            string sqlTemplate =
                "SELECT " +
                "    STT, " +
                "    MAHANG, " +
                "    TENHANG, " +
                "    GIOGIAO, " +
                "    SOLUONG, " +
                "    CASE " +
                "        WHEN STATUS IS NULL OR STATUS = '' " +
                "            THEN N'Chưa Bắn QRCODE' " +
                "        WHEN STATUS = '0' " +
                "            THEN N'Đang Bắn QRCODE' " +
                "        WHEN STATUS = '1' " +
                "            THEN N'Đã Bắn QRCODE' " +
                "        ELSE STATUS " +
                "    END AS STATUS " +
                $"FROM [{0}] " +
                "WHERE MAHANG = @ma " +
                "  AND SOLUONG = @sl " +
                "  AND (LOT = '' OR LOT IS NULL) " +
                "  AND MAHANG IN (" +
                $"      SELECT MAHANGFCC " +
                $"      FROM [{1}] " +
                "      WHERE ISNULL(KETQUA,'') <> 'DG' " +
                "      GROUP BY MAHANGFCC" +
                "  )";

            string sql = string.Format(
                sqlTemplate,
                tenBan,
                docQRTable);

            return Db.LoadData(
                sql,
                new SqlParameter("@ma", maHang ?? ""),
                new SqlParameter("@sl", sl));
        }

        // ====================================================================
        // CountTrungMaSl
        // ====================================================================

        /// <summary>
        /// Overload sử dụng PhieuTableSet.
        /// </summary>
        public int CountTrungMaSl(
            string maHang,
            int sl,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return CountTrungMaSl(
                maHang,
                sl,
                tables.TmpTable,
                tables.DocQRTable);
        }

        /// <summary>
        /// Đếm số phiếu trùng MAHANG + SOLUONG chưa có LOT
        /// và còn tồn tại trong DOCQRCODE chưa DG.
        /// </summary>
        public int CountTrungMaSl(
            string maHang,
            int sl,
            string tenBan,
            string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            string sql =
                $"SELECT COUNT(*) " +
                $"FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma " +
                $"  AND SOLUONG = @sl " +
                $"  AND (LOT = '' OR LOT IS NULL) " +
                $"  AND MAHANG IN (" +
                $"      SELECT MAHANGFCC " +
                $"      FROM [{docQRTable}] " +
                $"      WHERE KETQUA <> 'DG' " +
                $"      GROUP BY MAHANGFCC" +
                $"  )";

            object raw = Db.ExecuteScalar(
                sql,
                new SqlParameter("@ma", maHang ?? ""),
                new SqlParameter("@sl", sl));

            return DbValueHelper.SafeInt(raw);
        }

        // ====================================================================
        // GetDonHangChuaLot
        // ====================================================================

        /// <summary>
        /// Overload sử dụng PhieuTableSet.
        /// </summary>
        public DataTable GetDonHangChuaLot(
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetDonHangChuaLot(
                tables.TmpTable,
                tables.DocQRTable);
        }

        /// <summary>
        /// Lấy các dòng đơn hàng chưa có LOT
        /// và MAHANG vẫn còn trong DOCQRCODE chưa DG.
        /// </summary>
        public DataTable GetDonHangChuaLot(
            string tenBan,
            string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            string sql =
                $"SELECT " +
                $"    STT, " +
                $"    MAHANG, " +
                $"    LOT, " +
                $"    SOLUONG " +
                $"FROM [{tenBan}] " +
                $"WHERE (LOT = '' OR LOT IS NULL) " +
                $"  AND MAHANG IN (" +
                $"      SELECT MAHANGFCC " +
                $"      FROM [{docQRTable}] " +
                $"      WHERE ISNULL(KETQUA,'') <> 'DG' " +
                $"      GROUP BY MAHANGFCC" +
                $"  ) " +
                $"ORDER BY STT";

            return Db.LoadData(sql);
        }

        #endregion
        
    }
}
