using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
   

    public class TraHangQTChungRepository
        : SqlRepositoryBase,
          ITraHangQTChungRepository
    {
        public TraHangQTChungRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // XUẤT KHO REWORK
        // ============================================================

        // SQL Server datetime không lưu được DateTime.MinValue (0001) → dùng giờ hiện tại.
        private static DateTime SqlSafeDate(DateTime value)
        {
            return value < System.Data.SqlTypes.SqlDateTime.MinValue.Value
                ? DateTime.Now
                : value;
        }

        public int InsertXuat(TraHangQTChungXuat entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            const string sql = @"
INSERT INTO dbo.FVN_TraHangQTChungXuat
(
    OperationId,
    PhieuXuLyBatThuongId,
    SlotIdNguon,
    LotXuat,
    LoaiXuat,
    MaHang,
    SoLuongXuat,
    TonTruoc,
    TonSau,
    NgayXuat,
    NguoiXuat,
    LyDo,
    Note
)
OUTPUT INSERTED.Id
VALUES
(
    @OperationId,
    @PhieuXuLyBatThuongId,
    @SlotIdNguon,
    @LotXuat,
    @LoaiXuat,
    @MaHang,
    @SoLuongXuat,
    @TonTruoc,
    @TonSau,
    @NgayXuat,
    @NguoiXuat,
    @LyDo,
    @Note
);";

            return Convert.ToInt32(
                ExecuteScalar(
                    sql,

                    new SqlParameter("@OperationId", entity.OperationId),

                    new SqlParameter(
                        "@PhieuXuLyBatThuongId",
                        entity.PhieuXuLyBatThuongId),

                    new SqlParameter(
                        "@SlotIdNguon",
                        entity.SlotIdNguon),

                    new SqlParameter(
                        "@LotXuat",
                        DbValueHelper.DbValue(entity.LotXuat)),

                    new SqlParameter(
                        "@LoaiXuat",
                        DbValueHelper.DbValue(entity.LoaiXuat)),

                    new SqlParameter(
                        "@MaHang",
                        DbValueHelper.DbValue(entity.MaHang)),

                    new SqlParameter(
                        "@SoLuongXuat",
                        entity.SoLuongXuat),

                    new SqlParameter(
                        "@TonTruoc",
                        entity.TonTruoc),

                    new SqlParameter(
                        "@TonSau",
                        entity.TonSau),

                    // SQL Server datetime cannot store DateTime.MinValue (year 0001).
                    // Normalize omitted legacy timestamps at the persistence boundary.
                    new SqlParameter(
                        "@NgayXuat",
                        entity.NgayXuat < System.Data.SqlTypes.SqlDateTime.MinValue.Value
                            ? DateTime.Now
                            : entity.NgayXuat),

                    new SqlParameter(
                        "@NguoiXuat",
                        DbValueHelper.DbValue(entity.NguoiXuat)),

                    new SqlParameter(
                        "@LyDo",
                        DbValueHelper.DbValue(entity.LyDo)),

                    new SqlParameter(
                        "@Note",
                        DbValueHelper.DbValue(entity.Note))
                ));
        }

        public List<TraHangQTChungXuat> GetXuat(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            const string sql = @"
SELECT
    Id,
    OperationId,
    PhieuXuLyBatThuongId,
    SlotIdNguon,
    LotXuat,
    LoaiXuat,
    MaHang,
    SoLuongXuat,
    TonTruoc,
    TonSau,
    NgayXuat,
    NguoiXuat,
    LyDo,
    Note
FROM dbo.FVN_TraHangQTChungXuat
WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
ORDER BY NgayXuat, Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuXuLyBatThuongId",
                    phieuXuLyId));

            var result =
                new List<TraHangQTChungXuat>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapXuat(row));
            }

            return result;
        }

        public TraHangQTChungXuat GetXuatByOperationId(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("OperationId không hợp lệ.", nameof(operationId));

            const string sql = @"
SELECT TOP 1
    Id, OperationId, PhieuXuLyBatThuongId, SlotIdNguon, LotXuat, LoaiXuat,
    MaHang, SoLuongXuat, TonTruoc, TonSau, NgayXuat, NguoiXuat, LyDo, Note
FROM dbo.FVN_TraHangQTChungXuat
WHERE OperationId = @OperationId;";

            DataTable table = LoadData(sql, new SqlParameter("@OperationId", operationId));
            return table.Rows.Count == 0 ? null : MapXuat(table.Rows[0]);
        }

        public TraHangQTChungXuat GetXuatById(
            int id)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(id));

            const string sql = @"
