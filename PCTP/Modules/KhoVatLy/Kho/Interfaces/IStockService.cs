using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Services
{
    public interface IStockService
    {
        // ── Nhập kho (tra cứu hỗ trợ) ────────────────────────────────
        List<string> GetAvailableSlotsForImport(string itemCode, int soLuongNhap);
        InspectionConfig GetInspectionConfig(string itemCode);
        QRCodeInfo ParseQr(string qrText);

        // ── Xuất kho ──────────────────────────────────────────────────
        LotSplitResult ExportFromSlot(int slotId, int exportQty, string itemCode = null, string actionType = "EXPORT");
        StockService.ExportMoveResult ExportAndMoveRemaining(
            int fromSlotId, string toSlotSelectedText, int exportQty,
            string itemCode = null, string actionType = "EXPORT");
        void SyncSlotFromSplitResult(Slot slot, LotSplitResult result);

        // ── Slot chung ────────────────────────────────────────────────
        
        List<LotInfo> GetSlotLots(int slotId);
        void ClearSlotTemporarily(Slot slot);

        // ── In tem / phiếu ────────────────────────────────────────────
        PrintLotResult CreatePrintData(List<LotInfo> lots);
        List<PXuatINModel> BuildExportPreview(Slot slot, int exportQty, string productName, string nguoiThucHien = "");
        string GetProductNameByCode(string itemCode);

        // ── Slot ảo / kho tạm ────────────────────────────────────────
        string GetOrCreateBulkImportSlotText();
        string GetOrCreateVirtualSlotText(string warehouseName, string rackName, int capacity = 999999999);
        ScanResult ImportSlotOnlyAfterStockTpAlreadyUpdated(
            string selectedSlotText, string lotNo, string itemCode, int quantity);
        /// <summary>Ghi nhận các LOT vừa pick (export) vào trạng thái "chờ giao" — 
        /// dùng ngay sau ExportFromSlot/ExportAndMoveRemaining khi export phục vụ giao hàng.</summary>
        void GhiNhanChoGiao(IEnumerable<LotInfo> exportedLots, int slotIdNguon, string itemCode, string phieuGiaoId);
    }

}
