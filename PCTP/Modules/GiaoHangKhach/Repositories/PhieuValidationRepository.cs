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
    public sealed class PhieuValidationRepository : IPhieuValidationRepository
    {
        private readonly PhieuSqlExecutor _db;

        public PhieuValidationRepository(PhieuSqlExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        #region ═══════════════════════════════════════════════════════════════
        #region IPhieuValidationRepository
        #endregion ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Đếm số dòng hiện có trong bảng DOCQRCODE.
        /// </summary>
        public int CountDocQRCode(string docQRTable)
        {
            _db.ValidateTableName(docQRTable);

            object raw = _db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{docQRTable}]");

            return PhieuSqlExecutor.SafeInt(raw);
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
            object raw = _db.ExecuteScalar(
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
            _db.ValidateTableName(tenBan);

            object raw = _db.ExecuteScalar(
                $"SELECT COUNT(*) " +
                $"FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma",
                new SqlParameter(
                    "@ma",
                    maHang ?? (object)DBNull.Value));

            return PhieuSqlExecutor.SafeInt(raw) > 0;
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
            _db.ValidateTableName(tenBan);
            _db.ValidateTableName(docQRTable);

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

            return _db.LoadData(
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
            _db.ValidateTableName(tenBan);
            _db.ValidateTableName(docQRTable);

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

            object raw = _db.ExecuteScalar(
                sql,
                new SqlParameter("@ma", maHang ?? ""),
                new SqlParameter("@sl", sl));

            return PhieuSqlExecutor.SafeInt(raw);
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
            _db.ValidateTableName(tenBan);
            _db.ValidateTableName(docQRTable);

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

            return _db.LoadData(sql);
        }

        #endregion
    }
}
