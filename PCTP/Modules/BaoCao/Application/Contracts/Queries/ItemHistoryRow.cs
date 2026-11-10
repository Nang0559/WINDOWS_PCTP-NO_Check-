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

        /// <summary>
        /// Nội dung diễn giải của sự kiện phiếu xử lý hàng lỗi (hướng xử lý, kết quả QC, bộ phận nhận...).
        /// Dòng StockHistory thật không có nội dung này nên để trống.
        /// </summary>
        public string Note { get; set; }

        /// <summary>Tiền tố ActionType của sự kiện phiếu (không phải giao dịch kho) do báo cáo tự bổ sung.</summary>
        public const string DefectEventPrefix = "XLBT_";

        /// <summary>
        /// true = dòng này là sự kiện của phiếu trả / phiếu xử lý bất thường (lập phiếu, truy vết LOT, QC, giao sản xuất...),
        /// KHÔNG phải một giao dịch nhập/xuất kho. Luôn có Direction = "Khác" nên không cộng vào SL nhập/xuất.
        /// </summary>
        public bool IsDefectTicketEvent
        {
            get
            {
                return ActionType != null &&
                    ActionType.Trim().StartsWith(DefectEventPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>Tên tiếng Việt của sự kiện phiếu; null nếu không phải sự kiện phiếu.</summary>
        public string DefectEventLabel
        {
            get
            {
                if (!IsDefectTicketEvent) return null;
                switch (ActionType.Trim().ToUpperInvariant())
                {
                    case "XLBT_LAP_PHIEU_TRA": return "Lập phiếu trả hàng";
                    case "XLBT_LAP_PHIEU": return "Lập phiếu xử lý bất thường";
                    case "XLBT_TRUY_VET_LOT": return "Truy vết LOT ảnh hưởng";
                    case "XLBT_DINH_HUONG": return "QC định hướng";
                    case "XLBT_QC_BAN_DAU": return "QC phân loại ban đầu";
                    case "XLBT_GIAO_SX": return "Giao hàng rework cho sản xuất";
                    case "XLBT_QC_CUOI": return "QC xác nhận cuối";
                    case "XLBT_HOAN_TAT": return "Hoàn tất xử lý";
                    case "XLBT_HUY": return "Hủy phiếu xử lý";
                    default: return ActionType.Trim();
                }
            }
        }

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
