using PCTP.Modules.XuLyHangLoi.Models;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Truy vết toàn bộ nguồn hàng có cùng LOT bị ảnh hưởng.
    /// Phase 2: đọc -> kiểm tra completeness -> snapshot bền vững.
    /// </summary>
    public interface IAffectedLotTraceService
    {
        AffectedLotTraceResult Trace(string maSanPham, string lotNo, string nguoiThucHien);
        AffectedLotTraceResult TraceForPhieu(PhieuXuLyBatThuong phieu, string nguoiThucHien);

        /// <summary>
        /// Truy vết theo phiếu, snapshot AffectedLots và trả về tổng số lượng ảnh hưởng.
        /// Không cho snapshot một kết quả chưa complete để tránh QC kết luận thiếu nguồn.
        /// </summary>
        AffectedLotTraceResult TruyVetLOT(int phieuXuLyId, string nguoiThucHien);

        /// <summary>
        /// Đọc snapshot đã lưu của một phiếu.
        /// </summary>
        IReadOnlyList<PhieuXuLyBatThuongAffectedLot> GetSnapshot(int phieuXuLyId);
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