SELECT
    Id,
    OperationId,
    PhieuXuLyBatThuongId,
    SlotIdNguon,
    LotXuat,
    LoaiXuat,
    MaHang,
    SoLuongXuat,
    TonTruoc,
    TonSau,
    NgayXuat,
    NguoiXuat,
    LyDo,
    Note
FROM dbo.FVN_TraHangQTChungXuat
WHERE Id = @Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter("@Id", id));

            if (table.Rows.Count == 0)
                return null;

            return MapXuat(table.Rows[0]);
        }


        // ============================================================
        // GIAO CHO SẢN XUẤT
        // ============================================================

        public int InsertGiao(
            TraHangQTChungGiao entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            const string sql = @"
INSERT INTO dbo.FVN_TraHangQTChungGiao
(
    PhieuXuLyBatThuongId,
    LotGiao,
    MaHang,
    SoLuongGiao,
    ThoiGian,
    NgayGiao,
    NguoiNhan,
    BoPhanNhan,
    SoPhieuGiaoNhan,
    Note
)
OUTPUT INSERTED.Id
VALUES
(
    @PhieuXuLyBatThuongId,
    @LotGiao,
    @MaHang,
    @SoLuongGiao,
    @ThoiGian,
    @NgayGiao,
    @NguoiNhan,
    @BoPhanNhan,
    @SoPhieuGiaoNhan,
    @Note
);";

            return Convert.ToInt32(
                ExecuteScalar(
                    sql,

                    new SqlParameter(
                        "@PhieuXuLyBatThuongId",
                        entity.PhieuXuLyBatThuongId),

                    new SqlParameter(
                        "@LotGiao",
                        DbValueHelper.DbValue(entity.LotGiao)),

                    new SqlParameter(
                        "@MaHang",
                        DbValueHelper.DbValue(entity.MaHang)),

                    new SqlParameter(
                        "@SoLuongGiao",
                        entity.SoLuongGiao),

                    new SqlParameter(
                        "@ThoiGian",
                        SqlSafeDate(entity.ThoiGian)),

                    new SqlParameter(
                        "@NgayGiao",
                        DbValueHelper.DbValue(entity.NgayGiao)),

                    new SqlParameter(
                        "@NguoiNhan",
                        DbValueHelper.DbValue(entity.NguoiNhan)),

                    new SqlParameter(
                        "@BoPhanNhan",
                        DbValueHelper.DbValue(entity.BoPhanNhan)),

                    new SqlParameter(
                        "@SoPhieuGiaoNhan",
                        DbValueHelper.DbValue(entity.SoPhieuGiaoNhan)),

                    new SqlParameter(
                        "@Note",
                        DbValueHelper.DbValue(entity.Note))
                ));
        }

        public List<TraHangQTChungGiao> GetGiao(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            const string sql = @"
SELECT
    Id,
    PhieuXuLyBatThuongId,
    LotGiao,
    MaHang,
    SoLuongGiao,
    ThoiGian,
    NgayGiao,
    NguoiNhan,
    BoPhanNhan,
    SoPhieuGiaoNhan,
    Note
FROM dbo.FVN_TraHangQTChungGiao
WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
ORDER BY ThoiGian, Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuXuLyBatThuongId",
                    phieuXuLyId));

            var result =
                new List<TraHangQTChungGiao>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapGiao(row));
            }

            return result;
        }

        public void UpdateLatestGiaoNote(int phieuXuLyId, string ghiChu, string nguoiThucHien)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(nameof(phieuXuLyId));
            if (string.IsNullOrWhiteSpace(ghiChu))
                throw new ArgumentException("Ghi chú rework không được rỗng.", nameof(ghiChu));
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                throw new ArgumentException("Người thực hiện không được rỗng.", nameof(nguoiThucHien));

            const string sql = @"
