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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace PCTP.Modules.XuLyHangLoi.Repository
{

    public class PhieuXuLyBatThuongRepository
        : SqlRepositoryBase, IPhieuXuLyBatThuongRepository
    {
        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public PhieuXuLyBatThuongRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public QTChungStatus? GetStatus(int id)
        => GetStatus<QTChungStatus, int>("FVN_PhieuXuLyBatThuong", id);

        public bool UpdateStatusIfCurrentIs(int id, QTChungStatus expectedFrom, QTChungStatus newStatus, string nguoiThucHien)
            => UpdateStatusIfCurrentIs("FVN_PhieuXuLyBatThuong", id, expectedFrom, newStatus, nguoiThucHien);
        // ============================================================
        // TẠO PHIẾU
        // ============================================================

        public int Insert(
            int phieuTraHangCTId,
            PhieuXuLyBatThuong p)
        {
            // Legacy 1-1: một dòng phiếu trả -> một phiếu, kèm đúng một dòng detail.
            return InTransaction(() =>
            {
                if (p == null)
                    throw new ArgumentNullException(nameof(p));
                if (phieuTraHangCTId <= 0)
                    throw new ArgumentOutOfRangeException(nameof(phieuTraHangCTId));

                var items = new List<PhieuXuLyBatThuongCT>
                {
                    new PhieuXuLyBatThuongCT
                    {
                        PhieuTraHangCTId = phieuTraHangCTId,
                        SlotIdNguon = p.SlotIdNguon,
                        LotNguon = p.LotNguon ?? p.SoLoLoi,
                        MaSanPham = p.MaSanPham,
                        SoLuongLoi = p.SoLuongLoi
                    }
                };
                return InsertCore(p, items, true);
            });
        }

        public int InsertWithItems(
            PhieuXuLyBatThuong p,
            IList<PhieuXuLyBatThuongCT> items)
        {
            // INSERT header + INSERT detail + UPDATE FVN_PhieuTraHangCT phải atomic.
            return InTransaction(() => InsertCore(p, items, false));
        }

        private int InsertCore(
            PhieuXuLyBatThuong p,
            IList<PhieuXuLyBatThuongCT> items,
            bool legacySingleLine)
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            if (items == null || items.Count == 0)
                throw new InvalidOperationException(
                    "Phiếu xử lý bất thường phải có ít nhất một dòng chi tiết (Slot/LOT).");

            ValidateForInsert(p, legacySingleLine);

            // Cột legacy PhieuTraHangCTId của header: chỉ có giá trị với phiếu 1-1.
            int phieuTraHangCTId = legacySingleLine ? items[0].PhieuTraHangCTId : 0;

            const string sql = @"
INSERT INTO FVN_PhieuXuLyBatThuong
(
    SoPhieu,
    Nguon,
    PhieuTraHangCTId,
    PhieuTraHangId,
    SoPhieuTraHangGoc,
    PhieuKhachTraId,
    SlotIdNguon,
    LotNguon,
    Model,
    MaSanPham,
    SoLo,
    SoLoLoi,
    SoLuongLoi,
    NoiDungBatThuong,
    PhanLoaiXuLy,
    BoPhanPhatHanh,
    Status,
    HuongXuLy,
    NgayDinhHuong,
    NguoiDinhHuong,
    CreatedAt,
    CreatedBy
)
OUTPUT INSERTED.Id
VALUES
(
    @SoPhieu,
    @Nguon,
    @PhieuTraHangCTId,
    @PhieuTraHangId,
    @SoPhieuTraHangGoc,
    @PhieuKhachTraId,
    @SlotIdNguon,
    @LotNguon,
    @Model,
    @MaSanPham,
    @SoLo,
    @SoLoLoi,
    @SoLuongLoi,
    @NoiDungBatThuong,
    @PhanLoaiXuLy,
    @BoPhanPhatHanh,
    @Status,
    @HuongXuLy,
    @NgayDinhHuong,
    @NguoiDinhHuong,
    GETDATE(),
    @CreatedBy
);";

            int phieuXuLyId = Convert.ToInt32(
                ExecuteScalar(
                    sql,

                    new SqlParameter(
                        "@SoPhieu",
                        DbValueHelper.DbValue(p.SoPhieu)),

                    new SqlParameter(
                        "@Nguon",
                        (int)p.Nguon),

                    new SqlParameter(
                        "@PhieuTraHangCTId",
                        phieuTraHangCTId > 0
                            ? (object)phieuTraHangCTId
                            : DBNull.Value),

                    new SqlParameter(
                        "@PhieuTraHangId",
                        DbValueHelper.DbValue(p.PhieuTraHangId)),

                    new SqlParameter(
                        "@SoPhieuTraHangGoc",
                        DbValueHelper.DbValue(p.SoPhieuTraHangGoc)),

                    new SqlParameter(
                        "@PhieuKhachTraId",
                        DbValueHelper.DbValue(p.PhieuKhachTraId)),

                    new SqlParameter(
                        "@SlotIdNguon",
                        DbValueHelper.DbValue(p.SlotIdNguon)),

                    new SqlParameter(
                        "@LotNguon",
                        DbValueHelper.DbValue(p.LotNguon)),

                    new SqlParameter(
                        "@Model",
                        DbValueHelper.DbValue(p.Model)),

                    new SqlParameter(
                        "@MaSanPham",
                        DbValueHelper.DbValue(p.MaSanPham)),

                    new SqlParameter(
                        "@SoLo",
                        DbValueHelper.DbValue(p.SoLo)),

                    new SqlParameter(
                        "@SoLoLoi",
                        DbValueHelper.DbValue(p.SoLoLoi)),

                    new SqlParameter(
                        "@SoLuongLoi",
                        p.SoLuongLoi),

                    new SqlParameter(
                        "@NoiDungBatThuong",
                        DbValueHelper.DbValue(p.NoiDungBatThuong)),

                    new SqlParameter(
                        "@PhanLoaiXuLy",
                        DbValueHelper.DbValue(p.PhanLoaiXuLy)),

                    new SqlParameter(
                        "@BoPhanPhatHanh",
                        DbValueHelper.DbValue(p.BoPhanPhatHanh)),

                    // Phiếu mới luôn bắt đầu ở Moi.
                    // LƯU Ý: không dùng new SqlParameter("@Status", (int)QTChungStatus.Moi):
                    // (int)Moi là hằng số 0 → C# chọn overload (string, SqlDbType) và
                    // tham số bị tạo KHÔNG có giá trị ("expects the parameter '@Status'").
                    new SqlParameter("@Status", SqlDbType.Int)
                    {
                        Value = (int)QTChungStatus.Moi
                    },

                    // HuongXuLy hiện là enum, không còn là string.
                    new SqlParameter(
                        "@HuongXuLy",
                        (int)p.HuongXuLy),

                    new SqlParameter(
                        "@NgayDinhHuong",
                        DbValueHelper.DbValue(p.NgayDinhHuong)),

                    new SqlParameter(
                        "@NguoiDinhHuong",
                        DbValueHelper.DbValue(p.NguoiDinhHuong)),

                    new SqlParameter(
                        "@CreatedBy",
                        DbValueHelper.DbValue(p.CreatedBy))
                ));

            // ========================================================
            // DETAIL + LIÊN KẾT DÒNG PHIẾU TRẢ HÀNG
            //
            // Mỗi PhieuTraHangCT chỉ thuộc một dòng detail của một phiếu.
            // ========================================================
            InsertItems(phieuXuLyId, items);
            return phieuXuLyId;
        }

        private void InsertItems(
            int phieuXuLyId,
            IList<PhieuXuLyBatThuongCT> items)
        {
            const string insertItemSql = @"
INSERT INTO FVN_PhieuXuLyBatThuongCT
(
    PhieuXuLyBatThuongId, PhieuTraHangCTId, SlotIdNguon, SlotLotIdNguon,
    LotNguon, MaSanPham, SoLuongLoi, LyDoNg, CreatedAt
)
VALUES
(
    @PhieuXuLyBatThuongId, @PhieuTraHangCTId, @SlotIdNguon, @SlotLotIdNguon,
    @LotNguon, @MaSanPham, @SoLuongLoi, @LyDoNg, GETDATE()
);";

            const string updateItemSql = @"
UPDATE FVN_PhieuTraHangCT
SET PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
WHERE Id = @PhieuTraHangCTId
  AND (
        PhieuXuLyBatThuongId IS NULL
        OR PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
      );";

            // DB chưa chạy script PHASE11 (cột liên kết ngược) → bỏ qua liên kết ngược.
            object colLen = ExecuteScalar(
                "SELECT COL_LENGTH('dbo.FVN_PhieuTraHangCT', 'PhieuXuLyBatThuongId');");
            bool coCotLienKet = colLen != null && colLen != DBNull.Value;

            foreach (var it in items)
            {
                if (it.PhieuTraHangCTId <= 0)
                    throw new InvalidOperationException(
                        "Dòng detail thiếu PhieuTraHangCTId.");
                if (it.SoLuongLoi <= 0)
                    throw new InvalidOperationException(
                        $"Dòng phiếu trả Id={it.PhieuTraHangCTId} có số lượng không hợp lệ.");

                ExecuteNonQuery(
                    insertItemSql,
                    new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId),
                    new SqlParameter("@PhieuTraHangCTId", it.PhieuTraHangCTId),
                    new SqlParameter("@SlotIdNguon", DbValueHelper.DbValue(it.SlotIdNguon)),
                    new SqlParameter("@SlotLotIdNguon", DbValueHelper.DbValue(it.SlotLotIdNguon)),
                    new SqlParameter("@LotNguon", DbValueHelper.DbValue(it.LotNguon)),
                    new SqlParameter("@MaSanPham", DbValueHelper.DbValue(it.MaSanPham)),
                    new SqlParameter("@SoLuongLoi", it.SoLuongLoi),
                    new SqlParameter("@LyDoNg", DbValueHelper.DbValue(it.LyDoNg)));

                if (!coCotLienKet)
                    continue;

                int affected = ExecuteNonQuery(
                    updateItemSql,
                    new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId),
                    new SqlParameter("@PhieuTraHangCTId", it.PhieuTraHangCTId));

                if (affected == 0)
                {
                    throw new InvalidOperationException(
                        $"Không thể liên kết PhieuTraHangCT.Id={it.PhieuTraHangCTId} " +
                        $"với PhieuXuLyBatThuong.Id={phieuXuLyId}.");
                }
            }
        }

        public List<PhieuXuLyBatThuongCT> GetItems(int phieuXuLyId)
        {
            var list = new List<PhieuXuLyBatThuongCT>();
            if (phieuXuLyId <= 0)
                return list;

            const string sql = @"
SELECT Id, PhieuXuLyBatThuongId, PhieuTraHangCTId, SlotIdNguon, SlotLotIdNguon,
       LotNguon, MaSanPham, SoLuongLoi, LyDoNg, CreatedAt
FROM FVN_PhieuXuLyBatThuongCT
WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
ORDER BY Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId));

            foreach (DataRow row in table.Rows)
            {
                list.Add(new PhieuXuLyBatThuongCT
                {
                    Id = DbValueHelper.ToInt(row["Id"]),
                    PhieuXuLyBatThuongId = DbValueHelper.ToInt(row["PhieuXuLyBatThuongId"]),
                    PhieuTraHangCTId = DbValueHelper.ToInt(row["PhieuTraHangCTId"]),
                    SlotIdNguon = ToNullableInt(row["SlotIdNguon"]),
                    SlotLotIdNguon = ToNullableInt(row["SlotLotIdNguon"]),
                    LotNguon = DbValueHelper.ToString(row["LotNguon"]),
                    MaSanPham = DbValueHelper.ToString(row["MaSanPham"]),
                    SoLuongLoi = DbValueHelper.ToInt(row["SoLuongLoi"]),
                    LyDoNg = DbValueHelper.ToString(row["LyDoNg"]),
                    CreatedAt = DbValueHelper.ToDateTime(row["CreatedAt"]) ?? DateTime.MinValue
                });
            }

            return list;
        }


        // ============================================================
        // TRA CỨU
        // ============================================================

        public PhieuXuLyBatThuong GetById(int id)
        {
            if (id <= 0)
                return null;

            const string sql = @"
SELECT
    Id,
    SoPhieu,
    Nguon,
    PhieuTraHangCTId,
    PhieuTraHangId,
    SoPhieuTraHangGoc,
    PhieuKhachTraId,
    SlotIdNguon,
    LotNguon,
    Model,
    MaSanPham,
    SoLo,
    SoLoLoi,
    SoLuongLoi,
    NoiDungBatThuong,
    PhanLoaiXuLy,
    BoPhanPhatHanh,
    Status,
    HuongXuLy,
    NgayDinhHuong,
    NguoiDinhHuong,
 LyDoHuy, NgayHuy, NguoiHuy, 
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy
FROM FVN_PhieuXuLyBatThuong
WHERE Id = @Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter("@Id", id));

            if (table.Rows.Count == 0)
                return null;

            var phieu = Map(table.Rows[0]);
            phieu.Items = GetItems(phieu.Id);
            return phieu;
        }


        public List<PhieuXuLyBatThuong> GetByNguon(
            NguonXuLyBatThuong nguon)
        {
            const string sql = @"
SELECT
    Id,
    SoPhieu,
    Nguon,
    PhieuTraHangCTId,
    PhieuTraHangId,
    SoPhieuTraHangGoc,
    PhieuKhachTraId,
    SlotIdNguon,
    LotNguon,
    Model,
    MaSanPham,
    SoLo,
    SoLoLoi,
    SoLuongLoi,
    NoiDungBatThuong,
    PhanLoaiXuLy,
    BoPhanPhatHanh,
    Status,
    HuongXuLy,
    NgayDinhHuong,
    NguoiDinhHuong,
 LyDoHuy, NgayHuy, NguoiHuy, 
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy
FROM FVN_PhieuXuLyBatThuong
WHERE Nguon = @Nguon
ORDER BY CreatedAt DESC, Id DESC;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@Nguon",
                    (int)nguon));

            var result =
                new List<PhieuXuLyBatThuong>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(Map(row));
            }

            return result;
        }


        public PhieuXuLyBatThuong GetByPhieuTraHangId(
            int phieuTraHangId)
        {
            if (phieuTraHangId <= 0)
                return null;

            const string sql = @"
SELECT TOP 1
    Id,
    SoPhieu,
    Nguon,
    PhieuTraHangCTId,
    PhieuTraHangId,
    SoPhieuTraHangGoc,
    PhieuKhachTraId,
    SlotIdNguon,
    LotNguon,
    Model,
    MaSanPham,
    SoLo,
    SoLoLoi,
    SoLuongLoi,
    NoiDungBatThuong,
    PhanLoaiXuLy,
    BoPhanPhatHanh,
    Status,
    HuongXuLy,
    NgayDinhHuong,
    NguoiDinhHuong,
 LyDoHuy, NgayHuy, NguoiHuy, 
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy
FROM FVN_PhieuXuLyBatThuong
WHERE PhieuTraHangId = @PhieuTraHangId
ORDER BY CreatedAt DESC, Id DESC;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuTraHangId",
                    phieuTraHangId));

            if (table.Rows.Count == 0)
                return null;

            return Map(table.Rows[0]);
        }


        public List<PhieuXuLyBatThuong> GetAllByPhieuTraHangId(
            int phieuTraHangId)
        {
            var result = new List<PhieuXuLyBatThuong>();

            if (phieuTraHangId <= 0)
                return result;

            const string sql = @"
SELECT
    Id,
    SoPhieu,
    Nguon,
    PhieuTraHangCTId,
    PhieuTraHangId,
    SoPhieuTraHangGoc,
    PhieuKhachTraId,
    SlotIdNguon,
    LotNguon,
    Model,
    MaSanPham,
    SoLo,
    SoLoLoi,
    SoLuongLoi,
    NoiDungBatThuong,
    PhanLoaiXuLy,
    BoPhanPhatHanh,
    Status,
    HuongXuLy,
    NgayDinhHuong,
    NguoiDinhHuong,
 LyDoHuy, NgayHuy, NguoiHuy, 
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy
FROM FVN_PhieuXuLyBatThuong
WHERE PhieuTraHangId = @PhieuTraHangId
ORDER BY Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuTraHangId",
                    phieuTraHangId));

            foreach (DataRow row in table.Rows)
                result.Add(Map(row));

            return result;
        }

        // ============================================================
        // STATUS — QT CHUNG
        // ============================================================

        public void UpdateStatus(
            int id,
            QTChungStatus status,
            string nguoiThucHien)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
