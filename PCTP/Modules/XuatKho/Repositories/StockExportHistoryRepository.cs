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
    public sealed class StockExportHistoryRepository : IStockExportHistoryRepository
    {
        private readonly IStockHistoryRepository _coreHistory;   // ← gọi xuống Kho Core, không tự viết SQL nữa
        private readonly SqlRepositoryBase _base;                 // chỉ giữ lại cho ExistsHistoryForReference

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
                    MaPhieuKho = maPhieu,   // ⚠ xác nhận tên field đúng — xem Lỗi 5
                    RawQr = qrData
                },
                fromSlotId, toSlotId, performedBy);

            // ⚠ Cột "SlotId" riêng của bản Xuất Kho (khác fromSlotId/toSlotId) KHÔNG
            // còn chỗ ghi khi hợp nhất qua IStockHistoryRepository — cần xác nhận có
            // ai đang query cột này không, nếu có phải thêm slotId vào chữ ký
            // IStockHistoryRepository.SaveHistory (ảnh hưởng mọi caller), hoặc bỏ hẳn.
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
