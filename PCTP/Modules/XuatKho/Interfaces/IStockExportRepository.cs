using PCTP.Modules.XuatKho.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Interfaces
{
    public interface IStockExportRepository
    {
        /// <summary>SLCONLAI hiện tại của LOT — dùng để validate trước khi trừ.</summary>
        int GetSlConLai(string lotNo);


        /// <summary>
        /// Trừ SLCONLAI + cộng SLXUAT trên STOCKTP theo LOT. PHẢI chạy trong
        /// transaction đã Begin() và đã LockSlotForUpdate ở tầng Slot tương ứng
        /// (đảm bảo thứ tự khoá nhất quán: Slot trước, STOCKTP sau — tránh deadlock
        /// nếu có luồng khác khoá theo thứ tự ngược lại).
        /// </summary>
        void DecreaseStockTp(string lotNo, int soLuong);
        // ★ THÊM — chỉ SLCONLAI, dùng cho luồng nội bộ (rework/chuyển kho, không phải xuất bán)
        void AdjustSlConLai(string lotNo, int delta);              // delta âm = trừ, dương = cộng
        bool TryDecreaseSlConLai(string lotNo, int soLuong);
        List<StockTpLotInfo> FindLotsWithStock(string maHang, string lotNo);
    }
}
