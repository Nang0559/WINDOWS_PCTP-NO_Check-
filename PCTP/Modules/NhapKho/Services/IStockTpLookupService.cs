using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Services
{
    /// <summary>
    /// Đọc thông tin STOCKTP theo LOT — dùng để đối chiếu hiển thị trên UI
    /// (vd SlotDetailPanel). CHỈ đọc, không có quyền ghi — khác với
    /// IStockTpRepository vốn có cả Insert/Update/XuatKhoThat.
    /// </summary>
    public interface IStockTpLookupService
    {
        StockItem GetByLot(string lotNo);
    }
}
