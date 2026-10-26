using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    public sealed class StockExportItem
    {
        /// <summary>
        /// LOT thực tế được xuất.
        /// </summary>
        public string LotNo { get; set; }

        /// <summary>
        /// Mã hàng.
        /// </summary>
        public string MaHang { get; set; }

        /// <summary>
        /// Số lượng thực tế xuất.
        /// </summary>
        public int SoLuong { get; set; }

        /// <summary>
        /// ID SlotLot.
        /// Null nếu xuất trực tiếp từ KhoAoA0.
        /// </summary>
        public int? SlotId { get; set; }

        /// <summary>
        /// ID Slot vật lý.
        /// Null nếu không xuất từ Slot vật lý.
        /// </summary>
        public int? SlotVatLyId { get; set; }

        /// <summary>
        /// Nguồn xuất:
        /// Slot hoặc KhoAoA0.
        /// </summary>
        public StockExportSource Source { get; set; }

        /// <summary>
        /// Mục đích giao dịch tồn kho.
        /// Ví dụ:
        /// XuatChoGiao,
        /// XuatGiaoHang,
        /// XuatGiaoBuNG,
        /// XuatRework.
        /// </summary>
        public StockTransactionType Purpose { get; set; }

        /// <summary>
        /// ID bản ghi StockHistory tương ứng với lần xuất.
        /// </summary>
        public int? HistoryId { get; set; }
    }
}
