using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    public sealed class StockExportRequest
    {
        public string LotNo { get; set; }
        public string MaHang { get; set; }
        public int SoLuong { get; set; }

        public StockExportSource Source { get; set; }
        public StockTransactionType Purpose { get; set; }

        /// <summary>
        /// Bắt buộc khi Source = Slot (chỉ định rõ xuất từ slot nào).
        /// Bỏ trống khi Source = KhoAoA0 — StockExportService tự resolve
        /// qua BulkStockSlotRepository.GetOrCreateVirtualSlotId.
        /// </summary>
        public int? SlotId { get; set; }

        public StockExportReferenceType? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }

        public string LyDo { get; set; }
        public string NguoiThucHien { get; set; }
    }
}