;WITH LatestGiao AS
(
    SELECT TOP (1) *
    FROM dbo.FVN_TraHangQTChungGiao
    WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
    ORDER BY ThoiGian DESC, Id DESC
)
UPDATE LatestGiao
SET Note =
    ISNULL(NULLIF(Note, N'') + CHAR(10), N'')
    + N'[ĐANG REWORK] ' + @GhiChu
    + N' | ' + @NguoiThucHien
    + N' | ' + CONVERT(NVARCHAR(19), GETDATE(), 120);";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId),
                new SqlParameter("@GhiChu", ghiChu.Trim()),
                new SqlParameter("@NguoiThucHien", nguoiThucHien.Trim()));

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy lần giao sản xuất của phiếu xử lý Id={phieuXuLyId} để ghi nhận tiến độ rework.");
        }

        public TraHangQTChungGiao GetGiaoById(
            int id)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(id));

            const string sql = @"
SELECT
    Id,
    PhieuXuLyBatThuongId,
    LotGiao,
    MaHang,
    SoLuongGiao,
    ThoiGian,
    NgayGiao,
    NguoiNhan,
    BoPhanNhan,
    SoPhieuGiaoNhan,
    Note
FROM dbo.FVN_TraHangQTChungGiao
WHERE Id = @Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter("@Id", id));

            if (table.Rows.Count == 0)
                return null;

            return MapGiao(table.Rows[0]);
        }


        // ============================================================
        // QC XÁC NHẬN CUỐI
        // ============================================================

        public int InsertQC(
            TraHangQTChungQC entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            const string sql = @"
INSERT INTO dbo.FVN_TraHangQTChungQC
(
    PhieuXuLyBatThuongId,
    SoLuongDaRework,
    SoLuongOK,
    SoLuongNG,
    DaKiemTraTem,
    ThoiGian,
    NguoiQC,
    KetLuan,
    Note
)
OUTPUT INSERTED.Id
VALUES
(
    @PhieuXuLyBatThuongId,
    @SoLuongDaRework,
    @SoLuongOK,
    @SoLuongNG,
    @DaKiemTraTem,
    @ThoiGian,
    @NguoiQC,
    @KetLuan,
    @Note
);";

            return Convert.ToInt32(
                ExecuteScalar(
                    sql,

                    new SqlParameter(
                        "@PhieuXuLyBatThuongId",
                        entity.PhieuXuLyBatThuongId),

                    new SqlParameter(
                        "@SoLuongDaRework",
                        entity.SoLuongDaRework),

                    new SqlParameter(
                        "@SoLuongOK",
                        entity.SoLuongOK),

                    new SqlParameter(
                        "@SoLuongNG",
                        entity.SoLuongNG),

                    new SqlParameter(
                        "@DaKiemTraTem",
                        entity.DaKiemTraTem),

                    new SqlParameter(
                        "@ThoiGian",
                        SqlSafeDate(entity.ThoiGian)),

                    new SqlParameter(
                        "@NguoiQC",
                        DbValueHelper.DbValue(entity.NguoiQC)),

                    new SqlParameter(
                        "@KetLuan",
                        DbValueHelper.DbValue(entity.KetLuan)),

                    new SqlParameter(
                        "@Note",
                        DbValueHelper.DbValue(entity.Note))
                ));
        }

        public TraHangQTChungQC GetQC(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            const string sql = @"
SELECT TOP 1
    Id,
    PhieuXuLyBatThuongId,
    SoLuongDaRework,
    SoLuongOK,
    SoLuongNG,
    DaKiemTraTem,
    ThoiGian,
    NguoiQC,
    KetLuan,
    Note
FROM dbo.FVN_TraHangQTChungQC
WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
ORDER BY ThoiGian DESC, Id DESC;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuXuLyBatThuongId",
                    phieuXuLyId));

            if (table.Rows.Count == 0)
                return null;

            return MapQC(table.Rows[0]);
        }


        // ============================================================
        // NHẬP NG
        // ============================================================

        public int InsertNhapNG(
            TraHangQTChungNhapNG entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            const string sql = @"
INSERT INTO dbo.FVN_TraHangQTChungNhapNG
(
    PhieuXuLyBatThuongId,
    SlotIdOK,
    SlotIdNG,
    LotNhapLai,
    MaHang,
    SoLuongNG,
    SlotIdNhap,
    NgayNhap,
    NguoiNhap,
    LyDo,
    Note
)
OUTPUT INSERTED.Id
VALUES
(
    @PhieuXuLyBatThuongId,
    @SlotIdOK,
    @SlotIdNG,
    @LotNhapLai,
    @MaHang,
    @SoLuongNG,
    @SlotIdNhap,
    @NgayNhap,
    @NguoiNhap,
    @LyDo,
    @Note
);";

            return Convert.ToInt32(
                ExecuteScalar(
                    sql,

                    new SqlParameter(
                        "@PhieuXuLyBatThuongId",
                        entity.PhieuXuLyBatThuongId),

                    new SqlParameter(
                        "@SlotIdOK",
                        DbValueHelper.DbValue(entity.SlotIdOK)),

                    new SqlParameter(
                        "@SlotIdNG",
                        DbValueHelper.DbValue(entity.SlotIdNG)),

                    new SqlParameter(
                        "@LotNhapLai",
                        DbValueHelper.DbValue(entity.LotNhapLai)),

                    new SqlParameter(
                        "@MaHang",
                        DbValueHelper.DbValue(entity.MaHang)),

                    new SqlParameter(
                        "@SoLuongNG",
                        entity.SoLuongNG),

                    new SqlParameter(
                        "@SlotIdNhap",
                        DbValueHelper.DbValue(entity.SlotIdNhap)),

                    new SqlParameter(
                        "@NgayNhap",
                        SqlSafeDate(entity.NgayNhap)),

                    new SqlParameter(
                        "@NguoiNhap",
                        DbValueHelper.DbValue(entity.NguoiNhap)),

                    new SqlParameter(
                        "@LyDo",
                        DbValueHelper.DbValue(entity.LyDo)),

                    new SqlParameter(
                        "@Note",
                        DbValueHelper.DbValue(entity.Note))
                ));
        }

        public List<TraHangQTChungNhapNG> GetNhapNG(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            const string sql = @"
SELECT
    Id,
    PhieuXuLyBatThuongId,
    SlotIdOK,
    SlotIdNG,
    LotNhapLai,
    MaHang,
    SoLuongNG,
    SlotIdNhap,
    NgayNhap,
    NguoiNhap,
    LyDo,
    Note
FROM dbo.FVN_TraHangQTChungNhapNG
WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
ORDER BY NgayNhap, Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuXuLyBatThuongId",
                    phieuXuLyId));

            var result =
                new List<TraHangQTChungNhapNG>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(MapNhapNG(row));
            }

            return result;
        }

        public TraHangQTChungNhapNG GetNhapNGById(
            int id)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(id));

            const string sql = @"
