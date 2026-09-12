using PCTP.Applications.Services;
using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// IFS gốc (HVN, 100001) — Category quyết định bởi nhãn "giờ xuất" người dùng
    /// chọn (chứa "SP6"/"SP#"), KHÔNG phải bởi cột CUA của từng dòng dữ liệu.
    /// Dùng lại nguyên PhieuService.IsLoaiSP — không viết lại logic (đang có 6 nơi
    /// khác gọi trực tiếp method đó, giữ nguyên để không phá vỡ).
    /// </summary>
    public class GioMoTaCategoryResolver : IOrderCategoryResolver
    {
        public OrderCategory Resolve(OrderLoadContext ctx)
        {
            string gioMoTa = ctx?.GioFccMoTa;
            bool isSP = !string.IsNullOrEmpty(gioMoTa)
                        && (gioMoTa.Contains("SP6") || gioMoTa.Contains("SP#"));

            return isSP ? OrderCategory.SP : OrderCategory.MP;
        }

        // O-Type không thuộc OrderCategory (MP/SP) — giữ độc lập, không qua
        // IOrderCategoryResolver. Đặt cạnh đây vì cùng đọc chung 1 nhãn "giờ mô tả".
        public static bool IsLoaiOType(string gioMoTa)
            => !string.IsNullOrEmpty(gioMoTa) && gioMoTa.Contains("O TYPE");
    }
}