UPDATE FVN_PhieuXuLyBatThuong
SET
    Status = @Status,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;";

            int affected = ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Status",
                    (int)status),

                new SqlParameter(
                    "@UpdatedBy",
                    DbValueHelper.DbValue(nguoiThucHien)),

                new SqlParameter(
                    "@Id",
                    id));

            if (affected == 0)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy PhieuXuLyBatThuong.Id={id}.");
            }
        }


//        public QTChungStatus GetStatus(int id)
//        {
//            if (id <= 0)
//                throw new ArgumentOutOfRangeException(nameof(id));

//            const string sql = @"
//SELECT Status
//FROM FVN_PhieuXuLyBatThuong
//WHERE Id = @Id;";

//            object value = ExecuteScalar(
//                sql,
//                new SqlParameter("@Id", id));

//            if (value == null || value == DBNull.Value)
//            {
//                throw new InvalidOperationException(
//                    $"Không tìm thấy PhieuXuLyBatThuong.Id={id}.");
//            }

//            return (QTChungStatus)
//                DbValueHelper.ToInt(value);
//        }


        // ============================================================
        // QC ĐỊNH HƯỚNG
        // ============================================================

        public void UpdateDinhHuong(
            int id,
            HuongXuLyBatThuong huongXuLy,
            string nguoiThucHien)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
