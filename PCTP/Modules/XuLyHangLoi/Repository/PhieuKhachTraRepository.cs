using PCTP.Modules.GiaoHangKhach;
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
    public sealed class PhieuKhachTraRepository : SqlRepositoryBase, IPhieuKhachTraRepository
    {
        public PhieuKhachTraRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        #region Header

        public int Insert(PhieuKhachTra e)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_PhieuKhachTra " +
                "(Nguon, SoPhieu, NgayPhatHanh, SlipNo, TenKhachHang, BoPhanPhatHienLoi, " +
                " XacNhanBPPhatHienLoi, XacNhanQCKhach, XacNhanNhaCungCap, NgayNhanKho, " +
                " TongSoLuongNhan, Status, Note, CreatedAt, CreatedBy) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@nguon, @sp, @npt, @slip, @tkh, @bppl, @xnbp, @xnqc, @xnncc, @nnk, " +
                "@tsln, @st, @note, GETDATE(), @by)",
                new SqlParameter("@nguon", (int)e.Nguon),
                new SqlParameter("@sp", (object)e.SoPhieu ?? DBNull.Value),
                new SqlParameter("@npt", (object)e.NgayPhatHanh ?? DBNull.Value),
                new SqlParameter("@slip", (object)e.SlipNo ?? DBNull.Value),
                new SqlParameter("@tkh", (object)e.TenKhachHang ?? DBNull.Value),
                new SqlParameter("@bppl", (object)e.BoPhanPhatHienLoi ?? DBNull.Value),
                new SqlParameter("@xnbp", (object)e.XacNhanBPPhatHienLoi ?? DBNull.Value),
                new SqlParameter("@xnqc", (object)e.XacNhanQCKhach ?? DBNull.Value),
                new SqlParameter("@xnncc", (object)e.XacNhanNhaCungCap ?? DBNull.Value),
                new SqlParameter("@nnk", (object)e.NgayNhanKho ?? DBNull.Value),
                new SqlParameter("@tsln", e.TongSoLuongNhan),
                new SqlParameter("@st", (int)e.Status),
                new SqlParameter("@note", (object)e.Note ?? DBNull.Value),
                new SqlParameter("@by", (object)e.CreatedBy ?? DBNull.Value));

            int newId = Convert.ToInt32(id);

            if (e.Items != null && e.Items.Count > 0)
                InsertItems(newId, e.Items);

            return newId;
        }

        public PhieuKhachTra GetById(int id)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuKhachTra WHERE Id = @id", new SqlParameter("@id", id));
            if (dt.Rows.Count == 0) return null;
            var entity = MapHeader(dt.Rows[0]);
            entity.Items = GetItems(id);
            return entity;
        }

        public PhieuKhachTra GetBySoPhieu(string soPhieu)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuKhachTra WHERE SoPhieu = @sp",
                new SqlParameter("@sp", soPhieu));
            if (dt.Rows.Count == 0) return null;
            var entity = MapHeader(dt.Rows[0]);
            entity.Items = GetItems(entity.Id);
            return entity;
        }

        public List<PhieuKhachTra> GetByNguon(NguonXuLyBatThuong nguon)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuKhachTra WHERE Nguon = @n ORDER BY CreatedAt DESC",
                new SqlParameter("@n", (int)nguon));
            return dt.Rows.Cast<DataRow>().Select(MapHeader).ToList();
        }

        public List<PhieuKhachTra> GetChoXuLy()
        {
            // "Chờ xử lý" = chưa hoàn tất QTChung
            DataTable dt = LoadData(
                "SELECT * FROM FVN_PhieuKhachTra WHERE DaHoanTatQTChung = 0 ORDER BY CreatedAt",
                Array.Empty<SqlParameter>());
            return dt.Rows.Cast<DataRow>().Select(MapHeader).ToList();
        }

        public void Update(PhieuKhachTra e)
        {
            ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET " +
                "SoPhieu=@sp, NgayPhatHanh=@npt, SlipNo=@slip, TenKhachHang=@tkh, " +
                "BoPhanPhatHienLoi=@bppl, XacNhanBPPhatHienLoi=@xnbp, XacNhanQCKhach=@xnqc, " +
                "XacNhanNhaCungCap=@xnncc, NgayNhanKho=@nnk, TongSoLuongNhan=@tsln, " +
                "Status=@st, Note=@note, UpdatedAt=GETDATE(), UpdatedBy=@by " +
                "WHERE Id=@id",
                new SqlParameter("@sp", (object)e.SoPhieu ?? DBNull.Value),
                new SqlParameter("@npt", (object)e.NgayPhatHanh ?? DBNull.Value),
                new SqlParameter("@slip", (object)e.SlipNo ?? DBNull.Value),
                new SqlParameter("@tkh", (object)e.TenKhachHang ?? DBNull.Value),
                new SqlParameter("@bppl", (object)e.BoPhanPhatHienLoi ?? DBNull.Value),
                new SqlParameter("@xnbp", (object)e.XacNhanBPPhatHienLoi ?? DBNull.Value),
                new SqlParameter("@xnqc", (object)e.XacNhanQCKhach ?? DBNull.Value),
                new SqlParameter("@xnncc", (object)e.XacNhanNhaCungCap ?? DBNull.Value),
                new SqlParameter("@nnk", (object)e.NgayNhanKho ?? DBNull.Value),
                new SqlParameter("@tsln", e.TongSoLuongNhan),
                new SqlParameter("@st", (int)e.Status),
                new SqlParameter("@note", (object)e.Note ?? DBNull.Value),
                new SqlParameter("@by", (object)e.UpdatedBy ?? DBNull.Value),
                new SqlParameter("@id", e.Id));
        }

        public void UpdateStatus(int id, PhieuTraHangStatus status, string nguoiThucHien)
        {
            ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET Status=@st, UpdatedAt=GETDATE(), UpdatedBy=@by WHERE Id=@id",
                new SqlParameter("@st", (int)status),
                new SqlParameter("@by", nguoiThucHien),
                new SqlParameter("@id", id));
        }

        #endregion

        #region Item

        public int InsertItem(PhieuKhachTraItem item)
        {
            object id = ExecuteScalar(
                "INSERT INTO FVN_PhieuKhachTraItem " +
                "(PhieuKhachTraId, MaHang, TenHang, LotNo, SoLuong, NoiDungLoi, DinhDanhPhieuGiao, PoNo, NgayGiao, NhaMay) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@pid, @ma, @tenH, @lot, @sl, @nd, @ddpg, @po, @ng, @nm)",
                new SqlParameter("@pid", item.PhieuKhachTraId),
                new SqlParameter("@ma", (object)item.MaHang ?? DBNull.Value),
                new SqlParameter("@tenH", (object)item.TenHang ?? DBNull.Value),
                new SqlParameter("@lot", (object)item.LotNo ?? DBNull.Value),
                new SqlParameter("@sl", item.SoLuong),
                new SqlParameter("@nd", (object)item.NoiDungLoi ?? DBNull.Value),
                new SqlParameter("@ddpg", (object)item.DinhDanhPhieuGiao ?? DBNull.Value),
                new SqlParameter("@po", (object)item.PoNo ?? DBNull.Value),
                new SqlParameter("@ng", (object)item.NgayGiao ?? DBNull.Value),
                new SqlParameter("@nm", (object)item.NhaMay ?? DBNull.Value));
            return Convert.ToInt32(id);
        }

        public void InsertItems(int phieuKhachTraId, IEnumerable<PhieuKhachTraItem> items)
        {
            foreach (var item in items)
            {
                item.PhieuKhachTraId = phieuKhachTraId;
                InsertItem(item);
            }
        }

        public List<PhieuKhachTraItem> GetItems(int phieuKhachTraId)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuKhachTraItem WHERE PhieuKhachTraId = @id",
                new SqlParameter("@id", phieuKhachTraId));
            return dt.Rows.Cast<DataRow>().Select(MapItem).ToList();
        }

        public PhieuKhachTraItem GetItemById(int itemId)
        {
            DataTable dt = LoadData("SELECT * FROM FVN_PhieuKhachTraItem WHERE Id = @id",
                new SqlParameter("@id", itemId));
            return dt.Rows.Count > 0 ? MapItem(dt.Rows[0]) : null;
        }

        #endregion

        #region Liên kết phiếu bất thường

        public void GanPhieuXuLyBatThuong(int phieuKhachTraId, int phieuXuLyId)
        {
            // FK thực tế nằm ở FVN_PhieuXuLyBatThuong.PhieuKhachTraId (theo model đã chốt).
            ExecuteNonQuery(
                "UPDATE FVN_PhieuXuLyBatThuong SET PhieuKhachTraId = @pkt WHERE Id = @pxl",
                new SqlParameter("@pkt", phieuKhachTraId),
                new SqlParameter("@pxl", phieuXuLyId));

            ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET DaTaoPhieuBatThuong = 1, UpdatedAt = GETDATE() WHERE Id = @id",
                new SqlParameter("@id", phieuKhachTraId));
        }

        public int? GetPhieuXuLyBatThuongId(int phieuKhachTraId)
        {
            object kq = ExecuteScalar(
                "SELECT TOP 1 Id FROM FVN_PhieuXuLyBatThuong WHERE PhieuKhachTraId = @id ORDER BY CreatedAt DESC",
                new SqlParameter("@id", phieuKhachTraId));
            return kq == null || kq == DBNull.Value ? (int?)null : Convert.ToInt32(kq);
        }

        #endregion

        #region Giao bù

        public void DanhDauChoGiaoBu(int phieuKhachTraId)
            => ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET Status = @st, UpdatedAt = GETDATE() WHERE Id = @id",
                new SqlParameter("@st", (int)PhieuTraHangStatus.ChoGiaoBu),
                new SqlParameter("@id", phieuKhachTraId));

        public void DanhDauDaGiaoBu(int phieuKhachTraId, string nguoiThucHien)
            => ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET Status = @st, DaGiaoBu = 1, UpdatedAt = GETDATE(), UpdatedBy = @by WHERE Id = @id",
                new SqlParameter("@st", (int)PhieuTraHangStatus.DaGiaoBu),
                new SqlParameter("@by", nguoiThucHien),
                new SqlParameter("@id", phieuKhachTraId));

        #endregion

        public void UpdateNote(int id, string note, string nguoiThucHien)
            => ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET Note = @note, UpdatedAt = GETDATE(), UpdatedBy = @by WHERE Id = @id",
                new SqlParameter("@note", (object)note ?? DBNull.Value),
                new SqlParameter("@by", nguoiThucHien),
                new SqlParameter("@id", id));

        private static PhieuKhachTra MapHeader(DataRow r) => new PhieuKhachTra
        {
            Id = Convert.ToInt32(r["Id"]),
            Nguon = (NguonXuLyBatThuong)Convert.ToInt32(r["Nguon"]),
            SoPhieu = r["SoPhieu"] as string,
            NgayPhatHanh = r["NgayPhatHanh"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayPhatHanh"]),
            SlipNo = r["SlipNo"] as string,
            TenKhachHang = r["TenKhachHang"] as string,
            BoPhanPhatHienLoi = r["BoPhanPhatHienLoi"] as string,
            XacNhanBPPhatHienLoi = r["XacNhanBPPhatHienLoi"] as string,
            XacNhanQCKhach = r["XacNhanQCKhach"] as string,
            XacNhanNhaCungCap = r["XacNhanNhaCungCap"] as string,
            NgayNhanKho = r["NgayNhanKho"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayNhanKho"]),
            TongSoLuongNhan = Convert.ToInt32(r["TongSoLuongNhan"]),
            Status = (PhieuTraHangStatus)Convert.ToInt32(r["Status"]),
            DaTaoPhieuBatThuong = Convert.ToBoolean(r["DaTaoPhieuBatThuong"]),
            DaHoanTatQTChung = Convert.ToBoolean(r["DaHoanTatQTChung"]),
            DaGiaoBu = Convert.ToBoolean(r["DaGiaoBu"]),
            Note = r["Note"] as string,
            CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
            CreatedBy = r["CreatedBy"] as string,
            UpdatedAt = r["UpdatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["UpdatedAt"]),
            UpdatedBy = r["UpdatedBy"] as string
        };

        private static PhieuKhachTraItem MapItem(DataRow r) => new PhieuKhachTraItem
        {
            Id = Convert.ToInt32(r["Id"]),
            PhieuKhachTraId = Convert.ToInt32(r["PhieuKhachTraId"]),
            MaHang = r["MaHang"] as string,
            TenHang = r["TenHang"] as string,
            LotNo = r["LotNo"] as string,
            SoLuong = Convert.ToInt32(r["SoLuong"]),
            NoiDungLoi = r["NoiDungLoi"] as string,
            DinhDanhPhieuGiao = r["DinhDanhPhieuGiao"] as string,
            PoNo = r["PoNo"] as string,
            NgayGiao = r["NgayGiao"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NgayGiao"]),
            NhaMay = r["NhaMay"] as string
        };
        // Thêm vào PhieuKhachTraRepository (implement 3 method trên)

        public void MarkHoanTat(int id, string nguoiThucHien)
            => ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTra SET Status=@st, DaHoanTatQTChung=1, UpdatedAt=GETDATE(), UpdatedBy=@by WHERE Id=@id",
                new SqlParameter("@st", (int)PhieuTraHangStatus.HoanTat),
                new SqlParameter("@by", nguoiThucHien),
                new SqlParameter("@id", id));

        public List<PhieuKhachTra> GetChoXuLyByNguon(NguonXuLyBatThuong nguon)
        {
            DataTable dt = LoadData(
                "SELECT * FROM FVN_PhieuKhachTra WHERE Nguon = @n AND DaHoanTatQTChung = 0 ORDER BY CreatedAt",
                new SqlParameter("@n", (int)nguon));
            return dt.Rows.Cast<DataRow>().Select(MapHeader).ToList();
        }

        public void UpdateItemDinhDanhPhieuGiao(
            int itemId, string dinhDanhPhieuGiao, string poNo, DateTime? ngayGiao, string nhaMay)
            => ExecuteNonQuery(
                "UPDATE FVN_PhieuKhachTraItem SET DinhDanhPhieuGiao=@ddpg, PoNo=@po, NgayGiao=@ng, NhaMay=@nm WHERE Id=@id",
                new SqlParameter("@ddpg", (object)dinhDanhPhieuGiao ?? DBNull.Value),
                new SqlParameter("@po", (object)poNo ?? DBNull.Value),
                new SqlParameter("@ng", (object)ngayGiao ?? DBNull.Value),
                new SqlParameter("@nm", (object)nhaMay ?? DBNull.Value),
                new SqlParameter("@id", itemId));
    }
}
