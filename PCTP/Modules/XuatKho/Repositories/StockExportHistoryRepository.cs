using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
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
    public sealed class StockExportHistoryRepository
     : SqlRepositoryBase, IStockExportHistoryRepository
    {
        private readonly IStockHistoryRepository _coreHistory; // gọi xuống Kho Core, không tự viết SQL insert nữa

        public StockExportHistoryRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            IStockHistoryRepository coreHistory)
            : base(db, uow)
        {
            _coreHistory = coreHistory
                ?? throw new ArgumentNullException(nameof(coreHistory));
        }

        public void SaveHistory(
            string actionType, string itemCode, string lotNo, int quantity,
            int? slotId, int? fromSlotId, int? toSlotId, string qrData,
            StockExportReferenceType? referenceType, int? referenceId, string performedBy)
        {
            string maPhieu = StockExportReferenceFormatter.Format(referenceType, referenceId);
            _coreHistory.SaveHistory(
                actionType, itemCode,
                new LotInfo
                {
                    LotNo = lotNo,
                    Quantity = quantity,
                    MaPhieuKho = maPhieu,   // ⚠ vẫn cần xác nhận tên field đúng trên LotInfo
                    RawQr = qrData
                },
                fromSlotId, toSlotId, performedBy);
            // ⚠ slotId (khác fromSlotId/toSlotId) chưa có chỗ ghi — vẫn cần quyết định
        }

        public bool ExistsHistoryForReference(
            string actionType, StockExportReferenceType referenceType, int referenceId)
        {
            string maPhieu = StockExportReferenceKey.Build(referenceType, referenceId);
            object kq = ExecuteScalar(   // ✅ giờ gọi được trực tiếp vì đã kế thừa SqlRepositoryBase
                "SELECT COUNT(*) FROM StockHistory WHERE ActionType = @at AND MaPhieu = @mp",
                new SqlParameter("@at", actionType),
                new SqlParameter("@mp", maPhieu));
            return kq != null && kq != DBNull.Value && Convert.ToInt32(kq) > 0;
        }
    }
}