SELECT
    Id,
    PhieuXuLyBatThuongId,
    SlotIdOK,
    SlotIdNG,
    LotNhapLai,
    MaHang,
    SoLuongNG,
    SlotIdNhap,
    NgayNhap,
    NguoiNhap,
    LyDo,
    Note
FROM dbo.FVN_TraHangQTChungNhapNG
WHERE Id = @Id;";

            DataTable table = LoadData(
                sql,
                new SqlParameter("@Id", id));

            if (table.Rows.Count == 0)
                return null;

            return MapNhapNG(table.Rows[0]);
        }


        // ============================================================
        // TIMELINE
        // ============================================================

        public List<QTChungTimelineItem> GetTimeline(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            /*
             * Timeline là dữ liệu đọc tổng hợp từ:
             *   - XUAT
             *   - GIAO
             *   - QC
             *   - NHAP_NG
             *
             * Không lưu Timeline riêng.
             */

            const string sql = @"
SELECT
    PhieuXuLyId,
    Buoc,
    RefId,
    ThoiGian,
    LotNo,
    MaHang,
    SoLuong,
    NguoiThucHien,
    NoiDung,
    Note
FROM
(
    SELECT
        PhieuXuLyBatThuongId AS PhieuXuLyId,
        'XUAT' AS Buoc,
        Id AS RefId,
        NgayXuat AS ThoiGian,
        LotXuat AS LotNo,
        MaHang,
        SoLuongXuat AS SoLuong,
        NguoiXuat AS NguoiThucHien,
        LyDo AS NoiDung,
        Note
    FROM dbo.FVN_TraHangQTChungXuat

    UNION ALL

    SELECT
        PhieuXuLyBatThuongId,
        'GIAO',
        Id,
        ThoiGian,
        LotGiao,
        MaHang,
        SoLuongGiao,
        NguoiNhan,
        BoPhanNhan,
        Note
    FROM dbo.FVN_TraHangQTChungGiao

    UNION ALL

    SELECT
        PhieuXuLyBatThuongId,
        'QC',
        Id,
        ThoiGian,
        NULL,
        NULL,
        SoLuongOK + SoLuongNG,
        NguoiQC,
        KetLuan,
        Note
    FROM dbo.FVN_TraHangQTChungQC

    UNION ALL

    SELECT
        PhieuXuLyBatThuongId,
        'NHAP_NG',
        Id,
        NgayNhap,
        LotNhapLai,
        MaHang,
        SoLuongNG,
        NguoiNhap,
        LyDo,
        Note
    FROM dbo.FVN_TraHangQTChungNhapNG
) X
WHERE PhieuXuLyId = @PhieuXuLyBatThuongId
ORDER BY ThoiGian, RefId;";

            DataTable table = LoadData(
                sql,
                new SqlParameter(
                    "@PhieuXuLyBatThuongId",
                    phieuXuLyId));

            var result =
                new List<QTChungTimelineItem>();

            foreach (DataRow row in table.Rows)
            {
                result.Add(
                    new QTChungTimelineItem
                    {
                        PhieuXuLyId =
                            DbValueHelper.ToInt(
                                row["PhieuXuLyId"]),

                        Buoc =
                            DbValueHelper.ToString(
                                row["Buoc"]),

                        RefId =
                            row["RefId"] == DBNull.Value
                                ? (int?)null
                                : DbValueHelper.ToInt(
                                    row["RefId"]),

                        ThoiGian =
                            DbValueHelper.SafeDate(
                                row["ThoiGian"]),

                        LotNo =
                            DbValueHelper.ToString(
                                row["LotNo"]),

                        MaHang =
                            DbValueHelper.ToString(
                                row["MaHang"]),

                        SoLuong =
                            row["SoLuong"] == DBNull.Value
                                ? (int?)null
                                : DbValueHelper.ToInt(
                                    row["SoLuong"]),

                        NguoiThucHien =
                            DbValueHelper.ToString(
                                row["NguoiThucHien"]),

                        NoiDung =
                            DbValueHelper.ToString(
                                row["NoiDung"]),

                        Note =
                            DbValueHelper.ToString(
                                row["Note"])
                    });
            }

            return result;
        }


        // ============================================================
        // KIỂM TRA TIẾN ĐỘ
        // ============================================================

        public bool DaXuatKho(
            int phieuXuLyId)
        {
            return Exists(
                @"SELECT TOP 1 1
              FROM dbo.FVN_TraHangQTChungXuat
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public bool DaGiaoSanXuat(
            int phieuXuLyId)
        {
            return Exists(
                @"SELECT TOP 1 1
              FROM dbo.FVN_TraHangQTChungGiao
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public bool DaQCXacNhan(
            int phieuXuLyId)
        {
            return Exists(
                @"SELECT TOP 1 1
              FROM dbo.FVN_TraHangQTChungQC
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public bool DaNhapNG(
            int phieuXuLyId)
        {
            return Exists(
                @"SELECT TOP 1 1
              FROM dbo.FVN_TraHangQTChungNhapNG
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }


        // ============================================================
        // QC — KIỂM TRA TEM
        // ============================================================

        public void UpdateDaKiemTraTem(
            int qcId,
            bool daKiemTra)
        {
            if (qcId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(qcId));

            const string sql = @"
UPDATE dbo.FVN_TraHangQTChungQC
SET
    DaKiemTraTem = @DaKiemTraTem
WHERE Id = @Id;";

            int affected = ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@DaKiemTraTem",
                    daKiemTra),

                new SqlParameter(
                    "@Id",
                    qcId));

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy QC Id={qcId}.");
        }


        // ============================================================
        // TỔNG HỢP
        // ============================================================

        public int GetTongSoLuongDaXuat(
            int phieuXuLyId)
        {
            return GetTotal(
                @"SELECT ISNULL(SUM(SoLuongXuat), 0)
              FROM dbo.FVN_TraHangQTChungXuat
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public int GetTongSoLuongDaGiao(
            int phieuXuLyId)
        {
            return GetTotal(
                @"SELECT ISNULL(SUM(SoLuongGiao), 0)
              FROM dbo.FVN_TraHangQTChungGiao
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public int GetTongSoLuongOK(
            int phieuXuLyId)
        {
            return GetTotal(
                @"SELECT ISNULL(SUM(SoLuongOK), 0)
              FROM dbo.FVN_TraHangQTChungQC
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public int GetTongSoLuongNG(
            int phieuXuLyId)
        {
            return GetTotal(
                @"SELECT ISNULL(SUM(SoLuongNG), 0)
              FROM dbo.FVN_TraHangQTChungQC
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }

        public int GetTongSoLuongDaNhapNG(
            int phieuXuLyId)
        {
            return GetTotal(
                @"SELECT ISNULL(SUM(SoLuongNG), 0)
              FROM dbo.FVN_TraHangQTChungNhapNG
              WHERE PhieuXuLyBatThuongId = @Id",
                phieuXuLyId);
        }


        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private bool Exists(
            string sql,
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            object value = ExecuteScalar(
                sql,
                new SqlParameter("@Id", phieuXuLyId));

            return value != null &&
                   value != DBNull.Value;
        }

        private int GetTotal(
            string sql,
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(phieuXuLyId));

            object value = ExecuteScalar(
                sql,
                new SqlParameter("@Id", phieuXuLyId));

            return DbValueHelper.ToInt(value);
        }


        // ============================================================
        // MAPPING
        // ============================================================

        private static TraHangQTChungXuat MapXuat(
            DataRow row)
        {
            return new TraHangQTChungXuat
            {
                Id =
                    DbValueHelper.ToInt(row["Id"]),

                OperationId = row["OperationId"] == DBNull.Value ? Guid.Empty : (Guid)row["OperationId"],

                PhieuXuLyBatThuongId =
                    DbValueHelper.ToInt(
                        row["PhieuXuLyBatThuongId"]),

                SlotIdNguon =
                    DbValueHelper.ToInt(
                        row["SlotIdNguon"]),

                LotXuat =
                    DbValueHelper.ToString(
                        row["LotXuat"]),

                LoaiXuat =
                    DbValueHelper.ToString(
                        row["LoaiXuat"]),

                MaHang =
                    DbValueHelper.ToString(
                        row["MaHang"]),

                SoLuongXuat =
                    DbValueHelper.ToInt(
                        row["SoLuongXuat"]),

                TonTruoc =
                    DbValueHelper.ToInt(
                        row["TonTruoc"]),

                TonSau =
                    DbValueHelper.ToInt(
                        row["TonSau"]),

                NgayXuat =
                    DbValueHelper.SafeDate(
                        row["NgayXuat"]),

                NguoiXuat =
                    DbValueHelper.ToString(
                        row["NguoiXuat"]),

                LyDo =
                    DbValueHelper.ToString(
                        row["LyDo"]),

                Note =
                    DbValueHelper.ToString(
                        row["Note"])
            };
        }

        private static TraHangQTChungGiao MapGiao(
            DataRow row)
        {
            return new TraHangQTChungGiao
            {
                Id =
                    DbValueHelper.ToInt(row["Id"]),

                PhieuXuLyBatThuongId =
                    DbValueHelper.ToInt(
                        row["PhieuXuLyBatThuongId"]),

                LotGiao =
                    DbValueHelper.ToString(
                        row["LotGiao"]),

                MaHang =
                    DbValueHelper.ToString(
                        row["MaHang"]),

                SoLuongGiao =
                    DbValueHelper.ToInt(
                        row["SoLuongGiao"]),

                ThoiGian =
                    DbValueHelper.SafeDate(
                        row["ThoiGian"]),

                NgayGiao =
                    DbValueHelper.ToString(
                        row["NgayGiao"]),

                NguoiNhan =
                    DbValueHelper.ToString(
                        row["NguoiNhan"]),

                BoPhanNhan =
                    DbValueHelper.ToString(
                        row["BoPhanNhan"]),

                SoPhieuGiaoNhan =
                    DbValueHelper.ToString(
                        row["SoPhieuGiaoNhan"]),

                Note =
                    DbValueHelper.ToString(
                        row["Note"])
            };
        }

        private static TraHangQTChungQC MapQC(
            DataRow row)
        {
            return new TraHangQTChungQC
            {
                Id =
                    DbValueHelper.ToInt(row["Id"]),

                PhieuXuLyBatThuongId =
                    DbValueHelper.ToInt(
                        row["PhieuXuLyBatThuongId"]),

                SoLuongDaRework =
                    DbValueHelper.ToInt(
                        row["SoLuongDaRework"]),

                SoLuongOK =
                    DbValueHelper.ToInt(
                        row["SoLuongOK"]),

                SoLuongNG =
                    DbValueHelper.ToInt(
                        row["SoLuongNG"]),

                DaKiemTraTem =
                    DbValueHelper.ToBool(
                        row["DaKiemTraTem"]),

                ThoiGian =
                    DbValueHelper.SafeDate(
                        row["ThoiGian"]),

                NguoiQC =
                    DbValueHelper.ToString(
                        row["NguoiQC"]),

                KetLuan =
                    DbValueHelper.ToString(
                        row["KetLuan"]),

                Note =
                    DbValueHelper.ToString(
                        row["Note"])
            };
        }

        private static TraHangQTChungNhapNG MapNhapNG(
            DataRow row)
        {
            return new TraHangQTChungNhapNG
            {
                Id =
                    DbValueHelper.ToInt(row["Id"]),

                PhieuXuLyBatThuongId =
                    DbValueHelper.ToInt(
                        row["PhieuXuLyBatThuongId"]),

                SlotIdOK =
                    row["SlotIdOK"] == DBNull.Value
                        ? (int?)null
                        : DbValueHelper.ToInt(
                            row["SlotIdOK"]),

                SlotIdNG =
                    row["SlotIdNG"] == DBNull.Value
                        ? (int?)null
                        : DbValueHelper.ToInt(
                            row["SlotIdNG"]),

                LotNhapLai =
                    DbValueHelper.ToString(
                        row["LotNhapLai"]),

                MaHang =
                    DbValueHelper.ToString(
                        row["MaHang"]),

                SoLuongNG =
                    DbValueHelper.ToInt(
                        row["SoLuongNG"]),

                SlotIdNhap =
                    row["SlotIdNhap"] == DBNull.Value
                        ? (int?)null
                        : DbValueHelper.ToInt(
                            row["SlotIdNhap"]),

                NgayNhap =
                    DbValueHelper.SafeDate(
                        row["NgayNhap"]),

                NguoiNhap =
                    DbValueHelper.ToString(
                        row["NguoiNhap"]),

                LyDo =
                    DbValueHelper.ToString(
                        row["LyDo"]),

                Note =
                    DbValueHelper.ToString(
                        row["Note"])
            };
        }
    }
}
