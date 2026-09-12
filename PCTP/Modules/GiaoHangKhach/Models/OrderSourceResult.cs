using PCTP.Modules.GiaoHangKhach.Mode;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Models
{
    /// <summary>
    /// Kết quả load từ 1 IOrderSource — CHỈ chứa dữ liệu, không có state QR/TMP
    /// (đó là Phase 4, tách riêng), không publish EventBus (mục 26 "Không 3").
    /// </summary>
    public class OrderSourceResult
    {
        /// <summary>Đơn hàng gốc, đã merge LOT đã lưu (LUUPHIEUGIAOHANG) nếu có.</summary>
        public DataTable Orders { get; set; }

        /// <summary>Kết quả so sánh với IFS baseline — rỗng nếu Source không cần so sánh (IFS gốc, GiaoDB).</summary>
        public DataTable Difference { get; set; }

        public OrderSourceKind SourceKind { get; set; }
    }
}