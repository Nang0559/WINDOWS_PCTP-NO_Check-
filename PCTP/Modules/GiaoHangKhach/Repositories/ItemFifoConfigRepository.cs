using PCTP.Modules.GiaoHangKhach.Intefaces;
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
    public sealed class ItemFifoConfigRepository
     : SqlRepositoryBase, IItemFifoConfigRepository
    {
        public ItemFifoConfigRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public DataTable GetAll()
            => LoadData(@"
            SELECT c.ItemCode, ISNULL(b.Name,'') AS TenHang, c.EnforceFifo,
                   c.UpdatedAt, c.UpdatedBy
            FROM FVN_ItemFifoConfig c
            LEFT JOIN B20Item b ON b.Code = c.ItemCode
            ORDER BY c.ItemCode");

        public bool GetEnforceFifo(string itemCode)
        {
            object raw = ExecuteScalar(
                "SELECT EnforceFifo FROM FVN_ItemFifoConfig WHERE ItemCode = @ma",
                new SqlParameter("@ma", itemCode));
            return raw != null && raw != DBNull.Value && Convert.ToBoolean(raw);
        }

        // ★ SỬA — thêm nguoiThucHien để ghi UpdatedBy + audit lịch sử.
        // Toàn bộ Upsert + ghi lịch sử gói trong 1 transaction để đảm bảo
        // atomic (không xảy ra trường hợp cập nhật thành công nhưng mất
        // dấu lịch sử nếu có lỗi giữa 2 câu lệnh).
        public void Upsert(string itemCode, bool enforceFifo, string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                object rawCu = ExecuteScalar(
                    "SELECT EnforceFifo FROM FVN_ItemFifoConfig WHERE ItemCode = @ma",
                    new SqlParameter("@ma", itemCode.Trim()));
                bool? enforceFifoCu = rawCu == null || rawCu == DBNull.Value
                    ? (bool?)null
                    : Convert.ToBoolean(rawCu);

                // Không có gì thay đổi thật sự — không ghi lịch sử, không update
                if (enforceFifoCu.HasValue && enforceFifoCu.Value == enforceFifo)
                {
                    if (ownTransaction) Uow.Commit();
                    return;
                }

                ExecuteNonQuery(@"
                MERGE FVN_ItemFifoConfig AS t
                USING (SELECT @ma AS ItemCode) AS s ON t.ItemCode = s.ItemCode
                WHEN MATCHED THEN
                    UPDATE SET EnforceFifo = @ef, UpdatedAt = GETDATE(), UpdatedBy = @nguoi
                WHEN NOT MATCHED THEN
                    INSERT (ItemCode, EnforceFifo, UpdatedAt, UpdatedBy)
                    VALUES (@ma, @ef, GETDATE(), @nguoi);",
                    new SqlParameter("@ma", itemCode.Trim()),
                    new SqlParameter("@ef", enforceFifo),
                    new SqlParameter("@nguoi", (object)nguoiThucHien ?? DBNull.Value));

                ExecuteNonQuery(@"
                INSERT INTO FVN_ItemFifoConfig_History
                    (ItemCode, EnforceFifoCu, EnforceFifoMoi, NguoiThucHien)
                VALUES (@ma, @efCu, @ef, @nguoi);",
                    new SqlParameter("@ma", itemCode.Trim()),
                    new SqlParameter("@efCu", (object)enforceFifoCu ?? DBNull.Value),
                    new SqlParameter("@ef", enforceFifo),
                    new SqlParameter("@nguoi", (object)nguoiThucHien ?? DBNull.Value));

                if (ownTransaction) Uow.Commit();
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        public void Delete(string itemCode, string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                object rawCu = ExecuteScalar(
                    "SELECT EnforceFifo FROM FVN_ItemFifoConfig WHERE ItemCode = @ma",
                    new SqlParameter("@ma", itemCode.Trim()));

                if (rawCu == null || rawCu == DBNull.Value)
                {
                    // Không tồn tại — không có gì để xóa/ghi lịch sử
                    if (ownTransaction) Uow.Commit();
                    return;
                }

                bool enforceFifoCu = Convert.ToBoolean(rawCu);

                ExecuteNonQuery(
                    "DELETE FROM FVN_ItemFifoConfig WHERE ItemCode = @ma",
                    new SqlParameter("@ma", itemCode.Trim()));

                // Ghi lịch sử việc xóa: EnforceFifoMoi = false coi như "tắt hẳn cấu hình"
                ExecuteNonQuery(@"
                INSERT INTO FVN_ItemFifoConfig_History
                    (ItemCode, EnforceFifoCu, EnforceFifoMoi, NguoiThucHien)
                VALUES (@ma, @efCu, 0, @nguoi);",
                    new SqlParameter("@ma", itemCode.Trim()),
                    new SqlParameter("@efCu", enforceFifoCu),
                    new SqlParameter("@nguoi", (object)nguoiThucHien ?? DBNull.Value));

                if (ownTransaction) Uow.Commit();
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        // ★ MỚI — tra cứu lịch sử cho 1 mã hàng, dùng cho UI xem "ai đã đổi gì, khi nào"
        public DataTable GetHistory(string itemCode)
            => LoadData(
                "SELECT ItemCode, EnforceFifoCu, EnforceFifoMoi, ThoiGian, NguoiThucHien " +
                "FROM FVN_ItemFifoConfig_History WHERE ItemCode = @ma ORDER BY ThoiGian DESC",
                new SqlParameter("@ma", itemCode));
        public DataTable GetDanhSachMaHangKhaDung()
       => 
            LoadData(
                "SELECT Code, Name FROM B20Item WHERE LEN(Code) > 0 ORDER BY Code");
        
    }
}
