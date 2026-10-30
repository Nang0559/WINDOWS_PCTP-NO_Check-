using System;
using System.Collections.Generic;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class ItemHistoryRow
    {
        // ActionType ghi vào StockHistory (xem StockMovementRequest.Types). Sửa tại đây nếu nghiệp vụ đổi.
        private static readonly HashSet<string> InTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "RECEIVE", "IMPORT", "REWORK_OK_RECEIVE", "REWORK_CANCEL_RETURN" };
        private static readonly HashSet<string> OutTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "EXPORT", "PICK", "REWORK_EXPORT" };

        public string ItemCode { get; set; }
        public string QrCode { get; set; }
        public string LotNo { get; set; }
        public string ActionType { get; set; }
        public decimal Quantity { get; set; }
        public DateTime? EventTime { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }
        public string DocumentNo { get; set; }
        public string UserName { get; set; }

        public string Direction
        {
            get
            {
                if (ActionType != null && InTypes.Contains(ActionType.Trim())) return "Nhập";
                if (ActionType != null && OutTypes.Contains(ActionType.Trim())) return "Xuất";
                return "Khác";
            }
        }

        public decimal QuantityIn { get { return Direction == "Nhập" ? Quantity : 0m; } }
        public decimal QuantityOut { get { return Direction == "Xuất" ? Quantity : 0m; } }

        public bool LotNoFormatError { get { return CurrentStockRow.IsScientificLot(LotNo); } }
    }
}
