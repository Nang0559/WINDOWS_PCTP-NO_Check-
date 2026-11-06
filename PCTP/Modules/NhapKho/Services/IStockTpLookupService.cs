
using PCTP.Shared.Models;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.NhapKho.Services
{
    /// <summary>
    /// Đọc thông tin STOCKTP theo LOT — dùng để đối chiếu hiển thị trên UI.
    /// Contract này CHỈ đọc; mọi mutation tồn kho phải đi qua IStockMovementService.
    /// </summary>
    public interface IStockTpLookupService
    {
        StockItem GetByLot(string lotNo);
        DataTable GetItemLookup();
        DataTable GetTonKhoHienTai();
        DataTable GetTonKhoTheoLot(List<string> lots);

        /// <summary>
        /// Cổng tra cứu tồn kho dùng chung cho UI: tự nhận diện QR, LOT hoặc mã hàng.
        /// Chỉ đọc dữ liệu; không thực hiện cập nhật tồn kho.
        /// </summary>
        DataTable SearchStock(string input);
    }
}
