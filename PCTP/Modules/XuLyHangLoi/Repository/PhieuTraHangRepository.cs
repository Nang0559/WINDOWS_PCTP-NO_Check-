using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Enums;
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

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;

    public class PhieuTraHangRepository
        : SqlRepositoryBase,
          IPhieuTraHangRepository
    {
        private readonly PhieuSqlExecutor _sql;


        public PhieuTraHangRepository(
            PhieuSqlExecutor sql,
            IUnitOfWork unitOfWork)
            : base(sql, unitOfWork)
        {
            if (sql == null)
                throw new ArgumentNullException(nameof(sql));

            if (unitOfWork == null)
                throw new ArgumentNullException(nameof(unitOfWork));

            _sql = sql;
        }

        public PhieuTraHangStatus? GetStatus(int id)
        => GetStatus<PhieuTraHangStatus, int>("FVN_PhieuTraHang", id);

        public bool UpdateStatusIfCurrentIs(int id, PhieuTraHangStatus expectedFrom, PhieuTraHangStatus newStatus, string nguoiThucHien)
            => UpdateStatusIfCurrentIs("FVN_PhieuTraHang", id, expectedFrom, newStatus, nguoiThucHien);
        // ============================================================
        // HEADER
        // ============================================================

        public int Insert(PhieuTraHang e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            const string sql = @"
INSERT INTO FVN_PhieuTraHang
(
    Nguon,
    SoPhieu,
    NguonKhachTra,
    SoPhieuKhach,
    NgayPhatHanh,
    SlipNo,
    Ca,
    PhongBan,
    LyDo,
    TenKhachHang,
    BoPhanPhatHienLoi,
    XacNhanBPPhatHienLoi,
    XacNhanQCKhach,
    XacNhanNhaCungCap,
    NgayNhanKho,
    TongSoLuongNhan,
    Status,
    BoPhanNhanLai,
    SoLuongGiaoLai,
    NgayGiaoLaiBoPhan,
    NguoiGiaoLaiBoPhan,
    Note,
    CreatedAt,
    CreatedBy
)
OUTPUT INSERTED.Id
VALUES
(
    @Nguon,
    @SoPhieu,
    @NguonKhachTra,
    @SoPhieuKhach,
    @NgayPhatHanh,
    @SlipNo,
    @Ca,
    @PhongBan,
    @LyDo,
    @TenKhachHang,
    @BoPhanPhatHienLoi,
    @XacNhanBPPhatHienLoi,
    @XacNhanQCKhach,
    @XacNhanNhaCungCap,
    @NgayNhanKho,
    @TongSoLuongNhan,
    @Status,
    @BoPhanNhanLai,
    @SoLuongGiaoLai,
    @NgayGiaoLaiBoPhan,
    @NguoiGiaoLaiBoPhan,
    @Note,
    GETDATE(),
    @CreatedBy
);";

            object id = _sql.ExecuteScalar(
                sql,

                new SqlParameter(
                    "@Nguon",
                    (int)e.Nguon),

                new SqlParameter(
                    "@SoPhieu",
                    DbValueHelper.DbValue(e.SoPhieu)),

                new SqlParameter(
                    "@NguonKhachTra",
                    e.NguonKhachTra.HasValue
                        ? (object)(int)e.NguonKhachTra.Value
                        : DBNull.Value),

                new SqlParameter(
                    "@SoPhieuKhach",
                    DbValueHelper.DbValue(e.SoPhieuKhach)),

                new SqlParameter(
                    "@NgayPhatHanh",
                    DbValueHelper.DbValue(e.NgayPhatHanh)),

                new SqlParameter(
                    "@SlipNo",
                    DbValueHelper.DbValue(e.SlipNo)),

                new SqlParameter(
                    "@Ca",
                    DbValueHelper.DbValue(e.Ca)),

                new SqlParameter(
                    "@PhongBan",
                    DbValueHelper.DbValue(e.PhongBan)),

                new SqlParameter(
                    "@LyDo",
                    DbValueHelper.DbValue(e.LyDo)),

                new SqlParameter(
                    "@TenKhachHang",
                    DbValueHelper.DbValue(e.TenKhachHang)),

                new SqlParameter(
                    "@BoPhanPhatHienLoi",
                    DbValueHelper.DbValue(e.BoPhanPhatHienLoi)),

                new SqlParameter(
                    "@XacNhanBPPhatHienLoi",
                    DbValueHelper.DbValue(e.XacNhanBPPhatHienLoi)),

                new SqlParameter(
                    "@XacNhanQCKhach",
                    DbValueHelper.DbValue(e.XacNhanQCKhach)),

                new SqlParameter(
                    "@XacNhanNhaCungCap",
                    DbValueHelper.DbValue(e.XacNhanNhaCungCap)),

                new SqlParameter(
                    "@NgayNhanKho",
                    DbValueHelper.DbValue(e.NgayNhanKho)),

                new SqlParameter(
                    "@TongSoLuongNhan",
                    e.TongSoLuongNhan),

                new SqlParameter(
                    "@Status",
                    (int)e.Status),

                new SqlParameter(
                    "@BoPhanNhanLai",
                    DbValueHelper.DbValue(e.BoPhanNhanLai)),

                new SqlParameter(
                    "@SoLuongGiaoLai",
                    DbValueHelper.DbValue(e.SoLuongGiaoLai)),

                new SqlParameter(
                    "@NgayGiaoLaiBoPhan",
                    DbValueHelper.DbValue(e.NgayGiaoLaiBoPhan)),

                new SqlParameter(
                    "@NguoiGiaoLaiBoPhan",
                    DbValueHelper.DbValue(e.NguoiGiaoLaiBoPhan)),

                new SqlParameter(
                    "@Note",
                    DbValueHelper.DbValue(e.Note)),

                new SqlParameter(
                    "@CreatedBy",
                    DbValueHelper.DbValue(e.CreatedBy)));

            return DbValueHelper.ToInt(id);
        }


        public PhieuTraHang GetById(int id)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHang
WHERE Id = @Id;";

            DataTable table = _sql.LoadData(
                sql,
                new SqlParameter("@Id", id));

            if (table.Rows.Count == 0)
                return null;

            return MapHeader(table.Rows[0]);
        }


        public PhieuTraHang GetBySoPhieu(string soPhieu)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHang
