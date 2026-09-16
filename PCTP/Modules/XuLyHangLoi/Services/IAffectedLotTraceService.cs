using PCTP.Modules.XuLyHangLoi.Models;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Truy vết toàn bộ nguồn hàng có cùng LOT bị ảnh hưởng.
    /// Phase 2 chỉ đọc dữ liệu; không mutate stock.
    /// </summary>
    public interface IAffectedLotTraceService
    {
        AffectedLotTraceResult Trace(string maSanPham, string lotNo, string nguoiThucHien);
        AffectedLotTraceResult TraceForPhieu(PhieuXuLyBatThuong phieu, string nguoiThucHien);
    }

    public sealed class AffectedLotTraceResult
    {
        public string MaSanPham { get; set; }
        public string LotNo { get; set; }
        public bool IsComplete { get; set; }
        public List<PhieuXuLyBatThuongAffectedLot> Items { get; set; } = new List<PhieuXuLyBatThuongAffectedLot>();
        public List<string> Warnings { get; set; } = new List<string>();

        public int TotalAffectedQuantity
        {
            get
            {
                int total = 0;
                foreach (var item in Items)
                    if (item != null) total += item.SoLuongAnhHuong;
                return total;
            }
        }
    }

    public interface IProductionLotTraceProvider
    {
        IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo);
    }

    public interface ICustomerReturnLotTraceProvider
    {
        IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo);
    }
}
