using PCTP.Shared.Enums;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    /// <summary>
    /// Kết quả chuẩn của một lần load đơn hàng.
    /// Chỉ chứa dữ liệu nghiệp vụ, không chứa EventBus/UI state.
    /// </summary>
    public class OrderLoadResult
    {
        public DataTable Orders { get; set; }

        public bool HasMaNG { get; set; }

        public bool HasDifference { get; set; }

        public OrderSourceKind Source { get; set; }

        public OrderCategory Category { get; set; }

        public string Caption { get; set; }

        public string Warning { get; set; }

        public bool IsQr { get; set; }

        public static OrderLoadResult Empty(OrderLoadContext context)
        {
            return new OrderLoadResult
            {
                Orders = new DataTable(),
                HasMaNG = false,
                HasDifference = false,
                Source = context != null ? context.Source : OrderSourceKind.IFS,
                Category = context != null ? context.Category : OrderCategory.MP,
                Caption = string.Empty,
                Warning = null,
                IsQr = context != null && context.IsBanQR
            };
        }
    }
}
