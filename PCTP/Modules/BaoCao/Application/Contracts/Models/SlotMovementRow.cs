using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Models
{
    /// <summary>
    /// 1 dòng lịch sử vào/ra slot của 1 LOT, đọc từ bảng StockHistory —
    /// dùng cho màn hình tra cứu giao hàng, không liên quan StockMovementService (ghi).
    /// </summary>
    public sealed class SlotMovementRow
    {
        public string ActionType { get; set; }
        public string ItemCode { get; set; }
        public string LotNo { get; set; }
        public int Quantity { get; set; }
        public DateTime Date { get; set; }

        public int? FromSlotId { get; set; }
        public string FromWarehouse { get; set; }
        public string FromRack { get; set; }
        public int? FromSlotNumber { get; set; }

        public int? ToSlotId { get; set; }
        public string ToWarehouse { get; set; }
        public string ToRack { get; set; }
        public int? ToSlotNumber { get; set; }

        public string PerformedBy { get; set; }
    }

}