WHERE SoPhieu = @SoPhieu;";

            DataTable table = _sql.LoadData(
                sql,
                new SqlParameter(
                    "@SoPhieu",
                    DbValueHelper.DbValue(soPhieu)));

            if (table.Rows.Count == 0)
                return null;

            return MapHeader(table.Rows[0]);
        }


        public List<PhieuTraHang> GetByNguon(
            NguonXuLyBatThuong nguon)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHang
WHERE Nguon = @Nguon
ORDER BY CreatedAt DESC, Id DESC;";

            DataTable table = _sql.LoadData(
                sql,
                new SqlParameter(
                    "@Nguon",
                    (int)nguon));

            var result = new List<PhieuTraHang>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapHeader(row));
            }

            return result;
        }


        public List<PhieuTraHang> GetChoXuLy()
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHang
WHERE Status <> @HoanTat
ORDER BY CreatedAt DESC, Id DESC;";

            DataTable table = _sql.LoadData(
                sql,
                new SqlParameter(
                    "@HoanTat",
                    (int)PhieuTraHangStatus.HoanTat));

            var result = new List<PhieuTraHang>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapHeader(row));
            }

            return result;
        }


        public List<PhieuTraHang> GetChoXuLyByNguon(
            NguonXuLyBatThuong nguon)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHang
WHERE Nguon = @Nguon
  AND Status <> @HoanTat
