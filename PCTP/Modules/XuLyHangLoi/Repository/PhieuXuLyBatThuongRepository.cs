using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Enum;
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
    public sealed class PhieuXuLyBatThuongRepository : SqlRepositoryBase, IPhieuXuLyBatThuongRepository
    {
        public PhieuXuLyBatThuongRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public int Insert(PhieuXuLyBatThuong e)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_PhieuXuLyBatThuong " +
                "(Nguon, SoPhieu, PhieuKhachTraId, Model, MaSanPham, SoLo, SoLoLoi, SoLuongLoi, " +
                " NoiDungBatThuong, PhanLoaiXuLy, BoPhanPhatHanh, TrangThai, CreatedAt, CreatedBy) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@nguon, @sp, @ptId, @model, @masp, @solo, @sololoi, @sl, " +
                "@nd, @pl, @bp, @tt, GETDATE(), @by)",
                new SqlParameter("@nguon", (int)e.Nguon),
                new SqlParameter("@sp", (object)e.SoPhieu ?? DBNull.Value),
                new SqlParameter("@ptId", (object)e.PhieuKhachTraId ?? DBNull.Value),
                new SqlParameter("@model", (object)e.Model ?? DBNull.Value),
                new SqlParameter("@masp", (object)e.MaSanPham ?? DBNull.Value),
                new SqlParameter("@solo", (object)e.SoLo ?? DBNull.Value),
                new SqlParameter("@sololoi", (object)e.SoLoLoi ?? DBNull.Value),
                new SqlParameter("@sl", e.SoLuongLoi),
                new SqlParameter("@nd", (object)e.NoiDungBatThuong ?? DBNull.Value),
                new SqlParameter("@pl", (object)e.PhanLoaiXuLy ?? DBNull.Value),
                new SqlParameter("@bp", (object)e.BoPhanPhatHanh ?? DBNull.Value),
                new SqlParameter("@tt", (int)e.TrangThai),
                new SqlParameter("@by", (object)e.CreatedBy ?? DBNull.Value));
            return Convert.ToInt32(id);
        }

        public PhieuXuLyBatThuong GetById(int id)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuXuLyBatThuong WHERE Id = @id",
                new SqlParameter("@id", id));
            return dt.Rows.Count > 0 ? Map(dt.Rows[0]) : null;
        }

        public List<PhieuXuLyBatThuong> GetByNguon(NguonXuLyBatThuong nguon)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuXuLyBatThuong WHERE Nguon = @n ORDER BY CreatedAt DESC",
                new SqlParameter("@n", (int)nguon));
            return dt.Rows.Cast<DataRow>().Select(Map).ToList();
        }

        public void UpdateTrangThai(int id, QTChungStatus trangThai, string nguoiThucHien)
        {
            // Validate chuyển trạng thái nằm ở IQTChungService — repository chỉ ghi giá trị đã xác nhận hợp lệ.
            ExecuteNonQuery(
                "UPDATE FVN_PhieuXuLyBatThuong SET TrangThai = @tt, UpdatedAt = GETDATE(), UpdatedBy = @by WHERE Id = @id",
                new SqlParameter("@tt", (int)trangThai),
                new SqlParameter("@by", nguoiThucHien),
                new SqlParameter("@id", id));
        }

        public QTChungStatus GetTrangThai(int id)
        {
            object kq = ExecuteScalar("SELECT TrangThai FROM FVN_PhieuXuLyBatThuong WHERE Id = @id",
                new SqlParameter("@id", id));
            if (kq == null || kq == DBNull.Value)
                throw new InvalidOperationException($"Không tìm thấy FVN_PhieuXuLyBatThuong Id={id}");
            return (QTChungStatus)Convert.ToInt32(kq);
        }

        public void UpdateDinhHuong(int id, string huongXuLy, string nguoiThucHien)
        {
            ExecuteNonQuery(
                "UPDATE FVN_PhieuXuLyBatThuong SET HuongXuLy = @hx, NgayDinhHuong = GETDATE(), " +
                "NguoiDinhHuong = @nd, UpdatedAt = GETDATE(), UpdatedBy = @by WHERE Id = @id",
                new SqlParameter("@hx", huongXuLy),
                new SqlParameter("@nd", nguoiThucHien),
                new SqlParameter("@by", nguoiThucHien),
                new SqlParameter("@id", id));
        }

        public void GanPhieuKhachTra(int phieuXuLyId, int phieuKhachTraId)
        {
            ExecuteNonQuery(
                "UPDATE FVN_PhieuXuLyBatThuong SET PhieuKhachTraId = @ptId WHERE Id = @id",
                new SqlParameter("@ptId", phieuKhachTraId),
                new SqlParameter("@id", phieuXuLyId));
        }

        public int? GetPhieuKhachTraId(int phieuXuLyId)
        {
            object kq = ExecuteScalar("SELECT PhieuKhachTraId FROM FVN_PhieuXuLyBatThuong WHERE Id = @id",
                new SqlParameter("@id", phieuXuLyId));
            return kq == null || kq == DBNull.Value ? (int?)null : Convert.ToInt32(kq);
        }

        private static PhieuXuLyBatThuong Map(DataRow r) => new PhieuXuLyBatThuong
        {
            Id = Convert.ToInt32(r["Id"]),
            Nguon = (NguonXuLyBatThuong)Convert.ToInt32(r["Nguon"]),
            SoPhieu = r["SoPhieu"] as string,
            PhieuKhachTraId = r["PhieuKhachTraId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PhieuKhachTraId"]),
            Model = r["Model"] as string,
            MaSanPham = r["MaSanPham"] as string,
            SoLo = r["SoLo"] as string,
            SoLoLoi = r["SoLoLoi"] as string,
            SoLuongLoi = r["SoLuongLoi"] == DBNull.Value ? 0 : Convert.ToInt32(r["SoLuongLoi"]),
            NoiDungBatThuong = r["NoiDungBatThuong"] as string,
            PhanLoaiXuLy = r["PhanLoaiXuLy"] as string,
            BoPhanPhatHanh = r["BoPhanPhatHanh"] as string,
            TrangThai = (QTChungStatus)Convert.ToInt32(r["TrangThai"]),
            HuongXuLy = r["HuongXuLy"] as string,
            NgayDinhHuong = r["NgayDinhHuong"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayDinhHuong"]),
            NguoiDinhHuong = r["NguoiDinhHuong"] as string,
            CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
            CreatedBy = r["CreatedBy"] as string,
            UpdatedAt = r["UpdatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["UpdatedAt"]),
            UpdatedBy = r["UpdatedBy"] as string
        };
    }
}