UPDATE FVN_PhieuXuLyBatThuong
SET
    HuongXuLy = @HuongXuLy,
    NgayDinhHuong = GETDATE(),
    NguoiDinhHuong = @NguoiDinhHuong,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;";

            int affected = ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@HuongXuLy",
                    (int)huongXuLy),

                new SqlParameter(
                    "@NguoiDinhHuong",
                    DbValueHelper.DbValue(nguoiThucHien)),

                new SqlParameter(
                    "@UpdatedBy",
                    DbValueHelper.DbValue(nguoiThucHien)),

                new SqlParameter(
                    "@Id",
                    id));

            if (affected == 0)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy PhieuXuLyBatThuong.Id={id}.");
            }
        }


        // ============================================================
        // VALIDATE
        // ============================================================

        private static void ValidateForInsert(
            PhieuXuLyBatThuong p,
            bool requireSourceSlotLot)
        {
            if (p.Nguon == NguonXuLyBatThuong.KhachTra)
            {
                if (!p.PhieuTraHangId.HasValue ||
                    p.PhieuTraHangId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "PhieuTraHangId bắt buộc khi Nguon = KhachTra.");
                }

                if (!p.PhieuKhachTraId.HasValue ||
                    p.PhieuKhachTraId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "PhieuKhachTraId bắt buộc khi Nguon = KhachTra.");
                }
            }

            if (p.Nguon == NguonXuLyBatThuong.TraNoiBo)
            {
                if (!p.PhieuTraHangId.HasValue ||
                    p.PhieuTraHangId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "PhieuTraHangId bắt buộc khi Nguon = TraNoiBo.");
                }

                // Phiếu header + detail: Slot/LOT nằm ở dòng detail (đã kiểm ở Service).
                if (requireSourceSlotLot)
                {
                    if (!p.SlotIdNguon.HasValue ||
                        p.SlotIdNguon.Value <= 0)
                    {
                        throw new InvalidOperationException(
                            "SlotIdNguon bắt buộc khi Nguon = TraNoiBo.");
                    }

                    if (string.IsNullOrWhiteSpace(p.LotNguon))
                    {
                        throw new InvalidOperationException(
                            "LotNguon bắt buộc khi Nguon = TraNoiBo.");
                    }
                }
            }

            if (p.SoLuongLoi <= 0)
            {
                throw new InvalidOperationException(
                    "SoLuongLoi phải lớn hơn 0.");
            }
        }


        // ============================================================
        // MAPPING
        // ============================================================

        private static PhieuXuLyBatThuong Map(
            DataRow row)
        {
            return new PhieuXuLyBatThuong
            {
                Id =
                    DbValueHelper.ToInt(
                        row["Id"]),

                SoPhieu =
                    DbValueHelper.ToString(
                        row["SoPhieu"]),

                Nguon =
                    (NguonXuLyBatThuong)
                    DbValueHelper.ToInt(
                        row["Nguon"]),

                PhieuTraHangCTId =
                    DbValueHelper.ToInt(row["PhieuTraHangCTId"]),

                PhieuTraHangId =
                    ToNullableInt(
                        row["PhieuTraHangId"]),

                SoPhieuTraHangGoc =
                    DbValueHelper.ToString(
                        row["SoPhieuTraHangGoc"]),

                PhieuKhachTraId =
                    ToNullableInt(
                        row["PhieuKhachTraId"]),

                SlotIdNguon =
                    ToNullableInt(
                        row["SlotIdNguon"]),

                LotNguon =
                    DbValueHelper.ToString(
                        row["LotNguon"]),

                Model =
                    DbValueHelper.ToString(
                        row["Model"]),

                MaSanPham =
                    DbValueHelper.ToString(
                        row["MaSanPham"]),

                SoLo =
                    DbValueHelper.ToString(
                        row["SoLo"]),

                SoLoLoi =
                    DbValueHelper.ToString(
                        row["SoLoLoi"]),

                SoLuongLoi =
                    DbValueHelper.ToInt(
                        row["SoLuongLoi"]),

                NoiDungBatThuong =
                    DbValueHelper.ToString(
                        row["NoiDungBatThuong"]),

                PhanLoaiXuLy =
                    DbValueHelper.ToString(
                        row["PhanLoaiXuLy"]),

                BoPhanPhatHanh =
                    DbValueHelper.ToString(
                        row["BoPhanPhatHanh"]),

                Status =
                    (QTChungStatus)
                    DbValueHelper.ToInt(
                        row["Status"]),

                HuongXuLy =
                    (HuongXuLyBatThuong)
                    DbValueHelper.ToInt(
                        row["HuongXuLy"]),

                NgayDinhHuong =
                    DbValueHelper.ToDateTime(
                        row["NgayDinhHuong"]),

                NguoiDinhHuong =
                    DbValueHelper.ToString(
                        row["NguoiDinhHuong"]),
                LyDoHuy = DbValueHelper.ToString(row["LyDoHuy"]),
                NgayHuy = DbValueHelper.ToDateTime(row["NgayHuy"]),
                NguoiHuy = DbValueHelper.ToString(row["NguoiHuy"]),
                CreatedAt =
                    DbValueHelper.ToDateTime(
                        row["CreatedAt"])
                    ?? DateTime.MinValue,

                CreatedBy =
                    DbValueHelper.ToString(
                        row["CreatedBy"]),

                UpdatedAt =
                    DbValueHelper.ToDateTime(
                        row["UpdatedAt"]),

                UpdatedBy =
                    DbValueHelper.ToString(
                        row["UpdatedBy"])
            };
        }


        private static int? ToNullableInt(
            object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return null;
            }

            return DbValueHelper.ToInt(value);
        }
        public bool UpdateLyDoHuy(
            int id,
            QTChungStatus expectedFrom,
            string lyDoHuy,
            string nguoiThucHien)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            if (string.IsNullOrWhiteSpace(lyDoHuy))
                throw new ArgumentException("Lý do hủy không được rỗng.", nameof(lyDoHuy));

            const string sql = @"
