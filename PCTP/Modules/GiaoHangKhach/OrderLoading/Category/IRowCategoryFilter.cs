using System.Data;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.Category
{
    /// <summary>
    /// Lọc lại dữ liệu ĐÃ LOAD theo Category — chỉ cần khi 1 bảng lẫn cả MP và SP
    /// (Bảng riêng/MilkRun). IFS gốc KHÔNG dùng cái này — SQL WHERE theo giờ xuất
    /// đã tự scope đúng, không còn gì để lọc thêm.
    /// </summary>
    public interface IRowCategoryFilter
    {
        DataTable Filter(DataTable data, OrderCategory wanted, CustomerConfig cfg);
    }
}