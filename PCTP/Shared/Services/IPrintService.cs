using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Services
{
    /// <summary>
    /// Domain In ấn — dựng dữ liệu preview phiếu (KHÔNG ghi DB, KHÔNG trừ tồn).
    /// Dùng chung cho Xuất kho, Nhập kho, Xử lý hàng lỗi khi cần in phiếu giấy.
    /// Tách khỏi ISlotService/IStockExportService vì đây thuần là trình bày dữ liệu.
    /// </summary>
    public interface IPrintService
    {
        /// <summary>Gộp danh sách LOT thành dữ liệu in tem/phiếu (tổng SL, chuỗi LotNo/TemCode, QR gộp).</summary>
        PrintLotResult CreatePrintData(List<LotInfo> lots);

        /// <summary>
        /// Dựng preview phiếu xuất kho (1 dòng "PHIẾU XUẤT" + tuỳ chọn "PHIẾU NHẬP LẠI KHO"
        /// nếu còn phần dư). Đọc Lot mới nhất từ ISlotService — KHÔNG lưu DB.
        /// Ném InvalidOperationException nếu exportQty > tồn kho hiện tại.
        /// </summary>
        List<PXuatINModel> BuildExportPreview(
            int slotId,
            int slotNumber,
            int exportQty,
            string itemCode,
            string nguoiThucHien = "");

        /// <summary>Tên sản phẩm theo mã hàng — dùng để hiển thị trên phiếu in.</summary>
        string GetProductNameByCode(string itemCode);
    }
}
