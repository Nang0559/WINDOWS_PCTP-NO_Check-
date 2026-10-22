using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    /// <summary>
    /// Transaction-aware facade for PhieuTraHangRepository.
    ///
    /// The legacy repository uses PhieuSqlExecutor directly for several header
    /// reads/writes. When QT Chung already owns an active UnitOfWork transaction,
    /// those calls use another SqlConnection and can wait on locks held by the
    /// very transaction performing QC confirmation. This facade routes the
    /// transaction-sensitive operations through SqlRepositoryBase/Uow while
    /// delegating the remaining repository surface unchanged.
    /// </summary>
    public sealed class TransactionalPhieuTraHangRepository : SqlRepositoryBase, IPhieuTraHangRepository
    {
        private readonly IPhieuTraHangRepository _inner;

        public TransactionalPhieuTraHangRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            IPhieuTraHangRepository inner)
            : base(db, uow)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public PhieuTraHangStatus? GetStatus(int id)
            => GetStatus<PhieuTraHangStatus, int>("FVN_PhieuTraHang", id);

        public bool UpdateStatusIfCurrentIs(
            int id,
            PhieuTraHangStatus expectedFrom,
            PhieuTraHangStatus newStatus,
            string nguoiThucHien)
            => UpdateStatusIfCurrentIs(
                "FVN_PhieuTraHang",
                id,
                expectedFrom,
                newStatus,
                nguoiThucHien);

        public int Insert(PhieuTraHang e) => _inner.Insert(e);

        public PhieuTraHang GetById(int id)
        {
            if (!HasTransaction)
                return _inner.GetById(id);

            DataTable table = LoadData(@"
SELECT *
FROM FVN_PhieuTraHang
WHERE Id = @Id;", new SqlParameter("@Id", id));

            if (table.Rows.Count == 0)
                return null;

            return MapHeader(table.Rows[0]);
        }

        public PhieuTraHang GetBySoPhieu(string soPhieu)
            => _inner.GetBySoPhieu(soPhieu);

        public List<PhieuTraHang> GetByNguon(NguonXuLyBatThuong nguon)
            => _inner.GetByNguon(nguon);

        public List<PhieuTraHang> GetChoXuLy()
            => _inner.GetChoXuLy();

        public List<PhieuTraHang> GetChoXuLyByNguon(NguonXuLyBatThuong nguon)
            => _inner.GetChoXuLyByNguon(nguon);

        public void Update(PhieuTraHang e) => _inner.Update(e);

        public void UpdateStatus(
            int id,
            PhieuTraHangStatus status,
            string nguoiThucHien)
        {
            if (!HasTransaction)
            {
                _inner.UpdateStatus(id, status, nguoiThucHien);
                return;
            }

            int affected = ExecuteNonQuery(@"
UPDATE FVN_PhieuTraHang
SET Status = @Status,
    UpdatedAt = GETDATE(),
    UpdatedBy = @UpdatedBy
WHERE Id = @Id;",
                new SqlParameter("@Id", id),
                new SqlParameter("@Status", (int)status),
                new SqlParameter("@UpdatedBy", DbValueHelper.DbValue(nguoiThucHien)));

            if (affected == 0)
                throw new InvalidOperationException(
                    "Không tìm thấy PhieuTraHang để cập nhật Status. Id=" + id);
        }

        public void UpdateNote(int id, string note, string nguoiThucHien)
            => _inner.UpdateNote(id, note, nguoiThucHien);

        public int InsertItem(PhieuTraHangCT item)
            => _inner.InsertItem(item);

        public void InsertItems(int phieuTraHangId, IEnumerable<PhieuTraHangCT> items)
            => _inner.InsertItems(phieuTraHangId, items);

        public List<PhieuTraHangCT> GetItems(int phieuTraHangId)
            => _inner.GetItems(phieuTraHangId);

        public PhieuTraHangCT GetItemById(int itemId)
            => _inner.GetItemById(itemId);

        public void UpdateThongTinGiaoLaiBoPhan(
            int phieuTraHangId,
            string boPhanNhan,
            int soLuongGiaoLai,
            DateTime ngayGiaoLai,
            string nguoiThucHien)
            => _inner.UpdateThongTinGiaoLaiBoPhan(
                phieuTraHangId,
                boPhanNhan,
                soLuongGiaoLai,
                ngayGiaoLai,
                nguoiThucHien);

        public List<PhieuTraHangCT> GetItemsChuaXacDinhPhieuGiao(int phieuTraHangId)
            => _inner.GetItemsChuaXacDinhPhieuGiao(phieuTraHangId);

        public void UpdateItemDinhDanhPhieuGiao(
            int itemId,
            string dinhDanhPhieuGiao,
            string poNo,
            DateTime? ngayGiao,
            string nhaMay)
            => _inner.UpdateItemDinhDanhPhieuGiao(
                itemId,
                dinhDanhPhieuGiao,
                poNo,
                ngayGiao,
                nhaMay);

        public bool ConChoXuLy(int phieuTraHangId)
        {
            if (!HasTransaction)
                return _inner.ConChoXuLy(phieuTraHangId);

            object value = ExecuteScalar(@"
SELECT TOP 1 1
FROM FVN_PhieuXuLyBatThuong
WHERE PhieuTraHangId = @PhieuTraHangId
  AND Status NOT IN (@HoanTat, @Huy);",
                new SqlParameter("@PhieuTraHangId", phieuTraHangId),
                new SqlParameter("@HoanTat", (int)QTChungStatus.HoanTat),
                new SqlParameter("@Huy", (int)QTChungStatus.Huy));

            return value != null && value != DBNull.Value;
        }

        public bool CoPhieuXuLy(int phieuTraHangId)
            => _inner.CoPhieuXuLy(phieuTraHangId);

        public List<int> GetItemIdsChuaCoPhieuXuLy(int phieuTraHangId)
            => _inner.GetItemIdsChuaCoPhieuXuLy(phieuTraHangId);

        public void DeleteItems(int phieuTraHangId)
            => _inner.DeleteItems(phieuTraHangId);

        public void Delete(int id)
            => _inner.Delete(id);

        private static PhieuTraHang MapHeader(DataRow row)
        {
            return new PhieuTraHang
            {
                Id = ToInt(row["Id"]),
                Nguon = (NguonXuLyBatThuong)ToInt(row["Nguon"]),
                SoPhieu = ToStringValue(row["SoPhieu"]),
                CustomerNo = ToStringValue(row["CustomerNo"]),
                SoPhieuKhach = ToStringValue(row["SoPhieuKhach"]),
                NgayPhatHanh = ToNullableDateTime(row["NgayPhatHanh"]),
                SlipNo = ToStringValue(row["SlipNo"]),
                Ca = ToStringValue(row["Ca"]),
                PhongBan = ToStringValue(row["PhongBan"]),
                LyDo = ToStringValue(row["LyDo"]),
                TenKhachHang = ToStringValue(row["TenKhachHang"]),
                BoPhanPhatHienLoi = ToStringValue(row["BoPhanPhatHienLoi"]),
                XacNhanBPPhatHienLoi = ToStringValue(row["XacNhanBPPhatHienLoi"]),
                XacNhanQCKhach = ToStringValue(row["XacNhanQCKhach"]),
                XacNhanNhaCungCap = ToStringValue(row["XacNhanNhaCungCap"]),
                NgayNhanKho = ToNullableDateTime(row["NgayNhanKho"]),
                TongSoLuongNhan = ToInt(row["TongSoLuongNhan"]),
                Status = (PhieuTraHangStatus)ToInt(row["Status"]),
                BoPhanNhanLai = ToStringValue(row["BoPhanNhanLai"]),
                SoLuongGiaoLai = ToNullableInt(row["SoLuongGiaoLai"]),
                NgayGiaoLaiBoPhan = ToNullableDateTime(row["NgayGiaoLaiBoPhan"]),
                NguoiGiaoLaiBoPhan = ToStringValue(row["NguoiGiaoLaiBoPhan"]),
                Note = ToStringValue(row["Note"]),
                CreatedAt = ToDateTime(row["CreatedAt"]),
                CreatedBy = ToStringValue(row["CreatedBy"]),
                UpdatedAt = ToNullableDateTime(row["UpdatedAt"]),
                UpdatedBy = ToStringValue(row["UpdatedBy"])
            };
        }

        private static int ToInt(object value)
            => value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

        private static int? ToNullableInt(object value)
            => value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);

        private static DateTime ToDateTime(object value)
            => value == null || value == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(value);

        private static DateTime? ToNullableDateTime(object value)
            => value == null || value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value);

        private static string ToStringValue(object value)
            => value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }
}
