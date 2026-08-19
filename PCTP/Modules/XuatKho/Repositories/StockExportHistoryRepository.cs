using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Repositories
{
    public sealed class StockExportHistoryRepository : SqlRepositoryBase, IStockExportHistoryRepository
    {
        public StockExportHistoryRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public void SaveHistory(
            string actionType, string itemCode, string lotNo, int quantity,
            int? slotId, int? fromSlotId, int? toSlotId, string qrData,
            StockExportReferenceType? referenceType, int? referenceId, string performedBy)
        {
            if (!HasTransaction)
                throw new InvalidOperationException(
                    "SaveHistory (XuatKho) phải chạy trong transaction (Uow.Begin() trước).");

            string maPhieu = StockExportReferenceKey.Build(referenceType, referenceId);

            ExecuteNonQuery(
                @"INSERT INTO StockHistory
                    (ActionType, ItemCode, TemCode, LotNo, Quantity, Date,
                     SlotId, PerformedBy, QrData, MaPhieu, FromSlotId, ToSlotId)
                  VALUES
                    (@actionType, @itemCode, NULL, @lotNo, @quantity, GETDATE(),
                     @slotId, @performedBy, @qrData, @maPhieu, @fromSlotId, @toSlotId)",
                new SqlParameter("@actionType", actionType),
                new SqlParameter("@itemCode", (object)itemCode ?? DBNull.Value),
                new SqlParameter("@lotNo", (object)lotNo ?? DBNull.Value),
                new SqlParameter("@quantity", quantity),
                new SqlParameter("@slotId", (object)slotId ?? DBNull.Value),
                new SqlParameter("@performedBy",
                    (object)(string.IsNullOrEmpty(performedBy) ? Environment.UserName : performedBy)),
                new SqlParameter("@qrData", (object)qrData ?? DBNull.Value),
                new SqlParameter("@maPhieu", (object)maPhieu ?? DBNull.Value),
                new SqlParameter("@fromSlotId", (object)fromSlotId ?? DBNull.Value),
                new SqlParameter("@toSlotId", (object)toSlotId ?? DBNull.Value));
        }

        public bool ExistsHistoryForReference(
            string actionType, StockExportReferenceType referenceType, int referenceId)
        {
            string maPhieu = StockExportReferenceKey.Build(referenceType, referenceId);

            object kq = ExecuteScalar(
                "SELECT COUNT(*) FROM StockHistory WHERE ActionType = @at AND MaPhieu = @mp",
                new SqlParameter("@at", actionType),
                new SqlParameter("@mp", maPhieu));

            return kq != null && kq != DBNull.Value && Convert.ToInt32(kq) > 0;
        }
    }
}