ORDER BY CreatedAt DESC, Id DESC;";

            DataTable table = _sql.LoadData(
                sql,
                new SqlParameter(
                    "@Nguon",
                    (int)nguon),

                new SqlParameter(
                    "@HoanTat",
                    (int)PhieuTraHangStatus.HoanTat));

            var result = new List<PhieuTraHang>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapHeader(row));
            }

            return result;
        }


        public void Update(PhieuTraHang e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            const string sql = @"
UPDATE FVN_PhieuTraHang
SET
    NguonKhachTra = @NguonKhachTra,
    SoPhieuKhach = @SoPhieuKhach,
    NgayPhatHanh = @NgayPhatHanh,
    SlipNo = @SlipNo,
    Ca = @Ca,
    PhongBan = @PhongBan,
    LyDo = @LyDo,
    TenKhachHang = @TenKhachHang,
    BoPhanPhatHienLoi = @BoPhanPhatHienLoi,
    XacNhanBPPhatHienLoi = @XacNhanBPPhatHienLoi,
    XacNhanQCKhach = @XacNhanQCKhach,
    XacNhanNhaCungCap = @XacNhanNhaCungCap,
    NgayNhanKho = @NgayNhanKho,
    TongSoLuongNhan = @TongSoLuongNhan,
    BoPhanNhanLai = @BoPhanNhanLai,
    SoLuongGiaoLai = @SoLuongGiaoLai,
    NgayGiaoLaiBoPhan = @NgayGiaoLaiBoPhan,
    NguoiGiaoLaiBoPhan = @NguoiGiaoLaiBoPhan,
    Note = @Note,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;";

            _sql.ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Id",
                    e.Id),

                new SqlParameter(
                    "@NguonKhachTra",
                    e.NguonKhachTra.HasValue
                        ? (object)(int)e.NguonKhachTra.Value
                        : DBNull.Value),

                new SqlParameter(
                    "@SoPhieuKhach",
                    DbValueHelper.DbValue(e.SoPhieuKhach)),

                new SqlParameter(
                    "@NgayPhatHanh",
                    DbValueHelper.DbValue(e.NgayPhatHanh)),

                new SqlParameter(
                    "@SlipNo",
                    DbValueHelper.DbValue(e.SlipNo)),

                new SqlParameter(
                    "@Ca",
                    DbValueHelper.DbValue(e.Ca)),

                new SqlParameter(
                    "@PhongBan",
                    DbValueHelper.DbValue(e.PhongBan)),

                new SqlParameter(
                    "@LyDo",
                    DbValueHelper.DbValue(e.LyDo)),

                new SqlParameter(
                    "@TenKhachHang",
                    DbValueHelper.DbValue(e.TenKhachHang)),

                new SqlParameter(
                    "@BoPhanPhatHienLoi",
                    DbValueHelper.DbValue(e.BoPhanPhatHienLoi)),

                new SqlParameter(
                    "@XacNhanBPPhatHienLoi",
                    DbValueHelper.DbValue(e.XacNhanBPPhatHienLoi)),

                new SqlParameter(
                    "@XacNhanQCKhach",
                    DbValueHelper.DbValue(e.XacNhanQCKhach)),

                new SqlParameter(
                    "@XacNhanNhaCungCap",
                    DbValueHelper.DbValue(e.XacNhanNhaCungCap)),

                new SqlParameter(
                    "@NgayNhanKho",
                    DbValueHelper.DbValue(e.NgayNhanKho)),

                new SqlParameter(
                    "@TongSoLuongNhan",
                    e.TongSoLuongNhan),

                new SqlParameter(
                    "@BoPhanNhanLai",
                    DbValueHelper.DbValue(e.BoPhanNhanLai)),

                new SqlParameter(
                    "@SoLuongGiaoLai",
                    DbValueHelper.DbValue(e.SoLuongGiaoLai)),

                new SqlParameter(
                    "@NgayGiaoLaiBoPhan",
                    DbValueHelper.DbValue(e.NgayGiaoLaiBoPhan)),

                new SqlParameter(
                    "@NguoiGiaoLaiBoPhan",
                    DbValueHelper.DbValue(e.NguoiGiaoLaiBoPhan)),

                new SqlParameter(
                    "@Note",
                    DbValueHelper.DbValue(e.Note)),

                new SqlParameter(
                    "@UpdatedBy",
                    DbValueHelper.DbValue(e.UpdatedBy)));
        }


        // ============================================================
        // STATUS
        // ============================================================

        public void UpdateStatus(
            int id,
            PhieuTraHangStatus status,
            string nguoiThucHien)
        {
            const string sql = @"
UPDATE FVN_PhieuTraHang
SET
    Status = @Status,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;";

            int affected = _sql.ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Id",
                    id),

                new SqlParameter(
                    "@Status",
                    (int)status),

                new SqlParameter(
                    "@UpdatedBy",
                    DbValueHelper.DbValue(nguoiThucHien)));

            if (affected == 0)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy PhieuTraHang để cập nhật Status. Id="
                    + id);
            }
        }


        public void UpdateNote(
            int id,
            string note,
            string nguoiThucHien)
        {
            const string sql = @"
UPDATE FVN_PhieuTraHang
SET
    Note = @Note,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;";

            _sql.ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Id",
                    id),

                new SqlParameter(
                    "@Note",
                    DbValueHelper.DbValue(note)),

                new SqlParameter(
                    "@UpdatedBy",
                    DbValueHelper.DbValue(nguoiThucHien)));
        }


        // ============================================================
        // DETAIL
        // ============================================================

        public int InsertItem(
            PhieuTraHangCT item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            const string sql = @"
INSERT INTO FVN_PhieuTraHangCT
(
    PhieuTraHangId,
    SlotIdNguon,
    MaHang,
    TenHang,
    LotNo,
    SoLuong,
    LyDoNg,
    DinhDanhPhieuGiao,
    PoNo,
    NgayGiao,
    NhaMay
)
OUTPUT INSERTED.Id
VALUES
(
    @PhieuTraHangId,
    @SlotIdNguon,
    @MaHang,
    @TenHang,
    @LotNo,
    @SoLuong,
    @LyDoNg,
    @DinhDanhPhieuGiao,
    @PoNo,
    @NgayGiao,
    @NhaMay
);";

            object id = _sql.ExecuteScalar(
                sql,

                new SqlParameter(
                    "@PhieuTraHangId",
                    item.PhieuTraHangId),

                new SqlParameter(
                    "@SlotIdNguon",
                    DbValueHelper.DbValue(item.SlotIdNguon)),

                new SqlParameter(
                    "@MaHang",
                    DbValueHelper.DbValue(item.MaHang)),

                new SqlParameter(
                    "@TenHang",
                    DbValueHelper.DbValue(item.TenHang)),

                new SqlParameter(
                    "@LotNo",
                    DbValueHelper.DbValue(item.LotNo)),

                new SqlParameter(
                    "@SoLuong",
                    item.SoLuong),

                new SqlParameter(
                    "@LyDoNg",
                    DbValueHelper.DbValue(item.LyDoNg)),

                new SqlParameter(
                    "@DinhDanhPhieuGiao",
                    DbValueHelper.DbValue(item.DinhDanhPhieuGiao)),

                new SqlParameter(
                    "@PoNo",
                    DbValueHelper.DbValue(item.PoNo)),

                new SqlParameter(
                    "@NgayGiao",
                    DbValueHelper.DbValue(item.NgayGiao)),

                new SqlParameter(
                    "@NhaMay",
                    DbValueHelper.DbValue(item.NhaMay)));

            return DbValueHelper.ToInt(id);
        }


        public void InsertItems(
            int phieuTraHangId,
            IEnumerable<PhieuTraHangCT> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            foreach (PhieuTraHangCT item in items)
            {
                if (item == null)
                    continue;

                item.PhieuTraHangId = phieuTraHangId;
                InsertItem(item);
            }
        }


        public List<PhieuTraHangCT> GetItems(
            int phieuTraHangId)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHangCT
