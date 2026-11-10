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

        /// <summary>
        /// SlotLot còn hàng của mã (và LOT nếu có; LOT rỗng = mọi LOT của mã) ở trạng thái hoạt động hoặc NG cách ly,
        /// dùng để chọn nguồn ghi nhận loại bỏ (scrap). NG cách ly xếp trước.
        /// </summary>
        List<ScrapSourceLot> FindScrapSourceLots(string itemCode, string lotNo);

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

        /// <summary>
        /// Các (Mã hàng, LOT) đã XUẤT/GIAO trong [tuNgay, denNgay] (LUUPHIEUGIAOHANG.NGAYGIAO),
        /// LOT ghép "LOT1-100,LOT2-50" đã được tách. itemCode rỗng = mọi mã.
        /// </summary>
        List<ItemLotRef> GetLotsDeliveredBetween(System.DateTime tuNgay, System.DateTime denNgay, string itemCode);

        /// <summary>
        /// Các (Mã hàng, LOT) nằm trên phiếu xử lý hàng lỗi được LẬP trong [tuNgay, denNgay]:
        /// phiếu trả hàng (FVN_PhieuTraHang.CreatedAt) hoặc phiếu xử lý bất thường
        /// (FVN_PhieuXuLyBatThuong.CreatedAt) của dòng đó. itemCode rỗng = mọi mã.
        /// </summary>
        List<ItemLotRef> GetLotsOfDefectDocsBetween(System.DateTime tuNgay, System.DateTime denNgay, string itemCode);
    }
}
