using PCTP.Modules.XuatKho.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Interfaces
{
    public interface IStockExportHistoryRepository
    {
        /// <summary>
        /// Ghi 1 dòng StockHistory. PHẢI chạy trong transaction đang mở.
        /// </summary>
        void SaveHistory(
            string actionType,
            string itemCode,
            string lotNo,
            int quantity,
            int? slotId,
            int? fromSlotId,
            int? toSlotId,
            string qrData,
            StockExportReferenceType? referenceType,
            int? referenceId,
            string performedBy);

        /// <summary>
        /// Kiểm tra chứng từ tham chiếu đã từng ghi 1 dòng history với
        /// actionType này chưa — dùng để chống ghi trùng (double-confirm).
        /// </summary>
        bool ExistsHistoryForReference(
            string actionType,
            StockExportReferenceType referenceType,
            int referenceId);
    }
}