UPDATE FVN_PhieuXuLyBatThuong
SET
    Status        = @NewStatus,
    LyDoHuy       = @LyDoHuy,
    NgayHuy       = GETDATE(),
    NguoiHuy      = @NguoiThucHien,
    UpdatedAt     = GETDATE(),
    UpdatedBy     = @NguoiThucHien
WHERE Id = @Id
  AND Status = @ExpectedFrom;";

            int affected = ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@NewStatus",
                    (int)QTChungStatus.Huy),

                new SqlParameter(
                    "@LyDoHuy",
                    lyDoHuy.Trim()),

                new SqlParameter(
                    "@NguoiThucHien",
                    DbValueHelper.DbValue(nguoiThucHien)),

                new SqlParameter(
                    "@Id",
                    id),

                new SqlParameter(
                    "@ExpectedFrom",
                    (int)expectedFrom));

            return affected > 0;
        }

        public int CountByStatus(QTChungStatus status)
        {
            object kq = ExecuteScalar(
                "SELECT COUNT(1) FROM FVN_PhieuXuLyBatThuong WHERE Status = @status",
                new SqlParameter("@status", (int)status));   // ✅ sửa từ status.ToString() thành (int)status
            return kq == null || kq == DBNull.Value ? 0 : Convert.ToInt32(kq);
        }
        // PhieuXuLyBatThuongRepository.cs
        public List<PhieuXuLyBatThuong> GetByStatus(QTChungStatus status)
        {
            const string sql = @"
SELECT
    Id, SoPhieu, Nguon, PhieuTraHangCTId, PhieuTraHangId, SoPhieuTraHangGoc, PhieuKhachTraId,
    SlotIdNguon, LotNguon, Model, MaSanPham, SoLo, SoLoLoi, SoLuongLoi,
    NoiDungBatThuong, PhanLoaiXuLy, BoPhanPhatHanh, Status, HuongXuLy,
    NgayDinhHuong, NguoiDinhHuong, LyDoHuy, NgayHuy, NguoiHuy,
    CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
FROM FVN_PhieuXuLyBatThuong
WHERE Status = @Status
ORDER BY CreatedAt DESC, Id DESC;";
            DataTable table = LoadData(sql, new SqlParameter("@Status", (int)status));
            var result = new List<PhieuXuLyBatThuong>();
            foreach (DataRow row in table.Rows)
                result.Add(Map(row));
            return result;
        }

    }
}
