using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// Quyết định OrderCategory (MP/SP) cho CẢ PHIÊN load — không phải từng dòng.
    /// IFS gốc: đọc nhãn giờ xuất đã chọn (xem GioMoTaCategoryResolver).
    /// Bảng riêng/GiaoDB: đọc thẳng ctx.Category (đã set từ toggle UI ở Presenter),
    /// không cần resolver riêng cho 2 luồng đó.
    /// </summary>
    public interface IOrderCategoryResolver
    {
        OrderCategory Resolve(OrderLoadContext ctx);
    }
}