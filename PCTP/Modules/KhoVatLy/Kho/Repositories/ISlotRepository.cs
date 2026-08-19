using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public interface ISlotRepository
    {
        // ============================================================
        // TÌM SLOT
        // ============================================================

        int GetSlotId(
            string warehouseName,
            string rackName,
            int slotNumber);

        // ============================================================
        // CAPACITY
        // ============================================================

        int GetCapacity(int slotId);

        int GetQuantity(int slotId);

        int GetQuantityWithLock(int slotId);

        // ============================================================
        // UPDATE SLOT
        // ============================================================

        void LockSlotForUpdate(int slotId);

        void AddQuantity(
            int slotId,
            int quantity,
            string itemCode,
            DateTime? importDate);

        void Clear(int slotId);

        void UpdateQuantityFromLots(int slotId);

        // ============================================================
        // LOT
        // ============================================================

        List<LotInfo> GetLots(int slotId);

        void SaveLots(
            int slotId,
            List<LotInfo> lots);

        bool ExistsLot(
            int slotId,
            string lotNo);

        // ============================================================
        // SLOT CHƯA CÓ LOT
        // ============================================================
        // ============================================================
        // SLOTLOT — THAO TÁC THEO SlotLotId (dùng cho rework, xuất 1 dòng cụ thể)
        // ============================================================
        /// <summary>Đọc đúng 1 dòng SlotLot theo Id. Trả null nếu không tồn tại.</summary>
        SlotLotInfo GetSlotLotById(int slotLotId);

        /// <summary>Ghi đè Quantity của đúng 1 dòng SlotLot. Không tự xoá khi = 0,
        /// không tự cập nhật header Slot — đó là việc của tầng Service.</summary>
        void UpdateSlotLotQuantity(int slotLotId, int newQuantity);

        /// <summary>Xoá hẳn 1 dòng SlotLot.</summary>
        void DeleteSlotLot(int slotLotId);
        List<SlotChuaLotInfo> GetSlotsChuaLot(
            string lot);
        List<string> GetEmptySlots(string itemCode, int soLuongNhap);
        string GetOrCreateNamedSlot(string warehouseName, string rackName, int capacity);
        // ============================================================
        // UPDATE SLOT HEADER
        // ============================================================

        void UpdateHeader(
            int slotId,
            string itemCode,
            DateTime? importDate,
            int quantity);
        /// <summary>
        /// Lấy SlotId duy nhất trong 1 rack (dùng cho rack ảo bulk-import, nơi
        /// toàn bộ kho/rack chỉ chứa đúng 1 Slot vật lý). Trả về 0 nếu không tìm thấy
        /// hoặc rack có nhiều hơn 1 slot (dữ liệu bất thường, không được đoán bừa).
        /// </summary>
        int GetSingleSlotIdInRack(string warehouseName, string rackName);
    }
}