WHERE PhieuTraHangId = @PhieuTraHangId
ORDER BY Id;";

            DataTable table = _sql.LoadData(
                sql,

                new SqlParameter(
                    "@PhieuTraHangId",
                    phieuTraHangId));

            var result = new List<PhieuTraHangCT>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapItem(row));
            }

            return result;
        }


        public PhieuTraHangCT GetItemById(
            int itemId)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHangCT
WHERE Id = @Id;";

            DataTable table = _sql.LoadData(
                sql,
                new SqlParameter("@Id", itemId));

            if (table.Rows.Count == 0)
                return null;

            return MapItem(table.Rows[0]);
        }


        // ============================================================
        // TRA NOI BO - GIAO LAI BO PHAN
        // ============================================================

        public void UpdateThongTinGiaoLaiBoPhan(
            int phieuTraHangId,
            string boPhanNhan,
            int soLuongGiaoLai,
            DateTime ngayGiaoLai,
            string nguoiThucHien)
        {
            const string sql = @"
UPDATE FVN_PhieuTraHang
SET
    BoPhanNhanLai = @BoPhanNhanLai,
    SoLuongGiaoLai = @SoLuongGiaoLai,
    NgayGiaoLaiBoPhan = @NgayGiaoLaiBoPhan,
    NguoiGiaoLaiBoPhan = @NguoiGiaoLaiBoPhan,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;";

            int affected = _sql.ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Id",
                    phieuTraHangId),

                new SqlParameter(
                    "@BoPhanNhanLai",
                    DbValueHelper.DbValue(boPhanNhan)),

                new SqlParameter(
                    "@SoLuongGiaoLai",
                    soLuongGiaoLai),

                new SqlParameter(
                    "@NgayGiaoLaiBoPhan",
                    ngayGiaoLai),

                new SqlParameter(
                    "@NguoiGiaoLaiBoPhan",
                    DbValueHelper.DbValue(nguoiThucHien)),

                new SqlParameter(
                    "@UpdatedBy",
                    DbValueHelper.DbValue(nguoiThucHien)));

            if (affected == 0)
            {
                throw new InvalidOperationException(
                    "Không tìm thấy PhieuTraHang để cập nhật thông tin giao lại. Id="
                    + phieuTraHangId);
            }
        }


        // ============================================================
        // DOI CHIEU PHIEU GIAO
        // ============================================================

        public List<PhieuTraHangCT> GetItemsChuaXacDinhPhieuGiao(
            int phieuTraHangId)
        {
            const string sql = @"
SELECT *
FROM FVN_PhieuTraHangCT
WHERE PhieuTraHangId = @PhieuTraHangId
  AND
  (
      DinhDanhPhieuGiao IS NULL
      OR LTRIM(RTRIM(DinhDanhPhieuGiao)) = ''
  )
ORDER BY Id;";

            DataTable table = _sql.LoadData(
                sql,

                new SqlParameter(
                    "@PhieuTraHangId",
                    phieuTraHangId));

            var result = new List<PhieuTraHangCT>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapItem(row));
            }

            return result;
        }


        public void UpdateItemDinhDanhPhieuGiao(
            int itemId,
            string dinhDanhPhieuGiao,
            string poNo,
            DateTime? ngayGiao,
            string nhaMay)
        {
            const string sql = @"
UPDATE FVN_PhieuTraHangCT
SET
    DinhDanhPhieuGiao = @DinhDanhPhieuGiao,
    PoNo = @PoNo,
    NgayGiao = @NgayGiao,
    NhaMay = @NhaMay
WHERE Id = @Id;";

            _sql.ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Id",
                    itemId),

                new SqlParameter(
                    "@DinhDanhPhieuGiao",
                    DbValueHelper.DbValue(dinhDanhPhieuGiao)),

                new SqlParameter(
                    "@PoNo",
                    DbValueHelper.DbValue(poNo)),

                new SqlParameter(
                    "@NgayGiao",
                    DbValueHelper.DbValue(ngayGiao)),

                new SqlParameter(
                    "@NhaMay",
                    DbValueHelper.DbValue(nhaMay)));
        }


        // ============================================================
        // KIEM TRA CON PHIEU XU LY
        // ============================================================

        public bool ConChoXuLy(
            int phieuTraHangId)
        {
            const string sql = @"
SELECT TOP 1 1
FROM FVN_PhieuXuLyBatThuong
WHERE PhieuTraHangId = @PhieuTraHangId
  AND Status NOT IN
  (
      @HoanTat,
      @Huy
  );";

            object value = _sql.ExecuteScalar(
                sql,

                new SqlParameter(
                    "@PhieuTraHangId",
                    phieuTraHangId),

                new SqlParameter(
                    "@HoanTat",
                    (int)QTChungStatus.HoanTat),

                new SqlParameter(
                    "@Huy",
                    (int)QTChungStatus.Huy));

            return value != null &&
                   value != DBNull.Value;
        }


        // ============================================================
        // MAPPING - HEADER
        // ============================================================

        private PhieuTraHang MapHeader(
            DataRow row)
        {
            return new PhieuTraHang
            {
                Id = DbValueHelper.GetInt(
                    row,
                    "Id"),

                Nguon = DbValueHelper.GetEnum<NguonXuLyBatThuong>(
                    row,
                    "Nguon"),

                SoPhieu = DbValueHelper.GetString(
                    row,
                    "SoPhieu"),

                NguonKhachTra =
                    DbValueHelper.GetNullableEnum<NguonKhachTra>(
                        row,
                        "NguonKhachTra"),

                SoPhieuKhach = DbValueHelper.GetString(
                    row,
                    "SoPhieuKhach"),

                NgayPhatHanh =
                    DbValueHelper.GetNullableDateTime(
                        row,
                        "NgayPhatHanh"),

                SlipNo = DbValueHelper.GetString(
                    row,
                    "SlipNo"),

                Ca = DbValueHelper.GetString(
                    row,
                    "Ca"),

                PhongBan = DbValueHelper.GetString(
                    row,
                    "PhongBan"),

                LyDo = DbValueHelper.GetString(
                    row,
                    "LyDo"),

                TenKhachHang = DbValueHelper.GetString(
                    row,
                    "TenKhachHang"),

                BoPhanPhatHienLoi =
                    DbValueHelper.GetString(
                        row,
                        "BoPhanPhatHienLoi"),

                XacNhanBPPhatHienLoi =
                    DbValueHelper.GetString(
                        row,
                        "XacNhanBPPhatHienLoi"),

                XacNhanQCKhach =
                    DbValueHelper.GetString(
                        row,
                        "XacNhanQCKhach"),

                XacNhanNhaCungCap =
                    DbValueHelper.GetString(
                        row,
                        "XacNhanNhaCungCap"),

                NgayNhanKho =
                    DbValueHelper.GetNullableDateTime(
                        row,
                        "NgayNhanKho"),

                TongSoLuongNhan =
                    DbValueHelper.GetInt(
                        row,
                        "TongSoLuongNhan"),

                Status =
                    DbValueHelper.GetEnum<PhieuTraHangStatus>(
                        row,
                        "Status"),

                BoPhanNhanLai =
                    DbValueHelper.GetString(
                        row,
                        "BoPhanNhanLai"),

                SoLuongGiaoLai =
                    DbValueHelper.GetNullableInt(
                        row,
                        "SoLuongGiaoLai"),

                NgayGiaoLaiBoPhan =
                    DbValueHelper.GetNullableDateTime(
                        row,
                        "NgayGiaoLaiBoPhan"),

                NguoiGiaoLaiBoPhan =
                    DbValueHelper.GetString(
                        row,
                        "NguoiGiaoLaiBoPhan"),

                Note =
                    DbValueHelper.GetString(
                        row,
                        "Note"),

                CreatedAt =
                    DbValueHelper.GetDateTime(
                        row,
                        "CreatedAt"),

                CreatedBy =
                    DbValueHelper.GetString(
                        row,
                        "CreatedBy"),

                UpdatedAt =
                    DbValueHelper.GetNullableDateTime(
                        row,
                        "UpdatedAt"),

                UpdatedBy =
                    DbValueHelper.GetString(
                        row,
                        "UpdatedBy")
            };
        }


        // ============================================================
        // MAPPING - DETAIL
        // ============================================================

        private PhieuTraHangCT MapItem(
            DataRow row)
        {
            return new PhieuTraHangCT
            {
                Id = DbValueHelper.GetInt(
                    row,
                    "Id"),

                PhieuTraHangId =
                    DbValueHelper.GetInt(
                        row,
                        "PhieuTraHangId"),

                SlotIdNguon =
                    DbValueHelper.GetNullableInt(
                        row,
                        "SlotIdNguon"),

                MaHang =
                    DbValueHelper.GetString(
                        row,
                        "MaHang"),

                TenHang =
                    DbValueHelper.GetString(
                        row,
                        "TenHang"),

                LotNo =
                    DbValueHelper.GetString(
                        row,
                        "LotNo"),

                SoLuong =
                    DbValueHelper.GetInt(
                        row,
                        "SoLuong"),

                LyDoNg =
                    DbValueHelper.GetString(
                        row,
                        "LyDoNg"),

                DinhDanhPhieuGiao =
                    DbValueHelper.GetString(
                        row,
                        "DinhDanhPhieuGiao"),

                PoNo =
                    DbValueHelper.GetString(
                        row,
                        "PoNo"),

                NgayGiao =
                    DbValueHelper.GetNullableDateTime(
                        row,
                        "NgayGiao"),

                NhaMay =
                    DbValueHelper.GetString(
                        row,
                        "NhaMay")
            };
        }
    }
}
