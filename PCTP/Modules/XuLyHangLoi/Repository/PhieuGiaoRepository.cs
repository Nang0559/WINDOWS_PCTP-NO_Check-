using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public sealed class PhieuGiaoRepository
     : SqlRepositoryBase,
       IPhieuGiaoRepository
    {
        public PhieuGiaoRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // TÌM PHIẾU GIAO THEO LOT
        // ============================================================

        /// <summary>
        /// Tìm các phiếu giao hàng có chứa LOT cần tìm.
        ///
        /// Cột LOT trong LUUPHIEUGIAOHANG có thể chứa nhiều LOT
        /// theo dạng:
        ///
        ///     LOT001-100,LOT002-50
        ///
        /// Vì vậy không thể dùng phép so sánh trực tiếp.
        /// Repository tách từng phần bằng STRING_SPLIT rồi sử dụng
        /// LotCodeHelper để xây dựng điều kiện fuzzy-match thống nhất.
        /// </summary>
        public List<PhieuGiaoUngVienInfo> TimTheoLot(
            string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return new List<PhieuGiaoUngVienInfo>();

            const string lotValueExpr =
                "LTRIM(RTRIM(LEFT(part.value, CHARINDEX('-', part.value + '-') - 1)))";

            string match =
                LotCodeHelper.BuildLotMatchSql(
                    lotValueExpr,
                    "@lot");

            string sql = $@"
SELECT DISTINCT
       g.STT,
       g.LOT,
       g.MAHANG,
       g.TENHANG,
       g.SOLUONG,
       g.NGAYGIAO,
       g.GIOGIAO,
       g.GIOGIAOFCC,
       g.NHAMAY,
       g.CUA,
       g.TRUYEN,
       ISNULL(g.PO_NO, '') AS PO_NO,
       ISNULL(g.Note, '') AS Note
FROM LUUPHIEUGIAOHANG g
CROSS APPLY STRING_SPLIT(g.LOT, ',') part
WHERE {match}
ORDER BY g.NGAYGIAO DESC;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@lot",
                    DbValueHelper.DbValue(lotNo.Trim())));

            return Map(dt);
        }


        // ============================================================
        // TÌM THEO MÃ HÀNG + NGÀY GIAO
        // ============================================================

        /// <summary>
        /// Tìm các phiếu giao theo mã hàng và ngày giao.
        ///
        /// Chỉ so sánh phần DATE của NGAYGIAO, không phụ thuộc giờ
        /// được lưu trong database.
        /// </summary>
        public List<PhieuGiaoUngVienInfo> TimTheoMaHangNgayGiao(
            string maHang,
            DateTime ngayGiao)
        {
            if (string.IsNullOrWhiteSpace(maHang))
                return new List<PhieuGiaoUngVienInfo>();

            const string sql = @"
SELECT
       STT,
       LOT,
       MAHANG,
       TENHANG,
       SOLUONG,
       NGAYGIAO,
       GIOGIAO,
       GIOGIAOFCC,
       NHAMAY,
       CUA,
       TRUYEN,
       ISNULL(PO_NO, '') AS PO_NO,
       ISNULL(Note, '') AS Note
FROM LUUPHIEUGIAOHANG
WHERE MAHANG = @ma
  AND CAST(NGAYGIAO AS DATE) = @ng
ORDER BY GIOGIAO;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@ma",
                    DbValueHelper.DbValue(maHang.Trim())),
                new SqlParameter(
                    "@ng",
                    ngayGiao.Date));

            return Map(dt);
        }


        // ============================================================
        // TÌM PHIẾU CỤ THỂ THEO DINH DANH KEY
        // ============================================================

        /// <summary>
        /// Lấy đúng một phiếu giao theo DinhDanhKey.
        ///
        /// DinhDanhKey được xây dựng từ:
        ///     Nhà máy + Ngày + Giờ FCC + PO + STT
        ///
        /// Repository không dùng chuỗi DinhDanhKey để query trực tiếp
        /// mà parse thành các thành phần rồi tìm theo khóa nghiệp vụ
        /// tương ứng trong LUUPHIEUGIAOHANG.
        /// </summary>
        public PhieuGiaoUngVienInfo GetByDinhDanhKey(
            string dinhDanhKey)
        {
            if (string.IsNullOrWhiteSpace(dinhDanhKey))
                return null;

            if (!DinhDanhKeyHelper.TryParse(
                    dinhDanhKey,
                    out var nhaMay,
                    out var ngayGiao,
                    out var gioGiaoFcc,
                    out var poNo,
                    out var stt))
            {
                return null;
            }

            if (!ngayGiao.HasValue)
                return null;

            const string sql = @"
SELECT TOP 1
       STT,
       LOT,
       MAHANG,
       TENHANG,
       SOLUONG,
       NGAYGIAO,
       GIOGIAO,
       GIOGIAOFCC,
       NHAMAY,
       CUA,
       TRUYEN,
       ISNULL(PO_NO, '') AS PO_NO,
       ISNULL(Note, '') AS Note
FROM LUUPHIEUGIAOHANG
WHERE NHAMAY = @nm
  AND CAST(NGAYGIAO AS DATE) = @ng
  AND GIOGIAOFCC = @gg
  AND ISNULL(PO_NO, '') = @po
  AND STT = @stt;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@nm",
                    DbValueHelper.DbValue(nhaMay)),
                new SqlParameter(
                    "@ng",
                    ngayGiao.Value.Date),
                new SqlParameter(
                    "@gg",
                    DbValueHelper.DbValue(gioGiaoFcc)),
                new SqlParameter(
                    "@po",
                    DbValueHelper.DbValue(poNo)),
                new SqlParameter(
                    "@stt",
                    stt));

            if (dt.Rows.Count == 0)
                return null;

            return MapRow(dt.Rows[0]);
        }


        // ============================================================
        // PHIẾU CHỜ GIAO BÙ
        // ============================================================

        /// <summary>
        /// Lấy các phiếu giao đang ở trạng thái chờ giao bù.
        ///
        /// Trạng thái nghiệp vụ được lưu trong Note theo quy ước:
        ///
        ///     CHO_GIAO_BU:...
        ///
        /// Repository chỉ đọc dữ liệu.
        /// Việc quyết định khi nào phiếu được chuyển sang trạng thái
        /// chờ giao bù thuộc Service/state machine nghiệp vụ.
        /// </summary>
        public List<PhieuGiaoUngVienInfo> GetPhieuChoGiaoBu(
            string maHang)
        {
            if (string.IsNullOrWhiteSpace(maHang))
                return new List<PhieuGiaoUngVienInfo>();

            const string sql = @"
SELECT
       STT,
       LOT,
       MAHANG,
       TENHANG,
       SOLUONG,
       NGAYGIAO,
       GIOGIAO,
       GIOGIAOFCC,
       NHAMAY,
       CUA,
       TRUYEN,
       ISNULL(PO_NO, '') AS PO_NO,
       ISNULL(Note, '') AS Note
FROM LUUPHIEUGIAOHANG
WHERE MAHANG = @ma
  AND Note LIKE 'CHO_GIAO_BU:%'
ORDER BY NGAYGIAO DESC;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@ma",
                    DbValueHelper.DbValue(maHang.Trim())));

            return Map(dt);
        }


        // ============================================================
        // CẬP NHẬT NOTE PHIẾU GIAO
        // ============================================================

        /// <summary>
        /// Cập nhật Note của một phiếu giao.
        ///
        /// Phiếu được xác định bằng DinhDanhKey.
        ///
        /// DinhDanhKey không được lưu thành một cột riêng mà được
        /// tạo từ các trường nghiệp vụ của LUUPHIEUGIAOHANG.
        ///
        /// Method này dùng ExecuteNonQuery() của SqlRepositoryBase
        /// nên tự động tham gia transaction nếu Service đang mở
        /// IUnitOfWork.
        /// </summary>
        public void CapNhatNotePhieuGiao(
            string dinhDanhKey,
            string note)
        {
            if (string.IsNullOrWhiteSpace(dinhDanhKey))
            {
                throw new ArgumentException(
                    "DinhDanhKey không được rỗng.",
                    nameof(dinhDanhKey));
            }

            if (!DinhDanhKeyHelper.TryParse(
                    dinhDanhKey,
                    out var nhaMay,
                    out var ngayGiao,
                    out var gioGiaoFcc,
                    out var poNo,
                    out var stt))
            {
                throw new ArgumentException(
                    $"DinhDanhKey không hợp lệ: '{dinhDanhKey}'.",
                    nameof(dinhDanhKey));
            }

            if (!ngayGiao.HasValue)
            {
                throw new ArgumentException(
                    $"DinhDanhKey không chứa ngày giao hợp lệ: '{dinhDanhKey}'.",
                    nameof(dinhDanhKey));
            }

            const string sql = @"
UPDATE LUUPHIEUGIAOHANG
SET Note = @note
WHERE NHAMAY = @nm
  AND CAST(NGAYGIAO AS DATE) = @ng
  AND GIOGIAOFCC = @gg
  AND ISNULL(PO_NO, '') = @po
  AND STT = @stt;";

            ExecuteNonQuery(
                sql,
                new SqlParameter(
                    "@note",
                    DbValueHelper.DbValue(note)),
                new SqlParameter(
                    "@nm",
                    DbValueHelper.DbValue(nhaMay)),
                new SqlParameter(
                    "@ng",
                    ngayGiao.Value.Date),
                new SqlParameter(
                    "@gg",
                    DbValueHelper.DbValue(gioGiaoFcc)),
                new SqlParameter(
                    "@po",
                    DbValueHelper.DbValue(poNo)),
                new SqlParameter(
                    "@stt",
                    stt));
        }


        // ============================================================
        // MAPPING
        // ============================================================

        /// <summary>
        /// Map toàn bộ DataTable sang danh sách domain model.
        /// </summary>
        private static List<PhieuGiaoUngVienInfo> Map(
            DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
                return new List<PhieuGiaoUngVienInfo>();

            return dt.Rows
                .Cast<DataRow>()
                .Select(MapRow)
                .ToList();
        }


        /// <summary>
        /// Map một DataRow thành PhieuGiaoUngVienInfo.
        ///
        /// Việc đọc dữ liệu NULL được tập trung qua DbValueHelper
        /// để repository không lặp lại các đoạn:
        ///
        ///     == DBNull.Value
        ///     as string
        ///     Convert.ToInt32(...)
        /// </summary>
        private static PhieuGiaoUngVienInfo MapRow(
            DataRow row)
        {
            if (row == null)
                return null;

            short stt = Convert.ToInt16(
                DbValueHelper.ToInt(
                    row["STT"]));

            string nhaMay =
                DbValueHelper.ToString(
                    row["NHAMAY"]);

            DateTime? ngayGiao =
                DbValueHelper.ToDateTime(
                    row["NGAYGIAO"]);

            string gioGiaoFcc =
                DbValueHelper.ToString(
                    row["GIOGIAOFCC"]);

            string poNo =
                DbValueHelper.ToString(
                    row["PO_NO"]);

            return new PhieuGiaoUngVienInfo
            {
                STT = stt,

                DinhDanhKey =
                    DinhDanhKeyHelper.Build(
                        nhaMay,
                        ngayGiao,
                        gioGiaoFcc,
                        poNo,
                        stt),

                LOT =
                    DbValueHelper.ToString(
                        row["LOT"]),

                MAHANG =
                    DbValueHelper.ToString(
                        row["MAHANG"]),

                TENHANG =
                    DbValueHelper.ToString(
                        row["TENHANG"]),

                SOLUONG =
                    DbValueHelper.ToInt(
                        row["SOLUONG"]),

                NGAYGIAO = ngayGiao,

                GIOGIAO =
                    DbValueHelper.ToString(
                        row["GIOGIAO"]),

                GIOGIAOFCC = gioGiaoFcc,

                NHAMAY = nhaMay,

                CUA =
                    DbValueHelper.ToString(
                        row["CUA"]),

                TRUYEN =
                    DbValueHelper.ToString(
                        row["TRUYEN"]),

                PO_NO = poNo,

                Note =
                    DbValueHelper.ToString(
                        row["Note"])
            };
        }
    }
}
