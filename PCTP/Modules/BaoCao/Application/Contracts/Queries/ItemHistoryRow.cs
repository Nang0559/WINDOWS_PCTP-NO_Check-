using System;
using System.Collections.Generic;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public sealed class ItemHistoryRow
    {
        // ActionType ghi vào StockHistory. Liệt kê đủ các giá trị mà code đang ghi (SaveHistory); loại nào
        // không nằm trong 3 nhóm dưới (MOVE, MOVE_INT, RESERVE, CANCEL_CHO_GIAO...) là chuyển/giữ chỗ -> "Khác".
        private static readonly HashSet<string> InTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RECEIVE", "IMPORT",
            "REWORK_OK_RECEIVE", "REWORK_NG_RECEIVE", "REWORK_NG_IMPORT", "NHAP_LAI_SAU_REWORK",
            "REWORK_CANCEL_RETURN", "RETURN_INT", "KHACH_TRA_NHAP_NG"
        };
        private static readonly HashSet<string> OutTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EXPORT", "PICK", "REWORK", "REWORK_EXPORT", "SCRAP", "CHO_GIAO",
            "EXPORT_AUTO_HVN", "EXPORT_SHORTFALL", "EXPORT_CONFIRMED_HVN"
        };
        // Điều chỉnh tồn: Quantity là số chênh lệch có dấu (+ = tăng tồn -> Nhập, - = giảm tồn -> Xuất).
        private static readonly HashSet<string> SignedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "DIEU_CHINH_TON_CNK_LOI", "CORRECT" };

        public string ItemCode { get; set; }
        public string QrCode { get; set; }
        /// <summary>Mã tem / mã phiếu (StockHistory.TemCode) - KHÁC với QR đầy đủ (QrCode).</summary>
        public string TemCode { get; set; }
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
                string type = ActionType == null ? null : ActionType.Trim();
                if (string.IsNullOrEmpty(type)) return "Khác";
                if (SignedTypes.Contains(type)) return Quantity > 0m ? "Nhập" : Quantity < 0m ? "Xuất" : "Khác";
                if (InTypes.Contains(type)) return "Nhập";
                if (OutTypes.Contains(type)) return "Xuất";
                // Loại mới chưa khai báo: nhận diện theo tiền tố để không rơi về "Khác" làm SL nhập/xuất = 0.
                if (type.StartsWith("EXPORT", StringComparison.OrdinalIgnoreCase)) return "Xuất";
                if (type.StartsWith("IMPORT", StringComparison.OrdinalIgnoreCase) ||
                    type.StartsWith("RECEIVE", StringComparison.OrdinalIgnoreCase)) return "Nhập";
                return "Khác";
            }
        }

        public decimal QuantityIn { get { return Direction == "Nhập" ? Math.Abs(Quantity) : 0m; } }
        public decimal QuantityOut { get { return Direction == "Xuất" ? Math.Abs(Quantity) : 0m; } }

        public bool LotNoFormatError { get { return CurrentStockRow.IsScientificLot(LotNo); } }
    }
}
