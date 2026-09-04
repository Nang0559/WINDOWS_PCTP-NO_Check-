using PCTP.Modules.XuatKho.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Interfaces
{
    public interface IStockExportService
    {
        /// <summary>
        /// TRƯỜNG HỢP 1 — BƯỚC 1: hàng có Slot riêng → pick vào "chờ giao".
        /// Trừ Slot/SlotLot, ghi StockHistory=CHO_GIAO, tạo dòng HangChoGiao.
        /// KHÔNG đụng STOCKTP.
        /// </summary>
        StockExportResult PickToChoGiao(StockExportRequest request);

        /// <summary>
        /// TRƯỜNG HỢP 1 — BƯỚC 2: xác nhận hàng đã thực sự giao (gọi từ
        /// HVN_PGH sau khi CNK). Trừ STOCKTP theo đúng SL đã pick, ghi
        /// StockHistory=EXPORT, cập nhật HangChoGiao=DaGiao. KHÔNG đụng Slot
        /// (đã trừ ở bước 1 rồi).
        /// </summary>
        StockExportResult ConfirmGiaoHangTuChoGiao(int hangChoGiaoId, string nguoiThucHien);

        /// <summary>
        /// TRƯỜNG HỢP 2 (A0 giao thẳng) và TRƯỜNG HỢP 3 (NG → rework):
        /// xuất trực tiếp — Slot/SlotLot + STOCKTP + StockHistory=EXPORT|REWORK
        /// trong CÙNG 1 transaction, không qua staging.
        /// </summary>
        StockExportResult XuatTrucTiep(StockExportRequest request);

        LotSplitResult ExportFromSlot(
           int slotId,
           int exportQty,
           string itemCode = null,
           string actionType = "EXPORT");
    }
}
