using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Repositories
{
    public sealed class HangChoGiaoRepository : SqlRepositoryBase, IHangChoGiaoRepository
    {
        public HangChoGiaoRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        // ============================================================
        // INSERT
        // ============================================================
        public int Insert(HangChoGiao item)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("Insert HangChoGiao phải chạy trong transaction.");

            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (string.IsNullOrWhiteSpace(item.MaHang))
                throw new ArgumentException("MaHang không được để trống.", nameof(item));

            if (item.SoLuong <= 0)
                throw new ArgumentOutOfRangeException(nameof(item.SoLuong), "Số lượng phải lớn hơn 0.");

            // ── Đổ đúng 1 trong 3 cột FK theo ReferenceType — 2 cột còn lại NULL ────
            int? phieuKhachTraId = null;
            int? phieuXuLyBatThuongId = null;
            string dinhDanhPhieuGiao = null;

            switch (item.ReferenceType)
            {
                case StockExportReferenceType.PhieuKhachTra:
                    phieuKhachTraId = item.ReferenceId;
                    break;

                case StockExportReferenceType.PhieuXuLyBatThuong:
                    phieuXuLyBatThuongId = item.ReferenceId;
                    break;

                case StockExportReferenceType.PhieuGiao:
                case StockExportReferenceType.ChoGiaoBu:
                    // Cả PhieuGiao và ChoGiaoBu đều dùng chung cột DinhDanhPhieuGiao,
                    // phân biệt bằng prefix do StockExportReferenceFormatter sinh ra
                    // ("PGH#..." / "CGB#..."), và được parse ngược lại trong Map().
                    dinhDanhPhieuGiao = StockExportReferenceFormatter.Format(item.ReferenceType, item.ReferenceId);
                    break;

                case null:
                    break;
            }

            object id = ExecuteScalar(
                @"INSERT INTO FVN_HangChoGiao
                (MaHang, LotThung, LotGoc, SoLuong, SlotIdNguon, LoaiYeuCauGiao,
                 PhieuKhachTraId, PhieuXuLyBatThuongId, DinhDanhPhieuGiao,
                 TrangThai, NgayXuatKho, NguoiXuatKho, Note)
              OUTPUT INSERTED.Id
              VALUES
                (@maHang, @lotThung, @lotGoc, @sl, @slotId, @loai,
                 @pkt, @pxl, @ddpg,
                 @trangThai, GETDATE(), @nguoiXuat, @note)",
                new SqlParameter("@maHang", item.MaHang),
                new SqlParameter("@lotThung", (object)item.LotThung ?? DBNull.Value),
                new SqlParameter("@lotGoc", (object)item.LotGoc ?? DBNull.Value),
                new SqlParameter("@sl", item.SoLuong),
                new SqlParameter("@slotId", (object)item.SlotIdNguon ?? DBNull.Value),
                new SqlParameter("@loai", item.LoaiYeuCauGiao.ToString()),
                new SqlParameter("@pkt", (object)phieuKhachTraId ?? DBNull.Value),
                new SqlParameter("@pxl", (object)phieuXuLyBatThuongId ?? DBNull.Value),
                new SqlParameter("@ddpg", (object)dinhDanhPhieuGiao ?? DBNull.Value),
                new SqlParameter("@trangThai", HangChoGiaoStatus.ChoGiao.ToString()),
                new SqlParameter("@nguoiXuat", (object)item.NguoiXuatKho ?? DBNull.Value),
                new SqlParameter("@note", (object)item.Note ?? DBNull.Value));

            return Convert.ToInt32(id);
        }

        // ============================================================
        // ĐỌC — KHÓA DÒNG (bắt buộc trong transaction)
        // ============================================================
        public HangChoGiao GetForUpdate(int id)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("GetForUpdate phải chạy trong transaction.");

            DataTable dt = LoadData(
                @"SELECT Id, MaHang, LotThung, LotGoc, SoLuong, SlotIdNguon, LoaiYeuCauGiao,
                     PhieuKhachTraId, PhieuXuLyBatThuongId, DinhDanhPhieuGiao,
                     TrangThai, NgayXuatKho, NguoiXuatKho, NgayGiao, NguoiGiao, Note
              FROM FVN_HangChoGiao WITH (UPDLOCK, ROWLOCK)
              WHERE Id = @id",
                new SqlParameter("@id", id));

            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        // ============================================================
        // ĐỌC — KHÔNG KHÓA (tra cứu/hiển thị UI)
        // ============================================================
        public HangChoGiao GetById(int id)
        {
            DataTable dt = LoadData(
                @"SELECT Id, MaHang, LotThung, LotGoc, SoLuong, SlotIdNguon, LoaiYeuCauGiao,
                     PhieuKhachTraId, PhieuXuLyBatThuongId, DinhDanhPhieuGiao,
                     TrangThai, NgayXuatKho, NguoiXuatKho, NgayGiao, NguoiGiao, Note
              FROM FVN_HangChoGiao
              WHERE Id = @id",
                new SqlParameter("@id", id));

            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        // ============================================================
        // CẬP NHẬT TRẠNG THÁI
        // ============================================================
        public void UpdateStatus(int id, HangChoGiaoStatus status, string nguoiGiao = null)
        {
            // Chỉ ghi NgayGiao/NguoiGiao khi status chuyển thành DaGiao — các trạng thái
            // khác (DangGiao, Huy) giữ nguyên 2 cột này (CASE WHEN giữ giá trị cũ).
            ExecuteNonQuery(
                @"UPDATE FVN_HangChoGiao
              SET TrangThai = @trangThai,
                  NgayGiao  = CASE WHEN @trangThai = @daGiaoStr THEN GETDATE()   ELSE NgayGiao  END,
                  NguoiGiao = CASE WHEN @trangThai = @daGiaoStr THEN @nguoiGiao  ELSE NguoiGiao END
              WHERE Id = @id",
                new SqlParameter("@trangThai", status.ToString()),
                new SqlParameter("@daGiaoStr", HangChoGiaoStatus.DaGiao.ToString()),
                new SqlParameter("@nguoiGiao", (object)nguoiGiao ?? DBNull.Value),
                new SqlParameter("@id", id));
        }

        // ============================================================
        // TÌM THEO CHỨNG TỪ THAM CHIẾU
        // ============================================================
        public List<HangChoGiao> GetByReference(
            StockExportReferenceType type,
            int referenceId,
            HangChoGiaoStatus? status = null)
        {
            string sql;
            var parameters = new List<SqlParameter>();

            switch (type)
            {
                case StockExportReferenceType.PhieuKhachTra:
                    sql = @"SELECT Id, MaHang, LotThung, LotGoc, SoLuong, SlotIdNguon, LoaiYeuCauGiao,
                               PhieuKhachTraId, PhieuXuLyBatThuongId, DinhDanhPhieuGiao,
                               TrangThai, NgayXuatKho, NguoiXuatKho, NgayGiao, NguoiGiao, Note
                        FROM FVN_HangChoGiao WHERE PhieuKhachTraId = @refId";
                    parameters.Add(new SqlParameter("@refId", referenceId));
                    break;

                case StockExportReferenceType.PhieuXuLyBatThuong:
                    sql = @"SELECT Id, MaHang, LotThung, LotGoc, SoLuong, SlotIdNguon, LoaiYeuCauGiao,
                               PhieuKhachTraId, PhieuXuLyBatThuongId, DinhDanhPhieuGiao,
                               TrangThai, NgayXuatKho, NguoiXuatKho, NgayGiao, NguoiGiao, Note
                        FROM FVN_HangChoGiao WHERE PhieuXuLyBatThuongId = @refId";
                    parameters.Add(new SqlParameter("@refId", referenceId));
                    break;

                case StockExportReferenceType.PhieuGiao:
                case StockExportReferenceType.ChoGiaoBu:
                    sql = @"SELECT Id, MaHang, LotThung, LotGoc, SoLuong, SlotIdNguon, LoaiYeuCauGiao,
                               PhieuKhachTraId, PhieuXuLyBatThuongId, DinhDanhPhieuGiao,
                               TrangThai, NgayXuatKho, NguoiXuatKho, NgayGiao, NguoiGiao, Note
                        FROM FVN_HangChoGiao WHERE DinhDanhPhieuGiao = @ddpg";
                    parameters.Add(new SqlParameter("@ddpg", StockExportReferenceFormatter.Format(type, referenceId)));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), $"Không hỗ trợ ReferenceType '{type}'.");
            }

            if (status.HasValue)
            {
                sql += " AND TrangThai = @trangThai";
                parameters.Add(new SqlParameter("@trangThai", status.Value.ToString()));
            }

            sql += " ORDER BY NgayXuatKho";

            DataTable dt = LoadData(sql, parameters.ToArray());
            return dt.Rows.Cast<DataRow>().Select(Map).ToList();
        }

        // ============================================================
        // MAPPING
        // ============================================================
        private static HangChoGiao Map(DataRow r)
        {
            var (refType, refId) = ParseReference(r);

            return new HangChoGiao
            {
                Id = Convert.ToInt32(r["Id"]),
                MaHang = r["MaHang"] as string,
                LotThung = r["LotThung"] as string,
                LotGoc = r["LotGoc"] as string,
                SoLuong = Convert.ToInt32(r["SoLuong"]),
                SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),

                LoaiYeuCauGiao = (HangChoGiaoLoai)Enum.Parse(
                    typeof(HangChoGiaoLoai), r["LoaiYeuCauGiao"].ToString()),

                TrangThai = (HangChoGiaoStatus)Enum.Parse(
                    typeof(HangChoGiaoStatus), r["TrangThai"].ToString()),

                ReferenceType = refType,
                ReferenceId = refId,

                NgayXuatKho = r["NgayXuatKho"] == DBNull.Value
                    ? default : Convert.ToDateTime(r["NgayXuatKho"]),

                NguoiXuatKho = r["NguoiXuatKho"] as string,

                NgayGiao = r["NgayGiao"] == DBNull.Value
                    ? (DateTime?)null : Convert.ToDateTime(r["NgayGiao"]),

                NguoiGiao = r["NguoiGiao"] as string,
                Note = r["Note"] as string
            };
        }

        /// <summary>
        /// Đọc ngược ReferenceType/ReferenceId từ 3 cột FK vật lý.
        /// Ưu tiên PhieuKhachTraId, rồi PhieuXuLyBatThuongId, cuối cùng parse
        /// DinhDanhPhieuGiao theo prefix "PGH#"/"CGB#" do StockExportReferenceFormatter sinh ra.
        /// </summary>
        private static (StockExportReferenceType? Type, int? Id) ParseReference(DataRow r)
        {
            if (r["PhieuKhachTraId"] != DBNull.Value)
                return (StockExportReferenceType.PhieuKhachTra, Convert.ToInt32(r["PhieuKhachTraId"]));

            if (r["PhieuXuLyBatThuongId"] != DBNull.Value)
                return (StockExportReferenceType.PhieuXuLyBatThuong, Convert.ToInt32(r["PhieuXuLyBatThuongId"]));

            if (r["DinhDanhPhieuGiao"] != DBNull.Value)
            {
                string raw = r["DinhDanhPhieuGiao"].ToString();
                int hashIdx = raw.IndexOf('#');

                if (hashIdx > 0 && int.TryParse(raw.Substring(hashIdx + 1), out int parsedId))
                {
                    string prefix = raw.Substring(0, hashIdx);
                    if (prefix == "PGH") return (StockExportReferenceType.PhieuGiao, parsedId);
                    if (prefix == "CGB") return (StockExportReferenceType.ChoGiaoBu, parsedId);
                }
            }

            return (null, null);
        }

        // ============================================================
        // ĐÓNG CHỜ GIAO THEO LOT (dùng transaction ngoài, VD: CapNhapKho)
        // ============================================================
        public List<HangChoGiao> CloseChoGiaoTheoLotAndReturn(
            SqlConnection conn,
            SqlTransaction tran,
            List<string> lots,
            string nguoiGiao = null)
        {
            if (conn == null)
                throw new ArgumentNullException(nameof(conn));
            if (tran == null)
                throw new ArgumentNullException(nameof(tran));

            var result = new List<HangChoGiao>();

            var distinctLots = (lots ?? new List<string>())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Distinct()
                .ToList();

            if (distinctLots.Count == 0)
                return result;

            // So khớp LOT BẮT BUỘC qua LotCodeHelper.BuildLotMatchSql (tương thích LOT
            // cũ 13 ký tự / mới 20 ký tự) — không so sánh trực tiếp bằng '=' hay IN (...).
            var matchFragments = new List<string>();
            var lotParams = new List<SqlParameter>();

            for (int i = 0; i < distinctLots.Count; i++)
            {
                string paramName = $"@lot{i}";
                matchFragments.Add(PCTP.Common.LotCodeHelper.BuildLotMatchSql("LotGoc", paramName));
                lotParams.Add(new SqlParameter(paramName, distinctLots[i]));
            }

            string sql =
                "UPDATE FVN_HangChoGiao " +
                "SET TrangThai = @trangThaiDaGiao, " +
                "    NgayGiao  = GETDATE(), " +
                "    NguoiGiao = @nguoiGiao " +
                "OUTPUT INSERTED.Id, INSERTED.MaHang, INSERTED.LotThung, INSERTED.LotGoc, " +
                "       INSERTED.SoLuong, INSERTED.SlotIdNguon, INSERTED.LoaiYeuCauGiao, " +
                "       INSERTED.PhieuKhachTraId, INSERTED.PhieuXuLyBatThuongId, INSERTED.DinhDanhPhieuGiao, " +
                "       INSERTED.TrangThai, INSERTED.NgayXuatKho, INSERTED.NguoiXuatKho, " +
                "       INSERTED.NgayGiao, INSERTED.NguoiGiao, INSERTED.Note " +
                "WHERE TrangThai = @trangThaiChoGiao " +
                $"  AND ({string.Join(" OR ", matchFragments)})";

            using (var cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add(new SqlParameter("@trangThaiChoGiao", HangChoGiaoStatus.ChoGiao.ToString()));
                cmd.Parameters.Add(new SqlParameter("@trangThaiDaGiao", HangChoGiaoStatus.DaGiao.ToString()));
                cmd.Parameters.Add(new SqlParameter("@nguoiGiao", (object)nguoiGiao ?? DBNull.Value));
                cmd.Parameters.AddRange(lotParams.ToArray());

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);

                    foreach (DataRow row in table.Rows)
                        result.Add(Map(row));
                }
            }

            return result;
        }

        // ============================================================
        // MAPPING
        // ============================================================
        //private static HangChoGiao Map(DataRow r)
        //{
        //    var (refType, refId) = ParseReference(r);

        //    return new HangChoGiao
        //    {
        //        Id = Convert.ToInt32(r["Id"]),
        //        MaHang = r["MaHang"] as string,
        //        LotThung = r["LotThung"] as string,
        //        LotGoc = r["LotGoc"] as string,
        //        SoLuong = Convert.ToInt32(r["SoLuong"]),
        //        SlotIdNguon = r["SlotIdNguon"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SlotIdNguon"]),

        //        LoaiYeuCauGiao = (HangChoGiaoLoai)Enum.Parse(
        //            typeof(HangChoGiaoLoai), r["LoaiYeuCauGiao"].ToString()),

        //        TrangThai = (HangChoGiaoStatus)Enum.Parse(
        //            typeof(HangChoGiaoStatus), r["TrangThai"].ToString()),

        //        ReferenceType = refType,
        //        ReferenceId = refId,

        //        NgayXuatKho = r["NgayXuatKho"] == DBNull.Value
        //            ? default : Convert.ToDateTime(r["NgayXuatKho"]),

        //        NguoiXuatKho = r["NguoiXuatKho"] as string,

        //        NgayGiao = r["NgayGiao"] == DBNull.Value
        //            ? (DateTime?)null : Convert.ToDateTime(r["NgayGiao"]),

        //        NguoiGiao = r["NguoiGiao"] as string,
        //        Note = r["Note"] as string
        //    };
        //}

        /// <summary>
        /// Đọc ngược ReferenceType/ReferenceId từ 3 cột FK vật lý.
        /// Ưu tiên PhieuKhachTraId, rồi PhieuXuLyBatThuongId, cuối cùng parse
        /// DinhDanhPhieuGiao theo prefix "PGH#"/"CGB#" do StockExportReferenceFormatter sinh ra.
        /// </summary>
        //private static (StockExportReferenceType? Type, int? Id) ParseReference(DataRow r)
        //{
        //    if (r["PhieuKhachTraId"] != DBNull.Value)
        //        return (StockExportReferenceType.PhieuKhachTra, Convert.ToInt32(r["PhieuKhachTraId"]));

        //    if (r["PhieuXuLyBatThuongId"] != DBNull.Value)
        //        return (StockExportReferenceType.PhieuXuLyBatThuong, Convert.ToInt32(r["PhieuXuLyBatThuongId"]));

        //    if (r["DinhDanhPhieuGiao"] != DBNull.Value)
        //    {
        //        string raw = r["DinhDanhPhieuGiao"].ToString();
        //        int hashIdx = raw.IndexOf('#');

        //        if (hashIdx > 0 && int.TryParse(raw.Substring(hashIdx + 1), out int parsedId))
        //        {
        //            string prefix = raw.Substring(0, hashIdx);
        //            if (prefix == "PGH") return (StockExportReferenceType.PhieuGiao, parsedId);
        //            if (prefix == "CGB") return (StockExportReferenceType.ChoGiaoBu, parsedId);
        //        }
        //    }

        //    return (null, null);
        //}
    }
}

