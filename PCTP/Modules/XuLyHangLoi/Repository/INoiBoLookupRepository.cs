using PCTP.Modules.XuLyHangLoi.Models;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    /// <summary>Dữ liệu tra cứu cho form tạo phiếu trả nội bộ.</summary>
    public interface INoiBoLookupRepository
    {
        /// <summary>Bộ phận đang hoạt động từ B7R2_FCC.dbo.B20Dept.</summary>
        List<DeptLookupItem> GetActiveDepartments();

        /// <summary>Mã hàng (kèm tên) đang có hàng trong Slot.</summary>
        List<ItemLookupItem> GetItemsInSlots();

        /// <summary>Tên hàng theo mã (vB20Item). Null nếu mã không có trong danh mục.</summary>
        string GetItemName(string itemCode);

        /// <summary>
        /// Danh mục mã hàng đầy đủ (vB20Item), KHÔNG phụ thuộc tồn kho.
        /// Dùng cho phiếu khách trả: hàng khách trả có thể không còn trong kho.
        /// </summary>
        List<ItemLookupItem> GetItemCatalog();

        /// <summary>
        /// Tìm mã hàng theo LOT: SlotLot (còn tồn) + StockHistory (đã hết kho).
        /// Trả danh sách rỗng nếu LOT chưa từng xuất hiện.
        /// </summary>
        List<ItemLookupItem> FindItemsByLot(string lotNo);
    }
}
